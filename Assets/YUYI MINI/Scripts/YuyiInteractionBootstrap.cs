// YUYI MINI — attaches the gift interactions to whatever avatar MateEngine is showing.
// Modified version of MateEngine (© 2025 Johnson Jason / Shiny), MateEngine Pro License v2.1.
//
// No scene or prefab edits are needed: this starts automatically, looks for active
// AvatarAnimatorController components (the default model or a loaded VRM) and adds
// YuyiScruffGrab + YuyiHeartClick to them. It re-checks periodically because MateEngine
// swaps the avatar object when a model is loaded.

using UnityEngine;

public class YuyiInteractionBootstrap : MonoBehaviour
{
    const float ScanInterval = 0.75f;
    float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (FindAnyObjectByType<YuyiInteractionBootstrap>() != null) return;
        var go = new GameObject("[YuyiMini Interactions]");
        go.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
        DontDestroyOnLoad(go);
        go.AddComponent<YuyiInteractionBootstrap>();
        Debug.Log("[YuyiMini] interactions ready for " + YuyiGiftProfile.Current.characterName);
    }

    void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + ScanInterval;

        var controllers = FindObjectsByType<AvatarAnimatorController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var c in controllers)
        {
            if (c == null) continue;
            var go = c.gameObject;
            // order matters: the heart click looks up the grab component
            if (go.GetComponent<YuyiScruffGrab>() == null) go.AddComponent<YuyiScruffGrab>();
            if (go.GetComponent<YuyiHeartClick>() == null) go.AddComponent<YuyiHeartClick>();
        }
    }
}
