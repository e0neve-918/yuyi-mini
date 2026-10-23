// YUYI MINI — "grabbed by the scruff and dangling" interaction.
// Modified version of MateEngine (© 2025 Johnson Jason / Shiny), MateEngine Pro License v2.1.
//
// While the avatar is held (press longer than a click, or moved), this layers on top of
// MateEngine's normal drag animation:
//   • the cursor becomes a grabbing hand
//   • the whole body swings like a pendulum hanging from the NECK
//   • arms and legs droop toward the ground, legs kick a little (대롱대롱)
//   • an optional facial expression
// It runs after the Animator and before VRM spring bones, so hair/ears react to the swing.
// If you add a bool "isScruffHeld" to the animator controller, it is set too, so a custom
// hanging animation clip can be used later.

using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public class YuyiScruffGrab : MonoBehaviour
{
    [Header("Grab detection")]
    public float holdDelay = 0.16f;       // seconds before a press counts as a grab
    public float moveThreshold = 8f;      // or this many pixels of cursor movement

    [Header("Pendulum (swing from the neck)")]
    public float velocityToAngle = 0.05f; // degrees per (pixel/second)
    public float maxSwing = 32f;
    public float maxPitch = 12f;
    public float frequency = 1.7f;        // Hz
    public float damping = 0.16f;         // lower = swings longer
    public float idleSway = 2.5f;         // gentle sway while held still
    public float blendIn = 0.15f, blendOut = 0.25f;

    [Header("Limbs")]
    [Range(0f, 1f)] public float armDroop = 0.55f;
    [Range(0f, 1f)] public float legDroop = 0.45f;
    public float kickAngle = 14f;
    public float kickSpeed = 2.4f;        // Hz

    [Header("Cursor")]
    public Vector2 cursorHotspot = new Vector2(28, 46); // pinch point of cursor_grab.png (64x64)
    public CursorMode cursorMode = CursorMode.Auto;

    public const string AnimatorParam = "isScruffHeld";

    AvatarAnimatorController controller;
    Animator animator;

    bool pressed, grabbed;
    float pressStart;
    Vector2 pressPos, lastCursor, smoothedVel;

    float swing, swingVel, pitch, pitchVel;
    float weight, kickTime;

    static Texture2D grabCursor;
    static bool cursorLoaded;
    bool cursorApplied;

    Transform hips, neck, head;
    Transform lUpperArm, lLowerArm, rUpperArm, rLowerArm;
    Transform lUpperLeg, lLowerLeg, rUpperLeg, rLowerLeg;
    Animator cachedFor;

    public bool IsGrabbed => grabbed;
    /// <summary>True if the current/last press turned into a grab (so it isn't a click).</summary>
    public bool GrabbedThisPress { get; private set; }

    void Awake()
    {
        controller = GetComponent<AvatarAnimatorController>();
        lastCursor = YuyiAvatarUtil.GlobalCursor();
    }

    void Update()
    {
        var profile = YuyiGiftProfile.Current;
        if (!profile.scruffEnabled) { Release(); return; }

        animator = controller != null && controller.animator != null ? controller.animator : GetComponentInChildren<Animator>();
        CacheBones();

        float dt = Mathf.Clamp(Time.unscaledDeltaTime, 1e-4f, 0.25f);
        Vector2 cursor = YuyiAvatarUtil.GlobalCursor();
        Vector2 vel = Vector2.ClampMagnitude((cursor - lastCursor) / dt, 5000f); // ignore window snaps/teleports
        lastCursor = cursor;
        smoothedVel = Vector2.Lerp(smoothedVel, vel, 1f - Mathf.Exp(-14f * dt));

        // --- press / grab state ---
        if (Input.GetMouseButtonDown(0) && !YuyiAvatarUtil.ShouldIgnorePointer(controller))
        {
            pressed = true;
            GrabbedThisPress = false;
            pressStart = Time.unscaledTime;
            pressPos = cursor;
        }
        if (!Input.GetMouseButton(0) || (controller != null && controller.BlockDraggingOverride))
        {
            if (pressed || grabbed) Release();
        }
        else if (pressed && !grabbed)
        {
            if (Time.unscaledTime - pressStart >= holdDelay || Vector2.Distance(cursor, pressPos) > moveThreshold)
                BeginGrab();
        }

        // --- pendulum ---
        float strength = Mathf.Max(0f, profile.swingStrength);
        float targetSwing = 0f, targetPitch = 0f;
        if (grabbed)
        {
            // body lags behind the hand: moving right → feet trail to the left
            targetSwing = Mathf.Clamp(-smoothedVel.x * velocityToAngle * strength, -maxSwing, maxSwing);
            targetPitch = Mathf.Clamp(smoothedVel.y * velocityToAngle * 0.4f * strength, -maxPitch, maxPitch);
            targetSwing += Mathf.Sin(Time.unscaledTime * 1.3f) * idleSway;
        }
        // sub-step so the spring stays stable at low FPS limits
        for (float left = dt; left > 0f; left -= 0.02f)
        {
            float step = Mathf.Min(left, 0.02f);
            Spring(ref swing, ref swingVel, targetSwing, frequency, damping, step);
            Spring(ref pitch, ref pitchVel, targetPitch, frequency * 1.2f, damping * 1.5f, step);
        }
        swing = Mathf.Clamp(swing, -maxSwing * 1.4f, maxSwing * 1.4f);
        pitch = Mathf.Clamp(pitch, -maxPitch * 1.4f, maxPitch * 1.4f);

        float blendTime = grabbed ? blendIn : blendOut;
        weight = Mathf.MoveTowards(weight, grabbed ? 1f : 0f, dt / Mathf.Max(0.01f, blendTime));
        if (grabbed) kickTime += dt;

        if (grabbed) YuyiAvatarUtil.SetExpression(animator != null ? animator.gameObject : null, profile.scruffExpression, weight);
    }

    void LateUpdate()
    {
        if (weight <= 0.001f || animator == null || hips == null || neck == null) return;
        var cam = YuyiAvatarUtil.FindCamera();
        if (cam == null) return;

        Vector3 pivot = neck.position;
        Vector3 down = -cam.transform.up;

        // 1) whole body swings around the neck (rotate hips about the neck point)
        Quaternion q = Quaternion.AngleAxis(swing * weight, cam.transform.forward)
                     * Quaternion.AngleAxis(pitch * weight, cam.transform.right);
        hips.position = pivot + q * (hips.position - pivot);
        hips.rotation = q * hips.rotation;

        // keep the head roughly where it was so it looks "held"
        if (head != null)
            head.rotation = Quaternion.Slerp(head.rotation, Quaternion.Inverse(q) * head.rotation, 0.6f * weight);

        // 2) limbs droop toward the ground
        Droop(lUpperArm, lLowerArm, down + cam.transform.right * -0.15f, armDroop * weight);
        Droop(rUpperArm, rLowerArm, down + cam.transform.right * 0.15f, armDroop * weight);

        float kick = Mathf.Max(0f, YuyiGiftProfile.Current.legKick) * kickAngle * weight;
        float phase = kickTime * kickSpeed * Mathf.PI * 2f;
        Droop(lUpperLeg, lLowerLeg, down, legDroop * weight);
        Droop(rUpperLeg, rLowerLeg, down, legDroop * weight);
        if (kick > 0.01f)
        {
            if (lUpperLeg) lUpperLeg.rotation = Quaternion.AngleAxis(Mathf.Sin(phase) * kick, cam.transform.right) * lUpperLeg.rotation;
            if (rUpperLeg) rUpperLeg.rotation = Quaternion.AngleAxis(Mathf.Sin(phase + Mathf.PI) * kick, cam.transform.right) * rUpperLeg.rotation;
            if (lLowerLeg) lLowerLeg.rotation = Quaternion.AngleAxis(Mathf.Max(0f, Mathf.Sin(phase)) * kick * 1.2f, cam.transform.right) * lLowerLeg.rotation;
            if (rLowerLeg) rLowerLeg.rotation = Quaternion.AngleAxis(Mathf.Max(0f, Mathf.Sin(phase + Mathf.PI)) * kick * 1.2f, cam.transform.right) * rLowerLeg.rotation;
        }
    }

    // ----------------------------------------------------------------

    void BeginGrab()
    {
        grabbed = true;
        GrabbedThisPress = true;
        kickTime = 0f;
        // a little "yoink" swing when picked up
        swingVel += Random.Range(-60f, 60f);
        YuyiAvatarUtil.TrySetAnimatorBool(animator, AnimatorParam, true);
        ApplyCursor(true);
    }

    void Release()
    {
        bool wasGrabbed = grabbed;
        pressed = false;
        grabbed = false;
        if (wasGrabbed)
        {
            YuyiAvatarUtil.TrySetAnimatorBool(animator, AnimatorParam, false);
            YuyiAvatarUtil.SetExpression(animator != null ? animator.gameObject : null, YuyiGiftProfile.Current.scruffExpression, 0f);
        }
        ApplyCursor(false);
    }

    void ApplyCursor(bool on)
    {
        if (!YuyiGiftProfile.Current.useGrabCursor) on = false;
        if (on == cursorApplied) return;

        if (on)
        {
            if (!cursorLoaded) { grabCursor = YuyiGiftProfile.LoadPng("cursor_grab.png"); cursorLoaded = true; }
            if (grabCursor == null) return;
            Cursor.SetCursor(grabCursor, cursorHotspot, cursorMode);
        }
        else
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
        cursorApplied = on;
    }

    void OnDisable()
    {
        Release();
        weight = 0f;
    }

    void CacheBones()
    {
        if (animator == cachedFor && hips != null) return;
        cachedFor = animator;
        if (animator == null || !animator.isHuman) { hips = neck = null; return; }
        hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        head = animator.GetBoneTransform(HumanBodyBones.Head);
        neck = animator.GetBoneTransform(HumanBodyBones.Neck);
        if (neck == null) neck = head;
        lUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        lLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        rUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        rLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        lUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        lLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        rUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        rLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
    }

    static void Droop(Transform upper, Transform lower, Vector3 targetDir, float amount)
    {
        if (upper == null || lower == null || amount <= 0f) return;
        Vector3 cur = lower.position - upper.position;
        if (cur.sqrMagnitude < 1e-8f) return;
        Quaternion full = Quaternion.FromToRotation(cur.normalized, targetDir.normalized);
        upper.rotation = Quaternion.Slerp(Quaternion.identity, full, amount) * upper.rotation;
    }

    static void Spring(ref float x, ref float v, float target, float freq, float zeta, float dt)
    {
        float w = Mathf.Max(0.01f, freq) * 2f * Mathf.PI;
        float a = w * w * (target - x) - 2f * zeta * w * v;
        v += a * dt;
        x += v * dt;
    }
}
