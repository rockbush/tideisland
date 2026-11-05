// 特殊模式第二批 10 张地图。
// 风帆/漩涡/变色门/货轮/双生船/回转码头：局面不会自己变，沿用倒着摆船（Place）。
// 转盘/摇摆/碰碰船/合船：局面会被玩家带着变，倒推不回去，改成正着推（Forward）：
// 先随手摆好，再按规则模拟一局，随便挑现在走得掉的船走，走的先后就是队伍的顺序，这样一定解得开。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tide
{
    public partial class SpSim
    {
        // 地图基础：和单岛同一套尺寸
        void Yard(int r, int c, float size, float x, float z)
        {
            Harbor(r, c, size, x, z); Lagoon(); Sand(-5.6f, -11.8f, 11.2f, 8.1f);
            Palms(-4.7f, -11f, 4.7f, -10.9f);
            NewIsl("主岛");
        }
        void Docks4() { foreach (float x in new[] { -1.725f, -0.575f, 0.575f, 1.725f }) AddDock(0, x, -2.45f); }
        Vector2[] MainQ() => Serp(-4.4f, 4.4f, -4.3f, -1.08f, 6);

        // 随手摆：贴着别的船、靠中间；不管箭头前面有没有船
        bool Scatter(System.Random rnd, Func<SpBoat, int, int, int, bool> accept, float tight = 1)
        {
            var ids = new List<int>(); foreach (var b in boats) ids.Add(b.id);
            for (int k = ids.Count - 1; k > 0; k--) { int j = rnd.Next(k + 1); int t = ids[k]; ids[k] = ids[j]; ids[j] = t; }
            foreach (int id in ids)
            {
                var b = boats[id]; float best = -1e9f; int br = -1, bc = -1, bd = -1;
                for (int d = 0; d < 4; d++)
                    for (int r = 0; r < rows; r++)
                        for (int c = 0; c < cols; c++)
                        {
                            if (!Fits(b.len, r, c, d) || (accept != null && !accept(b, r, c, d))) continue;
                            float n = 0;
                            for (int i = 0; i < b.len; i++)
                            {
                                int rr = r + DR[d] * i, cc = c + DC[d] * i;
                                for (int e = 0; e < 4; e++) { int r2 = rr + DR[e], c2 = cc + DC[e]; if (Inb(r2, c2) && occ[r2, c2] >= 0) n++; }
                            }
                            float cx = (c - (cols - 1) / 2f) / cols, cz = (r - (rows - 1) / 2f) / rows;
                            float sc = n * tight - (cx * cx + cz * cz) * 6 + (float)rnd.NextDouble() * 2.2f;
                            if (sc > best) { best = sc; br = r; bc = c; bd = d; }
                        }
                if (br < 0) return false;
                b.r = br; b.col = bc; b.dir = bd; Fill(b); b.p = Center(b); b.yaw = YawDir(bd); b.sc = cs;
            }
            return true;
        }

        // 正着推一局。caps：依次走掉的船（颜色、当时的座位）
        readonly List<KeyValuePair<int, int>> caps = new List<KeyValuePair<int, int>>();
        bool Forward(System.Random r, double hitP = 0.7)
        {
            var snap = new int[boats.Count][]; var occ0 = (int[,])occ.Clone();
            foreach (var b in boats) snap[b.id] = new[] { b.r, b.col, b.dir, b.len, b.cap, b.mlen };
            steps.Clear(); sol.Clear(); caps.Clear();
            int left = boats.Count, n = 0; bool ok = true;
            Action<SpBoat> gone = b => { Clear(b); b.st = GONE; sol.Add(b.id); caps.Add(new KeyValuePair<int, int>(b.c, b.cap)); left--; };
            while (left > 0)
            {
                var free = new List<SpBoat>(); var hit = new List<KeyValuePair<SpBoat, SpBoat>>();
                foreach (var b in boats)
                {
                    if (b.st != LOT) continue;
                    var h = Room(b, b.dir, out int f, out bool open);
                    if (open) { free.Add(b); continue; }
                    if (kind == SpKind.Bump && f > 0) { Room(h, b.dir, out _, out bool o2); if (o2) hit.Add(new KeyValuePair<SpBoat, SpBoat>(b, h)); }
                    if (kind == SpKind.Merge && h.c == b.c && h.cap + b.cap <= 10) hit.Add(new KeyValuePair<SpBoat, SpBoat>(b, h));
                }
                if (free.Count == 0 && hit.Count == 0) { ok = false; break; }
                if (hit.Count > 0 && (free.Count == 0 || r.NextDouble() < hitP))
                {
                    var p = hit[r.Next(hit.Count)]; var a = p.Key; var h = p.Value;
                    if (kind == SpKind.Bump)
                    {
                        Room(a, a.dir, out int f, out _); Shift(a, a.dir, f);
                        steps.Add(new SpStep { tap = a.id, leave = h.id }); gone(h);
                    }
                    else { ApplyMerge(a, h); a.st = GONE; left--; steps.Add(new SpStep { tap = a.id, leave = -1 }); }
                    continue;
                }
                var q = free[r.Next(free.Count)]; steps.Add(new SpStep { tap = q.id, leave = q.id }); gone(q); n++;
                if (kind == SpKind.Turn && n % turnN == 0) TurnDisc();
                if (kind == SpKind.Tilt && n % tiltN == 0) Slosh(TiltNext);
            }
            foreach (var b in boats)
            {
                var s = snap[b.id]; b.r = s[0]; b.col = s[1]; b.dir = s[2]; b.len = s[3]; b.cap = s[4]; b.mlen = s[5];
                b.st = LOT; b.p = Center(b); b.yaw = YawDir(b.dir); b.sc = cs;
            }
            occ = occ0; tiltI = 0; turnCount = 0;
            return ok;
        }
        List<Plan> SolPlans() { var l = new List<Plan>(); foreach (int id in sol) l.Add(new Plan { c = boats[id].c, len = boats[id].len }); return l; }
        static List<int> LineCaps(List<KeyValuePair<int, int>> g, System.Random r, bool mix)
        {
            var q = new List<int>();
            for (int k = 0; k < g.Count; k++)
            {
                var a = g[k];
                if (mix && k + 1 < g.Count && g[k + 1].Key != a.Key && r.NextDouble() < 0.45)
                {
                    var b = g[k + 1]; int ha = a.Value / 2, hb = b.Value / 2;
                    for (int i = 0; i < ha; i++) q.Add(a.Key); for (int i = 0; i < hb; i++) q.Add(b.Key);
                    for (int i = ha; i < a.Value; i++) q.Add(a.Key); for (int i = hb; i < b.Value; i++) q.Add(b.Key);
                    k++;
                }
                else for (int i = 0; i < a.Value; i++) q.Add(a.Key);
            }
            return q;
        }

        // ---------- 地图 ----------
        bool MakeSail(System.Random r)
        {
            Yard(8, 7, 1.35f, -4.725f, -1); Docks4();
            var ps = MakePlan(r, NB(13), NC(5)); AddQueue(0, MainQ(), Line(ps, r, true));
            sol = AddBoats(ps, 0); wind = r.Next(4); timer = 80;
            return Place(r, null);
        }

        bool MakeTurn(System.Random r)
        {
            Yard(9, 7, 1.2f, -4.2f, -1); Docks4();
            d0r = 2; d0c = 1; dN = 5; discC = (Cell(d0r, d0c) + Cell(d0r + dN - 1, d0c + dN - 1)) / 2;
            var ps = MakePlan(r, NB(14), NC(5));
            foreach (var p in ps) NewBoat(p, 0);
            for (int k = 0; k < Math.Min(6, Math.Max(1, ps.Count / 2)); k++) { ps[k].len = Mathf.Min(ps[k].len, 3); boats[k].disc = true; }
            Func<SpBoat, int, int, int, bool> ok = (b, rr, c, d) =>
            {
                int inside = 0; for (int i = 0; i < b.len; i++) if (OnDisc(rr + DR[d] * i, c + DC[d] * i)) inside++;
                return b.disc ? inside == b.len : inside == 0;
            };
            if (!Scatter(r, ok) || !Forward(r)) return false;
            AddQueue(0, MainQ(), Line(SolPlans(), r, true));
            return true;
        }

        bool MakeBump(System.Random r)
        {
            Yard(9, 7, 1.2f, -4.2f, -1); Docks4();
            foreach (var p in MakePlan(r, NB(15), NC(5))) NewBoat(p, 0);
            if (!Scatter(r, null, 0.35f) || !Forward(r)) return false;
            int bumps = 0; foreach (var s in steps) if (s.tap != s.leave) bumps++;
            if (bumps < Math.Max(1, Math.Min(3, boats.Count / 5))) return false;
            AddQueue(0, MainQ(), Line(SolPlans(), r, true));
            return true;
        }

        bool MakeMerge(System.Random r)
        {
            Yard(8, 7, 1.35f, -4.725f, -1);
            foreach (float x in new[] { -1.15f, 0f, 1.15f }) AddDock(0, x, -2.45f);
            for (int k = 0; k < NB(17); k++)
            {
                double v = r.NextDouble();
                NewBoat(new Plan { c = r.Next(NC(4)), len = v < 0.45 ? 1 : v < 0.9 ? 2 : 3 }, 0);
            }
            if (!Scatter(r, null)) return false;
            foreach (var a in boats) { var h = Room(a, a.dir, out _, out _); if (h != null && r.NextDouble() < 0.6) a.c = h.c; }   // 前后挨着的多半同色，才有得合
            if (!Forward(r)) return false;
            int joins = 0; foreach (var s in steps) if (s.leave < 0) joins++;
            if (joins < Math.Max(1, Math.Min(4, boats.Count / 4))) return false;
            AddQueue(0, MainQ(), LineCaps(caps, r, true));
            return true;
        }

        bool MakeWhirl(System.Random r)
        {
            Yard(8, 7, 1.35f, -4.725f, -1); Docks4();
            wr[0] = 5; wc[0] = 1; wr[1] = 2; wc[1] = 5; mask[wr[0], wc[0]] = 3; mask[wr[1], wc[1]] = 3; whirlD = r.Next(4);
            var ps = MakePlan(r, NB(13), NC(5)); AddQueue(0, MainQ(), Line(ps, r, true));
            sol = AddBoats(ps, 0); timer = 80;
            var via = new HashSet<int>(); foreach (int id in sol) if (r.NextDouble() < 0.35) via.Add(id);
            return Place(r, (b, rr, c, d) => !via.Contains(b.id) || PortalLane(b.len, rr, c, d));
        }

        bool MakePaint(System.Random r)
        {
            Yard(8, 7, 1.35f, -4.725f, -1); Docks4();
            pc0 = 2; pcN = 3;
            int nc = Math.Max(2, NC(5)); var seq = new List<int>(); for (int k = 0; k < nc; k++) seq.Add(k); for (int k = nc - 1; k > 0; k--) { int j = r.Next(k + 1); int t = seq[k]; seq[k] = seq[j]; seq[j] = t; }
            paintSeq = seq.ToArray();
            var ps = MakePlan(r, NB(13), nc); AddQueue(0, MainQ(), Line(ps, r, true));
            sol = AddBoats(ps, 0);
            // 4 艘要靠门变色；再放 2 艘备用船（4 座），变错了颜色还有机会救回来
            int nn = Math.Max(1, Math.Min(4, sol.Count / 3)); var need = new HashSet<int>(); while (need.Count < nn) need.Add(sol[r.Next(sol.Count)]);
            var spare = new HashSet<int>();
            for (int k = 0; k < (sol.Count >= 8 ? 2 : 1); k++) { var b = NewBoat(new Plan { c = r.Next(nc), len = 2 }, 0); sol.Add(b.id); spare.Add(b.id); }
            bool ok = Place(r, (b, rr, c, d) =>
            {
                bool lane = d == 0 && c >= pc0 && c < pc0 + pcN;
                if (spare.Contains(b.id)) return lane && rr >= 5;
                return need.Contains(b.id) == lane;
            });
            if (!ok) return false;
            foreach (int id in need) { var b = boats[id]; b.want = b.c; b.c = (b.c + 1 + r.Next(nc - 1)) % nc; }
            return true;
        }

        bool MakeTruck(System.Random r)
        {
            Harbor(8, 7, 1.25f, -4.375f, -0.9f); Lagoon(); Sand(-5.6f, -12.1f, 11.2f, 5.7f);
            Palms(-4.7f, -11.4f, 4.7f, -11.3f);
            NewIsl("主岛");
            foreach (float x in new[] { -1.725f, -0.575f, 0.575f, 1.725f }) AddDock(0, x, -5.35f);
            laneZ = -2.65f; float at = 0;
            while (at < 34)
            {
                float len = 3 + (float)r.NextDouble() * 3.5f;
                trucks.Add(new SpTruck { x0 = at + len / 2, len = len });
                at += len + 3 + (float)r.NextDouble() * 4.5f;
            }
            truckSpan = at;
            var ps = MakePlan(r, NB(13), NC(5)); AddQueue(0, Serp(-4.4f, 4.4f, -7.2f, -1.08f, 5), Line(ps, r, true));
            sol = AddBoats(ps, 0); timer = 80;
            return Place(r, null);
        }

        bool MakeTilt(System.Random r)
        {
            Yard(8, 7, 1.35f, -4.725f, -1); Docks4();
            tiltSeq = new int[40]; int last = -1;
            for (int k = 0; k < tiltSeq.Length; k++) { int d; do { d = r.Next(4); } while (d == last); tiltSeq[k] = last = d; }
            foreach (var p in MakePlan(r, NB(12), NC(5))) NewBoat(p, 0);
            if (!Scatter(r, null) || !Forward(r)) return false;
            AddQueue(0, MainQ(), Line(SolPlans(), r, true));
            return true;
        }

        bool MakeRope(System.Random r)
        {
            Yard(8, 7, 1.35f, -4.725f, -1); Docks4();
            var ps = MakePlan(r, NB(14), NC(5)); AddQueue(0, MainQ(), Line(ps, r, true));
            sol = AddBoats(ps, 0);
            if (!Place(r, null)) return false;
            for (int k = 0; k + 1 < sol.Count; k++)
            {
                if (r.NextDouble() > 0.72) continue;
                var a = boats[sol[k]]; var b = boats[sol[k + 1]]; a.mate = b.id; b.mate = a.id; k++;
            }
            return true;
        }

        bool MakeSushi(System.Random r)
        {
            Harbor(8, 7, 1.25f, -4.375f, -1f); Lagoon();
            Sand(-5.6f, -12.2f, 11.2f, 2.95f); Sand(-2.6f, -6.9f, 5.2f, 2.4f, 0.8f);
            Palms(-4.9f, -11.8f, 4.9f, -11.7f);
            NewIsl("转盘");
            // 跑道形转盘，顺时针
            var pts = new List<Vector2> { new Vector2(-2.2f, -8f), new Vector2(2.2f, -8f) };
            for (int k = 1; k <= 16; k++) { float a = (-90 + k * 180f / 16) * Mathf.Deg2Rad; pts.Add(new Vector2(2.2f + Mathf.Cos(a) * 2.3f, -5.7f + Mathf.Sin(a) * 2.3f)); }
            pts.Add(new Vector2(-2.2f, -3.4f));
            for (int k = 1; k <= 16; k++) { float a = (90 + k * 180f / 16) * Mathf.Deg2Rad; pts.Add(new Vector2(-2.2f + Mathf.Cos(a) * 2.3f, -5.7f + Mathf.Sin(a) * 2.3f)); }
            beltPts = pts.ToArray(); beltCum = Cum(pts); beltLen = beltCum[beltCum.Length - 1];
            beltEntry = new Vector2(0, -3.4f); beltWait = new Vector2(0, -2.5f);
            float bd = 1e9f; for (float s = 0; s < beltLen; s += 0.05f) { float dd = (BeltAt(s) - beltEntry).sqrMagnitude; if (dd < bd) { bd = dd; beltEntryS = s; } }
            for (int k = 0; k < 8; k++) { var d = docks[AddDock(0, 0, 0)]; d.belt = true; d.moving = true; d.fade = true; }
            SetSlots();
            int sn = NB(13); var pa = MakePlan(r, (sn + 1) / 2, NC(5)); var pb = MakePlan(r, Math.Max(1, sn / 2), NC(5));
            var outer = new[] { new Vector2(0, -9.6f), new Vector2(4.9f, -9.6f), new Vector2(4.9f, -10.45f), new Vector2(-4.9f, -10.45f), new Vector2(-4.9f, -11.3f), new Vector2(4.9f, -11.3f) };
            var inner = new[] { new Vector2(-2.2f, -5f), new Vector2(2.2f, -5f), new Vector2(2.2f, -5.55f), new Vector2(-2.2f, -5.55f), new Vector2(-2.2f, -6.1f), new Vector2(2.2f, -6.1f), new Vector2(2.2f, -6.65f), new Vector2(-2.2f, -6.65f) };
            AddQueue(0, outer, Line(pa, r, false)).station = new Vector2(0, -8);
            AddQueue(0, inner, Line(pb, r, false)).station = new Vector2(-4.5f, -5.7f);
            Merge(r, AddBoats(pa, 0), AddBoats(pb, 0));
            timer = 80;
            return Place(r, null);
        }
    }
}
