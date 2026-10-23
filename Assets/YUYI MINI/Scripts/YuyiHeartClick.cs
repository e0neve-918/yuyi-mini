// YUYI MINI — "click and hearts pop out" interaction.
// Modified version of MateEngine (© 2025 Johnson Jason / Shiny), MateEngine Pro License v2.1.
//
// A quick left click on the avatar (press + release without moving) spawns a few hearts
// that pop, float up, wobble and fade. Long presses / drags are left to the scruff grab.
// Hearts are plain SpriteRenderers, so no shader has to be found at runtime.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class YuyiHeartClick : MonoBehaviour
{
    [Header("Click detection")]
    public float maxClickTime = 0.28f;     // seconds the button may be held
    public float maxClickMove = 8f;        // desktop pixels the cursor may move
    public float cooldown = 0.12f;

    [Header("Hearts")]
    public float lifetime = 1.15f;
    public float popInTime = 0.16f;
    public float riseSpeed = 1.1f;         // in "heart sizes" per second
    public float sidewaysSpread = 0.7f;
    public float spawnInterval = 0.07f;    // the "뿅 뿅" stagger
    public float sizeOfHead = 0.38f;       // heart size relative to hips→head distance

    [Header("Expression")]
    public float expressionHold = 1.2f;

    AvatarAnimatorController controller;
    YuyiScruffGrab scruff;
    Animator animator;

    float pressTime = -1f;
    Vector2 pressPos;
    float lastSpawn;

    static Sprite heartSprite;
    readonly Stack<GameObject> pool = new Stack<GameObject>();
    Coroutine expressionRoutine;

    void Awake()
    {
        controller = GetComponent<AvatarAnimatorController>();
        scruff = GetComponent<YuyiScruffGrab>();
        animator = controller != null && controller.animator != null ? controller.animator : GetComponentInChildren<Animator>();
    }

    void Update()
    {
        var profile = YuyiGiftProfile.Current;
        if (!profile.heartsEnabled) return;

        if (Input.GetMouseButtonDown(0))
        {
            pressTime = YuyiAvatarUtil.ShouldIgnorePointer(controller) ? -1f : Time.unscaledTime;
            pressPos = YuyiAvatarUtil.GlobalCursor();
        }

        if (Input.GetMouseButtonUp(0) && pressTime >= 0f)
        {
            float held = Time.unscaledTime - pressTime;
            float moved = Vector2.Distance(pressPos, YuyiAvatarUtil.GlobalCursor());
            pressTime = -1f;

            if (scruff == null) scruff = GetComponent<YuyiScruffGrab>();
            bool wasGrab = scruff != null && scruff.GrabbedThisPress;

            if (!wasGrab && held <= maxClickTime && moved <= maxClickMove && Time.unscaledTime - lastSpawn > cooldown)
            {
                lastSpawn = Time.unscaledTime;
                StartCoroutine(SpawnBurst(Input.mousePosition));
            }
        }
    }

    // ----------------------------------------------------------------

    IEnumerator SpawnBurst(Vector3 screenPos)
    {
        var profile = YuyiGiftProfile.Current;
        var cam = YuyiAvatarUtil.FindCamera();
        if (cam == null || animator == null) yield break;

        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        if (head == null) yield break;

        float bodySize = hips != null ? Vector3.Distance(head.position, hips.position) : 0.4f;
        if (bodySize < 0.01f) bodySize = 0.4f;
        float size = bodySize * sizeOfHead * Mathf.Max(0.1f, profile.heartSize);

        // Spawn where the click happened, at the depth of the head, nudged toward the camera.
        float depth = Mathf.Max(cam.nearClipPlane + 0.05f, cam.WorldToScreenPoint(head.position).z);
        Vector3 origin = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
        origin -= cam.transform.forward * size * 0.8f;

        PlayExpression(profile.clickExpression);

        int count = Random.Range(Mathf.Max(1, profile.heartCountMin), Mathf.Max(profile.heartCountMin, profile.heartCountMax) + 1);
        int colorOffset = Random.Range(0, 16);
        for (int i = 0; i < count; i++)
        {
            StartCoroutine(AnimateHeart(cam, origin, size, profile.GetHeartColor(colorOffset + i), i));
            yield return new WaitForSecondsRealtime(spawnInterval);
        }
    }

    IEnumerator AnimateHeart(Camera cam, Vector3 origin, float size, Color color, int index)
    {
        var go = GetHeart();
        var sr = go.GetComponent<SpriteRenderer>();
        go.layer = gameObject.layer;

        float side = Random.Range(-sidewaysSpread, sidewaysSpread);
        float scaleJitter = Random.Range(0.75f, 1.15f);
        float wobblePhase = Random.Range(0f, Mathf.PI * 2f);
        float tilt = Random.Range(-18f, 18f);
        Vector3 up = cam.transform.up, right = cam.transform.right;
        Vector3 start = origin + right * side * size * 0.4f;

        float t = 0f;
        while (t < lifetime)
        {
            if (cam == null) break;
            t += Time.unscaledDeltaTime;
            float n = t / lifetime;

            // pop: 0 → 1.25 → 1 (overshoot), then shrink slightly at the end
            float pop = t < popInTime ? EaseOutBack(t / popInTime) : 1f;
            float shrink = Mathf.Lerp(1f, 0.7f, Mathf.InverseLerp(0.7f, 1f, n));
            float s = size * scaleJitter * pop * shrink;

            float rise = riseSpeed * size * t * (1.2f - 0.4f * n);
            float sway = Mathf.Sin(wobblePhase + t * 7f) * size * 0.12f;
            Vector3 pos = start + up * rise + right * (side * size * t * 0.8f + sway);

            go.transform.position = pos;
            go.transform.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, tilt + Mathf.Sin(wobblePhase + t * 9f) * 12f);
            go.transform.localScale = Vector3.one * s;

            float alpha = 1f - Mathf.InverseLerp(0.55f, 1f, n);
            sr.color = new Color(color.r, color.g, color.b, color.a * alpha);
            sr.sortingOrder = 32000 - index;

            yield return null;
        }
        ReleaseHeart(go);
    }

    void PlayExpression(string expression)
    {
        if (string.IsNullOrEmpty(expression) || expression == "none" || animator == null) return;
        if (expressionRoutine != null) StopCoroutine(expressionRoutine);
        expressionRoutine = StartCoroutine(ExpressionRoutine(expression));
    }

    IEnumerator ExpressionRoutine(string expression)
    {
        float t = 0f, fade = 0.15f;
        while (t < expressionHold)
        {
            t += Time.unscaledDeltaTime;
            float w = Mathf.Min(Mathf.Clamp01(t / fade), Mathf.Clamp01((expressionHold - t) / fade));
            YuyiAvatarUtil.SetExpression(animator.gameObject, expression, w);
            yield return new WaitForEndOfFrame();
        }
        YuyiAvatarUtil.SetExpression(animator.gameObject, expression, 0f);
        expressionRoutine = null;
    }

    // ----------------------------------------------------------------

    GameObject GetHeart()
    {
        GameObject go = pool.Count > 0 ? pool.Pop() : null;
        if (go == null)
        {
            go = new GameObject("YuyiHeart");
            go.hideFlags = HideFlags.DontSave;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetHeartSprite();
        }
        go.SetActive(true);
        return go;
    }

    void ReleaseHeart(GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);
        pool.Push(go);
    }

    void OnDisable()
    {
        StopAllCoroutines();
        expressionRoutine = null;
        foreach (var go in pool) if (go != null) Destroy(go);
        pool.Clear();
        foreach (var sr in FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (sr != null && sr.gameObject.name == "YuyiHeart" && sr.gameObject.activeSelf) Destroy(sr.gameObject);
    }

    static Sprite GetHeartSprite()
    {
        if (heartSprite != null) return heartSprite;
        Texture2D tex = YuyiGiftProfile.LoadPng("heart.png") ?? BuildHeartTexture(128);
        heartSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        heartSprite.name = "YuyiHeart";
        return heartSprite;
    }

    /// <summary>Fallback heart drawn from the implicit heart curve (white, tinted at runtime).</summary>
    static Texture2D BuildHeartTexture(int res)
    {
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "YuyiHeartProcedural" };
        var px = new Color32[res * res];
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float fx = (x + 0.5f) / res * 2.6f - 1.3f;
                float fy = (y + 0.5f) / res * 2.6f - 1.25f;
                float a = fx * fx + fy * fy - 1f;
                float f = a * a * a - fx * fx * fy * fy * fy;
                float alpha = Mathf.Clamp01(-f * 18f + 0.5f);
                // soft highlight in the upper left lobe
                float hl = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(fx, fy), new Vector2(-0.45f, 0.55f)) / 0.28f);
                byte v = (byte)Mathf.Lerp(235, 255, hl);
                px[y * res + x] = new Color32(v, v, v, (byte)(alpha * 255));
            }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        return tex;
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.9f, c3 = c1 + 1f;
        x = Mathf.Clamp01(x) - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }
}
