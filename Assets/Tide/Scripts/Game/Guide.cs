// 新手引导：屏幕压暗、只把要看 / 要点的地方挖亮（一艘船、队头的人、一个按钮），卡通手指指着，旁边鹦鹉船长说一两句话。
// 每一步要么"点哪儿都行，看懂了就继续"（tap），要么"必须去点亮的那个东西"（done 条件成立才走）。步骤排成队一个个来，看过就记进存档不再出。
// 现在用到的地方：第 1 关（队头 → 点船 → 坐满开走）、第 2 关（被挡住的船）、第一次快停满（码头 → 锁）、道具解锁（第 5/7/9 关）。
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
        class GStep
        {
            public string id, text;
            public Func<Rect?> target;       // 屏幕像素里的一块（null = 整屏都不挖，只说话）
            public Func<bool> ready;         // 什么时候可以开始（默认：关卡在玩、没有弹窗、开局动画放完）
            public Func<bool> done;          // 什么时候算完成；null = 点一下任意地方就继续
            public Action onShow;
            public bool tapped;
            public string where = "level";   // 在哪个界面出：level 关卡里 / home 小岛 / title 开始界面
        }
        readonly List<GStep> guideQ = new List<GStep>();
        GStep guideCur; int guideTok;
        RectTransform gRoot, gHand, gBubble, gRing; RectTransform[] gDim = new RectTransform[4]; Image gCatch; Text gText;
        bool GuideOn => guideCur != null;

        void GuideClear()
        {
            guideQ.RemoveAll(x => x.where == "level"); if (guideCur != null && guideCur.where == "level") guideCur = null;
            if (guideCur != null) return;
            if (gRoot) gRoot.gameObject.SetActive(false);
        }

        void Guide(string id, string text, Func<Rect?> target, Func<bool> done = null, Func<bool> ready = null, Action onShow = null, string where = "level")
        {
            if (guideQ.Any(x => x.id == id) || (guideCur != null && guideCur.id == id)) return;     // 同一步不重复排
            guideQ.Add(new GStep { id = id, text = text, target = target, done = done, ready = ready, onShow = onShow, where = where });
        }
        // 这一步所在的界面现在能不能出引导
        bool GuideScreenOk(GStep g)
        {
            if (ModalOpen || menuOpen || (ui.ad && ui.ad.gameObject.activeSelf)) return false;
            if (g.where == "home") return homeOpen;
            if (g.where == "title") return ui.title.gameObject.activeSelf && !homeOpen;
            return !OnScreen;
        }

        void GuideBuild()
        {
            gRoot = UIKit.Rect("guide", ui.canvas.transform).Stretch();
            gRoot.SetSiblingIndex(ui.modal.GetSiblingIndex());          // 在弹窗下面：弹窗出来时引导自己等着
            for (int k = 0; k < 4; k++)
            {
                var im = UIKit.Img(gRoot, "dim" + k, null, new Color(0.04f, 0.1f, 0.18f, 0.62f), true);
                gDim[k] = im.rectTransform; gDim[k].anchorMin = gDim[k].anchorMax = new Vector2(0.5f, 0.5f); gDim[k].pivot = new Vector2(0.5f, 0.5f);
                UIKit.Btn(gDim[k], GuideTapOutside); Destroy(gDim[k].GetComponent<UIKit.Press>());   // 暗板不要"按下缩一下"：一缩就和洞之间漏出一条没压暗的竖缝（2026-10-23 用户截图）
            }
            // "点哪儿都行"那一步：挖亮的那块也盖一层透明的，免得点穿到船上
            gCatch = UIKit.Img(gRoot, "catch", null, new Color(0, 0, 0, 0), true);
            gCatch.rectTransform.anchorMin = gCatch.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            UIKit.Btn(gCatch.rectTransform, GuideTapOutside); Destroy(gCatch.GetComponent<UIKit.Press>());
            gRing = UIKit.Img(gRoot, "ring", UIKit.Ring, new Color(1, 1, 1, 0.9f)).rectTransform; gRing.anchorMin = gRing.anchorMax = new Vector2(0.5f, 0.5f);
            gHand = UIKit.Rect("hand", gRoot); gHand.anchorMin = gHand.anchorMax = new Vector2(0.5f, 0.5f); gHand.sizeDelta = new Vector2(64, 64);
            if (Skin.Has("hand_point")) Skin.Img(gHand, "hand_point").rectTransform.Stretch(); else UIKit.Txt(gHand, "t", "▼", 40, UIKit.Hex("ffd21f")).rectTransform.Stretch();
            gBubble = UIKit.Rect("bubble", gRoot); gBubble.anchorMin = gBubble.anchorMax = new Vector2(0.5f, 0.5f); gBubble.sizeDelta = new Vector2(330, 76);
            if (Skin.Has("bubble") && Skin.Has("mascot"))
            {
                var bub = Skin.Img(gBubble, "bubble"); bub.preserveAspect = false; bub.rectTransform.Stretch(62, 0, 0, 0);
                Skin.Img(gBubble, "mascot").rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(-6, 2), new Vector2(78, 78));
            }
            else UIKit.Img(gBubble, "bg", UIKit.Round, UIKit.Hex("fff8e6")).rectTransform.Stretch();
            gText = UIKit.Txt(gBubble, "t", "", 15, UIKit.Hex("5a3410")); gText.rectTransform.Stretch(84, 6, 14, 6); gText.lineSpacing = 1.08f;
            gRoot.gameObject.SetActive(false);
        }

        void GuideTapOutside()
        {
            if (guideCur == null) return;
            if (guideCur.done == null) { guideCur.tapped = true; sfx.Play("click"); return; }
            Bump(gHand);    // 要点亮着的那个，点别处就让手指跳一下
        }

        bool GuideReadyDefault() => st != null && st.status == "play" && !intro && !ModalOpen && !OnScreen && !menuOpen && !demo && !(ui.ad && ui.ad.gameObject.activeSelf);

        // LateUpdate 里调（标签、手指都摆好之后）
        void GuideTick()
        {
            if (!gRoot) { if (ui.canvas) GuideBuild(); else return; }
            if (guideTok != tok)
            {
                // 换关了：关卡里的步骤作废；开始界面、小岛上的留着（开局时开始界面后面也会读一关）
                guideTok = tok; guideQ.RemoveAll(x => x.where == "level");
                if (guideCur != null && guideCur.where == "level") { guideCur = null; if (gRoot) gRoot.gameObject.SetActive(false); }
            }
            if (guideCur == null)
            {
                if (guideQ.Count == 0) return;
                var nx = guideQ[0];
                if (!GuideScreenOk(nx) || !(nx.ready ?? (nx.where == "level" ? (Func<bool>)GuideReadyDefault : () => true))()) return;
                guideQ.RemoveAt(0); guideCur = nx;
                if (!save.seen.Contains(nx.id)) { save.seen.Add(nx.id); save.Persist(); }     // 出现过就算看过：中途退出也不再重来一遍
                nx.onShow?.Invoke();
                gText.text = T(nx.text);
                gRoot.gameObject.SetActive(true); gRoot.SetSiblingIndex(ui.modal.GetSiblingIndex());
                HideHand();
                Analytics.Track("tutorial", "id", nx.id, "lv", levelIdx + 1);
            }
            var g = guideCur;
            bool fin = g.done != null ? g.done() : g.tapped;
            if (fin) { if (!save.seen.Contains(g.id)) { save.seen.Add(g.id); save.Persist(); } guideCur = null; gRoot.gameObject.SetActive(false); return; }
            // 弹窗、菜单、广告出来时先藏起来（步骤还在）
            bool vis = GuideScreenOk(g);
            if (gRoot.gameObject.activeSelf != vis) gRoot.gameObject.SetActive(vis);
            if (!vis) return;
            var cr = ((RectTransform)ui.canvas.transform).rect; float W = cr.width, H = cr.height;
            Rect? sr = g.target?.Invoke();
            Rect hole;
            if (sr.HasValue)
            {
                var a = ToCanvas(new Vector3(sr.Value.xMin, sr.Value.yMin)); var b = ToCanvas(new Vector3(sr.Value.xMax, sr.Value.yMax));
                hole = Rect.MinMaxRect(Mathf.Min(a.x, b.x) - 8, Mathf.Min(a.y, b.y) - 8, Mathf.Max(a.x, b.x) + 8, Mathf.Max(a.y, b.y) + 8);
                // 太小的目标（队头那个小人）至少挖 84×84，不然一眼看不到
                if (hole.width < 84) hole = Rect.MinMaxRect(hole.center.x - 42, hole.yMin, hole.center.x + 42, hole.yMax);
                if (hole.height < 84) hole = Rect.MinMaxRect(hole.xMin, hole.center.y - 42, hole.xMax, hole.center.y + 42);
            }
            else hole = new Rect(0, -H, 0, 0);       // 没有目标：整屏压暗
            // 四块暗板围住洞（canvas 中心为原点）
            void Box(RectTransform r, float x0, float y0, float x1, float y1) { r.anchoredPosition = new Vector2((x0 + x1) / 2, (y0 + y1) / 2); r.sizeDelta = new Vector2(Mathf.Max(0, x1 - x0), Mathf.Max(0, y1 - y0)); }
            float L0 = -W / 2 - 60, R0 = W / 2 + 60, B0 = -H / 2 - 60, T0 = H / 2 + 60;
            if (sr.HasValue)
            {
                Box(gDim[0], L0, B0, hole.xMin, T0); Box(gDim[1], hole.xMax, B0, R0, T0);
                Box(gDim[2], hole.xMin, hole.yMax, hole.xMax, T0); Box(gDim[3], hole.xMin, B0, hole.xMax, hole.yMin);
            }
            else { Box(gDim[0], L0, B0, R0, T0); for (int k = 1; k < 4; k++) Box(gDim[k], 0, 0, 0, 0); }
            gCatch.gameObject.SetActive(g.done == null && sr.HasValue);
            if (gCatch.gameObject.activeSelf) Box(gCatch.rectTransform, hole.xMin, hole.yMin, hole.xMax, hole.yMax);
            float t = Fx.Now / 1000f;
            gRing.gameObject.SetActive(sr.HasValue && Mathf.Max(hole.width, hole.height) < 220);   // 码头那种大块就不画圈了，圈会大得出屏
            if (gRing.gameObject.activeSelf) { float k2 = (t * 1.1f) % 1; gRing.anchoredPosition = hole.center; float d = Mathf.Max(hole.width, hole.height) * (0.9f + 0.4f * k2); gRing.sizeDelta = new Vector2(d, d); gRing.GetComponent<Image>().color = new Color(1, 1, 1, 0.9f * (1 - k2)); }
            // 手指：指着洞的上沿，上下点；没有目标就不出
            gHand.gameObject.SetActive(sr.HasValue);
            if (sr.HasValue) gHand.anchoredPosition = new Vector2(hole.center.x + 10, hole.yMax + 28 + Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2)) * 14);
            // 气泡：洞在下半屏就放洞的上面，在上半屏就放下面
            float bx = Mathf.Clamp(hole.center.x, -W / 2 + 175, W / 2 - 175);
            float by = !sr.HasValue ? 0 : hole.center.y < 0 ? hole.yMax + 120 : hole.yMin - 70;
            by = Mathf.Clamp(by, -H / 2 + 60, H / 2 - 150);
            gBubble.anchoredPosition = new Vector2(bx, by);
        }

        // ---- 目标：世界里的一些点 → 屏幕上框住它们的矩形 ----
        Rect? WorldRect(params Vector3[] pts)
        {
            if (pts == null || pts.Length == 0) return null;
            float x0 = 1e9f, y0 = 1e9f, x1 = -1e9f, y1 = -1e9f;
            foreach (var p in pts) { var s = cam.WorldToScreenPoint(p); if (s.z < 0) return null; x0 = Mathf.Min(x0, s.x); y0 = Mathf.Min(y0, s.y); x1 = Mathf.Max(x1, s.x); y1 = Mathf.Max(y1, s.y); }
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }
        Rect? BoatRect(int b)
        {
            if (b < 0 || b >= boatV.Length || boatV[b] == null || !boatV[b].g) return null;
            var rs = boatV[b].g.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return null;
            var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
            var c = bb.center; var e = bb.extents;
            return WorldRect(c + new Vector3(-e.x, 0, -e.z), c + new Vector3(e.x, 0, -e.z), c + new Vector3(-e.x, 0, e.z), c + new Vector3(e.x, e.y, e.z));
        }
        Rect? GuyRect(Guy g)
        {
            if (g == null || !g.g) return null;
            var p = g.g.transform.position;
            return WorldRect(p + new Vector3(-0.28f, 0, 0), p + new Vector3(0.28f, 0, 0), p + new Vector3(0, 0.7f, 0), p + new Vector3(0, 0, -0.2f));
        }
        Rect? DockRect() => WorldRect(W(BX(0) - BW / 2, lay.zP), W(BX(MAXB - 1) + BW / 2, lay.zP), W(BX(0) - BW / 2, lay.zP + DEEP), W(BX(MAXB - 1) + BW / 2, lay.zP + DEEP));
        static Rect? UIRect(RectTransform r)
        {
            if (!r || !r.gameObject.activeInHierarchy) return null;
            var c = new Vector3[4]; r.GetWorldCorners(c);     // 屏幕空间画布：世界坐标就是屏幕像素
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        // ================================================================== 关卡里的引导
        // LoadLevel 末尾调（开始界面后面摆着的关不调）
        static bool GuideOff => AutoTest.Wanted && AutoTest.Arg("-tideGuide") == null && Array.IndexOf(Environment.GetCommandLineArgs(), "-tideGuide") < 0;   // 别的自动测试不放引导，截图里不要遮罩
        int PickBoat(Func<int, bool> f) { foreach (int x in Rules.InLot(L, st)) if (boatV[x] != null && f(x)) return x; return -1; }

        void GuideLevel()
        {
            GuideClear(); guideTok = tok;      // LoadLevel 刚把 tok 加过：先对齐，不然 GuideTick 会把刚排好的步骤当成上一关的清掉
            if (demo || GuideOff) return;
            if (levelIdx == 0 && !save.seen.Contains("g_l1c"))
            {
                Guide("g_l1a", "举手的人是<b>队头</b>\n他要坐<b>同样颜色</b>的船", () => qGuys.Count > 0 ? GuyRect(qGuys[0]) : null);
                int tb = -1;
                Guide("g_l1b", "点这艘船\n它会顺着<b>箭头</b>开到码头", () => BoatRect(tb), () => st.taps > 0, null, () => tb = TutorBoat());
                Guide("g_l1c", "岛民会自己上船\n<b>坐满就开走</b>，下一个接着来", DockRect, null, () => GuideReadyDefault() && st.taps > 0 && pipeN == 0);
            }
            if (levelIdx == 1 && !save.seen.Contains("g_l2"))
            {
                int bb = -1;
                Guide("g_l2", "船头前面<b>有船挡着</b>就开不出去\n先把挡路的船开走", () => BoatRect(bb), null,
                    () => GuideReadyDefault() && (bb = PickBoat(x => !Rules.CanGo(L, st, x) && L.boats[x].mys == 0)) >= 0);
            }
            for (int k = 0; k < 3; k++) if (levelIdx == UNLOCK[k] && !save.seen.Contains("unlock_" + BOOST_ID[k])) UnlockGuide(k);
        }

        // 第一次只剩 1 个空泊位（UpdateHUD 里调）：先亮码头讲"停满就输"，再亮一把锁讲"可以多开"
        void GuideNearlyFull()
        {
            if (save.seen.Contains("g_full2") || levelIdx < 2 || demo || GuideOff || guideQ.Any(x => x.id == "g_full1") || (guideCur != null && guideCur.id.StartsWith("g_full"))) return;
            Guide("g_full1", "码头只剩 <b>1 个空位</b>了\n<b>停满就输</b>，先看队头要什么颜色", DockRect);
            int k = st.berth.Count;
            if (k < MAXB && lockV[k]) Guide("g_full2", "点<b>锁</b>能多开一个泊位\n前两个看广告，再往后花金币", () => lockV[k] ? WorldRect(lockV[k].transform.position + new Vector3(-0.5f, 0, -1.1f), lockV[k].transform.position + new Vector3(0.5f, 0.7f, 1.1f)) : null);
        }

        // ================================================================== 道具分关解锁
        static readonly int[] UNLOCK = { 4, 6, 8 };        // 直升机第 5 关、喇叭第 7 关、临时码头第 9 关（关卡下标）
        bool Unlocked(int k) => Mathf.Max(save.top, levelIdx) >= UNLOCK[k] || save.seen.Contains("unlock_" + BOOST_ID[k]);
        // 所有"送道具"的地方（签到、每日任务、星星宝箱、区域完工）都先过这里：还没解锁的道具换成金币（按商店价的一半），
        // 免得玩家拿到一个还用不了、也看不懂的东西（2026-10-23 用户发现的 bug）。家园每天领的直升机/喇叭在 PerkReady 里等解锁
        (int coins, int crane, int sort) SafeGift(int coins, int crane, int sort)
        {
            if (crane > 0 && !Unlocked(0)) { coins += crane * COST["crane"] / 2; crane = 0; }
            if (sort > 0 && !Unlocked(1)) { coins += sort * COST["sort"] / 2; sort = 0; }
            return (coins, crane, sort);
        }
        int boosterTapped = -1;

        void UnlockGuide(int k)
        {
            string id = BOOST_ID[k];
            // ① 新道具卡片（送免费次数）
            Guide("unlock_" + id + "_card", "", null, () => !ModalOpen, null, () =>
            {
                if (k == 0) save.crane += 2; else if (k == 1) save.sort += 2; else save.berthFree += 1;
                if (!save.seen.Contains("tip_" + id)) save.seen.Add("tip_" + id);      // 卡片讲过了，第一次点就不再弹说明
                save.Persist(); UpdateHUD();
                ShowUnlockCard(k);
            });
            // ② 手指指按钮，必须点
            Guide("unlock_" + id + "_btn", k == 0 ? "点<b>直升机</b>" : k == 1 ? "点<b>喇叭</b>，喊一声" : "点这里<b>多开一个泊位</b>", () => UIRect(ui.bb[k]),
                () => boosterTapped == k, null, () => boosterTapped = -1);
            // ③ 直升机还要再点一艘船
            if (k == 0)
            {
                int bb = -1;
                Guide("unlock_crane_boat", "再点<b>任意一艘船</b>\n被挡着的也能吊走", () => BoatRect(bb), () => !craneMode, () => craneMode && GuideReadyDefault(),
                    () => { bb = PickBoat(x => !Rules.CanGo(L, st, x)); if (bb < 0) bb = PickBoat(x => true); });
            }
            Guide("unlock_" + id, "", null, () => true);     // 最后一步：只为记下"这个道具讲过了"
        }

        void ShowUnlockCard(int k)
        {
            Analytics.Track("booster_unlock", "lv", levelIdx + 1, "id", BOOST_ID[k]);
            var tip = BOOST_TIP[k];
            Modal(false);
            ModalTitle(T("新道具！"));     // 不用 "new" 贴图字：那张写的是"新机制！"
            var fr = UIKit.Rect("pic", boxContent); fr.gameObject.AddComponent<LayoutElement>().preferredHeight = 150;
            var bg = UIKit.Img(fr, "bg", UIKit.Round, UIKit.Hex("cfeefa")); bg.rectTransform.anchorMin = bg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); bg.rectTransform.sizeDelta = new Vector2(220, 150);
            if (Deco(tip.pic)) { var im = DecoImg(fr, tip.pic, new Vector2(0.5f, 0.5f), Vector2.zero, 136); Fx.Run(() => Fx.Tween(600, p => { if (im) im.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1, Fx.BackOut(p)); })); }
            MText(T(tip.name), 28, UIKit.Brown);
            MText(T(tip.desc), 16);
            MText(k < 2 ? T("送你 <b>2 次</b>免费！") : T("送你 <b>1 次</b>免费！"), 16, UIKit.Hex("2f6b2a"));
            MButton(T("试试看"), "3fb950", () => { sfx.Play("click"); HideModal(); });
            FinishModal();
            sfx.Play("reveal"); Haptics.Play(Haptics.Kind.Success);
        }

        // 还没解锁的道具按钮：灰掉 + 一把锁 + "第 N 关"
        void BoosterLockView()
        {
            for (int k = 0; k < 3; k++)
            {
                var b = ui.bb[k]; if (!b) continue;
                bool on = Unlocked(k);
                var lk = b.Find("lockMark");
                if (on) { if (lk) { Destroy(lk.gameObject); ui.cntBg[k].transform.parent.gameObject.SetActive(true); if (k < 2) SetAlpha(b, 1); } continue; }
                SetAlpha(b, 0.55f);
                if (!lk)
                {
                    var r = UIKit.Rect("lockMark", b).Stretch();
                    if (Skin.Has("icon_lock")) Skin.Img(r, "icon_lock").rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(40, 40));
                    var t = UIKit.Txt(r, "t", F("第 {0} 关", UNLOCK[k] + 1), 13, Color.white); t.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 12), new Vector2(90, 22));
                    Styled(t, UIKit.Hex("3b2a1a"), 1.6f, 1f);
                }
                ui.cntBg[k].transform.parent.gameObject.SetActive(false);
            }
        }

        // ================================================================== 第二批：小岛、宝箱、每日礼物、连胜、开始界面的按钮分批出现
        const int FEAT_HOME = 1, FEAT_DAILY = 3;     // 打过几关以后，开始界面才出现"我的小岛" / "签到、任务"
        RectTransform titleHomeBtn, titleSignBtn, titleTaskBtn;
        bool homeBuilding;                           // 小岛上正在播建造动画（引导等它放完）

        // 进小岛（ShowHome 末尾调）：第一次能建东西时，指着最便宜的牌子 → 建完讲星星从哪来
        void GuideHome()
        {
            if (GuideOff || save.seen.Contains("g_home1") || !CanBuild) return;
            Spot sp = null;
            Guide("g_home1", "用<b>星星</b>把小岛建起来\n点这个牌子", () => sp != null && sp.mark ? UIRect(sp.mark) : null, () => sp == null || Built(sp.t.id), null,
                () => sp = spots.Where(s => s.mark != null && save.star >= s.t.cost).OrderBy(s => s.t.cost).FirstOrDefault(), "home");
            Guide("g_home2", "每关<b>第一次通关</b>都给星星\n困难关、超难关给得更多", () => UIRect(homePlay), null, () => !homeBuilding, null, "home");
        }

        // 小岛界面刷新时（HomeRefresh 末尾调）：第一个宝箱、第一份每日礼物
        void GuideHomeExtras()
        {
            if (GuideOff || !homeOpen) return;
            if (ChestReady > 0 && !save.seen.Contains("g_chest"))
                Guide("g_chest", "攒满 10 颗星\n<b>星星宝箱</b>可以开了！", () => UIRect(homeChest), () => ChestReady == 0 || ModalOpen, () => !homeBuilding, null, "home");
            if (homePerks.Count > 0 && !save.seen.Contains("g_perk"))
            {
                string pk = homePerks[0].perk;
                Guide("g_perk", "这里<b>每天</b>都能领一份免费道具\n点它领取", () => homePerks.Count > 0 && homePerks[0].r ? UIRect(homePerks[0].r) : null, () => !PerkReady(pk), () => !homeBuilding, null, "home");
            }
        }

        // 开始界面不放引导（用户要求）：签到、任务按钮打过第 3 关才出现，出现时不再手指带着点
        void GuideTitle() { }

        // 第一次拿到连胜奖励：弹一张卡说清楚（StreakGift 里调）
        void GuideStreak()
        {
            if (GuideOff || save.seen.Contains("g_streak")) return;
            float at = Fx.Now + 1900;      // 等开局的连胜横幅放完
            Guide("g_streak", "", null, () => !ModalOpen, () => GuideReadyDefault() && Fx.Now > at, () => ShowStreakCard());
        }
        void ShowStreakCard()
        {
            Modal();
            ModalTitle(T("连胜奖励"));
            var fr = UIKit.Rect("pic", boxContent); fr.gameObject.AddComponent<LayoutElement>().preferredHeight = 90;
            string[] ic = { "ai_horn", "ai_dock", "ai_heli" };
            for (int k = 0; k < 3; k++)
            {
                DecoImg(fr, ic[k], new Vector2(0.5f, 0.5f), new Vector2((k - 1) * 86, 10), 64);
                var t = UIKit.Txt(fr, "n" + k, F("连胜 {0}", k + 1), 13, UIKit.Hex("7a3dd6")); t.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((k - 1) * 86, -34), new Vector2(84, 20));
            }
            MText(T("连着赢，下一关<b>开局就送道具</b>\n赢得越多送得越多"), 15);
            MText(T("输了放弃、或者打到一半退出\n连胜就<b>清零</b>"), 14, UIKit.Hex("b5371f"));
            MButton(T("知道了"), "3fb950", () => { sfx.Play("click"); HideModal(); });
            FinishModal();
        }

        // 弹窗里的按钮上挂一只一点一点的手（弹窗盖在引导层上面，压暗挖洞那套在弹窗里用不了）
        void PointAt(RectTransform btn)
        {
            var h = UIKit.Rect("hand", btn).Place(new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-24, -20), new Vector2(56, 56));
            if (Skin.Has("hand_point")) Skin.Img(h, "hand_point").rectTransform.Stretch(); else UIKit.Txt(h, "t", "▼", 36, UIKit.Hex("ffd21f")).rectTransform.Stretch();
            h.localRotation = Quaternion.Euler(0, 0, 28);
            h.gameObject.AddComponent<UIBob>();
        }
    }

    // 一上一下地点（PointAt 的手）
    public class UIBob : MonoBehaviour
    {
        RectTransform r; Vector2 p0;
        void Start() { r = (RectTransform)transform; p0 = r.anchoredPosition; }
        void Update() { if (r) r.anchoredPosition = p0 + new Vector2(0, Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI * 2)) * 10); }
    }
}
