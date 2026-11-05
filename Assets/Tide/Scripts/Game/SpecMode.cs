// 特殊模式（10-30）：开始界面的「特殊模式」按钮 → 13 种地形的列表 → 试玩。
// 这是给用户先试手感的独立模式：规则在 SpecSim.cs，地图在 SpecLevels.cs，这里只负责画面和输入。
// 进来时把主线的 world 藏起来，出去时放回来，不碰主线关卡、存档进度、金币。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static Tide.Loc;

namespace Tide
{
    public partial class Game
    {
        const float SP_GS = 1.12f, SP_Y = 0.25f;
        const string SP_SAND = "f3d69a", SP_SKIRT = "cfa76a", SP_WOOD = "b98a55", SP_STONE = "aab3bb", SP_LAGOON = "8fe3ef";
        bool specOpen, spBot, spDownOn; int spSteps = 1, spSeed = 1; SpKind spKind;
        SpSim sp; Transform spRoot; BoatV[] spB; Guy[] spG; bool[] spSeat; Text[] spNum; float[] spLit;
        readonly List<GameObject> spHead = new List<GameObject>(), spPiers = new List<GameObject>();
        GameObject spLaneBar, spGate, spBridge, spBar, spDrift, spFerry, spFogBeam, spPreDock; Mesh spBeamMesh; LineRenderer spLine; SpBoat spDown;
        Text spInfo, spTip, spTime, spFlareT; float spTipT, spBeamA; Image spFill; RectTransform spBarR; RectTransform[] spPileUI; Text[] spPileN;
        bool SpecList => ui.spec && ui.spec.gameObject.activeSelf && !specOpen;

        // ---------- 列表 ----------
        void ShowSpecList()
        {
            HideTitle(); HideModal(); SetHud(false);
            mixMap = false; mixOn = false; if (mixAnim) { Destroy(mixAnim); mixAnim = null; }
            foreach (Transform c in ui.spec) Destroy(c.gameObject);
            ui.spec.gameObject.SetActive(true);
            UIKit.Img(ui.spec, "bg", null, UIKit.Hex("0f4f73"), true).rectTransform.Stretch(-50, -50, -50, -50);
            var S = SafeRect(ui.spec, "safe");
            var t = UIKit.Txt(S, "t", T("特殊模式"), 30, Color.white, TextAnchor.MiddleCenter, true);
            t.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(260, 44));
            UIKit.Txt(S, "s", SpSim.NAMES.Length + " 种地形试玩 · 不影响主线进度", 14, UIKit.Hex("bfe3ff")).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -56), new Vector2(320, 22));
            SpBack(S, () => { sfx.Play("click"); SpecExit(); });
            var mb = UIKit.Panel(S, "mix", UIKit.Hex("ffc530"), 4, 3).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -84), new Vector2(360, 62));
            UIKit.Txt(mb, "t", "混合模式（新）", 22, UIKit.Hex("5a3410"), TextAnchor.MiddleCenter).rectTransform.Stretch(0, 22, 0, 4);
            UIKit.Txt(mb, "s", MIX_NI + " 座机制岛 · 每岛 8 关 · 简单 / 困难 / 地狱", 13, UIKit.Hex("7a4d21")).rectTransform.Stretch(0, 4, 0, 36);
            UIKit.Btn(mb, () => { sfx.Play("click"); ShowMixMap(mixI); });
            var view = UIKit.Rect("view", S).Stretch(10, 8, 10, 156); view.gameObject.AddComponent<RectMask2D>();
            UIKit.Img(view, "hit", null, new Color(0, 0, 0, 0), true).rectTransform.Stretch();
            var content = UIKit.Rect("content", view); content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            const float H = 66, GAP = 6; int n = SpSim.NAMES.Length;
            content.sizeDelta = new Vector2(0, n * (H + GAP)); content.anchoredPosition = Vector2.zero;
            for (int k = 0; k < n; k++)
            {
                int kk = k;
                var r = UIKit.Panel(content, "row" + k, UIKit.Hex("fff8e6"), 3, 2);
                r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(0.5f, 1); r.offsetMin = new Vector2(2, -k * (H + GAP) - H); r.offsetMax = new Vector2(-2, -k * (H + GAP));
                var d = UIKit.Rect("num", r).Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(32, 0), new Vector2(40, 40));
                UIKit.Img(d, "d", UIKit.Circle, UIKit.Hex("2f7be0")).rectTransform.Stretch();
                UIKit.Txt(d, "t", (k + 1).ToString(), 20, Color.white).rectTransform.Stretch();
                UIKit.Txt(r, "n", SpSim.NAMES[k], 19, UIKit.Hex("17306b"), TextAnchor.MiddleLeft).rectTransform.Stretch(62, 30, 8, 6);
                UIKit.Txt(r, "o", SpSim.ONE[k], 13, UIKit.Hex("6b5a44"), TextAnchor.MiddleLeft).rectTransform.Stretch(62, 6, 8, 34);
                UIKit.Btn(r, () => { sfx.Play("click"); SpecPlay((SpKind)kk, Random.Range(1, 100000)); });
            }
            var sr = view.gameObject.AddComponent<ScrollRect>(); sr.content = content; sr.horizontal = false; sr.viewport = view; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 30;
        }

        void SpBack(RectTransform S, System.Action fn)
        {
            var b = UIKit.Panel(S, "back", UIKit.Hex("fff8e6"), 3, 2).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -12), new Vector2(70, 40));
            UIKit.Txt(b, "t", "返回", 17, UIKit.Hex("17306b")).rectTransform.Stretch();
            UIKit.Btn(b, fn);
        }

        void SpecExit()
        {
            SpecClean(); specOpen = false; spBot = false; spSteps = 1; mixOn = false; mixMap = false;
            foreach (Transform c in ui.spec) Destroy(c.gameObject);
            ui.spec.gameObject.SetActive(false);
            if (world) world.gameObject.SetActive(true);
            if (rainRoot) rainRoot.gameObject.SetActive(true);
            FitCam(); PlaceCam(true);
            ShowTitle();
        }
        void SpecClean()
        {
            if (spB != null) foreach (var u in spB) if (u != null) foreach (var m in u.hull) Destroy(m);
            if (spRoot) Destroy(spRoot.gameObject);
            spRoot = null; sp = null; spB = null; spG = null; spNum = null; spHead.Clear(); spPiers.Clear(); spDown = null; spDownOn = false;
            spLaneBar = spGate = spBridge = spBar = spDrift = spFerry = spFogBeam = spPreDock = null; spLine = null; spPileUI = null; spPileN = null; SpecClean2(); MixClean();
        }
        void SpecBack()
        {
            if (specOpen && mixOn) { int ci = mixI; MixLeave(); ShowMixMap(ci); return; }
            if (specOpen) { HideModal(); specOpen = false; SpecClean(); if (world) world.gameObject.SetActive(true); FitCam(); ShowSpecList(); }
            else if (mixMap) { HideModal(); ShowSpecList(); }
            else SpecExit();
        }

        // ---------- 进一张地图 ----------
        void SpecPlay(SpKind k, int seed, bool rule = true)
        {
            SpecClean(); mixOn = false;
            SpecStart(SpSim.Good(k, seed), rule);
        }
        // 进一张已经生成好的图（混合模式每一波也走这里）
        void SpecStart(SpSim s, bool rule)
        {
            SpecClean();
            sp = s; spKind = s.kind; spSeed = s.seed;
            foreach (Transform c in ui.spec) Destroy(c.gameObject);
            ui.spec.gameObject.SetActive(true);
            specOpen = true;
            if (world) world.gameObject.SetActive(false);
            if (rainRoot) rainRoot.gameObject.SetActive(false);
            HideModal();
            SpecFit(); SpecScene(); SpecHud();
            if (mixOn) { MixScene(); MixHud(); }
            if (rule) SpecRule();
        }

        void SpecRule()
        {
            Modal(false);
            ModalTitle(SpSim.NAMES[(int)spKind]);
            foreach (var s in SpSim.HOW[(int)spKind]) { var t = MText(s, 15, UIKit.Hex("5a3410")); t.alignment = TextAnchor.MiddleLeft; }
            MSpace(4);
            MButton("开始", "3fb950", () => { sfx.Play("click"); HideModal(); });
            FinishModal();
        }
        void SpecEnd(bool win)
        {
            Modal(false);
            ModalTitle(win ? "过关！" : "失败");
            MText(win ? F("用时 {0} 秒，风暴还剩 {1} 秒", Mathf.RoundToInt(sp.t), Mathf.RoundToInt(sp.timer)) : sp.why, 16, UIKit.Hex("5a3410"));
            MSpace(4);
            if (win) MButton("下一种地形", "3fb950", () => { sfx.Play("click"); SpecPlay((SpKind)(((int)spKind + 1) % SpSim.NAMES.Length), Random.Range(1, 100000)); });
            else MButton("这张图再来", "3fb950", () => { sfx.Play("click"); SpecPlay(spKind, sp.seed, false); });
            MButton("换一张图", "ffb81f", () => { sfx.Play("click"); SpecPlay(spKind, Random.Range(1, 100000), false); }, true, "6b3d0c");
            MButton("回到列表", "14608f", () => { sfx.Play("click"); SpecBack(); });
            FinishModal();
        }

        // 取景：整块舞台（宽 11、从岛顶到港湾底）塞进屏幕，顶上留给时间条
        void SpecFit()
        {
            Canvas.ForceUpdateCanvases();
            float Wd = Screen.width, Hd = Screen.height, yTop = Hd * (mixOn ? 0.86f : 0.905f), yBot = Hd * (mixOn ? 0.125f : 0.012f);
            float zTop = -12.2f, zBot = 10.6f, xs = 5.5f;
            if (spKind == SpKind.Base || spKind == SpKind.Twin || spKind == SpKind.Dual) zBot = 10.0f;
            var pts = new[] { W(-xs, zBot), W(xs, zBot), W(-xs, zTop), W(xs, zTop) };
            for (int it = 0; it < 50; it++)
            {
                PlaceCam(true);
                float x0 = 1e9f, x1 = -1e9f, y0 = 1e9f, y1 = -1e9f;
                foreach (var p in pts) { var v = cam.WorldToScreenPoint(p); x0 = Mathf.Min(x0, v.x); x1 = Mathf.Max(x1, v.x); y0 = Mathf.Min(y0, v.y); y1 = Mathf.Max(y1, v.y); }
                float s = Mathf.Min(Wd / (x1 - x0), (yTop - yBot) / (y1 - y0)), dy = (yBot + yTop) / 2 - (y0 + y1) / 2;
                float a = cam.WorldToScreenPoint(W(0, camZ)).y, b2 = cam.WorldToScreenPoint(W(0, camZ + 1)).y, pz = Mathf.Max(1, a - b2);
                camDist /= Mathf.Pow(s, 0.85f); camZ += dy / pz * 0.8f;
                if (Mathf.Abs(s - 1) < 0.003f && Mathf.Abs(dy) < 0.6f) break;
            }
            seaT.position = new Vector3(0, -0.03f, -camZ);
        }

        // ---------- 网格工具 ----------
        static List<Vector2> SpRound(float x, float z, float w, float h, float r)
        {
            r = Mathf.Min(r, Mathf.Min(w, h) / 2); var o = new List<Vector2>();
            var c = new[] { new Vector2(x + w - r, z + r), new Vector2(x + w - r, z + h - r), new Vector2(x + r, z + h - r), new Vector2(x + r, z + r) };
            for (int k = 0; k < 4; k++) for (int i = 0; i <= 6; i++) { float a = (-90 + k * 90 + i * 15) * Mathf.Deg2Rad; o.Add(c[k] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r); }
            return o;
        }
        static List<Vector2> SpEllipse(float cx, float cz, float a, float b)
        {
            var o = new List<Vector2>(); for (int k = 0; k < 72; k++) { float t = k * 5 * Mathf.Deg2Rad; o.Add(new Vector2(cx + Mathf.Cos(t) * a, cz + Mathf.Sin(t) * b)); }
            return o;
        }
        GameObject SpFlat(List<Vector2> o, List<Vector2> hole, float y, Material m, Transform parent = null)
        {
            var v = new List<Vector3>(); var t = new List<int>(); var n = new List<Vector3>(); int c = o.Count;
            if (hole == null)
            {
                var ctr = Vector2.zero; foreach (var p in o) ctr += p; ctr /= c;
                v.Add(W(ctr.x, ctr.y, y)); foreach (var p in o) v.Add(W(p.x, p.y, y));
                for (int k = 0; k < c; k++) { int a = k + 1, b = (k + 1) % c + 1; t.Add(0); t.Add(a); t.Add(b); t.Add(0); t.Add(b); t.Add(a); }
            }
            else
            {
                for (int k = 0; k < c; k++) { v.Add(W(o[k].x, o[k].y, y)); v.Add(W(hole[k].x, hole[k].y, y)); }
                for (int k = 0; k < c; k++) { int a = k * 2, b = a + 1, d = (k + 1) % c * 2, e = d + 1; t.AddRange(new[] { a, b, d, b, e, d, a, d, b, b, d, e }); }
            }
            foreach (var p in v) n.Add(Vector3.up);
            var ms = new Mesh(); ms.SetVertices(v); ms.SetNormals(n); ms.SetTriangles(t, 0); ms.RecalculateBounds();
            return SpMesh(ms, m, parent);
        }
        GameObject SpSkirt(List<Vector2> o, float y0, float y1, Material m, Transform parent = null)
        {
            var v = new List<Vector3>(); var t = new List<int>(); var n = new List<Vector3>(); int c = o.Count;
            var ctr = Vector2.zero; foreach (var p in o) ctr += p; ctr /= c;
            for (int k = 0; k < c; k++) { v.Add(W(o[k].x, o[k].y, y0)); v.Add(W(o[k].x, o[k].y, y1)); var d = (o[k] - ctr).normalized; n.Add(new Vector3(d.x, 0, -d.y)); n.Add(new Vector3(d.x, 0, -d.y)); }
            for (int k = 0; k < c; k++) { int a = k * 2, b = a + 1, d = (k + 1) % c * 2, e = d + 1; t.AddRange(new[] { a, b, d, b, e, d, a, d, b, b, d, e }); }
            var ms = new Mesh(); ms.SetVertices(v); ms.SetNormals(n); ms.SetTriangles(t, 0); ms.RecalculateBounds();
            return SpMesh(ms, m, parent);
        }
        GameObject SpMesh(Mesh ms, Material m, Transform parent)
        {
            var g = new GameObject("flat"); g.transform.SetParent(parent ? parent : spRoot, false);
            g.AddComponent<MeshFilter>().sharedMesh = ms; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }
        void SpIsle(List<Vector2> o, float y = 0.22f, Transform parent = null)
        {
            var wet = new List<Vector2>(); var ctr = Vector2.zero; foreach (var p in o) ctr += p; ctr /= o.Count;
            foreach (var p in o) { var d = p - ctr; wet.Add(p + d.normalized * 0.28f); }
            SpFlat(wet, null, 0.02f, Art.UnlitMat(UIKit.Hex("c8f4e8", 0.75f), true), parent);
            SpSkirt(o, -0.1f, y, Art.UnlitMat(UIKit.Hex(SP_SKIRT)), parent);
            SpFlat(o, null, y, Art.UnlitMat(UIKit.Hex(SP_SAND)), parent);
        }

        // ---------- 场景 ----------
        void SpecScene()
        {
            spRoot = new GameObject("Spec").transform;
            foreach (var r in sp.rects) SpRectView(r);
            foreach (var p in sp.props)
            {
                var g = Art.Spawn(p.model, spRoot, false); g.transform.position = W(p.x, p.z, p.model == "lighthouse" ? 0 : 0.2f); g.transform.rotation = Quaternion.Euler(0, p.yaw, 0); g.transform.localScale = Vector3.one * p.sc;
            }
            foreach (var d in sp.docks)
            {
                if (d.belt) continue;
                if (d.bay) { var bm = Art.UnlitMat(UIKit.Hex("ffffff", 0.3f), true); SpFlat(SpRound(d.x - 0.62f, d.z - 1.2f, 1.24f, 2.4f, 0.3f), null, 0.03f, bm); continue; }
                var f = SpSim.Fwd(d.yaw); var g = Art.Spawn("dock_berth", spRoot, false);
                var e = d.P + f * 1.1f; g.transform.position = W(e.x, e.y); g.transform.rotation = Quaternion.Euler(0, d.yaw * Mathf.Rad2Deg, 0);
                spPiers.Add(g);
                if (d.moving) g.transform.SetParent(spDrift.transform, true);
            }
            spB = new BoatV[sp.boats.Count]; spNum = new Text[sp.boats.Count]; spLit = new float[sp.boats.Count];
            foreach (var b in sp.boats) spB[b.id] = SpecBoat(b);
            spG = new Guy[sp.guys.Count]; spSeat = new bool[sp.guys.Count];
            foreach (var s in sp.guys)
            {
                var g = MakeGuy(s.c); g.g.transform.SetParent(spRoot, false); g.g.transform.localScale = Vector3.one * SP_GS; spG[s.id] = g;
                g.g.transform.position = W(s.p.x, s.p.y, SP_Y);
            }
            foreach (var q in sp.queues)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(c.GetComponent<Collider>()); c.transform.SetParent(spRoot, false);
                c.transform.localScale = new Vector3(0.62f, 0.008f, 0.62f); c.GetComponent<MeshRenderer>().sharedMaterial = Art.UnlitMat(Color.white); spHead.Add(c);
            }
            if (sp.kind == SpKind.Ferry)
            {
                spFerry = Art.Spawn("boat_raft", spRoot, true); spFerry.transform.localScale = Vector3.one * 1.5f;
                var ms = new List<Material>(); foreach (var r in spFerry.GetComponentsInChildren<Renderer>()) { var a = r.sharedMaterials; for (int i = 0; i < a.Length; i++) a[i] = Art.ColorMat("f4f6f8"); r.sharedMaterials = a; }
            }
            if (sp.kind == SpKind.Fog) SpecFog();
            if (sp.kind == SpKind.Loop) SpecChevrons();
            SpecScene2();
            var lg = new GameObject("line"); lg.transform.SetParent(spRoot, false); spLine = lg.AddComponent<LineRenderer>();
            spLine.sharedMaterial = Art.UnlitMat(UIKit.Hex("ffffff", 0.75f), true); spLine.startWidth = spLine.endWidth = 0.13f; spLine.numCornerVertices = 3; spLine.useWorldSpace = true; spLine.positionCount = 0;
            spLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; spLine.alignment = LineAlignment.TransformZ; lg.transform.rotation = Quaternion.Euler(90, 0, 0);
            spPreDock = SpFlat(SpRound(-0.62f, -1.2f, 1.24f, 2.4f, 0.3f), null, 0.04f, Art.UnlitMat(UIKit.Hex("fff27a", 0.55f), true)); spPreDock.SetActive(false);
        }

        void SpRectView(SpRect r)
        {
            switch (r.kind)
            {
                case "lagoon": SpFlat(SpRound(r.x, r.z, r.w, r.h, r.r), null, 0.01f, Art.UnlitMat(UIKit.Hex(SP_LAGOON, 0.55f), true)); break;
                case "sand": SpIsle(SpRound(r.x, r.z, r.w, r.h, r.r)); break;
                case "bar": spBar = new GameObject("bar"); spBar.transform.SetParent(spRoot, false); spBar.transform.position = W(r.x + r.w / 2, r.z + r.h / 2); SpIsle(SpRound(-r.w / 2, -r.h / 2, r.w, r.h, r.r), 0.2f, spBar.transform); break;
                case "drift": spDrift = new GameObject("drift"); spDrift.transform.SetParent(spRoot, false); SpIsle(SpRound(r.x, r.z, r.w, r.h, r.r), 0.2f, spDrift.transform); break;
                case "stone": { var o = SpRound(r.x, r.z, r.w, r.h, r.r); SpSkirt(o, -0.1f, 0.3f, Art.UnlitMat(UIKit.Hex("7d8894"))); SpFlat(o, null, 0.3f, Art.UnlitMat(UIKit.Hex(SP_STONE))); break; }
                case "gate": { spGate = new GameObject("gate"); spGate.transform.SetParent(spRoot, false); var o = SpRound(r.x, r.z, r.w, r.h, 0); SpSkirt(o, -0.1f, 0.45f, Art.UnlitMat(UIKit.Hex("c98a12")), spGate.transform); SpFlat(o, null, 0.45f, Art.UnlitMat(UIKit.Hex("ffc530")), spGate.transform); break; }
                case "lanebar": { spLaneBar = new GameObject("lanebar"); spLaneBar.transform.SetParent(spRoot, false); var o = SpRound(r.x, r.z, r.w, r.h, 0.05f); SpSkirt(o, -0.1f, 0.4f, Art.UnlitMat(UIKit.Hex("a82c22")), spLaneBar.transform); SpFlat(o, null, 0.4f, Art.UnlitMat(UIKit.Hex("e8402f")), spLaneBar.transform); break; }
                case "bay": SpFlat(SpRound(r.x, r.z, r.w, r.h, r.r), null, 0.01f, Art.UnlitMat(UIKit.Hex(SP_LAGOON, 0.55f), true)); break;
                case "dashv": for (float z = r.z + 0.2f; z < r.z + r.h - 0.3f; z += 0.9f) SpFlat(SpRound(r.x - 0.05f, z, 0.1f, 0.5f, 0.05f), null, 0.03f, Art.UnlitMat(UIKit.Hex("ffffff", 0.8f), true)); break;
                case "bridge":
                    {
                        spBridge = new GameObject("bridge"); spBridge.transform.SetParent(spRoot, false);
                        SpFlat(SpRound(r.x, r.z, r.w, r.h, 0), null, 0.32f, Art.UnlitMat(UIKit.Hex(SP_WOOD)), spBridge.transform);
                        for (float z = r.z + 0.15f; z < r.z + r.h; z += 0.42f) SpFlat(SpRound(r.x - 0.08f, z, r.w + 0.16f, 0.24f, 0), null, 0.36f, Art.UnlitMat(UIKit.Hex("d4a974")), spBridge.transform);
                        break;
                    }
                case "ring":
                    {
                        var o = SpEllipse(r.x, r.z, r.w, r.h); var h = SpEllipse(r.x, r.z, r.r, 8.5f);
                        SpSkirt(o, -0.1f, 0.22f, Art.UnlitMat(UIKit.Hex(SP_SKIRT))); SpFlat(o, h, 0.22f, Art.UnlitMat(UIKit.Hex(SP_SAND))); break;
                    }
                case "ringin": SpFlat(SpEllipse(r.x, r.z, r.w, r.h), null, 0.01f, Art.UnlitMat(UIKit.Hex(SP_LAGOON, 0.55f), true)); break;
                case "lane":
                    SpFlat(SpRound(r.x - 0.8f, r.z - 0.8f, r.w + 1.6f, r.h + 1.6f, 1.1f), SpRound(r.x + 0.8f, r.z + 0.8f, r.w - 1.6f, r.h - 1.6f, 0.6f), 0.01f, Art.UnlitMat(UIKit.Hex(SP_LAGOON, 0.55f), true)); break;
            }
        }

        void SpecChevrons()
        {
            var m = Art.UnlitMat(UIKit.Hex("ffffff", 0.6f), true);
            for (float s = 1; s < sp.trackLen; s += 3.3f)
            {
                var p = sp.TrackAt(s); var d = (sp.TrackAt(s + 0.1f) - p).normalized; var n = new Vector2(-d.y, d.x);
                SpFlat(new List<Vector2> { p + d * 0.3f - n * 0.02f, p + d * 0.3f + n * 0.02f, p - d * 0.25f + n * 0.3f, p - d * 0.1f, p - d * 0.25f - n * 0.3f }, null, 0.03f, m);
            }
        }

        void SpecFog()
        {
            SpFlat(SpRound(sp.hx - 0.1f, sp.hz - 0.1f, sp.cols * sp.cs + 0.2f, sp.rows * sp.cs + 0.2f, 0.5f), null, 0.05f, Art.UnlitMat(UIKit.Hex("dfe7ea", 0.93f), true));
            spFogBeam = new GameObject("beam"); spFogBeam.transform.SetParent(spRoot, false);
            spBeamMesh = new Mesh(); spBeamMesh.MarkDynamic();
            spFogBeam.AddComponent<MeshFilter>().sharedMesh = spBeamMesh;
            var r = spFogBeam.AddComponent<MeshRenderer>(); r.sharedMaterial = Art.UnlitMat(UIKit.Hex("ffe98a", 0.5f), true); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        static readonly Vector2 SP_LIGHT = new Vector2(4.6f, 10.1f);
        const float SP_BEAM = 17;
        void SpecBeam()
        {
            spBeamA = -135 + 48 * Mathf.Sin(sp.t * 0.55f);
            var v = new List<Vector3> { W(SP_LIGHT.x, SP_LIGHT.y, 0.07f) }; var t = new List<int>(); var n = new List<Vector3> { Vector3.up };
            for (int k = 0; k <= 12; k++) { float a = (spBeamA - SP_BEAM + k * SP_BEAM * 2 / 12) * Mathf.Deg2Rad; v.Add(W(SP_LIGHT.x + Mathf.Cos(a) * 16, SP_LIGHT.y + Mathf.Sin(a) * 16, 0.07f)); n.Add(Vector3.up); }
            for (int k = 1; k <= 12; k++) { t.Add(0); t.Add(k); t.Add(k + 1); t.Add(0); t.Add(k + 1); t.Add(k); }
            spBeamMesh.Clear(); spBeamMesh.SetVertices(v); spBeamMesh.SetNormals(n); spBeamMesh.SetTriangles(t, 0); spBeamMesh.RecalculateBounds();
        }

        BoatV SpecBoat(SpBoat B)
        {
            var u = new BoatV { b = B.id, ph = Random.value * 6.28f, yaw = B.yaw };
            var g = Art.Spawn(KIND[B.L], spRoot, true); u.g = g;
            foreach (var r in g.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                {
                    if (!ms[i]) continue;
                    if (ms[i].name.StartsWith("M_BoatHull")) { ms[i] = new Material(Art.Hull); u.hull.Add(ms[i]); }
                    else if (ms[i].name.StartsWith("M_BoatDeck")) { ms[i] = new Material(Art.Deck); u.deck.Add(ms[i]); }
                }
                r.sharedMaterials = ms;
            }
            u.ol = Art.AddOutline(g, Art.OutlineMat(Color.black));
            SetBoatColor(u, sp.kind == SpKind.Fog ? MYS : COLS[B.c]);
            var a = ARW[B.L]; var arw = Art.ArrowFor(a[0], a[1], a[2], a[3], a[4]);
            var ag = new GameObject("arrow"); ag.transform.SetParent(g.transform, false); ag.transform.localPosition = new Vector3(0, a[5], 0);
            ag.AddComponent<MeshFilter>().sharedMesh = arw.mesh;
            var ar = ag.AddComponent<MeshRenderer>(); ar.sharedMaterial = arw.mat; ar.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; ar.receiveShadows = false;
            u.arrows = new[] { ag };
            u.seats = new Transform[B.cap]; for (int k = 0; k < B.cap; k++) u.seats[k] = Art.Find(g.transform, "Seat_" + k);
            g.transform.position = W(B.p.x, B.p.y); g.transform.rotation = Quaternion.Euler(0, B.yaw * Mathf.Rad2Deg, 0); g.transform.localScale = Vector3.one * B.sc;
            return u;
        }

        // ---------- 顶栏 ----------
        void SpecHud()
        {
            var S = SafeRect(ui.spec, "safe");
            if (mixOn) MixTop(S);
            else
            {
                SpBack(S, () => { sfx.Play("click"); SpecBack(); });
                var help = UIKit.Panel(S, "help", UIKit.Hex("fff8e6"), 3, 2).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-10, -12), new Vector2(40, 40));
                UIKit.Txt(help, "t", "?", 22, UIKit.Hex("17306b")).rectTransform.Stretch(); UIKit.Btn(help, () => { sfx.Play("click"); SpecRule(); });
                UIKit.Txt(S, "n", mixOn ? MixTitle() : SpSim.NAMES[(int)spKind], 18, Color.white, TextAnchor.MiddleCenter, true).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(230, 24));
                var bar = UIKit.Panel(S, "bar", UIKit.Hex("1c2a52", 0.85f), 0, 2).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(210, 18));
                spBarR = bar;
                spFill = UIKit.Img(bar, "f", UIKit.Round, UIKit.Hex("8fc3ff")); var fr = spFill.rectTransform; fr.anchorMin = new Vector2(0, 0); fr.anchorMax = new Vector2(0, 1); fr.pivot = new Vector2(0, 0.5f); fr.offsetMin = new Vector2(2, 2); fr.offsetMax = new Vector2(2, -2);
                spTime = UIKit.Txt(bar, "t", "", 12, Color.white); spTime.rectTransform.Stretch();
                spInfo = UIKit.Txt(S, "i", "", 15, Color.white, TextAnchor.MiddleCenter, true); spInfo.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -58), new Vector2(340, 22));
                spTip = UIKit.Txt(S, "tip", "", 21, UIKit.Hex("fff27a"), TextAnchor.MiddleCenter, true); spTip.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -86), new Vector2(360, 30));
            }
            if (sp.kind == SpKind.Fog)
            {
                var b = SkinBtn(S, "ffb81f", "", 15, SpFlare, out spFlareT);
                b.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(8, mixOn ? 150 : 4), new Vector2(120, 44));
            }
            if (sp.kind == SpKind.Ferry)
            {
                spPileUI = new RectTransform[sp.piles.Count]; spPileN = new Text[sp.piles.Count];
                for (int k = 0; k < sp.piles.Count; k++)
                {
                    var r = OnSpec("pile"); r.sizeDelta = new Vector2(46, 22); spPileUI[k] = r;
                    UIKit.Img(r, "b", UIKit.Round, UIKit.Hex("17306b", 0.85f)).rectTransform.Stretch();
                    spPileN[k] = UIKit.Txt(r, "t", "", 14, Color.white); spPileN[k].rectTransform.Stretch();
                }
            }
            foreach (var b in sp.boats)
            {
                var r = OnSpec("num"); r.sizeDelta = new Vector2(30, 22);
                spNum[b.id] = UIKit.Txt(r, "t", b.cap.ToString(), b.len == 1 ? 13 : 16, Color.white, TextAnchor.MiddleCenter, true); spNum[b.id].rectTransform.Stretch();
            }
        }
        RectTransform OnSpec(string name) { var r = UIKit.Rect(name, ui.spec); r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.SetAsFirstSibling(); return r; }
        void SpFlare()
        {
            if (sp == null || sp.flares <= 0 || sp.flareT > 0) return;
            sp.flares--; sp.flareT = 5; sfx.Play("reveal");
        }
        void SpTipShow(string s) { spTip.text = s; spTipT = 2.2f; }

        // ---------- 每帧 ----------
        void SpecTick(float dt)
        {
            if (sp == null) return;
            if (!ModalOpen) for (int k = 0; k < spSteps; k++) { if (spBot) sp.BotTick(dt); sp.Tick(dt); }
            foreach (var e in sp.evs)
            {
                if (e[0] == '!') { SpTipShow(e.Substring(1)); continue; }
                switch (e)
                {
                    case "board": sfx.Play("pop" + Random.Range(0, 5)); break;
                    case "go": sfx.Play("go"); break;
                    case "bump": sfx.Play("bad"); Haptics.Play(Haptics.Kind.Medium); break;
                    case "leave": sfx.Play("sail"); break;
                    case "gate": sfx.Play("unlock"); break;
                    case "break": sfx.Play("rumble"); shake = 0.5f; if (spLaneBar) spLaneBar.SetActive(false); if (spBridge) { var bt = spBridge.transform; Fx.Run(() => Fx.Tween(500, p => { if (bt) { bt.position = Vector3.down * p * 1.2f; if (p >= 1) bt.gameObject.SetActive(false); } })); } break;
                    case "win": sfx.Play("fanfare"); if (mixOn) { var s1 = sp; Fx.Run(async () => { await Fx.Sleep(700); if (sp == s1) MixWaveDone(); }); } else if (!spBot) { var s0 = sp; Fx.Run(async () => { await Fx.Sleep(700); if (sp == s0) SpecEnd(true); }); } break;
                    case "lose": sfx.Play("fail"); if (mixOn) { if (!spBot) { var s1 = sp; Fx.Run(async () => { await Fx.Sleep(600); if (sp == s1) MixLose(); }); } } else if (!spBot) { var s0 = sp; Fx.Run(async () => { await Fx.Sleep(600); if (sp == s0) SpecEnd(false); }); } break;
                    default: if (!MixEv(e)) SpecEv2(e); break;
                }
            }
            sp.evs.Clear();
            SpecSync(dt);
        }

        void SpecSync(float dt)
        {
            float now = Fx.Now;
            if (spFogBeam) SpecBeam();
            for (int i = 0; i < spB.Length; i++)
            {
                var u = spB[i]; if (u == null) continue;
                var b = sp.boats[i]; var n = spNum[i];
                if (b.st == SpSim.GONE)
                {
                    foreach (var g in sp.guys) if (g.boat == i && spG[g.id] != null) spG[g.id] = null;
                    Destroy(u.g); spB[i] = null; if (n) n.gameObject.SetActive(false); continue;
                }
                var tr = u.g.transform; var p = b.p;
                if (b.bump > 0) p += SpSim.Fwd(b.yaw) * Mathf.Sin((1 - b.bump) * Mathf.PI) * b.bumpD;
                u.yaw = Fx.LerpAng(u.yaw, b.yaw, Mathf.Min(1, dt * 14));
                tr.position = W(p.x, p.y, Mathf.Sin(now / 600 + u.ph) * 0.025f);
                tr.rotation = Quaternion.Euler(0, u.yaw * Mathf.Rad2Deg, 0);
                float tsc = b.fade && b.st == SpSim.LEAVE ? 0.02f : b.sc;
                tr.localScale = Vector3.one * Mathf.MoveTowards(tr.localScale.x, tsc, dt * (b.fade ? 2 : 3));
                if (b.warp && b.st == SpSim.MOVE) { float wd = Mathf.Min((b.p - sp.Cell(sp.wr[0], sp.wc[0])).magnitude, (b.p - sp.Cell(sp.wr[1], sp.wc[1])).magnitude); tr.localScale = Vector3.one * b.sc * Mathf.Clamp(wd / 1.1f, 0.05f, 1); }
                bool lot = b.st == SpSim.LOT;
                bool arw = lot && (sp.kind != SpKind.Sail || (b.dir + sp.wind) % 2 == 0);   // 风帆港：顺风的船才有箭头，箭头跟着风
                if (u.arrows[0].activeSelf != arw) u.arrows[0].SetActive(arw);
                if (arw && sp.kind == SpKind.Sail) u.arrows[0].transform.localRotation = Quaternion.Euler(0, b.dir == sp.wind ? 0 : 180, 0);
                if (n)
                {
                    bool show = lot && !(sp.kind == SpKind.Fog && spLit[i] <= 0);
                    if (n.gameObject.activeSelf != show) n.gameObject.SetActive(show);
                    if (show) n.rectTransform.parent.GetComponent<RectTransform>().anchoredPosition = ToCanvas(cam.WorldToScreenPoint(tr.position - tr.forward * b.sc * (b.L * 0.5f - 0.08f) + Vector3.up * 0.4f));
                }
                if (sp.kind == SpKind.Fog)
                {
                    bool lit = !lot || sp.flareT > 0 || InBeam(b.p);
                    float was = spLit[i]; spLit[i] = lit ? 0.6f : Mathf.Max(0, was - dt);   // 光扫过去后还亮一小会儿
                    if ((was > 0) != (spLit[i] > 0)) SetBoatColor(u, spLit[i] > 0 ? COLS[b.c] : MYS);
                }
            }
            foreach (var s in sp.guys)
            {
                var v = spG[s.id]; if (v == null) continue;
                var tr = v.g.transform;
                if (s.st == 2)
                {
                    if (spSeat[s.id]) continue;
                    spSeat[s.id] = true; var u = spB[s.boat]; if (u == null) continue;
                    var seat = s.seat < u.seats.Length ? u.seats[s.seat] : null; var par = seat ? seat : u.g.transform;
                    tr.SetParent(par, true); tr.position = par.position + Vector3.down * 0.05f; tr.rotation = u.g.transform.rotation; v.body.localPosition = Vector3.zero;
                    tr.localScale = Vector3.one; tr.localScale = Vector3.one * (0.62f / Mathf.Max(0.01f, tr.lossyScale.x));
                    continue;
                }
                var np = W(s.p.x, s.p.y, SP_Y);
                bool vis = !(s.st == 0 && s.q >= 0 && s.s >= sp.queues[s.q].len - 0.02f);
                if (v.g.activeSelf != vis) v.g.SetActive(vis);
                if (s.st == 1) np.y = SP_Y + Mathf.Sin(Mathf.Clamp01(s.hop) * Mathf.PI) * 0.7f;
                var dv = np - tr.position; dv.y = 0; tr.position = np;
                if (dv.sqrMagnitude > 1e-6f)
                {
                    v.yaw = Fx.LerpAng(v.yaw, Mathf.Atan2(dv.x, dv.z), 0.3f); v.walk += dt * 16;
                    v.body.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(v.walk)) * 0.09f, 0);
                }
                else v.body.localPosition = new Vector3(0, Mathf.Max(0, Mathf.Sin(now / 300 + v.ph)) * 0.03f, 0);
                tr.rotation = Quaternion.Euler(0, v.yaw * Mathf.Rad2Deg, 0);
            }
            for (int k = 0; k < spHead.Count; k++)
            {
                var q = sp.queues[k]; bool on = q.guys.Count > 0; if (spHead[k].activeSelf != on) spHead[k].SetActive(on);
                if (on) { var h = q.guys[0].p; spHead[k].transform.position = W(h.x, h.y, 0.235f); }
            }
            if (spGate) { var q = spGate.transform.position; q.y = Mathf.MoveTowards(q.y, sp.gateOpen ? -0.7f : 0, dt * 3); spGate.transform.position = q; }
            if (spDrift) { spDrift.transform.position = new Vector3(sp.driftX - sp.driftMid, 0, 0); }
            if (spBar) { float k = sp.barDone ? 1 : Mathf.Lerp(0.62f, 1, sp.barT / Mathf.Max(1, sp.barMax)); spBar.transform.localScale = new Vector3(k, 1, k); }
            if (spFerry)
            {
                var tr = spFerry.transform; var np = W(sp.ferryP.x, sp.ferryP.y); var d = np - tr.position; tr.position = np;
                if (d.sqrMagnitude > 1e-6f) tr.rotation = Quaternion.Lerp(tr.rotation, Quaternion.LookRotation(d, Vector3.up), 0.2f);
                for (int k = 0; k < sp.piles.Count; k++)
                {
                    var p = sp.piles[k]; bool on = p.guys.Count > 0; if (spPileUI[k].gameObject.activeSelf != on) spPileUI[k].gameObject.SetActive(on);
                    spPileUI[k].anchoredPosition = ToCanvas(cam.WorldToScreenPoint(W(p.x, p.z + 1.75f, 0.3f)));
                    spPileN[k].text = (sp.ferrySel == k && sp.ferrySt != 0 ? "去" : sp.ferryNext == k ? "下趟" : "") + p.guys.Count;
                }
            }
            // 顶栏
            float f = Mathf.Clamp01(sp.timer / SpSim.TIME_CAP);
            spFill.rectTransform.sizeDelta = new Vector2(Mathf.Max(0, (spBarR.rect.width - 4) * f), -4);
            spTime.text = Mathf.CeilToInt(sp.timer) + "秒" + (sp.timeRate > 1 ? " ×" + sp.timeRate.ToString("0.##") : "");
            int left = sp.PeopleLeft() + MixLater();
            string info = MixPre() + "还有 " + left + " 人";
            switch (sp.kind)
            {
                case SpKind.Sluice: info = (sp.gateOpen ? "闸门开着 " + Mathf.CeilToInt(sp.gateT) + " 秒" : "再走 " + sp.gateLeft + " 艘开闸") + " · " + info; break;
                case SpKind.Bridge: info = (sp.broken ? "桥断了" : "桥还能过 " + sp.bridgeLeft + " 人") + " · " + info; break;
                case SpKind.Bar: info = (sp.barDone ? "沙洲救完了" : "沙洲 " + Mathf.CeilToInt(sp.barT) + " 秒") + " · " + info; break;
                case SpKind.Ring: info = "转了 " + sp.laps + " 圈 · " + info; break;
            }
            spInfo.text = SpecInfo2(info);
            SpecSync2(dt);
            if (mixOn) MixSync(dt);
            if (spFlareT) spFlareT.text = sp.flareT > 0 ? "亮着 " + Mathf.CeilToInt(sp.flareT) : "照明弹 ×" + sp.flares;
            spTipT -= dt; if (spTipT <= 0 && spTip.text.Length > 0) spTip.text = "";
        }
        bool InBeam(Vector2 p)
        {
            var d = p - SP_LIGHT; float a = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            return Mathf.Abs(Mathf.DeltaAngle(a, spBeamA)) < SP_BEAM;
        }

        // ---------- 输入：按下先亮航线，松开才开船 ----------
        void SpecInput()
        {
            if (AutoTest.Wanted || sp == null || ModalOpen) return;
            bool down, up; Vector2 pos; int id = -1;
            if (Input.touchCount > 0) { var t = Input.GetTouch(0); down = t.phase == TouchPhase.Began; up = t.phase == TouchPhase.Ended; pos = t.position; id = t.fingerId; }
            else { down = Input.GetMouseButtonDown(0); up = Input.GetMouseButtonUp(0); pos = Input.mousePosition; }
            if (down)
            {
                if (UnityEngine.EventSystems.EventSystem.current && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(id)) return;
                spDownOn = true; spDown = SpecPick(pos); SpecPreview(spDown);
            }
            if (up && spDownOn)
            {
                spDownOn = false; SpecPreview(null);
                var b = SpecPick(pos);
                if (b != null && b == spDown) { if (mixCrane) MixCraneTap(b); else sp.Tap(b); }
                else if (spDown == null && sp.kind == SpKind.Ferry) SpecPileTap(pos);
                spDown = null;
            }
        }
        public void SpecTapAt(Vector2 pos)
        {
            var b = SpecPick(pos); if (b != null) { sp.Tap(b); return; }
            if (sp.kind == SpKind.Ferry) SpecPileTap(pos);
        }
        bool SpGround(Vector2 sc, out Vector2 q)
        {
            var ray = cam.ScreenPointToRay(sc); q = Vector2.zero;
            if (!new Plane(Vector3.up, new Vector3(0, 0.25f, 0)).Raycast(ray, out float en)) return false;
            var p = ray.GetPoint(en); q = new Vector2(p.x, -p.z); return true;
        }
        SpBoat SpecPick(Vector2 sc)
        {
            if (!SpGround(sc, out var q)) return null;
            SpBoat best = null; float bs = 0.75f;
            foreach (var b in sp.boats)
            {
                if (b.st != SpSim.LOT) continue;
                var f = SpSim.Fwd(b.yaw); var d = q - b.p; float along = Vector2.Dot(d, f), side = Vector2.Dot(d, new Vector2(-f.y, f.x));
                float ea = Mathf.Max(0, Mathf.Abs(along) - (b.L - 0.16f) / 2 * b.sc), es = Mathf.Max(0, Mathf.Abs(side) - 0.42f * b.sc), dist = Mathf.Sqrt(ea * ea + es * es);
                if (dist > 0.5f) continue;
                float score = dist + (sp.Probe(b).res == SpSim.OK ? 0 : 0.3f);
                if (score < bs) { bs = score; best = b; }
            }
            return best;
        }
        void SpecPileTap(Vector2 sc)
        {
            if (!SpGround(sc, out var q) || q.y > -7.6f) return;
            int best = -1; float bd = 9;
            for (int k = 0; k < sp.piles.Count; k++) { if (sp.piles[k].guys.Count == 0) continue; float d = Mathf.Abs(q.x - sp.piles[k].x); if (d < bd) { bd = d; best = k; } }
            if (best >= 0) sp.TapPile(best);
        }
        void SpecPreview(SpBoat b)
        {
            spLine.positionCount = 0; spLine2.positionCount = 0; spPreDock.SetActive(false);
            if (b == null) return;
            var g = sp.Probe(b);
            if (sp.kind == SpKind.Turn && b.disc)
            {
                sp.TurnPreview(b, out var c, out var fw); spLine2.positionCount = 2;
                spLine2.SetPosition(0, W(c.x, c.y, 0.5f)); spLine2.SetPosition(1, W(c.x + fw.x * 2.4f, c.y + fw.y * 2.4f, 0.5f));
            }
            if (g.res != SpSim.OK || g.path == null) return;
            if (g.path2 != null) { spLine2.positionCount = g.path2.Count; for (int k = 0; k < g.path2.Count; k++) spLine2.SetPosition(k, W(g.path2[k].x, g.path2[k].y, 0.5f)); }
            spLine.positionCount = g.path.Count;
            for (int k = 0; k < g.path.Count; k++) spLine.SetPosition(k, W(g.path[k].x, g.path[k].y, 0.5f));
            if (g.dock < 0) return;
            var d = sp.docks[g.dock]; spPreDock.SetActive(true);
            spPreDock.transform.position = W(d.x, d.z); spPreDock.transform.rotation = Quaternion.Euler(0, d.yaw * Mathf.Rad2Deg, 0);
        }
    }
}
