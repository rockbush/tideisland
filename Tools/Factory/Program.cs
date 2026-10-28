// 点船版关卡工厂（C# 版，从 潮汐岛Demo/boat_factory.js 搬过来，规则直接用游戏的 Rules.cs）。
//   cd Tools/Factory; dotnet run -c Release            出 100 关，写 Assets/Tide/Resources/levels.json
//   dotnet run -c Release -- from 51                   10-26 加到 100 关时用的：前 50 关照抄，只出第 51~100 关
//   dotnet run -c Release -- probe                     只看旋钮范围，不写文件
//   dotnet run -c Release -- from 15                   第 1~14 关原样保留（照抄现有 levels.json），只重出第 15 关起
//   dotnet run -c Release -- from 1 to 10              只重出第 1~10 关，其余照抄
//
// 每关的做法：按"订单"（船数、船型、颜色数、旋钮）出一批候选关卡 → 用"往前看一步的认真玩家"（BestMoveLook）
// 带风暴计时模拟 N 局量通关率 → 挑最接近目标的那关。
// 关卡一定有解：船是倒着摆的（一定能全部开走），队伍是照着"只用 plan 个泊位"的走法排的（泊位 ≥ plan 就一定救得完）。
//
// 难度曲线（tag：0 普通 / 1 困难 / 2 超难）：
//   1~3 关教学（几乎必过）→ 4~10 关普通从 0.80 降到 0.50 → 之后普通 0.50 → 0.38；
//   第 6、16、26… 关困难（0.27 → 0.22），第 10、20、30… 关超难（0.17 → 0.12）。困难/超难过后下一关回到普通，喘口气。
//   新东西都在奇数关登场（3、5、7…），和困难/超难关错开：登场关要让人好好认识新东西，不能同时是难关。
// 第 51~100 关（10-26）：不再一路变难，每 10 关一章"锯齿"——章头 2 关松一点（刚打完超难喘口气），往后慢慢紧，第 6 关困难、第 10 关超难；
//   船数在 40~67 之间随章内位置起伏；每章一个主题（THEME），某个现有机制特别多，让每章手感不一样。
// 风暴时间：先不计时模拟，取赢的局点了几下（中位数）× PACE 秒 = 人需要的时间，再乘松紧系数（普通 1.4 / 困难 1.3 / 超难 1.2，
//   第一次出风暴的第 4 关 2.3），扣掉"每开走一艘满船加 bonus 秒"的部分，剩下的就是开局的风暴秒数。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Tide;

static class Factory
{
    const int TOTAL = 100;
    const int OLD = 50;             // 原来的 50 关：曲线、船数都按它定，第 51 关起走 Late* 那套
    const double NOISE = 14;        // 认真玩家的手误程度
    const double DENS = 0.95;       // 港湾里船占格子的比例
    const double PACE = 1.2;        // 人平均点一下花几秒（模拟时 ×0.6~1.4 抖动）。10-21 从 2.0 改成真人手速：原来按 2 秒算，真人玩风暴最紧时还剩 90% 以上
    const double BONUS = 7.0;       // 开走一艘满船加几秒（10-21 从 1.5 降到 1；10-26 用户先要 2 秒，又改成 7 秒）
    const double STORM_FIXED = 120; // 10-26 用户：风暴一律 2 分钟（>0 就不再按难度算开局秒数）
    const int STORM_FROM = 3;       // 第 4 关起有风暴（0 起算）

    // 新东西第一次出现的关（0 起算）——现有的机制；新机制以后往这里加
    static readonly Dictionary<int, string> INTRO = new Dictionary<int, string> {
        { 2, "yacht" }, { 4, "raft" }, { 6, "rock" }, { 8, "house" }, { 10, "mys" }, { 12, "carrier" },
        { 14, "weed" }, { 16, "lock" }, { 18, "shark" } };

    static int Tag(int i) { int n = i + 1; return n % 10 == 0 ? 2 : n % 10 == 6 ? 1 : 0; }
    static double Target(int i)
    {
        int n = i + 1, tag = Tag(i);
        if (n >= 4 && n <= OPEN3) return T3 * Target0(i);   // 10-23：只开 3 个泊位的关，乱点的模拟玩家本来就更容易输，不压低目标的话工厂会挑更松的关把难度补回去
        return Target0(i);
    }
    static readonly double T3 = double.TryParse(Environment.GetEnvironmentVariable("TIDE_T3"), out var v) ? v : 0.6;
    static double Target0(int i)
    {
        int n = i + 1, tag = Tag(i);
        if (n > OLD) return LateTarget(n, tag);
        double late = Math.Clamp((n - 10) / 40.0, 0, 1);
        if (tag == 2) return 0.17 - 0.05 * late;
        if (tag == 1) return 0.27 - 0.05 * late;
        if (n <= 3) return new[] { 1.0, 0.95, 0.9 }[n - 1];
        if (n <= 10) return 0.8 - 0.3 * (n - 4) / 6.0;
        return 0.5 - 0.12 * late;
    }
    static double Slack(int i) => i == STORM_FROM ? 1.8 : Tag(i) == 2 ? 1.2 : Tag(i) == 1 ? 1.3 : 1.4;
    static int NB(int i) => i >= OLD ? LateNB(i + 1) : (int)Math.Round(8 + 50 * Math.Pow(i / (double)(OLD - 1), 0.85));
    // 第 51 关起：p = 章内第几关（0~9），ch = 第几章（5~9）
    static double LateTarget(int n, int tag)
    {
        int p = (n - 1) % 10;
        if (tag == 2) return 0.13;
        if (tag == 1) return 0.22;
        if (p == 0) return 0.55;
        if (p == 1) return 0.47;
        return 0.44 - 0.01 * p;
    }
    static int LateNB(int n)
    {
        int p = (n - 1) % 10, ch = (n - 1) / 10, tag = Tag(n - 1);
        return (int)Math.Round(40 + 1.5 * (ch - 5) + 2 * p + (tag == 2 ? 3 : tag == 1 ? 2 : 0));
    }
    // 每章的主题：这章里哪个机制特别多（第 1~50 关没有主题）
    static string Theme(int idx) => idx < OLD ? null : new[] { "shark", "mys", "rock", "lock", "mix" }[Math.Min(4, (idx - OLD) / 10)];
    static int[] Sizes(int i) => i < 2 ? new[] { 2 } : i < 4 ? new[] { 2, 2, 3 } : i < 12 ? new[] { 1, 2, 2, 3 } : i < 29 ? new[] { 1, 2, 2, 3, 4 } : new[] { 1, 2, 2, 3, 3, 4 };
    static int Colors(int i) => i < 2 ? 3 : i < 6 ? 4 : i < 20 ? 5 : 6;
    static double Clamp(double v, double a, double b) => Math.Max(a, Math.Min(b, v));

    static (int w, int h) LotSize(int nb, int[] sizes, int rot)
    {
        double avg = sizes.Average(), need = nb * avg / DENS;
        for (int tries = 0; tries < 60; tries++)
        {
            int w = (int)Clamp(Math.Round(Math.Sqrt(need * 0.72) + tries * 0.25), rot != 0 ? 5 : 4, 12);
            int h = (int)Clamp(Math.Ceiling(need / w * (rot != 0 ? 1.28 : 1) + tries * 0.4), 5, 18);
            var L = Rules.MakeLot(new Cfg { w = w, h = h, rot = rot });
            if (L.ins.Count(x => x != 0) * DENS >= nb * avg) return (w, h);
        }
        return (12, 18);
    }

    // 10-23 用户：前几关开局最多 3 个泊位（4 个太简单：会想的玩家前 12 关从没停满过 4 个）
    const int OPEN3 = 0;     // 10-23 试玩后用户：3 个不够用，改回全部 4 个（原来是 10；想再试就改回 10，第 4~OPEN3 关的目标会 ×T3）

    // 目标通关率 → 旋钮。t=0 最松，t=1 最紧
    static Cfg Order(int idx, double target, Rng r)
    {
        double t = Clamp((1 - target) / 0.9 + (r.Next() - 0.5) * 0.35, 0, 1.15);
        var sizes = Sizes(idx);
        int rot = idx < 2 ? 0 : r.Next() < 0.6 ? 1 : 0;
        var c = new Cfg
        {
            rot = rot, nb = NB(idx), colors = Colors(idx), sizes = sizes, berths = idx < OPEN3 ? 3 : 4,
            plan = (int)Clamp(Math.Round(1.3 + 3 * t + (r.Next() - 0.5)), 1, 4),
            bring = Math.Round(Clamp(0.3 + 0.55 * t, 0.2, 0.95), 2), lifo = Math.Round(Clamp(0.15 + 0.7 * t, 0, 0.95), 2),
            run = (int)Clamp(Math.Round(5 - 2.5 * t), 2, 5), deep = Math.Round(Clamp(0.15 + 0.8 * t, 0, 1), 2), dig = Math.Round(Clamp(0.6 * t, 0, 0.8), 2),
            tag = Tag(idx)
        };
        INTRO.TryGetValue(idx, out var intro);
        string th = Theme(idx); bool mix = th == "mix";
        if (idx >= 6 && (intro == "rock" || r.Next() < (th == "rock" ? 0.95 : mix ? 0.5 : 0.35))) c.rocks = th == "rock" ? 3 + (int)(r.Next() * 3) : 1 + (int)(r.Next() * 3);
        if (idx >= 8 && (intro == "house" || r.Next() < 0.3)) { c.houses = idx >= 30 && r.Next() < 0.4 ? 2 : 1; c.houseN = 3 + (int)(r.Next() * 3); c.nb -= c.houses * c.houseN; }
        if (idx >= 10 && (intro == "mys" || r.Next() < (th == "mys" ? 0.95 : mix ? 0.5 : 0.35))) c.mys = Math.Round(th == "mys" ? 0.45 + r.Next() * 0.2 : 0.25 + r.Next() * 0.2, 2);
        if (idx >= 14 && (intro == "weed" || r.Next() < (th == "rock" || mix ? 0.5 : 0.3))) c.weed = 1 + (int)(r.Next() * (idx >= 30 ? 3 : 2));   // 礁石章顺带多一点海藻
        if (idx >= 16 && (intro == "lock" || r.Next() < (th == "lock" ? 0.95 : mix ? 0.5 : 0.3))) c.locks = th == "lock" ? 2 + (int)(r.Next() * 2) : idx >= 30 && r.Next() < 0.3 ? 2 : 1;
        if (th == "lock" && c.houses == 0 && r.Next() < 0.5) { c.houses = 1; c.houseN = 3 + (int)(r.Next() * 3); c.nb -= c.houseN; }   // 锁链章：港口味道，多一点船坞
        if (idx >= 18 && (intro == "shark" || r.Next() < (th == "shark" ? 0.9 : mix ? 0.45 : 0.3))) { c.shark = 1; c.sharkV = Math.Round(1.2 + 0.4 * r.Next(), 2); c.rot = rot = 0; }   // 鲨鱼只在正方向的港湾：沿着直边游最清楚
        // 越简单的关，同色的人越连成长串（先让玩家爽一爽）：目标通关率 0.7 以上全开，0.35 以下不用
        c.chunk = Math.Round(Clamp((target - 0.35) / 0.35, 0, 1), 2);
        c.chain = Math.Round(Clamp((target - 0.4) / 0.4, 0, 0.85), 2);
        // 10-21：泊位余量收紧——教学关以后，正确走法至少要用到 3 个泊位，困难/超难要用满 4 个（原来普通关常常只要 1~2 个，玩家从来停不满）
        if (idx >= 3) c.plan = Math.Max(c.plan, c.tag > 0 ? 4 : 3);
        // 10-21：同色长串只留给前 5 关；第 6~10 关逐步收掉，第 11 关起不用。前期也多一点"要先挪开别的船"的摆法
        double fade = Clamp((9 - idx) / 4.0, 0, 1);
        if (idx >= 5) { c.chunk = Math.Round(c.chunk * fade, 2); c.chain = Math.Round(c.chain * fade, 2); }
        if (idx >= 3) { c.deep = Math.Max(c.deep, 0.45); c.dig = Math.Max(c.dig, 0.25); }
        c.plan = Math.Min(c.plan, c.berths);   // 10-23：前 OPEN3 关开局只有 3 个泊位，解法也只能用 3 个
        if (idx < 2) { c.plan = 1; c.lifo = 0; c.bring = 0.2; c.run = 6; c.deep = 0.1; c.chunk = 1; c.chain = 0.85; }   // 教学关：照着队伍点就行
        var sz = LotSize(c.nb, sizes, rot); c.w = sz.w; c.h = sz.h;
        return c;
    }

    // 队伍里同色连着的平均长度 / 最长一串
    static double AvgRun(List<int> q) { int n = 0; for (int i = 0; i < q.Count; i++) if (i == 0 || q[i] != q[i - 1]) n++; return q.Count / (double)Math.Max(1, n); }
    static int MaxRun(List<int> q) { int m = 0, c = 0; for (int i = 0; i < q.Count; i++) { c = i > 0 && q[i] == q[i - 1] ? c + 1 : 1; m = Math.Max(m, c); } return m; }

    static bool HasIntro(Lot L, string intro)
    {
        switch (intro)
        {
            case "yacht": return L.boats.Any(B => B.len == 3);
            case "raft": return L.boats.Any(B => B.len == 1);
            case "carrier": return L.boats.Any(B => B.len == 4);
            case "house": return L.houses.Count > 0;
            case "mys": return L.boats.Any(B => B.mys != 0);
            case "rock": return L.ins.Any(x => x == 2);
            case "weed": return L.boats.Any(B => B.weed > 0);
            case "lock": return L.boats.Any(B => B.lockBy >= 0);
            case "shark": return L.sharks.Count > 0;
            default: return true;
        }
    }

    // 不计时玩 n 局：赢的局里点了几下（中位数），用来定风暴时间
    static int MedianTaps(Lot L, int n, long seed)
    {
        var r = new Rng(seed); var t = new List<int>();
        for (int k = 0; k < n; k++) if (Rules.Playout(L, r, NOISE, 0, 0, PACE, out int taps)) t.Add(taps);
        if (t.Count == 0) return L.boats.Count;
        t.Sort(); return t[t.Count / 2];
    }
    static bool PlayoutFrom(Lot L, State s, Rng r, double noise)
    {
        s.t = -1;
        for (int k = 0; k < 3000 && s.status == "play"; k++) { int b = Rules.BestMoveLook(L, s, r, noise); if (b < 0) break; Rules.Tap(L, s, b, null); }
        return s.status == "win";
    }
    static double Measure(Lot L, Cfg c, int n, long seed)
    {
        var r = new Rng(seed); int w = 0;
        for (int k = 0; k < n; k++) if (Rules.Playout(L, r, NOISE, c.storm, c.bonus, PACE, out _)) w++;
        return w / (double)n;
    }
    static void SetStorm(int idx, Lot L, Cfg c, long seed)
    {
        if (idx < STORM_FROM) { c.storm = 0; c.bonus = 0; return; }
        double need = MedianTaps(L, 16, seed + 3) * PACE + 3;
        c.bonus = BONUS;
        c.storm = STORM_FIXED > 0 ? STORM_FIXED : Math.Round(Math.Max(15, need * Slack(idx) - BONUS * L.boats.Count));
    }

    static int Main(string[] args)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string outPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../Assets/Tide/Resources/levels.json"));
        // stats <levels.json>：看一份关卡文件每关的同色连续长度（对比改前改后用）
        if (args.Length > 1 && args[0] == "stats")
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(args[1]));
            int i = 0;
            foreach (var e in doc.RootElement.GetProperty("levels").EnumerateArray())
            {
                var c = JsonSerializer.Deserialize<Cfg>(e.GetProperty("cfg").GetRawText(), new JsonSerializerOptions { IncludeFields = true });
                var L = Rules.LoadLevel(new LevelEntry { cfg = c, seed = e.GetProperty("seed").GetInt64() });
                if (++i > 15) break;
                Console.WriteLine("关" + i + " 同色平均连 " + AvgRun(L.queue).ToString("0.0") + " 最长 " + MaxRun(L.queue));
            }
            return 0;
        }
        // diag：前 20 关"为什么玩家觉得平淡又简单"——同一关换几种玩家量：工厂用的玩家 / 会玩的真人（手误小、点得快）/ 随手乱点的人；
        // 再看会玩的人赢的那些局：风暴还剩几成时间、泊位最多停到几个（停满 4 个前那一下才紧张）、每一步平均有几艘船能开
        if (args.Length > 0 && args[0] == "diag")
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(outPath));
            int i = 0;
            Console.WriteLine("关 类型 | 工厂玩家 会玩的人 乱点的人 | 风暴剩余 | 泊位最多停到 2/3/4 个的局占比 | 每步可开的船");
            foreach (var e in doc.RootElement.GetProperty("levels").EnumerateArray())
            {
                if (++i > 20) break;
                var c = JsonSerializer.Deserialize<Cfg>(e.GetProperty("cfg").GetRawText(), new JsonSerializerOptions { IncludeFields = true });
                var L = Rules.LoadLevel(new LevelEntry { cfg = c, seed = e.GetProperty("seed").GetInt64() });
                double pF = Measure(L, c, 200, 11);
                var r = new Rng(77); int wG = 0, wR = 0; const int N = 200;
                for (int k = 0; k < N; k++) if (Rules.Playout(L, r, 3, c.storm, c.bonus, 1.1, out _)) wG++;
                for (int k = 0; k < N; k++) if (Rules.Playout(L, r, 200, c.storm, c.bonus, 1.1, out _)) wR++;
                // 会玩的人：手动走一遍，记录风暴余量、泊位峰值、可选步数
                var lefts = new List<double>(); var peak = new int[5]; double opts = 0; int steps = 0, wins = 0;
                for (int k = 0; k < N; k++)
                {
                    var s = Rules.NewState(L); double left = c.storm, minLeft = c.storm; int dep = 0, pk = 0;
                    for (int g = 0; g < 3000 && s.status == "play"; g++)
                    {
                        opts += Rules.ScoreMoves(L, s).Count; steps++;
                        int b = Rules.BestMoveLook(L, s, r, 3); if (b < 0) break;
                        Rules.Tap(L, s, b, null); s.t += 1.1;
                        int used = 0; foreach (int x in s.berth) if (x >= 0) used++; pk = Math.Max(pk, used);
                        if (c.storm > 0) { left -= 1.1; int d = Rules.Departed(L, s); left = Math.Min(c.storm, left + (d - dep) * c.bonus); dep = d; minLeft = Math.Min(minLeft, left); }
                    }
                    if (s.status == "win") { wins++; peak[Math.Min(4, pk)]++; if (c.storm > 0) lefts.Add(minLeft / c.storm); }
                }
                lefts.Sort();
                string storm = c.storm > 0 ? "最紧时还剩 " + (lefts.Count > 0 ? lefts[lefts.Count / 2] : 0).ToString("P0") : "无风暴";
                Console.WriteLine("关" + i + (c.tag == 2 ? "超难" : c.tag == 1 ? "困难" : "普通") + " | " + pF.ToString("P0") + " " + (wG / (double)N).ToString("P0") + " " + (wR / (double)N).ToString("P0") +
                    " | " + storm + " | " + string.Join("/", new[] { 2, 3, 4 }.Select(x => (peak[x] / (double)Math.Max(1, wins)).ToString("P0"))) + " | " + (opts / Math.Max(1, steps)).ToString("0.0"));
            }
            return 0;
        }
        // diag2：用"会想的玩家"（每一步把几个候选各往后试玩几局，挑赢面最大的）代表认真的真人，看前 20 关他赢多少、紧不紧
        if (args.Length > 0 && args[0] == "diag2")
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(outPath));
            int i = 0; int to = args.Length > 1 ? int.Parse(args[1]) : 20;
            Console.WriteLine("关 类型 plan | 会想的人赢 | 泊位最多停到 1/2/3/4 个 | 风暴最紧时剩余 | 点击数 船数");
            foreach (var e in doc.RootElement.GetProperty("levels").EnumerateArray())
            {
                if (++i > to) break;
                var c = JsonSerializer.Deserialize<Cfg>(e.GetProperty("cfg").GetRawText(), new JsonSerializerOptions { IncludeFields = true });
                var L = Rules.LoadLevel(new LevelEntry { cfg = c, seed = e.GetProperty("seed").GetInt64() });
                var r = new Rng(5 + i); const int G = 12; int wins = 0; var peak = new int[5]; var lefts = new List<double>(); int taps = 0;
                for (int g = 0; g < G; g++)
                {
                    var s = Rules.NewState(L); s.t = -1; double left = c.storm, minLeft = c.storm; int dep = 0, pk = 0;
                    for (int k = 0; k < 3000 && s.status == "play"; k++)
                    {
                        var m = Rules.ScoreMoves(L, s); if (m.Count == 0) break;
                        m.Sort((a, b) => b[1].CompareTo(a[1]));
                        int bi = m[0][0]; double bv = -1;
                        if (m.Count > 1)
                            foreach (var mv in m.Take(6))
                            {
                                int w = 0;
                                for (int t = 0; t < 6; t++) { var s2 = s.Clone(); Rules.Tap(L, s2, mv[0], null); if (s2.status == "win" || (s2.status == "play" && PlayoutFrom(L, s2, r, 14))) w++; }
                                if (w > bv) { bv = w; bi = mv[0]; }
                            }
                        Rules.Tap(L, s, bi, null);
                        int used = 0; foreach (int x in s.berth) if (x >= 0) used++; pk = Math.Max(pk, used);
                        if (c.storm > 0) { left -= 1.5; int d = Rules.Departed(L, s); left = Math.Min(c.storm, left + (d - dep) * c.bonus); dep = d; minLeft = Math.Min(minLeft, left); }
                    }
                    taps += s.taps;
                    if (s.status == "win" && minLeft >= 0) { wins++; peak[Math.Min(4, pk)]++; if (c.storm > 0) lefts.Add(minLeft / c.storm); }
                }
                lefts.Sort();
                Console.WriteLine("关" + i + (c.tag == 2 ? "超难" : c.tag == 1 ? "困难" : "普通") + " plan" + c.plan + " | " + (wins / (double)G).ToString("P0") + " | " +
                    string.Join("/", new[] { 1, 2, 3, 4 }.Select(x => peak[x].ToString())) + " | " + (c.storm > 0 && lefts.Count > 0 ? lefts[lefts.Count / 2].ToString("P0") : "-") +
                    " | " + (taps / G) + " " + L.boats.Count);
            }
            return 0;
        }
        // count [levels.json]：每关实际几艘船、几个人、实测通关率（核对规模接不接得上）
        if (args.Length > 0 && args[0] == "count")
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(args.Length > 1 ? args[1] : outPath));
            int i = 0;
            foreach (var e in doc.RootElement.GetProperty("levels").EnumerateArray())
            {
                var c = JsonSerializer.Deserialize<Cfg>(e.GetProperty("cfg").GetRawText(), new JsonSerializerOptions { IncludeFields = true });
                var L = Rules.LoadLevel(new LevelEntry { cfg = c, seed = e.GetProperty("seed").GetInt64() });
                i++;
                Console.WriteLine("关" + i + (c.tag == 2 ? " 超难" : c.tag == 1 ? " 困难" : "") + " 船 " + L.boats.Count + " 人 " + L.total + " nb " + c.nb + " " + c.w + "x" + c.h + " p " + e.GetProperty("p"));
            }
            return 0;
        }
        if (args.Length > 0 && args[0] == "probe")
        {
            foreach (int idx in new[] { 3, 10, 25, 45 })
            {
                var row = new List<string>();
                foreach (double tt in new[] { 0, 0.3, 0.6, 0.9, 1.1 })
                {
                    var r = new Rng(idx * 100 + (long)(tt * 10) + 1); var c = Order(idx, 1 - tt * 0.9, r);
                    var ps = new List<double>();
                    for (int s = 1; s <= 6; s++) { var L = Rules.GenLevel(c, s); if (L == null) continue; SetStorm(idx, L, c, s); ps.Add(Measure(L, c, 30, s)); }
                    row.Add("t" + tt + ":" + (ps.Count > 0 ? ps.Average().ToString("0.00") : "--"));
                }
                Console.WriteLine("关" + (idx + 1) + " nb " + NB(idx) + "  " + string.Join("  ", row));
            }
            return 0;
        }
        var outl = new List<object>();
        int from = args.Length > 1 && args[0] == "from" ? int.Parse(args[1]) - 1 : 0;
        int upTo = args.Length > 3 && args[2] == "to" ? int.Parse(args[3]) : TOTAL;
        var old = new List<JsonElement>();
        if (from > 0 || upTo < TOTAL)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(outPath));
            foreach (var e in doc.RootElement.GetProperty("levels").EnumerateArray()) old.Add(JsonSerializer.Deserialize<JsonElement>(e.GetRawText()));
            Console.WriteLine("只重出第 " + (from + 1) + "~" + upTo + " 关，其余照抄");
        }
        for (int idx = 0; idx < TOTAL; idx++)
        {
            if (idx < from || idx >= upTo) { outl.Add(old[idx]); continue; }
            var r = new Rng(idx * 7717 + 99); double target = Target(idx);
            INTRO.TryGetValue(idx, out var intro);
            (Cfg cfg, long seed, double p, double err, int nb, int q)? best = null;
            for (int tries = 0; tries < (idx < 15 ? 260 : 120); tries++)
            {
                var c = Order(idx, target, r); long seed = 1 + (long)(r.Next() * 1e6);
                var L = Rules.GenLevel(c, seed);
                if (L == null || (intro != null && !HasIntro(L, intro))) continue;
                SetStorm(idx, L, c, seed);
                double p0 = Measure(L, c, 24, seed);
                if (best != null && Math.Abs(p0 - target) > best.Value.err + 0.14) continue;
                double p = idx == 0 ? p0 : Measure(L, c, 100, seed + 7);
                double err = Math.Abs(p - target);
                if (best == null || err < best.Value.err) best = (c, seed, p, err, L.boats.Count, L.total);
                if (err < 0.025) break;
            }
            if (best == null) { Console.WriteLine("关" + (idx + 1) + " 生成失败"); return 1; }
            var b = best.Value;
            outl.Add(new { cfg = b.cfg, seed = b.seed, p = Math.Round(b.p, 2) });
            var LL = Rules.GenLevel(b.cfg, b.seed);
            Console.WriteLine("关" + (idx + 1) + (b.cfg.tag == 2 ? " [超难]" : b.cfg.tag == 1 ? " [困难]" : "") + " 目标 " + target.ToString("0.00") + " 实测 " + b.p.ToString("0.00") +
                " 同色平均连 " + AvgRun(LL.queue).ToString("0.0") + " 最长 " + MaxRun(LL.queue) +
                " 船 " + b.nb + " 人 " + b.q + " 色 " + b.cfg.colors + " 风暴 " + b.cfg.storm + "s+" + b.cfg.bonus + " " + (b.cfg.rot != 0 ? "斜" : "正") + b.cfg.w + "x" + b.cfg.h +
                (intro != null ? " 新:" + intro : "") + (Theme(idx) != null ? " 主题:" + Theme(idx) : ""));
        }
        var json = JsonSerializer.Serialize(new { levels = outl }, new JsonSerializerOptions { IncludeFields = true });
        File.WriteAllText(outPath, json);
        Console.WriteLine("写入 " + outPath + "  用时 " + sw.Elapsed.TotalSeconds.ToString("0") + " 秒");
        return 0;
    }
}
