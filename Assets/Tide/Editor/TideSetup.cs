// 编辑器一键配置：菜单 Tide/一键配置（命令行：-executeMethod Tide.TideSetup.Setup）
//   ① 调色板贴图设成点采样、不压缩 ② 建三种材质 + 两种不受光材质 ③ 模型导入设置 + 材质按名字对上
//   ④ 建 Main 场景（只有一个挂 Game 脚本的空物体）并设为打包第一个场景 ⑤ 竖屏、包名、图标、版本号
//   ⑥ 安卓：IL2CPP + ARM64（Google Play 要求 64 位），最低 Android 7，目标 API 35
// 打 Windows 测试包：Tide.TideSetup.BuildWin   打安卓测试包：Tide.TideSetup.BuildAndroid（.apk，直接装手机）
// 打上架包：Tide.TideSetup.BuildAAB（.aab）。上传 Google Play 要用你自己的上传密钥签名，打包前设好环境变量：
//   TIDE_KEYSTORE=密钥文件路径  TIDE_KS_PASS=密钥库密码  TIDE_KEY_ALIAS=别名  TIDE_KEY_PASS=别名密码
//   没设就用 Unity 的调试签名（能装手机，不能传商店）
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tide
{
    public static class TideSetup
    {
        const string ROOT = "Assets/Tide/Resources";
        public const string VERSION = "0.1.0";
        const int VERSION_CODE = 1;          // 每次传 Google Play 都要 +1

        [MenuItem("Tide/一键配置")]
        public static void Setup()
        {
            AssetDatabase.Refresh();
            // ① 调色板
            string palPath = ROOT + "/Textures/palette.png";
            var ti = (TextureImporter)AssetImporter.GetAtPath(palPath);
            if (ti)
            {
                ti.filterMode = FilterMode.Point; ti.mipmapEnabled = false; ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.sRGBTexture = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport();
            }
            else Debug.LogError("缺 " + palPath);
            var pal = AssetDatabase.LoadAssetAtPath<Texture2D>(palPath);
            // ①b 界面图（透明底 PNG）和应用图标
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ROOT + "/UI", "Assets/Tide/Icon" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var ui = (TextureImporter)AssetImporter.GetAtPath(p);
                bool icon = p.Contains("/Icon/");
                ui.textureType = TextureImporterType.Default; ui.alphaIsTransparency = true; ui.mipmapEnabled = false;
                ui.wrapMode = TextureWrapMode.Clamp; ui.npotScale = TextureImporterNPOTScale.None;
                // 界面贴图（skin/、ai_*）和背景图保留原图分辨率、不压缩：以前一律压到 256，背景和按钮放大后是糊的
                bool skin = p.Contains("/UI/skin/") || p.Contains("/UI/ai_");
                ui.maxTextureSize = icon || skin ? 2048 : 256;
                ui.textureCompression = icon || skin ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
                ui.SaveAndReimport();
            }
            // ①c iOS 震动插件只给 iOS 用
            var mm = AssetImporter.GetAtPath("Assets/Tide/Plugins/iOS/TideHaptics.mm") as PluginImporter;
            if (mm)
            {
                mm.SetCompatibleWithAnyPlatform(false); mm.SetCompatibleWithEditor(false);
                foreach (BuildTarget t in new[] { BuildTarget.Android, BuildTarget.StandaloneWindows64 }) mm.SetCompatibleWithPlatform(t, false);
                mm.SetCompatibleWithPlatform(BuildTarget.iOS, true); mm.SaveAndReimport();
            }
            // ② 材质
            Directory.CreateDirectory(ROOT + "/Materials");
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var mPal = Mat("M_Palette", lit, Color.white, pal, 0.08f);
            var mHull = Mat("M_BoatHull", lit, Color.white, null, 0.4f);
            var mDeck = Mat("M_BoatDeck", lit, Color.white, null, 0.15f);
            Mat("M_Unlit", unlit, Color.white, null, 0);
            var olSh = Shader.Find("Tide/Outline");
            if (olSh)
            {
                string op = ROOT + "/Materials/M_Outline.mat";
                var om = AssetDatabase.LoadAssetAtPath<Material>(op);
                if (!om) { om = new Material(olSh); AssetDatabase.CreateAsset(om, op); }
                om.shader = olSh; om.enableInstancing = true; EditorUtility.SetDirty(om);
            }
            else Debug.LogError("[Tide] 找不到 Tide/Outline 着色器");
            var arSh = Shader.Find("Tide/Arrow");
            if (arSh)
            {
                string ap = ROOT + "/Materials/M_Arrow.mat";
                var am = AssetDatabase.LoadAssetAtPath<Material>(ap);
                if (!am) { am = new Material(arSh); AssetDatabase.CreateAsset(am, ap); }
                am.shader = arSh; EditorUtility.SetDirty(am);
            }
            else Debug.LogError("[Tide] 找不到 Tide/Arrow 着色器");
            var mT = Mat("M_UnlitT", unlit, Color.white, null, 0);
            mT.SetFloat("_Surface", 1); mT.SetFloat("_Blend", 0); mT.SetOverrideTag("RenderType", "Transparent");
            mT.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); mT.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mT.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); mT.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mT.SetFloat("_ZWrite", 0); mT.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mT.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mT);
            AssetDatabase.SaveAssets();
            // ③ 模型
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ROOT + "/Models" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mi = (ModelImporter)AssetImporter.GetAtPath(path);
                mi.globalScale = 1; mi.useFileScale = true; mi.importAnimation = false; mi.importCameras = false; mi.importLights = false;
                mi.animationType = ModelImporterAnimationType.None; mi.importBlendShapes = false; mi.isReadable = true;   // 描边要读顶点
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                mi.importNormals = ModelImporterNormals.Import;
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_Palette"), mPal);
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_BoatHull"), mHull);
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_BoatDeck"), mDeck);
                mi.SaveAndReimport();
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var b = new Bounds(); bool first = true;
                foreach (var r in go.GetComponentsInChildren<MeshFilter>())
                {
                    var bb = r.sharedMesh.bounds; var m = r.transform.localToWorldMatrix;
                    var c = m.MultiplyPoint3x4(bb.center);
                    if (first) { b = new Bounds(c, Vector3.zero); first = false; }
                    b.Encapsulate(m.MultiplyPoint3x4(bb.min)); b.Encapsulate(m.MultiplyPoint3x4(bb.max));
                }
                var mats = go.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Where(x => x).Select(x => x.name).Distinct();
                Debug.Log("[Tide] 模型 " + Path.GetFileNameWithoutExtension(path) + " 尺寸=" + b.size.ToString("0.00") + " 中心=" + b.center.ToString("0.00") +
                    " 根旋转=" + go.transform.localEulerAngles + " 材质=" + string.Join(",", mats));
            }
            // ④ 场景
            string scenePath = "Assets/Tide/Main.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Game").AddComponent<Game>();
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            // ⑤ 玩家设置
            PlayerSettings.companyName = "TideIsland";
            PlayerSettings.productName = "Tide Island";
            PlayerSettings.bundleVersion = VERSION;
            PlayerSettings.Android.bundleVersionCode = VERSION_CODE;
            PlayerSettings.iOS.buildNumber = VERSION_CODE.ToString();
            var iconTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Tide/Icon/app_icon.png");
            if (iconTex) PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { iconTex });
            // ⑥ 安卓
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.Android, Il2CppCompilerConfiguration.Release);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)35;
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetOSVersionString = "13.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.tideisland.boat");
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.tideisland.boat");
            PlayerSettings.defaultScreenWidth = 390; PlayerSettings.defaultScreenHeight = 844;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            Debug.Log("[Tide] 配置完成");
        }

        static Material Mat(string name, Shader sh, Color c, Texture tex, float smooth)
        {
            string p = ROOT + "/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); }
            m.shader = sh;
            m.SetColor("_BaseColor", c);
            if (tex) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_Smoothness"))
            {
                m.SetFloat("_Smoothness", smooth);
                m.SetFloat("_EnvironmentReflections", 0); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                m.SetFloat("_SpecularHighlights", smooth > 0.2f ? 1 : 0);
                if (smooth <= 0.2f) m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        public static void BuildWin()
        {
            Setup();
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Tide/Main.unity" }, locationPathName = Environment.GetEnvironmentVariable("TIDE_WIN_OUT") ?? "Build/Win/island.exe",   // 旧的 exe 正开着时换个目录打
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
            });
            Debug.Log("[Tide] Windows 包：" + r.summary.result + " 错误 " + r.summary.totalErrors);
            if (Application.isBatchMode) EditorApplication.Exit(r.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }

        public static void BuildAndroid() { Android(false); }
        public static void BuildAAB() { Android(true); }

        static void Android(bool aab)
        {
            Setup();
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.buildAppBundle = aab;
            string ks = Environment.GetEnvironmentVariable("TIDE_KEYSTORE");
            bool signed = !string.IsNullOrEmpty(ks) && File.Exists(ks);
            PlayerSettings.Android.useCustomKeystore = signed;
            if (signed)
            {
                PlayerSettings.Android.keystoreName = ks;
                PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("TIDE_KS_PASS");
                PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("TIDE_KEY_ALIAS");
                PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("TIDE_KEY_PASS");
            }
            Debug.Log("[Tide] 签名：" + (signed ? "上传密钥 " + ks : "调试签名（不能传商店）"));
            var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Tide/Main.unity" }, locationPathName = aab ? "Build/Android/tideisland.aab" : "Build/Android/tideisland.apk",
                target = BuildTarget.Android, options = BuildOptions.None
            });
            Debug.Log("[Tide] 安卓包：" + r.summary.result + " 错误 " + r.summary.totalErrors + " 大小 " + (r.summary.totalSize / 1048576f).ToString("0.0") + "MB");
            if (Application.isBatchMode) EditorApplication.Exit(r.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }
    }
}
