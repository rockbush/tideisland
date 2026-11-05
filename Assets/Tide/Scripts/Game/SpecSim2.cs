// 特殊模式第二批：10 种换想法的新机制（11-01 用户试玩后说只有十字港/环礁/港中岛/雾港有新意，其他只是换了画面）。
// 规则都接在 SpecSim.cs 的 Probe/Tap/Tick 后面，一样全部在 Tick 里算，界面（SpecMode2.cs）只照着画。
using System.Collections.Generic;
using UnityEngine;

namespace Tide
{
    public class SpStep { public int tap, leave; }   // 机器人的一步：点 tap，走的是 leave（-1 = 这一步是合体，没有船走）
    public class SpHit { public int id, dir, chain; public float t; }
    public class SpTruck { public float x0, len; }

    public partial class SpSim
    {
        public List<SpStep> steps = new List<SpStep>();
        public List<int> dirty = new List<int>();   // 颜色、船型变了，界面要重画的船
        public List<Vector2> crash = new List<Vector2>(), merged = new List<Vector2>();
        // 风帆
        public int wind; public float windT, windPer = 3;
        public int NextWind => (wind + 1) % 4;
        // 转盘
        public int d0r, d0c, dN, turnN = 2, turnLeft = 2, turnCount; public float turnK = 1, turnDelay; public Vector2 discC;
        // 碰碰船
        public List<SpHit> hits = new List<SpHit>();
        // 漩涡
        public int[] wr = new int[2], wc = new int[2]; public int whirlD; public float whirlT, whirlPer = 3;
        public int PDir(int i) => (whirlD + i * 2) % 4;
        // 变色门
        public int pc0, pcN, paintI; public float paintT, paintPer = 1.5f; public int[] paintSeq;
        public int GateColor => paintSeq[paintI];
        public int GateNext => paintSeq[(paintI + 1) % paintSeq.Length];
        public int GateColorAt(float dt) => paintSeq[(paintI + Mathf.FloorToInt((paintT + dt) / paintPer)) % paintSeq.Length];
        // 货轮
        public List<SpTruck> trucks = new List<SpTruck>(); public float truckT, truckV = 4.5f, truckSpan, laneZ;
        // 摇摆
        public int tiltI, tiltN = 2, tiltLeft = 2; public int[] tiltSeq; public float tiltPend, tiltIdle, tiltPer = 6;
        public int TiltNext => tiltSeq[tiltI % tiltSeq.Length];
        // 回转码头
        public Vector2[] beltPts; public float[] beltCum; public float beltLen, beltS, beltV = 1.6f, beltEntryS; public Vector2 beltEntry, beltWait;

        bool Busy2 => hits.Count > 0 || turnK < 1 || turnDelay > 0 || tiltPend > 0;

        // ---------- 通用 ----------
        void Clear(SpBoat b) { for (int k = 0; k < b.len; k++) occ[b.r + DR[b.dir] * k, b.col + DC[b.dir] * k] = -1; }
        void Fill(SpBoat b) { for (int k = 0; k < b.len; k++) occ[b.r + DR[b.dir] * k, b.col + DC[b.dir] * k] = b.id; }
        bool Inb(int r, int c) => r >= 0 && c >= 0 && r < rows && c < cols;
        void Shift(SpBoat b, int d, int n) { Clear(b); b.r += DR[d] * n; b.col += DC[d] * n; Fill(b); }
        // b 整艘沿 d 方向能走几格（free）；碰到的第一艘船，或者一路通到港外（open）
        public SpBoat Room(SpBoat b, int d, out int free, out bool open)
        {
            free = 99; SpBoat hit = null;
            for (int i = 0; i < b.len; i++)
            {
                int r = b.r + DR[b.dir] * i, c = b.col + DC[b.dir] * i, n = 0;
                for (; ; )
                {
                    r += DR[d]; c += DC[d];
                    if (!Inb(r, c)) { if (n < free) free = n; break; }
                    int o = occ[r, c];
                    if (o >= 0 && o != b.id) { if (n < free) { free = n; hit = boats[o]; } break; }
                    n++;
                }
            }
            open = hit == null; return hit;
        }
        Vector2 ExitPt(Vector2 p, int d) => d == 0 ? new Vector2(p.x, RZ0) : d == 2 ? new Vector2(p.x, RZ0 + RH) : d == 1 ? new Vector2(RX0 + RW, p.y) : new Vector2(RX0, p.y);
        void Commit(SpBoat b, SpGo g) { Clear(b); b.dock = g.dock; docks[g.dock].boat = b.id; b.sc = DockSc(b.L); Go(b, g.path, MOVE); }
        int FreeCount() { int n = 0; foreach (var d in docks) if (d.boat < 0 && d.on && !d.bay) n++; return n; }

        // ---------- 点船分流 ----------
        public SpGo Probe(SpBoat b)
        {
            if (b.st != LOT) return new SpGo { res = NONE, dock = -1 };
            if (mix && MixStop(b, out var mg)) return mg;
            switch (kind)
            {
                case SpKind.Sail: return ProbeSail(b);
                case SpKind.Turn: if (turnK < 1) return new SpGo { res = NONE, dock = -1 }; break;
                case SpKind.Bump: return ProbeBump(b);
                case SpKind.Merge: return ProbeMerge(b);
                case SpKind.Whirl: return ProbeWhirl(b);
                case SpKind.Rope: return ProbeRope(b);
            }
            return Probe0(b);
        }
        // 返回 true：这一下处理完了，不走通用的"开去码头"
        bool Tap2(SpBoat b, SpGo g)
        {
            switch (kind)
            {
                case SpKind.Sail: if (b.dir != wind) Flip(b); return false;
                case SpKind.Bump: if (g.dock < 0) { DoBump(b); return true; } return false;
                case SpKind.Merge: if (g.merge > 0) { DoMerge(b, boats[g.merge - 1]); return true; } return false;
                case SpKind.Whirl: if (g.path2 != null) { b.next = g.path2; b.warp = true; } return false;
                case SpKind.Paint: b.gate = b.dir == 0 && b.col >= pc0 && b.col < pc0 + pcN; return false;
                case SpKind.Rope: return DoRope(b);
            }
            return false;
        }
        void After2(SpBoat b, SpGo g)
        {
            switch (kind)
            {
                case SpKind.Turn: if (--turnLeft <= 0) { turnLeft = turnN; turnDelay = 0.4f; } break;
                case SpKind.Tilt: tiltIdle = 0; if (--tiltLeft <= 0) { tiltLeft = tiltN; tiltPend = 0.4f; } break;
                case SpKind.Truck: b.cross = true; b.home = g.path.GetRange(0, g.path.Count - 1); break;
                case SpKind.Sushi: b.sc = 0.66f; break;
            }
        }
        void Tick2(float dt)
        {
            switch (kind)
            {
                case SpKind.Sail: windT += dt; if (windT >= windPer) { windT -= windPer; wind = NextWind; Ev("wind"); } break;
                case SpKind.Turn: TickTurn(dt); break;
                case SpKind.Bump: TickHits(dt); break;
                case SpKind.Whirl: whirlT += dt; if (whirlT >= whirlPer) { whirlT -= whirlPer; whirlD = (whirlD + 1) % 4; } break;
                case SpKind.Paint: paintT += dt; if (paintT >= paintPer) { paintT -= paintPer; paintI = (paintI + 1) % paintSeq.Length; } break;
                case SpKind.Truck: TickTruck(dt); break;
                case SpKind.Tilt: TickTilt(dt); break;
                case SpKind.Sushi: beltS += beltV * dt; SetSlots(); break;
            }
        }
        // 走完一段路之后的特殊处理
        bool End2(SpBoat b)
        {
            if (b.next != null) { var p = b.next; b.next = null; b.p = p[0]; Go(b, p, MOVE); Ev("warp"); return true; }
            if (b.back) { b.back = false; b.path = null; b.st = LOT; b.speed = 15; b.sc = cs; b.yaw = YawDir(b.dir); Fill(b); return true; }
            if (b.mergeTo >= 0)
            {
                var h = boats[b.mergeTo]; b.mergeTo = -1; b.st = GONE; h.inc--;
                h.sc = cs * h.len / h.L; dirty.Add(h.id); merged.Add(h.p); Ev("merge");
                return true;
            }
            return false;
        }

        // ---------- 风帆港 ----------
        void Flip(SpBoat b) { b.r += DR[b.dir] * (b.len - 1); b.col += DC[b.dir] * (b.len - 1); b.dir = (b.dir + 2) % 4; }
        SpGo ProbeSail(SpBoat b)
        {
            if ((b.dir + wind) % 2 != 0) return new SpGo { res = GATE, dock = -1, bump = 0.12f, why = "这阵风吹不动它" };
            bool f = b.dir != wind; if (f) Flip(b);
            var g = Probe0(b); if (f) Flip(b);
            return g;
        }

        // ---------- 转盘港 ----------
        public bool OnDisc(int r, int c) => r >= d0r && c >= d0c && r < d0r + dN && c < d0c + dN;
        void TurnDisc()
        {
            var list = new List<SpBoat>();
            foreach (var b in boats) if (b.disc && b.st == LOT) { list.Add(b); Clear(b); }
            foreach (var b in list) { int rr = b.r - d0r, cc = b.col - d0c; b.r = d0r + cc; b.col = d0c + dN - 1 - rr; b.dir = (b.dir + 1) % 4; Fill(b); }
            turnCount++;
        }
        void TickTurn(float dt)
        {
            if (turnDelay > 0) { turnDelay -= dt; if (turnDelay <= 0) { TurnDisc(); turnK = 0; Ev("turn"); } return; }
            if (turnK >= 1) return;
            turnK = Mathf.Min(1, turnK + dt / 0.6f);
            float a = (Mathf.SmoothStep(0, 1, turnK) - 1) * Mathf.PI / 2, co = Mathf.Cos(a), si = Mathf.Sin(a);
            foreach (var b in boats)
            {
                if (!b.disc || b.st != LOT) continue;
                var v = Center(b) - discC;
                b.p = discC + new Vector2(v.x * co - v.y * si, v.x * si + v.y * co); b.yaw = YawDir(b.dir) + a;
            }
        }
        // 再转一下之后，这艘盘上的船在哪、朝哪（按住时给玩家看）
        public void TurnPreview(SpBoat b, out Vector2 c, out Vector2 f)
        {
            var v = Center(b) - discC; c = discC + new Vector2(-v.y, v.x); f = Fwd(YawDir((b.dir + 1) % 4));
        }

        // ---------- 碰碰船 ----------
        SpGo ProbeBump(SpBoat b)
        {
            var h = Room(b, b.dir, out int free, out bool open);
            if (open) return Probe0(b);
            var g = new SpGo { dock = -1 };
            if (free == 0) { g.res = BLOCK; g.bump = 0.12f; g.why = "贴得太近撞不动，要隔一格助跑"; return g; }
            var q = Center(b) + Fwd(YawDir(b.dir)) * free * cs;
            g.path = new List<Vector2> { b.p, q };
            return g;
        }
        void DoBump(SpBoat b)
        {
            var h = Room(b, b.dir, out int free, out _);
            Shift(b, b.dir, free); b.lotV = 15;
            hits.Add(new SpHit { id = h.id, dir = b.dir, t = free * cs / 15 }); Ev("go");
        }
        void TickHits(float dt)
        {
            for (int k = hits.Count - 1; k >= 0; k--)
            {
                var e = hits[k]; e.t -= dt; if (e.t > 0) continue;
                hits.RemoveAt(k); Push(e);
            }
        }
        // 被撞的船沿撞它的方向滑；碰到下一艘继续传（连环撞）；一路通就出港
        void Push(SpHit e)
        {
            var h = boats[e.id]; if (h.st != LOT) return;
            crash.Add(h.p); Ev("crash"); h.lotV = 15;
            var n = Room(h, e.dir, out int free, out bool open);
            if (open)
            {
                int k = FreeDock(0, h.p);
                if (k >= 0)
                {
                    Clear(h); h.dock = k; docks[k].boat = h.id; h.sc = DockSc(h.len);
                    Go(h, Route(h.p, ExitPt(h.p, e.dir), docks[k], h.len), MOVE);
                    return;
                }
                Ev("!码头满了，它只能停在港湾边上");
            }
            if (free > 0) Shift(h, e.dir, free);
            if (n != null && free > 0 && e.chain < 2) hits.Add(new SpHit { id = n.id, dir = e.dir, chain = e.chain + 1, t = free * cs / 15 });
        }

        // ---------- 合船港 ----------
        static int Tier(int cap) => cap <= 2 ? 1 : cap <= 4 ? 2 : cap <= 6 ? 3 : 4;
        SpGo ProbeMerge(SpBoat b)
        {
            var h = Room(b, b.dir, out int free, out bool open);
            if (open) return Probe0(b);
            var g = new SpGo { dock = -1 };
            if (h.c != b.c) { g.res = BLOCK; g.bump = free * cs + 0.12f; return g; }
            if (h.cap + b.cap > 10) { g.res = BLOCK; g.bump = free * cs + 0.12f; g.why = "合体最多 10 座"; return g; }
            if (h.inc > 0) { g.res = NONE; return g; }
            g.path = new List<Vector2> { b.p, h.p }; g.merge = h.id + 1;
            return g;
        }
        // 船变大：先往船尾方向长，长不了再往船头长；都挤着就先不变长，只升座位
        void ApplyMerge(SpBoat a, SpBoat h)
        {
            Clear(a); h.cap += a.cap; h.mlen = Tier(h.cap);
            while (h.len < h.mlen)
            {
                int tr = h.r - DR[h.dir], tc = h.col - DC[h.dir];
                if (Inb(tr, tc) && occ[tr, tc] < 0) { h.r = tr; h.col = tc; occ[tr, tc] = h.id; h.len++; continue; }
                int fr = h.r + DR[h.dir] * h.len, fc = h.col + DC[h.dir] * h.len;
                if (Inb(fr, fc) && occ[fr, fc] < 0) { occ[fr, fc] = h.id; h.len++; continue; }
                break;
            }
        }
        void DoMerge(SpBoat b, SpBoat h)
        {
            ApplyMerge(b, h); h.inc++; b.mergeTo = h.id; b.speed = 13;
            Go(b, new List<Vector2> { b.p, h.p }, MOVE); Ev("go");
        }

        // ---------- 漩涡港 ----------
        int PortalAt(int r, int c) => r == wr[0] && c == wc[0] ? 0 : 1;
        bool Lane(int r, int c, int d, out Vector2 exit, out float room)
        {
            room = 0;
            for (; ; )
            {
                r += DR[d]; c += DC[d];
                if (!Inb(r, c)) { exit = Cell(r, c); return true; }
                if (occ[r, c] >= 0) { exit = Vector2.zero; return false; }
                room += cs;
            }
        }
        // 摆船时用：出口箭头转到哪边算哪边，只要有一个方向通就算走得掉
        bool PortalAny(int r, int c, out Vector2 exit)
        {
            int j = 1 - PortalAt(r, c);
            for (int d = 0; d < 4; d++) if (Lane(wr[j], wc[j], d, out exit, out _)) return true;
            exit = Vector2.zero; return false;
        }
        bool PortalLane(int len, int r, int c, int d)
        {
            r += DR[d] * (len - 1); c += DC[d] * (len - 1);
            for (; ; )
            {
                r += DR[d]; c += DC[d];
                if (!Inb(r, c) || occ[r, c] >= 0) return false;
                if (mask[r, c] == 3) return true;
            }
        }
        SpGo ProbeWhirl(SpBoat b)
        {
            int r = b.r + DR[b.dir] * (b.len - 1), c = b.col + DC[b.dir] * (b.len - 1); float room = 0;
            for (; ; )
            {
                r += DR[b.dir]; c += DC[b.dir];
                if (!Inb(r, c) || occ[r, c] >= 0) return Probe0(b);
                if (mask[r, c] == 3) break;
                room += cs;
            }
            var g = new SpGo { dock = -1 };
            int j = 1 - PortalAt(r, c);
            if (!Lane(wr[j], wc[j], PDir(j), out var exit, out _)) { g.res = BLOCK; g.bump = room + 0.12f; g.why = "漩涡出口被挡着，等箭头转开"; return g; }
            int tgt = FreeDock(0, exit);
            if (tgt < 0) { g.res = FULL; g.bump = room + 0.3f; g.why = "码头满了"; return g; }
            g.dock = tgt; g.path = new List<Vector2> { b.p, Cell(r, c) }; g.path2 = Route(Cell(wr[j], wc[j]), exit, docks[tgt], b.len);
            return g;
        }

        // ---------- 货轮航道 ----------
        public float TruckX(SpTruck k, float t) => Mathf.Repeat(k.x0 + truckV * t, truckSpan) - truckSpan / 2;
        bool TruckHit(float x, float hw, float t) { foreach (var k in trucks) if (Mathf.Abs(TruckX(k, t) - x) < k.len / 2 + hw) return true; return false; }
        static float CrossV(int len) => len <= 1 ? 9 : len == 2 ? 6.5f : 4.5f;
        void TickTruck(float dt)
        {
            truckT += dt;
            foreach (var b in boats)
            {
                if (!b.cross || b.st != MOVE) continue;
                bool zone = b.p.y < RZ0 - 0.05f;
                b.speed = zone ? CrossV(b.L) : 15;
                float h = b.L * 0.43f;
                if (zone && b.p.y - h < laneZ + 0.7f && b.p.y + h > laneZ - 0.7f && TruckHit(b.p.x, 0.42f, truckT)) Crash(b);
            }
        }
        void Crash(SpBoat b)
        {
            docks[b.dock].boat = -1; b.dock = -1; b.cross = false; b.back = true; b.speed = 18;
            timer = Mathf.Max(1, timer - 5); crash.Add(b.p); Ev("crash"); Ev("!撞上货轮！风暴 -5 秒");
            var pts = new List<Vector2> { b.p }; for (int k = b.home.Count - 1; k >= 0; k--) pts.Add(b.home[k]);
            Go(b, pts, MOVE);
        }
        // 现在松手过不过得去：照着航线算到航道的时间，再看穿过去那段时间里有没有货轮
        public bool TruckSafe(SpBoat b, SpGo g, float hw)
        {
            float s0 = 0; for (int k = 1; k < g.path.Count - 1; k++) s0 += Vector2.Distance(g.path[k - 1], g.path[k]);
            float t0 = s0 / 15, v = CrossV(b.L), x = g.path[g.path.Count - 2].x, h = b.L * 0.43f, end = g.path[g.path.Count - 1].y;
            for (float tt = 0; RZ0 - v * tt > end; tt += 0.04f)
            {
                float z = RZ0 - v * tt;
                if (z - h < laneZ + 0.7f && z + h > laneZ - 0.7f && TruckHit(x, hw, truckT + t0 + tt)) return false;
            }
            return true;
        }

        // ---------- 摇摆港 ----------
        int Lead(SpBoat b, int d) { int best = -999; for (int i = 0; i < b.len; i++) best = Mathf.Max(best, (b.r + DR[b.dir] * i) * DR[d] + (b.col + DC[b.dir] * i) * DC[d]); return best; }
        void Slosh(int d)
        {
            var list = new List<SpBoat>(); foreach (var b in boats) if (b.st == LOT) list.Add(b);
            list.Sort((a, b) => Lead(b, d).CompareTo(Lead(a, d)));   // 离墙近的先滑，后面的才能贴上去
            foreach (var b in list) { Room(b, d, out int n, out _); if (n > 0) { Shift(b, d, n); b.lotV = 11; } }
            tiltI++;
        }
        void TickTilt(float dt)
        {
            if (tiltPend > 0) { tiltPend -= dt; if (tiltPend <= 0) { Slosh(TiltNext); Ev("slosh"); } return; }
            if (!started || FreeCount() == 0) return;
            tiltIdle += dt;
            if (tiltIdle >= tiltPer) { tiltIdle = 0; Slosh(TiltNext); Ev("slosh"); }   // 一直没船走也晃一下，局面不会卡死
        }
        public float TiltIdleLeft => tiltPer - tiltIdle;

        // ---------- 双生船 ----------
        SpGo ProbeRope(SpBoat b)
        {
            if (b.mate < 0 || boats[b.mate].st != LOT) return Probe0(b);
            var m = boats[b.mate];
            Clear(m); var g = Probe0(b); Fill(m);
            Clear(b); var g2 = Probe0(m); Fill(b);
            if (g.res == BLOCK) return g;
            if (g2.res == BLOCK) { g.res = BLOCK; g.bump = 0.2f; g.why = "绳子另一头的船被挡着"; return g; }
            if (g.res != OK) return g;
            if (g2.res != OK) return g2;
            if (FreeCount() < 2) { g.res = FULL; g.bump = 0.3f; g.why = "两艘一起走，要 2 个空泊位"; return g; }
            g.path2 = g2.path;
            return g;
        }
        bool DoRope(SpBoat b)
        {
            if (b.mate < 0 || boats[b.mate].st != LOT) return false;
            var m = boats[b.mate];
            Clear(m); var g = Probe0(b); Fill(m); Commit(b, g);
            Commit(m, Probe0(m)); Ev("go");
            return true;
        }

        // ---------- 回转码头 ----------
        public Vector2 BeltAt(float s) => PathAt(beltPts, beltCum, Mathf.Repeat(s, beltLen));
        void SetSlots()
        {
            for (int k = 0; k < docks.Count; k++)
            {
                var d = docks[k]; float s = beltS + k * beltLen / docks.Count; var p = BeltAt(s);
                d.x = p.x; d.z = p.y; d.ts = Mathf.Repeat(s, beltLen); d.yaw = YawOf(BeltAt(s + 0.1f) - p);
            }
        }
        // 挑船开到入口时最先转过来的空泊位
        int BeltSlot(Vector2 from)
        {
            float lead = beltV * (Vector2.Distance(from, beltWait) * 1.3f / 15 + 0.2f), bd = 1e9f; int best = -1;
            for (int k = 0; k < docks.Count; k++)
            {
                if (docks[k].boat >= 0) continue;
                float f = Mathf.Repeat(beltEntryS - lead - docks[k].ts, beltLen);
                if (f < bd) { bd = f; best = k; }
            }
            return best;
        }
        // 开到入口外面等，泊位转到入口才上去
        bool BeltHold(SpBoat b)
        {
            float hold = b.cum[b.cum.Length - 2];
            if (b.s < hold) return false;
            if ((docks[b.dock].P - beltEntry).sqrMagnitude < 0.5f) return false;
            b.s = hold; b.p = beltWait; return true;
        }

        // ---------- 机器人 ----------
        bool Bot2()
        {
            switch (kind)
            {
                case SpKind.Turn: case SpKind.Tilt: case SpKind.Bump: case SpKind.Merge: BotSteps(); return true;
                case SpKind.Paint: BotPaint(); return true;
                case SpKind.Truck: BotTruck(); return true;
            }
            return false;
        }
        void BotSteps()
        {
            if (Busy2) return;
            foreach (var b in boats) if (b.mergeTo >= 0) return;
            while (solI < steps.Count)
            {
                var s0 = steps[solI];
                if (boats[s0.leave >= 0 ? s0.leave : s0.tap].st != LOT) solI++; else break;
            }
            if (solI >= steps.Count) return;
            var s = steps[solI]; var t = boats[s.tap];
            if (t.st != LOT) { solI++; return; }
            if (s.leave >= 0 && s.leave != s.tap && FreeDock(0, t.p) < 0) return;
            if (Probe(t).res != OK) return;
            Tap(t); solI++;
        }
        void BotPaint()
        {
            while (solI < sol.Count && boats[sol[solI]].st != LOT) solI++;
            if (solI >= sol.Count) return;
            var b = boats[sol[solI]]; if (Probe(b).res != OK) return;
            if (b.want >= 0 && GateColorAt((b.p.y - hz) / b.speed + 0.03f) != b.want) return;
            Tap(b); solI++;
        }
        void BotTruck()
        {
            while (solI < sol.Count && boats[sol[solI]].st != LOT) solI++;
            if (solI >= sol.Count) return;
            var b = boats[sol[solI]]; var g = Probe(b);
            if (g.res != OK || !TruckSafe(b, g, 0.8f)) return;
            Tap(b); solI++;
        }
    }
}
