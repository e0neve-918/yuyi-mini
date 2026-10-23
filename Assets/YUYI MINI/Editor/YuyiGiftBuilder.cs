// YUYI MINI — one-click gift builds, one .exe per character.
// Modified version of MateEngine (© 2025 Johnson Jason / Shiny), MateEngine Pro License v2.1.
//
// Menu: YUYI MINI ▸ Gift Builder
// Reads Gift/gift_builds.json (repository root) and, for each character:
//   1. copies the character model + profile.json into StreamingAssets/YuyiMini
//   2. sets the app name and icon
//   3. builds Windows x64 into Builds/<id>/
//   4. strips the default MateEngine avatar (Zome, © Yorshka Shop) from the build
//   5. copies LICENSE / NOTICE / CREDITS next to the .exe (required by the license)
//   6. checks the build for the default avatar and third-party assets, then zips it
// Project settings (name, icon) are restored afterwards.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class YuyiGiftCharacter
{
    public string id = "a";
    public string productName = "Yuyi Mini";
    public string companyName = "YuyiMini";
    public string exeName = "YuyiMini";
    public string model = "Gift/a/character.vrm";
    public string icon = "Gift/a/icon.png";
    public YuyiGiftProfile profile = new YuyiGiftProfile();
}

[Serializable]
public class YuyiGiftConfig
{
    public string outputFolder = "Builds";
    public string scene = "Assets/MATE ENGINE - Scenes/Mate Engine Main.unity";
    public List<YuyiGiftCharacter> characters = new List<YuyiGiftCharacter>();
}

public class YuyiGiftBuilder : EditorWindow
{
    public const string ConfigPath = "Gift/gift_builds.json";
    const string GiftAssetsFolder = "Assets/StreamingAssets/YuyiMini";
    const string GeneratedFolder = "Assets/YUYI MINI/Generated";

    static readonly string[] DefaultAvatarPaths = { "Assets/MATE ENGINE - Avatar/Zome" };
    static readonly string[] ThirdPartyArtPaths =
    {
        "Assets/MATE ENGINE - Avatar/DLCs", "Assets/MATE ENGINE - DLCs",
        "Assets/이루아_IruaStore", "Assets/noirunn", "Assets/[CooGee Works]", "Assets/QuQu",
    };
    static readonly string[] LicenseFiles = { "LICENSE.md", "NOTICE.txt", "CREDITS.md" };

    /// <summary>True only while a gift build is running (used by the scene stripper).</summary>
    public static bool IsGiftBuild { get; private set; }

    YuyiGiftConfig config;
    Vector2 scroll;
    string lastResult = "";

    [MenuItem("YUYI MINI/Gift Builder", priority = 0)]
    static void Open() => GetWindow<YuyiGiftBuilder>("Gift Builder").Reload();

    [MenuItem("YUYI MINI/Build All Gifts", priority = 1)]
    static void BuildAllMenu()
    {
        var cfg = LoadConfig(out string err);
        if (cfg == null) { EditorUtility.DisplayDialog("YUYI MINI", err, "OK"); return; }
        var sb = new StringBuilder();
        foreach (var c in cfg.characters) sb.AppendLine(BuildCharacter(cfg, c));
        EditorUtility.DisplayDialog("YUYI MINI", sb.ToString(), "OK");
    }

    [MenuItem("YUYI MINI/Open Builds Folder", priority = 20)]
    static void OpenBuilds()
    {
        var cfg = LoadConfig(out _) ?? new YuyiGiftConfig();
        string dir = Path.GetFullPath(cfg.outputFolder);
        Directory.CreateDirectory(dir);
        EditorUtility.RevealInFinder(dir);
    }

    void Reload() => config = LoadConfig(out lastResult);

    void OnGUI()
    {
        EditorGUILayout.LabelField("YUYI MINI — 선물용 빌드", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Gift/gift_builds.json 에 캐릭터를 설정하고 아래 버튼을 누르세요.\n" +
                                "결과물은 Builds/<id>/ 폴더와 Builds/<id>.zip 으로 나옵니다.", MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("설정 다시 읽기")) Reload();
            if (GUILayout.Button("설정 파일 열기")) EditorUtility.OpenWithDefaultApp(Path.GetFullPath(ConfigPath));
            if (GUILayout.Button("Builds 폴더 열기")) OpenBuilds();
        }

        if (config == null) { EditorGUILayout.HelpBox(lastResult, MessageType.Error); return; }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var c in config.characters)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField($"{c.profile.characterName}  ({c.id})", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("앱 이름", c.productName);
                DrawFile("모델", c.model);
                DrawFile("아이콘", c.icon);
                if (GUILayout.Button($"{c.profile.characterName} 빌드하기", GUILayout.Height(28)))
                {
                    lastResult = BuildCharacter(config, c);
                    GUIUtility.ExitGUI();
                }
            }
        }
        EditorGUILayout.EndScrollView();

        if (config.characters.Count > 1 && GUILayout.Button("전부 빌드하기", GUILayout.Height(32)))
        {
            var sb = new StringBuilder();
            foreach (var c in config.characters) sb.AppendLine(BuildCharacter(config, c));
            lastResult = sb.ToString();
            GUIUtility.ExitGUI();
        }

        if (!string.IsNullOrEmpty(lastResult)) EditorGUILayout.HelpBox(lastResult, MessageType.None);
    }

    static void DrawFile(string label, string path)
    {
        bool ok = !string.IsNullOrEmpty(path) && File.Exists(path);
        EditorGUILayout.LabelField(label, (ok ? "✔ " : "✘ 없음: ") + path);
    }

    // ------------------------------------------------------------------

    static YuyiGiftConfig LoadConfig(out string error)
    {
        error = "";
        if (!File.Exists(ConfigPath)) { error = $"{ConfigPath} 파일이 없습니다."; return null; }
        try
        {
            var cfg = JsonUtility.FromJson<YuyiGiftConfig>(File.ReadAllText(ConfigPath));
            if (cfg == null || cfg.characters == null || cfg.characters.Count == 0) { error = "캐릭터 설정이 비어 있습니다."; return null; }
            return cfg;
        }
        catch (Exception e) { error = "gift_builds.json 형식 오류: " + e.Message; return null; }
    }

    static string BuildCharacter(YuyiGiftConfig cfg, YuyiGiftCharacter c)
    {
        string name = string.IsNullOrEmpty(c.profile.characterName) ? c.id : c.profile.characterName;
        if (string.IsNullOrEmpty(c.model) || !File.Exists(c.model))
            return $"✘ {name}: 모델 파일이 없습니다 ({c.model})";
        if (!File.Exists(cfg.scene))
            return $"✘ {name}: 씬을 찾을 수 없습니다 ({cfg.scene})";

        string oldProduct = PlayerSettings.productName;
        string oldCompany = PlayerSettings.companyName;
        var oldIcons = PlayerSettings.GetIcons(NamedBuildTarget.Standalone, IconKind.Any);

        string outDir = Path.Combine(cfg.outputFolder, c.id);
        string exe = Path.Combine(outDir, SafeFileName(string.IsNullOrEmpty(c.exeName) ? c.productName : c.exeName) + ".exe");

        try
        {
            IsGiftBuild = true;
            PrepareStreamingAssets(c);

            PlayerSettings.productName = c.productName;
            PlayerSettings.companyName = string.IsNullOrEmpty(c.companyName) ? "YuyiMini" : c.companyName;
            var icon = ImportIcon(c);
            if (icon != null)
            {
                var icons = Enumerable.Repeat(icon, Mathf.Max(1, oldIcons.Length)).ToArray();
                PlayerSettings.SetIcons(NamedBuildTarget.Standalone, icons, IconKind.Any);
            }

            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { cfg.scene },
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });

            if (report.summary.result != BuildResult.Succeeded)
                return $"✘ {name}: 빌드 실패 ({report.summary.result}). Console 창을 확인하세요.";

            foreach (var f in LicenseFiles)
                if (File.Exists(f)) File.Copy(f, Path.Combine(outDir, f), true);
            CopyDir("Thirt Party Licenses", Path.Combine(outDir, "Third Party Licenses"));

            string check = CheckReport(report, out bool hasDefaultAvatar);
            File.WriteAllText(Path.Combine(outDir, "BUILD_CHECK.txt"), check, Encoding.UTF8);
            if (hasDefaultAvatar)
            {
                EditorUtility.DisplayDialog("YUYI MINI — 확인 필요",
                    $"{name} 빌드에 기본 아바타(Zome) 파일이 남아 있습니다.\n이대로 배포하면 라이선스 위반이에요.\n자세한 내용: {outDir}/BUILD_CHECK.txt", "알겠어요");
                return $"⚠ {name}: 빌드는 됐지만 기본 아바타가 남아 있어 zip을 만들지 않았습니다. BUILD_CHECK.txt 확인.";
            }

            string zip = Path.Combine(cfg.outputFolder, c.id + ".zip");
            if (File.Exists(zip)) File.Delete(zip);
            ZipFile.CreateFromDirectory(outDir, zip, System.IO.Compression.CompressionLevel.Optimal, false);
            return $"✔ {name}: {exe}  →  {zip}";
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return $"✘ {name}: {e.Message}";
        }
        finally
        {
            IsGiftBuild = false;
            PlayerSettings.productName = oldProduct;
            PlayerSettings.companyName = oldCompany;
            PlayerSettings.SetIcons(NamedBuildTarget.Standalone, oldIcons, IconKind.Any);
            CleanStreamingAssets();
            AssetDatabase.SaveAssets();
        }
    }

    static void PrepareStreamingAssets(YuyiGiftCharacter c)
    {
        Directory.CreateDirectory(GiftAssetsFolder);
        CleanStreamingAssets();
        // .vrm is renamed so Unity's VRM importer doesn't unpack it inside StreamingAssets
        // (MateEngine's loader reads the file by content, not by extension).
        string ext = Path.GetExtension(c.model).ToLowerInvariant();
        string modelFile = ext == ".me" ? "character.me" : "character.vrmdata";
        File.Copy(c.model, Path.Combine(GiftAssetsFolder, modelFile), true);

        c.profile.modelFile = modelFile;
        File.WriteAllText(Path.Combine(GiftAssetsFolder, "profile.json"), JsonUtility.ToJson(c.profile, true), Encoding.UTF8);
        AssetDatabase.Refresh();
    }

    /// <summary>Removes the per-character model so the repository stays clean.</summary>
    static void CleanStreamingAssets()
    {
        if (!Directory.Exists(GiftAssetsFolder)) return;
        foreach (var f in Directory.GetFiles(GiftAssetsFolder, "character.*"))
            File.Delete(f);
        File.WriteAllText(Path.Combine(GiftAssetsFolder, "profile.json"), JsonUtility.ToJson(new YuyiGiftProfile(), true), Encoding.UTF8);
        AssetDatabase.Refresh();
    }

    static Texture2D ImportIcon(YuyiGiftCharacter c)
    {
        if (string.IsNullOrEmpty(c.icon) || !File.Exists(c.icon)) return null;
        Directory.CreateDirectory(GeneratedFolder);
        string dst = $"{GeneratedFolder}/icon_{SafeFileName(c.id)}{Path.GetExtension(c.icon)}";
        File.Copy(c.icon, dst, true);
        AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
    }

    static string CheckReport(BuildReport report, out bool hasDefaultAvatar)
    {
        var found = new SortedSet<string>();
        try
        {
            foreach (var packed in report.packedAssets)
                foreach (var info in packed.contents)
                    if (!string.IsNullOrEmpty(info.sourceAssetPath)) found.Add(info.sourceAssetPath);
        }
        catch (Exception e) { Debug.LogWarning("[YuyiMini] could not read build report: " + e.Message); }

        var avatar = found.Where(p => DefaultAvatarPaths.Any(prefix => p.StartsWith(prefix, StringComparison.Ordinal))).ToList();
        var thirdParty = found.Where(p => ThirdPartyArtPaths.Any(prefix => p.StartsWith(prefix, StringComparison.Ordinal))).ToList();
        hasDefaultAvatar = avatar.Count > 0;

        var sb = new StringBuilder();
        sb.AppendLine("YUYI MINI build check  " + DateTime.Now);
        sb.AppendLine();
        sb.AppendLine(hasDefaultAvatar ? "[X] Default avatar (Zome, © Yorshka Shop) files are INCLUDED — do not distribute:" : "[OK] Default avatar (Zome) is not included.");
        foreach (var p in avatar) sb.AppendLine("    " + p);
        sb.AppendLine();
        sb.AppendLine(thirdParty.Count == 0 ? "[OK] No third-party DLC/accessory art found." : "[!] Third-party DLC/accessory art included (check that you may redistribute it):");
        foreach (var p in thirdParty) sb.AppendLine("    " + p);
        return sb.ToString();
    }

    static void CopyDir(string src, string dst)
    {
        if (!Directory.Exists(src)) return;
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
        {
            if (f.EndsWith(".meta")) continue;
            string target = Path.Combine(dst, f.Substring(src.Length).TrimStart('/', '\\'));
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(f, target, true);
        }
    }

    static string SafeFileName(string s)
    {
        foreach (char ch in Path.GetInvalidFileNameChars()) s = s.Replace(ch, '_');
        return string.IsNullOrWhiteSpace(s) ? "YuyiMini" : s.Trim();
    }
}

/// <summary>
/// During gift builds, removes the default avatar's artwork (meshes, materials, VRM meta)
/// from the scene copy that goes into the player. The project scene itself is untouched.
/// </summary>
public class YuyiStripDefaultAvatar : IProcessSceneWithReport
{
    public int callbackOrder => 0;

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (!BuildPipeline.isBuildingPlayer || !YuyiGiftBuilder.IsGiftBuild) return;

        foreach (var root in scene.GetRootGameObjects())
            foreach (var loader in root.GetComponentsInChildren<VRMLoader>(true))
            {
                if (loader == null || loader.mainModel == null) continue;
                Strip(loader.mainModel);
                Debug.Log($"[YuyiMini] stripped default avatar art from '{loader.mainModel.name}' in {scene.name}");
            }
    }

    static void Strip(GameObject model)
    {
        foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.sharedMesh = null;
            smr.sharedMaterials = new Material[0];
            smr.enabled = false;
        }
        foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true)) mf.sharedMesh = null;
        foreach (var mr in model.GetComponentsInChildren<MeshRenderer>(true))
        {
            mr.sharedMaterials = new Material[0];
            mr.enabled = false;
        }

        // VRM metadata (thumbnail, blendshape clips) points at the model's textures/meshes.
        foreach (var c in model.GetComponentsInChildren<Component>(true))
        {
            if (c == null) continue;
            string type = c.GetType().FullName ?? "";
            if (type == "VRM.VRMMeta" || type == "VRM.VRMBlendShapeProxy" || type == "VRM.VRMFirstPerson")
            {
                try { UnityEngine.Object.DestroyImmediate(c); }
                catch (Exception e) { Debug.LogWarning($"[YuyiMini] could not remove {type}: {e.Message}"); }
            }
        }
    }
}
