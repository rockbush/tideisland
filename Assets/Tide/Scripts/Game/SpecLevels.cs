// 特殊模式的 13 张地图：布局 + 生成器。
// 生成思路：先定好一个"解"（船的先后顺序 sol），队伍照这个顺序排；摆船时从最后走的那艘摆起，
// 每艘新摆的船，箭头前面不能有已经摆好的船 —— 这样按 sol 的顺序点，每艘都开得出去，一定解得开。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tide
{
    public partial class SpSim
    {
        public static readonly string[] NAMES = { "单岛单队", "双子岛·左右分流", "双队抢一个码头", "十字港·四岛", "长水道·先进后出", "环礁·旋转队伍", "水闸双港", "断桥双岛", "沙洲救援", "漂流码头岛", "港中岛·环形航道", "雾港·灯塔", "中转岛·接驳筏",
            "风帆港·看风开船", "转盘港·中间会转", "碰碰船·撞出去", "合船港·同色合体", "漩涡港·传送门", "变色门·彩虹水道", "货轮航道·过马路", "摇摆港·全场滑动", "双生船·绳子连着", "回转码头·寿司转盘" };
        public static readonly string[] ONE = {
            "现在的玩法，作为对照",
            "船从左边出港去左岛，从右边出港去右岛",
            "两个队头一起等，来什么颜色谁上",
            "船头朝哪边，就开去哪座岛",
            "只有最前面的船能走，不急着用的先进侧湾",
            "人绕着环一直转，把船放在人快走到的码头",
            "外港走够 3 艘，闸门才开 6 秒",
            "桥只能再过 N 个人，断了就要顾两个码头",
            "沙洲有自己的倒计时，先救谁自己选",
            "朝上的船对准漂流岛就去漂流岛",
            "船顺着水流绕圈，碰到空码头就停",
            "雾里看不清颜色，灯塔扫到才看得见",
            "点一堆人叫筏子接，自己安排排队顺序",
            "船没有箭头，能往哪开看现在的风",
            "中间的转盘每走 2 艘转 90°，朝向全变",
            "被挡住不弹回，把前面的船撞飞",
            "同色船撞上就合体，只占一个泊位",
            "开进漩涡，从另一个漩涡冒出来",
            "穿过彩虹门，门是什么色船就变什么色",
            "找货轮的空档冲过航道",
            "每走 2 艘，全场的船一起滑到底",
            "绳子连着的两艘一起走",
            "码头一直在转，船转到队头面前才上人" };
        public static readonly string[][] HOW = {
            new[] { "点船，沿箭头开出港湾，停到码头。", "队头按颜色上船，坐满开走。", "码头停满、队头等不到颜色就输。" },
            new[] { "朝左出港 → 左岛码头；朝右出港 → 右岛码头。", "朝上、朝下的船看在中线哪一边。", "船只接自己岛上的人，两边不串。", "目标码头满了，船会撞回来。任意一岛卡死就输。" },
            new[] { "一排码头，两条队共用，两个队头谁的颜色对谁上。", "一艘船可以由两条队一起坐满。", "先清空一条队：风暴 +10 秒。", "两个队头都等不到才算输。" },
            new[] { "船头朝上去北岛，朝右去东岛，朝下去南岛，朝左去西岛。", "每座岛的码头只接自己岛的人。", "颜色对、方向错的船现在是废船，先别动。", "任意一岛卡死就输。" },
            new[] { "每条水道只有最上面的船能走，走了下面的自动往前挪。", "队头要的颜色 → 直接去码头；不是队头要的 → 先进两边的侧湾寄存。", "侧湾里的船点一下就去码头。", "码头和侧湾都满了就卡死。" },
            new[] { "人绕着环一直顺时针走，没有固定的队头。", "点船，它开到离它最近的空码头。", "人走到码头前，同色有空座就跳上船。", "转满一圈风暴就加快。这一关只有风暴能输。" },
            new[] { "外港的船照常走；内港的船要等闸门开。", "外港每走 3 艘，闸门开 6 秒。", "闸门开着时点内港的船：穿过闸门、穿过外港出去。", "闸门上方那一列有船挡着也出不去。" },
            new[] { "前面和单岛一样，后岛的人过桥来排队。", "顶上的数字走到 0，桥断。", "桥断后后岛的人只能靠后岛码头。", "右上角朝右的船：桥断之前开不出去，桥断后专门去后岛。" },
            new[] { "右上角沙洲的人只能坐沙洲泊位上的船，有单独的倒计时。", "港湾上半部分朝右开的船去沙洲。", "救完：风暴 +20 秒；被淹：输。", "送错颜色会白占 3 秒。" },
            new[] { "朝上的船，出港那一刻漂流岛正好在这一列上方 → 去漂流岛。", "没对上 → 去左边的主码头。", "漂流岛的人只坐漂流岛的船。", "漂得很慢，看准了再点。" },
            new[] { "所有船只能顺时针走（看水面的白箭头）。", "点船，它顺着水流走，碰到第一个空码头就停。", "前面有船不动，它就堵在后面，要先点前面那艘。", "队头在中间，上下码头离得远，人上船慢一点。" },
            new[] { "规则和单岛一样，但雾里的船都是灰的。", "灯塔的光扫到才显出颜色，要记住。", "右下角的照明弹：整个港湾亮 5 秒（一局 2 次）。" },
            new[] { "最上面的远岛上，人按颜色扎堆。", "点一堆人，筏子去接（一趟 4 人），接来的排到队尾。", "筏子忙的时候再点，会记下一趟接谁。", "先接好放的颜色，难放的晚点接。" },
            new[] { "船没有箭头：横放的吃左右风，竖放的吃上下风。", "风向每 3 秒转一格，上→右→下→左，看右上角的风向标，小圆是下一阵。", "顺风的船会显示箭头，朝风吹的方向开；那头被挡住就开不了。", "吃不上风的船，点了只晃一下。" },
            new[] { "中间虚线框是转盘，外圈的船不动。", "每开走 2 艘，转盘顺时针转 90°，盘上的船跟着转。", "转完朝向变了，本来堵死的船可能就通了。", "按住盘上的船，黄线是它转完以后的朝向。" },
            new[] { "前面有船时不再弹回：开过去把它撞飞，自己停在撞击的地方。", "被撞的船沿撞它的方向滑，和它自己的箭头无关；滑出港湾就开去码头。", "还能连环撞，一下带动好几艘。", "贴着前面的船撞不动，至少隔一格助跑。", "小心把不要的颜色撞出去，白占泊位。" },
            new[] { "码头只有 3 个泊位。", "船开着碰到同色的船：两艘合成一艘大船，座位相加，最多 10 座。", "碰到不同颜色的船：照常弹回来。", "合体后船变大，会多占格子，可能挡住别的船。" },
            new[] { "港湾里有一对漩涡。船开进一个，从另一个冒出来。", "冒出来朝哪边开，看漩涡上的黄箭头，箭头每 3 秒转一格。", "出口那边被挡住，船就开不进去，等箭头转开。", "看着最难救的船，可能一步就出去了。" },
            new[] { "港湾上方中间有道彩虹门，门的颜色一直在变，右边小圆是下一个颜色。", "带颜色的那几列、箭头朝上的船会穿门：穿门那一刻门是什么色，船就变什么色。", "别的船不穿门，颜色不变。", "队伍要的颜色不够时，要靠门变出来。多备了 2 艘，变错了还能救。" },
            new[] { "码头和港湾中间是一条航道，货轮一直开过。", "点船，它开到航道边自己往上冲。", "撞上货轮：退回原位，风暴 -5 秒。", "船越大开得越慢，要等越大的空档。", "按住船不放，顶上会提示现在松手过不过得去。" },
            new[] { "每开走 2 艘，水晃一下：所有船沿晃的方向滑到底，碰到墙或别的船才停。", "船滑的时候朝向不变。", "右上角风向标是下一次晃的方向，小圆是再下一次。", "6 秒没有船开走，也会晃一下。" },
            new[] { "木绳连着的两艘船是一对，点哪艘都一起走。", "只要有一艘被挡住，两艘都不动。", "一起走要 2 个空泊位。", "没系绳子的船照常开。" },
            new[] { "码头是一圈一直顺时针转的转盘。", "点船，它开到下方入口，等空泊位转过来就上去。", "船跟着转，转到黄圈（队头面前）时，同色的人上船。", "转盘外一条队，转盘中间一条队。", "转盘停满，又没有船等得到人，就输了。" } };

        // ---------- 工具 ----------
        static Vector2[] Serp(float xa, float xb, float z, float dz, int rows)
        {
            var p = new List<Vector2>();
            for (int k = 0; k < rows; k++)
            {
                float a = k % 2 == 0 ? xa : xb, b = k % 2 == 0 ? xb : xa, zz = z + dz * k;
                p.Add(new Vector2(a, zz)); p.Add(new Vector2(b, zz));
            }
            return p.ToArray();
        }
        class Plan { public int c, len; }
        static List<Plan> MakePlan(System.Random r, int n, int colors, int[] only = null)
        {
            var l = new List<Plan>(); int last = -1;
            for (int k = 0; k < n; k++)
            {
                int c; do { c = only != null ? only[r.Next(only.Length)] : r.Next(colors); } while (c == last && colors > 1 && (only == null || only.Length > 1));
                double v = r.NextDouble();
                l.Add(new Plan { c = c, len = v < 0.2 ? 1 : v < 0.72 ? 2 : 3 }); last = c;
            }
            return l;
        }
        static int Seats(int len) => len == 1 ? 2 : len == 2 ? 4 : 6;
        // 队伍：一艘接一艘地坐满；mix 时相邻两艘穿插着来（要有 2 个空泊位才走得通）
        static List<int> Line(List<Plan> ps, System.Random r, bool mix)
        {
            var q = new List<int>();
            for (int k = 0; k < ps.Count; k++)
            {
                var a = ps[k];
                if (mix && k + 1 < ps.Count && ps[k + 1].c != a.c && r.NextDouble() < 0.45)
                {
                    var b = ps[k + 1]; int na = Seats(a.len), nb = Seats(b.len), ha = na / 2, hb = nb / 2;
                    for (int i = 0; i < ha; i++) q.Add(a.c); for (int i = 0; i < hb; i++) q.Add(b.c);
                    for (int i = 0; i < na - ha; i++) q.Add(a.c); for (int i = 0; i < nb - hb; i++) q.Add(b.c);
                    k++;
                }
                else for (int i = 0; i < Seats(a.len); i++) q.Add(a.c);
            }
            return q;
        }
        SpBoat NewBoat(Plan p, int isl) { var b = new SpBoat { id = boats.Count, c = p.c, len = p.len, cap = Seats(p.len), isl = isl }; boats.Add(b); return b; }
        SpIsland NewIsl(string name) { var i = new SpIsland { name = name }; isls.Add(i); return i; }
        int AddDock(int isl, float x, float z, float yaw = 0, bool bay = false) { docks.Add(new SpDock { x = x, z = z, yaw = yaw, isl = isl, bay = bay, ts = x }); int k = docks.Count - 1; isls[isl].docks.Add(k); return k; }
        SpQueue AddQueue(int isl, Vector2[] path, List<int> colors)
        {
            var q = new SpQueue { isl = isl }; q.SetPath(path); queues.Add(q); isls[isl].queues.Add(queues.Count - 1);
            foreach (int c in colors) AddGuy(q, c, queues.Count - 1);
            return q;
        }
        SpGuy AddGuy(SpQueue q, int c, int qi)
        {
            var g = new SpGuy { id = guys.Count, c = c, q = qi };
            if (q != null) { g.s = q.Slot(q.guys.Count); q.guys.Add(g); g.p = q.At(g.s); }
            guys.Add(g); return g;
        }
        void Harbor(int r, int c, float size, float x, float z)
        {
            rows = r; cols = c; cs = size; hx = x; hz = z; mask = new int[r, c]; occ = new int[r, c];
            for (int i = 0; i < r; i++) for (int j = 0; j < c; j++) { mask[i, j] = 1; occ[i, j] = -1; }
        }
        void Lagoon() => rects.Add(new SpRect("lagoon", hx - 0.1f, hz - 0.1f, cols * cs + 0.2f, rows * cs + 0.2f, 0.5f));
        void Sand(float x, float z, float w, float h, float r = 0.9f) => rects.Add(new SpRect("sand", x, z, w, h, r));
        void Prop(string m, float x, float z, float yaw = 0, float sc = 1) => props.Add(new SpProp { model = m, x = x, z = z, yaw = yaw, sc = sc });
        void Palms(params float[] xz) { for (int k = 0; k + 1 < xz.Length; k += 2) Prop(k % 4 == 0 ? "palm_a" : "palm_b", xz[k], xz[k + 1], k * 40); }

        // 摆船：从最后走的那艘摆起
        bool Place(System.Random rnd, Func<SpBoat, int, int, int, bool> accept)
        {
            if (mix) PutRocks(rnd);
            for (int k = sol.Count - 1; k >= 0; k--)
            {
                var b = boats[sol[k]]; float best = -1e9f; int br = -1, bc = -1, bd = -1;
                for (int d = 0; d < 4; d++)
                    for (int r = 0; r < rows; r++)
                        for (int c = 0; c < cols; c++)
                        {
                            if (!Fits(b.len, r, c, d) || (accept != null && !accept(b, r, c, d))) continue;
                            b.r = r; b.col = c; b.dir = d;
                            if (!Ray(b, out _, out _)) continue;
                            float n = 0;
                            for (int i = 0; i < b.len; i++)
                            {
                                int rr = r + DR[d] * i, cc = c + DC[d] * i;
                                for (int e = 0; e < 4; e++) { int r2 = rr + DR[e], c2 = cc + DC[e]; if (r2 >= 0 && c2 >= 0 && r2 < rows && c2 < cols && occ[r2, c2] >= 0 && occ[r2, c2] != b.id) n++; }
                            }
                            float cx = (c - (cols - 1) / 2f) / cols, cz = (r - (rows - 1) / 2f) / rows;
                            float sc = n - (cx * cx + cz * cz) * 6 + (float)rnd.NextDouble() * 2.2f;
                            if (sc > best) { best = sc; br = r; bc = c; bd = d; }
                        }
                if (br < 0) return false;
                b.r = br; b.col = bc; b.dir = bd;
                for (int i = 0; i < b.len; i++) occ[br + DR[bd] * i, bc + DC[bd] * i] = b.id;
                b.p = Center(b); b.yaw = YawDir(bd); b.sc = cs;
            }
            return true;
        }
        bool Fits(int len, int r, int c, int d)
        {
            for (int i = 0; i < len; i++)
            {
                int rr = r + DR[d] * i, cc = c + DC[d] * i;
                if (rr < 0 || cc < 0 || rr >= rows || cc >= cols || mask[rr, cc] != 1 || occ[rr, cc] >= 0) return false;
            }
            return true;
        }
        // 多条线按原来的先后合并成一个解
        void Merge(System.Random r, params List<int>[] lines)
        {
            var at = new int[lines.Length];
            for (; ; )
            {
                var open = new List<int>(); for (int k = 0; k < lines.Length; k++) if (at[k] < lines[k].Count) open.Add(k);
                if (open.Count == 0) return;
                int i = open[r.Next(open.Count)]; sol.Add(lines[i][at[i]++]);
            }
        }
        List<int> AddBoats(List<Plan> ps, int isl) { var l = new List<int>(); foreach (var p in ps) l.Add(NewBoat(p, isl).id); return l; }

        // ---------- 入口 ----------
        public static SpSim Build(SpKind kind, int seed)
        {
            for (int k = 0; k < 150; k++)
            {
                var s = new SpSim { kind = kind, seed = seed + k * 1000 };
                if (s.Make(new System.Random(s.seed))) return s;
            }
            return null;
        }
        // 用机器人打一遍：换几个种子，取第一张打得通的
        public static SpSim Good(SpKind kind, int seed)
        {
            for (int k = 0; k < 8; k++)
                if (Verify(kind, seed + k)) return Build(kind, seed + k);
            return Build(kind, seed);
        }
        public static bool Verify(SpKind kind, int seed, float[] tOut = null)
        {
            var s = Build(kind, seed); if (s == null) return false;
            for (int k = 0; k < 20 * 300; k++) { s.BotTick(0.05f); s.Tick(0.05f); s.evs.Clear(); if (s.status == 1 || s.status == 2) break; }
            if (tOut != null) { tOut[0] = s.t; tOut[1] = s.timer; }
            return s.status == 1;
        }

        bool Make(System.Random r)
        {
            switch (kind)
            {
                case SpKind.Base: case SpKind.Fog: return MakeBase(r);
                case SpKind.Twin: return MakeTwin(r);
                case SpKind.Dual: return MakeDual(r);
                case SpKind.Cross: return MakeCross(r);
                case SpKind.Canal: return MakeCanal(r);
                case SpKind.Ring: return MakeRing(r);
                case SpKind.Sluice: return MakeSluice(r);
                case SpKind.Bridge: return MakeBridge(r);
                case SpKind.Bar: return MakeBar(r);
                case SpKind.Drift: return MakeDrift(r);
                case SpKind.Loop: return MakeLoop(r);
                case SpKind.Ferry: return MakeFerry(r);
                case SpKind.Sail: return MakeSail(r);
                case SpKind.Turn: return MakeTurn(r);
                case SpKind.Bump: return MakeBump(r);
                case SpKind.Merge: return MakeMerge(r);
                case SpKind.Whirl: return MakeWhirl(r);
                case SpKind.Paint: return MakePaint(r);
                case SpKind.Truck: return MakeTruck(r);
                case SpKind.Tilt: return MakeTilt(r);
                case SpKind.Rope: return MakeRope(r);
                case SpKind.Sushi: return MakeSushi(r);
            }
            return false;
        }

        bool MakeBase(System.Random r)
        {
            Harbor(8, 7, 1.35f, -4.725f, -1); Lagoon(); Sand(-5.6f, -11.8f, 11.2f, 8.1f);
            Palms(-4.6f, -10.9f, 4.6f, -10.8f, 3.9f, -11.3f);
            NewIsl("主岛");
            foreach (float x in new[] { -1.725f, -0.575f, 0.575f, 1.725f }) AddDock(0, x, -2.45f);
            var ps = MakePlan(r, NB(14), NC(5)); AddQueue(0, Serp(-4.4f, 4.4f, -4.3f, -1.08f, 6), Line(ps, r, true));
            sol = AddBoats(ps, 0);
            if (kind == SpKind.Fog) { Prop("lighthouse", 4.6f, 10.4f, 0, 0.8f); }
            return Place(r, null);
        }

        bool MakeTwin(System.Random r)
        {
            Harbor(9, 8, 1.2f, -4.8f, -1); Lagoon(); rects.Add(new SpRect("dashv", 0, -1, 0, 10.8f));
            Sand(-5.6f, -11.8f, 5.25f, 8.1f); Sand(0.35f, -11.8f, 5.25f, 8.1f);
            Palms(-4.7f, -11f, 4.7f, -11f);
            NewIsl("左岛"); NewIsl("右岛");
            AddDock(0, -3.1f, -2.45f); AddDock(0, -1.95f, -2.45f); AddDock(1, 1.95f, -2.45f); AddDock(1, 3.1f, -2.45f);
            var pa = MakePlan(r, 7, 5); var pb = MakePlan(r, 7, 5);
            AddQueue(0, Serp(-0.9f, -4.6f, -4.3f, -1.08f, 6), Line(pa, r, true));
            AddQueue(1, Serp(0.9f, 4.6f, -4.3f, -1.08f, 6), Line(pb, r, true));
            Merge(r, AddBoats(pa, 0), AddBoats(pb, 1));
            return Place(r, (b, rr, c, d) => TwinSide(d, c) == b.isl && (d == 1 || d == 3 || c + 0 == c));
        }

        bool MakeDual(System.Random r)
        {
            Harbor(8, 7, 1.35f, -4.725f, -1); Lagoon(); Sand(-5.6f, -11.8f, 11.2f, 8.1f);
            Palms(0, -11.2f, -5, -7.4f, 5, -7.5f);
            NewIsl("主岛");
            foreach (float x in new[] { -1.725f, -0.575f, 0.575f, 1.725f }) AddDock(0, x, -2.45f);
            var ps = MakePlan(r, 14, 5); var pa = new List<Plan>(); var pb = new List<Plan>();
            foreach (var p in ps) (r.NextDouble() < 0.5 ? pa : pb).Add(p);
            if (pa.Count < 4 || pb.Count < 4) return false;
            AddQueue(0, Serp(-0.5f, -4.7f, -4.3f, -1.08f, 6), Line(pa, r, false));
            AddQueue(0, Serp(0.5f, 4.7f, -4.3f, -1.08f, 6), Line(pb, r, false));
            Merge(r, AddBoats(pa, 0), AddBoats(pb, 0));
            return Place(r, null);
        }

        bool MakeCross(System.Random r)
        {
            Harbor(6, 6, 1.1f, -3.3f, -3.9f); Lagoon();
            Sand(-5.8f, -12f, 11.6f, 6.3f); Sand(-5.8f, 4.7f, 11.6f, 6f);
            Sand(-5.9f, -5.4f, 1.9f, 10.8f, 0.6f); Sand(4f, -5.4f, 1.9f, 10.8f, 0.6f);
            Palms(-4.8f, -11f, 4.8f, -11.1f, -4.9f, 9.9f, 4.8f, 9.8f);
            NewIsl("北岛"); NewIsl("东岛"); NewIsl("南岛"); NewIsl("西岛");
            AddDock(0, -0.6f, -5.5f); AddDock(0, 0.6f, -5.5f);
            AddDock(1, 3.8f, -2.2f); AddDock(1, 3.8f, 0.4f);
            AddDock(2, -0.6f, 3.9f, Mathf.PI); AddDock(2, 0.6f, 3.9f, Mathf.PI);
            AddDock(3, -3.8f, -2.2f); AddDock(3, -3.8f, 0.4f);
            docks[2].fade = docks[3].fade = docks[6].fade = docks[7].fade = true;
            var lines = new List<int>[4];
            for (int k = 0; k < 4; k++)
            {
                var ps = MakePlan(r, Math.Max(1, NB(12) / 4), NC(4));
                var path = k == 0 ? Serp(-1.2f, 4.2f, -7.0f, -1.08f, 4) : k == 2 ? Serp(-1.2f, 4.2f, 5.9f, 1f, 4)
                    : k == 1 ? new[] { new Vector2(4.95f, -2.6f), new Vector2(4.95f, 4.6f) } : new[] { new Vector2(-4.95f, -2.6f), new Vector2(-4.95f, 4.6f) };
                AddQueue(k, path, Line(ps, r, true)); lines[k] = AddBoats(ps, k);
            }
            Merge(r, lines);
            return Place(r, (b, rr, c, d) => d == b.isl);
        }

        bool MakeCanal(System.Random r)
        {
            Harbor(9, 5, 1.25f, -3.125f, -1); Lagoon(); Sand(-5.6f, -11.8f, 11.2f, 8.1f);
            for (int i = 0; i < rows; i++) { mask[i, 1] = 0; mask[i, 3] = 0; }
            rects.Add(new SpRect("stone", hx + cs + 0.08f, hz, cs - 0.16f, rows * cs - 0.1f, 0.3f));
            rects.Add(new SpRect("stone", hx + 3 * cs + 0.08f, hz, cs - 0.16f, rows * cs - 0.1f, 0.3f));
            rects.Add(new SpRect("bay", -4.85f, 0.9f, 1.3f, 2.6f, 0.4f)); rects.Add(new SpRect("bay", 3.55f, 0.9f, 1.3f, 2.6f, 0.4f));
            Palms(-4.7f, -11f, 4.7f, -10.9f);
            NewIsl("主岛");
            foreach (float x in new[] { -1.15f, 0f, 1.15f }) AddDock(0, x, -2.45f);
            AddDock(0, -4.2f, 2.2f, 0, true); AddDock(0, 4.2f, 2.2f, 0, true);
            // 按解的顺序往三条水道里塞，先走的在上面；再交换相邻的两艘，逼玩家用侧湾
            var ps = new List<Plan>(); var fill = new int[3]; var stack = new List<Plan>[3];
            for (int k = 0; k < 3; k++) stack[k] = new List<Plan>();
            for (int guard = 0; guard < 80 && ps.Count < 14; guard++)
            {
                var p = MakePlan(r, 1, 5)[0]; if (ps.Count > 0 && ps[ps.Count - 1].c == p.c) continue;
                int ch = r.Next(3); if (fill[ch] + p.len > rows) continue;
                fill[ch] += p.len; stack[ch].Add(p); ps.Add(p);
            }
            AddQueue(0, Serp(-4.4f, 4.4f, -4.3f, -1.08f, 6), Line(ps, r, false));
            var ids = new Dictionary<Plan, SpBoat>();
            foreach (var p in ps) ids[p] = NewBoat(p, 0);
            for (int k = 0; k < 3; k++)
            {
                var st = stack[k];
                for (int i = 0; i + 1 < st.Count; i += 3) if (r.NextDouble() < 0.5) { var t = st[i]; st[i] = st[i + 1]; st[i + 1] = t; }
                int cur = 0;
                foreach (var p in st)
                {
                    var b = ids[p]; b.dir = 0; b.col = k * 2; b.r = cur + b.len - 1;
                    for (int i = 0; i < b.len; i++) occ[cur + i, b.col] = b.id;
                    cur += b.len; b.p = Center(b); b.yaw = 0; b.sc = cs; sol.Add(b.id);
                }
            }
            return ps.Count >= 10;
        }

        bool MakeRing(System.Random r)
        {
            Harbor(13, 7, 1.05f, -3.675f, -7.1f);
            ringC = new Vector2(0, -0.3f); ringA = 3.95f; ringB = 8.5f;
            rects.Add(new SpRect("ring", ringC.x, ringC.y, 5.45f, 10.1f) { r = 3.95f });
            rects.Add(new SpRect("ringin", ringC.x, ringC.y, 3.95f, 8.5f));
            NewIsl("环礁");
            float[] ang = { -90, 30, 150 };
            var pts = new List<Vector2>(); for (int k = 0; k <= 80; k++) { float a = k / 80f * Mathf.PI * 2; pts.Add(ringC + new Vector2(Mathf.Cos(a) * 4.7f, Mathf.Sin(a) * 9.3f)); }
            ringPts = pts.ToArray(); ringCum = Cum(pts); ringLen = ringCum[ringCum.Length - 1];
            foreach (float a in ang)
            {
                float t = a * Mathf.Deg2Rad; var p = ringC + new Vector2(Mathf.Cos(t) * 3.05f, Mathf.Sin(t) * 7.45f);
                float yaw = YawOf(p - ringC);
                int k = AddDock(0, p.x, p.y, yaw); docks[k].fade = true;
                var on = ringC + new Vector2(Mathf.Cos(t) * 4.7f, Mathf.Sin(t) * 9.3f); float bd = 1e9f;
                for (float s = 0; s < ringLen; s += 0.1f) { float dd = (RingAt(s) - on).sqrMagnitude; if (dd < bd) { bd = dd; docks[k].ringS = s; } }
            }
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                {
                    var p = Cell(i, j); float ex = (p.x - ringC.x) / 3.15f, ez = (p.y - ringC.y) / 7.3f;
                    if (ex * ex + ez * ez > 1) mask[i, j] = 2;
                    foreach (var d in docks) if ((p - d.P).magnitude < 1.5f) mask[i, j] = 2;
                }
            var ps = MakePlan(r, NB(11), NC(5)); var line = Line(ps, r, false);
            float gap = 0.62f, s0 = 0; int idx = 0, cap = Mathf.FloorToInt(ringLen / gap) - 2;
            if (line.Count > cap) return false;
            float extra = (ringLen - line.Count * gap) / Mathf.Max(1, ps.Count);
            foreach (var p in ps)
            {
                for (int i = 0; i < Seats(p.len); i++) { var g = AddGuy(null, line[idx++], -1); g.st = 5; g.s = s0; g.p = RingAt(s0); s0 += gap; }
                s0 += extra;
            }
            sol = AddBoats(ps, 0);
            // 先走的靠外
            return Place(r, null);
        }

        bool MakeSluice(System.Random r)
        {
            Harbor(11, 8, 1.05f, -4.2f, -1); Lagoon(); Sand(-5.6f, -11.8f, 11.2f, 8.1f);
            wallRow = 5; gate0 = 3;
            for (int c = 0; c < cols; c++) mask[wallRow, c] = 0;
            float wz = hz + wallRow * cs + 0.06f;
            rects.Add(new SpRect("stone", hx, wz, gate0 * cs - 0.05f, cs - 0.12f, 0.2f));
            rects.Add(new SpRect("stone", hx + (gate0 + 2) * cs + 0.05f, wz, (cols - gate0 - 2) * cs - 0.05f, cs - 0.12f, 0.2f));
            rects.Add(new SpRect("gate", hx + gate0 * cs, wz, 2 * cs, cs - 0.12f, 0));
            Palms(-4.7f, -11f, 4.7f, -10.9f);
            NewIsl("主岛");
            foreach (float x in new[] { -1.725f, -0.575f, 0.575f, 1.725f }) AddDock(0, x, -2.45f);
            var ps = MakePlan(r, 15, 5); AddQueue(0, Serp(-4.4f, 4.4f, -4.3f, -1.08f, 6), Line(ps, r, false));
            sol = AddBoats(ps, 0);
            gateLeft = gateNeed;
            // 解：外外外 内内 外外外 内内 …
            var kinds = new bool[sol.Count]; int firstInner = -1;
            for (int k = 0; k < sol.Count; k++) { kinds[k] = k % 5 >= 3; if (kinds[k] && firstInner < 0) firstInner = k; }
            var order = new Dictionary<int, int>();
            for (int k = 0; k < sol.Count; k++) { boats[sol[k]].inner = kinds[k]; order[sol[k]] = k; }
            return Place(r, (b, rr, c, d) =>
            {
                for (int i = 0; i < b.len; i++)
                {
                    int r2 = rr + DR[d] * i, c2 = c + DC[d] * i;
                    if (b.inner ? r2 < wallRow + 2 : r2 >= wallRow) return false;      // 闸门下面留一条通道
                    if (!b.inner && c2 == gate0 && order[b.id] > firstInner) return false;   // 闸门上方这一列要留给内港的船
                }
                if (b.inner) return d != 2;
                return d != 2;
            });
        }

        bool MakeBridge(System.Random r)
        {
            Harbor(8, 7, 1.3f, -4.55f, 0.15f); Lagoon();
            Sand(-5.6f, -12f, 7.9f, 3.5f); Sand(-5.6f, -5.9f, 8.2f, 4.3f);
            rects.Add(new SpRect("bridge", -2.55f, -8.65f, 1.1f, 2.9f, 0));
            Palms(1.5f, -6.6f + 1.6f, -5, -3f, 1.7f, -11.3f);
            NewIsl("前岛"); NewIsl("后岛");
            foreach (float x in new[] { -3.3f, -2.15f, -1f, 0.15f }) AddDock(0, x, -1.15f);
            AddDock(1, 3.5f, -11.1f, -Mathf.PI / 2); AddDock(1, 3.5f, -9.7f, -Mathf.PI / 2);
            foreach (int di in isls[1].docks) { var d = docks[di]; d.on = false; d.via = new[] { new Vector2(4.95f, -1.2f), new Vector2(4.95f, d.z) }; }
            laneRows = 3;
            rects.Add(new SpRect("lanebar", hx + cols * cs + 0.05f, hz + 0.05f, 0.2f, laneRows * cs - 0.1f, 0));
            // 前岛的人 = 桥能过的 + 桥这头站着的；后岛的人留到桥断
            List<Plan> pf, pr; int F = 13, nf, nr;
            pf = MakePlan(r, 8, 5); pr = MakePlan(r, 4, 5);
            var lf = Line(pf, r, false); var lr = Line(pr, r, false); nf = lf.Count; nr = lr.Count;
            if (nf <= F + 4) return false;
            var head = new List<Vector2> { new Vector2(-4.6f, -2.2f), new Vector2(-2f, -2.2f), new Vector2(-2f, -5.75f) };
            var q0 = new SpQueue { isl = 0, F = F };
            float hl = 2.6f + 3.55f;
            var rear = new[] { new Vector2(-2f, -8.75f), new Vector2(1.6f, -8.9f), new Vector2(1.6f, -9.7f), new Vector2(-5f, -9.7f), new Vector2(-5f, -10.5f), new Vector2(1.6f, -10.5f), new Vector2(1.6f, -11.3f), new Vector2(-5f, -11.3f) };
            var all = new List<Vector2>(head); all.AddRange(rear);
            q0.SetPath(all.ToArray()); q0.gap = hl / F; float sB = hl + 3f;
            q0.jump = sB - F * q0.gap; sBridgeEnd = sB;
            queues.Add(q0); isls[0].queues.Add(0);
            var q1 = new SpQueue { isl = 1 }; q1.SetPath(rear); queues.Add(q1); isls[1].queues.Add(1);
            q0.gap = hl / F;
            foreach (int c in lf) AddGuy(q0, c, 0);
            foreach (int c in lr) AddGuy(q0, c, 0);
            bridgeLeft = nf - F;
            var a = AddBoats(pf, 0); var bs = AddBoats(pr, 1);
            sol.AddRange(a); sol.AddRange(bs);
            return Place(r, (b, rr, c, d) => b.isl == 1 ? LaneBoat(d, rr) : !LaneBoat(d, rr));
        }

        bool MakeBar(System.Random r)
        {
            Harbor(8, 7, 1.35f, -4.725f, -1); Lagoon();
            Sand(-5.6f, -11.8f, 8.1f, 8.1f); rects.Add(new SpRect("bar", 3.1f, -8f, 2.6f, 3.2f, 0.9f));
            Palms(-4.7f, -11f, 1.6f, -11.1f);
            NewIsl("主岛"); NewIsl("沙洲");
            foreach (float x in new[] { -3.8f, -2.65f, -1.5f, -0.35f }) AddDock(0, x, -2.45f);
            barDock = AddDock(1, 4.2f, -2.9f);
            barColor = r.Next(5); barMax = barT = 25;
            var others = new List<int>(); for (int c = 0; c < 5; c++) if (c != barColor) others.Add(c);   // 主岛不用沙洲的颜色：沙洲的人不会把主岛的船坐掉
            var ps = MakePlan(r, 12, 5, others.ToArray());
            var pb = new List<Plan> { new Plan { c = barColor, len = 2 }, new Plan { c = barColor, len = 2 } };
            AddQueue(0, Serp(-4.6f, 1.5f, -4.3f, -1.08f, 6), Line(ps, r, true));
            var line = new List<int>(); for (int k = 0; k < 8; k++) line.Add(barColor);
            barQ = queues.Count; AddQueue(1, Serp(3.45f, 5.1f, -5.2f, -0.55f, 5), line);
            var a = AddBoats(ps, 0); var bb = AddBoats(pb, 1);
            // 沙洲的船排在第 3、4 艘附近：前面有一两艘挡路，要自己决定先后
            sol.AddRange(a); sol.Insert(2, bb[0]); sol.Insert(4, bb[1]);
            return Place(r, (b, rr, c, d) => b.isl != 1 || BarBoat(d, rr));
        }

        bool MakeDrift(System.Random r)
        {
            Harbor(8, 7, 1.35f, -4.725f, -1); Lagoon(); Sand(-5.6f, -11.9f, 11.2f, 4.4f);
            Palms(-4.9f, -11.3f, 4.9f, -11.2f);
            NewIsl("主岛"); NewIsl("漂流岛");
            float[] mz = { -6.5f, -5.2f, -3.9f };
            foreach (float z in mz) { int k = AddDock(0, -4.5f, z, -Mathf.PI / 2); docks[k].exit = new Vector2(-6, 0); docks[k].via = new[] { new Vector2(-4.95f, -1.6f), new Vector2(-3f, z) }; }
            driftMid = 0.6f; driftAmp = 2f; driftX = driftMid;
            foreach (float x in new[] { -0.575f, 0.575f }) { int k = AddDock(1, driftMid + x, -2.5f); docks[k].moving = true; docks[k].ts = docks[k].x; }
            rects.Add(new SpRect("drift", driftMid - 1.6f, -5.3f, 3.2f, 1.9f, 0.8f));
            int dn = Math.Max(1, NB(13) / 4); var pm = MakePlan(r, NB(13) - dn, NC(5)); var pd = MakePlan(r, dn, NC(5));
            AddQueue(0, Serp(-4.4f, 4.4f, -7.9f, -1f, 4), Line(pm, r, true));
            driftQ = queues.Count;
            AddQueue(1, Serp(driftMid - 1.15f, driftMid + 1.15f, -3.85f, -0.42f, 4), Line(pd, r, false));
            Merge(r, AddBoats(pm, 0), AddBoats(pd, 1));
            return Place(r, (b, rr, c, d) =>
            {
                if (b.isl != 1) return true;
                var x = hx + (c + 0.5f) * cs;
                return d == 0 && x > driftMid - driftAmp - driftW / 2 + 0.2f && x < driftMid + driftAmp + driftW / 2 - 0.2f;
            });
        }

        bool MakeLoop(System.Random r)
        {
            Harbor(1, 1, 1, 0, 0);
            float x = 4.25f, z0 = -9.7f, z1 = 9.5f;
            var pts = new List<Vector2> { new Vector2(-x, z0), new Vector2(x, z0), new Vector2(x, z1), new Vector2(-x, z1), new Vector2(-x, z0) };
            trackPts = pts.ToArray(); trackCum = Cum(pts); trackLen = trackCum[trackCum.Length - 1];
            rects.Add(new SpRect("lane", -x, z0, 2 * x, z1 - z0));
            Sand(-2.55f, -6.9f, 5.1f, 13.7f, 1f);
            Palms(-1.7f, -6f, 1.7f, -6f, -1.7f, 5.9f, 1.7f, 5.9f);
            NewIsl("中岛");
            int kN = AddDock(0, 0, -7.95f), kE = AddDock(0, 3.2f, 0), kS = AddDock(0, 0, 7.75f, Mathf.PI), kW = AddDock(0, -3.2f, 0, Mathf.PI);
            docks[kN].ts = x; docks[kE].ts = 2 * x + (0 - z0); docks[kS].ts = 2 * x + (z1 - z0) + x; docks[kW].ts = 2 * x + (z1 - z0) + 2 * x + (z1 - 0);
            docks[kN].exit = new Vector2(0, -7); docks[kS].exit = new Vector2(0, 7); docks[kE].exit = new Vector2(7, 0); docks[kW].exit = new Vector2(-7, 0);
            var ps = MakePlan(r, Math.Min(20, NB(15)), NC(5));
            AddQueue(0, Serp(-1.9f, 1.9f, -2.4f, 0.8f, 7), Line(ps, r, false));
            sol = AddBoats(ps, 0);
            // 航道被四个码头分成四段：先走的船排在离下一个码头最近的地方，后走的跟在后面（点错顺序就会堵）
            var cnt = new int[4];
            foreach (int id in sol)
            {
                int sec; do { sec = r.Next(4); } while (cnt[sec] >= 5);
                var b = boats[id]; b.track = true;
                b.ts = Mathf.Repeat(docks[sec].ts - 1.3f - cnt[sec]++ * (trackGap + 0.25f), trackLen);
                b.p = TrackAt(b.ts); b.yaw = YawOf(TrackAt(b.ts + 0.1f) - b.p); b.sc = LotSc(b);
            }
            return true;
        }

        bool MakeFerry(System.Random r)
        {
            Harbor(8, 7, 1.35f, -4.725f, -1); Lagoon();
            Sand(-5.6f, -12.2f, 11.2f, 3.7f); Sand(-5.6f, -5.75f, 11.2f, 2.05f, 0.6f);
            NewIsl("近岛");
            foreach (float x in new[] { -1.725f, -0.575f, 0.575f, 1.725f }) AddDock(0, x, -2.45f);
            var ps = MakePlan(r, NB(13), Math.Min(5, NC(5)));
            var pts = new[] { new Vector2(4.6f, -3.98f), new Vector2(-4.6f, -3.98f), new Vector2(-4.6f, -4.72f), new Vector2(3.9f, -4.72f), new Vector2(3.9f, -5.5f) };
            AddQueue(0, pts, new List<int>());
            ferryHome = ferryP = new Vector2(3.9f, -6.5f);
            var counts = new int[5]; foreach (var p in ps) counts[p.c] += Seats(p.len);
            int n = 0; for (int c = 0; c < 5; c++) if (counts[c] > 0) n++;
            int at = 0;
            for (int c = 0; c < 5; c++)
            {
                if (counts[c] == 0) continue;
                var pl = new SpPile { c = c, x = -4.1f + 8.2f * (at + 0.5f) / n, z = -10.2f }; at++;
                for (int k = 0; k < counts[c]; k++)
                {
                    var g = AddGuy(null, c, -1); g.st = 4;
                    int side = Mathf.Min(5, 8 / n + 2);
                    g.p = new Vector2(pl.x + (k % side - (side - 1) / 2f) * 0.36f, pl.z + 1.1f - (k / side) * 0.38f); pl.guys.Add(g);
                }
                piles.Add(pl);
            }
            sol = AddBoats(ps, 0);
            return Place(r, null);
        }
    }
}
