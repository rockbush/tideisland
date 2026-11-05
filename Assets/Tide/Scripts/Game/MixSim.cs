// 混合模式（11-03）规则层：在特殊模式的 SpSim 上加主线的东西（主线 Rules.cs 一行不动，这里是照着主线的规则另写一份）：
//   神秘白船：开局被挡着的船有一部分是白的，船头前面的路一通就露出颜色（主线 mys）
//   石头：港湾里几格是石头，挡路（主线 rocks）；只放在"倒着摆船"的地图上（摆船时自动避开）
//   撑杆跳：他一上船，同一条队里同色的人撑杆跳上这艘船，把空座补满（主线 Vault / Pull）
//   海藻：船被缠住，港湾里再开走 N 艘才松开（主线 weed）
//   钥匙锁链：锁着的船要等挂钥匙的船开走（主线 lockBy / key）
//   道具：直升机（吊任意一艘船去码头）、喇叭（能上船的人站到前面）、临时码头（多一个泊位）
//   复活：风暴 → 倒计时回到 30 秒；码头停满 → 多开一个泊位（开不了的图就喇叭理队）
// 每一波都是一张新生成、机器人打得通的图（MixBuild），这些东西在验证之前就放好了。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tide
{
    // 一波的参数
    public class MixCfg
    {
        public SpKind kind; public int boats, colors, rocks, poles, weed, locks; public double mys; public bool clock; public float per = SpSim.TIME_PER, start;
    }

    public partial class SpSim
    {
        public bool mix, clock = true; public float timePer = TIME_PER;
        public int mixN, mixC, mixRocks, goneN;
        public List<Vector2Int> rocks = new List<Vector2Int>();
        public List<int> revealed = new List<int>(), vaulted = new List<int>(), unlocked = new List<int>();
        float revealT;

        int NB(int d) => mixN > 0 ? mixN : d;
        int NC(int d) => mixC > 0 ? mixC : d;

        // 摆船之前放石头（Place 开头调）：只放在空水面上，石头之间不挨着
        void PutRocks(System.Random r)
        {
            if (mixRocks <= 0 || rocks.Count > 0) return;
            for (int k = 0, guard = 0; k < mixRocks && guard < 200; guard++)
            {
                int rr = r.Next(rows), cc = r.Next(cols);
                if (mask[rr, cc] != 1 || occ[rr, cc] >= 0) continue;
                if (kind == SpKind.Turn && OnDisc(rr, cc)) continue;
                bool near = false; foreach (var q in rocks) if (Mathf.Abs(q.x - rr) + Mathf.Abs(q.y - cc) <= 1) near = true;
                if (near) continue;
                mask[rr, cc] = 0; rocks.Add(new Vector2Int(rr, cc)); k++;
            }
        }

        static bool RockOK(SpKind k) => k == SpKind.Base || k == SpKind.Fog || k == SpKind.Cross || k == SpKind.Sail || k == SpKind.Ring || k == SpKind.Whirl || k == SpKind.Paint || k == SpKind.Truck || k == SpKind.Sushi || k == SpKind.Drift || k == SpKind.Ferry || k == SpKind.Rope;
        public static bool WeedOK(SpKind k) => RockOK(k);

        // 生成好以后：白船、撑杆跳、海藻、锁链
        void Decorate(System.Random r, MixCfg c)
        {
            // 机器人点船的先后：海藻要等前面开走够数，钥匙船要排在锁住的船前面
            var ord = new Dictionary<int, int>();
            for (int k = 0; k < sol.Count; k++) if (!ord.ContainsKey(sol[k])) ord[sol[k]] = k;
            if (WeedOK(kind))
            {
                var cand = new List<int>(); foreach (var b in boats) if (ord.TryGetValue(b.id, out int o) && o >= 3) cand.Add(b.id);
                Shuffle(r, cand);
                int wn = 0;
                foreach (int id in cand) { if (wn >= c.weed) break; var b = boats[id]; b.weed = Mathf.Min(ord[id], 2 + r.Next(3)); wn++; }
                int ln = 0;
                foreach (int id in cand)
                {
                    if (ln >= c.locks) break; var b = boats[id]; if (b.weed > 0 || b.key >= 0) continue;
                    int o = ord[id]; var keys = new List<int>();
                    foreach (var kb in boats) if (kb.id != id && ord.TryGetValue(kb.id, out int ko) && ko < o - 1 && kb.weed == 0 && kb.key < 0 && kb.lockBy < 0) keys.Add(kb.id);
                    if (keys.Count == 0) continue;
                    int kid = keys[r.Next(keys.Count)]; b.lockBy = kid; boats[kid].key = ln; ln++;
                }
            }
            if (c.mys > 0 && kind != SpKind.Fog)
                foreach (var b in boats) if (b.st == LOT && Probe(b).res == BLOCK && r.NextDouble() < c.mys) b.mys = true;
            // 撑杆跳：队伍里挑人（左右两边不能同色，后面还要有同色的人给他叫）
            for (int k = 0; k < c.poles; k++)
            {
                for (int t = 0; t < 40; t++)
                {
                    List<SpGuy> line;
                    if (queues.Count > 0) { var q = queues[r.Next(queues.Count)]; line = q.guys; }
                    else { line = new List<SpGuy>(); foreach (var g in guys) if (g.st == 5) line.Add(g); }
                    if (line.Count < 8) continue;
                    int i = 3 + r.Next(Mathf.Max(1, line.Count * 3 / 4 - 3)); if (i >= line.Count - 1) continue;
                    var p = line[i]; if (p.pole || line[i - 1].pole || line[i + 1].pole || line[i - 2].pole) continue;
                    bool more = false; for (int j = i + 2; j < line.Count; j++) if (line[j].c == p.c) more = true;
                    if (!more) continue;
                    if (line[i - 1].c != p.c && line[i + 1].c != p.c) { p.pole = true; break; }
                    // 同色的人都是成串的：一串的第一个人和他前面那个人换个位置，他两边就都不是同色了
                    if (line[i - 1].c != p.c && line[i + 1].c == p.c && line[i - 2].c != p.c)
                    {
                        var x = line[i - 1]; line[i - 1] = p; line[i] = x;
                        float s0 = p.s; p.s = x.s; x.s = s0; var p0 = p.p; p.p = x.p; x.p = p0;
                        p.pole = true; break;
                    }
                }
            }
        }
        static void Shuffle(System.Random r, List<int> l) { for (int k = l.Count - 1; k > 0; k--) { int j = r.Next(k + 1); int t = l[k]; l[k] = l[j]; l[j] = t; } }

        // 被海藻、锁链拦住（Probe 一开头问）
        bool MixStop(SpBoat b, out SpGo g)
        {
            g = new SpGo { dock = -1 };
            if (b.weed > goneN) { g.res = GATE; g.bump = 0.12f; g.why = "被海藻缠住：再开走 " + (b.weed - goneN) + " 艘别的船就松开"; return true; }
            if (b.lockBy >= 0 && boats[b.lockBy].st == LOT) { g.res = GATE; g.bump = 0.12f; g.why = "被锁链锁着：先把挂钥匙的那艘开走"; return true; }
            return false;
        }
        public int WeedLeft(SpBoat b) => Mathf.Max(0, b.weed - goneN);
        public bool Locked(SpBoat b) => b.lockBy >= 0 && boats[b.lockBy].st == LOT;

        // 每帧：白船露色、锁链松开
        void MixTick(float dt)
        {
            revealT -= dt;
            if (revealT <= 0)
            {
                revealT = 0.15f;
                foreach (var b in boats) if (b.mys && !b.seen && b.st == LOT && Probe(b).res != BLOCK) { b.seen = true; revealed.Add(b.id); }
            }
            foreach (var b in boats) if (b.lockBy >= 0 && b.key != -9 && !Locked(b)) { b.key = -9; unlocked.Add(b.id); }
        }

        // 撑杆跳：他一上船，同一条队（环礁就是环上）同色的人跳上来补满
        void PoleFire(SpGuy p, SpBoat b)
        {
            int need = b.cap - b.seat; if (need <= 0) return;
            var pick = new List<SpGuy>();
            if (p.q >= 0) { var q = queues[p.q]; foreach (var g in q.guys) if (g.c == p.c && pick.Count < need) pick.Add(g); foreach (var g in pick) q.guys.Remove(g); }
            else foreach (var g in guys) if (g.st == 5 && g.c == p.c && pick.Count < need) pick.Add(g);
            foreach (var g in pick) { Board(g, b); g.vault = true; g.hopT = 0.75f + Vector2.Distance(g.p, b.p) * 0.025f; }
            if (pick.Count > 0) { vaulted.Add(b.id); Ev("vault"); }
            if (p.q >= 0 && queues[p.q].guys.Count == 0) QueueOver(p.q);
        }

        // ---------- 机器人提示（新手关的手指）----------
        public SpBoat Hint()
        {
            if (steps.Count > 0) { foreach (var s in steps) { var t = boats[s.tap]; var lv = boats[s.leave >= 0 ? s.leave : s.tap]; if (lv.st == LOT && t.st == LOT) return t; } return null; }
            foreach (int id in sol) { var b = boats[id]; if (b.st == LOT) return b; }
            return null;
        }

        // ---------- 道具 ----------
        // 直升机：任意一艘港湾里的船（被挡着、被缠住、被锁着都行）直接吊去码头
        public string Crane(SpBoat b)
        {
            if (b == null || b.st != LOT) return "点港湾里的船";
            if (b.inc > 0 || b.mergeTo >= 0) return "这艘正在合体";
            int tgt = kind == SpKind.Sushi ? BeltSlot(b.p) : FreeDock(kind == SpKind.Cross ? b.isl : (b.isl < isls.Count ? b.isl : 0), b.p);
            if (tgt < 0) return "码头满了";
            if (b.track) b.track = false; else if (b.dock < 0) Clear(b); else docks[b.dock].boat = -1;
            if (b.mate >= 0) { boats[b.mate].mate = -1; b.mate = -1; }
            if (b.want >= 0 && b.c != b.want) { b.c = b.want; dirty.Add(b.id); }
            b.disc = false; b.weed = 0; if (b.mys && !b.seen) { b.seen = true; revealed.Add(b.id); }
            var d = docks[tgt]; b.dock = tgt; d.boat = b.id; b.sc = DockSc(b.L);
            var path = new List<Vector2> { b.p };
            if (kind == SpKind.Sushi) path.Add(beltWait);
            path.Add(d.At(b.L));
            b.speed = 8; b.crane = true; Go(b, path, MOVE);
            started = true; idle = 0; goneN++; Ev("crane");
            return null;
        }
        // 喇叭：每条队里，码头上船能接的人（按座位数）挪到最前面，其余人顺序不变；返回挪了几个人
        public int Sort()
        {
            int moved = 0;
            foreach (var q in queues)
            {
                if (q.guys.Count < 2) continue;
                var seats = new Dictionary<int, int>();
                foreach (var d in docks)
                {
                    if (d.boat < 0 || d.bay) continue; var b = boats[d.boat];
                    if (d.isl != q.isl && kind != SpKind.Sushi) continue;
                    seats.TryGetValue(b.c, out int n); seats[b.c] = n + b.cap - b.seat;
                }
                var a = new List<SpGuy>(); var z = new List<SpGuy>();
                foreach (var g in q.guys) { seats.TryGetValue(g.c, out int n); if (n > 0) { seats[g.c] = n - 1; a.Add(g); } else z.Add(g); }
                a.AddRange(z);
                for (int i = 0; i < a.Count; i++) if (a[i] != q.guys[i]) moved++;
                q.guys.Clear(); q.guys.AddRange(a);
            }
            if (moved > 0) { idle = 0; Ev("sort"); }
            return moved;
        }
        // 临时码头：码头排成一排的图，在一头再加一个泊位
        public bool CanBerth()
        {
            if (kind == SpKind.Cross || kind == SpKind.Ring || kind == SpKind.Loop || kind == SpKind.Sushi) return false;
            return BerthX(out _, out _);
        }
        bool BerthX(out float x, out float z)
        {
            x = z = 0; float x0 = 1e9f, x1 = -1e9f; int n = 0;
            foreach (int k in isls[0].docks) { var d = docks[k]; if (d.bay || d.belt || Mathf.Abs(d.yaw) > 0.01f) continue; x0 = Mathf.Min(x0, d.x); x1 = Mathf.Max(x1, d.x); z = d.z; n++; }
            if (n == 0 || n >= 6) return false;   // 最多多开 2 个
            float roomR = 3.6f - x1, roomL = x0 + 3.6f;
            if (Mathf.Max(roomR, roomL) < 1.15f) return false;
            x = roomR >= roomL ? x1 + 1.15f : x0 - 1.15f;
            return true;
        }
        public int AddBerth()
        {
            if (!CanBerth() || !BerthX(out float x, out float z)) return -1;
            int k = AddDock(0, x, z); idle = 0; Ev("berth");
            return k;
        }
        // 复活：风暴回到 sec 秒；码头停满就多开一个泊位（开不了就理队）
        public void Revive(float sec, bool storm)
        {
            status = 0; why = ""; idle = 0; started = true;
            if (storm) timer = Mathf.Max(timer, sec);
            else if (AddBerth() < 0) Sort();
        }

        // ---------- 一波：生成 + 机器人验证 ----------
        public static SpSim MixTry(MixCfg c, int seed)
        {
            for (int k = 0; k < 150; k++)
            {
                var s = new SpSim { kind = c.kind, seed = seed + k * 1000, mix = true, mixN = c.boats, mixC = c.colors, mixRocks = RockOK(c.kind) ? c.rocks : 0 };
                var r = new System.Random(s.seed);
                if (!s.Make(r)) continue;
                s.Decorate(r, c);
                s.clock = c.clock; s.timePer = c.per; if (c.start > 0) s.timer = c.start;
                return s;
            }
            return null;
        }
        public static bool MixRun(SpSim s, float[] tOut = null, float dt = 0.05f)
        {
            if (s == null) return false;
            int n = Mathf.CeilToInt(400 / dt);
            for (int k = 0; k < n; k++) { s.BotTick(dt); s.Tick(dt); s.evs.Clear(); if (s.status == 1 || s.status == 2) break; }
            if (tOut != null) { tOut[0] = s.t; tOut[1] = s.timer; }
            return s.status == 1;
        }
        // 中转岛的机器人点人堆看时机，换个帧率可能把自己堵死：这种图两种节奏都要打得通才要
        static bool MixOK(MixCfg c, int seed) => MixRun(MixTry(c, seed)) && (c.kind != SpKind.Ferry || MixRun(MixTry(c, seed), null, 1 / 60f));
        public static SpSim MixBuild(MixCfg c, int seed)
        {
            for (int k = 0; k < 10; k++) if (MixOK(c, seed + k)) return MixTry(c, seed + k);
            // 带着石头、海藻打不通：去掉这些再来（保底，不让玩家进到解不开的图）
            var c2 = new MixCfg { kind = c.kind, boats = c.boats, colors = c.colors, mys = c.mys, clock = c.clock, per = c.per, start = c.start };
            for (int k = 0; k < 10; k++) if (MixOK(c2, seed + 50 + k)) return MixTry(c2, seed + 50 + k);
            return MixTry(c2, seed);
        }
    }
}
