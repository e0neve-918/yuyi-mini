// YUYI MINI — small shared helpers (cursor position, expressions, input guards).
// Modified version of MateEngine (© 2025 Johnson Jason / Shiny), MateEngine Pro License v2.1.

using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;

public static class YuyiAvatarUtil
{
    // ---------- Global cursor position (window moves with the cursor while dragging,
    //            so Input.mousePosition alone can't measure drag speed) ----------

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int x; public int y; }
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
#endif

    /// <summary>Cursor position in desktop pixels, Y pointing UP (same direction as Unity).</summary>
    public static Vector2 GlobalCursor()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (GetCursorPos(out POINT p)) return new Vector2(p.x, -p.y);
#endif
        return Input.mousePosition;
    }

    // ---------- Input guards ----------

    /// <summary>True when a left click right now should be ignored (menu open, UI under cursor...).</summary>
    public static bool ShouldIgnorePointer(AvatarAnimatorController controller)
    {
        try
        {
            if (controller != null && controller.BlockDraggingOverride) return true;
            if (MenuActions.IsMovementBlocked()) return true;
            if (TutorialMenu.IsActive) return true;
        }
        catch { /* MateEngine internals not ready yet */ }

        var es = EventSystem.current;
        return es != null && es.IsPointerOverGameObject();
    }

    public static Camera FindCamera()
    {
        var cam = Camera.main;
        if (cam != null && cam.isActiveAndEnabled) return cam;
        foreach (var c in Camera.allCameras)
            if (c != null && c.isActiveAndEnabled && c.targetTexture == null) return c;
        return null;
    }

    // ---------- Facial expressions (VRM 1.0 and VRM 0.x) ----------

    /// <summary>Sets an expression weight on the avatar. name: happy/angry/sad/relaxed/surprised.</summary>
    public static void SetExpression(GameObject avatar, string name, float weight)
    {
        if (avatar == null || string.IsNullOrEmpty(name) || name == "none") return;
        weight = Mathf.Clamp01(weight);
        string key = name.Trim().ToLowerInvariant();

        try
        {
            var v10 = avatar.GetComponentInChildren<UniVRM10.Vrm10Instance>();
            if (v10 != null && v10.Runtime != null)
            {
                UniVRM10.ExpressionPreset preset;
                switch (key)
                {
                    case "happy": preset = UniVRM10.ExpressionPreset.happy; break;
                    case "angry": preset = UniVRM10.ExpressionPreset.angry; break;
                    case "sad": preset = UniVRM10.ExpressionPreset.sad; break;
                    case "relaxed": preset = UniVRM10.ExpressionPreset.relaxed; break;
                    case "surprised": preset = UniVRM10.ExpressionPreset.surprised; break;
                    default: return;
                }
                v10.Runtime.Expression.SetWeight(UniVRM10.ExpressionKey.CreateFromPreset(preset), weight);
                return;
            }
        }
        catch { }

        try
        {
            var proxy = avatar.GetComponentInChildren<VRM.VRMBlendShapeProxy>();
            if (proxy != null)
            {
                VRM.BlendShapePreset preset;
                switch (key)
                {
                    case "happy": preset = VRM.BlendShapePreset.Joy; break;
                    case "angry": preset = VRM.BlendShapePreset.Angry; break;
                    case "sad": preset = VRM.BlendShapePreset.Sorrow; break;
                    case "relaxed":
                    case "surprised": preset = VRM.BlendShapePreset.Fun; break;
                    default: return;
                }
                proxy.ImmediatelySetValue(VRM.BlendShapeKey.CreateFromPreset(preset), weight);
            }
        }
        catch { }
    }

    /// <summary>Optional animator bool (only set if the controller defines it).</summary>
    public static void TrySetAnimatorBool(Animator animator, string param, bool value)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (var p in animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Bool && p.name == param)
            {
                animator.SetBool(param, value);
                return;
            }
        }
    }
}
