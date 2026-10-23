// YUYI MINI — gift build profile
// Modified version of MateEngine (© 2025 Johnson Jason / Shiny), MateEngine Pro License v2.1.
//
// Reads StreamingAssets/YuyiMini/profile.json, which the gift builder writes for each character.
// Everything has a safe default, so the app still runs if the file is missing.

using System.IO;
using UnityEngine;

[System.Serializable]
public class YuyiGiftProfile
{
    public string characterName = "Yuyi";
    public string modelFile = "character.vrmdata";

    [Header("Click hearts")]
    public bool heartsEnabled = true;
    public string[] heartColors = { "#FF6FA8", "#FF9EC4", "#FF4D6D", "#FFC2D9" };
    public int heartCountMin = 3;
    public int heartCountMax = 5;
    public float heartSize = 1f;              // multiplier
    public bool tintHearts = true;            // false = show heart.png in its own colors
    public string clickExpression = "happy";  // happy / angry / sad / relaxed / surprised / none

    [Header("Scruff grab")]
    public bool scruffEnabled = true;
    public bool useGrabCursor = false;       // true = hand cursor while held
    public string scruffExpression = "relaxed";
    public float swingStrength = 1f;          // multiplier
    public float legKick = 1f;                // 0 = no kicking

    // ---------------------------------------------------------------

    const string FolderName = "YuyiMini";
    static YuyiGiftProfile _current;

    public static string Folder => Path.Combine(Application.streamingAssetsPath, FolderName);

    public static YuyiGiftProfile Current
    {
        get
        {
            if (_current != null) return _current;
            _current = new YuyiGiftProfile();
            try
            {
                string path = Path.Combine(Folder, "profile.json");
                if (File.Exists(path))
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(path), _current);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[YuyiMini] profile.json could not be read, using defaults: " + e.Message);
            }
            return _current;
        }
    }

    /// <summary>Full path of the bundled character model, or null when none is bundled.</summary>
    public static string BundledModelPath
    {
        get
        {
            string file = Current.modelFile;
            if (string.IsNullOrEmpty(file)) return null;
            string path = Path.Combine(Folder, file);
            return File.Exists(path) ? path : null;
        }
    }

    public Color GetHeartColor(int index)
    {
        if (heartColors == null || heartColors.Length == 0) return new Color(1f, 0.44f, 0.66f);
        string hex = heartColors[Mathf.Abs(index) % heartColors.Length];
        return ColorUtility.TryParseHtmlString(hex, out var c) ? c : new Color(1f, 0.44f, 0.66f);
    }

    /// <summary>Loads a PNG from StreamingAssets/YuyiMini as a readable texture, or null.</summary>
    public static Texture2D LoadPng(string fileName)
    {
        try
        {
            string path = Path.Combine(Folder, fileName);
            if (!File.Exists(path)) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(path))) { Object.Destroy(tex); return null; }
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = fileName;
            return tex;
        }
        catch { return null; }
    }
}
