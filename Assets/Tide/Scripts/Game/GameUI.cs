// 界面：顶部栏、队伍进度、道具按钮、弹窗（胜利/失败/新机制卡片/营地）、提示字、广告占位、跟着 3D 物体走的小标签。
// 开始界面、关卡地图、设置在 Screens.cs。所有文字都过 Loc.T（中文写在代码里，英文在 Loc.cs 查表）。
// 网页版里的 emoji 在 Unity 默认字体里显示不出来，这里先用文字和色块代替，以后换成美术图标。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Tide.Loc;

namespace Tide
{
    public partial class Game
    {
        static readonly Dictionary<int, string> TUT = new Dictionary<int, string> {
            // 第 1~3 关原来是这里的一句话（鹦鹉气泡一直挂在顶上），现在换成手把手的引导（Guide.cs），这里留空
        };
        static readonly Dictionary<string, string[]> CARD = new Dictionary<string, string[]> {
            { "yacht", new[] { "游艇", "游艇坐 <b>6 个人</b>，快艇坐 4 个。" } },
            { "raft", new[] { "木筏", "小木筏只坐 <b>2 个人</b>\n一下就坐满了\n它是方的，<b>四面</b>都能朝" } },
            { "carrier", new[] { "航母", "航母最长，一次能装 <b>10 个人</b>！" } },
            { "rock", new[] { "礁石", "礁石挡路，永远不会动。" } },
            { "house", new[] { "船坞", "船坞里还停着船\n<b>门口的船开走了</b>\n下一艘才出来\n屋顶数字 = 里面还剩几艘" } },
            { "mys", new[] { "神秘白船", "白色的船看不出是什么颜色：<b>船头前面的路一通</b>，就露出颜色。" } },
            { "weed", new[] { "海藻缠船", "船被<b>海藻缠住</b>了，先开别的船\n数字 = 还要先开走几艘\n到 0 海藻就松开" } },
            { "lock", new[] { "钥匙和锁链", "被<b>锁链</b>锁住的船开不走\n先把<b>挂着同色钥匙</b>的船开走\n锁就打开了" } },
            { "shark", new[] { "鲨鱼", "鲨鱼在港湾边上<b>来回游</b>\n船正撞上它，会被吓回来\n<b>看准时机</b>再点！" } },
            { "pole", new[] { "撑杆跳", "扛着<b>长杆</b>、一闪一闪的小人\n一上船，队伍里<b>和他同色</b>的人\n都撑杆跳上去，把空座补满！" } },
            { "storm", new[] { "风暴要来了", "上面的<b>风暴条走完就输</b>\n开走一艘坐满的船\n风暴就<b>往后推一点</b>\n手要快，也要准！" } } };
        // 每关第一次通关得一位，100 关 100 位（第 51~100 位是 10-26 扩关时加的）
        static readonly string[] COLLECT = { "农夫", "厨师", "医生", "画家", "消防员", "老师", "科学家", "机长", "歌手", "警察", "侦探",
            "宇航员", "魔法师", "小仙子", "美人鱼", "精灵", "超级英雄", "忍者", "公主", "圣诞老人", "小天使", "牛仔", "小狗", "小猫", "兔子", "狐狸",
            "熊猫", "考拉", "小老虎", "狮子", "青蛙", "猴子", "企鹅", "鹦鹉", "海龟", "海豚", "鲸鱼", "螃蟹", "章鱼", "海豹", "水獭", "火烈鸟", "蝴蝶",
            "刺猬", "树懒", "松鼠", "猫头鹰", "恐龙", "小龙", "独角兽",
            "渔夫", "船长", "潜水员", "救生员", "冲浪手", "邮递员", "面包师", "木匠", "园丁", "理发师",
            "摄影师", "小丑", "舞蹈家", "钢琴家", "探险家", "考古学家", "天文学家", "发明家", "机器人", "海盗",
            "骑士", "国王", "王后", "女巫", "雪人", "稻草人", "外星人", "大象", "长颈鹿", "斑马",
            "河马", "袋鼠", "北极熊", "浣熊", "小鹿", "羊驼", "小鸭", "天鹅", "孔雀", "啄木鸟",
            "蜜蜂", "瓢虫", "蜗牛", "海马", "水母", "海星", "寄居蟹", "小丑鱼", "海狮", "锤头鲨" };
        static readonly Dictionary<int, string> PRAISE = new Dictionary<int, string> { { 3, "不错！" }, { 5, "漂亮！" }, { 8, "太棒了！" }, { 12, "停不下来！" }, { 16, "神了！" } };

        class UIRefs
        {
            public Canvas canvas; public CanvasScaler scaler; public float scale = 1;
            public RectTransform bannerBox, praiseBox, fx, safe, hud, labels, top, queue, tutBox, boost, menu, modal, box, toasts, ad, hand, coinPill, gift, qbar, title, map, home;
            public Text lvName, coins, qlbl, tut, banner, praise, adN, handT;
            public Image qfill, vig, handRing, bannerBg, bannerImg, praiseImg;
            public RectTransform[] bb = new RectTransform[3]; public Text[] cnt = new Text[3]; public Image[] cntBg = new Image[3], cntIc = new Image[3];
            public RectTransform[] run = new RectTransform[RUNS]; public Text[] runT = new Text[RUNS];
            public List<RectTransform> safes = new List<RectTransform>();
        }
        const int RUNS = 7;   // 队伍条上显示接下来几串颜色
        UIRefs ui = new UIRefs();
        bool menuOpen, modalClosable;
        bool ModalOpen => ui.modal && ui.modal.gameObject.activeSelf;

        // ======================================================================
        void BuildUI()
        {
            var esx = FindObjectOfType<EventSystem>();
            if (!esx)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                DontDestroyOnLoad(es); esx = es.GetComponent<EventSystem>();
            }
            // 拖动多远才算"拖"（不是点）：按屏幕密度算，大约 2 毫米，免得在地图上点关卡时手一抖就变成滑动
            esx.pixelDragThreshold = Mathf.Max(10, Mathf.RoundToInt((Screen.dpi > 0 ? Screen.dpi : 160) * 0.08f));
            var cg = new GameObject("UI", typeof(RectTransform));
            ui.canvas = cg.AddComponent<Canvas>(); ui.canvas.renderMode = RenderMode.ScreenSpaceOverlay; ui.canvas.sortingOrder = 10;
            ui.scaler = cg.AddComponent<CanvasScaler>();
            ui.scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; ui.scaler.referenceResolution = new Vector2(390, 844);
            cg.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)cg.transform;
            ui.vig = UIKit.Img(root, "vig", UIKit.Soft, new Color(0.63f, 0.12f, 0.08f, 0)); ui.vig.rectTransform.Stretch(-60, -60, -60, -60);
            ui.labels = UIKit.Rect("labels", root).Stretch();
            ui.safe = SafeRect(root, "safe");
            var S = ui.safe;
            ui.hud = UIKit.Rect("hud", S).Stretch();
            ui.hud.gameObject.AddComponent<CanvasGroup>();
            var H = ui.hud;

            // ---- 顶部（照概念图 hud.png）：左上菜单圆钮（10-23 换成齿轮 btn_gear） · 中间关卡牌 + 下面一条进度条（有风暴的关是风暴条）· 右上金币木牌 ----
            ui.top = UIKit.Rect("top", H).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(370, 84));
            IconBtn(ui.top, Skin.Has("btn_gear") ? "btn_gear" : "btn_menu", new Vector2(0, 1), new Vector2(26, -28), 52, () => { sfx.Play("click"); if (menuOpen) CloseMenu(); else OpenMenu(); }, "≡");
            RectTransform lv;
            if (Skin.Has("plate_cream")) { lv = Skin.Img(ui.top, "plate_cream").rectTransform; lv.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(150, 36)); }
            else lv = UIKit.Panel(ui.top, "lv", UIKit.Cream, 4).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(150, 36));
            ui.lvName = UIKit.Txt(lv, "name", "", 22, UIKit.Hex("5a3410"), TextAnchor.MiddleCenter);
            ui.lvName.rectTransform.Stretch(10, 3, 10, 0);
            // 进度条：槽 + 填充（救人进度绿色；风暴关换成蓝紫色风暴条，Storm.cs）+ 末端的奖励星星
            ui.qbar = UIKit.Rect("bar", ui.top).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-22, -46), new Vector2(168, 24));   // 短一点、往左挪：末端的乌云 / 星星别压到金币牌
            if (Skin.Has("bar_track")) Skin.Img(ui.qbar, "bar_track").rectTransform.Stretch();
            else UIKit.Panel(ui.qbar, "track", UIKit.Hex("e3d6b8"), 0, 2).Stretch();
            ui.qfill = Skin.Has("bar_green") ? Skin.Img(ui.qbar, "bar_green") : UIKit.Img(ui.qbar, "f", UIKit.Round, UIKit.Green);
            ui.qfill.rectTransform.anchorMin = new Vector2(0, 0); ui.qfill.rectTransform.anchorMax = new Vector2(0, 1);
            ui.qfill.rectTransform.pivot = new Vector2(0, 0.5f); ui.qfill.rectTransform.offsetMin = new Vector2(4, 4); ui.qfill.rectTransform.offsetMax = new Vector2(4, -4);
            ui.gift = UIKit.Rect("gift", ui.top).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(70, -58), new Vector2(34, 34));
            if (Skin.Has("star_gold")) Skin.Img(ui.gift, "star_gold").rectTransform.Stretch();
            else UIKit.Txt(ui.gift, "t", "★", 22, UIKit.Hex("ffb81f")).rectTransform.Stretch();
            if (Skin.Has("plate_wood")) { ui.coinPill = Skin.Img(ui.top, "plate_wood").rectTransform; ui.coinPill.Place(new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-2, -27), new Vector2(92, 38)); }
            else ui.coinPill = UIKit.Panel(ui.top, "coins", UIKit.Cream, 4).Place(new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-2, -27), new Vector2(92, 38));
            CoinIcon(ui.coinPill, new Vector2(8, 1), 32);
            ui.coins = UIKit.Txt(ui.coinPill, "n", "0", 19, UIKit.Hex("fff3d6"), TextAnchor.MiddleCenter); ui.coins.rectTransform.Stretch(26, 2, 6, 0);
            Styled(ui.coins, UIKit.Hex("4a2a12"), 1.6f, 1.5f);

            // ---- 第二行：奶白长条 + 接下来要的颜色（同色连着几个人合成一个光泽球，数字 = 人数）----
            // 沙滩上的小人在后期只有 8pt 高，看颜色全靠这一条
            if (Skin.Has("strip_cream")) { ui.queue = Skin.Img(H, "strip_cream").rectTransform; ui.queue.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -84), new Vector2(150, 40)); }
            else ui.queue = UIKit.Panel(H, "queue", UIKit.Hex("fff8e6", 0.96f), 3).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -84), new Vector2(150, 40));
            ui.qlbl = UIKit.Txt(ui.queue, "lbl", "", 15, UIKit.Hex("6b4a2a"), TextAnchor.MiddleCenter); ui.qlbl.rectTransform.Stretch(10, 4, 10, 0);
            var runs = UIKit.Rect("runs", ui.queue);
            runs.anchorMin = new Vector2(0, 0); runs.anchorMax = new Vector2(1, 1); runs.offsetMin = new Vector2(90, 0); runs.offsetMax = new Vector2(-14, 0);
            var hl = runs.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleLeft; hl.spacing = 4; hl.childControlWidth = hl.childControlHeight = false; hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            for (int k = 0; k < RUNS; k++)
            {
                float sz = k == 0 ? 38 : 32;
                RectTransform d;
                if (Skin.Has("ball_white")) { d = Skin.Img(runs, "ball_white").rectTransform; d.name = "run" + k; d.sizeDelta = new Vector2(sz, sz); }
                else { d = UIKit.Disc(runs, "run" + k, Color.white, 0, k == 0 ? 3 : 2); d.sizeDelta = new Vector2(sz, sz); }
                var t = UIKit.Txt(d, "n", "", k == 0 ? 18 : 15, Color.white, TextAnchor.MiddleCenter);
                Styled(t, new Color(0, 0, 0, 0.55f), 1.5f, 0);
                t.rectTransform.Stretch(0, 2, 0, 0);
                ui.run[k] = d; ui.runT[k] = t;
            }

            runs.gameObject.SetActive(false);   // 一串颜色圆圈不显示了（数据照样每帧更新，想恢复把这行删掉、人数牌改回 370 宽）
            ui.queue.gameObject.SetActive(false);   // 2026-10-22 剩余人数挪到沙滩小屋前的牌子上（QueueHouse.cs），顶上这块不显示了

            // ---- 教学字 ----
            ui.tutBox = UIKit.Rect("tutBox", H).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -144), new Vector2(370, 58));
            if (Skin.Has("bubble") && Skin.Has("mascot"))
            {
                // 照概念图的风格：左边鹦鹉船长，右边奶白对话气泡
                var bub = Skin.Img(ui.tutBox, "bubble"); bub.preserveAspect = false; bub.rectTransform.Stretch(58, 0, 0, 0);
                Skin.Img(ui.tutBox, "mascot").rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(-6, 2), new Vector2(70, 70));
                ui.tut = UIKit.Txt(ui.tutBox, "t", "", 13, UIKit.Hex("5a3410")); ui.tut.rectTransform.Stretch(80, 4, 12, 4);
            }
            else
            {
                UIKit.Img(ui.tutBox, "bg", UIKit.Round, UIKit.Hex("123c5a", 0.62f)).rectTransform.Stretch();
                ui.tut = UIKit.Txt(ui.tutBox, "t", "", 14, Color.white); ui.tut.rectTransform.Stretch(8, 2, 8, 2);
            }
            ui.tut.lineSpacing = 1.05f;

            StormUI(root);

            // ---- 底部三个道具（照概念图：奶白圆盘 + 金边，里面是图标，右上角一个圆角标）----
            // 10-28 草图：按钮缩小（直径 92 → 70），三个分得更开，给上面的港湾让地方
            ui.boost = UIKit.Rect("boost", H).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(370, 100));
            string[] ic = { "↑", "≡", "+" }, col = { "ffd76a", "a8ea7c", "8fd4ff" }, icon = { "ai_heli", "ai_horn", "ai_dock" };
            for (int k = 0; k < 3; k++)
            {
                RectTransform b;
                string disc = Skin.Has("disc_booster_g") ? "disc_booster_g" : "disc_booster";   // 10-23 用户：边框换成和上面草地一样的绿（签到、小岛那几处还是金边）
                if (Skin.Has(disc)) { b = Skin.Img(ui.boost, disc).rectTransform; b.name = "bb" + k; b.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2((k - 1) * 124, 2), new Vector2(70, 70)); }
                else b = UIKit.Disc(ui.boost, "bb" + k, UIKit.Hex(col[k]), 5).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2((k - 1) * 116, 4), new Vector2(76, 76));
                if (Deco(icon[k])) DecoImg(b, icon[k], new Vector2(0.5f, 0.5f), new Vector2(0, 2), 54);
                else UIKit.Txt(b, "ic", ic[k], k == 2 ? 40 : 34, UIKit.Brown).rectTransform.Stretch(0, 2, 0, 0);
                var badge = UIKit.Rect("cnt", b).Place(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-7, -7), new Vector2(30, 30));
                ui.cntBg[k] = Skin.Has("ball_white") ? Skin.Img(badge, "ball_white") : UIKit.Img(badge, "bg", UIKit.Circle, UIKit.Green);
                ui.cntBg[k].rectTransform.Stretch();
                ui.cnt[k] = UIKit.Txt(badge, "n", "", 13, Color.white); ui.cnt[k].rectTransform.Stretch(-6, 2, -6, 0);
                Styled(ui.cnt[k], new Color(0, 0, 0, 0.5f), 1.3f, 0);
                ui.cntIc[k] = UIKit.Img(badge, "ic", null, Color.white); ui.cntIc[k].preserveAspect = true; ui.cntIc[k].gameObject.SetActive(false);
                ui.bb[k] = b;
                int kk = k; UIKit.Btn(b, () => BoosterTap(kk));
            }

            ui.bannerBox = UIKit.Rect("banner", S).Place(new Vector2(0.5f, 0.64f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360, 110));
            ui.bannerBox.gameObject.AddComponent<CanvasGroup>().alpha = 0;
            ui.bannerBg = Skin.Has("ribbon_gold") ? Skin.Img(ui.bannerBox, "ribbon_gold") : UIKit.Img(ui.bannerBox, "bg", UIKit.Round, UIKit.Hex("123c5a", 0.7f));
            ui.bannerBg.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340, 78));
            ui.banner = UIKit.Txt(ui.bannerBox, "t", "", 26, UIKit.Hex("5a3410"), TextAnchor.MiddleCenter);
            ui.banner.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(250, 44));   // 拱形丝带：带面中心比图片正中高
            ui.banner.resizeTextForBestFit = true; ui.banner.resizeTextMinSize = 14; ui.banner.resizeTextMaxSize = 26;
            ui.bannerImg = UIKit.Img(ui.bannerBox, "word", null, Color.white); ui.bannerImg.preserveAspect = true; ui.bannerImg.rectTransform.Stretch();
            ui.praiseBox = UIKit.Rect("praise", S).Place(new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320, 96));
            ui.praiseBox.gameObject.AddComponent<CanvasGroup>().alpha = 0;
            ui.praise = UIKit.Txt(ui.praiseBox, "t", "", 40, UIKit.Hex("ffd21f"), TextAnchor.MiddleCenter, false);
            Styled(ui.praise, UIKit.Brown, 3f, 4f); ui.praise.rectTransform.Stretch();
            ui.praiseImg = UIKit.Img(ui.praiseBox, "word", null, Color.white); ui.praiseImg.preserveAspect = true; ui.praiseImg.rectTransform.Stretch();

            // ---- 引导手指（用一个跳动的箭头 + 光圈代替）----
            ui.hand = UIKit.Rect("hand", root).Place(Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 90));
            ui.handRing = UIKit.Img(ui.hand, "ring", UIKit.Ring, new Color(1, 1, 1, 0.9f)); ui.handRing.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70, 70));
            string hp = Skin.Has("hand_point") ? "hand_point" : Skin.Has("pin_red") ? "pin_red" : null;
            ui.handT = UIKit.Txt(ui.hand, "t", hp != null ? "" : "▼", 40, UIKit.Hex("ffd21f"), TextAnchor.MiddleCenter, true);
            ui.handT.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 50), new Vector2(64, 64));
            if (hp != null) Skin.Img(ui.handT.transform, hp).rectTransform.Stretch();
            ui.hand.gameObject.SetActive(false);

            // ---- 菜单 ----
            string[] rows = { "重来这一关", "关卡地图", "我的小岛", "每日任务", "设置" };
            if (Skin.Has("panel_wood")) { ui.menu = Skin.Img(S, "panel_wood").rectTransform; ui.menu.raycastTargetOn(); ui.menu.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(6, -60), new Vector2(210, rows.Length * 52 + 48)); }
            else ui.menu = UIKit.Panel(S, "menu", UIKit.Hex("fff8e6", 0.98f)).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -60), new Vector2(190, rows.Length * 54 + 12));
            for (int k = 0; k < rows.Length; k++)
            {
                var r = UIKit.Rect("row" + k, ui.menu).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -24 - k * 52), new Vector2(174, 52));
                var t = UIKit.Txt(r, "t", T(rows[k]), 19, UIKit.Hex("5a3410"), TextAnchor.MiddleLeft); t.rectTransform.Stretch(10, 0, 0, 0);
                int kk = k; UIKit.Btn(r, () => MenuRow(kk));
            }
            ui.menu.gameObject.SetActive(false);

            // ---- 开始界面、关卡地图（Screens.cs 填内容）----
            ui.fx = UIKit.Rect("fx", root).Stretch();       // 飞金币、彩纸、飘字：在 HUD 上面、开始界面和弹窗下面
            ui.title = UIKit.Rect("title", root).Stretch(); ui.title.gameObject.SetActive(false);
            ui.map = UIKit.Rect("map", root).Stretch(); ui.map.gameObject.SetActive(false);
            ui.home = UIKit.Rect("home", root).Stretch(); ui.home.gameObject.SetActive(false);   // 家园（Home.cs）

            // ---- 提示字 ----
            var tsafe = SafeRect(root, "toastSafe");
            ui.toasts = UIKit.Rect("toasts", tsafe).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -212), new Vector2(360, 10));
            var vlg = ui.toasts.gameObject.AddComponent<VerticalLayoutGroup>(); vlg.childAlignment = TextAnchor.UpperCenter; vlg.spacing = 6;
            vlg.childControlHeight = vlg.childControlWidth = false; vlg.childForceExpandHeight = vlg.childForceExpandWidth = false;

            // ---- 弹窗 ----
            ui.modal = UIKit.Rect("modal", root).Stretch();
            var dim = UIKit.Img(ui.modal, "dim", null, UIKit.Hex("0a283c", 0.55f), true); dim.rectTransform.Stretch(-50, -50, -50, -50);
            ui.box = UIKit.Rect("box", ui.modal).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320, 200));
            ui.modal.gameObject.SetActive(false);

            // ---- 广告占位 ----
            ui.ad = UIKit.Rect("ad", root).Stretch();
            UIKit.Img(ui.ad, "bg", null, Color.black, true).rectTransform.Stretch(-50, -50, -50, -50);
            ui.adN = UIKit.Txt(ui.ad, "t", "", 22, Color.white); ui.adN.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(360, 40));
            UIKit.Txt(ui.ad, "s", T("（演示占位：正式版这里是激励视频）"), 13, new Color(1, 1, 1, 0.7f)).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -22), new Vector2(360, 30));
            ui.ad.gameObject.SetActive(false);
            ApplySafeArea();
        }

        RectTransform SafeRect(Transform parent, string name)
        {
            var r = UIKit.Rect(name, parent).Stretch();
            ui.safes.Add(r);
            FitSafe(r);
            return r;
        }
        // 安全区（刘海、挖孔、底部手势条让出来的区域）。自动测试可以用 -tideSafe 上,下（像素）假装有刘海和手势条，电脑上没有真的安全区
        static Rect SafeArea
        {
            get
            {
                var sa = Screen.safeArea; var a = AutoTest.Arg("-tideSafe");
                if (a != null) { var p = a.Split(','); float t = float.Parse(p[0]), b = float.Parse(p[1]); sa = new Rect(sa.x, sa.y + b, sa.width, sa.height - t - b); }
                return sa;
            }
        }
        static void FitSafe(RectTransform r)
        {
            var sa = SafeArea;
            r.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            r.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        void CoinIcon(Transform parent, Vector2 pos, float size)
        {
            if (Deco("ai_coin")) { DecoImg(parent, "ai_coin", new Vector2(0, 0.5f), pos, size * 1.25f); return; }
            var c = UIKit.Disc(parent, "coin", UIKit.Hex("ffc530"), 0, 2).Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            c.Find("border").GetComponent<Image>().color = UIKit.Hex("c98a12");
            UIKit.Img(c, "in", UIKit.Ring, UIKit.Hex("e5a11c")).rectTransform.Stretch(size * 0.2f, size * 0.2f, size * 0.2f, size * 0.2f);
        }

        void ApplySafeArea()
        {
            if (!ui.safe) return;
            // 竖屏手机：按宽度缩放；电脑上横着的大窗口：按高度缩放，免得界面巨大
            ui.scaler.matchWidthOrHeight = Screen.width / (float)Screen.height > 390f / 844f ? 1 : 0;
            ui.safes.RemoveAll(r => !r);
            foreach (var r in ui.safes) FitSafe(r);
            Canvas.ForceUpdateCanvases();
            ui.scale = ui.canvas.scaleFactor;
            Shader.SetGlobalFloat("_TideOutlinePx", ui.scale);   // 描边宽度按界面缩放走：任何屏幕上看起来一样粗
            // 顶部那几块和道具行的宽度跟着屏幕走（最宽 370）
            float w = Mathf.Min(370, ((RectTransform)ui.canvas.transform).rect.width - 20);
            foreach (var r in new[] { ui.top, ui.tutBox }) r.sizeDelta = new Vector2(w, r.sizeDelta.y);   // 人数牌固定宽度，不跟着拉
        }

        // 3D 画面可用的区域（屏幕像素，y 朝上）：x = 道具按钮上沿，y = 顶部面板下沿
        Vector2 FreeRect()
        {
            float top = Screen.height;
            var c = new Vector3[4];
            foreach (var r in new[] { ui.top, ui.queue, ui.tutBox })
            {
                if (!r.gameObject.activeInHierarchy) continue;
                r.GetWorldCorners(c); top = Mathf.Min(top, c[0].y);
            }
            ui.bb[0].GetWorldCorners(c);
            float bot = c[1].y + 6 * ui.scale;
            return new Vector2(bot, top - 4 * ui.scale);
        }

        // ======================================================================
        void UpdateHUD()
        {
            ui.coins.text = save.coins.ToString();
            ui.lvName.text = F("第 {0} 关", levelIdx + 1);
            if (L == null) return;
            int left = L.total - vq; float pr = L.total > 0 ? vq / (float)L.total : 0;
            ui.qlbl.text = F("剩 <b><size=20>{0}</size></b> 人", left);
            // 接下来的颜色：从画面上的队头（vq）开始，同色连着的合成一个
            int qi = vq;
            for (int k = 0; k < RUNS; k++)
            {
                if (qi >= st.q.Count) { ui.run[k].gameObject.SetActive(false); continue; }
                int c = st.q[qi], n = 0;
                while (qi < st.q.Count && st.q[qi] == c) { n++; qi++; }
                ui.run[k].gameObject.SetActive(true);
                ui.run[k].Fill().color = UIKit.Hex(COLS[c]);
                ui.runT[k].text = n.ToString();
            }
            ui.qfill.rectTransform.sizeDelta = new Vector2(Mathf.Max(0, (ui.qbar.rect.width - 8) * pr), -8);
            ui.qfill.gameObject.SetActive(pr > 0.005f && !stormOn);
            bool got = save.Got(levelIdx);
            foreach (var gi in ui.gift.GetComponentsInChildren<Graphic>()) gi.color = got ? new Color(0.6f, 0.6f, 0.6f, 0.8f) : Color.white;
            giftPulse = !got && pr >= 0.7f;
            void Cnt(int i, int n, int cost)
            {
                if (boosterUse[i] >= BOOST_CAP) { Badge(i, T("用完"), UIKit.Hex("9a948a"), null); SetAlpha(ui.bb[i], 0.5f); return; }
                SetAlpha(ui.bb[i], 1);
                n = Mathf.Min(n, BOOST_CAP - boosterUse[i]);     // 角标写这关还能用几次，不写库存总数：库存 4 个也只显示 2（用户看到 4 以为每关上限没生效）
                if (n > 0) Badge(i, n.ToString(), UIKit.Green, null);
                else if (save.coins >= cost) Badge(i, cost.ToString(), UIKit.Gold, "coin");
                else Badge(i, T("广告"), UIKit.Purple, "video");
            }
            Cnt(0, save.crane + gCrane, COST["crane"]); Cnt(1, save.sort + gSort, COST["sort"]);
            int nb = st.berth.Count;
            if (nb >= MAXB) { Badge(2, T("满"), UIKit.Green, null); SetAlpha(ui.bb[2], 0.55f); }
            else
            {
                SetAlpha(ui.bb[2], 1);
                if (save.berthFree > 0) Badge(2, save.berthFree.ToString(), UIKit.Green, null);
                else if (nb < L.berths + NAD) Badge(2, T("广告"), UIKit.Purple, "video");
                else Badge(2, COST["berth"].ToString(), UIKit.Gold, "coin");
            }
            int free = st.berth.Count(x => x < 0);
            sfx.tensT = st.status == "play" && free <= 1 && !OnScreen ? 0.7f : 0;
            berthPulse = st.status == "play" && free <= 1 && nb < MAXB;
            if (st.status == "play" && free == 1 && !warned1 && st.taps > 0) { warned1 = true; Toast(T("只剩 1 个空泊位了！")); sfx.Play("warn"); Haptics.Play(Haptics.Kind.Medium); GuideNearlyFull(); }
            if (free > 1) warned1 = false;
            BoosterLockView();
        }
        bool giftPulse, berthPulse;
        // 道具角标：kind = null 只写字；"video" 只放视频图标；"coin" 小金币在上、数字在下
        void Badge(int i, string text, Color bg, string kind)
        {
            ui.cntBg[i].color = bg;
            var ic = ui.cntIc[i]; var t = ui.cnt[i];
            Sprite sp = kind == "video" && Skin.Has("icon_video") ? Skin.Get("icon_video") : kind == "coin" ? Deco("ai_coin") : null;
            ic.gameObject.SetActive(sp != null); ic.sprite = sp;
            t.color = Color.white;
            if (sp == null) { t.text = text; t.fontSize = 14; t.rectTransform.Stretch(-6, 2, -6, 0); return; }
            if (kind == "video") { t.text = ""; ic.rectTransform.Stretch(7, 8, 7, 7); return; }
            ic.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -2), new Vector2(20, 20));
            t.text = text; t.fontSize = 12; t.rectTransform.Stretch(-8, -6, -8, 18);
        }
        static void SetAlpha(RectTransform r, float a) { var cg = r.GetComponent<CanvasGroup>(); if (!cg) cg = r.gameObject.AddComponent<CanvasGroup>(); cg.alpha = a; }

        void SetCrane(bool on) { craneMode = on; }
        void ClearToasts() { foreach (Transform c in ui.toasts) Destroy(c.gameObject); }

        // 底部那条蓝色提示条 + 海鸥头像：2026-10-23 用户说弹窗太多，全部关掉（调用都留着，改回 true 就恢复）。
        // 点了不能点的东西还有别的反馈：船抖一下 + 音效、按钮跳一下、海藻/锁链船上的数字和图标
        static readonly bool TOASTS_ON = false;
        void Toast(string msg)
        {
            if (!TOASTS_ON) { Debug.Log("[Toast 已关] " + msg); return; }
            var p = UIKit.Rect("toast", ui.toasts);
            bool sk = Skin.Has("toast_bg");
            var t = UIKit.Txt(p, "t", msg, 15, UIKit.Hex("fff3d6"));
            if (sk) Styled(t, UIKit.Hex("0e2a4a"), 1.4f, 1f);
            float icon = sk && Skin.Has("mascot") ? 34 : 0;
            float w = Mathf.Min(350, t.preferredWidth + 40 + icon);
            p.sizeDelta = new Vector2(w, 42);
            var bg = sk ? Skin.Img(p, "toast_bg") : UIKit.Img(p, "bg", UIKit.Round, UIKit.Hex("281c10", 0.88f));
            bg.transform.SetAsFirstSibling(); bg.rectTransform.Stretch();
            if (icon > 0) Skin.Img(p, "mascot").rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(4, 4), new Vector2(46, 46));
            t.rectTransform.Stretch(16 + icon, 2, 16, 0);
            var cg = p.gameObject.AddComponent<CanvasGroup>();
            Fx.Run(async () => { await Fx.Sleep(2000); if (!p) return; await Fx.Tween(400, k => { if (cg) cg.alpha = 1 - k; }); if (p) Destroy(p.gameObject); });
        }

        // 艺术字贴图（codex 画的 w_<名>_zh / w_<名>_en）；没有就 null
        static Sprite WordArt(string key)
        {
            if (key == null) return null;
            string n = "w_" + key + (Loc.En ? "_en" : "_zh");
            return Skin.Has(n) ? Skin.Get(n) : null;
        }

        // 屏幕中间的大横幅。word = 固定内容的艺术字（有贴图就整张贴，没有就用 s）；会变的内容放在金丝带上
        void Banner(string s, string word = null)
        {
            var sp = WordArt(word);
            ui.bannerImg.gameObject.SetActive(sp != null); ui.bannerImg.sprite = sp;
            ui.bannerBg.gameObject.SetActive(sp == null); ui.banner.gameObject.SetActive(sp == null);
            ui.banner.text = s;
            sfx.Voice(word);
            var b = ui.bannerBox; var cg = b.GetComponent<CanvasGroup>();
            Fx.Run(() => Fx.Tween(1200, p =>
            {
                float sc = p < 0.15f ? Mathf.Lerp(0.4f, 1.15f, p / 0.15f) : p < 0.3f ? Mathf.Lerp(1.15f, 1, (p - 0.15f) / 0.15f) : 1;
                float a = p < 0.15f ? p / 0.15f : p < 0.8f ? 1 : 1 - (p - 0.8f) / 0.2f;
                b.localScale = Vector3.one * sc; cg.alpha = a;
            }));
        }

        void Praise(int k)
        {
            if (!PRAISE.TryGetValue(k, out var s)) return;
            int idx = Array.IndexOf(PRAISE.Keys.ToArray(), k) + 1;
            var sp = WordArt("praise" + idx);
            ui.praiseImg.gameObject.SetActive(sp != null); ui.praiseImg.sprite = sp;
            ui.praise.gameObject.SetActive(sp == null); ui.praise.text = T(s); sfx.Play("help"); sfx.Voice("praise" + idx);
            var b = ui.praiseBox; var cg = b.GetComponent<CanvasGroup>();
            Fx.Run(() => Fx.Tween(1000, p =>
            {
                float sc = p < 0.22f ? Mathf.Lerp(0.3f, 1.25f, p / 0.22f) : p < 0.42f ? Mathf.Lerp(1.25f, 1, (p - 0.22f) / 0.2f) : 1;
                float a = p < 0.22f ? p / 0.22f : p < 0.78f ? 1 : 1 - (p - 0.78f) / 0.22f;
                b.localScale = Vector3.one * sc; cg.alpha = a;
                b.anchoredPosition = new Vector2(0, p > 0.78f ? (p - 0.78f) / 0.22f * 34 : 0);
            }));
        }

        Vector2 ToCanvas(Vector3 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)ui.canvas.transform, screen, null, out var lp);
            return lp;
        }
        RectTransform OnCanvas(string name)
        {
            var r = UIKit.Rect(name, ui.fx);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            return r;
        }
        // 换关时清掉：Fx.Clear() 把动画都丢了，这些东西不删就会停在半空
        void ClearUIFx()
        {
            if (!ui.fx) return;
            foreach (Transform c in ui.fx) Destroy(c.gameObject);
            ClearToasts();
            ui.bannerBox.GetComponent<CanvasGroup>().alpha = 0; ui.praiseBox.GetComponent<CanvasGroup>().alpha = 0;
        }

        void FloatText(Vector3 world, string s)
        {
            var r = OnCanvas("ft"); r.sizeDelta = new Vector2(160, 40);
            var t = UIKit.Txt(r, "t", s, 26, UIKit.Hex("ffd84a"), TextAnchor.MiddleCenter); Styled(t, UIKit.Hex("6b3d0c"), 2.2f, 2f); t.rectTransform.Stretch();
            var p0 = ToCanvas(cam.WorldToScreenPoint(world)); r.anchoredPosition = p0;
            Fx.Run(async () =>
            {
                await Fx.Tween(950, p =>
                {
                    if (!r) return;
                    r.anchoredPosition = p0 + new Vector2(0, p < 0.2f ? p / 0.2f * 16 : 16 + (p - 0.2f) / 0.8f * 40);
                    r.localScale = Vector3.one * (p < 0.2f ? 1 + p / 0.2f * 0.25f : 1.25f - (p - 0.2f) * 0.3f);
                    t.color = new Color(1, 0.85f, 0.29f, p < 0.6f ? 1 : 1 - (p - 0.6f) / 0.4f);
                });
                if (r) Destroy(r.gameObject);
            });
        }

        void FlyCoin(Vector3 world, Action cb)
        {
            var r = OnCanvas("fly"); r.sizeDelta = new Vector2(22, 22);
            CoinIcon(r, new Vector2(11, 0), 22);
            var p0 = ToCanvas(cam.WorldToScreenPoint(world));
            var c = new Vector3[4]; ui.coinPill.GetWorldCorners(c);
            var p1 = ToCanvas(new Vector3(c[0].x + 22 * ui.scale, (c[0].y + c[1].y) / 2, 0));
            Fx.Run(async () =>
            {
                await Fx.Tween(650, p =>
                {
                    if (!r) return;
                    float e = p * p * (3 - 2 * p);
                    r.anchoredPosition = Vector2.Lerp(p0, p1, e) + new Vector2(0, Mathf.Sin(p * Mathf.PI) * 40);
                    r.localScale = Vector3.one * (1 - 0.35f * p);
                });
                if (r) Destroy(r.gameObject);
                Bump(ui.coinPill); cb?.Invoke();
            });
        }
        void Bump(RectTransform r) { Fx.Run(() => Fx.Tween(360, p => { if (r) r.localScale = Vector3.one * (1 + 0.18f * Mathf.Sin(p * Mathf.PI)); })); }

        void Confetti()
        {
            var rt = (RectTransform)ui.canvas.transform; float W0 = rt.rect.width, H0 = rt.rect.height;
            for (int k = 0; k < 46; k++)
            {
                var r = OnCanvas("conf"); r.sizeDelta = new Vector2(10, 16);
                UIKit.Img(r, "c", UIKit.Round, UIKit.Hex(COLS[k % 7])).rectTransform.Stretch();
                float x = (UnityEngine.Random.value - 0.5f) * W0, dur = 1400 + UnityEngine.Random.value * 1400, delay = UnityEngine.Random.value * 500, rot = UnityEngine.Random.value * 360;
                r.anchoredPosition = new Vector2(x, H0 / 2 + 20);
                Fx.Run(async () =>
                {
                    await Fx.Sleep(delay);
                    await Fx.Tween(dur, p => { if (!r) return; r.anchoredPosition = new Vector2(x, H0 / 2 + 20 - p * (H0 * 1.1f)); r.localRotation = Quaternion.Euler(0, 0, rot + p * 720); });
                    if (r) Destroy(r.gameObject);
                });
            }
        }

        // ---- 跟着 3D 物体走的小标签（船坞数字、锁、问号、队头）----
        class Label { public RectTransform root; public Text text; public Transform anchor; public Vector3 pos; public bool follow, hide; }
        readonly List<Label> labels = new List<Label>();

        Label NewLabel(Transform anchor, Vector3 pos, string s, Color bg, int size, float disc, Color? fg = null)
        {
            var lb = new Label { anchor = anchor, pos = pos, follow = anchor != null };
            var r = UIKit.Rect("lbl", ui.labels); r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            Text t;
            if (disc > 0)
            {
                if (Skin.Has("ball_white")) Skin.Img(r, "ball_white", bg).rectTransform.Stretch();
                else UIKit.Disc(r, "d", bg, 0, 3).Stretch();
                r.sizeDelta = new Vector2(disc, disc);
                t = UIKit.Txt(r, "t", s, size, fg ?? UIKit.Ink); t.rectTransform.Stretch(0, 2, 0, 0);
            }
            else
            {
                var p = UIKit.Panel(r, "p", bg, 0, 0); p.Stretch();
                t = UIKit.Txt(r, "t", s, size, fg ?? UIKit.Ink); t.rectTransform.Stretch(6, 0, 6, 0);
                r.sizeDelta = new Vector2(t.preferredWidth + 16, size + 12);
            }
            lb.root = r; lb.text = t;
            labels.Add(lb);
            return lb;
        }
        Label IconLabel(Transform anchor, Vector3 pos, string skin, float size)
        {
            var lb = new Label { anchor = anchor, pos = pos, follow = anchor != null };
            var r = UIKit.Rect("lbl", ui.labels); r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(size, size);
            Skin.Img(r, skin).rectTransform.Stretch();
            lb.root = r; labels.Add(lb);
            return lb;
        }
        // 只有字、没有底的标签（船上的人数）：白字蓝描边
        Label TextLabel(Transform anchor, Vector3 pos, string s, int size)
        {
            var lb = new Label { anchor = anchor, pos = pos, follow = anchor != null };
            var r = UIKit.Rect("lbl", ui.labels); r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(size * 2, size * 1.4f);
            var t = UIKit.Txt(r, "t", s, size, Color.white, TextAnchor.MiddleCenter, true); t.rectTransform.Stretch();
            var o8 = t.GetComponent<Outline8>(); o8.size = 1.8f; o8.drop = 1.5f;   // 字小，描边和投影也减薄
            lb.root = r; lb.text = t; labels.Add(lb);
            return lb;
        }
        void RemoveLabel(Label lb) { if (lb == null) return; labels.Remove(lb); if (lb.root) Destroy(lb.root.gameObject); }
        void ClearLabels()
        {
            foreach (var lb in labels) if (lb.root) Destroy(lb.root.gameObject); labels.Clear();
            foreach (var s in shards) if (s) Destroy(s); shards.Clear();   // 换关时锁的碎片还在飞：Fx.Clear 把动画丢了，碎片得在这里删
        }

        void UpdateLabels()
        {
            for (int k = labels.Count - 1; k >= 0; k--)
            {
                var lb = labels[k];
                if (lb.follow && !lb.anchor) { RemoveLabel(lb); continue; }
                var wp = lb.follow ? lb.anchor.position + lb.pos : lb.pos;
                var sp = cam.WorldToScreenPoint(wp);
                lb.root.anchoredPosition = ToCanvas(sp);
                // 跟着镜头远近缩放：一格（1 米）在屏幕上 44 个界面单位时是原大
                float ppu = Vector3.Distance(sp, cam.WorldToScreenPoint(wp + cam.transform.right)) / Mathf.Max(0.01f, ui.scale);
                // 挂在船、礁石、船坞上的标签再跟着它缩（网格缩小的关，船上的人数数字跟船一起变小，不会撑出船身）
                float own = lb.follow ? Mathf.Clamp(lb.anchor.lossyScale.x, 0.3f, 1) : 1;
                lb.root.localScale = Vector3.one * Mathf.Clamp(ppu / 44f, 0.35f, 1.15f) * own;
                bool vis = !lb.hide && sp.z > 0 && (!lb.follow || (lb.anchor.gameObject.activeInHierarchy && lb.anchor.lossyScale.x > 0.05f));   // 船开局是一艘艘弹出来的：还没长出来的船不显示数字
                if (lb.root.gameObject.activeSelf != vis) lb.root.gameObject.SetActive(vis);
            }
            float s = 1 + 0.06f * Mathf.Sin(Fx.Now / 140);
            ui.gift.localScale = Vector3.one * (giftPulse ? s : 1);
            ui.bb[2].localScale = Vector3.one * (berthPulse ? s * 1.03f : 1);
            var b0 = ui.bb[0].GetComponent<Image>(); if (b0) b0.color = craneMode ? UIKit.Hex("fff2b0") : Color.white;
            ui.bb[0].localScale = Vector3.one * (craneMode ? s * 1.04f : 1);
        }

        // ---- 引导 ----
        void ShowHand(int b) { hintB = b; ui.hand.gameObject.SetActive(true); if (boatV[b] != null) boatV[b].bob = false; }
        void HideHand()
        {
            if (hintB >= 0 && hintB < boatV.Length && boatV[hintB] != null && boatV[hintB].state == "lot") boatV[hintB].bob = true;
            hintB = -1; if (ui.hand) ui.hand.gameObject.SetActive(false);
        }
        void UpdateHand()
        {
            if (hintB < 0 || boatV[hintB] == null) { if (ui.hand.gameObject.activeSelf) ui.hand.gameObject.SetActive(false); return; }
            var sp = cam.WorldToScreenPoint(boatV[hintB].g.transform.position + Vector3.up * 0.4f);
            ui.hand.anchoredPosition = ToCanvas(sp) + ((RectTransform)ui.canvas.transform).rect.size / 2;
            float t = Fx.Now / 450f;
            ui.handT.rectTransform.anchoredPosition = new Vector2(0, 46 + Mathf.Abs(Mathf.Sin(t * Mathf.PI)) * 14);
            float k = (Fx.Now / 900f) % 1;
            ui.handRing.rectTransform.localScale = Vector3.one * (0.6f + 0.6f * k);
            ui.handRing.color = new Color(1, 1, 1, 0.9f * (1 - k));
        }

        // ---- 菜单 ----
        void OpenMenu() { menuOpen = true; ui.menu.gameObject.SetActive(true); }
        void CloseMenu() { menuOpen = false; ui.menu.gameObject.SetActive(false); }
        void MenuRow(int k)
        {
            CloseMenu(); sfx.Play("click");
            if (k == 0) { demo = false; HideModal(); QuitTrack("restart"); LoadLevel(levelIdx); }
            else if (k == 1) { demo = false; ShowMap(false); }
            else if (k == 2) { if (save.top < FEAT_HOME) Toast(F("通关第 {0} 关解锁", FEAT_HOME)); else if (!demo) ShowHome("game"); }
            else if (k == 3) { if (save.top < FEAT_DAILY) Toast(F("通关第 {0} 关解锁", FEAT_DAILY)); else ShowTasks(); }
            else ShowSettings();
        }

        // ======================================================================
        // 弹窗。closable = 安卓返回键 / 点外面能不能关（胜利、失败弹窗不能）
        RectTransform boxContent;
        RectTransform Modal(bool closable = true, bool pop = true)
        {
            modalClosable = closable;
            foreach (Transform c in ui.box) Destroy(c.gameObject);
            bool wood = Skin.Has("panel_wood");
            if (wood) { var pw = Skin.Img(ui.box, "panel_wood"); pw.raycastTarget = true; pw.rectTransform.Stretch(); }
            else UIKit.Panel(ui.box, "panel", UIKit.Hex("fff8e6", 0.98f), 4).Stretch();
            boxContent = wood ? UIKit.Rect("content", ui.box).Stretch(30, 34, 30, 30) : UIKit.Rect("content", ui.box).Stretch(16, 18, 16, 16);
            if (closable) ModalClose();
            var v = boxContent.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.UpperCenter; v.spacing = 8; v.childControlHeight = true; v.childControlWidth = true;
            v.childForceExpandHeight = false; v.childForceExpandWidth = true;
            ui.modal.gameObject.SetActive(true);
            if (pop)
            {
                ui.box.localScale = Vector3.one * 0.6f;
                Fx.Run(() => Fx.Tween(300, p => { if (ui.box) ui.box.localScale = Vector3.one * Mathf.Lerp(0.6f, 1, Fx.BackOut(p)); }));
            }
            return boxContent;
        }
        Text MText(string s, int size = 15, Color? c = null)
        {
            var t = UIKit.Txt(boxContent, "t", s, size, c ?? UIKit.Ink);
            t.lineSpacing = 1.15f;
            return t;
        }
        Button MButton(string s, string fill, Action fn, bool enabled = true, string fg = "ffffff", int size = 18, float h = 50)
        {
            var r = SkinBtn(boxContent, fill, s, size + 1, () => { if (enabled) fn(); }, out _);
            r.gameObject.AddComponent<LayoutElement>().preferredHeight = Mathf.Max(h, 44) + 4;
            if (!enabled) SetAlpha(r, 0.55f);
            return r.GetComponent<Button>();
        }
        void MSpace(float h) { var r = UIKit.Rect("sp", boxContent); r.gameObject.AddComponent<LayoutElement>().preferredHeight = h; }
        // 弹窗内容排好后，按内容高度定弹窗高度
        void FinishModal()
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(boxContent);
            float h = LayoutUtility.GetPreferredHeight(boxContent) + (Skin.Has("panel_wood") ? 66 : 34);
            float maxH = ((RectTransform)ui.canvas.transform).rect.height * 0.92f;
            ui.box.sizeDelta = new Vector2(Mathf.Min(340, ((RectTransform)ui.canvas.transform).rect.width * 0.9f), Mathf.Min(h, maxH));
        }
        void HideModal() { if (ui.modal) ui.modal.gameObject.SetActive(false); }

        // 每个新东西的配图（Blender 渲染，Resources/UI/*.png）
        // 船、礁石、船坞用 Blender 拿游戏模型渲的图（必须和游戏里长得一样）；风暴这类不是游戏里物件的用 codex 画的 ai_*
        static readonly Dictionary<string, string> CARD_PIC = new Dictionary<string, string> {
            { "yacht", "deco_boat_yacht" }, { "raft", "deco_boat_raft" }, { "carrier", "deco_boat_carrier" }, { "rock", "deco_rock_a" },
            { "house", "deco_boathouse" }, { "mys", "card_mys" }, { "storm", "ai_storm" },
            { "weed", "card_weed" }, { "lock", "card_lock" }, { "shark", "card_shark" } };

        void ShowCard(string k)
        {
            Analytics.Track("intro_card", "lv", levelIdx + 1, "id", k);
            Modal();
            ModalTitle(T("新机制！"), "new");
            // 配图：淡蓝底的圆角框里放渲染图
            if (CARD_PIC.TryGetValue(k, out var pic))
            {
                var fr = UIKit.Rect("pic", boxContent); fr.gameObject.AddComponent<LayoutElement>().preferredHeight = 150;
                var bg = UIKit.Img(fr, "bg", UIKit.Round, UIKit.Hex("cfeefa")); bg.rectTransform.anchorMin = bg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); bg.rectTransform.sizeDelta = new Vector2(220, 150);
                if (pic.StartsWith("ai_") && !Deco(pic)) pic = "card_" + k;      // 还没有 AI 图就用 Blender 那张
                var im = DecoImg(fr, pic.StartsWith("ai_") ? pic : pic.Replace("deco_", "").Replace("card_", "card:"), new Vector2(0.5f, 0.5f), Vector2.zero, 150);
                Fx.Run(() => Fx.Tween(900, p => { if (im) im.rectTransform.localScale = Vector3.one * (1 + 0.06f * Mathf.Sin(p * Mathf.PI * 2)); }));
            }
            MText(T(CARD[k][0]), 28, UIKit.Brown);
            MText(T(CARD[k][1]), 16);
            MButton(T("知道了"), "3fb950", () => { sfx.Play("click"); HideModal(); });
            FinishModal();
        }

        // 道具第一次点：先弹说明（玩家不知道这三个按钮干嘛用），看过一次以后点了直接用
        static readonly string[] BOOST_ID = { "crane", "sort", "berth" };
        static readonly (string name, string pic, string desc)[] BOOST_TIP = {
            // 每行不超过 12 个字：弹窗窄，长了会在标点前后断得很难看
            ("直升机", "ai_heli", "先点它，再点<b>任意一艘船</b>\n直升机把它吊到码头\n被挡着的也行，堵死时用它"),
            ("喇叭", "ai_horn", "喊一声，<b>能马上上船的人</b>\n先站到队头\n队头的人上不了船时用它"),
            ("临时码头", "ai_dock", "码头<b>多开一个泊位</b>\n这一关一直有效\n码头快停满时用它") };

        void BoosterTap(int k)
        {
            if (!Unlocked(k)) { sfx.Play("bad"); Toast(F("第 {0} 关解锁", UNLOCK[k] + 1)); Bump(ui.bb[k]); return; }
            if (k < 2 && boosterUse[k] >= BOOST_CAP) { sfx.Play("bad"); Toast(F("每关{0}最多用 {1} 次", T(BOOST_TIP[k].name), BOOST_CAP)); Bump(ui.bb[k]); return; }
            boosterTapped = k;     // 解锁引导在等这个
            Action go = k == 0 ? DoCrane : k == 1 ? (Action)DoSort : DoBerth;
            string key = "tip_" + BOOST_ID[k];
            if (save.seen.Contains(key) || st == null || st.status != "play" || demo) { go(); return; }
            save.seen.Add(key); save.Persist();
            sfx.Play("click"); ShowBoosterTip(k, go);
        }

        void ShowBoosterTip(int k, Action go)
        {
            Analytics.Track("booster_tip", "lv", levelIdx + 1, "id", BOOST_ID[k]);
            var tip = BOOST_TIP[k];
            Modal();
            ModalTitle(T("道具说明"), "tip");
            var fr = UIKit.Rect("pic", boxContent); fr.gameObject.AddComponent<LayoutElement>().preferredHeight = 150;
            var bg = UIKit.Img(fr, "bg", UIKit.Round, UIKit.Hex("cfeefa")); bg.rectTransform.anchorMin = bg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); bg.rectTransform.sizeDelta = new Vector2(220, 150);
            if (Deco(tip.pic))
            {
                var im = DecoImg(fr, tip.pic, new Vector2(0.5f, 0.5f), Vector2.zero, 136);
                Fx.Run(() => Fx.Tween(900, p => { if (im) im.rectTransform.localScale = Vector3.one * (1 + 0.06f * Mathf.Sin(p * Mathf.PI * 2)); }));
            }
            MText(T(tip.name), 28, UIKit.Brown);
            MText(T(tip.desc), 16);
            string cost;
            if (k < 2) { int have = k == 0 ? save.crane + gCrane : save.sort + gSort; int left = BOOST_CAP - boosterUse[k]; cost = (have > 0 ? F("你还有 <b>{0}</b> 次免费", have) : F("每次 <b>{0}</b> 金币", COST[BOOST_ID[k]])) + "\n" + F("每关最多用 {0} 次，这关还能用 <b>{1}</b> 次", BOOST_CAP, left); }
            else { int n = st.berth.Count; cost = n >= MAXB ? T("泊位已经全开了") : n < L.berths + NAD ? T("看一段广告就能开") : F("花 <b>{0}</b> 金币开", COST["berth"]); }
            MText(cost, 15, UIKit.Hex("2f6b2a"));
            if (go != null)
            {
                MButton(T("马上用"), "3fb950", () => { sfx.Play("click"); HideModal(); go(); });
                MButton(T("先不用"), "fff8e6", () => { sfx.Play("click"); HideModal(); }, true, "7a4d21", 15, 40);
            }
            else MButton(T("知道了"), "3fb950", () => { sfx.Play("click"); HideModal(); });
            FinishModal();
        }

        string Stars(int k) => string.Concat(Enumerable.Range(1, 3).Select(j => j <= k ? "<color=#ffb81f>★</color>" : "<color=#d8cfbd>★</color>"));

        async Task WinFx()
        {
            Banner(T("全员上船！"), "win"); sfx.Play("fanfare"); sfx.Play("cheer", 1, 0.9f); Confetti(); Fireworks(); Haptics.Play(Haptics.Kind.Success);
            int t = tok; await Fx.Sleep(1300); if (t != tok) return;
            if (demo)
            {
                demo = false; Modal(false); ModalTitle(T("演示结束"));
                MButton(T("我来试试"), "3fb950", () => { HideModal(); LoadLevel(levelIdx); }); FinishModal(); return;
            }
            bool newColl = !save.Got(levelIdx); if (newColl) save.coll.Add(levelIdx);
            int gain = GrantStars(), tag = L.cfg.tag;     // 星星只在第一次通关给（Home.cs）
            TaskAdd("win"); TaskAdd("folk", L.total); if (tag > 0) TaskAdd("hard"); if (!usedBooster && !usedRevive) TaskAdd("clean");
            save.streak++; lastEarn = 10 + Mathf.Min(levelIdx, 20) + tag * 10;     // 用户嫌金币给太多（道具随便买、难度没了）：普通关 10~30，困难 +10、超难 +20
            if (HasPerk("coin")) lastEarn = Mathf.RoundToInt(lastEarn * 1.1f);     // 岛上建了小餐馆
            save.coins += lastEarn; save.pop += L.total;
            if (levelIdx + 1 > save.top && levelIdx + 1 < LEVELS.Length) mapUnlockAnim = levelIdx + 1;     // 回地图时播"下一关解锁"
            save.top = Mathf.Max(save.top, levelIdx + 1); save.Persist(); UpdateHUD();
            Analytics.Track("level_win", "lv", levelIdx + 1, "try", save.Tries(levelIdx), "stars", gain, "sec", LevelSec, "taps", st.taps, "booster", usedBooster, "revive", usedRevive, "streak", save.streak);
            bool next = levelIdx < LEVELS.Length - 1;
            Modal(false);
            ModalTitle(T("全员上船！"), "win");
            // 10-23 用户嫌字太多：只留一行奖励（星星 +N、金币 +N）+ 按钮；岛民、星星总数、宝箱进度、每日任务都不在这里说了（岛 / 地图上有红点）
            var srow = UIKit.Rect("reward", boxContent); srow.gameObject.AddComponent<LayoutElement>().preferredHeight = 84;
            RectTransform bigStar = null; Text plus = null;
            float cx = gain > 0 ? 70 : 0;     // 有星星时星星在左、金币在右；没星星金币居中
            if (gain > 0)
            {
                bigStar = UIKit.Rect("s", srow).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-110, 0), new Vector2(68, 68));
                if (Skin.Has("star_gold")) Skin.Img(bigStar, "star_gold").rectTransform.Stretch(); else UIKit.Txt(bigStar, "t", "★", 56, UIKit.Hex("ffb81f")).rectTransform.Stretch();
                bigStar.localScale = Vector3.one * 0.01f;
                plus = UIKit.Txt(srow, "n", "+" + gain, 40, UIKit.Hex("ffd21f"), TextAnchor.MiddleLeft); Styled(plus, UIKit.Brown, 3f, 3f);
                plus.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), new Vector2(-72, 0), new Vector2(90, 60)); plus.color = new Color(1, 0.82f, 0.12f, 0);
            }
            var coinSp = Deco("ai_coin");
            if (coinSp) { var ci = UIKit.Img(srow, "c", coinSp, Color.white); ci.preserveAspect = true; ci.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(cx - 34, 0), new Vector2(56, 56)); }
            var earn = UIKit.Txt(srow, "e", "+0", 40, UIKit.Hex("ffd21f"), TextAnchor.MiddleLeft); Styled(earn, UIKit.Brown, 3f, 3f);
            earn.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), new Vector2(cx + 2, 0), new Vector2(120, 60));
            if (!next) MText(T("全部通关！") + "\n" + T("更多小岛正在建造中，敬请期待。"), 15, UIKit.Hex("b5371f"));
            Button dbl = null;
            dbl = MButton(F("金币翻倍 +{0}", lastEarn), "8a5cff", () =>
            {
                sfx.Play("click");
                WatchAd("double_coins", () => { save.coins += lastEarn; save.Persist(); UpdateHUD(); sfx.Play("coin"); if (dbl) { dbl.interactable = false; dbl.GetComponentInChildren<Text>().text = F("已领取 +{0}", lastEarn); SetAlpha((RectTransform)dbl.transform, 0.6f); } });
            });
            // 能建东西（或者有宝箱）就把"去岛上建造"放最上面
            bool build = CanBuild || ChestReady > 0;
            if (build)
            {
                var bt = MButton(T("去岛上建造"), "ffb81f", () => { sfx.Play("click"); ShowHome("win"); });
                if (!save.seen.Contains("g_home1") && !GuideOff) PointAt((RectTransform)bt.transform);     // 第一次：手指指着它
            }
            if (next) MButton(T("继续"), "3fb950", () => { sfx.Play("click"); HideModal(); ShowMap(true); });     // 过关先回地图看下一关解锁，玩家自己点（用户要求，不再直接进下一关）
            else MButton(T("地图"), "3fb950", () => { HideModal(); ShowMap(false); });
            FinishModal();
            if (gain > 0)
                Fx.Run(async () =>
                {
                    await Fx.Sleep(300); if (t != tok || !bigStar) return;
                    sfx.Play("star3"); Haptics.Play(Haptics.Kind.Light); if (gain >= 3) sfx.Voice("amazing");
                    await Fx.Tween(420, p =>
                    {
                        if (bigStar) { bigStar.localScale = Vector3.one * Mathf.Lerp(0.2f, 1, Fx.BackOut(p)); bigStar.localRotation = Quaternion.Euler(0, 0, (1 - p) * 160); }
                        if (plus) plus.color = new Color(1, 0.82f, 0.12f, Mathf.Clamp01(p * 2 - 0.4f));
                    });
                });
            int to = lastEarn;
            Fx.Run(() => Fx.Tween(700, p => { if (earn) earn.text = "+" + Mathf.RoundToInt(to * (1 - Mathf.Pow(1 - p, 3))); }));
        }

        async Task LoseFx()
        {
            sfx.Play("fail"); sfx.Voice("lose", 0.8f); shake = 0.4f; Haptics.Play(Haptics.Kind.Fail);
            int t = tok; await Fx.Sleep(500); if (t != tok) return;
            if (demo) { demo = false; return; }
            int left = st.q.Count - st.qi; bool storm = stormLose, can = storm || st.berth.Count < MAXB;
            Analytics.Track("level_fail", "lv", levelIdx + 1, "try", save.Tries(levelIdx), "sec", LevelSec, "taps", st.taps, "left", left, "why", storm ? "storm" : "full");
            Modal(false);
            string lp = storm ? "ai_storm" : "ai_ring";
            if (Deco(lp))
            {
                var fr = UIKit.Rect("pic", boxContent); fr.gameObject.AddComponent<LayoutElement>().preferredHeight = 96;
                DecoImg(fr, lp, new Vector2(0.5f, 0.5f), Vector2.zero, 104);
            }
            if (storm)
            {
                ModalTitle(T("风暴来了！"), "storm");
                MText(F("还有 {0} 人没救出来", left), 20, UIKit.Hex("d5392a"));
                MText(T("风暴来之前没能把大家都送走"), 13, UIKit.Hex("7a5a3a"));
            }
            else
            {
                ModalTitle(T("码头停满了！"), "full");
                MText(F("还有 {0} 人没上船", left), 20, UIKit.Hex("d5392a"));
                MText(T("队头的人要的颜色，码头上一艘都没有"), 13, UIKit.Hex("7a5a3a"));
            }
            if (save.streak > 0 && STREAK_GIFT_ON) MText(F("放弃会断掉 {0} 连胜", save.streak), 14, UIKit.Hex("b5371f"));
            if (storm)
            {
                MText(F("复活：风暴推迟 {0} 秒", STORM_REVIVE), 14, UIKit.Hex("2f6b2a"));
                MButton(T("看广告复活"), "8a5cff", () => Revive(false));
                MButton(F("{0} 金币复活", COST["cont"]), "ffb81f", () => Revive(true), save.coins >= COST["cont"], "5a3a10");
            }
            else if (can)
            {
                MText(T("复活：多开一个泊位接着玩"), 14, UIKit.Hex("2f6b2a"));
                MButton(T("看广告复活"), "8a5cff", () => Revive(false));
                MButton(F("{0} 金币复活", COST["cont"]), "ffb81f", () => Revive(true), save.coins >= COST["cont"], "5a3a10");
            }
            MButton(T("放弃，重新开始"), "fff8e6", () => { sfx.Play("click"); save.streak = 0; save.Persist(); HideModal(); LoadLevel(levelIdx); }, true, "7a4d21", 15, 40);
            FinishModal();
        }

        void Revive(bool coin)
        {
            sfx.Play("click");
            bool storm = stormLose;
            BLog("复活 " + (coin ? "金币" : "广告"));
            Action go = () => { HideModal(); usedRevive = true; Analytics.Track("revive", "lv", levelIdx + 1, "pay", coin ? "coin" : "ad", "why", storm ? "storm" : "full"); if (storm) StormRevive(); else OpenBerth(); };
            if (coin) { if (save.coins < COST["cont"]) return; save.coins -= COST["cont"]; save.Persist(); go(); }
            else { HideModal(); WatchAd("revive", go); }
        }

        // 激励视频（占位）。place = 哪个位置的广告，统计用
        void WatchAd(string place, Action cb)
        {
            ui.ad.gameObject.SetActive(true); ui.ad.SetAsLastSibling();
            Fx.Run(async () =>
            {
                for (int n = 2; n >= 0; n--) { ui.adN.text = F("广告播放中… {0}", n); await Fx.Sleep(550); }
                ui.ad.gameObject.SetActive(false);
                Analytics.Track("ad_rewarded", "place", place, "lv", levelIdx + 1);
                cb();
            });
        }
    }
}
