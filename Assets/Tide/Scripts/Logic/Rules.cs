// 规则层（纯逻辑，不碰 Unity）。最早是 boat.html 里 //<LOGIC> 段的逐行翻译，现在是唯一的规则来源：
// 游戏和关卡工厂（island_dev/Tools/Factory，dotnet run）都编译这一个文件。
// 港湾是一块方格（rot=1 时整块转 45°，只是画面不同）。船宽 1 格、长 1~4 格，只能顺着船头方向直直开：
// 船头前方一路到港湾边上都没东西挡，才开得出去；开出去的船停进码头的空泊位。
// 排队的岛民从队头开始，碰上泊位里同色、还有空座的船就自己上船；坐满了船就开走，泊位空出来。
// 泊位全停满、队头的人又没船可上 = 输。所有人都上船了 = 赢。
//
//
// 风暴（第 4 关起）：cfg.storm 秒倒计时（点第一下开始走），每开走一艘坐满的船加 cfg.bonus 秒（最多回到 storm+10）。走完 = 输。
// 计时在画面层按真实秒数走；关卡工厂用带计时的 Playout 按"人每点一下花 pace 秒"模拟。
//
// 第 15 关起的新机制（cfg 里是 0 就完全不出现，也不多用一个随机数，老关卡不变）：
//   海藻 weed：船被海藻缠住，港湾里再开走 N 艘别的船才松开（Boat.weed = 要开走几艘，跟 State.gone 比）
//   钥匙锁链 locks：一艘船被锁链锁住，挂钥匙的那艘船开走了才解开（Boat.lockBy = 钥匙船）
//   鲨鱼 shark：沿港湾一边来回游（Lot.sharks），船往那边开、出口正好碰上鲨鱼就被吓回来；要算时间（State.t，秒）
// 直升机（Crane）不管这些，照样吊走。
//
// 特殊船（2026-10-22 原型：连击奖励，画面层在连击 3/6/9… 时把港湾里一艘船变成特殊船；工厂出的关卡里没有，sp 永远是 0）：
//   集合船 sp=1：港湾里一艘船变的；一靠码头，队伍里同色的人插队到最前面，直接坐满（GatherQ）
//   彩虹船 sp=2：额外送的一艘船（AddRainbow，不占港湾格子），从海上直接开进空泊位，什么颜色的人都能上、而且优先上它
//     （不能拿港湾里现成的船变：那色的座位少了，最后那色的人没船坐，死局）
//   彩虹船会"抢"别的颜色的人，于是某个颜色的人可能先救完、那色的船还空着座位：
//   规则加一条——某个颜色的人一个都不剩了，码头上那色的船直接开走（正常关卡座位数 = 人数，这条永远碰不到）
//
// 功能人（2026-10-28 用户：每种颜色以后都有一个功能人；工厂出的关卡里没有，画面层开局时 MarkSpecial 标上）：
//   Lot.qx / State.qx 和 queue / q 一一对应，是每个人的能力（0 普通人）；q 怎么重排 qx 就怎么跟着排。
//   撑杆跳 POLE（各种颜色都有）：他一上船，队伍里（不管排在哪）同色的人撑杆跳上这艘船，把空座补满；人不够就有几个算几个（Vault）
//
// ⚠ 改了 GenLevel（出关卡）里的随机数调用顺序，同一个 seed 就会变成另一关：之后要重跑工厂，重新量难度。
using System;
using System.Collections.Generic;

namespace Tide
{
    [Serializable]
    public class Cfg
    {
        public int rot, nb, colors, berths, plan, run, w, h, rocks, houses, houseN;
        public int[] sizes;
        public double bring, lifo, deep, dig, mys;
        public double storm, bonus;     // 风暴倒计时（秒，0 = 这关没有风暴）、每开走一艘满船加几秒
        // 队伍"爽感"（简单关用）：chunk = 一串人一次把那艘船的座位排满的概率（不再拆成小段夹在别的颜色里）；
        // chain = 下一串优先接同颜色的船的概率（红色后面再跟一串红色，连成长串）
        public double chunk, chain;
        public int tag;                 // 0 普通 / 1 困难 / 2 超难（开局显示标签）
        public int weed, locks, shark;  // 几艘被海藻缠住的船、几对钥匙锁链、几条鲨鱼
        public double sharkV;           // 鲨鱼速度（格/秒，0 = 默认 1.4）
    }

    [Serializable]
    public class LevelEntry { public Cfg cfg; public long seed; public double p; }

    [Serializable]
    public class LevelList { public LevelEntry[] levels; }

    public class Boat
    {
        public int len, cap, d, h = -1, mys, c;
        public int weed;                  // >0：被海藻缠住，港湾里一共开走这么多艘船以后才松开
        public int lockBy = -1, key = -1; // lockBy：锁住它的钥匙在哪艘船上；key：这艘船挂着第几把钥匙
        public int[] cells;   // 船尾 → 船头
        public int sp;        // 特殊船：0 普通 / 1 集合船 / 2 彩虹船（见文件开头）
    }

    // 鲨鱼：沿着港湾的 d 那一边（船头朝 d 的船从这一边出去）来回游，pos 是出口那条线的横坐标（格），在 lo~hi 之间
    public class Shark { public int d; public double lo, hi, v, ph; }

    public class House
    {
        public int cell, d;
        public List<int> list = new List<int>();
    }

    public class Lot
    {
        public int rot, NU, NV;
        public double cu, cv;
        public byte[] ins;          // 0 港湾外，1 水面，2 礁石，3 船坞
        public List<Boat> boats = new List<Boat>();
        public List<House> houses = new List<House>();
        public List<Shark> sharks = new List<Shark>();
        public List<int> queue = new List<int>();
        public List<byte> qx;       // 每个人的能力（null = 都是普通人），见文件头"功能人"
        public int berths, total;
        public Cfg cfg;
        public List<Lot> waves; public int wave;   // 涨潮分波：后面还没进港湾的几波（null = 普通关），见 Tide()
    }

    public class State
    {
        public short[] occ;
        public byte[] inl, load, seen;
        public int[] arr;
        public List<int> hq, berth, freed, q;
        public List<byte> qx;    // 和 q 一一对应（null = 都是普通人）
        public int qi, clock, taps;
        public int gone;         // 离开港湾的船有几艘（海藻用）
        public double t;         // 这局过了几秒（鲨鱼用；画面层每帧更新，工厂按点击节奏推进）。< 0 = 不管鲨鱼（往前看一步时用）
        public string status;

        public State Clone()
        {
            return new State
            {
                occ = (short[])occ.Clone(), inl = (byte[])inl.Clone(), load = (byte[])load.Clone(), seen = (byte[])seen.Clone(),
                arr = (int[])arr.Clone(), hq = new List<int>(hq), berth = new List<int>(berth), freed = new List<int>(freed),
                q = new List<int>(q), qx = qx == null ? null : new List<byte>(qx), qi = qi, clock = clock, taps = taps, status = status, gone = gone, t = t
            };
        }
    }

    public class Ev
    {
        public string t, how;
        public int b = -1, k = -1, n, hit, h = -1, seat, qi;
        public int[] list;   // vault：跳上船的人原来排在队头后面第几个
    }

    public struct Way { public bool free; public int n, hit; }

    public class Rng
    {
        uint a;
        public Rng(long seed)
        {
            // JS: (a>>>0)||1，a 是可能超过 2^32 的整数（double）
            ulong u = (ulong)(seed % 4294967296L + 4294967296L) % 4294967296UL;
            a = (uint)u;
            if (a == 0) a = 1;
        }
        public double Next()
        {
            unchecked
            {
                a = a + 0x6D2B79F5u;
                uint t = (a ^ (a >> 15)) * (1u | a);
                t = (t + (t ^ (t >> 7)) * (61u | t)) ^ t;
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }
        public int Int(int n) { return (int)Math.Floor(Next() * n); }
    }

    public static class Rules
    {
        public static readonly int[][] DIR4 = { new[] { 1, 0 }, new[] { 0, 1 }, new[] { -1, 0 }, new[] { 0, -1 } };
        public const double H2 = 0.70710678118654757;
        public static int Seats(int len) { return len == 1 ? 2 : len == 2 ? 4 : len == 3 ? 6 : 10; }

        static int Or(int v, int d) { return v != 0 ? v : d; }
        static double Or(double v, double d) { return v != 0 ? v : d; }

        public static List<T> Shuffle<T>(List<T> a, Rng r)
        {
            for (int i = a.Count - 1; i > 0; i--) { int j = (int)Math.Floor(r.Next() * (i + 1)); T t = a[i]; a[i] = a[j]; a[j] = t; }
            return a;
        }

        // 格子 → 网页版的世界坐标（x 向右，z 朝屏幕下方）。画面层自己换成 Unity 坐标
        public static void CellXZ(Lot L, int i, out double x, out double z)
        {
            double u = i % L.NU - L.cu, v = Math.Floor((double)i / L.NU) - L.cv;
            if (L.rot != 0) { x = (u - v) * H2; z = (u + v) * H2; } else { x = u; z = v; }
        }
        public static void DirXZ(Lot L, int d, out double x, out double z)
        {
            var a = DIR4[d];
            if (L.rot != 0) { x = (a[0] - a[1]) * H2; z = (a[0] + a[1]) * H2; } else { x = a[0]; z = a[1]; }
        }

        public static Lot MakeLot(Cfg cfg)
        {
            int rot = cfg.rot != 0 ? 1 : 0;
            double A = cfg.w / 2.0, B = cfg.h / 2.0;
            int NU, NV;
            if (rot != 0) { NU = NV = 2 * (int)Math.Ceiling((A + B) * H2) + 3; } else { NU = cfg.w; NV = cfg.h; }
            var L = new Lot { rot = rot, NU = NU, NV = NV, cu = (NU - 1) / 2.0, cv = (NV - 1) / 2.0 };
            L.ins = new byte[NU * NV];
            for (int i = 0; i < NU * NV; i++)
            {
                CellXZ(L, i, out double x, out double z);
                if (rot == 0 || (Math.Abs(x) <= A - 0.36 && Math.Abs(z) <= B - 0.36)) L.ins[i] = 1;
            }
            return L;
        }

        public static int StepC(Lot L, int i, int d)
        {
            int u = i % L.NU + DIR4[d][0], v = i / L.NU + DIR4[d][1];
            if (u < 0 || v < 0 || u >= L.NU || v >= L.NV) return -1;
            int j = v * L.NU + u;
            return L.ins[j] != 0 ? j : -1;
        }

        // 船头前方一路到港湾边：n = 前面空着几格，hit = 挡路的船（-2 = 礁石或船坞）
        public static Way PathOf(Lot L, State s, int b)
        {
            var B = L.boats[b];
            int j = B.cells[B.cells.Length - 1], n = 0;
            for (; ; )
            {
                j = StepC(L, j, B.d);
                if (j < 0) return new Way { free = true, n = n, hit = -1 };
                if (L.ins[j] >= 2) return new Way { free = false, n = n, hit = -2 };
                int o = s.occ[j];
                if (o >= 0) return new Way { free = false, n = n, hit = o };
                n++;
            }
        }

        // ---- 新机制 ----
        public static int WeedLeft(Lot L, State s, int b) => Math.Max(0, L.boats[b].weed - s.gone);
        public static bool Chained(Lot L, State s, int b) { int k = L.boats[b].lockBy; return k >= 0 && s.arr[k] == 0; }   // 钥匙船还没离开港湾
        public static bool Stuck(Lot L, State s, int b) => WeedLeft(L, s, b) > 0 || Chained(L, s, b);
        public static bool CanGo(Lot L, State s, int b) => !Stuck(L, s, b) && PathOf(L, s, b).free;

        // 出口那条线的横坐标：船头朝 ±u 走的船看 v，朝 ±v 走的看 u
        public static int Perp(Lot L, int cell, int d) => d % 2 == 0 ? cell / L.NU : cell % L.NU;
        public static double SharkPos(Shark k, double t)
        {
            double span = k.hi - k.lo; if (span <= 0) return k.lo;
            double x = (t * k.v / span + k.ph) % 2; if (x < 0) x += 2;
            return k.lo + span * (x < 1 ? x : 2 - x);
        }
        // 船现在开出去会不会正好撞上鲨鱼：返回第几条鲨鱼，-1 = 不会
        public static int SharkHit(Lot L, State s, int b)
        {
            if (s.t < 0 || L.sharks.Count == 0) return -1;
            var B = L.boats[b]; int p = Perp(L, B.cells[B.cells.Length - 1], B.d);
            for (int k = 0; k < L.sharks.Count; k++)
                if (L.sharks[k].d == B.d && Math.Abs(p - SharkPos(L.sharks[k], s.t)) < 1.0) return k;
            return -1;
        }
        // 鲨鱼在格子坐标里的位置（画面层换成世界坐标）：出口那条线上、港湾外面一点
        public static void SharkUV(Lot L, Shark k, double t, out double u, out double v)
        {
            int mn = int.MaxValue, mx = int.MinValue;
            for (int i = 0; i < L.ins.Length; i++) if (L.ins[i] != 0) { int a = k.d % 2 == 0 ? i % L.NU : i / L.NU; mn = Math.Min(mn, a); mx = Math.Max(mx, a); }
            double along = k.d < 2 ? mx + 1.0 : mn - 1.0, p = SharkPos(k, t);
            if (k.d % 2 == 0) { u = along; v = p; } else { u = p; v = along; }
        }
        public static void UVtoXZ(Lot L, double u, double v, out double x, out double z)
        {
            u -= L.cu; v -= L.cv;
            if (L.rot != 0) { x = (u - v) * H2; z = (u + v) * H2; } else { x = u; z = v; }
        }

        public static State NewState(Lot L)
        {
            int nb = L.boats.Count;
            var s = new State
            {
                occ = new short[L.NU * L.NV], inl = new byte[nb], load = new byte[nb], arr = new int[nb], seen = new byte[nb],
                hq = new List<int>(), berth = new List<int>(), freed = new List<int>(), q = new List<int>(L.queue), qx = L.qx == null ? null : new List<byte>(L.qx),
                qi = 0, clock = 0, status = "play", taps = 0
            };
            for (int i = 0; i < s.occ.Length; i++) s.occ[i] = -1;
            for (int h = 0; h < L.houses.Count; h++) s.hq.Add(0);
            for (int k = 0; k < L.berths; k++) { s.berth.Add(-1); s.freed.Add(0); }
            for (int b = 0; b < nb; b++) if (L.boats[b].h < 0) Put(L, s, b);
            Spawn(L, s, null); Reveal(L, s, null);
            return s;
        }

        static void Put(Lot L, State s, int b) { foreach (int j in L.boats[b].cells) s.occ[j] = (short)b; s.inl[b] = 1; }

        // 船坞：门口空了，下一艘船开出来
        static void Spawn(Lot L, State s, List<Ev> ev)
        {
            for (int h = 0; h < L.houses.Count; h++)
            {
                var H = L.houses[h];
                if (s.hq[h] >= H.list.Count) continue;
                int b = H.list[s.hq[h]];
                bool clear = true;
                foreach (int j in L.boats[b].cells) if (s.occ[j] >= 0) { clear = false; break; }
                if (clear) { Put(L, s, b); s.hq[h]++; if (ev != null) ev.Add(new Ev { t = "spawn", b = b, h = h }); }
            }
        }

        // 问号船：前面的路一通，就露出颜色
        static void Reveal(Lot L, State s, List<Ev> ev)
        {
            for (int b = 0; b < L.boats.Count; b++)
                if (s.inl[b] != 0 && L.boats[b].mys != 0 && s.seen[b] == 0 && PathOf(L, s, b).free)
                { s.seen[b] = 1; if (ev != null) ev.Add(new Ev { t = "reveal", b = b }); }
        }

        public static List<int> InLot(Lot L, State s)
        {
            var a = new List<int>();
            for (int b = 0; b < L.boats.Count; b++) if (s.inl[b] != 0) a.Add(b);
            return a;
        }

        public static string Tap(Lot L, State s, int b, List<Ev> ev)
        {
            if (s.status != "play" || s.inl[b] == 0) return "no";
            if (Chained(L, s, b)) return "locked";
            if (WeedLeft(L, s, b) > 0) return "weed";
            var p = PathOf(L, s, b);
            if (!p.free) { if (ev != null) ev.Add(new Ev { t = "bump", b = b, n = p.n, hit = p.hit }); return "blocked"; }
            int sh = SharkHit(L, s, b);
            if (sh >= 0) { if (ev != null) ev.Add(new Ev { t = "bump", b = b, n = p.n, hit = -3, k = sh }); return "shark"; }
            if (s.berth.IndexOf(-1) < 0) { if (ev != null) ev.Add(new Ev { t = "full", b = b }); return "full"; }
            Lift(L, s, b, ev, "sail");
            return "ok";
        }

        // 吊船（道具）：不管有没有东西挡，直接吊到码头
        public static string Crane(Lot L, State s, int b, List<Ev> ev)
        {
            if (s.status != "play" || s.inl[b] == 0 || s.berth.IndexOf(-1) < 0) return "no";
            Lift(L, s, b, ev, "crane");
            return "ok";
        }

        // 停进空得最久的泊位（规则上哪个空泊位都一样；这样画面上刚开走的船不会和新来的船抢同一个位子）
        static void Lift(Lot L, State s, int b, List<Ev> ev, string how)
        {
            int k = -1;
            for (int j = 0; j < s.berth.Count; j++) if (s.berth[j] < 0 && (k < 0 || s.freed[j] < s.freed[k])) k = j;
            foreach (int j in L.boats[b].cells) s.occ[j] = -1;
            s.inl[b] = 0; s.berth[k] = b; s.arr[b] = ++s.clock; s.taps++; s.gone++;
            if (ev != null) ev.Add(new Ev { t = "go", b = b, k = k, how = how });
            if (L.boats[b].sp == 1 && GatherQ(L, s, b) > 0 && ev != null) ev.Add(new Ev { t = "gather", b = b, k = k });
            Spawn(L, s, ev); Reveal(L, s, ev); Board(L, s, ev);
        }

        // 上船：队头的人找同色、有空座的船（先到的先坐满）；坐满就开走
        public static void Board(Lot L, State s, List<Ev> ev)
        {
            while (s.qi < s.q.Count)
            {
                int c = s.q[s.qi], best = -1;
                for (int k = 0; k < s.berth.Count; k++)   // 码头上有彩虹船就先上彩虹船（它是送的额外座位，别让它干占着泊位）
                {
                    int bb = s.berth[k];
                    if (bb >= 0 && L.boats[bb].sp == 2 && s.load[bb] < L.boats[bb].cap && (best < 0 || s.arr[bb] < s.arr[s.berth[best]])) best = k;
                }
                if (best < 0)
                    for (int k = 0; k < s.berth.Count; k++)
                    {
                        int bb = s.berth[k];
                        if (bb >= 0 && L.boats[bb].c == c && L.boats[bb].sp != 2 && s.load[bb] < L.boats[bb].cap && (best < 0 || s.arr[bb] < s.arr[s.berth[best]])) best = k;
                    }
                if (best < 0) break;
                int b = s.berth[best];
                if (ev != null) ev.Add(new Ev { t = "board", b = b, k = best, seat = s.load[b], qi = s.qi });
                s.load[b]++; s.qi++;
                if (s.qx != null && s.qx[s.qi - 1] == POLE && s.load[b] < L.boats[b].cap)
                {
                    var idx = Vault(L, s, b, c);
                    if (idx.Count > 0 && ev != null) ev.Add(new Ev { t = "vault", b = b, k = best, h = c, n = idx.Count, list = idx.ToArray() });
                }
                if (s.load[b] >= L.boats[b].cap)
                {
                    s.berth[best] = -1; s.freed[best] = ++s.clock;
                    if (ev != null) ev.Add(new Ev { t = "leave", b = b, k = best });
                }
            }
            // 某个颜色的人救完了，码头上那色（还没坐满）的船直接开走；彩虹船等到所有人都救完
            if (s.qi < s.q.Count)
            {
                var left = new int[16]; for (int i = s.qi; i < s.q.Count; i++) left[s.q[i]]++;
                for (int k = 0; k < s.berth.Count; k++)
                {
                    int bb = s.berth[k];
                    if (bb < 0 || L.boats[bb].sp == 2 || left[L.boats[bb].c] > 0) continue;
                    s.berth[k] = -1; s.freed[k] = ++s.clock;
                    if (ev != null) ev.Add(new Ev { t = "leave", b = bb, k = k });
                }
            }
            if (s.qi >= s.q.Count) s.status = "win";
            else if (s.berth.IndexOf(-1) < 0) s.status = "lose";
        }

        // 彩虹船：在关卡最后加一艘额外的船（不在港湾里，cells 是空的），直接停进空得最久的泊位，然后上人。没空泊位返回 -1
        public static int AddRainbow(Lot L, State s, int len, List<Ev> ev)
        {
            if (s.status != "play") return -1;
            int k = -1;
            for (int j = 0; j < s.berth.Count; j++) if (s.berth[j] < 0 && (k < 0 || s.freed[j] < s.freed[k])) k = j;
            if (k < 0) return -1;
            L.boats.Add(new Boat { len = len, cap = Seats(len), d = 1, c = 0, sp = 2, cells = new int[0] });
            int b = L.boats.Count - 1, nb = L.boats.Count;
            Array.Resize(ref s.inl, nb); Array.Resize(ref s.load, nb); Array.Resize(ref s.arr, nb); Array.Resize(ref s.seen, nb);
            s.seen[b] = 1; s.berth[k] = b; s.arr[b] = ++s.clock;
            if (ev != null) ev.Add(new Ev { t = "go", b = b, k = k, how = "sea" });
            Board(L, s, ev);
            return b;
        }

        // 集合船：队伍里（还没上船的）同色的人插到最前面，最多插满这艘船的空座；其余人顺序不变。返回插了几个
        public static int GatherQ(Lot L, State s, int b) => Pull(s, L.boats[b].c, L.boats[b].cap - s.load[b]).Count;

        // 撑杆跳：同上，只是颜色按功能人自己的（上的要是彩虹船也照样补满）。返回被叫到的人原来排在队头后面第几个
        public const byte POLE = 1;
        public static List<int> Vault(Lot L, State s, int b, int c) => Pull(s, c, L.boats[b].cap - s.load[b]);

        // 队伍里颜色 c 的人（最多 need 个）插到最前面，其余人顺序不变；qx 跟着一起排
        static List<int> Pull(State s, int c, int need)
        {
            var a = new List<int>(); var z = new List<int>();
            for (int i = s.qi; i < s.q.Count; i++) { if (s.q[i] == c && a.Count < need) a.Add(i); else z.Add(i); }
            if (a.Count > 0) { var all = new List<int>(a); all.AddRange(z); Reorder(s, all); }
            for (int j = 0; j < a.Count; j++) a[j] -= s.qi;
            return a;
        }
        // 按 order（绝对下标，从 qi 起的新顺序）重排 q 和 qx
        static void Reorder(State s, List<int> order)
        {
            var q = new List<int>(); var x = s.qx == null ? null : new List<byte>();
            foreach (int i in order) { q.Add(s.q[i]); if (x != null) x.Add(s.qx[i]); }
            for (int j = 0; j < order.Count; j++) { s.q[s.qi + j] = q[j]; if (x != null) s.qx[s.qi + j] = x[j]; }
        }

        // 给关卡随机标功能人（10-28 用户：各种颜色都会随机出现，但概率别太高）：12 人以上的颜色才有资格，
        // 每种有资格的颜色 30% 机会出 1 个，一关最多 2 个、至少 1 个（一个没抽中就随便挑一种颜色给 1 个）；
        // 位置在那色人里 10%~75% 之间随机，后面总留着同色的人给他叫。
        // 工厂不调，关卡难度量的是没有功能人的版本，功能人只会让关变容易
        public const double SPECIAL_P = 0.3; public const int SPECIAL_MAX = 2;
        public static void MarkSpecial(Lot L, byte ab, Rng r)
        {
            int nc = 0; foreach (int c in L.queue) nc = Math.Max(nc, c + 1);
            var ok = new List<List<int>>();
            for (int c = 0; c < nc; c++)
            {
                var at = new List<int>(); for (int i = 0; i < L.queue.Count; i++) if (L.queue[i] == c) at.Add(i);
                if (at.Count >= 12) ok.Add(at);
            }
            if (ok.Count == 0) return;
            var pick = new List<List<int>>();
            foreach (var at in ok) if (pick.Count < SPECIAL_MAX && r.Next() < SPECIAL_P) pick.Add(at);
            if (pick.Count == 0) pick.Add(ok[r.Int(ok.Count)]);
            L.qx = new List<byte>(new byte[L.queue.Count]);
            foreach (var at in pick) L.qx[at[(int)(at.Count * (0.1 + 0.65 * r.Next()))]] = ab;
        }

        // ---- 涨潮分波（10-29 原型）----
        // 大关拆成几波：每波是一关完整的小关（网格一样大；礁石、船坞、鲨鱼每波各摆各的），排队 = 几波的队伍首尾接起来，所以总座位数和人数照样对得上。
        // 港湾里的船全开走了 → 涨潮，下一波的船一起冲进港湾。船一直按小关那么大画，手指好点（大关 100 条船也不用缩小）
        public static Lot Waves(List<Lot> w)
        {
            var L = w[0]; L.waves = new List<Lot>(); L.wave = 0;
            for (int i = 1; i < w.Count; i++) { L.waves.Add(w[i]); L.queue.AddRange(w[i].queue); }
            L.total = L.queue.Count;
            return L;
        }
        public static bool TideDue(Lot L, State s)
        {
            if (L.waves == null || L.wave >= L.waves.Count || s.status != "play") return false;
            for (int b = 0; b < L.boats.Count; b++) if (s.inl[b] != 0) return false;
            for (int h = 0; h < L.houses.Count; h++) if (s.hq[h] < L.houses[h].list.Count) return false;
            return true;
        }
        // 下一波的船接在 L.boats 后面（下标整体往后挪），一起放进港湾；礁石（ins）、船坞、鲨鱼换成这一波的
        public static void Tide(Lot L, State s, List<Ev> ev)
        {
            var W = L.waves[L.wave++]; int off = L.boats.Count;
            foreach (var B in W.boats) { if (B.lockBy >= 0) B.lockBy += off; if (B.weed > 0) B.weed += s.gone; L.boats.Add(B); }
            foreach (var H in W.houses) for (int i = 0; i < H.list.Count; i++) H.list[i] += off;
            L.ins = W.ins; L.houses = W.houses; L.sharks = W.sharks;
            s.hq = new List<int>(); for (int h = 0; h < L.houses.Count; h++) s.hq.Add(0);
            int nb = L.boats.Count;
            Array.Resize(ref s.inl, nb); Array.Resize(ref s.load, nb); Array.Resize(ref s.arr, nb); Array.Resize(ref s.seen, nb);
            for (int b = off; b < nb; b++) if (L.boats[b].h < 0) Put(L, s, b);
            Spawn(L, s, null); Reveal(L, s, null);
            if (ev != null) ev.Add(new Ev { t = "tide", b = off, n = nb - off, k = L.wave });
        }

        public static void AddBerth(Lot L, State s) { s.berth.Add(-1); s.freed.Add(0); if (s.status == "lose") s.status = "play"; }

        // 理队（道具）：接下来 W 个人里，能马上上码头那几艘船的先站到前面，其余人顺序不变
        public static int SortQ(Lot L, State s, int W)
        {
            var rem = new int[16];
            foreach (int b in s.berth) if (b >= 0) rem[L.boats[b].c] += L.boats[b].cap - s.load[b];
            int end = Math.Min(s.q.Count, s.qi + W);
            var a = new List<int>(); var z = new List<int>();
            for (int i = s.qi; i < end; i++) { int c = s.q[i]; if (rem[c] > 0) { rem[c]--; a.Add(i); } else z.Add(i); }
            if (a.Count == 0) return 0;
            int n = a.Count; a.AddRange(z); Reorder(s, a);
            return n;
        }

        // ---- 认真玩家（关卡工厂量难度用；游戏里的提示和演示也用它） ----
        public struct Demand { public int[] first, cnt, run; }
        public static Demand GetDemand(Lot L, State s, int W)
        {
            var rem = new int[16]; var first = new int[16]; var cnt = new int[16]; var run = new int[16];
            for (int i = 0; i < 16; i++) first[i] = 999;
            foreach (int b in s.berth) if (b >= 0) rem[L.boats[b].c] += L.boats[b].cap - s.load[b];
            int r = 0, prev = -1;
            for (int i = s.qi; i < s.q.Count && r < W; i++)
            {
                int c = s.q[i];
                if (rem[c] > 0) { rem[c]--; continue; }
                if (first[c] == 999) first[c] = r;
                cnt[c]++;
                if (first[c] == r || (prev == c && run[c] == r - first[c])) run[c]++;
                prev = c; r++;
            }
            return new Demand { first = first, cnt = cnt, run = run };
        }

        public static List<int[]> ScoreMoves(Lot L, State s)
        {
            var all = InLot(L, s);
            var hit = new int[L.boats.Count];
            for (int i = 0; i < hit.Length; i++) hit[i] = -1;
            var free = new List<int>();
            foreach (int b in all) { var p = PathOf(L, s, b); if (p.free && !Stuck(L, s, b)) free.Add(b); else if (!p.free) hit[b] = p.hit; }
            var nd = GetDemand(L, s, 40);
            int fb = 0; foreach (int x in s.berth) if (x < 0) fb++;
            var outl = new List<int[]>();
            foreach (int b in free)
            {
                var B = L.boats[b]; int f = nd.first[B.c];
                int sc = f < 999 ? 100 - f * 8 + Math.Min(nd.cnt[B.c], B.cap) * 2 : -60;
                if (f < 999)
                {
                    int rn = nd.run[B.c];
                    sc += B.cap == rn ? 25 : B.cap < rn ? 8 : -(B.cap - rn) * 6;
                    if (B.cap > nd.cnt[B.c]) sc -= (B.cap - nd.cnt[B.c]) * 12;
                }
                foreach (int x in all) if (hit[x] == b) { int fx = nd.first[L.boats[x].c]; sc += fx == 0 ? 60 : fx < 999 ? Math.Max(4, 28 - fx * 2) : 2; }
                // 钥匙船：开走就解开锁着的船，那艘船队伍里越急着要，越值得先开
                foreach (int x in all) if (L.boats[x].lockBy == b) { int fx = nd.first[L.boats[x].c]; sc += fx == 0 ? 50 : fx < 999 ? Math.Max(6, 30 - fx * 2) : 6; }
                if (f != 0) { if (fb <= 1) sc -= 300; else if (fb <= 2) sc -= 120; else if (f > 6) sc -= 40; }
                outl.Add(new[] { b, sc });
            }
            return outl;
        }

        public static int BestMove(Lot L, State s, Rng r, double noise)
        {
            var m = ScoreMoves(L, s);
            int bi = -1; double bv = -1e9;
            foreach (var mv in m)
            {
                double x = mv[1] + (noise != 0 ? noise * r.Next() : 0);
                if (x > bv) { bv = x; bi = mv[0]; }
            }
            return bi;
        }

        public static bool Playout(Lot L, Rng r, double noise)
        {
            var s = NewState(L); s.t = -1;    // 不计时：鲨鱼不算
            for (int k = 0; k < 3000 && s.status == "play"; k++)
            {
                int b = BestMove(L, s, r, noise);
                if (b < 0) break;
                Tap(L, s, b, null);
            }
            return s.status == "win";
        }

        // 往前看一步的认真玩家：分数最高的几步各试一下，看走完之后局面好不好（下一步还有没有好棋、空泊位多不多）。
        // 只看眼前一步的 BestMove 比真人弱，拿它量出来的"难"关，人玩起来很简单
        public static int BestMoveLook(Lot L, State s, Rng r, double noise)
        {
            var m = ScoreMoves(L, s);
            if (m.Count == 0) return -1;
            m.Sort((a, b) => b[1].CompareTo(a[1]));
            int K = Math.Min(4, m.Count), bi = -1; double bv = -1e18;
            for (int i = 0; i < K; i++)
            {
                var s2 = s.Clone(); s2.t = -1; Tap(L, s2, m[i][0], null);   // 往前看时不管鲨鱼（等一下就过去了）
                double v;
                if (s2.status == "win") v = 1e6;
                else if (s2.status == "lose") v = -1e6;
                else
                {
                    double b2 = -400; foreach (var x in ScoreMoves(L, s2)) b2 = Math.Max(b2, x[1]);
                    int fb = 0; foreach (int x in s2.berth) if (x < 0) fb++;
                    v = m[i][1] * 0.6 + b2 + fb * 18;
                }
                if (noise != 0) v += noise * r.Next();
                if (v > bv) { bv = v; bi = m[i][0]; }
            }
            return bi;
        }

        // 带风暴计时的试玩（用 BestMoveLook）：人每点一下花 pace×(0.6~1.4) 秒；开走一艘满船加 bonus 秒（最多回到 storm+10）。
        // storm = 0 表示不计时。taps = 这局点了几下
        public static bool Playout(Lot L, Rng r, double noise, double storm, double bonus, double pace, out int taps)
        {
            var s = NewState(L);
            double left = storm, cap = storm; int dep = 0;   // 上限 = 开局秒数（和 Storm.cs 一致）
            for (int k = 0; k < 3000 && s.status == "play"; k++)
            {
                int b = BestMoveLook(L, s, r, noise);
                if (b < 0) break;
                // 鲨鱼正好在出口：人会等它游过去再点（等的时间算进风暴）
                if (SharkHit(L, s, b) >= 0)
                {
                    double t0 = s.t;
                    while (SharkHit(L, s, b) >= 0 && s.t - t0 < 15) s.t += 0.1;
                    s.t += 0.25; if (storm > 0) left -= s.t - t0;
                }
                Tap(L, s, b, null);
                if (storm <= 0) s.t += pace;
                else
                {
                    double dt = pace * (0.6 + 0.8 * r.Next()); s.t += dt;
                    left -= dt;
                    int d = Departed(L, s); left = Math.Min(cap, left + (d - dep) * bonus); dep = d;
                    if (left < 0 && s.status == "play") { taps = s.taps; return false; }
                }
            }
            taps = s.taps;
            return s.status == "win";
        }

        // 已经坐满开走的船有几艘
        public static int Departed(Lot L, State s)
        {
            int n = 0;
            for (int b = 0; b < L.boats.Count; b++) if (s.inl[b] == 0 && s.load[b] >= L.boats[b].cap) n++;
            return n;
        }

        static List<int> Line(Lot L, int i, int d)
        {
            var a = new List<int>();
            for (int j = StepC(L, i, d); j >= 0; j = StepC(L, j, d)) a.Add(j);
            return a;
        }

        class Tile { public List<int> cells; public int ax; }

        // ---- 出关卡（见 boat.html genLevel 的注释）----
        public static Lot GenLevel(Cfg cfg, long seed)
        {
            var r = new Rng(seed * 7919 + (long)cfg.nb * 131 + 17);
            var L = MakeLot(cfg);
            L.cfg = cfg; L.berths = Or(cfg.berths, 4);
            int NC = L.NU * L.NV;
            var occ = new int[NC]; var res = new byte[NC];
            for (int i = 0; i < NC; i++) occ[i] = -1;
            var inside = new List<int>();
            for (int i = 0; i < NC; i++) if (L.ins[i] == 1) inside.Add(i);

            // 船坞：一格小屋，门口 3 格留给它的船
            for (int h = 0; h < cfg.houses; h++)
            {
                for (int t = 0; t < 300; t++)
                {
                    int g = inside[r.Int(inside.Count)], d = r.Int(4);
                    if (L.ins[g] != 1 || res[g] != 0) continue;
                    var ln = Line(L, g, d);
                    if (ln.Count < 5) continue;
                    bool bad = false;
                    foreach (int j in ln) if (L.ins[j] != 1 || res[j] == 1) { bad = true; break; }
                    if (bad) continue;
                    L.ins[g] = 3;
                    for (int k = 0; k < ln.Count; k++) res[ln[k]] = (byte)(k < 3 ? 1 : Math.Max(res[ln[k]], (byte)2));
                    L.houses.Add(new House { cell = g, d = d });
                    break;
                }
            }
            for (int k = 0; k < cfg.rocks; k++)
                for (int t = 0; t < 60; t++)
                {
                    int i = inside[r.Int(inside.Count)];
                    if (L.ins[i] == 1 && res[i] == 0) { L.ins[i] = 2; break; }
                }
            int[] sizes = cfg.sizes != null && cfg.sizes.Length > 0 ? cfg.sizes : new[] { 2 };
            int minSize = int.MaxValue; foreach (int x in sizes) minSize = Math.Min(minSize, x);
            Func<int, bool> free = j => L.ins[j] == 1 && occ[j] < 0 && res[j] != 1;

            // ① 先把港湾铺满
            var tiles = new List<Tile>();
            foreach (int i in Shuffle(new List<int>(inside), r))
            {
                if (!free(i)) continue;
                var lens = Shuffle(new List<int>(sizes), r); lens.Add(minSize);
                Tile got = null;
                foreach (int len in lens)
                {
                    int[] axes = r.Next() < 0.5 ? new[] { 0, 1 } : new[] { 1, 0 };
                    foreach (int ax in axes)
                    {
                        for (int off = 0; off < len && got == null; off++)
                        {
                            int st = i; bool ok = true;
                            for (int k = 0; k < off && ok; k++) { st = StepC(L, st, ax + 2); if (st < 0) ok = false; }
                            if (!ok) continue;
                            var cells = new List<int> { st };
                            if (!free(st)) continue;
                            for (int k = 1; k < len && ok; k++) { int j = StepC(L, cells[k - 1], ax); if (j < 0 || !free(j)) ok = false; else cells.Add(j); }
                            if (ok && cells.IndexOf(i) >= 0) got = new Tile { cells = cells, ax = ax };
                        }
                        if (got != null) break;
                    }
                    if (got != null) break;
                }
                if (got != null) { foreach (int j in got.cells) occ[j] = tiles.Count; tiles.Add(got); }
            }

            // ② 倒推出路
            Func<int, int, int> exitLen = (t, d) =>
            {
                var T = tiles[t]; int end = d < 2 ? T.cells[T.cells.Count - 1] : T.cells[0]; int n = 0;
                for (int j = StepC(L, end, d); j >= 0; j = StepC(L, j, d)) { if (L.ins[j] >= 2 || res[j] == 1 || occ[j] >= 0) return -1; n++; }
                return n;
            };
            var alive = new List<int>(); for (int t = 0; t < tiles.Count; t++) alive.Add(t);
            var dirOf = new int[tiles.Count]; for (int t = 0; t < dirOf.Length; t++) dirOf[t] = -1;
            while (alive.Count > 0)
            {
                var can = new List<int[]>();
                foreach (int t in alive)
                {
                    int[] ds = tiles[t].cells.Count == 1 ? new[] { 0, 1, 2, 3 } : new[] { tiles[t].ax, tiles[t].ax + 2 };
                    foreach (int d in ds) { int n = exitLen(t, d); if (n >= 0) can.Add(new[] { t, d, n }); }
                }
                if (can.Count == 0)
                {
                    int t = alive[r.Int(alive.Count)];
                    foreach (int j in tiles[t].cells) occ[j] = -1;
                    alive.Remove(t);
                    continue;
                }
                var c = can[r.Int(can.Count)];
                if (r.Next() < cfg.deep)
                {
                    int[] alt = null;
                    foreach (var x in can) if (x[0] == c[0] && x[1] != c[1]) { alt = x; break; }
                    if (alt != null && alt[2] > c[2]) c = alt;
                }
                dirOf[c[0]] = c[1];
                foreach (int j in tiles[c[0]].cells) occ[j] = -1;
                alive.Remove(c[0]);
            }
            var boats = L.boats;
            for (int t = 0; t < tiles.Count; t++)
            {
                if (dirOf[t] < 0) continue;
                var T = tiles[t]; int d = dirOf[t];
                var cells = new List<int>(T.cells); if (d >= 2) cells.Reverse();
                boats.Add(new Boat { len = cells.Count, cap = Seats(cells.Count), d = d, cells = cells.ToArray(), h = -1 });
            }
            if (boats.Count < 4) return null;
            var hs = new List<int>(); foreach (int l in sizes) if (l <= 3) hs.Add(l);
            for (int h = 0; h < L.houses.Count; h++)
            {
                var H = L.houses[h]; var ln = Line(L, H.cell, H.d);
                for (int k = 0; k < Or(cfg.houseN, 4); k++)
                {
                    int len = hs[r.Int(hs.Count)];   // 航母塞不进船坞
                    boats.Add(new Boat { len = len, cap = Seats(len), d = H.d, cells = ln.GetRange(0, len).ToArray(), h = h });
                    H.list.Add(boats.Count - 1);
                }
            }
            // 颜色：按座位数尽量均匀
            int nc = cfg.colors; var seat = new int[nc];
            var ord = new List<int>(); for (int b = 0; b < boats.Count; b++) ord.Add(b);
            Shuffle(ord, r);
            foreach (int b in ord)
            {
                int c = 0;
                for (int k = 1; k < nc; k++) if (seat[k] < seat[c] || (seat[k] == seat[c] && r.Next() < 0.5)) c = k;
                boats[b].c = c; seat[c] += boats[b].cap;
            }
            L.queue = new List<int>();
            if (cfg.mys != 0)
            {
                var s0 = NewState(L);
                for (int b = 0; b < boats.Count; b++) if (s0.inl[b] != 0 && !PathOf(L, s0, b).free && r.Next() < cfg.mys) boats[b].mys = 1;
            }
            // 海藻：挑开局就开得出去的船缠上（一眼看着能走、偏偏走不了），要先开走 2~4 艘别的船
            if (cfg.weed > 0)
            {
                var s0 = NewState(L);
                var cand = new List<int>(); for (int b = 0; b < boats.Count; b++) if (boats[b].h < 0) cand.Add(b);
                Shuffle(cand, r);
                // 能走的排前面（手写稳定分组：List.Sort 不稳定，游戏和工厂两个运行时排出来可能不一样）
                var fst = cand.FindAll(x => PathOf(L, s0, x).free); fst.AddRange(cand.FindAll(x => !PathOf(L, s0, x).free)); cand = fst;
                for (int k = 0; k < Math.Min(cfg.weed, cand.Count / 4); k++) boats[cand[k]].weed = Math.Min(boats.Count / 3, 2 + r.Int(3));
            }
            // 钥匙锁链：锁住一艘开局就能走的船，钥匙挂在一艘被挡住的船上（要先把钥匙船挖出来）
            for (int k = 0; k < cfg.locks; k++)
            {
                var s0 = NewState(L);
                var lk = new List<int>(); var ky = new List<int>();
                for (int b = 0; b < boats.Count; b++)
                {
                    var B = boats[b]; if (B.h >= 0 || B.weed > 0 || B.lockBy >= 0 || B.key >= 0) continue;
                    if (PathOf(L, s0, b).free) lk.Add(b); else if (B.mys == 0) ky.Add(b);
                }
                if (lk.Count == 0 || ky.Count == 0) break;
                int lb = lk[r.Int(lk.Count)], kb = ky[r.Int(ky.Count)];
                boats[lb].lockBy = kb; boats[kb].key = k;
            }
            // 鲨鱼：巡逻出船最多的那一边。不放最下面那条边（d=1，朝屏幕下方）：那儿被底部的道具栏挡着，看不见鲨鱼
            for (int k = 0; k < cfg.shark; k++)
            {
                var cnt = new int[4]; foreach (var B in boats) cnt[B.d]++;
                cnt[1] = -1;
                foreach (var S in L.sharks) cnt[S.d] = -1;
                int d = 0; for (int j = 1; j < 4; j++) if (cnt[j] > cnt[d] || (cnt[j] == cnt[d] && r.Next() < 0.5)) d = j;
                int mn = int.MaxValue, mx = int.MinValue;
                for (int i = 0; i < L.ins.Length; i++) if (L.ins[i] != 0) { int p = Perp(L, i, d); mn = Math.Min(mn, p); mx = Math.Max(mx, p); }
                L.sharks.Add(new Shark { d = d, lo = mn, hi = mx, v = Or(cfg.sharkV, 1.4), ph = r.Next() * 2 });
            }
            // 规划排队
            var s = NewState(L); int Bp = Or(cfg.plan, 2);
            var berth = new List<int[]>();   // [b, left]
            int lastC = -1;                  // 上一串人的颜色（chain 用）
            for (int guard = 0; guard < 100000; guard++)
            {
                var lot = InLot(L, s);
                int pend = 0; for (int h = 0; h < L.houses.Count; h++) pend += L.houses[h].list.Count - s.hq[h];
                if (lot.Count == 0 && pend == 0 && berth.Count == 0) break;
                var fr = new List<int>(); foreach (int b in lot) if (CanGo(L, s, b)) fr.Add(b);
                if (berth.Count < Bp && fr.Count > 0 && (berth.Count == 0 || r.Next() < Or(cfg.bring, 0.5)))
                {
                    int b = fr[r.Int(fr.Count)];
                    if (cfg.chain > 0 && lastC >= 0 && r.Next() < cfg.chain)
                    {
                        var same = fr.FindAll(x => boats[x].c == lastC);
                        if (same.Count > 0) b = same[r.Int(same.Count)];
                    }
                    else if (r.Next() < cfg.dig)
                    {
                        int bv = -1;
                        foreach (int x in fr)
                        {
                            int n = 0;
                            foreach (int y in lot) if (y != x && PathOf(L, s, y).hit == x) n++;
                            if (n > bv) { bv = n; b = x; }
                        }
                    }
                    foreach (int j in boats[b].cells) s.occ[j] = -1;
                    s.inl[b] = 0; s.arr[b] = ++s.clock; s.gone++; Spawn(L, s, null);   // arr/gone：海藻、锁链按这个算
                    berth.Add(new[] { b, boats[b].cap });
                }
                else if (berth.Count > 0)
                {
                    var e = r.Next() < cfg.lifo ? berth[berth.Count - 1] : berth[r.Int(berth.Count)];
                    if (cfg.chain > 0 && lastC >= 0 && r.Next() < cfg.chain)
                    {
                        var same = berth.Find(x => boats[x[0]].c == lastC);
                        if (same != null) e = same;
                    }
                    int run = cfg.chunk > 0 && r.Next() < cfg.chunk ? e[1] : 1 + r.Int(Math.Min(e[1], Or(cfg.run, 4)));
                    lastC = boats[e[0]].c;
                    for (int k = 0; k < run; k++) L.queue.Add(boats[e[0]].c);
                    e[1] -= run;
                    if (e[1] <= 0) berth.Remove(e);
                }
                else return null;
            }
            L.total = L.queue.Count;
            return L;
        }

        // 游戏里读关：seed 不行就 +1 往后试（和网页版 loadLevel 一样）
        // 换颜色：map[旧颜色] = 新颜色。船和排队的人一起换，规则、难度完全不变（重来时看起来像新的一关）
        public static void Recolor(Lot L, int[] map)
        {
            foreach (var B in L.boats) B.c = map[B.c];
            if (L.waves != null) foreach (var W in L.waves) foreach (var B in W.boats) B.c = map[B.c];
            for (int i = 0; i < L.queue.Count; i++) L.queue[i] = map[L.queue[i]];
        }

        public static Lot LoadLevel(LevelEntry lv)
        {
            Lot L = null;
            for (int k = 0; k < 40 && L == null; k++) L = GenLevel(lv.cfg, lv.seed + k);
            return L;
        }
    }
}
