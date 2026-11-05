// 特殊模式的规则层（10-30 用户要先试玩 13 种地形，好了再决定要不要进主线）。
// 和主线 Rules.cs 完全分开，主线 100 关一行规则都不动。
// 所有东西（船走到哪、人走到哪、谁上了哪条船）都在 Tick 里算，界面（SpecMode.cs）只照着画；
// 这样同一份规则可以不开画面让机器人快速打一遍（SpecLevels.cs 的 Verify），确认每张图解得开。
// 坐标和主线一样：x 向右，z 向下（屏幕上），画的时候 W(x, z) 换成世界坐标。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tide
{
    public enum SpKind { Base, Twin, Dual, Cross, Canal, Ring, Sluice, Bridge, Bar, Drift, Loop, Fog, Ferry, Sail, Turn, Bump, Merge, Whirl, Paint, Truck, Tilt, Rope, Sushi }

    public class SpBoat
    {
        public int id, len, cap, c, dir, r, col, isl;
        public int st, dock = -1, seat, load;
        public Vector2 p; public float yaw, sc = 1, s, bump, bumpD, stay, ts, trav;
        public List<Vector2> path; public float[] cum; public float speed = 15;
        public bool inner, track, fade;
        // 第二批（SpecSim2.cs）：mlen 合体后的船型（格子还是 len）；mergeTo 正在开去合体的那艘；inc 有几艘正在开过来；mate 绳子另一头
        public int mlen, mergeTo = -1, inc, mate = -1, want = -1; public float lotV = 8; public bool disc, gate, cross, back, warp; public List<Vector2> next, home;
        public int L => mlen > 0 ? mlen : len;
        // 混合模式（MixSim.cs）：白船 / 海藻 / 锁链 / 被直升机吊着
        public bool mys, seen, crane; public int weed, lockBy = -1, key = -1;
    }
    public class SpDock
    {
        public float x, z, yaw, ts, ringS, cd; public int isl, boat = -1; public bool bay, on = true, moving, fade, belt;
        public Vector2[] via; public Vector2 exit;
        public Vector2 P => new Vector2(x, z);
        // 船头靠着栈桥：长短不同的船，船身中心离栈桥的距离不一样
        public Vector2 At(int len) => bay || belt ? P : P + SpSim.Fwd(yaw) * (1.1f - (len - 0.16f) * SpSim.DockSc(len) / 2);
    }
    public class SpGuy { public int id, c, st, q = -1, boat = -1, seat; public float s, hop, hopT; public Vector2 p, from, off; public bool pole, vault; }
    public class SpQueue
    {
        public int isl; public List<SpGuy> guys = new List<SpGuy>(); public Vector2[] path; public float[] cum; public float len, gap = 0.46f, cd, ox;
        public Vector2 station; public int F = -1; public float jump;   // 断桥：前 F 个位子在桥这头，后面的位子跳过桥面（没人站在桥上）
        public bool over;
        public void SetPath(Vector2[] pts) { path = pts; cum = new float[pts.Length]; for (int k = 1; k < pts.Length; k++) cum[k] = cum[k - 1] + Vector2.Distance(pts[k - 1], pts[k]); len = cum[cum.Length - 1]; }
        public float Slot(int i) => Mathf.Min(len, i * gap + (F >= 0 && i >= F ? jump : 0));
        public Vector2 At(float s) => SpSim.PathAt(path, cum, s) + new Vector2(ox, 0);
    }
    public class SpIsland { public string name; public List<int> queues = new List<int>(), docks = new List<int>(); public bool over; }
    public class SpPile { public int c; public float x, z; public List<SpGuy> guys = new List<SpGuy>(); }
    public class SpRect { public string kind; public float x, z, w, h, r; public SpRect(string k, float x, float z, float w, float h, float r = 0.8f) { kind = k; this.x = x; this.z = z; this.w = w; this.h = h; this.r = r; } }
    public class SpProp { public string model; public float x, z, yaw, sc; }
    public struct SpGo { public int res, dock, merge; public List<Vector2> path, path2; public float bump; public string why; }

    public partial class SpSim
    {
        public const int LOT = 0, MOVE = 1, DOCK = 2, LEAVE = 3, GONE = 4;
        public const int OK = 0, BLOCK = 1, FULL = 2, GATE = 3, NONE = -1;
        public const float TIME_START = 60, TIME_CAP = 99, TIME_PER = 0.75f;
        public static readonly int[] DR = { -1, 0, 1, 0 }, DC = { 0, 1, 0, -1 };

        public SpKind kind; public int seed;
        public int rows, cols, status;   // status：0 在玩 1 赢 2 输
        public float cs = 1, hx, hz, t, timer = TIME_START, timeRate = 1, idle, winT; public bool started; public string why = "";
        public int[,] mask, occ;   // mask：1 水，0 石堤（挡路），2 岸（开到这里就算出港）
        public List<SpBoat> boats = new List<SpBoat>(); public List<SpDock> docks = new List<SpDock>(); public List<SpQueue> queues = new List<SpQueue>();
        public List<SpIsland> isls = new List<SpIsland>(); public List<SpGuy> guys = new List<SpGuy>(); public List<int> sol = new List<int>();
        public List<SpRect> rects = new List<SpRect>(); public List<SpProp> props = new List<SpProp>(); public List<string> evs = new List<string>();
        // 水闸
        public int wallRow = -1, gate0, gateNeed = 3, gateLeft; public float gateT, gateDur = 6; public bool gateOpen;
        // 断桥
        public int bridgeLeft, laneRows; public bool broken; public float sBridgeEnd;
        // 沙洲
        public int barDock = -1, barColor, barQ; public float barT, barMax; public bool barDone;
        // 漂流岛
        public float driftX, driftW = 3.2f, driftMid, driftAmp; public int driftQ;
        // 环礁：人沿椭圆走；ringA/ringB 是港湾的水边
        public Vector2 ringC; public float ringA, ringB, ringLen, ringV = 1.6f, ringTrav; public Vector2[] ringPts; public float[] ringCum; public int laps;
        // 港中岛：船沿矩形航道顺时针走
        public Vector2[] trackPts; public float[] trackCum; public float trackLen, trackGap = 1.75f, trackV = 7;
        // 中转岛
        public List<SpPile> piles = new List<SpPile>(); public int ferrySt, ferrySel = -1, ferryNext = -1, ferryCap = 4; public Vector2 ferryP, ferryHome, ferryFrom, ferryTo; public float ferryK, ferryDur = 1.1f; public List<SpGuy> ferryLoad = new List<SpGuy>();
        // 雾港
        public int flares = 2; public float flareT;

        public void Ev(string s) => evs.Add(s);

        // ---------- 几何 ----------
        public Vector2 Cell(int r, int c) => new Vector2(hx + (c + 0.5f) * cs, hz + (r + 0.5f) * cs);
        public Vector2 Center(SpBoat b) => (Cell(b.r, b.col) + Cell(b.r + DR[b.dir] * (b.len - 1), b.col + DC[b.dir] * (b.len - 1))) / 2;
        public static float YawDir(int d) => d * Mathf.PI / 2;
        public static Vector2 Fwd(float yaw) => new Vector2(Mathf.Sin(yaw), -Mathf.Cos(yaw));
        public static float YawOf(Vector2 d) => Mathf.Atan2(d.x, -d.y);
        public static float DockSc(int len) => Mathf.Min(0.86f, 2.2f / (len - 0.16f));
        public float LotSc(SpBoat b) => kind == SpKind.Loop ? Mathf.Min(0.72f, 1.5f / b.len) : cs;
        public static Vector2 PathAt(Vector2[] pts, float[] cum, float s)
        {
            if (s <= 0) return pts[0];
            for (int k = 1; k < pts.Length; k++)
                if (s <= cum[k]) return Vector2.Lerp(pts[k - 1], pts[k], (s - cum[k - 1]) / Mathf.Max(1e-4f, cum[k] - cum[k - 1]));
            return pts[pts.Length - 1];
        }
        static float[] Cum(List<Vector2> pts) { var c = new float[pts.Count]; for (int k = 1; k < pts.Count; k++) c[k] = c[k - 1] + Vector2.Distance(pts[k - 1], pts[k]); return c; }

        void Go(SpBoat b, List<Vector2> path, int st) { b.path = path; b.cum = Cum(path); b.s = 0; b.st = st; }

        // 沿着港湾外面一圈走：从 a 绕到 b，走近的那边
        float RX0 => hx - cs * 0.5f; float RZ0 => hz - cs * 0.5f; float RW => cols * cs + cs; float RH => rows * cs + cs;
        float PerT(Vector2 p)
        {
            float x0 = RX0, z0 = RZ0, w = RW, h = RH, x1 = x0 + w, z1 = z0 + h;
            float dt = Mathf.Abs(p.y - z0), db = Mathf.Abs(p.y - z1), dl = Mathf.Abs(p.x - x0), dr = Mathf.Abs(p.x - x1), m = Mathf.Min(dt, Mathf.Min(db, Mathf.Min(dl, dr)));
            if (m == dt) return Mathf.Clamp(p.x - x0, 0, w);
            if (m == dr) return w + Mathf.Clamp(p.y - z0, 0, h);
            if (m == db) return w + h + Mathf.Clamp(x1 - p.x, 0, w);
            return w + h + w + Mathf.Clamp(z1 - p.y, 0, h);
        }
        Vector2 PerPt(float t)
        {
            float x0 = RX0, z0 = RZ0, w = RW, h = RH, P = 2 * (w + h); t = Mathf.Repeat(t, P);
            if (t <= w) return new Vector2(x0 + t, z0);
            if (t <= w + h) return new Vector2(x0 + w, z0 + t - w);
            if (t <= w + h + w) return new Vector2(x0 + w - (t - w - h), z0 + h);
            return new Vector2(x0, z0 + h - (t - w - h - w));
        }
        void Around(Vector2 a, Vector2 b, List<Vector2> pts)
        {
            float w = RW, h = RH, P = 2 * (w + h), ta = PerT(a), tb = PerT(b), fw = Mathf.Repeat(tb - ta, P), bk = P - fw;
            var cor = new List<float> { 0, w, w + h, w + h + w };
            if (fw <= bk) { cor.Sort((x, y) => Mathf.Repeat(x - ta, P).CompareTo(Mathf.Repeat(y - ta, P))); foreach (var c in cor) if (Mathf.Repeat(c - ta, P) < fw) pts.Add(PerPt(c)); }
            else { cor.Sort((x, y) => Mathf.Repeat(ta - x, P).CompareTo(Mathf.Repeat(ta - y, P))); foreach (var c in cor) if (Mathf.Repeat(ta - c, P) < bk) pts.Add(PerPt(c)); }
            pts.Add(PerPt(tb));
        }
        List<Vector2> Route(Vector2 start, Vector2 exit, SpDock d, int len)
        {
            var pts = new List<Vector2> { start };
            if ((exit - start).sqrMagnitude > 1e-4f) pts.Add(exit);
            var f = Fwd(d.yaw);
            if (kind == SpKind.Truck) { Around(exit, new Vector2(d.x, RZ0), pts); pts.Add(d.At(len)); return pts; }
            if (kind == SpKind.Sushi) { Around(exit, new Vector2(beltWait.x, RZ0), pts); pts.Add(beltWait); pts.Add(d.At(len)); return pts; }
            if (kind == SpKind.Ring)
            {
                float a0 = Mathf.Atan2((exit.y - ringC.y) / ringB, (exit.x - ringC.x) / ringA), a1 = Mathf.Atan2((d.z - ringC.y) / ringB, (d.x - ringC.x) / ringA);
                float da = Mathf.DeltaAngle(a0 * Mathf.Rad2Deg, a1 * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                for (int k = 1; k <= 8; k++) { float a = a0 + da * k / 8; pts.Add(ringC + new Vector2(Mathf.Cos(a) * ringA * 0.86f, Mathf.Sin(a) * ringB * 0.86f)); }
            }
            else
            {
                var entry = d.via != null ? d.via[0] : d.P - f * 1.5f;
                Around(exit, entry, pts);
                if (d.via != null) pts.AddRange(d.via); else pts.Add(d.P - f * 1.5f);
            }
            pts.Add(d.At(len));
            return pts;
        }

        // ---------- 点船 ----------
        bool Ray(SpBoat b, out Vector2 exit, out float room)
        {
            int r = b.r + DR[b.dir] * (b.len - 1), c = b.col + DC[b.dir] * (b.len - 1); room = 0;
            for (; ; )
            {
                r += DR[b.dir]; c += DC[b.dir];
                if (r < 0 || c < 0 || r >= rows || c >= cols) { exit = Cell(r, c); return true; }
                int m = mask[r, c];
                if (m == 2) { exit = Cell(r, c); return true; }
                if (m == 0) { exit = Cell(r - DR[b.dir], c - DC[b.dir]); return kind == SpKind.Sluice && b.inner; }
                if (m == 3) return PortalAny(r, c, out exit);
                if (occ[r, c] >= 0) { exit = Vector2.zero; return false; }
                room += cs;
            }
        }
        public int FreeDock(int isl, Vector2 near)
        {
            int best = -1; float bd = 1e9f;
            foreach (int k in isls[isl].docks)
            {
                var d = docks[k];
                if (d.boat >= 0 || !d.on || d.bay || k == barDock) continue;
                float dd = (d.P - near).sqrMagnitude;
                if (dd < bd) { bd = dd; best = k; }
            }
            return best;
        }
        int FreeBay() { for (int k = 0; k < docks.Count; k++) if (docks[k].bay && docks[k].boat < 0) return k; return -1; }
        public bool Head(int c) { foreach (var q in queues) if (q.guys.Count > 0 && q.guys[0].c == c) return true; return false; }
        public int TwinSide(int dir, int col) => dir == 3 ? 0 : dir == 1 ? 1 : (col < cols / 2 ? 0 : 1);
        public bool LaneBoat(int dir, int r) => dir == 1 && r < laneRows;
        public bool BarBoat(int dir, int r) => dir == 1 && r < rows / 2;

        SpGo Probe0(SpBoat b)
        {
            var g = new SpGo { dock = -1 };
            if (b.st != LOT) { g.res = NONE; return g; }
            if (kind == SpKind.Loop) return ProbeLoop(b);
            if (b.dock >= 0)   // 侧湾里的船：直接去码头
            {
                int d0 = FreeDock(0, b.p);
                if (d0 < 0) { g.res = FULL; g.bump = 0.3f; g.why = "码头满了"; return g; }
                g.dock = d0; g.path = Route(b.p, b.p, docks[d0], b.len); return g;
            }
            if (!Ray(b, out var exit, out float room)) { g.res = BLOCK; g.bump = room + 0.12f; return g; }
            float mouth = room + 0.45f;
            int tgt = -1; string full = "码头满了";
            switch (kind)
            {
                case SpKind.Twin: tgt = FreeDock(TwinSide(b.dir, b.col), exit); full = TwinSide(b.dir, b.col) == 0 ? "左岛码头满了" : "右岛码头满了"; break;
                case SpKind.Cross:
                    if (isls[b.dir].over) { g.res = FULL; g.bump = mouth; g.why = isls[b.dir].name + "已经救完了"; return g; }
                    tgt = FreeDock(b.dir, exit); full = isls[b.dir].name + "码头满了"; break;
                case SpKind.Canal: { int bay = FreeBay(); tgt = !Head(b.c) && bay >= 0 ? bay : FreeDock(0, exit); if (tgt < 0 && bay >= 0) tgt = bay; break; }
                case SpKind.Sluice:
                    tgt = FreeDock(0, exit);
                    if (b.inner)
                    {
                        if (!gateOpen) { g.res = GATE; g.bump = room + 0.12f; g.why = "闸门还关着"; return g; }
                        for (int r = 0; r < wallRow; r++) if (occ[r, gate0] >= 0) { g.res = BLOCK; g.bump = room + 0.12f; g.why = "闸门前面有船挡着"; return g; }
                        if (tgt < 0) break;
                        float lz = Cell(wallRow + 1, 0).y, gx = hx + (gate0 + 1) * cs;
                        var pts = new List<Vector2> { b.p, exit }; if (b.dir != 0) pts.Add(new Vector2(exit.x, lz));
                        pts.Add(new Vector2(gx, lz)); var top = new Vector2(gx, RZ0); pts.Add(top);
                        var dd = docks[tgt]; var f = Fwd(dd.yaw); Around(top, dd.P - f * 1.5f, pts); pts.Add(dd.P - f * 1.5f); pts.Add(dd.At(b.len));
                        g.dock = tgt; g.path = pts; return g;
                    }
                    break;
                case SpKind.Bridge:
                    if (LaneBoat(b.dir, b.r))
                    {
                        if (!broken) { g.res = GATE; g.bump = room + 0.3f; g.why = "桥断了这条水道才通"; return g; }
                        tgt = FreeDock(1, exit); full = "后岛码头满了";
                    }
                    else tgt = FreeDock(0, exit);
                    break;
                case SpKind.Bar:
                    if (BarBoat(b.dir, b.r) && !barDone) { tgt = docks[barDock].boat < 0 ? barDock : -1; full = "沙洲泊位被占着"; }
                    else tgt = FreeDock(0, exit);
                    break;
                case SpKind.Drift: tgt = OverDrift(b) ? FreeDock(1, exit) : -1; if (tgt < 0) tgt = FreeDock(0, exit); break;
                case SpKind.Sushi: tgt = BeltSlot(b.p); full = "转盘满了"; break;
                default: tgt = FreeDock(0, exit); break;
            }
            if (tgt < 0) { g.res = FULL; g.bump = mouth; g.why = full; return g; }
            g.dock = tgt; g.path = Route(b.p, exit, docks[tgt], b.L);
            return g;
        }
        public bool OverDrift(SpBoat b) => b.dir == 0 && Mathf.Abs(Center(b).x - driftX) < driftW / 2;

        public SpGo Tap(SpBoat b)
        {
            var g = Probe(b);
            if (g.res == NONE) return g;
            if (g.res != OK) { b.bump = 1; b.bumpD = g.bump; if (kind == SpKind.Rope && b.mate >= 0) { boats[b.mate].bump = 1; boats[b.mate].bumpD = 0.2f; } Ev("bump"); if (g.why != null) Ev("!" + g.why); return g; }
            started = true; idle = 0; goneN++;
            if (kind >= SpKind.Sail && Tap2(b, g)) return g;
            if (kind == SpKind.Loop) { b.st = MOVE; b.trav = 0; Ev("go"); return g; }
            bool fromGrid = b.dock < 0;
            if (!fromGrid) docks[b.dock].boat = -1;
            else for (int k = 0; k < b.len; k++) occ[b.r + DR[b.dir] * k, b.col + DC[b.dir] * k] = -1;
            b.dock = g.dock; docks[g.dock].boat = b.id; b.sc = DockSc(b.L);
            Go(b, g.path, MOVE); Ev("go");
            if (kind >= SpKind.Sail) After2(b, g);
            if (fromGrid && kind == SpKind.Canal) Slide(b.col);
            if (fromGrid && kind == SpKind.Sluice && !b.inner) OuterGone();
            return g;
        }

        // 长水道：走了一艘，下面的船往前挪
        void Slide(int col)
        {
            var list = new List<SpBoat>();
            foreach (var b in boats) if (b.st == LOT && b.dock < 0 && b.col == col) list.Add(b);
            list.Sort((a, b) => a.r.CompareTo(b.r));
            for (int r = 0; r < rows; r++) if (mask[r, col] == 1) occ[r, col] = -1;
            int cur = 0;
            foreach (var b in list) { b.r = cur + b.len - 1; for (int k = 0; k < b.len; k++) occ[cur + k, col] = b.id; cur += b.len; }
        }

        void OuterGone()
        {
            if (gateOpen) return;
            gateLeft--;
            int rest = 0; foreach (var b in boats) if (b.st == LOT && !b.inner) rest++;
            if (gateLeft <= 0 || rest == 0) { gateOpen = true; gateT = gateDur; Ev("gate"); Ev("!闸门开了！"); }
        }
        void TickGate(float dt)
        {
            if (!gateOpen) return;
            int rest = 0; foreach (var b in boats) if (b.st == LOT && !b.inner) rest++;
            if (rest == 0) { gateT = gateDur; return; }   // 外港空了：闸门不再关
            gateT -= dt;
            if (gateT <= 0) { gateOpen = false; gateLeft = Mathf.Min(gateNeed, rest); Ev("gate"); }
        }

        // ---------- 港中岛：顺时针航道 ----------
        public Vector2 TrackAt(float s) => PathAt(trackPts, trackCum, Mathf.Repeat(s, trackLen));
        float Fwd2(float a, float b) => Mathf.Repeat(b - a, trackLen);
        SpBoat Ahead(SpBoat b, out float dist)
        {
            SpBoat best = null; dist = 1e9f;
            foreach (var o in boats) { if (o == b || !o.track) continue; float d = Fwd2(b.ts, o.ts); if (d < dist) { dist = d; best = o; } }
            return best;
        }
        SpGo ProbeLoop(SpBoat b)
        {
            var g = new SpGo { dock = -1 };
            var a = Ahead(b, out float d);
            if (a != null && a.st == LOT && d < trackGap + 0.08f) { g.res = BLOCK; g.bump = 0.3f; return g; }
            int best = -1; float bd = 1e9f;
            for (int k = 0; k < docks.Count; k++) { var dk = docks[k]; if (dk.boat >= 0) continue; float f = Fwd2(b.ts, dk.ts); if (f < bd) { bd = f; best = k; } }
            if (best < 0) { g.res = FULL; g.bump = 0.3f; g.why = "码头满了"; return g; }
            g.dock = best; g.path = new List<Vector2> { b.p };
            for (float s = 0.8f; s < bd; s += 0.8f) g.path.Add(TrackAt(b.ts + s));
            g.path.Add(TrackAt(b.ts + bd)); g.path.Add(docks[best].At(b.len));
            return g;
        }
        void TickLoop(float dt)
        {
            foreach (var b in boats)
            {
                if (!b.track) continue;
                if (b.st == MOVE)
                {
                    float step = trackV * dt, ns = b.ts + step;
                    var a = Ahead(b, out float d);
                    if (a != null && d - step < trackGap)
                    {
                        if (a.st == LOT) { step = Mathf.Max(0, d - trackGap); ns = b.ts + step; b.st = LOT; }
                        else { step = Mathf.Max(0, d - trackGap); ns = b.ts + step; }
                    }
                    for (int k = 0; k < docks.Count; k++)
                    {
                        var dk = docks[k]; if (dk.boat >= 0) continue;
                        if (Fwd2(b.ts, dk.ts) <= step + 0.01f)
                        {
                            dk.boat = b.id; b.dock = k; b.track = false; b.sc = DockSc(b.len);
                            b.ts = ns; b.p = TrackAt(b.ts);
                            Go(b, new List<Vector2> { b.p, dk.At(b.len) }, MOVE); break;
                        }
                    }
                    if (!b.track) continue;
                    b.trav += step; b.ts = Mathf.Repeat(ns, trackLen);
                    if (b.trav >= trackLen) b.st = LOT;
                }
                b.p = TrackAt(b.ts);
                b.yaw = YawOf(TrackAt(b.ts + 0.1f) - b.p);
            }
        }

        // ---------- 每帧 ----------
        public void Tick(float dt)
        {
            if (status == 1 || status == 2) { foreach (var b in boats) TickBoat(b, dt); return; }
            t += dt;
            if (kind == SpKind.Drift) TickDrift(dt);
            if (kind == SpKind.Sluice) TickGate(dt);
            if (kind == SpKind.Loop) TickLoop(dt);
            if (kind == SpKind.Ring) TickRing(dt);
            if (kind == SpKind.Ferry) TickFerry(dt);
            if (kind == SpKind.Bar) TickBar(dt);
            if (kind >= SpKind.Sail) Tick2(dt);
            if (mix) MixTick(dt);
            flareT = Mathf.Max(0, flareT - dt);
            bool busy = kind >= SpKind.Sail && Busy2;
            foreach (var b in boats)
            {
                TickBoat(b, dt);
                if (b.st == MOVE) busy = true;
                if (b.st == DOCK && b.load >= b.cap) Leave(b);
            }
            for (int k = 0; k < queues.Count; k++) TickQueue(k, dt);
            foreach (var g in guys)
                if (g.st == 1)
                {
                    busy = true;
                    var b = boats[g.boat]; g.hop += dt / g.hopT; g.p = Vector2.Lerp(g.from, b.p, g.hop);
                    if (g.hop >= 1) { g.st = 2; b.load++; }
                }
            if (started && clock) { timer -= dt * timeRate; if (timer <= 0) { timer = 0; End(2, "风暴来了"); return; } }
            CheckEnd(busy, dt);
        }

        void TickBoat(SpBoat b, float dt)
        {
            b.bump = Mathf.Max(0, b.bump - dt / 0.3f);
            if (b.st == LOT && b.dock < 0 && !b.track) { if (kind == SpKind.Turn && b.disc && turnK < 1) return; b.p = Vector2.MoveTowards(b.p, Center(b), b.lotV * dt); return; }
            if (b.st != MOVE && b.st != LEAVE)
            {
                if (b.dock >= 0 && docks[b.dock].moving) { var dm = docks[b.dock]; b.p = dm.At(b.L); if (dm.belt) b.yaw = dm.yaw; }
                return;
            }
            if (b.path == null) return;
            if (b.st == MOVE && b.dock >= 0 && docks[b.dock].moving) { b.path[b.path.Count - 1] = docks[b.dock].At(b.L); b.cum = Cum(b.path); }
            if (kind == SpKind.Sushi && b.st == MOVE && b.dock >= 0 && BeltHold(b)) return;
            float total = b.cum[b.cum.Length - 1];
            b.s += b.speed * dt;
            var np = PathAt(b.path.ToArray(), b.cum, b.s);
            if ((np - b.p).sqrMagnitude > 1e-6f) b.yaw = YawOf(np - b.p);
            b.p = np;
            if (b.gate && b.p.y <= hz) { b.gate = false; if (b.c != GateColor) { b.c = GateColor; dirty.Add(b.id); } Ev("go"); }
            if (b.s >= total)
            {
                if (kind >= SpKind.Sail && End2(b)) return;
                if (b.st == LEAVE) { b.st = GONE; return; }
                var d = docks[b.dock];
                b.p = d.At(b.L); b.yaw = d.yaw; b.path = null; b.warp = false; b.cross = false; b.speed = 15;
                b.st = d.bay ? LOT : DOCK;
            }
        }

        void Leave(SpBoat b)
        {
            var d = docks[b.dock]; d.boat = -1; var f = Fwd(d.yaw); var p = b.p; var pts = new List<Vector2> { p };
            b.fade = d.fade;
            if (d.fade) pts.Add(p + f * 1.2f);
            else if (d.exit != Vector2.zero) pts.Add(p + d.exit);
            else { var q = p - f * 1.5f; pts.Add(q); pts.Add(new Vector2((p.x < 0 ? -1 : 1) * 9, q.y)); }
            b.dock = -1; b.speed = d.fade ? 3 : 13;
            Go(b, pts, LEAVE); Ev("leave");
        }

        void TickQueue(int k, float dt)
        {
            var q = queues[k];
            for (int i = 0; i < q.guys.Count; i++)
            {
                var g = q.guys[i]; float ts = q.Slot(i);
                if (g.s > ts || (mix && g.s < ts - 0.01f)) g.s = Mathf.MoveTowards(g.s, ts, dt * Mathf.Max(5, Mathf.Abs(g.s - ts) * 5));
                g.p = q.At(g.s);
            }
            q.cd -= dt;
            if (q.cd > 0 || q.guys.Count == 0) return;
            var h = q.guys[0];
            if (h.s > 0.15f) return;
            foreach (int di in isls[q.isl].docks)
            {
                var d = docks[di]; if (d.boat < 0 || d.bay) continue;
                var b = boats[d.boat];
                if (b.st != DOCK || b.c != h.c || b.seat >= b.cap) continue;
                if (kind == SpKind.Sushi && (d.P - q.station).sqrMagnitude > 1.7f) continue;   // 回转码头：转到队头面前才上
                q.guys.RemoveAt(0); Board(h, b); q.cd = 0.085f;
                if (kind == SpKind.Bridge && !broken) { bridgeLeft--; if (bridgeLeft <= 0) BreakBridge(); }
                if (q.guys.Count == 0) QueueOver(k);
                return;
            }
        }
        void Board(SpGuy g, SpBoat b)
        {
            g.st = 1; g.from = g.p; g.boat = b.id; g.seat = b.seat++; g.hop = 0; g.hopT = 0.16f + Vector2.Distance(g.p, b.p) * 0.03f;
            timer = Mathf.Min(TIME_CAP, timer + timePer); idle = 0; Ev("board");
            if (g.pole && mix) { g.pole = false; PoleFire(g, b); }
        }
        void QueueOver(int k)
        {
            var q = queues[k]; if (q.over) return;
            if (kind == SpKind.Bridge && k == 0 && !broken) return;
            if (kind == SpKind.Ferry) return;   // 中转岛：队排空了，远岛还可能再送人来
            q.over = true;
            if (kind == SpKind.Dual) { bool first = true; foreach (var o in queues) if (o != q && o.over) first = false; if (first) { timer = Mathf.Min(TIME_CAP, timer + 10); Ev("!一条队清空！风暴 +10 秒"); } }
            if (kind == SpKind.Bar && k == barQ) { barDone = true; timer = Mathf.Min(TIME_CAP, timer + 20); Ev("!沙洲救完！风暴 +20 秒"); return; }
            var isl = isls[q.isl]; bool all = true; foreach (int qi in isl.queues) if (queues[qi].guys.Count > 0) all = false;
            if (all && !isl.over) { isl.over = true; if (kind == SpKind.Cross || kind == SpKind.Twin || kind == SpKind.Drift) Ev("!" + isl.name + "救完了！"); }
        }

        // 断桥：桥上不站人，这时候还在后岛的人留在后岛，后岛码头启用
        void BreakBridge()
        {
            broken = true; Ev("break"); Ev("!桥断了！右上角水道通了");
            var q0 = queues[0]; var q1 = queues[1];
            for (int i = q0.F; i < q0.guys.Count; i++) { var g = q0.guys[i]; g.s = Mathf.Max(0, g.s - sBridgeEnd); g.q = 1; q1.guys.Add(g); }
            q0.guys.RemoveRange(q0.F, q0.guys.Count - q0.F); q0.F = -1;
            foreach (int di in isls[1].docks) docks[di].on = true;
        }

        void TickBar(float dt)
        {
            if (!started || barDone) { BarEject(dt); return; }
            barT -= dt;
            if (barT <= 0) { barT = 0; End(2, "沙洲被淹了"); return; }
            BarEject(dt);
        }
        // 送错颜色到沙洲：白占 3 秒，再自己开去主码头
        void BarEject(float dt)
        {
            var d = docks[barDock]; if (d.boat < 0) return;
            var b = boats[d.boat]; if (b.st != DOCK || (b.c == barColor && !barDone)) return;
            if (b.seat > 0) return;
            b.stay += dt; if (b.stay < 3) return;
            int k = FreeDock(0, b.p); if (k < 0) return;
            d.boat = -1; docks[k].boat = b.id; b.dock = k; b.stay = 0;
            Go(b, Route(b.p, b.p, docks[k], b.len), MOVE);
        }

        void TickDrift(float dt)
        {
            driftX = driftMid + Mathf.Sin(t * Mathf.PI * 2 / 15) * driftAmp;
            float dx = driftX - driftMid;
            queues[driftQ].ox = dx;
            foreach (int di in isls[1].docks) { var d = docks[di]; d.x = d.ts + dx; }
        }

        // 环礁：人一直转；走到码头前面，码头上有同色空座就上
        public Vector2 RingAt(float s) => PathAt(ringPts, ringCum, Mathf.Repeat(s, ringLen));
        void TickRing(float dt)
        {
            float step = ringV * dt; ringTrav += step;
            foreach (var g in guys) if (g.st == 5) { g.s = Mathf.Repeat(g.s + step, ringLen); g.p = RingAt(g.s); }
            if (ringTrav >= ringLen) { ringTrav -= ringLen; laps++; timeRate = Mathf.Min(2, 1 + laps * 0.15f); Ev("!转了一整圈，风暴加快"); }
            foreach (var d in docks)
            {
                d.cd -= dt; if (d.boat < 0 || d.cd > 0) continue;
                var b = boats[d.boat]; if (b.st != DOCK || b.seat >= b.cap) continue;
                foreach (var g in guys)
                {
                    if (g.st != 5 || g.c != b.c) continue;
                    float ds = Mathf.Abs(Mathf.DeltaAngle(g.s / ringLen * 360, d.ringS / ringLen * 360)) / 360 * ringLen;
                    if (ds > 0.7f) continue;
                    Board(g, b); d.cd = 0.1f; break;
                }
            }
        }

        // 中转岛：点一堆人，筏子去接
        public void TapPile(int i)
        {
            if (i < 0 || i >= piles.Count || piles[i].guys.Count == 0) return;
            started = true; Ev("go");
            if (ferrySt == 0) { ferrySel = i; ferrySt = 1; ferryFrom = ferryP; ferryTo = new Vector2(piles[i].x, piles[i].z + 1.5f); ferryK = 0; }
            else ferryNext = i;
        }
        void TickFerry(float dt)
        {
            if (ferrySt == 0) return;
            ferryK += dt / ferryDur;
            ferryP = Vector2.Lerp(ferryFrom, ferryTo, Mathf.SmoothStep(0, 1, Mathf.Min(1, ferryK)));
            for (int k = 0; k < ferryLoad.Count; k++) { var g = ferryLoad[k]; g.p = ferryP + new Vector2((k % 2 - 0.5f) * 0.4f, (k / 2 - 0.5f) * 0.4f); }
            if (ferryK < 1) return;
            if (ferrySt == 1)
            {
                var pl = piles[ferrySel];
                while (ferryLoad.Count < ferryCap && pl.guys.Count > 0) { var g = pl.guys[pl.guys.Count - 1]; pl.guys.RemoveAt(pl.guys.Count - 1); g.st = 3; ferryLoad.Add(g); }
                ferrySt = 3; ferryFrom = ferryP; ferryTo = ferryHome; ferryK = 0;
            }
            else
            {
                var q = queues[0];
                foreach (var g in ferryLoad) { g.st = 0; g.s = q.len; g.q = 0; q.guys.Add(g); }
                ferryLoad.Clear(); ferrySt = 0; ferryP = ferryHome; int nx = ferryNext; ferryNext = -1;
                if (nx >= 0) TapPile(nx);
            }
        }

        void CheckEnd(bool busy, float dt)
        {
            if (status == 3) { winT -= dt; if (winT <= 0) End(1, ""); return; }
            bool all = true; foreach (var g in guys) if (g.st != 2) { all = false; break; }
            if (all) { status = 3; winT = 0.5f; return; }
            if (kind == SpKind.Ring) return;   // 环礁不会卡死：人会转回来，只有风暴能输
            foreach (var isl in isls)
            {
                int on = 0, taken = 0; bool wait = false, anyQ = false, ok = false;
                foreach (int di in isl.docks)
                {
                    var d = docks[di]; if (!d.on || d.bay || di == barDock) continue;
                    on++; if (d.boat < 0) continue; taken++;
                    var b = boats[d.boat]; if (b.st != DOCK || b.seat >= b.cap) wait = true;
                    foreach (int qi in isl.queues) { var q = queues[qi]; if (q.guys.Count > 0 && q.guys[0].c == b.c && b.seat < b.cap) ok = true; }
                }
                foreach (int qi in isl.queues) if (queues[qi].guys.Count > 0) anyQ = true;
                if (on > 0 && anyQ && taken == on && !wait && !ok) { End(2, isls.Count > 1 ? isl.name + "码头停满了，队头等不到颜色" : "码头停满了，队头等不到颜色"); return; }
            }
            if (busy) { idle = 0; return; }
            idle += dt;
            if (idle < 1.5f || status != 0) return;
            if (kind == SpKind.Drift || (kind == SpKind.Sluice && gateOpen) || kind == SpKind.Sail || kind == SpKind.Whirl || kind == SpKind.Tilt || kind == SpKind.Sushi) return;   // 风、漩涡、晃水会变，转盘上的船还在转，都不算卡死
            if (kind == SpKind.Ferry && (ferrySt != 0 || FerryLeft() > 0)) return;
            foreach (var q in queues) if (q.guys.Count > 0 && q.guys[0].s > 0.2f) return;
            foreach (var b in boats) if (b.st == LOT && Probe(b).res == OK) return;
            End(2, "没有能开的船了");
        }
        public int FerryLeft() { int n = 0; foreach (var p in piles) n += p.guys.Count; return n; }
        void End(int s, string w) { status = s; why = w; Ev(s == 1 ? "win" : "lose"); }

        public int PeopleLeft() { int n = 0; foreach (var g in guys) if (g.st != 1 && g.st != 2) n++; return n; }

        // ---------- 机器人：验证解得开 ----------
        int solI; float botT;
        public int HeadNeed(int isl)
        {
            var seats = new Dictionary<int, int>();
            foreach (int di in isls[isl].docks) { var d = docks[di]; if (d.boat < 0 || d.bay) continue; var b = boats[d.boat]; seats.TryGetValue(b.c, out int n); seats[b.c] = n + b.cap - b.seat; }
            foreach (int qi in isls[isl].queues)
            {
                var q = queues[qi];
                for (int i = 0; i < q.guys.Count && i < 30; i++)
                {
                    seats.TryGetValue(q.guys[i].c, out int n);
                    if (n <= 0) return q.guys[i].c;
                    seats[q.guys[i].c] = n - 1;
                }
            }
            return -1;
        }
        public void BotTick(float dt)
        {
            botT -= dt; if (botT > 0 || status != 0) return; botT = 0.25f;
            if (kind >= SpKind.Sail && Bot2()) return;
            switch (kind)
            {
                case SpKind.Loop: BotLoop(); return;
                case SpKind.Canal: BotCanal(); return;
                case SpKind.Ferry: BotFerry(); return;
            }
            while (solI < sol.Count && boats[sol[solI]].st != LOT) solI++;
            if (solI >= sol.Count) return;
            var b = boats[sol[solI]]; var g = Probe(b);
            if (g.res != OK) return;
            if (kind == SpKind.Drift && (b.isl == 1) != (docks[g.dock].isl == 1)) return;   // 等漂流岛对准（或者错开）再点
            Tap(b); solI++;
        }
        void BotCanal()
        {
            int need = HeadNeed(0); if (need < 0) return;
            foreach (var b in boats) if (b.st == LOT && b.dock >= 0 && b.c == need) { if (Probe(b).res == OK) Tap(b); return; }
            SpBoat tgt = null;
            foreach (var b in boats) if (b.st == LOT && b.dock < 0 && b.c == need && (tgt == null || b.r - b.len < tgt.r - tgt.len)) tgt = b;
            if (tgt == null) return;
            SpBoat top = null;
            foreach (var b in boats) if (b.st == LOT && b.dock < 0 && b.col == tgt.col && (top == null || b.r < top.r)) top = b;
            if (top != null && Probe(top).res == OK) Tap(top);
        }
        void BotLoop()
        {
            int need = HeadNeed(0); if (need < 0) return;
            foreach (int id in sol)
            {
                var b = boats[id]; if (b.st != LOT || b.c != need) continue;
                var g = Probe(b); if (g.res != OK) return;
                var a = Ahead(b, out float dd);
                if (a == null || dd > Fwd2(b.ts, docks[g.dock].ts)) Tap(b);   // 下一个空码头前面没有别的船才走，不然等码头空出来
                return;
            }
        }
        void BotFerry()
        {
            var q = queues[0];
            int need = HeadNeed(0);
            if (need >= 0 && q.guys.Count > 0 && q.guys[0].c == need)
                foreach (var b in boats) if (b.st == LOT && b.c == need && Probe(b).res == OK) { Tap(b); return; }
            if (ferrySt == 0)
            {
                int c = -1;
                foreach (var d in docks) { if (d.boat < 0) continue; var b = boats[d.boat]; int have = 0; foreach (var g in q.guys) if (g.c == b.c) have++; if (b.cap - b.seat > have) { c = b.c; break; } }
                if (c < 0) { while (solI < sol.Count && boats[sol[solI]].st != LOT) solI++; if (solI < sol.Count) c = boats[sol[solI]].c; }
                for (int k = 0; k < piles.Count; k++) if (piles[k].c == c && piles[k].guys.Count > 0) { TapPile(k); break; }
            }
            int taken = 0; foreach (var d in docks) if (d.boat >= 0) taken++;
            if (taken == 0) { while (solI < sol.Count && boats[sol[solI]].st != LOT) solI++; if (solI < sol.Count) { var b = boats[sol[solI]]; if (Probe(b).res == OK) Tap(b); } }
        }
    }
}
