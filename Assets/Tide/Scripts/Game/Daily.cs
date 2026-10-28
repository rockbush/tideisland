// 每日：7 天签到 + 每天 3 个任务（日子按手机本地时间的零点换，SaveData.Day）。
// 签到：连着签，奖励一天比一天多，第 7 天一个大礼包；断一天就从第 1 天重来。每天第一次到开始界面自动弹一次（打过第 1 关以后）。
// 任务：每天从任务池里抽 3 个不同类的（按日期定种子，同一天怎么进出都是这三个），做完点领取；三个都领了再送一份大奖。
// 入口：开始界面左边两个圆钮（有奖励可领就挂红点）、关卡里左上角菜单的"每日任务"。
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static Tide.Loc;

namespace Tide
{
    public partial class Game
    {
        struct Gift { public int coins, crane, sort; }
        static readonly Gift[] SIGN = {
            new Gift { coins = 50 }, new Gift { sort = 1 }, new Gift { coins = 100 }, new Gift { crane = 1 },
            new Gift { coins = 150 }, new Gift { sort = 2 }, new Gift { coins = 300, crane = 2, sort = 1 } };

        class DTask { public string id, group, kind, text, icon; public int need; public Gift gift; }
        // group 相同的一天只抽一个（比如"通关 2 关"和"通关 3 关"不会同时出现）
        static readonly DTask[] TASKS = {
            new DTask { id = "win2", group = "win", kind = "win", text = "通关 {0} 关", need = 2, icon = "ai_ring", gift = new Gift { coins = 60 } },
            new DTask { id = "win3", group = "win", kind = "win", text = "通关 {0} 关", need = 3, icon = "ai_ring", gift = new Gift { coins = 90 } },
            new DTask { id = "boat30", group = "boat", kind = "boat", text = "开走 {0} 艘船", need = 30, icon = "ai_dock", gift = new Gift { coins = 50 } },
            new DTask { id = "boat60", group = "boat", kind = "boat", text = "开走 {0} 艘船", need = 60, icon = "ai_dock", gift = new Gift { sort = 1 } },
            new DTask { id = "folk150", group = "folk", kind = "folk", text = "救出 {0} 位岛民", need = 150, icon = "card:folks", gift = new Gift { coins = 60 } },
            new DTask { id = "folk300", group = "folk", kind = "folk", text = "救出 {0} 位岛民", need = 300, icon = "card:folks", gift = new Gift { crane = 1 } },
            new DTask { id = "boost2", group = "boost", kind = "boost", text = "使用 {0} 次道具", need = 2, icon = "ai_heli", gift = new Gift { crane = 1 } },
            new DTask { id = "hard1", group = "hard", kind = "hard", text = "通关 {0} 个困难或超难关", need = 1, icon = "ai_storm", gift = new Gift { coins = 80 } },
            new DTask { id = "clean1", group = "clean", kind = "clean", text = "不用道具不复活通关 {0} 关", need = 1, icon = "starfish", gift = new Gift { coins = 80 } },
            new DTask { id = "chain5", group = "chain", kind = "chain", text = "一口气连着开走 {0} 艘船", need = 5, icon = "ai_dock", gift = new Gift { sort = 1 } },
            new DTask { id = "build1", group = "build", kind = "build", text = "在小岛上建 {0} 样东西", need = 1, icon = "hut", gift = new Gift { coins = 60 } } };
        static readonly Gift TASK_BONUS = new Gift { coins = 100, crane = 1 };

        RectTransform signDot, taskDot;
        bool signAutoShown;

        // ---------------------------------------------------------------- 签到
        bool SignedToday => save.signDay == SaveData.Day;
        int SignNext => save.signDay == SaveData.Day - 1 && save.signStep < SIGN.Length ? save.signStep : 0;   // 今天该领第几天（下标）
        int SignShown => SignedToday ? save.signStep : SignNext;                                                // 格子里有几个已经领过

        Gift Safe(Gift g) { var (c, cr, so) = SafeGift(g.coins, g.crane, g.sort); return new Gift { coins = c, crane = cr, sort = so }; }   // 没解锁的道具换金币（Guide.SafeGift）
        string GiftText(Gift g, int mul = 1)
        {
            g = Safe(g);
            var l = new List<string>();
            if (g.coins > 0) l.Add(F("金币 +{0}", g.coins * mul));
            if (g.crane > 0) l.Add(F("直升机 +{0}", g.crane * mul));
            if (g.sort > 0) l.Add(F("喇叭 +{0}", g.sort * mul));
            return string.Join(En ? ", " : "　", l);
        }
        void Give(Gift g, int mul = 1) { g = Safe(g); save.coins += g.coins * mul; save.crane += g.crane * mul; save.sort += g.sort * mul; }
        string GiftIcon(Gift g) => (g = Safe(g)).coins > 0 && (g.crane > 0 || g.sort > 0) ? "card:chest" : g.crane > 0 ? "ai_heli" : g.sort > 0 ? "ai_horn" : "ai_coin";

        void ShowSign(bool pop = true)
        {
            if (pop) Analytics.Track("screen", "name", "sign", "step", SignShown);
            Modal(true, pop);
            ModalTitle(T("每日签到"), "sign");
            MText(T("连着签到，奖励一天比一天多"), 13, UIKit.Hex("7a5a3a"));
            // 7 个格子：上面一排 4 个，下面 3 个（第 7 天占两格宽）
            const float CW = 64, CH = 86, G = 6;
            var grid = UIKit.Rect("grid", boxContent); grid.gameObject.AddComponent<LayoutElement>().preferredHeight = CH * 2 + G;
            int shown = SignShown, today = SignedToday ? -1 : SignNext;
            for (int i = 0; i < SIGN.Length; i++)
            {
                int row = i < 4 ? 0 : 1, col = i < 4 ? i : i - 4;
                float w = i == 6 ? CW * 2 + G : CW;
                float x = -(CW * 4 + G * 3) / 2 + col * (CW + G) + w / 2;
                var c = UIKit.Rect("d" + i, grid).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x, -row * (CH + G)), new Vector2(w, CH));
                bool got = i < shown, isToday = i == today;
                UIKit.Panel(c, "p", got ? UIKit.Hex("e3d6b8") : isToday ? UIKit.Hex("ffe08a") : UIKit.Hex("fff8e6"), 2, isToday ? 3 : 2).Stretch();
                UIKit.Txt(c, "day", F("第 {0} 天", i + 1), 12, UIKit.Hex("7a5a3a")).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -3), new Vector2(w, 18));
                var g = Safe(SIGN[i]);
                DecoImg(c, GiftIcon(g), new Vector2(0.5f, 0.5f), new Vector2(0, 2), i == 6 ? 50 : 40);
                string amt = g.coins > 0 && g.crane + g.sort == 0 ? "×" + g.coins : g.crane > 0 && g.coins == 0 ? "×" + g.crane : g.sort > 0 && g.coins == 0 ? "×" + g.sort : T("大礼包");
                var at = UIKit.Txt(c, "amt", amt, 14, UIKit.Hex("6b3d14")); at.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 3), new Vector2(w, 20));
                if (got)
                {
                    SetAlpha(c, 0.75f);
                    var tag = UIKit.Rect("got", c).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(52, 24));
                    tag.localRotation = Quaternion.Euler(0, 0, 12);
                    if (Skin.Has("btn_green")) Skin.Img(tag, "btn_green").rectTransform.Stretch(); else UIKit.Panel(tag, "p", UIKit.Green, 0, 2).Stretch();
                    UIKit.Txt(tag, "t", T("已领"), 13, Color.white).rectTransform.Stretch(0, 1, 0, 0);
                }
                if (isToday) { var cc = c; Fx.Run(() => Fx.Tween(900, p => { if (cc) cc.localScale = Vector3.one * (1 + 0.06f * Mathf.Sin(p * Mathf.PI * 2)); })); }
            }
            if (!SignedToday)
            {
                MText(F("今天：<b>{0}</b>", GiftText(SIGN[SignNext])), 15, UIKit.Hex("2f6b2a"));
                MButton(T("领取"), "3fb950", () => SignClaim(1));
                MButton(T("看广告领双倍"), "8a5cff", () => { sfx.Play("click"); WatchAd("sign_double", () => SignClaim(2)); }, true, "ffffff", 16, 44);
            }
            else
            {
                int nx = save.signStep >= SIGN.Length ? 0 : save.signStep;
                MText(F("明天再来领第 {0} 天：{1}", nx + 1, GiftText(SIGN[nx])), 14, UIKit.Hex("2f6b2a"));
                MText(T("断一天就要从第 1 天重新开始哦"), 12, UIKit.Hex("a08a6a"));
                MButton(T("关闭"), "3fb950", () => { sfx.Play("click"); HideModal(); }, true, "ffffff", 17, 46);
            }
            FinishModal();
        }

        void SignClaim(int mul)
        {
            if (SignedToday) return;
            int i = SignNext; var g = SIGN[i];
            Give(g, mul); save.signStep = i + 1; save.signDay = SaveData.Day; save.Persist();
            Analytics.Track("sign", "day", i + 1, "double", mul > 1);
            sfx.Play("coin"); sfx.Play(i == 6 ? "fanfare" : "reveal"); Haptics.Play(Haptics.Kind.Success);
            if (i == 6) Confetti();
            Toast(F("签到成功：{0}", GiftText(g, mul)));
            UpdateHUD(); UpdateDailyDots();
            ShowSign(false);
        }

        // 开始界面：今天还没签就自动弹一次（第 1 关都没打过的新玩家先别弹，先让他玩）
        void MaybeAutoSign()
        {
            if (signAutoShown || SignedToday || save.top < FEAT_DAILY || AutoTest.Wanted) return;
            signAutoShown = true;
            Fx.Run(async () => { await Fx.SleepKeep(700); if (ui.title.gameObject.activeSelf && !ModalOpen) ShowSign(); });
        }

        // ---------------------------------------------------------------- 任务
        // 今天的三个任务（换了一天就重抽）
        void TaskEnsure()
        {
            if (save.taskDay == SaveData.Day && save.taskId.Count == 3) return;
            var rnd = new System.Random((int)(SaveData.Day * 7919 % int.MaxValue));
            var pool = TASKS.Where(t => !(t.kind == "hard" && save.top < 5) && !(t.kind == "chain" && save.top < 3) && !(t.kind == "build" && CurArea >= HOME.areas.Length)).ToList();
            // 洗牌（手写，不用 List.Sort：不同运行时排出来不一样）
            for (int i = pool.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); var x = pool[i]; pool[i] = pool[j]; pool[j] = x; }
            var pick = new List<DTask>();
            foreach (var t in pool) if (pick.Count < 3 && pick.All(p => p.group != t.group)) pick.Add(t);
            save.taskDay = SaveData.Day; save.taskId = pick.Select(t => t.id).ToList();
            save.taskProg = new List<int> { 0, 0, 0 }; save.taskGot = new List<int> { 0, 0, 0 }; save.taskBonus = false;
            save.Persist();
        }
        DTask TaskOf(int i) => TASKS.First(t => t.id == save.taskId[i]);
        string TaskText(DTask t) => F(t.text, t.need);   // F 自己会翻译
        bool TaskDone(int i) => save.taskProg[i] >= TaskOf(i).need;
        int TasksDone { get { TaskEnsure(); int n = 0; for (int i = 0; i < 3; i++) if (TaskDone(i)) n++; return n; } }
        bool TaskClaimable { get { TaskEnsure(); for (int i = 0; i < 3; i++) if (TaskDone(i) && save.taskGot[i] == 0) return true; return save.taskGot.All(x => x == 1) && !save.taskBonus; } }

        // 进度：kind 是事件种类；chain 是"最多连着几艘"，取最大值，其他是累加
        void TaskAdd(string kind, int n = 1)
        {
            if (demo || HOME == null || save.top < FEAT_DAILY) return;     // 每日任务还没出现就不记（不然会冒出"任务完成"的提示）
            TaskEnsure();
            bool changed = false;
            for (int i = 0; i < 3; i++)
            {
                var t = TaskOf(i); if (t.kind != kind || save.taskGot[i] == 1) continue;
                int before = save.taskProg[i];
                save.taskProg[i] = Mathf.Min(t.need, kind == "chain" ? Mathf.Max(before, n) : before + n);
                if (save.taskProg[i] != before) changed = true;
                if (before < t.need && save.taskProg[i] >= t.need)
                {
                    Analytics.Track("task_done", "id", t.id, "lv", levelIdx + 1);
                    Toast(F("每日任务完成：{0}", TaskText(t))); sfx.Play("reveal");
                }
            }
            if (changed) { save.Persist(); UpdateDailyDots(); }
        }

        void ShowTasks(bool pop = true)
        {
            TaskEnsure();
            if (pop) Analytics.Track("screen", "name", "tasks", "done", TasksDone);
            Modal(true, pop);
            ModalTitle(T("每日任务"), "tasks");
            var left = DateTime.Today.AddDays(1) - DateTime.Now;
            MText(F("{0} 小时 {1} 分后换新任务", (int)left.TotalHours, left.Minutes), 12, UIKit.Hex("a08a6a"));
            for (int i = 0; i < 3; i++)
            {
                var t = TaskOf(i); int prog = save.taskProg[i]; bool done = prog >= t.need, got = save.taskGot[i] == 1;
                var row = UIKit.Rect("task" + i, boxContent); row.gameObject.AddComponent<LayoutElement>().preferredHeight = 70;
                UIKit.Panel(row, "p", got ? UIKit.Hex("e3d6b8") : done ? UIKit.Hex("e6f7d2") : UIKit.Hex("fff8e6"), 2, 2).Stretch();
                if (Deco(t.icon)) DecoImg(row, t.icon, new Vector2(0, 0.5f), new Vector2(32, 0), 50);
                var d = UIKit.Txt(row, "d", TaskText(t), 14, UIKit.Ink, TextAnchor.MiddleLeft); d.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(62, -8), new Vector2(150, 20));   // 一行放不下就缩字，别折到进度条上
                d.resizeTextForBestFit = true; d.resizeTextMinSize = 9; d.resizeTextMaxSize = 14; d.horizontalOverflow = HorizontalWrapMode.Wrap; d.verticalOverflow = VerticalWrapMode.Truncate;   // 竖向截断，自动缩字才会生效
                // 进度条 + 奖励
                var bar = UIKit.Rect("bar", row).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(62, 22), new Vector2(110, 16));
                if (Skin.Has("bar_track")) Skin.Img(bar, "bar_track").rectTransform.Stretch(); else UIKit.Panel(bar, "t", UIKit.Hex("e3d6b8"), 0, 2).Stretch();
                if (prog > 0)
                {
                    var f = Skin.Has("bar_green") ? Skin.Img(bar, "bar_green") : UIKit.Img(bar, "f", UIKit.Round, UIKit.Green);
                    var fr = f.rectTransform; fr.anchorMin = new Vector2(0, 0); fr.anchorMax = new Vector2(0, 1); fr.pivot = new Vector2(0, 0.5f);
                    fr.offsetMin = new Vector2(3, 3); fr.offsetMax = new Vector2(3, -3); fr.sizeDelta = new Vector2(104 * Mathf.Min(1, prog / (float)t.need), -6);
                }
                var pt = UIKit.Txt(bar, "n", prog + "/" + t.need, 11, Color.white); pt.rectTransform.Stretch(0, 1, 0, 0); Styled(pt, UIKit.Hex("3b2a1a"), 1.2f, 0.8f);
                UIKit.Txt(row, "g", GiftText(t.gift), 11, UIKit.Hex("2f6b2a"), TextAnchor.MiddleLeft).rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(62, 4), new Vector2(150, 16));
                // 右边：领取 / 已领
                if (got) UIKit.Txt(row, "s", T("已领"), 14, UIKit.Hex("8a8174")).rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(64, 30));
                else if (done)
                {
                    int ii = i;
                    var b = SkinBtn(row, "3fb950", T("领取"), 15, () => TaskClaim(ii), out _);
                    b.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-6, 0), new Vector2(76, 42));
                    var bb = b; Fx.Run(() => Fx.Tween(900, p => { if (bb) bb.localScale = Vector3.one * (1 + 0.06f * Mathf.Sin(p * Mathf.PI * 2)); }));
                }
            }
            // 三个都领完的大奖
            bool all = save.taskGot.All(x => x == 1);
            var br = UIKit.Rect("bonus", boxContent); br.gameObject.AddComponent<LayoutElement>().preferredHeight = 62;
            UIKit.Panel(br, "p", save.taskBonus ? UIKit.Hex("e3d6b8") : UIKit.Hex("ffe08a"), 2, 2).Stretch();
            DecoImg(br, "card:chest", new Vector2(0, 0.5f), new Vector2(32, 0), 50);
            var bt = UIKit.Txt(br, "t", T("三个都完成再送") + "\n<size=12>" + GiftText(TASK_BONUS) + "</size>", 14, UIKit.Hex("6b3d14"), TextAnchor.MiddleLeft);
            bt.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(62, 0), new Vector2(160, 54));
            if (save.taskBonus) UIKit.Txt(br, "s", T("已领"), 14, UIKit.Hex("8a8174")).rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(64, 30));
            else if (all)
            {
                var b = SkinBtn(br, "ffb81f", T("领取"), 15, TaskBonusClaim, out _);
                b.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-6, 0), new Vector2(76, 42));
            }
            else UIKit.Txt(br, "s", save.taskGot.Count(x => x == 1) + "/3", 16, UIKit.Hex("8a8174")).rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(64, 30));
            MButton(T("关闭"), "3fb950", () => { sfx.Play("click"); HideModal(); }, true, "ffffff", 17, 46);
            FinishModal();
        }

        void TaskClaim(int i)
        {
            if (!TaskDone(i) || save.taskGot[i] == 1) return;
            var t = TaskOf(i); Give(t.gift); save.taskGot[i] = 1; save.Persist();
            Analytics.Track("task_claim", "id", t.id);
            sfx.Play("coin"); Haptics.Play(Haptics.Kind.Light);
            Toast(GiftText(t.gift));
            UpdateHUD(); UpdateDailyDots(); ShowTasks(false);
        }
        void TaskBonusClaim()
        {
            if (save.taskBonus || !save.taskGot.All(x => x == 1)) return;
            Give(TASK_BONUS); save.taskBonus = true; save.Persist();
            Analytics.Track("task_bonus");
            sfx.Play("fanfare"); Confetti(); Haptics.Play(Haptics.Kind.Success);
            Toast(GiftText(TASK_BONUS));
            UpdateHUD(); UpdateDailyDots(); ShowTasks(false);
        }

        // ---------------------------------------------------------------- 开始界面的两个圆钮
        void DailyButtons(RectTransform S)
        {
            signDot = SideBtn(S, "card:calendar", T("签到"), new Vector2(46, 70), () => { sfx.Play("click"); ShowSign(); });
            taskDot = SideBtn(S, "card:tasks", T("任务"), new Vector2(46, -40), () => { sfx.Play("click"); ShowTasks(); });
            titleSignBtn = (RectTransform)signDot.parent; titleTaskBtn = (RectTransform)taskDot.parent;
            UpdateDailyDots();
        }
        RectTransform SideBtn(RectTransform S, string icon, string label, Vector2 pos, Action fn)
        {
            RectTransform b;
            if (Skin.Has("disc_booster")) { b = Skin.Img(S, "disc_booster").rectTransform; b.Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(74, 74)); }
            else b = UIKit.Disc(S, "sb", UIKit.Cream, 5).Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(66, 66));
            if (Deco(icon)) DecoImg(b, icon, new Vector2(0.5f, 0.5f), new Vector2(0, 4), 56);
            var l = UIKit.Txt(b, "l", label, 14, UIKit.Hex("fff3d6")); l.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(90, 22));
            Styled(l, UIKit.Hex("4a2a12"), 1.6f, 1.2f);
            var dot = UIKit.Rect("dot", b).Place(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-8, -8), new Vector2(26, 26));
            if (Skin.Has("badge_red")) Skin.Img(dot, "badge_red").rectTransform.Stretch(); else UIKit.Img(dot, "d", UIKit.Circle, UIKit.Hex("e8402f")).rectTransform.Stretch();
            UIKit.Txt(dot, "t", "!", 16, Color.white).rectTransform.Stretch(0, 1, 0, 0);
            UIKit.Btn(b, fn);
            return dot;
        }
        void UpdateDailyDots()
        {
            if (signDot) signDot.gameObject.SetActive(!SignedToday);
            if (taskDot) taskDot.gameObject.SetActive(TaskClaimable);
        }
    }
}
