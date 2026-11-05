// 特殊模式第二批的画面：风向标、转盘、漩涡、彩虹门、货轮、绳子、回转码头。规则在 SpecSim2.cs。
using System.Collections.Generic;
using UnityEngine;

namespace Tide
{
    public partial class Game
    {
        static readonly string[] SP_DIR = { "上", "右", "下", "左" }, SP_COL = { "红", "蓝", "黄", "绿", "紫" };
        Transform spDial, spDialNext, spDisc, spWave; GameObject[] spSail; Transform[] spSpin, spExit, spTruckV, spPads;
        readonly List<Transform> spStreak = new List<Transform>(); readonly List<LineRenderer> spRopes = new List<LineRenderer>(); readonly List<SpBoat> spRopeB = new List<SpBoat>();
        MeshRenderer spPaintBar, spPaintNext, spPaintLane; Material[] spPaintM, spPaintA; LineRenderer spLine2;

        void SpecClean2()
        {
            spDial = spDialNext = spDisc = spWave = null; spSail = null; spSpin = spExit = spTruckV = spPads = null;
            spStreak.Clear(); spRopes.Clear(); spRopeB.Clear(); spPaintBar = spPaintNext = spPaintLane = null; spLine2 = null;
        }

        // ---------- 小零件 ----------
        Transform SpNode(string name, Vector2 p) { var t = new GameObject(name).transform; t.SetParent(spRoot, false); t.position = W(p.x, p.y); return t; }
        static List<Vector2> SpArrowPoly(float s) => new List<Vector2> { new Vector2(0, -s), new Vector2(s * 0.62f, -s * 0.1f), new Vector2(s * 0.22f, -s * 0.1f), new Vector2(s * 0.22f, s), new Vector2(-s * 0.22f, s), new Vector2(-s * 0.22f, -s * 0.1f), new Vector2(-s * 0.62f, -s * 0.1f) };
        static Quaternion SpYaw(int d) => Quaternion.Euler(0, d * 90, 0);
        LineRenderer SpNewLine(string hex, float a, float w, Transform parent = null, bool world = true)
        {
            var lg = new GameObject("line"); lg.transform.SetParent(parent ? parent : spRoot, false); lg.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var l = lg.AddComponent<LineRenderer>(); l.sharedMaterial = Art.UnlitMat(UIKit.Hex(hex, a), a < 1);
            l.startWidth = l.endWidth = w; l.numCornerVertices = 3; l.useWorldSpace = world; l.positionCount = 0; l.alignment = LineAlignment.TransformZ;
            l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return l;
        }
        void SpDialMake(float x, float z, string hex)
        {
            var t = SpNode("dial", new Vector2(x, z));
            SpFlat(SpEllipse(0, 0, 1.02f, 1.02f), null, 0.06f, Art.UnlitMat(UIKit.Hex("14324d")), t);
            SpFlat(SpEllipse(0, 0, 0.9f, 0.9f), null, 0.07f, Art.UnlitMat(Color.white), t);
            spDial = new GameObject("arrow").transform; spDial.SetParent(t, false);
            SpFlat(SpArrowPoly(0.72f), null, 0.09f, Art.UnlitMat(UIKit.Hex(hex)), spDial);
            var n = SpNode("next", new Vector2(x - 1.3f, z + 0.5f));
            SpFlat(SpEllipse(0, 0, 0.4f, 0.4f), null, 0.06f, Art.UnlitMat(UIKit.Hex("ffffff", 0.75f), true), n);
            spDialNext = new GameObject("arrow").transform; spDialNext.SetParent(n, false);
            SpFlat(SpArrowPoly(0.28f), null, 0.08f, Art.UnlitMat(UIKit.Hex("14324d")), spDialNext);
        }
        void SpBurst(Vector2 p, string hex, float size)
        {
            var t = SpNode("burst", p); t.position = W(p.x, p.y, 0.75f);
            var o = new List<Vector2>(); for (int k = 0; k < 16; k++) { float a = k * Mathf.PI / 8, s = k % 2 == 0 ? 1 : 0.45f; o.Add(new Vector2(Mathf.Cos(a) * s, Mathf.Sin(a) * s)); }
            SpFlat(o, null, 0, Art.UnlitMat(UIKit.Hex(hex)), t);
            Fx.Run(() => Fx.Tween(280, k => { if (t) { t.localScale = Vector3.one * (0.3f + k * size); if (k >= 1) Destroy(t.gameObject); } }));
        }

        // ---------- 场景 ----------
        void SpecScene2()
        {
            spLine2 = SpNewLine("fff27a", 0.8f, 0.13f);
            switch (sp.kind)
            {
                case SpKind.Sail: SceneSail(); break;
                case SpKind.Turn: SceneTurn(); break;
                case SpKind.Whirl: SceneWhirl(); break;
                case SpKind.Paint: ScenePaint(); break;
                case SpKind.Truck: SceneTruck(); break;
                case SpKind.Tilt: SceneTilt(); break;
                case SpKind.Rope: SceneRope(); break;
                case SpKind.Sushi: SceneSushi(); break;
            }
        }
        void SceneSail()
        {
            SpDialMake(4.1f, -2.35f, "ff7a3c");
            spSail = new GameObject[sp.boats.Count];
            foreach (var b in sp.boats)
            {
                var g = new GameObject("sail"); g.transform.SetParent(spB[b.id].g.transform, false); spSail[b.id] = g;
                var n = new GameObject("c").transform; n.SetParent(g.transform, false); n.localPosition = new Vector3(0, 0, ARW[b.L][0] - 0.05f);
                var oo = new List<Vector2>(); var ii = new List<Vector2>();
                for (int k = 0; k <= 8; k++) { float x = (k - 4) / 4f, e = 1 - x * x; oo.Add(new Vector2(x * 0.46f, -0.42f * e)); ii.Add(new Vector2(x * 0.46f, -0.14f * e)); }
                SpFlat(oo, ii, 0.62f, Art.UnlitMat(Color.white), n);
                SpFlat(SpEllipse(0, 0, 0.07f, 0.07f), null, 0.64f, Art.UnlitMat(UIKit.Hex("7a5a34")), n);
            }
            var mt = Art.UnlitMat(UIKit.Hex("ffffff", 0.55f), true);
            for (int k = 0; k < 10; k++) { var t = new GameObject("streak").transform; t.SetParent(spRoot, false); SpFlat(SpRound(-0.04f, -0.7f, 0.08f, 1.4f, 0.04f), null, 0.03f, mt, t); spStreak.Add(t); }
        }
        void SceneTurn()
        {
            float h = sp.dN * sp.cs / 2;
            spDisc = SpNode("disc", sp.discC);
            SpFlat(SpRound(-h, -h, 2 * h, 2 * h, 0.7f), null, 0.02f, Art.UnlitMat(UIKit.Hex("bff3f6", 0.8f), true), spDisc);
            SpFlat(SpRound(-h - 0.08f, -h - 0.08f, 2 * h + 0.16f, 2 * h + 0.16f, 0.75f), SpRound(-h + 0.05f, -h + 0.05f, 2 * h - 0.1f, 2 * h - 0.1f, 0.65f), 0.03f, Art.UnlitMat(Color.white), spDisc);
            var mk = Art.UnlitMat(UIKit.Hex("ff7a3c"));
            for (int k = 0; k < 4; k++) { var a = new GameObject("mark").transform; a.SetParent(spDisc, false); a.localRotation = SpYaw(k); SpFlat(SpRound(-0.5f, -h + 0.14f, 1f, 0.16f, 0.08f), null, 0.04f, mk, a); }
        }
        void SceneWhirl()
        {
            spSpin = new Transform[2]; spExit = new Transform[2];
            string[] hex = { "2a7fb5", "c98a12" };
            for (int i = 0; i < 2; i++)
            {
                var t = SpNode("whirl", sp.Cell(sp.wr[i], sp.wc[i])); float r = sp.cs * 0.52f;
                SpFlat(SpEllipse(0, 0, r, r), null, 0.03f, Art.UnlitMat(UIKit.Hex(hex[i])), t);
                var s = new GameObject("spin").transform; s.SetParent(t, false); s.localPosition = Vector3.up * 0.05f; spSpin[i] = s;
                for (int arm = 0; arm < 3; arm++)
                {
                    var l = SpNewLine("ffffff", 1, 0.09f, s, false); l.positionCount = 12;
                    for (int k = 0; k < 12; k++) { float a = (arm * 120 + k * 22) * Mathf.Deg2Rad, q = r * (0.12f + 0.8f * k / 11); l.SetPosition(k, new Vector3(Mathf.Cos(a) * q, Mathf.Sin(a) * q, 0)); }
                }
                var e = new GameObject("exit").transform; e.SetParent(t, false); spExit[i] = e;
                var c = new GameObject("arrow").transform; c.SetParent(e, false); c.localPosition = new Vector3(0, 0, sp.cs * 0.72f);
                SpFlat(SpArrowPoly(0.46f), null, 0.6f, Art.UnlitMat(UIKit.Hex("ffe45e")), c);
            }
        }
        void ScenePaint()
        {
            float x0 = sp.hx + sp.pc0 * sp.cs, w = sp.pcN * sp.cs, z = sp.hz - 0.26f;
            spPaintM = new Material[5]; spPaintA = new Material[5];
            for (int k = 0; k < 5; k++) { spPaintM[k] = Art.UnlitMat(UIKit.Hex(COLS[k])); spPaintA[k] = Art.UnlitMat(UIKit.Hex(COLS[k], 0.3f), true); }
            spPaintLane = SpFlat(SpRound(x0, sp.hz - 0.1f, w, sp.rows * sp.cs + 0.2f, 0.2f), null, 0.012f, spPaintA[0]).GetComponent<MeshRenderer>();
            foreach (float x in new[] { x0 - 0.3f, x0 + w - 0.12f })
            {
                var o = SpRound(x, z - 0.21f, 0.42f, 0.42f, 0.08f); SpSkirt(o, -0.1f, 0.85f, Art.UnlitMat(UIKit.Hex("5a6470"))); SpFlat(o, null, 0.85f, Art.UnlitMat(UIKit.Hex("aab3bb")));
            }
            var bo = SpRound(x0, z - 0.3f, w, 0.6f, 0.1f); SpSkirt(bo, 0.55f, 0.8f, Art.UnlitMat(UIKit.Hex("14324d")));
            SpFlat(bo, null, 0.8f, Art.UnlitMat(Color.white));
            spPaintBar = SpFlat(SpRound(x0 + 0.08f, z - 0.22f, w - 0.16f, 0.44f, 0.08f), null, 0.82f, spPaintM[0]).GetComponent<MeshRenderer>();
            var nt = SpEllipse(x0 + w + 0.75f, z, 0.4f, 0.4f);
            SpFlat(SpEllipse(x0 + w + 0.75f, z, 0.52f, 0.52f), null, 0.05f, Art.UnlitMat(Color.white));
            spPaintNext = SpFlat(nt, null, 0.06f, spPaintM[0]).GetComponent<MeshRenderer>();
        }
        void SceneTruck()
        {
            var z = sp.laneZ;
            SpFlat(SpRound(-9, z - 1.2f, 18, 2.4f, 0), null, 0.012f, Art.UnlitMat(UIKit.Hex("1f6fa8", 0.75f), true));
            var dm = Art.UnlitMat(UIKit.Hex("ffffff", 0.7f), true);
            foreach (float e in new[] { -1.2f, 1.2f }) for (float x = -8.5f; x < 8.5f; x += 1.4f) SpFlat(SpRound(x, z + e - 0.04f, 0.8f, 0.08f, 0.04f), null, 0.02f, dm);
            spTruckV = new Transform[sp.trucks.Count];
            string[] hull = { "59636f", "7a4d2f", "37506b" }, box = { "ff9a3c", "3d8bff", "3dbd58", "ffc530", "ff5548" };
            for (int k = 0; k < sp.trucks.Count; k++)
            {
                var t = sp.trucks[k]; var n = SpNode("truck", new Vector2(0, 0)); spTruckV[k] = n;
                var o = SpRound(-t.len / 2, z - 0.68f, t.len, 1.36f, 0.6f);
                SpSkirt(o, -0.1f, 0.5f, Art.UnlitMat(UIKit.Hex(hull[k % 3])), n); SpFlat(o, null, 0.5f, Art.UnlitMat(UIKit.Hex("dfe5ec")), n);
                int cnt = Mathf.FloorToInt((t.len - 1f) / 0.95f);
                for (int i = 0; i < cnt; i++)
                {
                    float x = -t.len / 2 + 0.55f + i * 0.95f; var c = SpRound(x, z - 0.48f, 0.85f, 0.96f, 0.04f); string hx = box[(k * 2 + i) % 5];
                    SpSkirt(c, 0.5f, 1.05f, Art.UnlitMat(UIKit.Hex(hx) * 0.8f), n); SpFlat(c, null, 1.05f, Art.UnlitMat(UIKit.Hex(hx)), n);
                }
            }
        }
        void SceneTilt()
        {
            SpDialMake(4.1f, -2.35f, "2f86c8");
            float w = sp.cols * sp.cs + 1;
            spWave = SpNode("wave", Vector2.zero); SpFlat(SpRound(-w / 2, -0.5f, w, 1f, 0.5f), null, 0.04f, Art.UnlitMat(UIKit.Hex("ffffff", 0.6f), true), spWave);
            spWave.gameObject.SetActive(false);
        }
        void SceneRope()
        {
            foreach (var b in sp.boats)
            {
                if (b.mate <= b.id) continue;
                var l = SpNewLine("7a5230", 1, 0.13f); l.positionCount = 7; spRopes.Add(l); spRopeB.Add(b);
                SpMark(b.id); SpMark(b.mate);
            }
        }
        // 有绳子的船：船头系一个木环，一眼看出它不是普通船
        void SpMark(int id)
        {
            var g = new GameObject("knot"); g.transform.SetParent(spB[id].g.transform, false); g.transform.localPosition = new Vector3(0, 0.45f, 0);
            SpFlat(SpEllipse(0, 0, 0.22f, 0.22f), SpEllipse(0, 0, 0.11f, 0.11f), 0, Art.UnlitMat(UIKit.Hex("7a5230")), g.transform);
        }
        void SceneSushi()
        {
            var o = SpRound(-5.15f, -8.65f, 10.3f, 5.9f, 2.95f); var h = SpRound(-3.85f, -7.35f, 7.7f, 3.3f, 1.65f);
            SpSkirt(o, -0.1f, 0.06f, Art.UnlitMat(UIKit.Hex("8a6238"))); SpFlat(o, h, 0.06f, Art.UnlitMat(UIKit.Hex(SP_WOOD)));
            spPads = new Transform[sp.docks.Count]; var pm = Art.UnlitMat(UIKit.Hex("d4a974"));
            for (int k = 0; k < sp.docks.Count; k++)
            {
                var d = sp.docks[k]; var n = SpNode("pad", d.P); spPads[k] = n;
                SpFlat(SpRound(-0.5f, -0.9f, 1f, 1.8f, 0.2f), null, 0.075f, pm, n);
            }
            var sm = Art.UnlitMat(UIKit.Hex("fff27a", 0.7f), true);
            foreach (var q in sp.queues) SpFlat(SpEllipse(q.station.x, q.station.y, 1f, 1f), SpEllipse(q.station.x, q.station.y, 0.82f, 0.82f), 0.09f, sm);
        }

        // ---------- 每帧 ----------
        void SpecSync2(float dt)
        {
            foreach (int id in sp.dirty) SpecDirty(id);
            sp.dirty.Clear();
            foreach (var p in sp.crash) { SpBurst(p, "ffe45e", 1.6f); shake = 0.18f; }
            sp.crash.Clear();
            foreach (var p in sp.merged) SpBurst(p, "ffffff", 1.9f);
            sp.merged.Clear();
            float now = Fx.Now;
            switch (sp.kind)
            {
                case SpKind.Sail:
                    SpDialTurn(sp.wind, sp.NextWind, dt);
                    {
                        var f = SpSim.Fwd(SpSim.YawDir(sp.wind));
                        for (int k = 0; k < spStreak.Count; k++)
                        {
                            float s = Mathf.Repeat(now / 1000 * 6 + k * 2.3f, 22) - 11, o = (k - 4.5f) * 1.25f;
                            var q = f * s + new Vector2(-f.y, f.x) * o + new Vector2(0, 3.8f);
                            spStreak[k].position = W(q.x, q.y); spStreak[k].rotation = SpYaw(sp.wind);
                        }
                    }
                    foreach (var b in sp.boats)
                    {
                        if (spB[b.id] == null) continue;
                        bool on = b.st == SpSim.LOT || (b.st == SpSim.MOVE);
                        var g = spSail[b.id]; if (g.activeSelf != on) g.SetActive(on);
                        if (on) { bool full = b.st != SpSim.LOT || (b.dir + sp.wind) % 2 == 0; float y = Mathf.MoveTowards(g.transform.localScale.z, full ? 1 : 0.25f, dt * 3); g.transform.localScale = new Vector3(1, 1, y); if (b.st == SpSim.LOT && full) g.transform.localRotation = SpYaw(b.dir == sp.wind ? 0 : 2); }
                    }
                    break;
                case SpKind.Turn:
                    if (spDisc) spDisc.rotation = Quaternion.Euler(0, (sp.turnCount - 1 + Mathf.SmoothStep(0, 1, sp.turnK)) * 90, 0);
                    break;
                case SpKind.Whirl:
                    for (int i = 0; i < 2; i++) { spSpin[i].Rotate(0, dt * 200, 0, Space.World); spExit[i].rotation = Quaternion.RotateTowards(spExit[i].rotation, SpYaw(sp.PDir(i)), dt * 400); }
                    break;
                case SpKind.Paint:
                    spPaintBar.sharedMaterial = spPaintM[sp.GateColor]; spPaintNext.sharedMaterial = spPaintM[sp.GateNext]; spPaintLane.sharedMaterial = spPaintA[sp.GateColor];
                    if (spDownOn && spDown != null && spDown.dir == 0 && spDown.col >= sp.pc0 && spDown.col < sp.pc0 + sp.pcN && sp.Probe(spDown).res == SpSim.OK) { spTip.text = "现在松手，变" + SP_COL[sp.GateColorAt((spDown.p.y - sp.hz) / 15)] + "色"; spTipT = 0.2f; }
                    break;
                case SpKind.Truck:
                    for (int k = 0; k < spTruckV.Length; k++) spTruckV[k].position = W(sp.TruckX(sp.trucks[k], sp.truckT), 0);
                    if (spDownOn && spDown != null) { var g = sp.Probe(spDown); if (g.res == SpSim.OK) { spTip.text = sp.TruckSafe(spDown, g, 0.42f) ? "现在松手：过得去" : "现在松手：会撞"; spTipT = 0.2f; } }
                    break;
                case SpKind.Tilt:
                    SpDialTurn(sp.TiltNext, sp.tiltSeq[(sp.tiltI + 1) % sp.tiltSeq.Length], dt);
                    break;
                case SpKind.Rope:
                    for (int k = 0; k < spRopes.Count; k++)
                    {
                        var a = spRopeB[k]; var b = sp.boats[a.mate]; bool on = a.st == SpSim.LOT && b.st == SpSim.LOT;
                        if (spRopes[k].gameObject.activeSelf != on) spRopes[k].gameObject.SetActive(on);
                        if (!on) continue;
                        float bob = Mathf.Sin(now / 500 + k) * 0.08f;
                        for (int i = 0; i < 7; i++) { float t = i / 6f, s = Mathf.Sin(t * Mathf.PI); var p = Vector2.Lerp(a.p, b.p, t) + new Vector2(0, s * (0.35f + bob)); spRopes[k].SetPosition(i, W(p.x, p.y, 0.62f)); }
                    }
                    break;
                case SpKind.Sushi:
                    for (int k = 0; k < spPads.Length; k++) { var d = sp.docks[k]; spPads[k].position = W(d.x, d.z); spPads[k].rotation = Quaternion.Euler(0, d.yaw * Mathf.Rad2Deg, 0); }
                    break;
            }
        }
        void SpDialTurn(int d, int next, float dt)
        {
            spDial.rotation = Quaternion.RotateTowards(spDial.rotation, SpYaw(d), dt * 500);
            spDialNext.rotation = SpYaw(next);
        }
        void SpecDirty(int id)
        {
            var b = sp.boats[id]; var u = spB[id]; if (u == null) return;
            if (sp.kind == SpKind.Paint) { SetBoatColor(u, COLS[b.c]); SpBurst(b.p, COLS[b.c], 1.2f); return; }
            if (sp.kind != SpKind.Merge) return;
            foreach (var m in u.hull) Destroy(m);
            Destroy(u.g);
            var nu = SpecBoat(b); nu.yaw = u.yaw; spB[id] = nu; nu.g.transform.localScale = Vector3.one * b.sc * 1.35f;
            var n = spNum[id]; if (n) { n.text = b.cap.ToString(); n.fontSize = 16; }
        }
        void SpecEv2(string e)
        {
            switch (e)
            {
                case "crash": sfx.Play("rumble"); Haptics.Play(Haptics.Kind.Medium); break;
                case "merge": sfx.Play("unlock"); Haptics.Play(Haptics.Kind.Medium); break;
                case "warp": sfx.Play("reveal"); break;
                case "turn": sfx.Play("rumble"); break;
                case "slosh": sfx.Play("sail"); shake = 0.15f; SpWaveRun(); break;
            }
        }
        void SpWaveRun()
        {
            if (!spWave) return;
            int d = sp.tiltI > 0 ? sp.tiltSeq[(sp.tiltI - 1) % sp.tiltSeq.Length] : 0; var t = spWave;
            var f = SpSim.Fwd(SpSim.YawDir(d)); var c = new Vector2(0, sp.hz + sp.rows * sp.cs / 2);
            float ex = (d % 2 == 0 ? sp.rows : sp.cols) * sp.cs / 2 + 0.5f;
            t.gameObject.SetActive(true); t.rotation = SpYaw(d);
            Fx.Run(() => Fx.Tween(420, k => { if (t) { var p = c + f * Mathf.Lerp(-ex, ex, k); t.position = W(p.x, p.y); if (k >= 1) t.gameObject.SetActive(false); } }));
        }
        // 顶栏多的一句
        string SpecInfo2(string info)
        {
            switch (sp.kind)
            {
                case SpKind.Sail: return "风向" + SP_DIR[sp.wind] + " · 下一阵" + SP_DIR[sp.NextWind] + " · " + info;
                case SpKind.Turn: return "再走 " + sp.turnLeft + " 艘转盘转 · " + info;
                case SpKind.Paint: return "门是" + SP_COL[sp.GateColor] + "色 · 下一个" + SP_COL[sp.GateNext] + " · " + info;
                case SpKind.Tilt: return "再走 " + sp.tiltLeft + " 艘水往" + SP_DIR[sp.TiltNext] + "晃 · " + info;
                case SpKind.Sushi: { int n = 0; foreach (var d in sp.docks) if (d.boat >= 0) n++; return "转盘 " + n + "/" + sp.docks.Count + " · " + info; }
            }
            return info;
        }
    }
}
