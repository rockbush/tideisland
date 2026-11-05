// 混合模式（11-03 用户）：特殊模式里的一个入口，验证"一个机制 8 关、简单 / 困难 / 地狱逐步解锁"能不能提高可玩性。
// 主线一行不动：关卡在 SpSim 里玩（规则见 MixSim.cs），进度单独存在 PlayerPrefs（tide_mix1），不碰主线存档、金币。
//   13 座机制大岛，每座周围一圈 8 个小岛 = 8 关：简单 1~3、困难 4~7、地狱 8
//   解锁：本岛简单通 → 开下一座岛；13 座岛简单全通 → 所有岛的困难一起开；
//         第 1 座岛的地狱 = 它的困难通了就开；后面每座岛的地狱 = 上一座岛的地狱通 + 本岛困难通
//   人数 12 / 30 / 60 / 100 / 150 / 200 / 300 / 500，一波一张新图（涨潮分波），每波大约 50 人
//   解锁时弹大窗（新岛屿 / 困难开启 / 地狱解锁），回到地图云雾散开、小岛一个个弹出来
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static Tide.Loc;

namespace Tide
{
    public class MixAnim : MonoBehaviour { public Action<float> tick; void Update() { tick?.Invoke(Time.unscaledDeltaTime); } }

    public partial class Game
    {
        // ---------- 配置 ----------
        static readonly SpKind[] MIX_K = { SpKind.Drift, SpKind.Loop, SpKind.Fog, SpKind.Ferry, SpKind.Sail, SpKind.Turn, SpKind.Bump, SpKind.Merge, SpKind.Whirl, SpKind.Paint, SpKind.Truck, SpKind.Tilt, SpKind.Rope, SpKind.Sushi };
        static readonly string[] MIX_N = { "漂流码头", "港中岛", "雾港", "中转岛", "风帆港", "转盘港", "碰碰船", "合船港", "漩涡港", "变色门", "货轮航道", "摇摆港", "双生船", "回转码头" };
        static readonly string[] MIX_ONE = { "朝上的船对准漂流岛，就开去漂流岛", "船顺着水流绕圈，碰到空码头就停", "雾里看不清，灯塔扫到才看得见", "点一堆人叫筏子接，自己安排排队顺序", "船没箭头，往哪开看风", "中间转盘每走 2 艘转 90°", "被挡不弹回，把前面的船撞飞", "同色船撞上就合体，只占一个泊位", "开进漩涡，从另一个冒出来", "穿过彩虹门，门是什么色船就变什么色", "找货轮的空档冲过航道", "每走 2 艘，全场的船一起滑到底", "绳子连着的两艘一起走", "码头一直转，船转到队头面前才上人" };
        static readonly string[] MIX_TUT = { "漂流岛会左右漂，等它漂到船头前面再点", "船顺着水流走，先点前面的船", "灯塔扫到才看得见颜色，记住它", "点一堆人，筏子把他们接到排队区", "看右上角的风向，顺风的船才开得动", "每开走 2 艘，中间的转盘转一下", "点手指指着的船，把挡路的船撞飞", "点手指指着的船，撞上同色的船就合体", "点对着漩涡的船，看它从哪冒出来", "穿过彩虹门的船会变成门的颜色", "等货轮开过去再点船", "每开走 2 艘，全场的船一起滑一下", "绳子连着的两艘船要一起开得走", "点船，等它转到队头面前上人" };
        static readonly int[] MIX_DEF = { 13, 15, 14, 13, 13, 14, 15, 17, 13, 13, 13, 12, 14, 13 };   // 每波最多几艘（港湾放得下的）
        class MixTpl { public string t, nw; public int ppl, waves, colors, rocks, poles, weed, locks; public double mys; public bool clock; public float per = 0.75f, start; }
        static readonly MixTpl[] MIX_T = {
            new MixTpl { t = "新手教学", ppl = 12, waves = 1, colors = 2, nw = "手指引导" },
            new MixTpl { t = "自己玩一次", ppl = 30, waves = 1, colors = 3 },
            new MixTpl { t = "熟悉一下", ppl = 60, waves = 2, colors = 5, nw = "涨潮分波" },
            new MixTpl { t = "撑杆跳", ppl = 100, waves = 2, colors = 6, poles = 1, nw = "撑杆跳" },
            new MixTpl { t = "风暴来了", ppl = 150, waves = 3, colors = 6, poles = 1, rocks = 2, clock = true, nw = "风暴倒计时、石头" },
            new MixTpl { t = "看不清的船", ppl = 200, waves = 4, colors = 6, poles = 1, rocks = 2, mys = 0.35, clock = true, nw = "神秘白船" },
            new MixTpl { t = "人潮", ppl = 300, waves = 6, colors = 7, poles = 3, rocks = 3, mys = 0.35, clock = true, nw = "多个撑杆跳、人数暴增" },
            new MixTpl { t = "地狱", ppl = 500, waves = 10, colors = 8, poles = 2, rocks = 3, mys = 0.45, weed = 1, locks = 1, clock = true, per = 0.6f, start = 50, nw = "海藻、钥匙锁链、全部叠加" } };
        static readonly (string k, string d)[] MIX_NEWS = {
            ("涨潮分波", "一波船和人救完，会涨潮送来下一波，一关有好几波。"),
            ("撑杆跳", "扛着杆子、一闪一闪的人会撑杆跳：他一上船，队伍里同色的人都撑杆跳上来，把座位补满。"),
            ("风暴倒计时", "顶上的倒计时走完，风暴就来了；每上一个人加一点时间。"),
            ("石头", "港湾里的石头挡路，船开不过去。"),
            ("神秘白船", "白色的船看不出颜色：船头前面的路一通，就露出颜色。"),
            ("人数暴增", "人多、船多、波数多，一队里有好几个撑杆跳。"),
            ("海藻", "被海藻缠住的船，要等别的船再开走几艘才松开（看船上的绿数字）。"),
            ("钥匙锁链", "被锁住的船，要先把挂着钥匙的那艘开走。") };
        static readonly string[] TIER_N = { "简单", "困难", "地狱" }, TIER_C = { "3fb950", "ff9a3c", "a53be0" };
        static int TierOf(int k) => k < 3 ? 0 : k < 7 ? 1 : 2;
        static int TierStart(int t) => t == 0 ? 0 : t == 1 ? 3 : 7;
        static int TierLen(int t) => t == 0 ? 3 : t == 1 ? 4 : 1;
        static float MixAvg(SpKind k) => k == SpKind.Merge ? 3.3f : 4.16f;
        const int MIX_NI = 14;

        // ---------- 进度（单独存，不碰主线）----------
        int[] mixE = new int[MIX_NI], mixH = new int[MIX_NI], mixX = new int[MIX_NI]; bool mixAll, mixLoaded;
        void MixLoad()
        {
            if (mixLoaded) return; mixLoaded = true;
            var s = PlayerPrefs.GetString("tide_mix2", ""); var parts = s.Split('|');
            for (int i = 0; i < MIX_NI && i < parts.Length; i++)
            {
                var a = parts[i].Split(','); if (a.Length < 3) continue;
                int.TryParse(a[0], out mixE[i]); int.TryParse(a[1], out mixH[i]); int.TryParse(a[2], out mixX[i]);
            }
            mixAll = PlayerPrefs.GetInt("tide_mix_all", 0) == 1;
        }
        void MixSave()
        {
            var a = new List<string>(); for (int i = 0; i < MIX_NI; i++) a.Add(mixE[i] + "," + mixH[i] + "," + mixX[i]);
            PlayerPrefs.SetString("tide_mix2", string.Join("|", a)); PlayerPrefs.SetInt("tide_mix_all", mixAll ? 1 : 0); PlayerPrefs.Save();
        }
        int MixDone(int i, int t) => t == 0 ? mixE[i] : t == 1 ? mixH[i] : mixX[i];
        bool HardOpen() { for (int i = 0; i < MIX_NI; i++) if (mixE[i] < 3) return false; return true; }
        // 大地图：岛之间按航线相邻；打通一座岛的简单（第 1~3 关）就开它周围所有的岛，先去哪座自己挑
        static List<int>[] mixNb;
        static List<int> MixNb(int i)
        {
            if (mixNb == null)
            {
                mixNb = new List<int>[MIX_NI]; for (int k = 0; k < MIX_NI; k++) mixNb[k] = new List<int>();
                for (int e = 0; e < MIX_EDGE.GetLength(0); e++) { mixNb[MIX_EDGE[e, 0]].Add(MIX_EDGE[e, 1]); mixNb[MIX_EDGE[e, 1]].Add(MIX_EDGE[e, 0]); }
            }
            return mixNb[i];
        }
        bool MixOpen(int i, int t)
        {
            if (t == 1) return HardOpen();
            if (t == 0) { if (i == 0) return true; foreach (var j in MixNb(i)) if (mixE[j] >= 3) return true; return false; }
            if (mixH[i] < 4) return false;
            if (i == 0) return true; foreach (var j in MixNb(i)) if (mixX[j] >= 1) return true; return false;
        }
        // 相邻岛的名字：优先列已经开了的（玩家能去打的），都没开就全列
        string MixNbNames(int i, int t)
        {
            var l = new List<string>(); foreach (var j in MixNb(i)) if (MixOpen(j, t) || mixAll) l.Add("「" + MIX_N[j] + "」");
            if (l.Count == 0) foreach (var j in MixNb(i)) l.Add("「" + MIX_N[j] + "」");
            return string.Join("或", l);
        }
        bool MixFront(int i) { foreach (var j in MixNb(i)) if (MixOpen(j, 0)) return true; return false; }
        // 2 打通 / 1 现在该打 / -1 这一档开了但前一关没打 / 0 锁着
        int MixLv(int i, int k)
        {
            int t = TierOf(k), j = k - TierStart(t), d = MixDone(i, t);
            if (!MixOpen(i, t)) return 0;
            return j < d ? 2 : j == d ? 1 : -1;
        }
        bool MixPlayable(int i, int k) { int s = MixLv(i, k); return s >= 1 || mixAll; }
        string MixWhy(int i, int k)
        {
            int t = TierOf(k);
            if (!MixOpen(i, t))
            {
                if (t == 0) return "先打通相邻的" + MixNbNames(i, 0) + "的简单关（第 1~3 关）";
                if (t == 1) { int n = 0; for (int j = 0; j < MIX_NI; j++) if (mixE[j] < 3) n++; return MIX_NI + " 座岛的简单全部通关后开（还差 " + n + " 座岛）"; }
                if (mixH[i] < 4) return "先打通这座岛的困难（第 4~7 关）";
                return "先打通相邻的" + MixNbNames(i, 2) + "的地狱";
            }
            return "先打通第 " + (TierStart(t) + MixDone(i, t)) + " 关";
        }

        // ---------- 状态 ----------
        bool mixOn, mixMap, mixBusy, mixCrane; int mixI, mixK, mixWave, mixSaved; float mixT;
        int[] mixUse = { 2, 2, 2 };
        MixAnim mixAnim; readonly List<RectTransform> mixPulse = new List<RectTransform>(); readonly List<(RectTransform r, float y)> mixHands = new List<(RectTransform, float)>();
        RectTransform mixContent, mixView; readonly Dictionary<string, RectTransform> mixLvR = new Dictionary<string, RectTransform>(); readonly Dictionary<int, CanvasGroup> mixFog = new Dictionary<int, CanvasGroup>();
        // 环礁沙盘：每座岛是同一个底座（草地 + 一圈环形沙路），中间放这座岛的主题物件，8 关摆在沙路上
        const float IW = 420, IH = IW * 447f / 640f, RRX = 0.80f * IW / 2, RRY = 0.65f * IH / 2, RCY = 0.03f * IH;
        static readonly float[] MIX_ANG = { 247.5f, 202.5f, 157.5f, 112.5f, 67.5f, 22.5f, -22.5f, -67.5f };   // y 往上，1 号在左下，顺时针
        static float ISc(int i) => i == 0 ? 1.12f : 1f;   // 起点岛大一点
        // 大地图：起点岛在 (0,0)，y 往上；海图 2800x2490（codex 画的整张环礁），屏幕只看得到其中一块，拖着看、捏合缩放
        static readonly Vector2[] MIX_P = {
            new Vector2(0, 0), new Vector2(-505, 128), new Vector2(468, 196), new Vector2(-66, -442), new Vector2(47, 459), new Vector2(-561, -332), new Vector2(515, -272),
            new Vector2(-486, 595), new Vector2(542, 663), new Vector2(-1010, 77), new Vector2(991, 119), new Vector2(-29, -884), new Vector2(-598, -808), new Vector2(561, -748) };
        static readonly int[,] MIX_EDGE = { { 0, 1 }, { 0, 2 }, { 0, 3 }, { 0, 4 }, { 1, 5 }, { 1, 7 }, { 1, 9 }, { 2, 6 }, { 2, 8 }, { 2, 10 }, { 3, 5 }, { 3, 6 }, { 3, 11 }, { 4, 7 }, { 4, 8 }, { 5, 12 }, { 6, 13 }, { 11, 12 }, { 11, 13 }, { 5, 9 }, { 6, 10 } };
        const float MW = 2800, MHt = 2490, MTOP = 1120, MCY = MTOP - MHt / 2;
        // 子物体都按 content 顶边中点摆（anchor (0.5,1)）
        Vector2 MixC(int i) => new Vector2(MIX_P[i].x, MIX_P[i].y - MTOP);
        Vector2 MixSlot(int i, int k) { float a = MIX_ANG[k] * Mathf.Deg2Rad, sc = ISc(i); var c = MixC(i); return new Vector2(c.x + Mathf.Cos(a) * RRX * sc, c.y + (Mathf.Sin(a) * RRY + RCY) * sc); }

        // ---------- 地图 ----------
        RectTransform MImg(Transform p, string name, Sprite s, string hex, Vector2 pos, Vector2 size, float a = 1)
        {
            var im = UIKit.Img(p, name, s, UIKit.Hex(hex, a)); im.raycastTarget = false;
            return im.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), pos, size);
        }

        RectTransform MImgC(Transform p, string name, Sprite s, string hex, Vector2 pos, Vector2 size, float a = 1)
        {
            var im = UIKit.Img(p, name, s, UIKit.Hex(hex, a)); im.raycastTarget = false;
            return im.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        }

        // ---------- 贴图 ----------
        // 混合模式自己的图（codex 照主线的地图、节点、丝带出的）放在 Resources/UI/skin/mix_*，读法和主线一样（Skin.Img）；
        // mix_btn_* 是两头半圆的长条，这里自己按高度九宫格拉伸（主线 Skin 的拉伸表不动）
        static readonly Dictionary<string, Sprite> mixPill = new Dictionary<string, Sprite>();
        static readonly string[] TIER_NODE = { "node_green", "mix_node_orange", "mix_node_purple" }, TIER_BTN = { "btn_green", "mix_btn_orange", "btn_purple" }, TIER_EDGE = { "1f6b18", "8a4306", "4a1a7a" };
        Image MSk(Transform p, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color? tint = null)
        {
            Image im;
            if (name.StartsWith("mix_btn"))
            {
                if (!mixPill.TryGetValue(name, out var s))
                {
                    var t = Resources.Load<Texture2D>("UI/skin/" + name);
                    if (t) { float r = Mathf.Min(t.height * 0.5f - 1, t.width * 0.45f); s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(r, t.height * 0.5f - 1, r, t.height * 0.5f - 1)); }
                    mixPill[name] = s;
                }
                im = UIKit.Img(p, name, s, tint ?? Color.white); im.type = Image.Type.Sliced;
                im.rectTransform.Place(anchor, new Vector2(0.5f, 0.5f), pos, size);
                if (s) im.pixelsPerUnitMultiplier = s.rect.height / Mathf.Max(1, size.y);
            }
            else { im = Skin.Img(p, name, tint); im.rectTransform.Place(anchor, new Vector2(0.5f, 0.5f), pos, size); }
            im.raycastTarget = false;
            return im;
        }
        string TierNode(int t) => Skin.Has(TIER_NODE[t]) ? TIER_NODE[t] : "node_green";
        string TierBtn(int t) => Skin.Has(TIER_BTN[t]) ? TIER_BTN[t] : "btn_gold";
        // 一块贴图长条（按钮 / 小牌子）+ 白字
        RectTransform MPill(Transform p, string skin, string text, int size, Vector2 anchor, Vector2 pos, Vector2 wh, string edge)
        {
            var r = UIKit.Rect("pill", p).Place(anchor, new Vector2(0.5f, 0.5f), pos, wh);
            if (Skin.Has(skin)) MSk(r, skin, new Vector2(0.5f, 0.5f), Vector2.zero, wh);
            else UIKit.Img(r, "bg", UIKit.Round, UIKit.Hex("7f8b97")).rectTransform.Stretch();
            var t = UIKit.Txt(r, "t", text, size, Color.white); t.rectTransform.Stretch(6, wh.y * 0.08f, 6, 0);
            if (edge != null) Styled(t, UIKit.Hex(edge), 1.4f, 1f);
            return r;
        }

        static float mixZoom = 0.55f; const float MIX_ZMAX = 1.15f, MIX_ZDEF = 0.55f;
        ScrollRect mixSR; Text mixZoomT; int mixFocus;
        float MixMinZoom => mixView && mixView.rect.width > 1 ? Mathf.Max(mixView.rect.width / MW, mixView.rect.height / MHt) : 0.3f;
        void ShowMixMap(int focus, List<int> clouds = null, List<Vector2Int> popLv = null, List<Vector2Int> pops = null)
        {
            MixLoad();
            if (mixAnim) Destroy(mixAnim);
            HideTitle(); HideModal(); SetHud(false);
            mixMap = true; mixOn = false; mixPulse.Clear(); mixHands.Clear(); mixLvR.Clear(); mixFog.Clear();
            foreach (Transform c in ui.spec) Destroy(c.gameObject);
            ui.spec.gameObject.SetActive(true);
            UIKit.Img(ui.spec, "bg", null, UIKit.Hex("1d5fbf"), true).rectTransform.Stretch(-50, -50, -50, -50);   // 环礁外的深蓝外海
            var S = SafeRect(ui.spec, "safe");
            var view = UIKit.Rect("view", S).Stretch(0, 0, 0, 0); view.gameObject.AddComponent<RectMask2D>(); mixView = view;
            UIKit.Img(view, "hit", null, new Color(0, 0, 0, 0), true).rectTransform.Stretch();
            var content = UIKit.Rect("content", view); content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(MW, MHt); content.anchoredPosition = Vector2.zero; mixContent = content;
            // 海：codex 画的整张环礁（外圈礁石椰子树、里面翡翠绿内湖），铺满整张地图
            if (Skin.Has("mix_at_atoll_bg")) { var im = Skin.Img(content, "mix_at_atoll_bg"); im.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -MHt / 2), new Vector2(MW, MHt)); }
            else UIKit.Img(content, "lagoon", null, UIKit.Hex("3fd3c6")).rectTransform.Stretch();
            // 岛和岛之间：两头都开了是一条沙洲小路，通往还锁着的岛是一串踏脚石（先画，岛压在上面）
            for (int e = 0; e < MIX_EDGE.GetLength(0); e++)
            {
                int a = MIX_EDGE[e, 0], b = MIX_EDGE[e, 1];
                bool lit = (MixOpen(a, 0) && MixOpen(b, 0)) || mixAll;
                Vector2 pa = MixC(a), pb = MixC(b), d = (pb - pa).normalized;
                MixPath(content, pa + d * MixEdgeR(a, d) * 0.8f, pb - d * MixEdgeR(b, d) * 0.8f, lit, e);
            }
            var cs = clouds ?? new List<int>();
            var popSet = new HashSet<string>(); if (popLv != null) foreach (var v in popLv) popSet.Add(v.x + "_" + v.y);
            for (int i = 0; i < MIX_NI; i++) MixIsland(content, i, cs.Contains(i), popSet);
            // 顶栏：和主线关卡地图一样（奶白长牌 + 左边返回圆钮 + 右边星星数）
            int se = 0, sh = 0, sx = 0; for (int i = 0; i < MIX_NI; i++) { se += mixE[i]; sh += mixH[i]; sx += mixX[i]; }
            var head = UIKit.Rect("head", S).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(370, 58));
            RectTransform ht;
            if (Skin.Has("plate_cream")) { var hi = Skin.Img(head, "plate_cream"); hi.raycastTarget = true; ht = hi.rectTransform; ht.Stretch(20, 2, 0, 2); }
            else { ht = UIKit.Panel(head, "t", UIKit.Cream, 4); ht.Stretch(20, 2, 0, 2); }
            UIKit.Txt(ht, "n", "混合模式", 24, UIKit.Hex("6b3d14"), TextAnchor.MiddleCenter).rectTransform.Stretch(40, 2, 40, 0);
            var starI = UIKit.Rect("star", ht).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-70, 0), new Vector2(30, 30));
            if (Skin.Has("star_gold")) Skin.Img(starI, "star_gold").rectTransform.Stretch();
            UIKit.Txt(ht, "s", (se + sh + sx) + "/" + (MIX_NI * 8), 17, UIKit.Hex("6b3d14"), TextAnchor.MiddleLeft).rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-8, 0), new Vector2(62, 30));
            IconBtn(head, "icon_back", new Vector2(0, 0.5f), new Vector2(26, 0), 58, () => { sfx.Play("click"); ShowSpecList(); }, "<");
            bool ho = HardOpen(), xo = MixOpen(0, 2) || sx > 0;
            MixTab(S, 0, se + "/" + (MIX_NI * 3), true); MixTab(S, 1, ho ? sh + "/" + (MIX_NI * 4) : "简单全通后开", ho); MixTab(S, 2, xo ? sx + "/" + MIX_NI : "未开", xo);
            // 右下：缩小看全局 / 放大回当前岛（也可以两指捏合、鼠标滚轮）
            var zb = MPill(S, Skin.Has("btn_blue") ? "btn_blue" : "btn_green", "缩小", 18, new Vector2(1, 0), new Vector2(-50, 74), new Vector2(84, 50), "14608f");
            mixZoomT = zb.Find("t")?.GetComponent<Text>();
            UIKit.Img(zb, "hit", null, new Color(0, 0, 0, 0), true).rectTransform.Stretch(); UIKit.Btn(zb, () => { sfx.Play("click"); MixZoomToggle(); });
            // 开发用：全部解锁 / 重置进度（正式版去掉）
            var dev = UIKit.Rect("dev", S).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(330, 36));
            MixSmall(dev, mixAll ? "测试：取消全部解锁" : "测试：全部解锁", new Vector2(-84, 0), "5a6b7d", () => { mixAll = !mixAll; MixSave(); ShowMixMap(mixFocus); });
            MixSmall(dev, "测试：重置进度", new Vector2(84, 0), "5a6b7d", () => { for (int i = 0; i < MIX_NI; i++) mixE[i] = mixH[i] = mixX[i] = 0; MixSave(); ShowMixMap(0); });
            var sr = view.gameObject.AddComponent<ScrollRect>(); sr.content = content; sr.horizontal = true; sr.vertical = true; sr.viewport = view;
            sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 0; mixSR = sr;
            mixAnim = ui.spec.gameObject.AddComponent<MixAnim>(); float tt = 0;
            mixAnim.tick = dt =>
            {
                tt += dt;
                foreach (var p in mixPulse) if (p) p.localScale = Vector3.one * (1 + 0.06f * Mathf.Sin(tt * 5));
                foreach (var (r, y) in mixHands) if (r) r.anchoredPosition = new Vector2(r.anchoredPosition.x, y + Mathf.Abs(Mathf.Sin(tt * 4)) * 10);
                if (ModalOpen) return;
                // 两指捏合：缩放 + 跟着两指中点平移；这时先停掉 ScrollRect 免得抢拖动
                if (Input.touchCount >= 2)
                {
                    var ta = Input.GetTouch(0); var tb = Input.GetTouch(1);
                    Vector2 pa = ta.position - ta.deltaPosition, pb = tb.position - tb.deltaPosition;
                    if (sr.enabled) { sr.StopMovement(); sr.enabled = false; }
                    float d0 = Vector2.Distance(pa, pb), d1 = Vector2.Distance(ta.position, tb.position);
                    if (d0 > 1) MixZoomAt(mixZoom * d1 / d0, (ta.position + tb.position) / 2, (pa + pb) / 2);
                }
                else if (!sr.enabled) sr.enabled = true;
                float wh = Input.mouseScrollDelta.y;
                if (Mathf.Abs(wh) > 0.01f) { sr.StopMovement(); MixZoomAt(mixZoom * Mathf.Pow(1.15f, wh), Input.mousePosition, Input.mousePosition); }
            };
            Canvas.ForceUpdateCanvases();
            MixScroll(focus, false);
            // 解锁弹窗一个接一个，弹完镜头飞过去、云散开、小岛弹出来
            int idx = 0;
            Action next = null;
            next = () =>
            {
                if (pops != null && idx < pops.Count)
                {
                    var p = pops[idx++];
                    if (p.x == 1 && cs.Count > 0) MixPop(1, cs[0], next, cs); else MixPop(p.x, p.y, next);
                    return;
                }
                MixReveal(cs, popLv);
            };
            next();
        }
        // 顶栏下面三档：简单 / 困难 / 地狱（开了是各自的颜色，没开是灰的）
        void MixTab(RectTransform S, int k, string sub, bool on)
        {
            var r = MPill(S, on ? TierBtn(k) : "btn_grey", "", 15, new Vector2(0.5f, 1), new Vector2((k - 1) * 122, -88), new Vector2(116, 50), null);
            var n = UIKit.Txt(r, "n", TIER_N[k], 17, Color.white, TextAnchor.MiddleCenter); n.rectTransform.Stretch(0, 22, 0, 4); Styled(n, UIKit.Hex(on ? TIER_EDGE[k] : "6d6a64"), 1.5f, 1.2f);
            var s = UIKit.Txt(r, "s", sub, 12, Color.white); s.rectTransform.Stretch(4, 6, 4, 26); Styled(s, UIKit.Hex(on ? TIER_EDGE[k] : "6d6a64"), 1.2f, 0.8f);
        }
        void MixSmall(RectTransform p, string s, Vector2 pos, string hex, Action fn)
        {
            var b = UIKit.Img(p, "b", UIKit.Round, UIKit.Hex(hex, 0.85f), true).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(156, 32));
            UIKit.Txt(b, "t", s, 13, Color.white).rectTransform.Stretch(); UIKit.Btn(b, () => { sfx.Play("click"); fn(); });
        }
        void MixDots(RectTransform p, Vector2 a, Vector2 b, float alpha, float size)
        {
            float d = Vector2.Distance(a, b); int n = Mathf.Max(1, Mathf.FloorToInt(d / 15));
            for (int k = 1; k < n; k++) MImg(p, "dot", UIKit.Circle, "ffffff", Vector2.Lerp(a, b, k / (float)n), new Vector2(size, size), alpha);
        }
        // 从岛中心沿 d 方向到岛底座边缘的距离（底座是横的椭圆）
        float MixEdgeR(int i, Vector2 d) { float rx = IW / 2 * ISc(i), ry = IH / 2 * ISc(i); return 1f / Mathf.Sqrt(d.x * d.x / (rx * rx) + d.y * d.y / (ry * ry)); }
        // 一条沙路 / 一串踏脚石。a、b 是 content 顶边坐标
        void MixPath(RectTransform p, Vector2 a, Vector2 b, bool lit, int seed)
        {
            float len = Vector2.Distance(a, b); if (len < 8) return;
            var d = (b - a) / len; var n = new Vector2(-d.y, d.x); float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var rd = new System.Random(seed * 7 + 3);
            if (lit)
            {
                // 沙路贴图是 8:1 的横条，按长度切成几段首尾叠一点接起来
                int cnt = Mathf.Max(1, Mathf.CeilToInt(len / 300)); float seg = len / cnt;
                for (int k = 0; k < cnt; k++)
                {
                    var c = a + d * (seg * (k + 0.5f));
                    RectTransform r = Skin.Has("mix_at_path_sand") ? MSk(p, "mix_at_path_sand", new Vector2(0.5f, 1), c, new Vector2(seg + 24, 52)).rectTransform
                        : MImg(p, "path", UIKit.Round, "f1dfae", c, new Vector2(seg + 10, 40));
                    r.localRotation = Quaternion.Euler(0, 0, ang);
                }
            }
            else
            {
                int cnt = Mathf.Max(1, Mathf.FloorToInt(len / 40));
                for (int k = 0; k <= cnt; k++)
                {
                    var c = Vector2.Lerp(a, b, k / (float)cnt) + n * (k % 2 == 0 ? 8 : -8);
                    float sz = 32 + (float)rd.NextDouble() * 10;
                    if (Skin.Has("mix_at_stone2")) { var im = MSk(p, "mix_at_stone2", new Vector2(0.5f, 1), c, new Vector2(sz, sz)); if (rd.Next(2) == 1) im.rectTransform.localScale = new Vector3(-1, 1, 1); }
                    else MImg(p, "stone", UIKit.Circle, "9aa3ab", c, new Vector2(sz, sz * 0.75f), 0.9f);
                }
            }
        }
        // 屏幕点 → 相对视口中心的坐标（content 的 anchoredPosition 就是相对视口中心的）
        Vector2 MixLocal(Vector2 scr)
        {
            var cv = mixView.GetComponentInParent<Canvas>().rootCanvas; var cam = cv.renderMode == RenderMode.ScreenSpaceOverlay ? null : cv.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(mixView, scr, cam, out var lp); return lp - mixView.rect.center;
        }
        Vector2 MixClamp(Vector2 p, float s)
        {
            float mx = Mathf.Max(0, (MW * s - mixView.rect.width) / 2), my = Mathf.Max(0, (MHt * s - mixView.rect.height) / 2);
            return new Vector2(Mathf.Clamp(p.x, -mx, mx), Mathf.Clamp(p.y, -my, my));
        }
        // 以某个屏幕点为中心缩放（scrPrev → scr 顺便平移，捏合时用）
        void MixZoomAt(float s2, Vector2 scr, Vector2 scrPrev)
        {
            if (!mixContent || !mixView) return;
            s2 = Mathf.Clamp(s2, MixMinZoom, MIX_ZMAX);
            Vector2 lp = MixLocal(scr), lq = MixLocal(scrPrev), c = (lq - mixContent.anchoredPosition) / mixZoom;
            mixZoom = s2; mixContent.localScale = Vector3.one * s2;
            mixContent.anchoredPosition = MixClamp(lp - c * s2, s2);
            MixZoomLabel();
        }
        void MixZoomLabel() { if (mixZoomT) mixZoomT.text = mixZoom < 0.45f ? "放大" : "缩小"; }
        void MixTween(float s2, Vector2 to, bool smooth)
        {
            var ct = mixContent; if (!ct) return;
            if (mixSR) mixSR.StopMovement();
            float s0 = mixZoom; var from = ct.anchoredPosition;
            mixZoom = s2; MixZoomLabel();
            if (!smooth) { ct.localScale = Vector3.one * s2; ct.anchoredPosition = to; return; }
            Fx.Run(() => Fx.Tween(700, p => { if (!ct) return; float q = Mathf.SmoothStep(0, 1, p); ct.localScale = Vector3.one * Mathf.Lerp(s0, s2, q); ct.anchoredPosition = Vector2.Lerp(from, to, q); }));
        }
        // 镜头飞到某座岛（s2 < 0 保持当前缩放；缩得太小时拉回默认大小，免得看不清）
        void MixScroll(int i, bool smooth, float s2 = -1)
        {
            if (!mixContent || !mixView) return;
            i = Mathf.Clamp(i, 0, MIX_NI - 1); mixFocus = i;
            if (s2 < 0) s2 = Mathf.Max(mixZoom, 0.45f);
            s2 = Mathf.Clamp(s2, MixMinZoom, MIX_ZMAX);
            var rel = MIX_P[i] - new Vector2(0, MCY);
            MixTween(s2, MixClamp(-rel * s2 + new Vector2(0, -20), s2), smooth);
        }
        void MixZoomToggle()
        {
            if (!mixContent || !mixView) return;
            if (mixZoom < 0.45f) { MixScroll(mixFocus, true, MIX_ZDEF); return; }
            float s2 = MixMinZoom; MixTween(s2, MixClamp(mixContent.anchoredPosition * s2 / mixZoom, s2), true);
        }
        string MixArt(int i) => "mix_isl_" + MIX_K[i].ToString().ToLower();
        string MixFeat(int i) => "mix_at_f_" + MIX_K[i].ToString().ToLower();
        // 一座岛的图（底座 + 中间主题物件），弹窗 / 详情里用；anchor 在父物体中心
        RectTransform MixIslArt(Transform p, int i, Vector2 pos, float w)
        {
            var r = UIKit.Rect("isl", p).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(w, w * IH / IW));
            if (Skin.Has("mix_at_isl_base")) { MSk(r, "mix_at_isl_base", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, w * IH / IW)); if (Skin.Has(MixFeat(i))) MSk(r, MixFeat(i), new Vector2(0.5f, 0.5f), new Vector2(0, w * 0.06f), new Vector2(w * 0.44f, w * 0.44f)); }
            else if (Skin.Has(MixArt(i))) MSk(r, MixArt(i), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, w * 0.87f));
            return r;
        }

        void MixIsland(RectTransform content, int i, bool cloudAnim, HashSet<string> popSet)
        {
            var c = MixC(i); bool on = MixOpen(i, 0) || mixAll; float sc = ISc(i), w = IW * sc, h = IH * sc;
            // 底座（草地 + 环形沙路）+ 中间的主题物件
            if (Skin.Has("mix_at_isl_base")) MSk(content, "mix_at_isl_base", new Vector2(0.5f, 1), c, new Vector2(w, h));
            else MImg(content, "sand", UIKit.Circle, "9fd36a", c, new Vector2(w * 0.9f, h * 0.85f));
            if (Skin.Has(MixFeat(i))) MSk(content, MixFeat(i), new Vector2(0.5f, 1), c + new Vector2(0, RCY * sc + 14 * sc), new Vector2(w * 0.42f, w * 0.42f));
            else if (Skin.Has(MixArt(i))) MSk(content, MixArt(i), new Vector2(0.5f, 1), c + new Vector2(0, 10), new Vector2(w * 0.5f, w * 0.44f));
            // 岛名：主线地图的蓝丝带，挂在岛下沿
            RectTransform nm;
            var np = c + new Vector2(0, -h / 2 - 4);
            if (Skin.Has("ribbon_blue")) { nm = Skin.Img(content, "ribbon_blue").rectTransform; nm.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), np, new Vector2(230, 52)); }
            else nm = UIKit.Panel(content, "name" + i, UIKit.Hex("14608f"), 3, 3).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), np, new Vector2(186, 34));
            var nt = UIKit.Txt(nm, "t", (i + 1) + ". " + MIX_N[i], 21, Color.white); nt.rectTransform.Stretch(30, 22, 30, 3);
            nt.resizeTextForBestFit = true; nt.resizeTextMinSize = 14; nt.resizeTextMaxSize = 22; Styled(nt, UIKit.Hex("123f7a"), 2f, 1.5f);
            if (mixX[i] >= 1)
            {
                if (Skin.Has("mix_crown")) MSk(content, "mix_crown", new Vector2(0.5f, 1), c + new Vector2(0, h / 2 + 34), new Vector2(60, 60));
                MPill(content, "btn_gold", "地狱通关", 12, new Vector2(0.5f, 1), c + new Vector2(0, h / 2 + 2), new Vector2(84, 24), "8a5a00");
            }
            var hit = UIKit.Img(content, "hit", UIKit.Circle, new Color(0, 0, 0, 0), true).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), c + new Vector2(0, RCY * sc), new Vector2(w * 0.62f, h * 0.5f));
            int ii = i; UIKit.Btn(hit, () => { sfx.Play("click"); MixDetail(ii); });
            // 8 关：后排（上半圈）先画，前排压在上面
            for (int q = 0; q < 8; q++) { int k = new[] { 3, 4, 2, 5, 1, 6, 0, 7 }[q]; MixIslet(content, i, k, popSet.Contains(i + "_" + k) || cloudAnim); }
            nm.SetAsLastSibling();   // 岛名压在定位针上面，别被针挡住
            if (!on || cloudAnim)
            {
                // 没开的岛：一大团云 + 挂铁链的金锁盖住，云顶上露出这座岛的主题物件，下面写开启条件
                var fog = UIKit.Rect("fog" + i, content).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), c, new Vector2(10, 10));
                var cg = fog.gameObject.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false; mixFog[i] = cg;
                RectTransform cl;
                if (Skin.Has("mix_at_fog_lock")) cl = MSk(fog, "mix_at_fog_lock", new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(w * 1.06f, w * 1.06f * 242 / 512f)).rectTransform;
                else { cl = MImgC(fog, "c", UIKit.Circle, "eef3f7", new Vector2(0, 6), new Vector2(w, h * 0.8f), 0.97f); if (Skin.Has("icon_lock")) Skin.Img(cl, "icon_lock").rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60)); }
                // 云顶上露出这座岛的主题物件（看得出是什么机制）
                if (Skin.Has(MixFeat(i))) MSk(fog, MixFeat(i), new Vector2(0.5f, 0.5f), new Vector2(0, 86 * sc), new Vector2(116, 116));
                bool front = MixFront(i);
                var tag = UIKit.Panel(fog, "cond", UIKit.Hex("281c10", 0.85f), 0, 0).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -h / 2 - 8), new Vector2(360, 46));
                var ct = UIKit.Txt(tag, "t", front ? "打通" + MixNbNames(i, 0) + "的简单关解锁" : "先解锁旁边的岛才能到这里", 20, Color.white); ct.rectTransform.Stretch(10, 0, 10, 0);
                ct.resizeTextForBestFit = true; ct.resizeTextMinSize = 12; ct.resizeTextMaxSize = 20;
                var nn = UIKit.Txt(tag, "n", (i + 1) + ". " + MIX_N[i], 22, Color.white); nn.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0), new Vector2(0, 2), new Vector2(300, 32)); Styled(nn, UIKit.Hex("3d4a58"), 2f, 1.5f);
                if (front) mixPulse.Add(cl);
            }
        }
        void MixIslet(RectTransform content, int i, int k, bool hidden)
        {
            int st = MixLv(i, k), t = TierOf(k);
            if (st == 0 && mixAll) st = -1;
            // 糖果色圆片：灰 = 锁着，金币带星 = 打通，绿 / 橙 / 紫 = 简单 / 困难 / 地狱；现在该打的大一圈、插红针
            var pos = MixSlot(i, k); float sz = st == 1 ? 84 : 68, hh = sz * 0.73f;
            var root = UIKit.Rect("lv" + i + "_" + k, content).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), pos, new Vector2(sz, hh));
            mixLvR[i + "_" + k] = root;
            string node = st == 0 ? "mix_at_n_grey" : st == 2 ? "mix_at_n_gold" : new[] { "mix_at_n_green", "mix_at_n_orange", "mix_at_n_purple" }[t];
            if (Skin.Has(node)) { var im = Skin.Img(root, node); im.rectTransform.Stretch(); if (st == -1) im.color = new Color(1, 1, 1, 0.75f); }
            else UIKit.Disc(root, "d", UIKit.Hex(st == 0 ? "aab3be" : st == 2 ? "f2c230" : TIER_C[t]), 4, 3).Stretch(4, 4, 4, 4);
            if (st != 2)
            {
                var tx = UIKit.Txt(root, "n", (k + 1).ToString(), st == 1 ? 32 : 26, Color.white);
                tx.rectTransform.Stretch(0, hh * 0.22f, 0, 0);
                Styled(tx, UIKit.Hex(st == 0 ? "6d6a64" : TIER_EDGE[t]), 1.8f, 1.5f);
                if (st == 0) tx.color = new Color(1, 1, 1, 0.85f);
            }
            if (st == 1)
            {
                mixPulse.Add(root);
                // 红色定位针（主线地图当前关的那根针），上下跳
                if (Skin.Has("pin_red")) { var pin = Skin.Img(root, "pin_red").rectTransform; pin.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0), new Vector2(0, -hh * 0.25f), new Vector2(42, 52)); mixHands.Add((pin, pin.anchoredPosition.y)); }
                else
                {
                    var hd = UIKit.Rect("hand", root).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0), new Vector2(0, 2), new Vector2(34, 34));
                    if (Skin.Has("hand_point")) Skin.Img(hd, "hand_point").rectTransform.Stretch();
                    mixHands.Add((hd, 2));
                }
            }
            var hit = UIKit.Img(root, "hit", UIKit.Circle, new Color(0, 0, 0, 0), true); hit.rectTransform.Stretch(-8, -12, -8, -12);
            int ii = i, kk = k; UIKit.Btn(hit.rectTransform, () => MixTapLv(ii, kk));
            if (hidden) root.localScale = Vector3.zero;
        }
        void MixTapLv(int i, int k)
        {
            if (!MixPlayable(i, k)) { sfx.Play("bad"); Toast(MixWhy(i, k)); if (mixLvR.TryGetValue(i + "_" + k, out var r)) Bump(r); return; }
            sfx.Play("click"); MixPlay(i, k);
        }
        // 点大岛：这座岛的 8 关一览
        void MixDetail(int i)
        {
            Modal();
            ModalTitle((i + 1) + ". " + MIX_N[i]);
            MixIslArt(MBlock(118), i, Vector2.zero, 170);
            MText(MIX_ONE[i], 15, UIKit.Hex("5a3410"));
            for (int t = 0; t < 3; t++)
            {
                bool op = MixOpen(i, t) || mixAll; int d = MixDone(i, t), a = TierStart(t), n = TierLen(t);
                string range = n > 1 ? "第 " + (a + 1) + "~" + (a + n) + " 关" : "第 " + (a + 1) + " 关";
                var tx = MText("<b><color=#" + TIER_C[t] + ">" + TIER_N[t] + "</color></b>  " + range + "  " + (op ? d + "/" + n : "未开"), 16);
                tx.alignment = TextAnchor.MiddleLeft;
                if (!op) { var w = MText("　" + MixWhy(i, a), 13, UIKit.Hex("7a5230")); w.alignment = TextAnchor.MiddleLeft; }
                else for (int k = a; k < a + n; k++) { var w = MText("　" + (k + 1) + ". " + MIX_T[k].t + " · " + MIX_T[k].ppl + " 人" + (MixLv(i, k) == 2 ? "  ★" : ""), 13, UIKit.Hex("5a6b7d")); w.alignment = TextAnchor.MiddleLeft; }
            }
            MSpace(4);
            int cur = -1; for (int k = 0; k < 8 && cur < 0; k++) if (MixLv(i, k) == 1) cur = k;
            if (cur >= 0) { int cc = cur; MButton("开始第 " + (cur + 1) + " 关", "3fb950", () => { sfx.Play("click"); HideModal(); MixPlay(i, cc); }); }
            MButton("这个机制怎么玩", "ffb81f", () => { sfx.Play("click"); spKind = MIX_K[i]; SpecRule(); }, true, "6b3d0c");
            MButton("关闭", "fff8e6", () => { sfx.Play("click"); HideModal(); }, true, "7a4d21", 15, 40);
            FinishModal();
        }

        // 云散开、小岛一个个弹出来
        void MixReveal(List<int> clouds, List<Vector2Int> popLv)
        {
            var cs = clouds ?? new List<int>();
            var rest = new List<RectTransform>(); int restI = -1;
            if (popLv != null) foreach (var v in popLv) if (!cs.Contains(v.x) && mixLvR.TryGetValue(v.x + "_" + v.y, out var r)) { rest.Add(r); if (restI < 0) restI = v.x; }
            if (cs.Count == 0 && rest.Count == 0) return;
            var map = mixContent;
            Fx.Run(async () =>
            {
                foreach (var ci in cs)
                {
                    if (!map || map != mixContent) return;
                    MixScroll(ci, true); await Fx.Sleep(750);
                    if (mixFog.TryGetValue(ci, out var cg) && cg)
                    {
                        sfx.Play("reveal"); var tr = cg.transform;
                        Fx.Run(() => Fx.Tween(1300, p => { if (cg) { cg.alpha = 1 - p; tr.localScale = Vector3.one * (1 + p * 0.6f); } }));
                        await Fx.Sleep(500);
                    }
                    var list = new List<RectTransform>(); for (int k = 0; k < 8; k++) if (mixLvR.TryGetValue(ci + "_" + k, out var r)) list.Add(r);
                    await MixPopIslets(list);
                    await Fx.Sleep(350);
                }
                if (rest.Count == 0 || !map || map != mixContent) return;
                if (cs.Count == 0) { MixScroll(restI, true); await Fx.Sleep(650); }
                await MixPopIslets(rest);
            });
        }
        async System.Threading.Tasks.Task MixPopIslets(List<RectTransform> list)
        {
            foreach (var r in list)
            {
                var rr = r; if (!rr) continue; sfx.Play("pop2");
                Fx.Run(() => Fx.Tween(420, p => { if (rr) rr.localScale = Vector3.one * (p < 0.7f ? p / 0.7f * 1.25f : 1.25f - (p - 0.7f) / 0.3f * 0.25f); }));
                await Fx.Sleep(110);
            }
        }

        // ---------- 解锁大弹窗 ----------
        // type 1 新岛屿 / 2 困难开启 / 3 地狱解锁。用主线的木框弹窗 + 各档颜色的丝带，后面一圈转动的光芒，中间是 codex 画的徽章 / 岛
        // 弹窗后面的放射光：宽光束、往外渐隐、中间一团亮，白色（按档位染色）
        static Sprite mixRaysS;
        static Sprite MixRays()
        {
            if (mixRaysS) return mixRaysS;
            const int N = 256; var tx = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2 - 1, dy = (y + 0.5f) / N * 2 - 1, r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx);
                    float w = Mathf.SmoothStep(0.15f, 0.65f, 0.5f + 0.5f * Mathf.Cos(a * 12));
                    float fall = Mathf.Clamp01(1 - r);
                    float al = Mathf.Clamp01(w * Mathf.Pow(fall, 0.8f) + Mathf.Exp(-r * 4) * 0.6f);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(al * 255));
                }
            tx.SetPixels32(px); tx.Apply();
            return mixRaysS = Sprite.Create(tx, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
        }
        RectTransform MBlock(float h) { var r = UIKit.Rect("blk", boxContent); r.gameObject.AddComponent<LayoutElement>().preferredHeight = h; return r; }
        void MixPop(int type, int i, Action done, List<int> many = null)
        {
            int t = type - 1; bool hell = type == 3;
            Modal(false);
            string rib = t == 0 ? "mix_ribbon_green" : t == 1 ? "mix_ribbon_orange" : "mix_ribbon_purple";
            if (!Skin.Has(rib)) rib = "ribbon_gold";
            ModalTitle(type == 1 ? "新岛屿解锁！" : type == 2 ? "困难模式开启！" : "地狱模式解锁！", null, rib);
            var rt = ui.box.Find(rib)?.Find("t")?.GetComponent<Text>();
            if (rt && rib != "ribbon_gold") { rt.color = Color.white; Styled(rt, UIKit.Hex(TIER_EDGE[t]), 1.8f, 1.5f); }
            // 光芒：在木框后面转
            var ri = UIKit.Img(ui.box, "rays", MixRays(), t == 0 ? UIKit.Hex("fff0a0") : t == 1 ? UIKit.Hex("ffbf6a") : UIKit.Hex("cf8cff")); ri.raycastTarget = false;
            var rays = ri.rectTransform; rays.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(940, 940)); rays.SetAsFirstSibling();
            // 插画
            var art = MBlock(168); var sparks = new List<RectTransform>(); var dots = new List<Image>(); RectTransform hero = null;
            if (type == 1)
            {
                if (many != null && many.Count > 1)
                {
                    // 一次开了好几座：几座岛并排
                    hero = UIKit.Rect("heroes", art).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(300, 170));
                    int n = Mathf.Min(many.Count, 4); float w = n == 2 ? 150 : n == 3 ? 112 : 92;
                    for (int q = 0; q < n; q++)
                    {
                        float x = (q - (n - 1) / 2f) * w * 0.92f, y = n > 2 && q % 2 == 1 ? -14 : 8;
                        MixIslArt(hero, many[q], new Vector2(x, y), w * 1.2f);
                    }
                }
                else if (Skin.Has("mix_at_isl_base") || Skin.Has(MixArt(i))) hero = MixIslArt(art, i, new Vector2(0, 2), 230);
                else if (Skin.Has("mix_emb_new")) hero = MSk(art, "mix_emb_new", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 150)).rectTransform;
            }
            else if (type == 2)
            {
                if (Skin.Has("mix_emb_hard")) hero = MSk(art, "mix_emb_hard", new Vector2(0.5f, 0.5f), new Vector2(-62, 14), new Vector2(120, 120)).rectTransform;
                var big = UIKit.Txt(art, "n", (MIX_NI * 4) + " 关", 40, UIKit.Hex("ff9a2e"), TextAnchor.MiddleCenter); big.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(62, 22), new Vector2(150, 60)); Styled(big, UIKit.Hex("8a4306"), 2.4f, 2.5f);
                // 每座岛一个小节点，一个个亮起来
                for (int k = 0; k < MIX_NI; k++)
                {
                    var d = Skin.Has("node_grey") ? Skin.Img(art, "node_grey") : UIKit.Img(art, "d", UIKit.Circle, UIKit.Hex("c9d0d8"));
                    d.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((k % 7 - 3) * 34 + (k >= 7 ? 17 : 0), -46 - (k / 7) * 30), new Vector2(30, 30)); dots.Add(d);
                }
            }
            else
            {
                if (Skin.Has("mix_emb_hell")) hero = MSk(art, "mix_emb_hell", new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(150, 150)).rectTransform;
                if (Skin.Has(TierNode(2))) { var n8 = MSk(art, TierNode(2), new Vector2(0.5f, 0.5f), new Vector2(86, -50), new Vector2(64, 64)); var tx = UIKit.Txt(n8.rectTransform, "n", "8", 24, Color.white); tx.rectTransform.Stretch(0, 19, 0, 0); Styled(tx, UIKit.Hex(TIER_EDGE[2]), 1.8f, 1.5f); }
            }
            if (Skin.Has("mix_sparkle"))
                foreach (var v in new[] { new Vector2(-112, 58), new Vector2(118, 46), new Vector2(-96, -60), new Vector2(104, -66), new Vector2(0, 80) })
                    sparks.Add(MSk(art, "mix_sparkle", new Vector2(0.5f, 0.5f), v, new Vector2(30, 30)).rectTransform);
            bool multi = type == 1 && many != null && many.Count > 1;
            string name = multi ? "一下开了 " + many.Count + " 座新岛！" : type == 2 ? MIX_NI + " 座岛的困难全部开放" : (i + 1) + ". " + MIX_N[i] + (type == 3 ? " · 第 8 关" : "");
            string desc = multi ? string.Join("、", many.ConvertAll(j => MIX_N[j])) + "\n想先去哪座，你自己挑"
                : type == 1 ? MIX_ONE[i] : type == 2 ? "每座岛的第 4~7 关都开了，先打哪座随便挑\n打通一座岛的困难，会开它的地狱" : "500 人，所有东西拉满\n打通它，相邻岛的地狱也会开";
            var nt = MText(name, 21, UIKit.Hex(TIER_C[t] == "3fb950" ? "2f8a3a" : TIER_C[t])); Styled(nt, Color.white, 1.2f, 0);
            MText(desc, 14, UIKit.Hex("5a3410"));
            MText(type == 1 ? "简单 · 第 1~3 关已开放" : type == 2 ? "困难 · 第 4~7 关" : "地狱 · 第 8 关", 14, UIKit.Hex("7a5230"));
            MSpace(2);
            bool closed = false;
            Action go = () => { if (closed) return; closed = true; sfx.Play("click"); HideModal(); done?.Invoke(); };
            if (type == 3) { var b = SkinBtn(boxContent, "8a5cff", "", 22, go, out var lb); lb.text = "敢不敢？"; b.gameObject.AddComponent<LayoutElement>().preferredHeight = 58; }
            else MButton(type == 1 ? "去看看" : "去挑战", type == 1 ? "3fb950" : "ffb81f", go, true, "ffffff", 21);
            FinishModal();
            // 动画：光芒转、星光闪、徽章弹出来、困难的小节点一个个亮、地狱的弹窗抖
            sfx.Play(hell ? "rumble" : "fanfare"); sfx.Play("unlock"); Haptics.Play(Haptics.Kind.Success);
            if (!hell) Confetti();
            if (hero) hero.localScale = Vector3.zero;
            float t0 = 0; var box = ui.box;
            var an = art.gameObject.AddComponent<MixAnim>();
            an.tick = dt =>
            {
                t0 += dt;
                if (rays) rays.localRotation = Quaternion.Euler(0, 0, -t0 * 30);
                if (hero) { float p = Mathf.Clamp01((t0 - 0.15f) / 0.45f); hero.localScale = Vector3.one * (p < 0.7f ? p / 0.7f * 1.15f : 1.15f - (p - 0.7f) / 0.3f * 0.15f); }
                for (int k = 0; k < sparks.Count; k++) if (sparks[k]) { float s = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(t0 * 3 + k * 1.3f)); sparks[k].localScale = Vector3.one * s; sparks[k].localRotation = Quaternion.Euler(0, 0, t0 * 40 + k * 20); }
                for (int k = 0; k < dots.Count; k++) if (dots[k] && t0 > 0.6f + k * 0.07f && dots[k].name != "on") { dots[k].name = "on"; if (Skin.Has(TierNode(1))) dots[k].sprite = Skin.Get(TierNode(1)); else dots[k].color = UIKit.Hex("ff9a3c"); sfx.Play("pop2"); }
                if (hell && box) box.anchoredPosition = new Vector2(t0 > 0.5f && t0 < 1.0f ? Mathf.Sin(t0 * 60) * 7 : 0, 0);
            };
        }

        // ---------- 进一关 ----------
        MixCfg MixCfgFor(int w)
        {
            var T = MIX_T[mixK]; var kind = MIX_K[mixI];
            int per = Mathf.RoundToInt(T.ppl / (float)T.waves / MixAvg(kind));
            return new MixCfg
            {
                kind = kind, boats = Mathf.Clamp(per, 2, MIX_DEF[mixI]), colors = T.colors, rocks = T.rocks, poles = T.poles, weed = T.weed, locks = T.locks,
                mys = T.mys, clock = T.clock, per = T.per, start = w == 0 ? T.start : 0
            };
        }
        int MixPlanWave() { var c = MixCfgFor(1); return Mathf.RoundToInt(c.boats * MixAvg(c.kind)); }
        int MixLater() => mixOn ? Mathf.Max(0, MIX_T[mixK].waves - mixWave - 1) * MixPlanWave() : 0;
        string MixPre() => !mixOn ? "" : MIX_N[mixI] + " · " + (MIX_T[mixK].waves > 1 ? "第 " + (mixWave + 1) + "/" + MIX_T[mixK].waves + " 波 · " : "");
        string MixTitle() => MIX_N[mixI] + " · 第 " + (mixK + 1) + " 关 · " + TIER_N[TierOf(mixK)];

        void MixPlay(int i, int k)
        {
            MixLoad();
            mixI = i; mixK = k; mixWave = 0; mixSaved = 0; mixT = 0; mixUse = new[] { 2, 2, 2 }; mixCrane = false; mixBusy = false;
            mixMap = false; mixOn = true;
            var s = SpSim.MixBuild(MixCfgFor(0), UnityEngine.Random.Range(1, 100000));
            SpecStart(s, false);
            if (k == 0) SpecRule();
            else MixNewModal(k);
        }
        // 这一关新加的东西：第一次见到时说明一下
        void MixNewModal(int k)
        {
            var T = MIX_T[k]; if (T.nw == null || PlayerPrefs.GetInt("tide_mix_new" + k, 0) == 1) return;
            PlayerPrefs.SetInt("tide_mix_new" + k, 1); PlayerPrefs.Save();
            Modal(false);
            ModalTitle("第 " + (k + 1) + " 关 · 新东西");
            foreach (var (n, d) in MIX_NEWS) if (T.nw.Contains(n)) { var t = MText("<b>" + n + "</b>：" + d, 15, UIKit.Hex("5a3410")); t.alignment = TextAnchor.MiddleLeft; }
            MText(T.ppl + " 人 · " + T.waves + " 波" + (T.clock ? " · 有风暴倒计时" : " · 不计时"), 14, UIKit.Hex("2f6b2a"));
            MSpace(4);
            MButton("开始", "3fb950", () => { sfx.Play("click"); HideModal(); });
            FinishModal();
        }
        void MixLeave()
        {
            HideModal(); specOpen = false; mixOn = false; mixBusy = false; SpecClean();
            if (world) world.gameObject.SetActive(true);
            FitCam();
        }

        // 一波打完
        void MixWaveDone()
        {
            if (!mixOn || mixBusy) return;
            if (mixWave + 1 < MIX_T[mixK].waves) MixNextWave(); else MixWin();
        }
        void MixNextWave()
        {
            mixBusy = true; float carry = sp.timer; mixSaved += sp.guys.Count; mixT += sp.t;
            var host = ui.spec.parent; var ov = UIKit.Rect("tide", host).Stretch(); ov.SetAsLastSibling();
            var band = UIKit.Rect("band", ov).Stretch(0, 0, 0, 0);
            string wv = Skin.Has("e3_wave") ? "e3_wave" : Skin.Has("e_wave") ? "e_wave" : null, wvCol = wv != null ? "0247ab" : "2a8fd0";
            UIKit.Img(band, "sea", null, UIKit.Hex(wvCol), true).rectTransform.Stretch(-50, -50, -50, 0);
            if (wv != null) for (int k = -1; k <= 1; k++) { var c = Skin.Img(band, wv).rectTransform; c.anchorMin = c.anchorMax = new Vector2(0.5f, 1); c.pivot = new Vector2(0.5f, 0.25f); c.sizeDelta = new Vector2(330, 80); c.anchoredPosition = new Vector2(k * 300, 0); }
            else for (int k = -3; k <= 3; k++) { var c = UIKit.Img(band, "crest", UIKit.Circle, UIKit.Hex("bfeaff")).rectTransform; c.anchorMin = c.anchorMax = new Vector2(0.5f, 1); c.sizeDelta = new Vector2(120, 70); c.anchoredPosition = new Vector2(k * 70, 0); }
            int nw = mixWave + 2, all = MIX_T[mixK].waves;
            var rb = UIKit.Rect("rib", band).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(340, 78));
            if (Skin.Has("ribbon_gold")) Skin.Img(rb, "ribbon_gold").rectTransform.Stretch();
            var wt = UIKit.Txt(rb, "t", "第 " + nw + " 波涨潮！", 26, UIKit.Hex("5a3410"), TextAnchor.MiddleCenter); wt.rectTransform.Stretch(40, 26, 40, 4);
            var ws = UIKit.Txt(band, "s", "新一批船和人来了 · 一共 " + all + " 波 · 已救出 " + mixSaved + " 人", 15, Color.white); ws.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(370, 24)); Styled(ws, UIKit.Hex("123f7a"), 1.5f, 1.2f);
            float H = ((RectTransform)host).rect.height + 120;
            band.anchoredPosition = new Vector2(0, -H);
            sfx.Play("sail"); sfx.Play("cheer", 1, 0.6f);
            Fx.Run(async () =>
            {
                await Fx.Tween(520, p => { if (band) band.anchoredPosition = new Vector2(0, -H * (1 - Mathf.SmoothStep(0, 1, p))); });
                mixWave++;
                var s = SpSim.MixBuild(MixCfgFor(mixWave), UnityEngine.Random.Range(1, 100000));
                s.timer = Mathf.Min(SpSim.TIME_CAP, carry + 5);
                SpecStart(s, false);
                if (ov) ov.SetAsLastSibling();
                await Fx.Sleep(650);
                await Fx.Tween(520, p => { if (band) band.anchoredPosition = new Vector2(0, H * Mathf.SmoothStep(0, 1, p)); });
                if (ov) Destroy(ov.gameObject);
                mixBusy = false;
            });
        }

        // 整关打完：记进度、算解锁，过关窗 → 回地图 → 解锁大弹窗 → 云散开
        void MixWin()
        {
            if (sp != null) { mixSaved += sp.guys.Count; mixT += sp.t; }
            mixBusy = true;
            var was = new bool[MIX_NI, 8]; for (int i = 0; i < MIX_NI; i++) for (int k = 0; k < 8; k++) was[i, k] = MixLv(i, k) >= 1;
            var isl0 = new bool[MIX_NI]; var hell0 = new bool[MIX_NI]; bool hard0 = HardOpen();
            for (int i = 0; i < MIX_NI; i++) { isl0[i] = MixOpen(i, 0); hell0[i] = MixOpen(i, 2); }
            int t = TierOf(mixK), j = mixK - TierStart(t);
            if (MixOpen(mixI, t) && j == MixDone(mixI, t))
            {
                if (t == 0) mixE[mixI]++; else if (t == 1) mixH[mixI]++; else mixX[mixI]++;
                MixSave();
            }
            var pops = new List<Vector2Int>(); var popLv = new List<Vector2Int>(); var opened = new List<int>();
            for (int i = 0; i < MIX_NI; i++) if (!isl0[i] && MixOpen(i, 0)) opened.Add(i);
            if (opened.Count > 0) pops.Add(new Vector2Int(1, opened[0]));
            if (!hard0 && HardOpen()) pops.Add(new Vector2Int(2, 0));
            for (int i = 0; i < MIX_NI; i++) if (!hell0[i] && MixOpen(i, 2)) pops.Add(new Vector2Int(3, i));
            for (int i = 0; i < MIX_NI; i++) for (int k = 0; k < 8; k++) if (!was[i, k] && MixLv(i, k) >= 1) popLv.Add(new Vector2Int(i, k));
            int ci = mixI, ck = mixK;
            Modal(false);
            ModalTitle("过关！", "win");
            MText(MIX_N[ci] + " · 第 " + (ck + 1) + " 关 · " + TIER_N[TierOf(ck)], 17, UIKit.Hex(TIER_C[TierOf(ck)]));
            MText("救出 " + mixSaved + " 人 · " + MIX_T[ck].waves + " 波 · 用时 " + Mathf.RoundToInt(mixT) + " 秒", 15, UIKit.Hex("5a3410"));
            if (pops.Count > 0) MText("解锁了新东西！", 15, UIKit.Hex("2f6b2a"));
            MSpace(4);
            MButton("继续", "3fb950", () => { sfx.Play("click"); MixLeave(); ShowMixMap(ci, opened, popLv, pops); });
            MButton("再玩一次", "ffb81f", () => { sfx.Play("click"); MixLeave(); MixPlay(ci, ck); }, true, "6b3d0c");
            FinishModal();
            Confetti();
        }

        void MixLose()
        {
            if (!mixOn || sp == null) return;
            bool storm = sp.why == "风暴来了"; int left = sp.PeopleLeft() + MixLater();
            Modal(false);
            ModalTitle(storm ? "风暴来了！" : "卡住了！", storm ? "storm" : "full");
            MText("还有 " + left + " 人没救出来", 20, UIKit.Hex("d5392a"));
            if (!storm) MText(sp.why, 13, UIKit.Hex("7a5a3a"));
            string how = storm ? "复活：风暴倒计时回到 " + Mathf.RoundToInt(STORM_START) + " 秒" : sp.CanBerth() ? "复活：多开一个泊位，再送一次直升机" : "复活：喇叭理队，再送一次直升机";
            MText(how, 14, UIKit.Hex("2f6b2a"));
            MButton("看广告复活", "8a5cff", () => { sfx.Play("click"); HideModal(); WatchAd("mix_revive", () => { if (sp == null) return; sp.Revive(STORM_START, storm); if (!storm) mixUse[0]++; MixBoostView(); sfx.Play("reveal"); }); });
            MButton("重新开始这一关", "ffb81f", () => { sfx.Play("click"); MixLeave(); MixPlay(mixI, mixK); }, true, "6b3d0c");
            MButton("回到地图", "14608f", () => { sfx.Play("click"); int ci = mixI; MixLeave(); ShowMixMap(ci); });
            FinishModal();
        }

        // ---------- 关卡里的画面 ----------
        Text[] mixCnt = new Text[3]; RectTransform[] mixBB = new RectTransform[3]; RectTransform mixHand; Text mixTutT;
        class MixTag { public int b; public string kind; public RectTransform r; public Text t; public GameObject model; }
        readonly List<MixTag> mixTags = new List<MixTag>(); readonly Dictionary<int, Transform> mixSticks = new Dictionary<int, Transform>();
        readonly Dictionary<int, GameObject> mixHeli = new Dictionary<int, GameObject>();

        void MixClean()
        {
            foreach (var s in mixSticks.Values) if (s) Destroy(s.gameObject);
            foreach (var h in mixHeli.Values) if (h) Destroy(h);
            mixTags.Clear(); mixSticks.Clear(); mixHeli.Clear(); mixHand = null; mixTutT = null;
            if (mixAnim) Destroy(mixAnim); mixAnim = null;
        }

        // 场景建好之后：石头、白船、海藻、锁链、撑杆跳的人
        void MixScene()
        {
            foreach (var q in sp.rocks)
            {
                var c = sp.Cell(q.x, q.y); var g = Art.Spawn((q.x + q.y) % 2 == 0 ? "rock_a" : "rock_b", spRoot, false);
                g.transform.position = W(c.x, c.y, 0.02f); g.transform.rotation = Quaternion.Euler(0, (q.x * 73 + q.y * 41) % 360, 0); g.transform.localScale = Vector3.one * sp.cs * 0.95f;
            }
            foreach (var b in sp.boats)
            {
                var u = spB[b.id]; if (u == null) continue;
                if (b.mys && !b.seen) { SetBoatColor(u, MYS); if (spNum[b.id]) spNum[b.id].text = "?"; }
                if (b.weed > 0)
                {
                    var w = new GameObject("weed"); w.transform.SetParent(u.g.transform, false);
                    float half = (b.len - 0.16f) / 2; var at = b.len == 1 ? new[] { 0f } : new[] { -half + 0.4f, half - 0.4f };
                    foreach (float z in at) { var m = Art.Spawn("weed_band", w.transform, true).transform; m.localPosition = new Vector3(0, 0, z); }
                    mixTags.Add(MixTagMake(b.id, "weed", w));
                }
                if (b.lockBy >= 0) mixTags.Add(MixTagMake(b.id, "lock", Art.Spawn("chain_band", u.g.transform, true)));
                if (b.key >= 0) mixTags.Add(MixTagMake(b.id, "key", null));
            }
            foreach (var s in sp.guys) if (s.pole && spG[s.id] != null) SetSpecial(spG[s.id], Rules.POLE);
        }
        MixTag MixTagMake(int b, string kind, GameObject model)
        {
            var r = OnSpec("tag"); r.sizeDelta = new Vector2(34, 34);
            var tg = new MixTag { b = b, kind = kind, r = r, model = model };
            if (kind == "weed")
            {
                UIKit.Img(r, "bg", UIKit.Circle, UIKit.Hex("2f9e3f")).rectTransform.Stretch();
                tg.t = UIKit.Txt(r, "t", "", 18, Color.white, TextAnchor.MiddleCenter, true); tg.t.rectTransform.Stretch();
            }
            else
            {
                var bb = sp.boats[b]; int pair = kind == "key" ? bb.key : sp.boats[bb.lockBy].key; string hex = KEY_COL[Mathf.Max(0, pair) % KEY_COL.Length];
                UIKit.Img(r, "bd", UIKit.Circle, UIKit.Hex(hex)).rectTransform.Stretch();
                UIKit.Img(r, "in", UIKit.Circle, UIKit.Hex("fff8e6")).rectTransform.Stretch(4, 4, 4, 4);
                var im = DecoImg(r, "card:" + (kind == "key" ? "keyicon" : "lockicon"), new Vector2(0.5f, 0.5f), Vector2.zero, 24); if (im) im.color = Color.Lerp(UIKit.Hex(hex), Color.black, 0.15f);
            }
            return tg;
        }

        // 关卡顶栏：照主线 HUD 摆（左上圆钮、中间奶白关卡牌 + 下面进度条 / 风暴条、右上圆钮），SpecHud 在混合模式里调这个
        Image mixFillG; RectTransform mixCloud, mixStar;
        void MixTop(RectTransform S)
        {
            var top = UIKit.Rect("top", S).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(370, 84));
            IconBtn(top, "icon_back", new Vector2(0, 1), new Vector2(26, -28), 52, () => { sfx.Play("click"); SpecBack(); }, "<");
            var help = IconBtn(top, "mix_help", new Vector2(1, 1), new Vector2(-26, -28), 46, () => { sfx.Play("click"); SpecRule(); }, "?");
            RectTransform lv;
            if (Skin.Has("plate_cream")) { lv = Skin.Img(top, "plate_cream").rectTransform; lv.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(170, 36)); }
            else lv = UIKit.Panel(top, "lv", UIKit.Cream, 4).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(170, 36));
            int tr = TierOf(mixK);
            var ln = UIKit.Txt(lv, "name", "第 " + (mixK + 1) + " 关 · <color=#" + (tr == 0 ? "2f8a3a" : TIER_C[tr]) + ">" + TIER_N[tr] + "</color>", 21, UIKit.Hex("5a3410"), TextAnchor.MiddleCenter);
            ln.rectTransform.Stretch(10, 3, 10, 0); ln.supportRichText = true;
            // 进度条（和主线一样的槽）：有风暴 = 风暴条 + 末端小乌云；不计时 = 救人进度 + 末端星星
            var bar = UIKit.Rect("bar", top).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-14, -46), new Vector2(184, 24));
            if (Skin.Has("bar_track")) Skin.Img(bar, "bar_track", sp.clock ? UIKit.Hex("c9d4ea") : Color.white).rectTransform.Stretch();
            else UIKit.Panel(bar, "track", UIKit.Hex("e3d6b8"), 0, 2).Stretch();
            string fk = sp.clock ? "bar_storm" : "bar_green";
            spFill = Skin.Has(fk) ? Skin.Img(bar, fk) : UIKit.Img(bar, "f", UIKit.Round, UIKit.Hex(sp.clock ? "8fc3ff" : "3fb950"));
            var fr = spFill.rectTransform; fr.anchorMin = new Vector2(0, 0); fr.anchorMax = new Vector2(0, 1); fr.pivot = new Vector2(0, 0.5f); fr.offsetMin = new Vector2(4, 4); fr.offsetMax = new Vector2(4, -4);
            spTime = UIKit.Txt(bar, "t", "", 14, Color.white, TextAnchor.MiddleCenter); Styled(spTime, UIKit.Hex(sp.clock ? "1c2a52" : "1f6b18"), 1.6f, 1.2f); spTime.rectTransform.Stretch(0, 2, 18, 0);
            mixCloud = mixStar = null;
            if (sp.clock && Skin.Has("icon_cloud")) { mixCloud = Skin.Img(bar, "icon_cloud").rectTransform; mixCloud.Place(new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(6, 2), new Vector2(40, 40)); }
            if (!sp.clock && Skin.Has("star_gold")) { mixStar = Skin.Img(bar, "star_gold").rectTransform; mixStar.Place(new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(4, 0), new Vector2(34, 34)); }
            spBarR = bar;
            // 第二行：奶白长条，岛名 · 第几波 · 还剩多少人
            RectTransform strip;
            if (Skin.Has("strip_cream")) { strip = Skin.Img(S, "strip_cream").rectTransform; strip.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -86), new Vector2(300, 36)); }
            else strip = UIKit.Panel(S, "strip", UIKit.Hex("fff8e6", 0.96f), 3).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -86), new Vector2(300, 36));
            spInfo = UIKit.Txt(strip, "i", "", 15, UIKit.Hex("6b4a2a"), TextAnchor.MiddleCenter); spInfo.rectTransform.Stretch(14, 3, 14, 0);
            spInfo.resizeTextForBestFit = true; spInfo.resizeTextMinSize = 10; spInfo.resizeTextMaxSize = 15;
            spTip = UIKit.Txt(S, "tip", "", 21, UIKit.Hex("fff27a"), TextAnchor.MiddleCenter, true); spTip.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(370, 30));
            Styled(spTip, UIKit.Hex("5a3410"), 1.8f, 1.5f);
        }
        // 顶栏每帧（SpecSync 设完条以后调）：主线同款的条宽；不计时的关，条显示救人进度
        void MixTopSync()
        {
            if (!spFill || !spBarR) return;
            float f;
            if (sp.clock) f = Mathf.Clamp01(sp.timer / SpSim.TIME_CAP);
            else
            {
                int here = sp.guys.Count, left = sp.PeopleLeft(), all = mixSaved + here + MixLater();
                f = all > 0 ? Mathf.Clamp01((mixSaved + here - left) / (float)all) : 0;
                spTime.text = (mixSaved + here - left) + " / " + all + " 人";
            }
            spFill.rectTransform.sizeDelta = new Vector2(Mathf.Max(0, (spBarR.rect.width - 8) * f), -8);
            if (mixCloud) mixCloud.localScale = Vector3.one * (sp.timer < 10 && sp.clock ? 1 + 0.12f * Mathf.Abs(Mathf.Sin(Fx.Now / 150f)) : 1);
        }

        void MixHud()
        {
            // 底部三个道具：和主线一模一样（绿边圆盘 70、间距 124、右上角白球数字）
            var bar = UIKit.Rect("mixboost", ui.spec).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(370, 100));
            var safeB = SafeRect(ui.spec, "safeB"); bar.SetParent(safeB, false);
            string[] icon = { "ai_heli", "ai_horn", "ai_dock" }, fb = { "↑", "≡", "+" };
            for (int k = 0; k < 3; k++)
            {
                RectTransform b; string disc = Skin.Has("disc_booster_g") ? "disc_booster_g" : "disc_booster";
                if (Skin.Has(disc)) { b = Skin.Img(bar, disc).rectTransform; b.name = "bb" + k; b.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2((k - 1) * 124, 2), new Vector2(70, 70)); }
                else b = UIKit.Disc(bar, "bb" + k, UIKit.Hex("ffd76a"), 5).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2((k - 1) * 116, 4), new Vector2(76, 76));
                if (Deco(icon[k])) DecoImg(b, icon[k], new Vector2(0.5f, 0.5f), new Vector2(0, 2), 54); else UIKit.Txt(b, "ic", fb[k], 34, UIKit.Brown).rectTransform.Stretch(0, 2, 0, 0);
                var badge = UIKit.Rect("cnt", b).Place(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-7, -7), new Vector2(30, 30));
                (Skin.Has("ball_white") ? Skin.Img(badge, "ball_white", UIKit.Green) : UIKit.Img(badge, "bg", UIKit.Circle, UIKit.Green)).rectTransform.Stretch();
                mixCnt[k] = UIKit.Txt(badge, "n", "", 13, Color.white); mixCnt[k].rectTransform.Stretch(-6, 2, -6, 0); Styled(mixCnt[k], new Color(0, 0, 0, 0.5f), 1.3f, 0);
                mixBB[k] = b; int kk = k; UIKit.Btn(b, () => MixBoost(kk));
            }
            MixBoostView();
            // 测试用：直接过关（正式版去掉）
            var sk = UIKit.Img(ui.spec, "skip", UIKit.Round, UIKit.Hex("5a6b7d", 0.8f), true).rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(8, 118), new Vector2(80, 26));
            UIKit.Txt(sk, "t", "测试:过关", 12, Color.white).rectTransform.Stretch();
            UIKit.Btn(sk, () => { if (mixBusy || ModalOpen) return; sfx.Play("click"); MixWin(); });
            // 新手关：主线教学同款（左边鹦鹉船长 + 奶白对话气泡）+ 手指
            if (mixK == 0)
            {
                var S = SafeRect(ui.spec, "safeT");
                var tb = UIKit.Rect("tut", S).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(370, 58));
                if (Skin.Has("bubble") && Skin.Has("mascot"))
                {
                    var bub = Skin.Img(tb, "bubble"); bub.preserveAspect = false; bub.rectTransform.Stretch(58, 0, 0, 0);
                    Skin.Img(tb, "mascot").rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(-6, 2), new Vector2(70, 70));
                    mixTutT = UIKit.Txt(tb, "t", MIX_TUT[mixI], 14, UIKit.Hex("5a3410")); mixTutT.rectTransform.Stretch(80, 4, 12, 4);
                }
                else
                {
                    UIKit.Img(tb, "bg", UIKit.Round, UIKit.Hex("123c5a", 0.62f)).rectTransform.Stretch();
                    mixTutT = UIKit.Txt(tb, "t", MIX_TUT[mixI], 14, Color.white); mixTutT.rectTransform.Stretch(8, 2, 8, 2);
                }
                mixHand = UIKit.Rect("hand", ui.spec); mixHand.anchorMin = mixHand.anchorMax = new Vector2(0.5f, 0.5f); mixHand.sizeDelta = new Vector2(64, 64); mixHand.pivot = new Vector2(0.3f, 1);
                if (Skin.Has("hand_point")) { var hi = Skin.Img(mixHand, "hand_point"); hi.rectTransform.Stretch(); hi.rectTransform.localRotation = Quaternion.Euler(0, 0, 180); }
                else UIKit.Txt(mixHand, "t", "▲", 40, UIKit.Hex("ffd21f"), TextAnchor.MiddleCenter, true).rectTransform.Stretch();
                mixHand.gameObject.SetActive(false);
            }
        }
        void MixBoostView()
        {
            for (int k = 0; k < 3; k++)
            {
                if (!mixCnt[k]) continue; bool can = mixUse[k] > 0 && (k != 2 || (sp != null && sp.CanBerth())) && !(k == 1 && sp != null && sp.queues.Count == 0);
                mixCnt[k].text = mixUse[k].ToString();
                var cg = mixBB[k].GetComponent<CanvasGroup>() ?? mixBB[k].gameObject.AddComponent<CanvasGroup>(); cg.alpha = can ? 1 : 0.45f;
                if (k == 0) mixBB[k].localScale = Vector3.one * (mixCrane ? 1.15f : 1);
            }
        }
        void MixBoost(int k)
        {
            if (sp == null || mixBusy || sp.status != 0) return;
            if (mixUse[k] <= 0) { sfx.Play("bad"); Toast("这一关的次数用完了"); Bump(mixBB[k]); return; }
            if (k == 0)
            {
                mixCrane = !mixCrane; sfx.Play("click");
                if (mixCrane) SpTipShow("点任意一艘船（被挡着的也行），直升机吊去码头");
            }
            else if (k == 1)
            {
                if (sp.queues.Count == 0) { sfx.Play("bad"); Toast("这张图的人不排队，喇叭用不上"); return; }
                int n = sp.Sort(); if (n == 0) { sfx.Play("bad"); Toast("现在理队没有用"); return; }
                mixUse[1]--; sfx.Play("reveal"); SpTipShow("喇叭一喊，能上船的人站到前面了");
            }
            else
            {
                if (!sp.CanBerth()) { sfx.Play("bad"); Toast("这张图没地方加码头"); return; }
                sp.AddBerth(); mixUse[2]--;
            }
            MixBoostView();
        }
        void MixCraneTap(SpBoat b)
        {
            string err = sp.Crane(b);
            if (err != null) { sfx.Play("bad"); Toast(err); return; }
            mixCrane = false; mixUse[0]--; MixBoostView();
        }
        bool MixEv(string e)
        {
            if (!mixOn) return false;
            switch (e)
            {
                case "vault": sfx.Play("reveal"); Haptics.Play(Haptics.Kind.Medium); return true;
                case "crane": sfx.Play("heli"); return true;
                case "sort": return true;
                case "berth":
                    {
                        var d = sp.docks[sp.docks.Count - 1]; var f = SpSim.Fwd(d.yaw); var g = Art.Spawn("dock_berth", spRoot, false);
                        var e2 = d.P + f * 1.1f; g.transform.position = W(e2.x, e2.y); g.transform.rotation = Quaternion.Euler(0, d.yaw * Mathf.Rad2Deg, 0); spPiers.Add(g);
                        SpBurst(d.P, "ffd23a", 1.6f); sfx.Play("unlock"); SpTipShow("多了一个泊位！"); MixBoostView(); return true;
                    }
            }
            return false;
        }

        // 每帧（SpecSync 最后调）
        void MixSync(float dt)
        {
            float now = Fx.Now;
            foreach (int id in sp.revealed)
            {
                var u = spB[id]; var b = sp.boats[id]; if (u == null) continue;
                SetBoatColor(u, COLS[b.c]); SpBurst(b.p, COLS[b.c], 1.3f); sfx.Play("reveal");
                if (spNum[id]) spNum[id].text = b.cap.ToString();
            }
            sp.revealed.Clear();
            foreach (int id in sp.vaulted) { var u = spB[id]; if (u != null) FloatText(u.g.transform.position + Vector3.up, "撑杆跳！"); }
            sp.vaulted.Clear();
            foreach (int id in sp.unlocked) { var b = sp.boats[id]; SpBurst(b.p, KEY_COL[0], 1.4f); sfx.Play("unlock"); }
            sp.unlocked.Clear();
            // 海藻数字、锁、钥匙
            for (int k = mixTags.Count - 1; k >= 0; k--)
            {
                var tg = mixTags[k]; var b = sp.boats[tg.b]; var u = spB[tg.b];
                bool gone = u == null || (tg.kind == "weed" && sp.WeedLeft(b) <= 0) || (tg.kind == "lock" && !sp.Locked(b)) || (tg.kind == "key" && b.st != SpSim.LOT) || (tg.kind != "key" && b.st != SpSim.LOT);
                if (gone)
                {
                    if (tg.r) Destroy(tg.r.gameObject);
                    if (tg.model) { if (u != null && b.st == SpSim.LOT) SpBurst(b.p, tg.kind == "weed" ? "52bb3c" : KEY_COL[0], 1.2f); Destroy(tg.model); }
                    if (tg.kind == "weed" && u != null && b.st == SpSim.LOT) sfx.Play("reveal");
                    mixTags.RemoveAt(k); continue;
                }
                if (tg.t) tg.t.text = sp.WeedLeft(b).ToString();
                var tr = u.g.transform; float ox = tg.kind == "lock" && b.weed > 0 ? 20 : 0;
                tg.r.anchoredPosition = ToCanvas(cam.WorldToScreenPoint(tr.position + Vector3.up * 0.9f)) + new Vector2(ox, 26);
            }
            // 撑杆跳的人：闪；跳的时候扛杆、跳得高
            bool hi = (int)(now / BLINK_MS) % 2 == 0;
            foreach (var s in sp.guys)
            {
                var v = spG[s.id]; if (v == null) continue;
                if (v.x != 0)
                {
                    if (s.st != 0 && s.st != 5) SetSpecial(v, 0);
                    else if (v.hi != hi && v.r) { v.hi = hi; string hex = hi ? ColorUtility.ToHtmlStringRGB(Color.Lerp(UIKit.Hex(COLS[v.c]), Color.white, 0.45f)).ToLower() : COLS[v.c]; v.r.sharedMaterials = Art.GuyMats(v.v, hex); }
                }
                if (s.vault && s.st == 1)
                {
                    var tr = v.g.transform; float h = Mathf.Clamp01(s.hop);
                    var pos = W(s.p.x, s.p.y, SP_Y); pos.y = SP_Y + Mathf.Sin(h * Mathf.PI) * 2.3f; tr.position = pos;
                    if (!mixSticks.TryGetValue(s.id, out var st)) { st = Prim(PrimitiveType.Cylinder, spRoot, Art.ColorMat(POLE_WOOD)); mixSticks[s.id] = st; }
                    if (st)
                    {
                        if (h < 0.6f) { var to = spB[s.boat] != null ? spB[s.boat].g.transform.position : pos; var a = W(s.from.x, s.from.y, 0.05f); var plant = Vector3.Lerp(a, to, 0.42f); plant.y = 0.05f; PoleSet(st, plant, pos + Vector3.up * 0.5f, 0.04f); }
                        else st.gameObject.SetActive(false);
                    }
                }
                else if (mixSticks.TryGetValue(s.id, out var st2)) { if (st2) Destroy(st2.gameObject); mixSticks.Remove(s.id); }
            }
            // 直升机吊船：船飞起来，头顶一架直升机
            foreach (var b in sp.boats)
            {
                var u = spB[b.id];
                if (b.crane && b.st == SpSim.MOVE && u != null && b.cum != null)
                {
                    float p = Mathf.Clamp01(b.s / Mathf.Max(0.01f, b.cum[b.cum.Length - 1]));
                    var tr = u.g.transform; tr.position += Vector3.up * Mathf.Sin(p * Mathf.PI) * 1.8f;
                    if (!mixHeli.TryGetValue(b.id, out var hg) || !hg) { hg = new GameObject("heli"); hg.transform.SetParent(spRoot, false); Art.Spawn("heli_body", hg.transform, false); mixHeli[b.id] = hg; }
                    hg.transform.position = tr.position + Vector3.up * 2.2f; hg.transform.localScale = Vector3.one * 0.9f;
                }
                else if (mixHeli.TryGetValue(b.id, out var h2)) { if (h2) Destroy(h2); mixHeli.Remove(b.id); if (b.crane) { b.crane = false; if (u != null) SpBurst(b.p, "ffffff", 1.2f); } }
            }
            // 新手关的手指
            if (mixHand)
            {
                var hb = ModalOpen ? null : sp.Hint(); bool show = hb != null && spB[hb.id] != null;
                if (mixHand.gameObject.activeSelf != show) mixHand.gameObject.SetActive(show);
                if (show) mixHand.anchoredPosition = ToCanvas(cam.WorldToScreenPoint(spB[hb.id].g.transform.position)) + new Vector2(6, -6 - Mathf.Abs(Mathf.Sin(now / 220)) * 12);
            }
            MixTopSync();
            if (mixCrane) foreach (var b in sp.boats) if (b.st == SpSim.LOT && spB[b.id] != null) { /* 直升机模式：船轻轻跳，提示可以点 */ var tr = spB[b.id].g.transform; tr.position += Vector3.up * Mathf.Abs(Mathf.Sin(now / 200 + b.id)) * 0.12f; }
        }
    }
}
