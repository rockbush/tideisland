// 美术资源：Blender 导出的模型（Resources/Models）、三种材质、卡通描边、小人和箭头。
// 模型导进来的朝向由模型里的标记点自动判断：船的 "Bow" 必须朝 +Z，背景的 "Marker_Beach" 必须朝 +Z（屏幕上方）。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tide
{
    public static class Art
    {
        public static Material Palette, Hull, Deck, Unlit, UnlitT, Outline;
        public static Quaternion BoatFix = Quaternion.identity, EnvFix = Quaternion.identity;
        static readonly Dictionary<string, GameObject> models = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Material> colorMats = new Dictionary<string, Material>();
        static readonly Dictionary<string, Material> unlitMats = new Dictionary<string, Material>();
        static readonly Dictionary<string, (Mesh mesh, bool[] team)> guyMesh = new Dictionary<string, (Mesh, bool[])>();
        static readonly Dictionary<Mesh, Mesh> outlineMesh = new Dictionary<Mesh, Mesh>();
        static readonly Dictionary<string, Material> outlineMats = new Dictionary<string, Material>();
        static readonly Dictionary<string, Material> teamMats = new Dictionary<string, Material>();
        static bool inited;

        public static void Init()
        {
            if (inited) return;
            inited = true;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            Palette = Resources.Load<Material>("Materials/M_Palette");
            Hull = Resources.Load<Material>("Materials/M_BoatHull");
            Deck = Resources.Load<Material>("Materials/M_BoatDeck");
            Unlit = Resources.Load<Material>("Materials/M_Unlit");
            UnlitT = Resources.Load<Material>("Materials/M_UnlitT");
            Outline = Resources.Load<Material>("Materials/M_Outline");
            ArrowMat = Resources.Load<Material>("Materials/M_Arrow");
            if (!ArrowMat) Debug.LogError("Resources/Materials/M_Arrow 不存在：先在编辑器里跑 Tide/一键配置");
            if (!Outline) Debug.LogError("Resources/Materials/M_Outline 不存在：先在编辑器里跑 Tide/一键配置");
            if (!Palette) Debug.LogError("Resources/Materials/M_Palette 不存在：先在编辑器里跑 Tide/一键配置");
            BoatFix = FixFrom("boat_speed", "Bow");
            EnvFix = FixFrom("env_harbor", "Marker_Beach");
            Debug.Log("[Tide] 朝向修正 船=" + BoatFix.eulerAngles + " 背景=" + EnvFix.eulerAngles);
        }

        static Quaternion FixFrom(string model, string marker)
        {
            var src = Model(model);
            if (!src) return Quaternion.identity;
            var go = Object.Instantiate(src);
            go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity;
            var m = Find(go.transform, marker);
            var q = Quaternion.identity;
            if (m)
            {
                var p = m.position;
                if (Mathf.Abs(p.x) > Mathf.Abs(p.z)) q = Quaternion.Euler(0, p.x > 0 ? -90 : 90, 0);
                else if (p.z < 0) q = Quaternion.Euler(0, 180, 0);
            }
            else Debug.LogWarning("[Tide] " + model + " 里没找到 " + marker);
            Object.Destroy(go);
            return q;
        }

        public static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = Find(c, name); if (r) return r; }
            return null;
        }

        public static GameObject Model(string name)
        {
            if (models.TryGetValue(name, out var g)) return g;
            g = Resources.Load<GameObject>("Models/" + name);
            if (!g) Debug.LogError("[Tide] 缺模型 Resources/Models/" + name);
            models[name] = g;
            return g;
        }

        // 生成一个模型：外面包一层，里面的模型按朝向修正转好。boat=true 用船的修正，否则用背景的修正
        public static GameObject Spawn(string name, Transform parent, bool boatFrame)
        {
            var wrap = new GameObject(name);
            wrap.transform.SetParent(parent, false);
            var src = Model(name);
            if (src)
            {
                var m = Object.Instantiate(src, wrap.transform, false);
                m.transform.localPosition = Vector3.zero;
                m.transform.localRotation = (boatFrame ? BoatFix : EnvFix) * src.transform.localRotation;
                foreach (var r in m.GetComponentsInChildren<Renderer>())
                {
                    r.shadowCastingMode = ShadowCastingMode.On;
                    r.receiveShadows = true;
                }
            }
            return wrap;
        }

        public static Material ColorMat(string hex)
        {
            if (colorMats.TryGetValue(hex, out var m)) return m;
            m = new Material(Hull) { name = "c_" + hex };
            m.SetColor("_BaseColor", UIKit.Hex(hex));
            colorMats[hex] = m;
            return m;
        }

        public static Material UnlitMat(Color c, bool transparent = false)
        {
            string k = ColorUtility.ToHtmlStringRGBA(c) + transparent;
            if (unlitMats.TryGetValue(k, out var m)) return m;
            m = new Material(transparent ? UnlitT : Unlit);
            m.SetColor("_BaseColor", c);
            unlitMats[k] = m;
            return m;
        }

        // ---- 小人：Blender 做的 Q 版大头小人（10-28 make_islanders_hats.py：每种颜色一种头饰，下标 = 颜色下标 COLS）----
        // 把模型的网格连同朝向修正一起烘成一个独立网格（脚底在原点、脸朝 +Z），team[i] = 第 i 个子网格要不要染队伍色
        // GuyW = 同一个人去掉右手（队头招手时换上，另挂一只会挥的手）；子网格顺序和完整版一样，材质通用
        // 红船长帽 / 蓝水手帽 / 黄草帽 / 绿椰子叶 / 紫丸子头 / 粉双发髻 / 青潜水镜 / 橙头巾：只看剪影也分得出颜色（帽子和衣服都是船的队伍色）
        public static readonly string[] GUYS = { "guyh_captain", "guyh_sailor", "guyh_straw", "guyh_sprout", "guyh_bun", "guyh_twin", "guyh_goggle", "guyh_bandana" };
        static int GuyIdx(int variant) => ((variant % GUYS.Length) + GUYS.Length) % GUYS.Length;
        public static (Mesh mesh, bool[] team) Guy(int variant) => GuyMesh(GUYS[GuyIdx(variant)]);
        public static (Mesh mesh, bool[] team) GuyW(int variant) => GuyMesh(GUYS[GuyIdx(variant)] + "_w");
        // 招手的那只手（GuyW 缺的那只）在网格的 +X 还是 -X（导入时轴会不会翻不用猜：完整版比 GuyW 多出来的顶点就是那只手，看它们 x 加起来的正负）
        // 10-28 原来比包围盒宽窄：新模型草帽帽檐、飘带比胳膊还宽，两边包围盒一样，永远判成 -X
        static readonly Dictionary<int, float> waveX = new Dictionary<int, float>();
        public static float GuyWaveX(int variant) => waveX.TryGetValue(variant, out var w) ? w : waveX[variant] = SumX(Guy(variant).mesh) > SumX(GuyW(variant).mesh) ? 1 : -1;
        static float SumX(Mesh m) { float s = 0; foreach (var v in m.vertices) s += v.x; return s; }
        static (Mesh mesh, bool[] team) GuyMesh(string name)
        {
            if (guyMesh.TryGetValue(name, out var g)) return g;
            var src = Model(name);
            var go = Object.Instantiate(src);
            go.transform.position = Vector3.zero; go.transform.rotation = BoatFix * src.transform.localRotation;
            var mf = go.GetComponentInChildren<MeshFilter>(); var mr = mf.GetComponent<MeshRenderer>();
            var mesh = Object.Instantiate(mf.sharedMesh); mesh.name = name;
            var m = mf.transform.localToWorldMatrix;
            var v = mesh.vertices; var n = mesh.normals;
            for (int i = 0; i < v.Length; i++) { v[i] = m.MultiplyPoint3x4(v[i]); n[i] = m.MultiplyVector(n[i]).normalized; }
            mesh.vertices = v; mesh.normals = n; mesh.RecalculateBounds();
            var team = new bool[mesh.subMeshCount];
            var ms = mr.sharedMaterials;
            for (int i = 0; i < team.Length && i < ms.Length; i++) team[i] = ms[i] && ms[i].name.StartsWith("M_BoatHull");
            Object.Destroy(go);
            return guyMesh[name] = (mesh, team);
        }
        public static Material[] GuyMats(int variant, string hex)
        {
            var g = Guy(variant); var r = new Material[g.team.Length];
            for (int i = 0; i < r.Length; i++) r[i] = g.team[i] ? TeamMat(hex) : Palette;
            return r;
        }

        // 船和小人的队伍色材质：太阳 + 天光加起来朝上的面会过曝（红色发粉），先压暗一点、饱和度提一点，屏幕上才是 COLS 里的颜色
        public static Color Lit(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            var o = Color.HSVToRGB(h, Mathf.Min(1, s * 1.08f), v * 0.9f); o.a = c.a; return o;
        }
        public static Material TeamMat(string hex)
        {
            if (teamMats.TryGetValue(hex, out var m)) return m;
            m = new Material(Hull) { name = "t_" + hex };
            m.SetColor("_BaseColor", Lit(UIKit.Hex(hex)));
            return teamMats[hex] = m;
        }

        // ---- 卡通描边 ----
        // 描边色 = 本身颜色的深色版（比纯黑柔和）；没有队伍色的东西用深棕
        public static Color OutlineColor(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            return Color.HSVToRGB(h, Mathf.Min(1, s * 1.1f + 0.1f), Mathf.Min(v, 1) * 0.36f);
        }
        public static Material OutlineMat(Color c, float width = 1.5f)
        {
            string k = ColorUtility.ToHtmlStringRGB(c) + width;
            if (outlineMats.TryGetValue(k, out var m)) return m;
            m = new Material(Outline) { name = "ol_" + k };
            m.SetColor("_Color", c); m.SetFloat("_Width", width);
            return outlineMats[k] = m;
        }
        public static readonly Color Ink = new Color(0.22f, 0.15f, 0.09f);

        // 描边用的网格：所有子网格合成一个；同一位置的顶点法线取平均（硬边那里法线是分开的，不平均的话线会断成一截截）
        public static Mesh OutlineMesh(Mesh src)
        {
            if (!src) return null;
            if (outlineMesh.TryGetValue(src, out var m)) return m;
            if (!src.isReadable) { Debug.LogWarning("[Tide] " + src.name + " 不可读，没法描边（导入设置要勾 Read/Write）"); return outlineMesh[src] = null; }
            var v = src.vertices; var n = src.normals;
            var sum = new Dictionary<Vector3Int, Vector3>();
            Vector3Int K(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x * 2000), Mathf.RoundToInt(p.y * 2000), Mathf.RoundToInt(p.z * 2000));
            for (int i = 0; i < v.Length; i++) { var k = K(v[i]); sum[k] = (sum.TryGetValue(k, out var s) ? s : Vector3.zero) + n[i]; }
            var sn = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++) sn[i] = sum[K(v[i])].normalized;
            var tris = new List<int>();
            for (int s = 0; s < src.subMeshCount; s++) tris.AddRange(src.GetTriangles(s));
            m = new Mesh { name = src.name + "_ol", indexFormat = v.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.vertices = v; m.normals = sn; m.SetTriangles(tris, 0); m.RecalculateBounds();
            return outlineMesh[src] = m;
        }

        // 描边总开关：用户看了标题界面上那艘没描边的船，说"这就是我要的样子"，所以现在关掉（代码留着，改 true 就回来）
        public static bool Outlines = false;

        // 给一个物体下面所有网格挂上描边（子物体 "ol"），返回挂上的描边渲染器（换颜色用）
        public static List<MeshRenderer> AddOutline(GameObject g, Material mat)
        {
            var list = new List<MeshRenderer>();
            if (!Outlines) return list;
            foreach (var mf in g.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.name == "ol") continue;
                var om = OutlineMesh(mf.sharedMesh); if (!om) continue;
                var o = new GameObject("ol"); o.transform.SetParent(mf.transform, false);
                o.AddComponent<MeshFilter>().sharedMesh = om;
                var r = o.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                list.Add(r);
            }
            return list;
        }

        // ---- 箭头：一张程序画的贴图 + 一块平放的四边形，+Z 是箭头尖 ----
        // 以前是两层多边形（深色描边层 + 白色层只差 6mm）：镜头远了两层抢深度会闪、边缘一圈锯齿。
        // 现在画成一张图：白芯 + 深蓝边 + 淡淡的外阴影，边缘按距离场抗锯齿，开 mipmap，远看也是干净的一条
        const float ARROW_PPM = 220, ARROW_MARGIN = 0.07f, ARROW_BORDER = 0.034f, ARROW_ROUND = 0.03f;
        static readonly Dictionary<string, (Mesh, Material)> arrows = new Dictionary<string, (Mesh, Material)>();
        public static (Mesh mesh, Material mat) ArrowFor(float y0, float y1, float hw, float sw, float hl)
        {
            string k = y0 + "_" + y1 + "_" + hw + "_" + sw + "_" + hl;
            if (arrows.TryGetValue(k, out var got)) return got;
            float yh = y1 - hl, x0 = -hw - ARROW_MARGIN, x1 = hw + ARROW_MARGIN, z0 = y0 - ARROW_MARGIN, z1 = y1 + ARROW_MARGIN;
            // 圆角：多边形往里缩 r，再按距离 - r 画，转角就是圆的
            float r = ARROW_ROUND;
            var poly = new[] { new Vector2(-sw + r, y0 + r), new Vector2(sw - r, y0 + r), new Vector2(sw - r, yh + r * 0.4f), new Vector2(hw - r * 1.6f, yh + r * 0.4f),
                new Vector2(0, y1 - r * 1.6f), new Vector2(-hw + r * 1.6f, yh + r * 0.4f), new Vector2(-sw + r, yh + r * 0.4f) };
            int W = Mathf.CeilToInt((x1 - x0) * ARROW_PPM), H = Mathf.CeilToInt((z1 - z0) * ARROW_PPM);
            var px = new Color32[W * H];
            Color white = Color.white, edge = UIKit.Hex("24405e");
            float aa = 1.2f / ARROW_PPM;
            for (int j = 0; j < H; j++)
                for (int i = 0; i < W; i++)
                {
                    var p = new Vector2(x0 + (i + 0.5f) / ARROW_PPM, z0 + (j + 0.5f) / ARROW_PPM);
                    float d = PolyDist(poly, p) - r;                        // < 0 在箭头里
                    float inner = Mathf.Clamp01(0.5f - (d + ARROW_BORDER) / aa);   // 白芯
                    float body = Mathf.Clamp01(0.5f - d / aa);                    // 白芯 + 描边
                    float shadow = d > 0 ? 0.32f * Mathf.Clamp01(1 - d / 0.045f) : 0;
                    var c = Color.Lerp(edge, white, inner);
                    float alpha = Mathf.Max(body, shadow * (1 - body));
                    if (body < 1) c = Color.Lerp(edge, c, body);
                    c.a = alpha;
                    px[j * W + i] = c;
                }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "arrow_" + k, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            tex.SetPixels32(px); tex.Apply(true, true);
            var m = new Mesh { name = "arrow" };
            m.vertices = new[] { new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), new Vector3(x0, 0, z1) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateNormals(); m.RecalculateBounds();
            var mat = new Material(ArrowMat) { name = "arrow_" + k };
            mat.SetTexture("_BaseMap", tex);
            return arrows[k] = (m, mat);
        }
        public static Material ArrowMat;

        // 点到多边形边界的距离（里面为负）
        static float PolyDist(Vector2[] v, Vector2 p)
        {
            float d = float.MaxValue; bool inside = false;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
            {
                Vector2 a = v[j], b = v[i], e = b - a, w = p - a;
                float t = Mathf.Clamp01(Vector2.Dot(w, e) / e.sqrMagnitude);
                d = Mathf.Min(d, (w - e * t).magnitude);
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside ? -d : d;
        }
    }
}
