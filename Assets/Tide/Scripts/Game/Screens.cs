// 开始界面、关卡地图、设置、安卓返回键。
// 开始界面是半透明的一层，后面就是要打的那一关（已经摆好），点"开始"才真正开局（这时才弹新机制卡片、记 level_start）。
// 地图：100 关分 10 座岛，每岛 10 关，从下往上走；每座岛一张自己的插画 map1~10（没有的退回循环用前 5 张）。打过的绿色带星星，当前关金色跳动，没解锁的灰色。
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static Tide.Loc;

namespace Tide
{
    public partial class Game
    {
        static readonly (string name, string band, string[] deco)[] ISLES = {
            ("椰林湾", "fde4a6", new[] { "palm_a", "umbrella", "starfish" }),
            ("珊瑚湾", "ffd3c4", new[] { "lifering", "buoy_red", "palm_b" }),
            ("海龟礁", "c9efc0", new[] { "bush", "rock_a", "crate" }),
            ("火山岛", "ecd2bb", new[] { "rock_a", "barrel", "hut" }),
            ("灯塔角", "d5e7ff", new[] { "lighthouse", "boathouse", "palm_a" }),
            // 10-26 加到 100 关：第 6~10 岛，每岛一个主题（Tools/Factory 的 THEME）
            ("鲨鱼湾", "cfe6f5", new[] { "buoy_red", "lifering", "rock_a" }),
            ("迷雾岛", "e1dcf0", new[] { "lighthouse", "bush", "crate" }),
            ("礁石滩", "e6dccb", new[] { "rock_a", "starfish", "barrel" }),
            ("锁链港", "f2e0c2", new[] { "boathouse", "crate", "barrel" }),
            ("风暴角", "d3dbe6", new[] { "lighthouse", "hut", "palm_b" }) };
        // 第 k 座岛用哪张插画（以及哪套按钮坐标）：有 map{k+1} 就用自己的，没有就循环用前 5 张
        static int MapArt(int k) => Skin.Has("map" + (k + 1)) ? k : k % 5;
        const int PER_ISLE = 10;

        bool OnScreen => ui.title && (ui.title.gameObject.activeSelf || ui.map.gameObject.activeSelf || (ui.home && ui.home.gameObject.activeSelf));
        bool mapFromTitle;
        float levelT0;
        int LevelSec => Mathf.RoundToInt(Time.realtimeSinceStartup - levelT0);
        RectTransform mapCur, titlePlay, titleBoat, titleDot;
        int devTaps; float devT;

        // 打到一半走掉（重来 / 去地图选了别的关）
        void QuitTrack(string why)
        {
            if (st != null && st.status == "play" && st.taps > 0)
            {
                Analytics.Track("level_quit", "lv", levelIdx + 1, "try", save.Tries(levelIdx), "sec", LevelSec, "taps", st.taps, "why", why);
                BreakStreak();      // 打到一半不打了也算输
            }
        }

        void ApplySettings()
        {
            if (sfx) { sfx.muted = !save.sfx; sfx.musicOff = !save.music; }
            Haptics.On = save.vibe;
        }

        void SetHud(bool on)
        {
            var cg = ui.hud.GetComponent<CanvasGroup>();
            cg.alpha = on ? 1 : 0; cg.blocksRaycasts = on;
            ui.labels.gameObject.SetActive(on);   // 场景里的小标签（广告/解锁/队头）也藏起来，开始界面干净点
            if (!on) { CloseMenu(); HideHand(); }
        }

        static readonly Dictionary<string, Sprite> decoCache = new Dictionary<string, Sprite>();
        static Sprite Deco(string name)
        {
            if (decoCache.TryGetValue(name, out var s)) return s;
            var t = Resources.Load<Texture2D>(name.StartsWith("card:") ? "UI/card_" + name.Substring(5) : name.StartsWith("ai_") ? "UI/" + name : "UI/deco_" + name);
            s = t ? Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f)) : null;
            decoCache[name] = s;
            return s;
        }
        static Image DecoImg(Transform parent, string name, Vector2 anchor, Vector2 pos, float size)
        {
            var im = UIKit.Img(parent, "deco_" + name, Deco(name), Color.white);
            im.preserveAspect = true;
            im.rectTransform.Place(anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            return im;
        }

        // 竖直渐变（上深下浅），开始界面压暗用
        static Sprite gradSprite;
        static Sprite Grad()
        {
            if (gradSprite) return gradSprite;
            var t = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++) { float k = y / 63f; t.SetPixel(0, y, new Color(0.04f, 0.16f, 0.24f, Mathf.Lerp(0.18f, 0.62f, k * k))); }
            t.Apply();
            return gradSprite = Sprite.Create(t, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f));
        }

        int CurLevel => Mathf.Clamp(save.top, 0, LEVELS.Length - 1);

        // ======================================================================
        // 开始界面
        void ShowTitle()
        {
            Analytics.Track("screen", "name", "title");
            var R = ui.title; foreach (Transform c in R) Destroy(c.gameObject);
            R.gameObject.SetActive(true); SetHud(false); HideModal();
            if (TitleV2(R)) { GuideTitle(); return; }       // 第 2 版（Title2.cs）；素材不全时退回下面的老版
            // 照概念图 title.png：满屏的海港插画 + 上方木牌标志 + 绿色大 PLAY 按钮 + 关卡号 + 两个奶白方钮（地图、设置）
            if (Skin.Has("bg_title"))
            {
                var bg = Skin.Img(R, "bg_title"); bg.raycastTarget = true; bg.preserveAspect = false;
                var fit = bg.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = bg.sprite.rect.width / bg.sprite.rect.height;
            }
            else UIKit.Img(R, "dim", Grad(), Color.white, true).rectTransform.Stretch(-50, -50, -50, -50);
            var S = SafeRect(R, "safe");
            string logo = En ? "logo_en" : "logo_zh";
            if (!Skin.Has(logo)) logo = "logo_en";
            if (Skin.Has(logo))
            {
                var lg = Skin.Img(S, logo).rectTransform; lg.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(380, 250));
                titleBoat = lg;
            }
            else
            {
                var title = UIKit.Txt(S, "name", T("潮汐岛"), En ? 54 : 60, Color.white, TextAnchor.MiddleCenter, true);
                title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(380, 80));
            }
            bool all = save.top >= LEVELS.Length;
            titlePlay = SkinBtn(S, "3fb950", T("开始"), 42, () => { sfx.Play("click"); if (all) ShowMap(true); else PlayLevel(CurLevel); }, out var pl);
            titlePlay.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 236), new Vector2(256, 88));
            pl.rectTransform.offsetMin = new Vector2(14, 10);
            var lvT = UIKit.Txt(S, "lv", all ? T("全部通关！") : F("第 {0} 关", CurLevel + 1), 22, UIKit.Hex("fff3d6"), TextAnchor.MiddleCenter);
            Styled(lvT, UIKit.Hex("6b4a2a"), 2f, 1.5f);
            lvT.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 172), new Vector2(240, 32));
            string[] ic = { "icon_map", "hut", "icon_gear" }, lbl = { "地图", "我的小岛", "设置" };
            titleHomeBtn = titleSignBtn = titleTaskBtn = null;
            bool homeOn = save.top >= FEAT_HOME;           // "我的小岛"打过第 1 关才出现（新玩家开局只有开始、地图、设置）
            var keys = homeOn ? new[] { 0, 1, 2 } : new[] { 0, 2 };
            for (int i = 0; i < keys.Length; i++)
            {
                int k = keys[i], kk = k; float x = keys.Length == 3 ? (i - 1) * 112 : (i == 0 ? -62 : 62);
                RectTransform b;
                if (Skin.Has("btn_square")) { b = Skin.Img(S, "btn_square").rectTransform; b.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(x, 92), new Vector2(84, 84)); }
                else b = UIKit.Panel(S, "b" + k, UIKit.Cream, 4).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(x, 92), new Vector2(84, 84));
                if (k == 1 && Deco("hut")) DecoImg(b, "hut", new Vector2(0.5f, 0.5f), new Vector2(0, 4), 64);
                else if (Skin.Has(ic[k])) Skin.Img(b, ic[k]).rectTransform.Stretch(14, 14, 14, 14);
                else UIKit.Txt(b, "t", T(lbl[k]), 18, UIKit.Brown).rectTransform.Stretch();
                if (k == 1)
                {
                    var cap = UIKit.Txt(b, "l", T("我的小岛"), 13, UIKit.Hex("fff3d6")); cap.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(110, 22));
                    Styled(cap, UIKit.Hex("4a2a12"), 1.5f, 1f);
                    if (HomeNews)
                    {
                        var dot = UIKit.Rect("dot", b).Place(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-8, -8), new Vector2(28, 28));
                        if (Skin.Has("badge_red")) Skin.Img(dot, "badge_red").rectTransform.Stretch(); else UIKit.Img(dot, "d", UIKit.Circle, UIKit.Hex("e8402f")).rectTransform.Stretch();
                        UIKit.Txt(dot, "t", "!", 17, Color.white).rectTransform.Stretch(0, 1, 0, 0);
                        titleDot = dot;
                    }
                }
                if (k == 1) titleHomeBtn = b;
                UIKit.Btn(b, () => { sfx.Play("click"); if (kk == 0) ShowMap(true); else if (kk == 1) ShowHome("title"); else ShowSettings(); });
            }
            if (save.top >= FEAT_DAILY) { DailyButtons(S); MaybeAutoSign(); }     // 签到、任务打过第 3 关才出现
            GuideTitle();
            UIKit.Txt(S, "ver", "v" + Application.version, 11, new Color(1, 1, 1, 0.7f), TextAnchor.LowerRight).rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-10, 8), new Vector2(120, 16));
        }
        void HideTitle() { ui.title.gameObject.SetActive(false); titlePlay = null; titleBoat = null; titleDot = null; signDot = taskDot = null; }

        void PlayLevel(int i)
        {
            if (i != levelIdx) QuitTrack("map");
            HideTitle(); HideMap(); HideModal(); SetHud(true);
            LoadLevel(i);
        }

        // ======================================================================
        // 关卡地图（照概念图 map.png）：每座岛一张竖长的插画（skin/map1..5，codex 画的），从下往上接起来；
        // 关卡按钮摆在插画里的沙地小平台上 —— 平台位置看图标在 MAP_PATH 里（u = 横向 0~1，v = 从下往上 0~1）。
        // 某张图还没标坐标就用默认的 S 形路线
        // 坐标用 art/tools/find_pads2.ps1 从插画里找每个圆平台的正中心（10-26 换的：第 1 版取沙地重心，平台连着沙滩时按钮会歪到边上）；
        // 锁链港那张（8）画了 11 个平台，最下面那个贴着岛名丝带，不用
        static readonly Dictionary<int, Vector2[]> MAP_PATH = new Dictionary<int, Vector2[]> {
            { 0, new[] { new Vector2(0.620f, 0.179f), new Vector2(0.440f, 0.309f), new Vector2(0.665f, 0.413f), new Vector2(0.419f, 0.502f), new Vector2(0.649f, 0.585f),
                         new Vector2(0.442f, 0.663f), new Vector2(0.675f, 0.722f), new Vector2(0.504f, 0.797f), new Vector2(0.697f, 0.864f), new Vector2(0.544f, 0.929f) } },
            { 1, new[] { new Vector2(0.569f, 0.130f), new Vector2(0.396f, 0.221f), new Vector2(0.583f, 0.313f), new Vector2(0.396f, 0.404f), new Vector2(0.628f, 0.471f),
                         new Vector2(0.437f, 0.551f), new Vector2(0.599f, 0.641f), new Vector2(0.417f, 0.710f), new Vector2(0.561f, 0.786f), new Vector2(0.383f, 0.849f) } },
            { 2, new[] { new Vector2(0.474f, 0.169f), new Vector2(0.587f, 0.273f), new Vector2(0.467f, 0.362f), new Vector2(0.583f, 0.452f), new Vector2(0.472f, 0.537f),
                         new Vector2(0.599f, 0.618f), new Vector2(0.487f, 0.698f), new Vector2(0.608f, 0.781f), new Vector2(0.509f, 0.856f), new Vector2(0.596f, 0.935f) } },
            { 3, new[] { new Vector2(0.596f, 0.124f), new Vector2(0.418f, 0.221f), new Vector2(0.635f, 0.318f), new Vector2(0.426f, 0.406f), new Vector2(0.630f, 0.489f),
                         new Vector2(0.444f, 0.565f), new Vector2(0.640f, 0.636f), new Vector2(0.481f, 0.705f), new Vector2(0.639f, 0.769f), new Vector2(0.476f, 0.828f) } },
            { 4, new[] { new Vector2(0.468f, 0.142f), new Vector2(0.663f, 0.258f), new Vector2(0.455f, 0.354f), new Vector2(0.660f, 0.449f), new Vector2(0.480f, 0.533f),
                         new Vector2(0.657f, 0.604f), new Vector2(0.486f, 0.670f), new Vector2(0.654f, 0.737f), new Vector2(0.489f, 0.789f), new Vector2(0.639f, 0.837f) } },
            // 10-26 第 6~10 岛（map6~10）
            { 5, new[] { new Vector2(0.580f, 0.162f), new Vector2(0.413f, 0.270f), new Vector2(0.616f, 0.357f), new Vector2(0.422f, 0.458f), new Vector2(0.625f, 0.539f),
                         new Vector2(0.454f, 0.628f), new Vector2(0.662f, 0.703f), new Vector2(0.505f, 0.786f), new Vector2(0.695f, 0.856f), new Vector2(0.527f, 0.926f) } },
            { 6, new[] { new Vector2(0.604f, 0.208f), new Vector2(0.429f, 0.309f), new Vector2(0.609f, 0.405f), new Vector2(0.421f, 0.495f), new Vector2(0.635f, 0.574f),
                         new Vector2(0.453f, 0.657f), new Vector2(0.665f, 0.722f), new Vector2(0.522f, 0.800f), new Vector2(0.699f, 0.864f), new Vector2(0.556f, 0.934f) } },
            { 7, new[] { new Vector2(0.396f, 0.180f), new Vector2(0.610f, 0.281f), new Vector2(0.393f, 0.371f), new Vector2(0.604f, 0.463f), new Vector2(0.393f, 0.544f),
                         new Vector2(0.595f, 0.628f), new Vector2(0.396f, 0.705f), new Vector2(0.594f, 0.781f), new Vector2(0.404f, 0.853f), new Vector2(0.592f, 0.925f) } },
            { 8, new[] { new Vector2(0.592f, 0.217f), new Vector2(0.389f, 0.326f), new Vector2(0.605f, 0.406f), new Vector2(0.400f, 0.493f), new Vector2(0.632f, 0.574f),
                         new Vector2(0.433f, 0.653f), new Vector2(0.646f, 0.719f), new Vector2(0.467f, 0.795f), new Vector2(0.664f, 0.862f), new Vector2(0.518f, 0.930f) } },
            { 9, new[] { new Vector2(0.447f, 0.156f), new Vector2(0.609f, 0.263f), new Vector2(0.429f, 0.359f), new Vector2(0.630f, 0.443f), new Vector2(0.448f, 0.525f),
                         new Vector2(0.613f, 0.608f), new Vector2(0.445f, 0.683f), new Vector2(0.604f, 0.758f), new Vector2(0.450f, 0.834f), new Vector2(0.557f, 0.919f) } } };
        const float ISLE_H = 1000;      // 一座岛（一张插画）在地图上多高（界面单位）

        Vector2 NodeUV(int isle, int j)
        {
            if (MAP_PATH.TryGetValue(isle, out var p) && j < p.Length) return p[j];
            return new Vector2(0.5f + 0.24f * Mathf.Sin(j * 0.95f + 0.4f), 0.07f + j * 0.092f);
        }

        void ShowMap(bool fromTitle)
        {
            Analytics.Track("screen", "name", "map");
            mapFromTitle = fromTitle || ui.title.gameObject.activeSelf;
            HideModal(); CloseMenu();
            var R = ui.map; foreach (Transform c in R) Destroy(c.gameObject);
            R.gameObject.SetActive(true); SetHud(false);
            UIKit.Img(R, "bg", null, UIKit.Hex("47bfe4"), true).rectTransform.Stretch(-50, -50, -50, -50);
            var S = SafeRect(R, "safe");
            int N = LEVELS.Length, nIsle = (N + PER_ISLE - 1) / PER_ISLE;

            // ---- 滚动区（先建，顶栏盖在它上面）----
            var view = UIKit.Rect("view", S).Stretch(0, 0, 0, 0);
            view.gameObject.AddComponent<RectMask2D>();
            view.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            var content = UIKit.Rect("content", view);
            const float PAD = 0;
            float H = PAD * 2 + nIsle * ISLE_H + 120;
            content.anchorMin = new Vector2(0, 0); content.anchorMax = new Vector2(1, 0); content.pivot = new Vector2(0.5f, 0);
            content.anchoredPosition = Vector2.zero; content.sizeDelta = new Vector2(0, H);
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.content = content; sr.viewport = view; sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped; sr.inertia = true; sr.decelerationRate = 0.135f; sr.scrollSensitivity = 40;

            // 每座岛：插画（等比铺满这一段的高度，左右多出来的裁掉）+ 岛名丝带
            var imgW = new float[nIsle];
            for (int k = 0; k < nIsle; k++)
            {
                var I = ISLES[k % ISLES.Length];
                float y0 = PAD + k * ISLE_H;
                string mk = "map" + (MapArt(k) + 1);
                if (Skin.Has(mk))
                {
                    var im = Skin.Img(content, mk); var sp = im.sprite;
                    imgW[k] = ISLE_H * sp.rect.width / sp.rect.height;
                    im.preserveAspect = false;
                    im.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, y0), new Vector2(imgW[k], ISLE_H + 2));
                }
                else
                {
                    imgW[k] = 390;
                    var band = UIKit.Img(content, "isle" + k, null, UIKit.Hex(I.band)); band.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, y0), new Vector2(390, ISLE_H));
                }
                RectTransform nm;
                if (Skin.Has("ribbon_blue")) { nm = Skin.Img(content, "ribbon_blue").rectTransform; nm.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, y0 + 64), new Vector2(300, 69)); }
                else nm = UIKit.Panel(content, "name" + k, UIKit.Hex("14608f"), 3, 3).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, y0 + 64), new Vector2(260, 40));
                var nt = UIKit.Txt(nm, "t", F("第 {0} 岛 · {1}", k + 1, T(I.name)), 18, Color.white); nt.rectTransform.Stretch(40, 29, 40, 5);   // 拱形丝带：字对准带面（中心在上面 31% 处）
                nt.horizontalOverflow = HorizontalWrapMode.Wrap; nt.resizeTextForBestFit = true; nt.resizeTextMinSize = 11; nt.resizeTextMaxSize = 18;
                Styled(nt, UIKit.Hex("123f7a"), 1.8f, 1.5f);
            }
            // 两座岛之间接缝：一条渐变的海水遮一下
            for (int k = 1; k < nIsle; k++)
            {
                var seam = UIKit.Img(content, "seam", SeamSprite(), UIKit.Hex("5fd3ec"));
                seam.rectTransform.anchorMin = new Vector2(0, 0); seam.rectTransform.anchorMax = new Vector2(1, 0); seam.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                seam.rectTransform.anchoredPosition = new Vector2(0, PAD + k * ISLE_H); seam.rectTransform.sizeDelta = new Vector2(0, 110);
            }
            Vector2 NodePos(int i)
            {
                int k = i / PER_ISLE; var uv = NodeUV(MapArt(k), i % PER_ISLE);
                return new Vector2(Mathf.Clamp((uv.x - 0.5f) * imgW[k], -150, 150), PAD + k * ISLE_H + uv.y * ISLE_H);
            }
            int cur = save.top >= N ? -1 : CurLevel;
            mapCur = null; mapPin = null;
            bool skin = Skin.Has("node_green");
            for (int i = 0; i < N; i++)
            {
                bool done = i < save.top, isCur = i == cur, locked = i > save.top;
                float sz = isCur ? 96 : 80;
                RectTransform n; Text t = null;
                if (skin)
                {
                    n = Skin.Img(content, locked ? "node_grey" : isCur ? "node_gold" : "node_green").rectTransform;
                    n.name = "lv" + (i + 1); n.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), NodePos(i), new Vector2(sz, sz));
                    if (!locked)
                    {
                        t = UIKit.Txt(n, "n", (i + 1).ToString(), isCur ? 30 : 25, isCur ? UIKit.Hex("6b3d0c") : Color.white);
                        t.rectTransform.Stretch(0, sz * 0.3f, 0, 0);
                        if (!isCur) Styled(t, UIKit.Hex("1f6b18"), 1.8f, 1.5f);
                    }
                }
                else
                {
                    var fill = isCur ? UIKit.Hex("ffc530") : locked ? UIKit.Hex("c9d2da") : UIKit.Hex("3fb950");
                    n = UIKit.Disc(content, "lv" + (i + 1), fill, 5, 3).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), NodePos(i), new Vector2(sz * 0.8f, sz * 0.8f));
                    t = UIKit.Txt(n, "n", (i + 1).ToString(), 24, Color.white); t.rectTransform.Stretch(0, 2, 0, 0);
                }
                int tg = LEVELS[i].cfg.tag;
                if (tg > 0 && !done)
                {
                    var tag = UIKit.Rect("tag", n).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, 8), new Vector2(58, 22));
                    if (Skin.Has("btn_purple")) Skin.Img(tag, tg == 2 ? "btn_grey" : "btn_purple", tg == 2 ? UIKit.Hex("ff6a5a") : Color.white).rectTransform.Stretch();
                    else UIKit.Panel(tag, "p", UIKit.Hex(tg == 2 ? "d5392a" : "7a3dd6"), 0, 2).Stretch();
                    UIKit.Txt(tag, "t", T(tg == 2 ? "超难" : "困难"), 13, Color.white).rectTransform.Stretch(0, 1, 0, 0);
                }
                if (isCur)
                {
                    mapCur = n;
                    if (Skin.Has("pin_red")) { mapPin = Skin.Img(content, "pin_red").rectTransform; mapPin.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), NodePos(i) + new Vector2(0, sz * 0.38f), new Vector2(48, 60)); mapPinY0 = mapPin.anchoredPosition.y; }
                }
                int ii = i;
                if (!locked) UIKit.Btn(n, () => { sfx.Play("click"); PlayLevel(ii); });
            }
            // 没解锁的岛盖一层暗色
            for (int k = 1; k < nIsle; k++)
            {
                int first = k * PER_ISLE; if (first <= save.top) continue;
                float y0 = PAD + k * ISLE_H;
                var lk = UIKit.Img(content, "lock" + k, null, UIKit.Hex("1d2c3c", 0.38f));
                lk.rectTransform.anchorMin = new Vector2(0, 0); lk.rectTransform.anchorMax = new Vector2(1, 0); lk.rectTransform.pivot = new Vector2(0.5f, 0);
                lk.rectTransform.offsetMin = new Vector2(0, y0 + 40); lk.rectTransform.offsetMax = new Vector2(0, y0 + ISLE_H - 110);
                var tag = UIKit.Panel(content, "lockT" + k, UIKit.Hex("281c10", 0.85f), 0, 0).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, y0 + ISLE_H * 0.45f), new Vector2(240, 40));
                UIKit.Txt(tag, "t", F("通关第 {0} 关解锁", first), 16, Color.white).rectTransform.Stretch();
            }

            // ---- 顶栏：奶白长牌 + 左边蓝色返回圆钮 + 中间"关卡地图" + 右边星星数 ----
            var head = UIKit.Rect("head", S).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(370, 58));
            RectTransform ht;
            if (Skin.Has("plate_cream")) { ht = Skin.Img(head, "plate_cream").rectTransform; ht.raycastTargetOn(); ht.Stretch(20, 2, 0, 2); }
            else { ht = UIKit.Panel(head, "t", UIKit.Cream, 4); ht.Stretch(20, 2, 0, 2); }
            var title = UIKit.Txt(ht, "n", T("关卡地图"), 24, UIKit.Hex("6b3d14"), TextAnchor.MiddleCenter); title.rectTransform.Stretch(40, 2, 40, 0);
            var starI = UIKit.Rect("star", ht).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-62, 0), new Vector2(32, 32));
            if (Skin.Has("star_gold")) Skin.Img(starI, "star_gold").rectTransform.Stretch();
            UIKit.Txt(ht, "s", save.star.ToString(), 20, UIKit.Hex("6b3d14"), TextAnchor.MiddleLeft).rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(48, 30));
            IconBtn(head, "icon_back", new Vector2(0, 0.5f), new Vector2(26, 0), 58, () => { sfx.Play("click"); MapBack(); }, "<");
            // 测试用：3 秒内连点标题 5 下，全部解锁
            UIKit.Btn(ht, () =>
            {
                if (Time.realtimeSinceStartup - devT > 3) devTaps = 0;
                devT = Time.realtimeSinceStartup;
                if (++devTaps >= 5 && save.top < N - 1) { save.top = N - 1; save.Persist(); ShowMap(mapFromTitle); Toast(T("测试：全部关卡已解锁")); }
            });
            // 滚到当前关
            Canvas.ForceUpdateCanvases();
            float vh = view.rect.height, focus = NodePos(cur >= 0 ? cur : N - 1).y;
            sr.verticalNormalizedPosition = H > vh ? Mathf.Clamp01((focus - vh * 0.42f) / (H - vh)) : 0;
            // 刚解锁的那一关：先盖一个灰色带锁的节点，停一下，"啪"地碎开，露出下面金色的新关，星星往外飞
            if (mapUnlockAnim >= 0 && mapUnlockAnim == cur && mapCur && Skin.Has("node_grey"))
            {
                mapUnlockAnim = -1;
                var ov = Skin.Img(content, "node_grey").rectTransform; ov.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), NodePos(cur), new Vector2(96, 96));
                var cg = ov.gameObject.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false;
                if (mapPin) mapPin.gameObject.SetActive(false);
                Vector2 at = NodePos(cur);
                Fx.Run(async () =>
                {
                    await Fx.Sleep(650); if (!ov) return;
                    await Fx.Tween(160, p => { if (ov) ov.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(p * Mathf.PI * 4) * 8); });   // 抖两下
                    if (!ov) return;
                    sfx.Play("unlock"); sfx.Play("star3"); Haptics.Play(Haptics.Kind.Success);
                    for (int k = 0; k < 10; k++)
                    {
                        var st_ = Skin.Img(content, "star_gold").rectTransform; st_.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), at, new Vector2(30, 30));
                        float a = k * Mathf.PI * 2 / 10 + 0.3f; var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Fx.Run(() => Fx.Tween(600, p => { if (!st_) return; st_.anchoredPosition = at + dir * (30 + 90 * p); st_.localScale = Vector3.one * (1 - p * 0.7f); st_.GetComponent<Image>().color = new Color(1, 1, 1, 1 - p); if (p >= 1) Destroy(st_.gameObject); }));
                    }
                    await Fx.Tween(320, p => { if (!ov) return; ov.localScale = Vector3.one * (1 + 0.5f * p); cg.alpha = 1 - p; });
                    if (ov) Destroy(ov.gameObject);
                    if (mapPin) { mapPin.gameObject.SetActive(true); var y1 = mapPinY0; Fx.Run(() => Fx.Tween(400, p => { if (mapPin) mapPin.anchoredPosition = new Vector2(mapPin.anchoredPosition.x, y1 + (1 - Fx.BackOut(p)) * 120); })); }
                });
            }
            else if (mapUnlockAnim >= 0 && mapUnlockAnim != cur) mapUnlockAnim = -1;
        }
        RectTransform mapPin; float mapPinY0;
        int mapUnlockAnim = -1;     // 刚通关解锁的那一关：下次打开地图时播解锁动画（WinFx 里设）
        static Sprite seamSprite;
        static Sprite SeamSprite()
        {
            if (seamSprite) return seamSprite;
            var t = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++) { float k = 1 - Mathf.Abs(y / 63f * 2 - 1); t.SetPixel(0, y, new Color(1, 1, 1, Mathf.SmoothStep(0, 0.85f, k))); }
            t.Apply();
            return seamSprite = Sprite.Create(t, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f));
        }
        void HideMap() { ui.map.gameObject.SetActive(false); mapCur = null; mapPin = null; }
        void MapBack()
        {
            HideMap();
            if (mapFromTitle) ShowTitle();
            else SetHud(true);
        }

        // ======================================================================
        // 设置
        void ShowSettings(bool pop = true)
        {
            if (pop) Analytics.Track("screen", "name", "settings");
            CloseMenu();
            Modal(true, pop);
            ModalTitle(T("设置"), "settings");
            string sep = En ? ": " : "：";
            void Toggle(string name, Func<bool> get, Action<bool> set, string key)
            {
                Button b = null;
                string Txt() => T(name) + sep + T(get() ? "开" : "关");
                b = MButton(Txt(), get() ? "3fb950" : "b9b0a0", () =>
                {
                    set(!get()); save.Persist(); ApplySettings(); sfx.Play("click");
                    Analytics.Track("setting", "key", key, "value", get() ? "on" : "off");
                    ShowSettings(false);
                }, true, "ffffff", 17, 48);
            }
            Toggle("音乐", () => save.music, v => save.music = v, "music");
            Toggle("音效", () => save.sfx, v => save.sfx = v, "sfx");
            Toggle("震动", () => save.vibe, v => { save.vibe = v; Haptics.On = v; if (v) Haptics.Play(Haptics.Kind.Medium); }, "vibe");
            MButton(T("语言") + sep + (En ? "English" : "中文"), "14608f", () =>
            {
                sfx.Play("click");
                save.lang = En ? "zh" : "en"; save.Persist();
                Analytics.Track("setting", "key", "lang", "value", save.lang);
                Relocalize(); ShowSettings();
            }, true, "ffffff", 17, 48);
            MSpace(4);
            bool armed = false; Button rb = null;
            rb = MButton(T("清空进度"), "fff8e6", () =>
            {
                if (!armed) { armed = true; rb.GetComponentInChildren<Text>().text = T("再点一次确认清空"); rb.GetComponentInChildren<Text>().color = UIKit.Hex("d5392a"); return; }
                Analytics.Track("setting", "key", "reset", "value", save.top);
                save = save.Reset(); save.Persist(); HideModal(); HideMap();
                levelIdx = 0; ShowTitle(); Relocalize();
            }, true, "7a4d21", 14, 38);
            MText(F("版本 {0}", Application.version), 12, UIKit.Hex("a08a6a"));
            MButton(T("关闭"), "3fb950", () => { sfx.Play("click"); HideModal(); }, true, "ffffff", 17, 46);
            FinishModal();
        }

        // 换语言：整个界面重搭一遍（3D 场景里的小标签也要重做，所以当前关重新开始）
        void Relocalize()
        {
            Loc.Set(save.lang);
            bool t = ui.title.gameObject.activeSelf, m = ui.map.gameObject.activeSelf, h = homeOpen; string hf = homeFrom;
            if (h) HideHome();
            ClearLabels(); HideHand();
            Destroy(ui.canvas.gameObject);
            ui = new UIRefs(); menuOpen = false;
            BuildUI();
            if (t) ShowTitle();
            else if (m) ShowMap(mapFromTitle);
            LoadLevel(Mathf.Clamp(levelIdx, 0, LEVELS.Length - 1));
            if (!t && !m) SetHud(true);
            if (h) ShowHome(hf);
        }

        // 安卓返回键 / 电脑上的 Esc
        void Back()
        {
            if (ui.ad.gameObject.activeSelf) return;
            if (menuOpen) { CloseMenu(); return; }
            if (ModalOpen) { if (modalClosable) HideModal(); return; }
            if (homeOpen) { HomeBack(); return; }
            if (ui.map.gameObject.activeSelf) { MapBack(); return; }
            if (ui.title.gameObject.activeSelf)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                Application.Quit();
#endif
                return;
            }
            ShowMap(false);
        }

        void UpdateScreens()
        {
            float s = 1 + 0.06f * Mathf.Sin(Fx.Now / 160);
            if (mapCur) mapCur.localScale = Vector3.one * s;
            if (mapPin) mapPin.anchoredPosition = new Vector2(mapPin.anchoredPosition.x, mapPinY0 + Mathf.Abs(Mathf.Sin(Fx.Now / 260)) * 10);
            if (titlePlay) titlePlay.localScale = Vector3.one * (1 + 0.035f * Mathf.Sin(Fx.Now / 220));
            float dp = 1 + 0.15f * Mathf.Abs(Mathf.Sin(Fx.Now / 200));
            foreach (var d in new[] { titleDot, signDot, taskDot }) if (d) d.localScale = Vector3.one * dp;
            if (titleBoat) titleBoat.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Fx.Now / 900) * 1.5f);   // 标志牌轻轻晃
        }

        void OnApplicationPause(bool paused) { if (paused) { save?.Persist(); Analytics.Flush(); } }
        void OnApplicationQuit() { Analytics.Flush(); }
    }
}
