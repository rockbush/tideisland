// 自动测试：启动参数带 -tideTest <输出目录> 时，自己点一遍、截图、记录报错，然后退出。
// 打包好的 exe：island.exe -screen-width 390 -screen-height 844 -screen-fullscreen 0 -tideTest D:\shots [-tideLang en|zh] [-tideMeasure]
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Tide
{
    public class AutoTest : MonoBehaviour
    {
        public static string Dir
        {
            get
            {
                var a = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(a, "-tideTest");
                return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
            }
        }
        public static bool Wanted => Dir != null;
        public static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }
        public static readonly List<string> Log = new List<string>();
        int errors;

        void Awake()
        {
            Application.logMessageReceived += (msg, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { errors++; Log.Add("[" + type + "] " + msg + "\n" + stack); }
                else if (msg.StartsWith("[Tide]") || msg.StartsWith("[Test]")) Log.Add(msg);
            };
        }

        async void Start()
        {
            // 测试窗口不接受鼠标（用户在旁边玩的时候可能点到它，点一下"下一关"测试就乱了）
            var es = UnityEngine.EventSystems.EventSystem.current; if (es) es.enabled = false;
            string dir = Dir; Directory.CreateDirectory(dir);
            try { await Game.I.RunTest(dir); }
            catch (Exception e) { Log.Add("[TestException] " + e); errors++; }
            if (Loc.Missing.Count > 0) { Log.Add("[Test] 没翻译的字：" + string.Join(" | ", Loc.Missing)); errors++; }
            Log.Add("[Test] errors=" + errors);
            File.WriteAllText(System.IO.Path.Combine(dir, "log.txt"), string.Join("\n", Log));
            Application.Quit();
        }
    }

    public partial class Game
    {
        // 每关：港湾最远一排一格在屏幕上多少 pt（界面单位，= iPhone 的 pt），小人多高，泊位多宽
        async Task Measure(string dir)
        {
            var rows = new List<string> { "关,船,列x行,格子pt(最远排),格子pt(最近排),小人高pt,泊位宽pt" };
            int tapTotal = 0, tapMiss = 0;
            for (int i = 0; i < LEVELS.Length; i++)
            {
                LoadLevel(i); await Fx.SleepKeep(60); FitCam(); PlaceCam(true);
                float Pt(Vector3 a, Vector3 b) => Vector3.Distance(cam.WorldToScreenPoint(a), cam.WorldToScreenPoint(b)) / ui.scale;
                float hg = 0.5f * lay.gs, far = Pt(W(-hg, lay.z0 + hg), W(hg, lay.z0 + hg)), near = Pt(W(-hg, lay.z1 - hg), W(hg, lay.z1 - hg));
                float guy = Pt(W(0, lay.zQ1, 0.25f), W(0, lay.zQ1, 0.25f + 0.8f * GS));
                float berth = Pt(W(BX(0) - BW / 2, lay.zP), W(BX(0) + BW / 2, lay.zP));
                // 点每艘船的中心和两头（各往里 0.3 格），看是不是选中它自己
                foreach (var u in boatV)
                {
                    if (u == null || u.state != "lot") continue;
                    var hm = BoatHome(u.b); var d = DirU(u.b); float half = (L.boats[u.b].len - 0.16f) / 2 * lay.gs;
                    foreach (float o in new[] { 0f, half - 0.3f * lay.gs, -(half - 0.3f * lay.gs) })
                    {
                        tapTotal++;
                        if (PickBoat(cam.WorldToScreenPoint(hm.pos + d * o + Vector3.up * 0.25f)) != u.b) tapMiss++;
                    }
                }
                rows.Add(string.Join(",", i + 1, L.boats.Count, L.cfg.w + "x" + L.cfg.h + (L.rot != 0 ? "斜" : ""), far.ToString("0.0"), near.ToString("0.0"), guy.ToString("0.0"), berth.ToString("0.0")));
            }
            rows.Add("点船测试：" + tapTotal + " 次，选错 " + tapMiss + " 次");
            File.WriteAllText(System.IO.Path.Combine(dir, "measure.csv"), string.Join("\n", rows));
        }

        // 直升机演出连拍：吊一艘被挡住的船，每 0.03 秒拍一张（配 -tideSlow 0.05 用）
        async Task HeliShots(string dir)
        {
            LoadLevel(19); await Fx.SleepKeep(1500); save.coins = 500;
            for (int r = 0; r < 2; r++)
            {
                var lot = Rules.InLot(L, st).Where(x => !Rules.PathOf(L, st, x).free).ToList(); if (lot.Count == 0) break;
                int blk = r == 0 ? lot[0] : lot[lot.Count - 1];
                DoCrane(); TapAt(cam.WorldToScreenPoint(boatV[blk].g.transform.position + Vector3.up * 0.3f));
                for (int i = 0; i < 21; i++) { await Fx.SleepKeep(30); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "h" + r + "_" + i.ToString("00") + ".png")); }   // 不用 Shot()：它前后各等一会儿，拍不到中间
                await WaitIdle(); TLog("直升机 第" + r + "次 船" + blk + " 状态=" + boatV[blk].state + " 泊位=" + string.Join(",", st.berth));
            }
        }

        // 泊位锁碎掉的过程（-tideLock，可配 -tideSlow 0.25 拍慢动作）
        async Task LockShots(string dir)
        {
            LoadLevel(12); await Fx.SleepKeep(4000); save.berthFree = 1;   // 等连胜横幅过去
            TapLock();
            for (int i = 0; i < 40; i++) { await Fx.SleepKeep(40); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "l_" + i.ToString("00") + ".png")); }
            await Fx.SleepKeep(1500); TLog("开锁后 泊位=" + st.berth.Count + " 碎片残留=" + shards.Count);
        }

        // 家园：通关拿星 → 进岛 → 建一样（拍中间几帧）→ 建完一个区领奖 → 四个区全建好的样子 → 宝箱、图鉴 → 开始界面红点 → 连胜开局送道具
        async Task HomeShots(string dir)
        {
            save = save.Reset(); save.homeVer = 1; save.seen.AddRange(new[] { "yacht", "raft", "carrier", "rock", "house", "mys", "storm", "weed", "lock", "shark" }); save.Persist();
            PlayLevel(0); await Fx.SleepKeep(1200);
            await WinFx(); await Fx.SleepKeep(1200); await Shot(dir, "h_win");
            TLog("第 1 关赢了 星星=" + save.star + " 能建=" + CanBuild);
            ShowHome("win"); await Fx.SleepKeep(900); await Shot(dir, "h_home0");
            var s0 = spots.First(s => s.a == 0 && s.t.cost <= save.star);
            TapSpot(s0);
            for (int i = 0; i < 16; i++) { await Fx.SleepKeep(80); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "h_build_" + i.ToString("00") + ".png")); }
            await Fx.SleepKeep(1500); await Shot(dir, "h_build_done");
            TLog("建了 " + s0.t.id + " 剩星星=" + save.star + " 岛民=" + folks.Count);
            // 星星不够的牌子点一下
            save.star = 0; HomeRefresh(); TapSpot(spots.First(s => s.a == 0 && !Built(s.t.id))); await Fx.SleepKeep(300); await Shot(dir, "h_need");
            // 把第一区建到只剩一样，再建最后一样 → 完工
            save.star = 30; foreach (var s in spots.Where(s => s.a == 0 && !Built(s.t.id)).Skip(1).ToList()) save.built.Add(s.t.id);
            save.Persist(); BuildHome(); BuildHomeUI(); await Fx.SleepKeep(600); await Shot(dir, "h_almost");
            TapSpot(spots.First(s => s.a == 0 && !Built(s.t.id))); await Fx.SleepKeep(3200); await Shot(dir, "h_area_done");
            TLog("第一区完工 弹窗=" + ModalOpen + " 金币=" + save.coins + " 直升机=" + save.crane);
            HideModal(); homeZT = HOME.areas[1].z; HomeRefresh(); await Fx.SleepKeep(1500); await Shot(dir, "h_village_ruin");
            // 全部建好：每个区拍一张
            foreach (var A in HOME.areas) foreach (var t in A.tasks) if (!Built(t.id)) save.built.Add(t.id);
            foreach (var A in HOME.areas) if (!save.areasDone.Contains(A.id)) save.areasDone.Add(A.id);
            save.Persist(); BuildHome(); BuildHomeUI();
            for (int a = 0; a < HOME.areas.Length; a++) { homeZT = HOME.areas[a].z; await Fx.SleepKeep(1400); await Shot(dir, "h_built_" + a); }
            TLog("每日礼物 直升机=" + PerkReady("heli") + " 喇叭=" + PerkReady("horn") + " 金币加成=" + HasPerk("coin"));
            // 宝箱、图鉴
            save.starGot = 20; save.chests = 0; HomeRefresh(); await Fx.SleepKeep(300); await Shot(dir, "h_chest_ready");
            OpenChest(); await Fx.SleepKeep(700); await Shot(dir, "h_chest"); HideModal();
            ShowAlbum(); await Fx.SleepKeep(500); await Shot(dir, "h_album"); HideModal();
            HomeBack(); await Fx.SleepKeep(1200);
            // 开始界面的红点（还有一个宝箱没开）
            HideHome(); SetHud(false); ShowTitle(); await Fx.SleepKeep(800); await Shot(dir, "h_title");
            // 连胜 3：开局送喇叭 + 泊位 + 直升机
            save.streak = 3; save.Persist(); PlayLevel(3); await Fx.SleepKeep(1800); await Shot(dir, "h_streak");
            TLog("连胜开局 送直升机=" + gCrane + " 送喇叭=" + gSort + " 泊位=" + st.berth.Count);
            // 进岛、出岛不留东西：回到关卡后镜头和输入正常
            ShowHome("game"); await Fx.SleepKeep(600); HomeBack(); await Fx.SleepKeep(500);
            TLog("回到关卡 OnScreen=" + OnScreen + " 家园=" + homeOpen + " 镜头=" + cam.transform.position.ToString("0.0"));
            await Shot(dir, "h_back");
        }

        // 每日：开始界面两个圆钮 → 签到 7 天（用 DayShift 模拟过日子）→ 断签重来 → 每日任务从 0 到全领完 → 真打一关看任务进度
        async Task DailyShots(string dir)
        {
            save = save.Reset(); save.homeVer = 1; save.top = 6; save.seen.AddRange(new[] { "yacht", "raft", "carrier", "rock", "house", "mys", "storm", "weed", "lock", "shark" }); save.Persist();
            SaveData.DayShift = 0;
            ShowTitle(); await Fx.SleepKeep(800); await Shot(dir, "d_title");
            ShowSign(); await Fx.SleepKeep(600); await Shot(dir, "d_sign0");
            int c0 = save.coins; SignClaim(1); await Fx.SleepKeep(500); await Shot(dir, "d_sign1");
            TLog("签到第 1 天 金币 " + c0 + "→" + save.coins + " 今天签了=" + SignedToday);
            for (int d = 1; d < 7; d++)
            {
                SaveData.DayShift = d; HideModal(); ShowSign(); await Fx.SleepKeep(300);
                if (d == 6) await Shot(dir, "d_sign6");
                SignClaim(1); await Fx.SleepKeep(300);
            }
            await Shot(dir, "d_sign7");
            TLog("签满 7 天 step=" + save.signStep + " 金币=" + save.coins + " 直升机=" + save.crane + " 喇叭=" + save.sort);
            SaveData.DayShift = 7; HideModal(); ShowSign(); await Fx.SleepKeep(400); TLog("第 8 天（连着）该领第 " + (SignNext + 1) + " 天");
            SaveData.DayShift = 9; HideModal(); ShowSign(); await Fx.SleepKeep(400); await Shot(dir, "d_sign_reset");
            TLog("断了一天 该领第 " + (SignNext + 1) + " 天 已领格子=" + SignShown);
            HideModal();
            // 任务
            SaveData.DayShift = 10; ShowTitle(); await Fx.SleepKeep(500);
            ShowTasks(); await Fx.SleepKeep(500); await Shot(dir, "d_tasks0");
            TLog("今天的任务：" + string.Join(" / ", save.taskId));
            HideModal(); PlayLevel(0); await Fx.SleepKeep(1200);
            await WinFx(); await Fx.SleepKeep(1500); await Shot(dir, "d_win");
            TLog("通关一关后进度：" + string.Join(",", save.taskProg));
            HideModal();
            for (int i = 0; i < 3; i++) { var t = TaskOf(i); TaskAdd(t.kind, t.need); }
            await Fx.SleepKeep(400); ShowTasks(); await Fx.SleepKeep(500); await Shot(dir, "d_tasks_done");
            for (int i = 0; i < 3; i++) TaskClaim(i);
            await Fx.SleepKeep(500); await Shot(dir, "d_tasks_claimed");
            TaskBonusClaim(); await Fx.SleepKeep(600); await Shot(dir, "d_tasks_bonus");
            TLog("任务全领 金币=" + save.coins + " 大奖=" + save.taskBonus);
            // 换天：任务重抽、进度清零
            SaveData.DayShift = 11; TaskEnsure(); TLog("第二天的任务：" + string.Join(" / ", save.taskId) + " 进度=" + string.Join(",", save.taskProg));
            HideModal(); ShowTitle(); await Fx.SleepKeep(600); await Shot(dir, "d_title2");
            PlayLevel(1); await Fx.SleepKeep(1200); OpenMenu(); await Fx.SleepKeep(300); await Shot(dir, "d_menu"); CloseMenu();
            SaveData.DayShift = 0;
        }

        // 新手引导：第 1 关三步、第 2 关被挡住的船、第一次快停满（码头 → 锁）、道具没解锁的样子、第 5/7/9 关解锁道具
        async Task GuideShots(string dir)
        {
            save = save.Reset(); save.homeVer = 1; save.seen.AddRange(new[] { "yacht", "raft", "carrier", "rock", "house", "mys", "storm", "weed", "lock", "shark" }); save.Persist();
            string Cur() => guideCur != null ? guideCur.id : "（无）";
            PlayLevel(0); await Fx.SleepKeep(1600); await Shot(dir, "g1_head"); TLog("第1关 引导=" + Cur());
            GuideTapOutside(); await Fx.SleepKeep(500); await Shot(dir, "g1_boat"); TLog("点一下以后 引导=" + Cur());
            GuideTapOutside(); await Fx.SleepKeep(300); TLog("点暗处（不该走）引导=" + Cur());
            OnBoat(TutorBoat()); await Fx.SleepKeep(600); await WaitIdle(); await Fx.SleepKeep(600); await Shot(dir, "g1_dock"); TLog("点船以后 引导=" + Cur());
            GuideTapOutside(); await Fx.SleepKeep(500); await Shot(dir, "g1_free"); TLog("第1关引导走完 引导=" + Cur() + " 排队=" + guideQ.Count);
            PlayLevel(1); await Fx.SleepKeep(1600); await Shot(dir, "g2_block"); TLog("第2关 引导=" + Cur());
            GuideTapOutside(); await Fx.SleepKeep(300);
            PlayLevel(2); await Fx.SleepKeep(1400); GuideNearlyFull(); await Fx.SleepKeep(500); await Shot(dir, "g3_full"); TLog("快停满 引导=" + Cur());
            GuideTapOutside(); await Fx.SleepKeep(500); await Shot(dir, "g3_lock"); TLog("下一步 引导=" + Cur());
            GuideTapOutside(); await Fx.SleepKeep(300);
            PlayLevel(3); await Fx.SleepKeep(1400); await Shot(dir, "g4_locked"); BoosterTap(0); await Fx.SleepKeep(300); await Shot(dir, "g4_locked_tap");
            TLog("第4关 道具解锁=" + Unlocked(0) + "," + Unlocked(1) + "," + Unlocked(2));
            // 第 5 关：直升机
            save.top = 4; save.Persist(); int cr0 = save.crane;
            PlayLevel(4); await Fx.SleepKeep(1600); await Shot(dir, "u1_card"); TLog("第5关 引导=" + Cur() + " 弹窗=" + ModalOpen + " 直升机 " + cr0 + "→" + save.crane);
            HideModal(); await Fx.SleepKeep(500); await Shot(dir, "u1_btn"); TLog("关卡片后 引导=" + Cur());
            BoosterTap(0); await Fx.SleepKeep(500); await Shot(dir, "u1_boat"); TLog("点直升机后 引导=" + Cur() + " craneMode=" + craneMode);
            int bb = PickBoat(x => !Rules.CanGo(L, st, x)); if (bb < 0) bb = PickBoat(x => true);
            OnBoat(bb); await Fx.SleepKeep(3000); await Shot(dir, "u1_done"); TLog("吊完 引导=" + Cur() + " 讲过=" + save.seen.Contains("unlock_crane"));
            // 第 7 关：喇叭
            save.top = 6; save.Persist();
            PlayLevel(6); await Fx.SleepKeep(1600); await Shot(dir, "u2_card"); HideModal(); await Fx.SleepKeep(400); await Shot(dir, "u2_btn");
            BoosterTap(1); await Fx.SleepKeep(800); TLog("第7关 点喇叭后 引导=" + Cur() + " 讲过=" + save.seen.Contains("unlock_sort"));
            // 第 9 关：临时码头（送 1 次免费）
            save.top = 8; save.Persist();
            PlayLevel(8); await Fx.SleepKeep(1600); await Shot(dir, "u3_card"); HideModal(); await Fx.SleepKeep(400); await Shot(dir, "u3_btn");
            int nb0 = st.berth.Count; BoosterTap(2); await Fx.SleepKeep(800); await Shot(dir, "u3_done");
            TLog("第9关 泊位 " + nb0 + "→" + st.berth.Count + " 免费剩=" + save.berthFree + " 引导=" + Cur() + " 讲过=" + save.seen.Contains("unlock_berth"));
            // 连胜奖励：喇叭没解锁时不送
            save = save.Reset(); save.homeVer = 1; save.seen.AddRange(new[] { "g_l1c", "g_l2", "g_full2" }); save.streak = 1; save.top = 1; save.Persist();
            PlayLevel(1); await Fx.SleepKeep(1200); TLog("第2关 连胜1 送喇叭=" + gSort + "（喇叭没解锁，应为0）");
            // ---- 第二批：通关 → 小岛走一圈 → 宝箱 → 每日礼物 → 开始界面按钮分批出现 → 连胜卡片 ----
            save = save.Reset(); save.homeVer = 1; save.seen.AddRange(new[] { "yacht", "raft", "carrier", "rock", "house", "mys", "storm", "weed", "lock", "shark", "g_l1c", "g_l2", "g_full2" }); save.Persist();
            HideHome(); ShowTitle(); await Fx.SleepKeep(700); await Shot(dir, "gt0_title"); TLog("新玩家开始界面 小岛按钮=" + (titleHomeBtn != null) + " 签到按钮=" + (titleSignBtn != null));
            PlayLevel(0); await Fx.SleepKeep(1200); await WinFx(); await Fx.SleepKeep(1300); await Shot(dir, "gh_win");
            ShowHome("win"); await Fx.SleepKeep(900); await Shot(dir, "gh_home1"); TLog("进小岛 引导=" + Cur());
            var s1 = spots.Where(s => s.mark != null && save.star >= s.t.cost).OrderBy(s => s.t.cost).First();
            TapSpot(s1); await Fx.SleepKeep(3000); await Shot(dir, "gh_home2"); TLog("建完 引导=" + Cur());
            GuideTapOutside(); await Fx.SleepKeep(400); TLog("点一下 引导=" + Cur());
            save.starGot = 10; HomeRefresh(); await Fx.SleepKeep(500); await Shot(dir, "gh_chest"); TLog("宝箱 引导=" + Cur());
            OpenChest(); await Fx.SleepKeep(500); HideModal(); await Fx.SleepKeep(300); TLog("开了宝箱 引导=" + Cur());
            if (!save.built.Contains("v_pad")) save.built.Add("v_pad");
            save.Persist(); BuildHome(); BuildHomeUI(); homeZT = HOME.areas[1].z; await Fx.SleepKeep(1500); await Shot(dir, "gh_perk"); TLog("每日礼物 引导=" + Cur());
            ClaimPerk(spots.First(s => s.t.perk == "heli"), "heli"); await Fx.SleepKeep(400); TLog("领了 引导=" + Cur());
            HideHome(); save.top = 3; save.Persist(); ShowTitle(); await Fx.SleepKeep(800); await Shot(dir, "gt3_sign"); TLog("打过3关 开始界面 引导=" + Cur());
            ShowSign(); await Fx.SleepKeep(400); HideModal(); await Fx.SleepKeep(500); await Shot(dir, "gt3_tasks"); TLog("签到以后 引导=" + Cur());
            ShowTasks(); await Fx.SleepKeep(400); HideModal(); await Fx.SleepKeep(300); TLog("任务以后 引导=" + Cur());
            save.top = 7; save.seen.AddRange(new[] { "unlock_crane", "unlock_sort", "unlock_berth" }); save.streak = 1; save.Persist();
            PlayLevel(7); await Fx.SleepKeep(3200); await Shot(dir, "gs_streak"); TLog("连胜卡片 弹窗=" + ModalOpen + " 引导=" + Cur());
            HideModal();
            // 过关回地图：下一关解锁动画（拍几帧）
            PlayLevel(2); await Fx.SleepKeep(1200); save.top = 2; await WinFx(); await Fx.SleepKeep(1300); TLog("第3关赢了 要播解锁=" + mapUnlockAnim);
            HideModal(); ShowMap(true);
            foreach (int ms in new[] { 300, 500, 250, 200, 600 }) { await Fx.SleepKeep(ms); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "m_unlock_" + ms + "_" + Mathf.RoundToInt(Fx.Now) + ".png")); }
            TLog("地图 当前关=" + CurLevel + " 解锁动画用掉=" + (mapUnlockAnim < 0));
        }


        // 重来换色、道具每关上限、通关金币
        // 开始界面第 2 版（Title2.cs）：老玩家（全部入口都有）连拍几帧看动画，再看新玩家（只有地图、开始）
        async Task TitleShots(string dir)
        {
            save = save.Reset(); save.top = 31; save.coins = 1250; save.streak = 3; save.star = 29; save.starGot = 27; save.chests = 1;
            for (int i = 0; i < 31; i++) save.SetStar(i, 1 + i % 3);
            save.seen.Add("g_streak"); save.Persist();
            ShowTitle();
            for (int k = 0; k < 4; k++) { await Fx.SleepKeep(k == 0 ? 1200 : 350); await Shot(dir, "tt_vet" + k); }
            ShowTasks(); await Fx.SleepKeep(400); await Shot(dir, "tt_tasks"); HideModal();
            save = save.Reset(); save.Persist(); ShowTitle(); await Fx.SleepKeep(800); await Shot(dir, "tt_new");
            TLog("开始界面 新玩家 小岛按钮=" + (titleHomeBtn != null) + " 签到=" + (titleSignBtn != null));
        }

        async Task BalanceShots(string dir)
        {
            save = save.Reset(); save.homeVer = 1; save.top = 12; save.seen.AddRange(new[] { "yacht", "raft", "carrier", "rock", "house", "mys", "storm", "weed", "lock", "shark", "unlock_crane", "unlock_sort", "unlock_berth", "tip_crane", "tip_sort", "tip_berth" }); save.Persist();
            string Sig() => string.Join("", L.boats.Take(8).Select(b => b.c)) + " | 队伍 " + string.Join("", st.q.Take(10));
            for (int k = 0; k < 3; k++) { PlayLevel(4); await Fx.SleepKeep(1300); await Shot(dir, "c_try" + k); TLog("第5关 第" + (k + 1) + "次 颜色 " + Sig()); }
            // 道具上限
            save.crane = 5; save.sort = 5; save.Persist();
            PlayLevel(9); await Fx.SleepKeep(1400); await Shot(dir, "c_cap0"); TLog("库存 5 开局 角标 直升机=" + ui.cnt[0].text + " 喇叭=" + ui.cnt[1].text + "（应为 2）");
            for (int k = 0; k < 3; k++)
            {
                BoosterTap(0); await Fx.SleepKeep(200);
                int bb = PickBoat(x => true); if (craneMode && bb >= 0) OnBoat(bb);
                await Fx.SleepKeep(2200);
                TLog("第" + (k + 1) + "次点直升机 用了=" + boosterUse[0] + " 还剩=" + save.crane + " 吊船模式=" + craneMode + " 角标=" + ui.cnt[0].text);
            }
            await Shot(dir, "c_cap");
            // 通关金币
            foreach (int lv in new[] { 0, 9, 15, 30 })
            {
                PlayLevel(lv); await Fx.SleepKeep(1000); int c0 = save.coins; await WinFx(); await Fx.SleepKeep(1200);
                TLog("第" + (lv + 1) + "关 难度=" + L.cfg.tag + " 通关金币=" + (save.coins - c0)); HideModal();
            }
        }

        // 压力测试：真实动画、很快的节奏连续点（七成按最佳走法、三成乱点），输了就花金币复活，偶尔用直升机。
        // 看门狗：规则里泊位上有船、画面上那艘船却超过 8 秒还没开到 → 记下当时的情况（"第一个泊位永远不停船"的 bug）
        // 新机制（第 15/17/19 关）：开局画面、介绍卡片、点被缠住/锁住的船、自动玩一段，每步拍一张
        async Task MechShots(string dir)
        {
            save.seen.AddRange(new[] { "yacht", "raft", "carrier", "rock", "house", "mys", "storm", "tip_crane", "tip_sort", "tip_berth" });
            foreach (int lv in new[] { 14, 16, 18 })
            {
                string mk = lv == 14 ? "weed" : lv == 16 ? "lock" : "shark";
                save.seen.RemoveAll(x => x == mk); if (!save.seen.Contains("weed") && lv > 14) save.seen.Add("weed"); if (lv > 16 && !save.seen.Contains("lock")) save.seen.Add("lock");
                LoadLevel(lv); await Fx.SleepKeep(1600);
                await Shot(dir, "m" + (lv + 1) + "_card"); HideModal(); await Fx.SleepKeep(400);
                await Shot(dir, "m" + (lv + 1) + "_a");
                TLog("机制 关" + (lv + 1) + " 海藻船 " + string.Join(",", Enumerable.Range(0, L.boats.Count).Where(b => L.boats[b].weed > 0).Select(b => b + "(" + L.boats[b].weed + ")")) +
                     " 锁 " + string.Join(",", Enumerable.Range(0, L.boats.Count).Where(b => L.boats[b].lockBy >= 0).Select(b => b + "←钥匙" + L.boats[b].lockBy)) + " 鲨鱼 " + L.sharks.Count);
                // 点一艘被缠住 / 锁住的船：应该走不了
                int stuckB = Rules.InLot(L, st).FirstOrDefault(b => Rules.Stuck(L, st, b));
                if (Rules.Stuck(L, st, stuckB)) { TapAt(cam.WorldToScreenPoint(boatV[stuckB].g.transform.position + Vector3.up * 0.3f)); await Fx.SleepKeep(300); await Shot(dir, "m" + (lv + 1) + "_refuse"); TLog("点被困的船" + stuckB + " 还在港湾=" + (st.inl[stuckB] != 0)); }
                // 钥匙：用直升机把钥匙船吊走，拍开锁动画
                int kb = Enumerable.Range(0, L.boats.Count).FirstOrDefault(b => L.boats[b].key >= 0 && st.inl[b] != 0);
                if (L.boats[kb].key >= 0 && st.inl[kb] != 0)
                {
                    save.coins = 999; DoCrane(); TapAt(cam.WorldToScreenPoint(boatV[kb].g.transform.position + Vector3.up * 0.3f));
                    for (int i = 0; i < 6; i++) { await Fx.SleepKeep(150); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "m" + (lv + 1) + "_key" + i + ".png")); }
                    int lk = L.boats.FindIndex(B => B.lockBy == kb); TLog("钥匙船" + kb + "吊走 锁住的船" + lk + " 还锁着=" + Rules.Chained(L, st, lk)); await WaitIdle();
                }
                // 鲨鱼：等鲨鱼正好挡在某艘能走的船出口，点它，应该被吓回来
                if (L.sharks.Count > 0)
                {
                    int sb = -1; for (int w = 0; w < 120 && sb < 0; w++) { sb = Rules.InLot(L, st).FirstOrDefault(b => Rules.CanGo(L, st, b) && Rules.SharkHit(L, st, b) >= 0); if (!(Rules.CanGo(L, st, sb) && Rules.SharkHit(L, st, sb) >= 0)) { sb = -1; await Fx.SleepKeep(100); } }
                    if (sb >= 0)
                    {
                        TapAt(cam.WorldToScreenPoint(boatV[sb].g.transform.position + Vector3.up * 0.3f));
                        for (int i = 0; i < 4; i++) { await Fx.SleepKeep(90); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "m" + (lv + 1) + "_shark" + i + ".png")); }
                        TLog("鲨鱼挡住船" + sb + " 还在港湾=" + (st.inl[sb] != 0));
                    }
                    else TLog("没等到鲨鱼挡路的时机");
                }
                for (int i = 0; i < 30 && st.status == "play"; i++)
                {
                    int b = Rules.BestMove(L, st, null, 0); if (b < 0) break;
                    TapAt(cam.WorldToScreenPoint(boatV[b].g.transform.position + Vector3.up * 0.3f));
                    await Fx.SleepKeep(700);
                    if (i % 3 == 2) await Shot(dir, "m" + (lv + 1) + "_p" + i.ToString("00"));
                }
                TLog("机制 关" + (lv + 1) + " 走了 " + st.gone + " 艘 状态=" + st.status + " t=" + st.t.ToString("0.0"));
            }
        }

        async Task Stress(string dir)
        {
            HideTitle(); SetHud(true);
            var rnd = new Rng(42); var log = new List<string>(); int stuck = 0, ghosts = 0, wins = 0, revives = 0;
            var goSince = new Dictionary<int, float>();
            foreach (int lv in (AutoTest.Arg("-tideStress") ?? "4,7,9,12,14,16,17,18,20,23,26,29").Split(',').Select(int.Parse))
                for (int rep = 0; rep < 2; rep++)
                {
                    PlayLevel(lv); await Fx.SleepKeep(1100); goSince.Clear(); save.coins = 9999;
                    float t0 = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - t0 < 120)
                    {
                        if (ModalOpen)
                        {
                            if (st.status == "win") { wins++; break; }
                            if (st.status == "lose") { if (st.berth.Count >= MAXB) break; save.coins = 9999; Revive(true); revives++; await Fx.SleepKeep(300); continue; }
                            HideModal();
                        }
                        if (st.status == "win") { await Fx.SleepKeep(300); continue; }
                        // 看门狗
                        for (int k = 0; k < st.berth.Count; k++)
                        {
                            int b = st.berth[k]; if (b < 0) continue;
                            var u = boatV[b];
                            if (u == null || !u.g) { ghosts++; log.Add("幽灵 关" + (lv + 1) + " 泊位" + k + " 船" + b + " 画面上没有这艘船"); continue; }
                            if (u.state == "go") { if (!goSince.ContainsKey(b)) goSince[b] = Time.realtimeSinceStartup; else if (Time.realtimeSinceStartup - goSince[b] > 20) { stuck++; log.Add("卡住 关" + (lv + 1) + " 泊位" + k + " 船" + b + " 门(gate)已开=" + gates[k].p.IsCompleted + " 门的主人=船" + gates[k].owner + "(" + (gates[k].owner >= 0 && boatV[gates[k].owner] != null ? boatV[gates[k].owner].state + " 已上" + boatV[gates[k].owner].riders.Count + "/" + L.boats[gates[k].owner].cap + " 规则load=" + st.load[gates[k].owner] : "?") + ") 阶段=" + gates[k].stage + (gates[k].owner >= 0 && boatV[gates[k].owner] != null ? " 岛民=[" + string.Join("; ", boatV[gates[k].owner].dashG.Select(gg => gg.dbg + (gg.g ? "" : " (已销毁)"))) + "] 现在=" + Fx.Now.ToString("0") + " 动画数=" + Fx.TweenCount : "") + " pipeN=" + pipeN + " 位置=" + u.g.transform.position.ToString("0.0") + " 泊位表=" + string.Join(",", st.berth)); goSince[b] = 1e9f; } }
                            else goSince.Remove(b);
                        }
                        var fr = Rules.InLot(L, st).Where(x => Rules.PathOf(L, st, x).free && boatV[x] != null && boatV[x].state == "lot").ToList();
                        if (st.status == "play" && fr.Count > 0)
                        {
                            int b = rnd.Next() < 0.6 ? Rules.BestMove(L, st, null, 0) : fr[rnd.Int(fr.Count)];
                            if (rnd.Next() < 0.05) { var bl = Rules.InLot(L, st).Where(x => boatV[x] != null && boatV[x].state == "lot").ToList(); if (bl.Count > 0) { DoCrane(); b = bl[rnd.Int(bl.Count)]; } }
                            if (b >= 0 && boatV[b] != null) TapAt(cam.WorldToScreenPoint(boatV[b].g.transform.position + Vector3.up * 0.3f));
                        }
                        await Fx.SleepKeep(350 + rnd.Int(500));
                    }
                    TLog("压力 关" + (lv + 1) + " 第" + (rep + 1) + "次 状态=" + st.status + " 用时" + (Time.realtimeSinceStartup - t0).ToString("0") + "s");
                }
            log.Insert(0, "过关 " + wins + " 复活 " + revives + " 卡住 " + stuck + " 幽灵 " + ghosts);
            File.WriteAllText(System.IO.Path.Combine(dir, "stress.txt"), string.Join("\n", log));
            TLog(log[0]);
        }

        // 连击 + 特殊船原型（-tideCombo 4,8,12）：机器人按最佳步快点，记连击/特殊船/集合，变出特殊船和连击时截图
        async Task ComboTest(string dir)
        {
            HideTitle(); SetHud(true);
            int shots = 0, fvShots = 0, maxChain = 0, totalSp = 0, wins = 0, loses = 0, pace = int.Parse(AutoTest.Arg("-tidePace") ?? "300");   // -tidePace 1200：按真人节奏点
            float mix = float.Parse(AutoTest.Arg("-tideMix") ?? "0", System.Globalization.CultureInfo.InvariantCulture);   // -tideMix 0.4：四成随便点一艘能走的（像真人不总是最优）
            if (AutoTest.Arg("-tideRunWin") != null) RUN_WIN = float.Parse(AutoTest.Arg("-tideRunWin"), System.Globalization.CultureInfo.InvariantCulture);
            var rng = new Rng(7);
            foreach (int lv in (AutoTest.Arg("-tideCombo") ?? "4,8,12").Split(',').Select(int.Parse))
            {
                float ld0 = Time.realtimeSinceStartup; PlayLevel(lv); float ld = Time.realtimeSinceStartup - ld0; TLog("关" + (lv + 1) + " 分波 " + (L.waves == null ? 0 : L.waves.Count + 1) + " 波，网格缩放 " + lay.gs.ToString("0.00") + "，读关 " + (ld * 1000).ToString("0") + "ms，船 " + L.boats.Count + " 人 " + L.total + "，礁石 " + L.ins.Count(v => v == 2) + " 船坞 " + L.houses.Count + " 鲨鱼 " + L.sharks.Count); await Fx.SleepKeep(1100); save.coins = 9999;
                float t0 = Time.realtimeSinceStartup; int sp0 = 0, ch0 = 0, rainbowRide = 0, lvMax = 0, fvLast = 0, v0 = 0, vShots = 0; vaultN = 0; bool spgShot = false; int td0 = 0;
                while (Time.realtimeSinceStartup - t0 < (pace > 600 ? 240 : 100))
                {
                    if (ModalOpen)
                    {
                        if (st.status == "win") { wins++; break; }
                        if (st.status == "lose") { loses++; if (st.berth.Count >= MAXB) break; Revive(true); await Fx.SleepKeep(300); continue; }
                        HideModal();
                    }
                    if (spMade != sp0) { sp0 = spMade; totalSp++; TLog("关" + (lv + 1) + " 连击×" + run + "人 → 特殊船 #" + spMade); if (shots < 12) { await Fx.SleepKeep(150); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "sp_" + (shots++).ToString("00") + ".png")); } }
                    { var sg = qGuys.FirstOrDefault(g => g.x != 0 && !g.vault && g.s <= SlotS(g.slot) + 0.01f); if (sg != null && !spgShot) { spgShot = true; await Fx.SleepKeep(600); var sp = cam.WorldToScreenPoint(sg.g.transform.position); TLog("功能人在屏幕 " + sp.ToString("F0") + " 排第 " + sg.slot); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "spg_" + (lv + 1) + ".png")); await Fx.SleepKeep(500); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "spg_" + (lv + 1) + "b.png")); } }   // 功能人的长相
                    if (vaultN != v0) { v0 = vaultN; TLog("关" + (lv + 1) + " 撑杆跳 #" + vaultN); if (vShots++ < 2) for (int i = 0; i < 9; i++) { ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "v" + vShots + "_" + i + ".png")); await Fx.SleepKeep(110); } }   // 撑杆跳（Pole.cs）：连拍
                    if (tideN != td0) { td0 = tideN; TLog("关" + (lv + 1) + " 涨潮 → 第 " + (L.wave + 1) + " 波，船 " + Rules.InLot(L, st).Count + " 艘，队伍 " + st.qi + "/" + st.q.Count); if (td0 <= 2) for (int i = 0; i < 20; i++) { ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "tide" + (lv + 1) + "_" + td0 + "_" + i + ".png")); await Fx.SleepKeep(130); } }   // 涨潮（TideWave.cs）：连拍
                    lvMax = Mathf.Max(lvMax, run);
                    if (FeverTier != fvLast) { if (FeverTier > fvLast) TLog("关" + (lv + 1) + " 热潮升到 " + FeverTier + " 档（连击 " + run + " 人，上船时长 ×" + BoardSpeed + "）"); fvLast = FeverTier; if (fvLast >= 2 && fvShots < 6) { await Fx.SleepKeep(700); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "fv" + fvLast + "_" + (fvShots++) + ".png")); } }
                    if (run / 10 != ch0) { ch0 = run / 10; maxChain = Mathf.Max(maxChain, run); if (run >= 10 && shots < 12) { await Fx.SleepKeep(200); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "c_" + (shots++).ToString("00") + ".png")); } }
                    for (int k = 0; k < st.berth.Count; k++) { int b = st.berth[k]; if (b >= 0 && L.boats[b].sp == 2) rainbowRide = Mathf.Max(rainbowRide, st.load[b]); }
                    if (st.status == "play") { var frs = Rules.InLot(L, st).Where(x => Rules.CanGo(L, st, x)).ToList(); int b = rng.Next() < mix && frs.Count > 0 ? frs[rng.Int(frs.Count)] : Rules.BestMove(L, st, null, 0); if (b >= 0 && boatV[b] != null && boatV[b].state == "lot") TapAt(cam.WorldToScreenPoint(boatV[b].g.transform.position + Vector3.up * 0.3f)); }
                    await Fx.SleepKeep(pace);
                }
                { var lot = Rules.InLot(L, st); TLog("  诊断 qi=" + st.qi + "/" + st.q.Count + " vq=" + vq + " pipeN=" + pipeN + " 泊位=[" + string.Join(",", st.berth.Select(b => b < 0 ? "空" : b + "(c" + L.boats[b].c + " sp" + L.boats[b].sp + " " + st.load[b] + "/" + L.boats[b].cap + " " + (boatV[b] != null ? boatV[b].state : "null") + ")")) + "] 港湾" + lot.Count + " 能走" + lot.Count(b => Rules.CanGo(L, st, b)) + " best=" + Rules.BestMove(L, st, null, 0) + " 队头色=" + (st.qi < st.q.Count ? st.q[st.qi] : -1)); }
                TLog("关" + (lv + 1) + " 沿通道兜底 " + aRuns + " 次，最远 " + aMax.ToString("0.0") + "m"); aRuns = 0; aMax = 0;
                TLog("关" + (lv + 1) + " 状态=" + st.status + " 最长连击=" + lvMax + "人 特殊船=" + spMade + " 彩虹船最多坐了" + rainbowRide + " 用时" + (Time.realtimeSinceStartup - t0).ToString("0") + "s");
            }
            TLog("总计 过关 " + wins + " 输(复活) " + loses + " 最高连击 " + maxChain + " 特殊船 " + totalSp);
        }

        async Task Shot(string dir, string name)
        {
            await Fx.SleepKeep(120);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name + ".png"));
            await Fx.SleepKeep(250);
        }
        void TLog(string s) { Debug.Log("[Test] " + s); }

        async Task WaitIdle(int maxMs = 15000)
        {
            float t0 = Fx.Now;
            while (Fx.Now - t0 < maxMs && (pipeN > 0 || intro || boatV.Any(u => u != null && u.state == "go"))) await Fx.SleepKeep(100);
        }

        // -tideSafe 时在屏幕上下画两条黑边，截图里能看出界面有没有躲开"刘海"和"手势条"
        void FakeNotch()
        {
            var a = AutoTest.Arg("-tideSafe"); if (a == null) return;
            var p = a.Split(','); float t = float.Parse(p[0]), b = float.Parse(p[1]);
            var go = new GameObject("notch", typeof(RectTransform)); var c = go.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 999;
            foreach (var (y, h) in new[] { (Screen.height - t, t), (0f, b) })
            {
                var im = UIKit.Img(go.transform, "bar", null, new Color(0, 0, 0, 0.85f)); var rt = im.rectTransform;
                rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = Vector2.zero; rt.anchoredPosition = new Vector2(0, y); rt.sizeDelta = new Vector2(Screen.width, h);
            }
        }

        public async Task RunTest(string dir)
        {
            FakeNotch();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-tideMeasure") >= 0) { HideTitle(); SetHud(true); await Measure(dir); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-tideStress") >= 0) { await Stress(dir); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-tideTitle") >= 0) { await TitleShots(dir); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-tideBalance") >= 0) { await Fx.SleepKeep(800); await BalanceShots(dir); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-tideGuide") >= 0) { await Fx.SleepKeep(800); await GuideShots(dir); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-tideDaily") >= 0) { await Fx.SleepKeep(800); await DailyShots(dir); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-tideHome") >= 0) { await Fx.SleepKeep(800); await HomeShots(dir); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-tideMech") >= 0) { HideTitle(); SetHud(true); await MechShots(dir); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-tideCombo") >= 0) { await ComboTest(dir); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-tideLock") >= 0) { if (AutoTest.Arg("-tideSlow") != null) Fx.SetSpeed(float.Parse(AutoTest.Arg("-tideSlow"), System.Globalization.CultureInfo.InvariantCulture)); HideTitle(); SetHud(true); await LockShots(dir); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-tideHeli") >= 0) { if (AutoTest.Arg("-tideSlow") != null) Fx.SetSpeed(float.Parse(AutoTest.Arg("-tideSlow"), System.Globalization.CultureInfo.InvariantCulture)); HideTitle(); SetHud(true); await HeliShots(dir); return; }
            TLog("音效放大后最响的峰值 " + Synth.sfxPeakMax.ToString("0.00"));
            TLog("屏幕 " + Screen.width + "x" + Screen.height + " 安全区 " + Screen.safeArea + " 语言 " + (Loc.En ? "en" : "zh"));
            // 0) 开始界面 → 地图（打到第 24 关的样子）→ 设置
            await Fx.SleepKeep(1400); await Shot(dir, "t_title");
            save = save.Reset(); save.top = 23; for (int i = 0; i < 23; i++) save.SetStar(i, 1 + i % 3); save.Persist();
            ShowMap(true); await Fx.SleepKeep(400); await Shot(dir, "t_map");
            save.top = 9; save.Persist(); ShowMap(true); await Fx.SleepKeep(400); await Shot(dir, "t_map2");   // 岛与岛交界：岛名、没解锁的灰罩
            foreach (int tp in new[] { 13, 33, 45, 53, 63, 73, 83, 93 }) { save.top = tp; save.Persist(); ShowMap(true); await Fx.SleepKeep(400); await Shot(dir, "t_map_" + (tp + 1)); }   // 第 2、4、5 座岛
            save.top = 23; save.Persist();
            ShowSettings(); await Fx.SleepKeep(400); await Shot(dir, "t_settings"); HideModal();
            Back(); await Fx.SleepKeep(300); TLog("地图按返回 → 开始界面 " + ui.title.gameObject.activeSelf);
            save = save.Reset(); save.seen.AddRange(new[] { "yacht", "raft", "carrier", "rock", "house", "mys", "storm", "weed", "lock", "shark" }); save.Persist();
            // 1) 几个代表性的关：开局画面
            foreach (int lv in new[] { 0, 2, 8, 12, 15, 29, 45, 49, 50, 60, 70, 80, 90, 99 })
            {
                PlayLevel(lv); await Fx.SleepKeep(1300);
                TLog("关" + (lv + 1) + " 船=" + L.boats.Count + " 人=" + L.total + " 镜头距离=" + camDist.ToString("0.0") + " rot=" + L.rot);
                await Shot(dir, "lv" + (lv + 1));
            }
            // 2) 第 1 关真玩到赢（用认真玩家的走法，带全部动画）
            PlayLevel(0); await Fx.SleepKeep(1200);
            await Shot(dir, "a_tutorial");
            int taps = 0;
            while (st.status == "play" && taps < 60)
            {
                int b = Rules.BestMove(L, st, null, 0); if (b < 0) break;
                var sp = cam.WorldToScreenPoint(boatV[b].g.transform.position + Vector3.up * 0.3f);
                TapAt(sp); taps++;                         // 走真实的点击路径（射线选船）
                await Fx.SleepKeep(450);
                if (taps == 3) await Shot(dir, "a_playing");
            }
            TLog("第1关 点了 " + taps + " 次，状态 " + st.status);
            await WaitIdle(); await Fx.SleepKeep(2600);
            await Shot(dir, "a_win");
            TLog("赢了之后 解锁到 " + save.top);
            Confetti(); await Fx.SleepKeep(300); HideModal(); LoadLevel(1); await Fx.SleepKeep(100);
            TLog("过关后马上换关：屏幕上残留的彩纸/飘字 = " + ui.fx.childCount + " 个（应为 0）"); if (ui.fx.childCount > 0) Debug.LogError("换关后彩纸没清掉");
            // 游戏里按返回 → 地图 → 再按返回回到游戏
            HideModal(); Back(); await Fx.SleepKeep(300); TLog("游戏里按返回 → 地图 " + ui.map.gameObject.activeSelf);
            Back(); await Fx.SleepKeep(200); TLog("再按返回 → 回游戏 " + (!OnScreen));
            // 3) 被挡的船：撞一下
            LoadLevel(5); await Fx.SleepKeep(1300);
            var blocked = Rules.InLot(L, st).FirstOrDefault(x => !Rules.PathOf(L, st, x).free);
            if (blocked >= 0) { TapAt(cam.WorldToScreenPoint(boatV[blocked].g.transform.position + Vector3.up * 0.3f)); await Fx.SleepKeep(150); await Shot(dir, "b_bump"); }
            // 4) 乱点到输 → 失败弹窗 → 复活（金币）
            LoadLevel(30); await Fx.SleepKeep(1300);
            save.coins = 500;
            var rnd = new Rng(5); int guard = 0;
            while (st.status == "play" && guard++ < 80)
            {
                var fr = Rules.InLot(L, st).Where(x => Rules.PathOf(L, st, x).free).ToList(); if (fr.Count == 0) break;
                int b = fr[rnd.Int(fr.Count)];
                TapAt(cam.WorldToScreenPoint(boatV[b].g.transform.position + Vector3.up * 0.3f));
                await Fx.SleepKeep(140);
            }
            await Fx.SleepKeep(1200); await Shot(dir, "c_berths");
            await WaitIdle(); await Fx.SleepKeep(1500);
            TLog("乱点结果 " + st.status + " 弹窗=" + ModalOpen);
            await Shot(dir, "c_lose");
            if (st.status == "lose") { Revive(true); await Fx.SleepKeep(900); TLog("复活后 " + st.status + " 泊位 " + st.berth.Count); await Shot(dir, "c_revive"); }
            // 5) 道具：吊船、理队
            LoadLevel(20); await Fx.SleepKeep(1300);
            var blk = Rules.InLot(L, st).FirstOrDefault(x => !Rules.PathOf(L, st, x).free);
            DoCrane(); TapAt(cam.WorldToScreenPoint(boatV[blk].g.transform.position + Vector3.up * 0.3f));
            await Fx.SleepKeep(600); await Shot(dir, "d_crane");
            await WaitIdle(); TLog("吊船后 泊位=" + string.Join(",", st.berth));
            // 5b) 风暴：第 4 关点一下开始计时 → 拨到快结束看天气 → 拨到 0 看失败弹窗 → 金币复活
            PlayLevel(3); await Fx.SleepKeep(1300);
            var fb = Rules.InLot(L, st).FirstOrDefault(x => Rules.PathOf(L, st, x).free);
            TapAt(cam.WorldToScreenPoint(boatV[fb].g.transform.position + Vector3.up * 0.3f)); await Fx.SleepKeep(400);
            TLog("风暴 " + stormMax + " 秒，点了之后剩 " + stormLeft.ToString("0.0"));
            stormLeft = stormMax * 0.12f; await Fx.SleepKeep(2600); await Shot(dir, "s_storm");
            stormLeft = 0.3f; await Fx.SleepKeep(1600);
            TLog("风暴走完 状态=" + st.status + " 风暴输=" + stormLose + " 弹窗=" + ModalOpen); await Shot(dir, "s_lose");
            save.coins = 500; Revive(true); await Fx.SleepKeep(600);
            TLog("风暴复活后 状态=" + st.status + " 剩 " + stormLeft.ToString("0") + " 秒");
            // 5c) 新机制弹窗
            ShowCard("storm"); await Fx.SleepKeep(500); await Shot(dir, "e_card_storm"); HideModal();
            ShowCard("house"); await Fx.SleepKeep(500); await Shot(dir, "e_card_house"); HideModal();
            // 5d) 弹出字 / 提示条
            PlayLevel(2); await Fx.SleepKeep(1400);
            Banner(Loc.T("困难关！"), "hard"); await Fx.SleepKeep(450); await Shot(dir, "w_banner");
            Praise(8); Toast(Loc.T("前面有船挡着，开不出去")); await Fx.SleepKeep(350); await Shot(dir, "w_praise_toast");
            // 6) 家园
            save.star = 5; ShowHome("game"); await Fx.SleepKeep(800); await Shot(dir, "e_home"); HomeBack(); await Fx.SleepKeep(300);
            for (int k = 0; k < 3; k++) { ShowBoosterTip(k, () => { }); await Fx.SleepKeep(500); await Shot(dir, "e_tip" + k); HideModal(); }
            // 7) 全部关读一遍，查报错
            for (int i = 0; i < LEVELS.Length; i++) { LoadLevel(i); await Fx.SleepKeep(80); }
            TLog(LEVELS.Length + " 关读完");
        }
    }
}
