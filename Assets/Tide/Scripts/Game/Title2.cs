// 开始界面第 3 版（10-23）：用户嫌第 2 版的写实小孩、老爷爷和游戏里的 Q 版小人风格不搭，换成 art/ai/concept/ingame/T2.png——
//   游戏里同款的 Q 版大头小人 + 玩具船 + 红色圆胖直升机。贴图 bg3 / e3_*（有 bg3 就用第 3 版，没有退回第 2 版）：
//   生气的乌云（一胀一缩 + 下雨 + 闪电）、左上直升机（悬停 + 旋翼转）下面吊一筏小人来回荡、中间大黄游艇挤满欢呼的小人（随浪摇）、前景海浪。
// 开始界面第 2 版：照 art/ai/concept/title2_e.png「风暴前夕」（用户 10-21 定的）。
// 背景是一张不带人的静态插画（skin/bg_storm），上面一层层叠会动的贴纸：
//   左边沙滩上 4 个着急挥手的岛民（一蹦一蹦）、右边带脸的大乌云（一胀一缩 + 下雨 + 闪电）、
//   中间赶来的直升机（悬停晃 + 旋翼转）和黄色救援船（随浪摇）、海鸥飞过、前景两层海浪来回涌。
// 贴纸的位置用背景图上的比例坐标（u 从左到右 0~1，v 从上到下 0~1），背景怎么裁切它们都跟着对齐。
// 界面照欧美休闲游戏的排法：顶栏（头像、金币、星星、设置）→ 标志 → 两侧活动入口（签到、任务 | 宝箱、连胜）
//   → 绿色大关卡按钮 → 底部导航栏（地图 | 开始 | 小岛）。
using UnityEngine;
using UnityEngine.UI;
using static Tide.Loc;

namespace Tide
{
    public partial class Game
    {
        static readonly Color NAVY = UIKit.Hex("17306b");

        // 贴纸：名字、中心 (u,v)、宽（占背景宽的比例）、动法
        static readonly (string name, float u, float v, float w, TitleAnim.Kind kind)[] TITLE_SCENE = {
            ("e_cloud", 0.75f, 0.395f, 0.44f, TitleAnim.Kind.Cloud),
            ("e_heli",  0.40f, 0.40f, 0.28f, TitleAnim.Kind.Hover),
            ("e_boat",  0.70f, 0.67f, 0.34f, TitleAnim.Kind.Rock),
            ("e_folk2", 0.27f, 0.585f, 0.13f, TitleAnim.Kind.Hop),
            ("e_folk1", 0.15f, 0.595f, 0.14f, TitleAnim.Kind.Hop),
            ("e_folk4", 0.37f, 0.615f, 0.105f, TitleAnim.Kind.Hop),
            ("e_folk3", 0.22f, 0.635f, 0.12f, TitleAnim.Kind.Hop),
        };

        static readonly (string name, float u, float v, float w, TitleAnim.Kind kind)[] TITLE_SCENE3 = {
            ("e3_cloud", 0.79f, 0.335f, 0.42f, TitleAnim.Kind.Cloud),
            ("e3_heli",  0.36f, 0.37f, 0.24f, TitleAnim.Kind.Hover),
            ("e3_boat",  0.665f, 0.59f, 0.48f, TitleAnim.Kind.Rock),
        };

        // 背景图上放一张贴纸：(u,v) 是中心，w 是宽占背景宽的比例，高按贴图比例算
        static RectTransform SceneSprite(RectTransform bg, string name, float u, float v, float w, float bgAspect)
        {
            var im = Skin.Img(bg, name); var r = im.rectTransform;
            var sp = im.sprite; float h = w * bgAspect * sp.rect.height / sp.rect.width;   // 换算成占背景高的比例
            r.anchorMin = new Vector2(u - w / 2, 1 - v - h / 2); r.anchorMax = new Vector2(u + w / 2, 1 - v + h / 2);
            r.offsetMin = r.offsetMax = Vector2.zero; im.preserveAspect = true;
            return r;
        }

        static readonly Vector2 ROTOR3 = new Vector2(0.585f, 0.955f);     // 第 3 版直升机旋翼轴在贴图上的位置（比例）
        static readonly Vector2 RAFT3 = new Vector2(0.55f, 0.22f);      // 吊绳挂在直升机贴图上的点
        const float RAFT3_W = 0.66f;                                    // 筏子宽 = 直升机宽 × 这个数
        void TitleRaft(RectTransform heli)
        {
            if (!Skin.Has("e3_raft")) return;
            var im = Skin.Img(heli, "e3_raft"); var r = im.rectTransform; r.SetAsFirstSibling();     // 画在直升机后面：吊绳从机肚子底下出来
            var hs = heli.GetComponent<Image>().sprite; var rs = im.sprite;
            float hh = RAFT3_W * (hs.rect.width / hs.rect.height) * (rs.rect.height / rs.rect.width);     // 筏子高占直升机高的比例
            r.anchorMin = new Vector2(RAFT3.x - RAFT3_W / 2, RAFT3.y - hh); r.anchorMax = new Vector2(RAFT3.x + RAFT3_W / 2, RAFT3.y);
            r.offsetMin = r.offsetMax = Vector2.zero; r.pivot = new Vector2(0.5f, 1);
            TitleAnim.Add(r, TitleAnim.Kind.Swing, 0.4f);
        }

        bool TitleV2(RectTransform R)
        {
            bool v3 = Skin.Has("bg3");
            if (!v3 && !Skin.Has("bg_storm")) return false;
            // ---- 背景 + 会动的场景 ----
            var bgIm = Skin.Img(R, v3 ? "bg3" : "bg_storm"); bgIm.raycastTarget = true; bgIm.preserveAspect = false;
            var fit = bgIm.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            float asp = bgIm.sprite.rect.width / bgIm.sprite.rect.height; fit.aspectRatio = asp;
            var bg = bgIm.rectTransform;
            // 雨（乌云下面一片斜的细线往下落）
            if (Skin.Has(v3 ? "e3_cloud" : "e_cloud"))
            {
                var rain = UIKit.Rect("rain", bg); rain.anchorMin = new Vector2(0.58f, v3 ? 0.40f : 0.32f); rain.anchorMax = new Vector2(1.02f, v3 ? 0.62f : 0.62f); rain.offsetMin = rain.offsetMax = Vector2.zero;
                for (int i = 0; i < 26; i++)
                {
                    var d = UIKit.Img(rain, "d", null, new Color(0.85f, 0.92f, 1f, 0.45f)).rectTransform;
                    d.anchorMin = d.anchorMax = new Vector2(Random.value, Random.value); d.sizeDelta = new Vector2(2, 16 + Random.value * 10);
                    d.localRotation = Quaternion.Euler(0, 0, -14);
                    TitleAnim.Add(d, TitleAnim.Kind.Rain, Random.value * 10);
                }
            }
            // 闪电（在乌云后面亮）
            if (Skin.Has("e_bolt")) TitleAnim.Add(SceneSprite(bg, "e_bolt", v3 ? 0.86f : 0.88f, v3 ? 0.46f : 0.52f, v3 ? 0.10f : 0.12f, asp), TitleAnim.Kind.Bolt, 0);
            int fk = 0;
            foreach (var s in v3 ? TITLE_SCENE3 : TITLE_SCENE)
            {
                if (!Skin.Has(s.name)) continue;
                var r = SceneSprite(bg, s.name, s.u, s.v, s.w, asp);
                if (s.kind == TitleAnim.Kind.Hop) r.pivot = new Vector2(0.5f, 0);      // 脚底为轴：蹦、压扁都从脚下算
                TitleAnim.Add(r, s.kind, s.kind == TitleAnim.Kind.Hop ? fk++ * 0.37f : 0);
                if (s.name == "e3_heli") TitleRaft(r);
                if (s.name == "e_heli") r.localScale = new Vector3(-1, 1, 1);      // 原图机头朝右，翻过来朝沙滩上的岛民飞
                if ((s.name == "e_heli" || s.name == "e3_heli") && Skin.Has("e_rotor"))
                {
                    var rot = Skin.Img(r, "e_rotor").rectTransform;
                    if (s.name == "e3_heli") { rot.anchorMin = new Vector2(ROTOR3.x - 0.55f, ROTOR3.y - 0.05f); rot.anchorMax = new Vector2(ROTOR3.x + 0.55f, ROTOR3.y + 0.05f); }
                    else { rot.anchorMin = new Vector2(0.07f, 0.90f); rot.anchorMax = new Vector2(1.17f, 1.01f); }      // 旋翼轴在机身 62% 处 rot.offsetMin = rot.offsetMax = Vector2.zero;
                    TitleAnim.Add(rot, TitleAnim.Kind.Rotor, 0);
                }
            }
            if (!v3 && Skin.Has("e_gull"))
                for (int i = 0; i < 2; i++) TitleAnim.Add(SceneSprite(bg, "e_gull", 0.5f, 0.24f + i * 0.09f, 0.08f - i * 0.02f, asp), TitleAnim.Kind.Gull, i * 5.3f);
            string wave = v3 && Skin.Has("e3_wave") ? "e3_wave" : "e_wave";
            if (Skin.Has(wave))
                for (int i = 0; i < 2; i++)
                {
                    var w = SceneSprite(bg, wave, 0.5f, 0.90f + i * 0.05f, 1.3f, asp);
                    if (i == 0) w.GetComponent<Image>().color = new Color(0.7f, 0.82f, 0.92f);
                    TitleAnim.Add(w, TitleAnim.Kind.Wave, i * 2.1f);
                }

            // ---- 界面 ----
            var S = SafeRect(R, "safe");
            // 顶栏：头像（点开岛民图鉴，角标 = 救下来几位）、金币、星星、设置
            var av = Icon(S, v3 && Skin.Has("e3_avatar") ? "e3_avatar" : "ui_avatar", new Vector2(0, 1), new Vector2(38, -36), 58);
            if (save.top >= FEAT_HOME)
            {
                int gotN = 0; for (int k = 0; k < Mathf.Min(COLLECT.Length, LEVELS.Length); k++) if (save.Got(k)) gotN++;
                var bd = UIKit.Rect("badge", av).Place(new Vector2(1, 0), new Vector2(0.5f, 0.5f), new Vector2(-6, 8), new Vector2(26, 26));
                UIKit.Img(bd, "d", UIKit.Circle, UIKit.Hex("2f7be0")).rectTransform.Stretch();
                UIKit.Img(bd, "r", UIKit.Ring, Color.white).rectTransform.Stretch();
                NavyTxt(bd, gotN.ToString(), 13).rectTransform.Stretch(0, 1, 0, 0);
                UIKit.Btn(av, () => { sfx.Play("click"); ShowAlbum(); });
            }
            Counter(S, "ui_coin", save.coins.ToString("N0"), new Vector2(-44, -36), 112);
            Counter(S, Skin.Has("star_gold") ? "star_gold" : "ui_coin", save.star.ToString(), new Vector2(66, -36), 84);
            UIKit.Btn(Icon(S, "ui_gear", new Vector2(1, 1), new Vector2(-36, -36), 52), () => { sfx.Play("click"); ShowSettings(); });

            // 标志
            string logo = En || !Skin.Has("logo2_zh") ? "logo2_en" : "logo2_zh";
            if (Skin.Has(logo)) { titleBoat = Skin.Img(S, logo).rectTransform; titleBoat.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -64), new Vector2(262, 160)); }

            // 两侧活动入口
            titleSignBtn = titleTaskBtn = null;
            if (save.top >= FEAT_DAILY)
            {
                signDot = SideBtn2(S, "ui_daily", T("签到"), null, 0, 0, () => { sfx.Play("click"); ShowSign(); });
                taskDot = SideBtn2(S, "ui_tasks", T("任务"), TasksDone + "/3", 0, 1, () => { sfx.Play("click"); ShowTasks(); });
                titleSignBtn = (RectTransform)signDot.parent; titleTaskBtn = (RectTransform)taskDot.parent;
                UpdateDailyDots(); MaybeAutoSign();
            }
            if (save.top >= FEAT_HOME)
            {
                var cd = SideBtn2(S, "ui_chest", T("宝箱"), ChestText, 1, 0, () =>
                {
                    sfx.Play("click");
                    if (ChestReady > 0) OpenChest(); else Toast(F("再拿 {0} 颗星就能开宝箱", CHEST_EVERY - save.starGot % CHEST_EVERY));
                });
                cd.gameObject.SetActive(ChestReady > 0);
            }
            if (save.top >= UNLOCK[0])
                SideBtn2(S, "ui_streak", F("连胜 ×{0}", save.streak), null, 1, 1, () => { sfx.Play("click"); ShowStreakCard(); }).gameObject.SetActive(false);

            // 关卡大按钮
            bool all = save.top >= LEVELS.Length;
            System.Action go = () => { sfx.Play("click"); if (all) ShowMap(true); else PlayLevel(CurLevel); };
            titlePlay = SkinBtn(S, "3fb950", all ? T("全部通关！") : F("第 {0} 关", CurLevel + 1), 34, go, out var pl);
            titlePlay.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 156), new Vector2(236, 76));
            pl.rectTransform.offsetMin = new Vector2(14, 8);

            // 底部导航栏：地图 | 开始（选中、凸起） | 小岛
            var nav = UIKit.Rect("nav", R).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 0));
            nav.anchorMin = new Vector2(0, 0); nav.anchorMax = new Vector2(1, 0); nav.sizeDelta = new Vector2(0, 76 + SafeBottom);
            if (Skin.Has("ui_navbar")) Skin.Img(nav, "ui_navbar").rectTransform.Stretch(-6, -20, -6, 0);
            else UIKit.Img(nav, "bar", null, NAVY).rectTransform.Stretch();
            var navIn = UIKit.Rect("in", nav).Stretch(0, SafeBottom, 0, 0);
            NavTab(navIn, "ui_map", T("地图"), -1, () => { sfx.Play("click"); ShowMap(true); });
            var tab = UIKit.Rect("tab", navIn).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 2), new Vector2(112, 96));
            if (Skin.Has("ui_tab")) Skin.Img(tab, "ui_tab").rectTransform.Stretch();
            var pi = Icon(tab, "ui_play", new Vector2(0.5f, 1), new Vector2(0, -36), 60); pi.gameObject.AddComponent<UIBob>();
            NavyTxt(tab, T("开始"), 17).rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(110, 24));
            UIKit.Btn(tab, go);
            titleHomeBtn = null;
            var hb = NavTab(navIn, "ui_hut", T("小岛"), 1, () =>
            {
                sfx.Play("click");
                if (save.top >= FEAT_HOME) ShowHome("title"); else Toast(F("通关第 {0} 关解锁", FEAT_HOME));
            });
            if (save.top >= FEAT_HOME)
            {
                titleHomeBtn = hb;
                if (HomeNews)
                {
                    var dot = UIKit.Rect("dot", hb).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(26, -6), new Vector2(24, 24));
                    if (Skin.Has("badge_red")) Skin.Img(dot, "badge_red").rectTransform.Stretch(); else UIKit.Img(dot, "d", UIKit.Circle, UIKit.Hex("e8402f")).rectTransform.Stretch();
                    UIKit.Txt(dot, "t", "!", 15, Color.white).rectTransform.Stretch(0, 1, 0, 0);
                    titleDot = dot;
                }
            }
            else foreach (var g in hb.GetComponentsInChildren<Graphic>()) g.color = new Color(g.color.r * 0.55f, g.color.g * 0.6f, g.color.b * 0.7f, g.color.a);
            UIKit.Txt(S, "ver", "v" + Application.version, 10, new Color(1, 1, 1, 0.6f), TextAnchor.LowerRight).rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-8, 96), new Vector2(120, 14));
            return true;
        }

        float SafeBottom { get { var c = ui.canvas.GetComponent<RectTransform>(); return SafeArea.yMin / Screen.height * c.rect.height; } }

        string ChestText => ChestReady > 0 ? T("开宝箱！") : (save.starGot % CHEST_EVERY) + "/" + CHEST_EVERY;

        RectTransform Icon(Transform p, string name, Vector2 anchor, Vector2 pos, float size)
        {
            var r = UIKit.Rect(name, p).Place(anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            if (Skin.Has(name)) Skin.Img(r, name).rectTransform.Stretch(); else UIKit.Img(r, "d", UIKit.Circle, UIKit.Hex("2f7be0")).rectTransform.Stretch();
            return r;
        }

        Text NavyTxt(Transform p, string s, int size)
        {
            var t = UIKit.Txt(p, "t", s, size, Color.white); Styled(t, NAVY, 2f, 1.5f); return t;
        }

        // 顶栏的数值条：深蓝胶囊 + 左边压一个大图标
        void Counter(Transform S, string icon, string val, Vector2 pos, float w)
        {
            var r = UIKit.Rect("cnt", S).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), pos, new Vector2(w, 32));
            if (Skin.Has("ui_pill")) Skin.Img(r, "ui_pill").rectTransform.Stretch(); else UIKit.Img(r, "p", UIKit.Round, new Color(0.08f, 0.15f, 0.35f, 0.75f)).rectTransform.Stretch();
            Icon(r, icon, new Vector2(0, 0.5f), new Vector2(4, 0), 40);
            var t = NavyTxt(r, val, 17); t.rectTransform.Stretch(26, 0, 4, 0);
        }

        // 两侧的圆形活动按钮：图标（自带蓝色圆底座）+ 下面白字 + 可选的小进度条；返回右上角的红点
        RectTransform SideBtn2(Transform S, string icon, string label, string prog, int side, int row, System.Action fn)
        {
            float x = side == 0 ? 40 : -40, y = -150 - row * 92;
            var b = Icon(S, icon, new Vector2(side, 1), new Vector2(x, y), 64);
            var l = NavyTxt(b, label, 14); l.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(110, 22));
            if (prog != null)
            {
                var pr = UIKit.Rect("prog", b).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, -18), new Vector2(64, 18));
                if (Skin.Has("ui_pill")) Skin.Img(pr, "ui_pill").rectTransform.Stretch(); else UIKit.Img(pr, "p", UIKit.Round, NAVY).rectTransform.Stretch();
                NavyTxt(pr, prog, 12).rectTransform.Stretch(0, 1, 0, 0);
            }
            var dot = UIKit.Rect("dot", b).Place(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-8, -8), new Vector2(24, 24));
            if (Skin.Has("badge_red")) Skin.Img(dot, "badge_red").rectTransform.Stretch(); else UIKit.Img(dot, "d", UIKit.Circle, UIKit.Hex("e8402f")).rectTransform.Stretch();
            UIKit.Txt(dot, "t", "!", 15, Color.white).rectTransform.Stretch(0, 1, 0, 0);
            UIKit.Btn(b, fn);
            return dot;
        }

        RectTransform NavTab(Transform nav, string icon, string label, int side, System.Action fn)
        {
            var r = UIKit.Rect("tab" + side, nav).Place(new Vector2(0.5f + side * 0.33f, 0), new Vector2(0.5f, 0), new Vector2(0, 2), new Vector2(110, 72));
            var g = UIKit.Img(r, "hit", null, new Color(0, 0, 0, 0), true); g.rectTransform.Stretch();
            Icon(r, icon, new Vector2(0.5f, 1), new Vector2(0, -24), 54);
            NavyTxt(r, label, 15).rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 2), new Vector2(110, 22));
            UIKit.Btn(r, fn);
            return r;
        }
    }

    // 开始界面贴纸的循环小动画（不受暂停影响）
    public class TitleAnim : MonoBehaviour
    {
        public enum Kind { Hop, Cloud, Hover, Rotor, Rock, Gull, Wave, Bolt, Rain, Swing }
        Kind kind; float ph; RectTransform r; Vector2 p0; Vector3 s0; Graphic g;

        public static void Add(RectTransform r, Kind k, float phase)
        {
            var a = r.gameObject.AddComponent<TitleAnim>(); a.kind = k; a.ph = phase; a.r = r;
            a.p0 = r.anchoredPosition; a.s0 = r.localScale; a.g = r.GetComponent<Graphic>();
        }

        void Update()
        {
            float t = Time.unscaledTime + ph, PI2 = Mathf.PI * 2;
            switch (kind)
            {
                case Kind.Hop:          // 着急地一蹦一蹦 + 左右扭，落地压扁一下
                {
                    float c = (t * 1.6f) % 1f, up = Mathf.Max(0, Mathf.Sin(c * PI2));
                    r.anchoredPosition = p0 + new Vector2(0, up * r.rect.height * 0.07f);
                    r.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 3.2f) * 5);
                    float sq = up < 0.05f ? 0.94f : 1;
                    r.localScale = new Vector3(s0.x * (2 - sq), s0.y * sq, 1);
                    break;
                }
                case Kind.Cloud:        // 气鼓鼓地一胀一缩，慢慢往左压过来又退回
                    r.localScale = s0 * (1 + 0.035f * Mathf.Sin(t * 2.4f));
                    r.anchoredPosition = p0 + new Vector2(Mathf.Sin(t * 0.35f) * 10, Mathf.Sin(t * 0.9f) * 3);
                    break;
                case Kind.Hover:        // 直升机悬停：上下飘 + 机头点一点
                    r.anchoredPosition = p0 + new Vector2(Mathf.Sin(t * 0.7f) * 6, Mathf.Sin(t * 2.2f) * 7);
                    r.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 1.1f) * 3);
                    break;
                case Kind.Swing:        // 吊着的筏子：绕吊绳顶点来回荡
                    r.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 1.7f) * 7);
                    break;
                case Kind.Rotor:        // 旋翼：横向缩放来回翻，看起来在转
                    r.localScale = new Vector3(Mathf.Lerp(0.15f, 1f, Mathf.Abs(Mathf.Cos(t * 22))), 1, 1);
                    break;
                case Kind.Rock:         // 船随浪：起伏 + 前后摇
                    r.anchoredPosition = p0 + new Vector2(0, Mathf.Sin(t * 1.8f) * 6);
                    r.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 1.3f) * 4);
                    break;
                case Kind.Gull:         // 海鸥从左飞到右，翅膀扑扇，飞出去再从左边进来
                {
                    var P = (RectTransform)r.parent; float W = P.rect.width, k = (t / 11f) % 1f;
                    r.anchoredPosition = p0 + new Vector2((k - 0.5f) * W * 1.4f, Mathf.Sin(t * 1.5f) * 14);
                    r.localScale = new Vector3(s0.x, s0.y * (0.65f + 0.35f * Mathf.Abs(Mathf.Sin(t * 7))), 1);
                    break;
                }
                case Kind.Wave:         // 前景海浪：左右涌 + 上下起伏
                    r.anchoredPosition = p0 + new Vector2(Mathf.Sin(t * 0.8f) * 22, Mathf.Sin(t * 1.6f) * 6);
                    break;
                case Kind.Bolt:         // 闪电：每 4 秒闪两下
                {
                    float c = t % 4f; bool on = (c > 0.0f && c < 0.09f) || (c > 0.18f && c < 0.3f);
                    if (g) g.color = new Color(1, 1, 1, on ? 1 : 0);
                    break;
                }
                case Kind.Rain:         // 雨点往下落，落出去再从上面来
                {
                    float k = (t * 0.9f) % 1f;
                    r.anchorMin = r.anchorMax = new Vector2(r.anchorMin.x, 1 - k);
                    if (g) g.color = new Color(0.85f, 0.92f, 1f, 0.45f * Mathf.Sin(k * Mathf.PI));
                    break;
                }
            }
        }
    }
}
