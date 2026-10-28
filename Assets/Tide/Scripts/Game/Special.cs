// 连击 + 特殊船（2026-10-22 快速原型：玩了十几关觉得雷同，学消消乐"玩得好 → 出特效 → 更容易连"的循环）。
// 连击 = 连着上船的人数（用户：按"开走几艘船"算，手点很难连上；改成按"接了多少人"）：
//   每上一个人 +1，RUN_WIN 毫秒内没人再上船就断。左边水面上救生圈连击牌子（见 ComboBuild），每上一个人晃一下，
//   每满 10 人大跳一下 + 风暴推后 1 秒；满 15、30、45、60 人轮流奖励一艘特殊船（一关最多 4 艘，规则见 Rules.cs 开头）：
//   集合船（金色"集"牌）：港湾里随机一艘船闪一下变成它；靠岸时队伍里同色的人插队上来直接坐满
//   彩虹船（船身七彩变色 + "★"牌）：额外送一艘，从海上开进空泊位，什么颜色的人都能上（码头满了就等有空位再来）
// 关卡里本来没有特殊船，只能靠连击挣；重开一关就没了（关卡数据是从 seed 重新生成的）。
// Game.chain（连着开走船）还在，只管喊"不错/漂亮"。
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using static Tide.Loc;

namespace Tide
{
    public partial class Game
    {
        const float RUN_STORM = 1;   // 每满 10 人风暴推后几秒
        static float RUN_WIN = 2500;  // 断连的间隔（毫秒）；测试时 -tideRunWin 改（真人节奏机器人：3 秒每关都拿满 4 艘特殊船，2 秒 1~3 艘）
        const int RUN_STEP = 10, SP_EVERY = 15, SP_MAX = 4;
        int spMade, rainbowWait;   // 这关已经奖励了几艘特殊船（轮流出：集合、彩虹、集合…）；还有几艘彩虹船等空泊位
        int run; float runT = -9999, runBigT = -9999;   // 连击人数、最后一个人上船的时间、最近一次满 10 的时间
        // 10-28 用户：连续救人 5 个以上，排队的人全体跳 2 秒；连击没断就一直续（每上一个人从头算 2 秒）
        const int JUMP_RUN = 5; const float JUMP_MS = 2000, JUMP_HOP = 380, JUMP_H = 0.32f;   // 一跳 0.38 秒，跳起 0.32（模型单位，人身高 0.9）
        float jumpT = -9999;
        // 全体跳的高度（每帧每人调）：同一个节拍一起跳，每人差一点点免得像复制粘贴；开头结尾 0.2 秒淡入淡出
        float JumpY(Guy g, float now)
        {
            float e = now - jumpT; if (e < 0 || e > JUMP_MS) return 0;
            float env = Mathf.Clamp01(e / 200) * Mathf.Clamp01((JUMP_MS - e) / 200);
            return Mathf.Abs(Mathf.Sin((e / JUMP_HOP + g.ph * 0.04f) * Mathf.PI)) * JUMP_H * env;
        }
        RectTransform comboBox, comboRing, comboSplash; UnityEngine.UI.Image comboTimer; UnityEngine.UI.Text comboTxt, comboSub; CanvasGroup comboCg;
        const float RING = 76;                  // 救生圈在界面上的直径
        static readonly Vector2 RING_C = new Vector2(0, 20);   // 救生圈中心在牌子里的位置（下面挂木牌）

        // BoardFx 里每上一个人调一次
        void RunHit()
        {
            run = Fx.Now - runT < RUN_WIN ? run + 1 : 1; runT = Fx.Now;
            if (run >= JUMP_RUN) { if (Fx.Now - jumpT > JUMP_MS) jumpT = Fx.Now; else jumpT = Fx.Now - ((Fx.Now - jumpT) % JUMP_HOP); }   // 续的时候对齐到节拍上，不会半空中一顿
            if (run < 2) return;
            if (!comboBox) ComboBuild();
            comboTxt.text = comboRing ? "×" + run : F("连击 ×{0}", run);
            if (run % RUN_STEP == 0)
            {
                runBigT = Fx.Now;
                sfx.Play("help", Mathf.Pow(1.06f, Mathf.Min(run / RUN_STEP - 1, 10))); Haptics.Play(Haptics.Kind.Light);
                if (stormOn && st.status == "play") { stormLeft = Mathf.Min(stormCap, stormLeft + RUN_STORM); comboSub.text = F("风暴 +{0} 秒", RUN_STORM.ToString("0.#")); }
                ComboStars(3 + Mathf.Min(run / RUN_STEP, 4));
            }
            if (run % SP_EVERY == 0 && spMade < SP_MAX) MakeSpecial();
        }

        // 牌子（2026-10-22 第 2 版，用户从概念图 art/ai/concept/combo/combo_concepts_D.png 选的「救生圈」，扁平欧美风）：
        // 红白救生圈（combo_ring，中间垫一块蓝圆底）里写 ×N，下面麻绳挂木牌（combo_sign_zh/en）；
        // 救生圈外面一道金色细环 = 连击还剩多久断（倒计时，代码画的）；×20 起后面一圈水花（combo_splash），每满 10 人飞出小星星（combo_star）。
        // 贴图是 codex 出的（art/tools/gen_skin.sh），没有贴图就退回纯文字
        void ComboBuild()
        {
            comboBox = OnCanvas("combo"); comboBox.sizeDelta = new Vector2(120, 140);
            comboCg = comboBox.gameObject.AddComponent<CanvasGroup>(); comboCg.blocksRaycasts = false;
            ComboPlace();
            comboRing = null; comboSplash = null; comboTimer = null;
            if (Skin.Has("combo_ring"))
            {
                RectTransform At(RectTransform r, Vector2 c, float w, float h) { r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f); r.anchoredPosition = c; r.sizeDelta = new Vector2(w, h); return r; }
                if (Skin.Has("combo_splash")) { comboSplash = At(Skin.Img(comboBox, "combo_splash").rectTransform, RING_C, RING * 1.55f, RING * 1.55f); comboSplash.localScale = Vector3.zero; }
                comboTimer = UIKit.Img(comboBox, "timer", CircleSprite(true), UIKit.Hex("ffd34d"));
                comboTimer.type = UnityEngine.UI.Image.Type.Filled; comboTimer.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
                comboTimer.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top; comboTimer.fillClockwise = false;
                At(comboTimer.rectTransform, RING_C, RING * 1.16f, RING * 1.16f);
                string sk = Loc.En ? "combo_sign_en" : "combo_sign_zh";
                if (Skin.Has(sk)) { var s = Skin.Img(comboBox, sk); At(s.rectTransform, RING_C + new Vector2(0, -RING * 0.5f - 12), 70, 36); }   // 在救生圈下面一层：吊绳塞到圈后面
                At(UIKit.Img(comboBox, "disc", CircleSprite(false), UIKit.Hex("3aa0e8")).rectTransform, RING_C, RING * 0.6f, RING * 0.6f);
                comboRing = At(Skin.Img(comboBox, "combo_ring").rectTransform, RING_C, RING, RING);
                comboTxt = UIKit.Txt(comboRing, "n", "", 21, Color.white, TextAnchor.MiddleCenter); Styled(comboTxt, UIKit.Hex("3a2312"), 2.6f, 1.6f);
                comboTxt.rectTransform.Stretch(0, 0, 0, 2);
            }
            else
            {
                comboTxt = UIKit.Txt(comboBox, "t", "", 30, UIKit.Hex("ff8a3d"), TextAnchor.MiddleCenter); Styled(comboTxt, UIKit.Hex("5a1d00"), 2.6f, 2.2f);
                comboTxt.rectTransform.Stretch(0, 0, 0, 30);
            }
            comboSub = UIKit.Txt(comboBox, "s", "", 14, UIKit.Hex("bfe3ff"), TextAnchor.LowerCenter); Styled(comboSub, UIKit.Hex("0e2a4a"), 1.6f, 1.2f);
            comboSub.rectTransform.Stretch(-30, -6, -30, 0);
        }

        // 满 10 人：几颗小星星从救生圈往外飞、转着淡掉
        void ComboStars(int n)
        {
            if (!comboBox || !Skin.Has("combo_star")) return;
            for (int i = 0; i < n; i++)
            {
                var r = Skin.Img(comboBox, "combo_star").rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = Vector2.one * UnityEngine.Random.Range(16f, 24f); r.anchoredPosition = RING_C;
                float a = (i + UnityEngine.Random.value * 0.6f) / n * Mathf.PI * 2, d = UnityEngine.Random.Range(50f, 75f), w = UnityEngine.Random.Range(-200f, 200f);
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); var im = r.GetComponent<UnityEngine.UI.Image>();
                Fx.Run(async () =>
                {
                    await Fx.Tween(650, p =>
                    {
                        if (!r) return;
                        float q = 1 - (1 - p) * (1 - p);
                        r.anchoredPosition = RING_C + dir * (RING * 0.4f + d * q);
                        r.localRotation = Quaternion.Euler(0, 0, w * p); r.localScale = Vector3.one * (p < 0.2f ? p / 0.2f : 1);
                        im.color = new Color(1, 1, 1, p < 0.6f ? 1 : 1 - (p - 0.6f) / 0.4f);
                    });
                    if (r) Destroy(r.gameObject);
                });
            }
        }

        // 程序画的圆（抗锯齿）：ring = 细圆环（连击倒计时），否则实心圆（救生圈中间的蓝底）
        static Sprite discSp, annSp;
        static Sprite CircleSprite(bool ring)
        {
            if (ring ? annSp : discSp) return ring ? annSp : discSp;
            const int N = 128; var tx = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N / 2f, N / 2f));
                    float a = Mathf.Clamp01(N / 2f - 1 - d);
                    if (ring) a = Mathf.Min(a, Mathf.Clamp01(d - (N / 2f - 8)));
                    tx.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            tx.Apply();
            var sp = Sprite.Create(tx, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100);
            if (ring) annSp = sp; else discSp = sp;
            return sp;
        }

        // 10-28 草图：排队区右边、小屋下面那一格（第 1 道右头外面的沙滩，屏幕约 82%, 32%），比原来小一圈
        //（原来在左上角草地上；再早放在码头下面的水面左边，宽一点的关卡会盖住港湾左边的船）
        const float COMBO_K = 0.75f;
        void ComboPlace()
        {
            if (!comboBox || L == null) return;
            comboBox.anchoredPosition = ToCanvas(cam.WorldToScreenPoint(W(lay.xT + 0.3f * QS + 1.0f, lay.zQ1 - 0.05f * QLG))) - new Vector2(0, RING_C.y * 0.25f);
        }

        // 每帧：每上一个人救生圈晃一下、满 10 人整个牌子大跳；连击越高牌子越大、×20 起水花；倒计时环；RUN_WIN 内没人再上船就淡出（连击断了）
        void ComboTick()
        {
            if (!comboBox) return;
            float e = Fx.Now - runT, eb = Fx.Now - runBigT;
            float s = 0.9f + 0.06f * Mathf.Min(run / RUN_STEP, 3);
            if (eb < 300) s *= eb < 130 ? Mathf.Lerp(0.75f, 1.25f, eb / 130) : Mathf.Lerp(1.25f, 1, (eb - 130) / 170);
            comboBox.localScale = Vector3.one * s * COMBO_K;
            comboCg.alpha = e < RUN_WIN - 500 ? 1 : Mathf.Clamp01(1 - (e - (RUN_WIN - 500)) / 500);
            if (comboRing)
            {
                comboRing.localRotation = Quaternion.Euler(0, 0, e < 180 ? Mathf.Sin(e / 180 * Mathf.PI * 2) * 8 : 0);
                comboRing.localScale = Vector3.one * (e < 120 ? 1 + 0.08f * Mathf.Sin(e / 120 * Mathf.PI) : 1);
            }
            if (comboTimer)
            {
                float left = Mathf.Clamp01(1 - e / RUN_WIN);
                comboTimer.fillAmount = left;
                comboTimer.color = left > 0.35f ? UIKit.Hex("ffd34d") : Color.Lerp(UIKit.Hex("ff5a3c"), UIKit.Hex("ffd34d"), left / 0.35f);
            }
            if (comboSplash)
            {
                bool on = run >= 2 * RUN_STEP;
                float pop = eb < 350 ? 1 + 0.25f * Mathf.Sin(eb / 350 * Mathf.PI) : 1;
                float target = on ? (1 + 0.04f * Mathf.Sin(Fx.Now / 260)) * pop : 0;
                comboSplash.localScale = Vector3.one * Mathf.MoveTowards(comboSplash.localScale.x, target, Time.unscaledDeltaTime * 4);
                comboSplash.localRotation = Quaternion.Euler(0, 0, Fx.Now / 60 % 360);
            }
            comboSub.color = new Color(comboSub.color.r, comboSub.color.g, comboSub.color.b, eb < 1500 ? 1 : Mathf.Clamp01(1 - (eb - 1500) / 500));
        }

        // 港湾里挑一艘普通船变成特殊船：优先挑现在就开得出去的（马上能用上），没有就随便挑一艘
        void MakeSpecial()
        {
            if (st.status != "play") return;
            if (spMade % 2 == 1) { spMade++; rainbowWait++; RainbowTip(); TrySendRainbow(); return; }
            var all = Rules.InLot(L, st).Where(b => L.boats[b].sp == 0 && (L.boats[b].mys == 0 || st.seen[b] != 0) && boatV[b] != null && boatV[b].state == "lot").ToList();
            if (all.Count == 0) return;
            var free = all.Where(b => Rules.CanGo(L, st, b)).ToList();
            var pick = free.Count > 0 && UnityEngine.Random.value < 0.6f ? free : all;
            int bb = pick[UnityEngine.Random.Range(0, pick.Count)];
            int kind = 1; spMade++;
            L.boats[bb].sp = kind;
            var u = boatV[bb]; var g = u.g.transform;
            SpecialLook(u, kind);
            sfx.Play("reveal"); sfx.Play("star3"); Haptics.Play(Haptics.Kind.Success);
            Burst(g.position + Vector3.up * 0.7f, kind == 1 ? UIKit.Hex("ffd34d") : UIKit.Hex("ff7ac0"), 22, 1.4f);
            float s0 = lay.gs;   // 挑的都是港湾里的船：网格缩小的关船本来就小，弹完回到 lay.gs
            Fx.Run(() => Fx.Tween(420, p => { if (g) g.localScale = Vector3.one * s0 * (1 + 0.3f * Mathf.Sin(p * Mathf.PI)); }));
            if (!save.seen.Contains("sp_gather")) { save.seen.Add("sp_gather"); save.Persist(); Toast(T("连击奖励：集合船！一靠岸，同色的人全部插队上船")); }
            else Toast(T("连击奖励：集合船！"));
        }

        void RainbowTip()
        {
            if (!save.seen.Contains("sp_rainbow")) { save.seen.Add("sp_rainbow"); save.Persist(); Toast(T("连击奖励：彩虹船！什么颜色的人都能上")); }
            else Toast(T("连击奖励：彩虹船！"));
        }

        // 送彩虹船：码头有空泊位就从海上开进来；没有就等（SpecialTick 里每帧再试）
        void TrySendRainbow()
        {
            if (rainbowWait <= 0 || st.status != "play" || st.berth.IndexOf(-1) < 0) return;
            var ev = new List<Ev>();
            int b = Rules.AddRainbow(L, st, 2, ev); if (b < 0) return;
            rainbowWait--;
            System.Array.Resize(ref boatV, L.boats.Count);
            AddBoatView(b, false); SetBoatColor(boatV[b], "ff7ac0"); SpecialLook(boatV[b], 2);
            sfx.Play("reveal"); sfx.Play("star3"); Haptics.Play(Haptics.Kind.Success);
            RunEvents(ev);
        }

        void SpecialLook(BoatV u, int kind)
        {
            if (kind == 1) { if (Skin.Has("icon_gather")) IconLabel(u.g.transform, new Vector3(0, 0.95f, 0), "icon_gather", 36); else NewLabel(u.g.transform, new Vector3(0, 0.95f, 0), T("集"), UIKit.Hex("ffc530"), 16, 30, UIKit.Hex("5a3300")); }
            else
            {
                if (Skin.Has("icon_rainbow")) IconLabel(u.g.transform, new Vector3(0, 0.95f, 0), "icon_rainbow", 36); else NewLabel(u.g.transform, new Vector3(0, 0.95f, 0), "★", UIKit.Hex("ff5fa8"), 18, 30, Color.white);
                if (u.ol != null) { var om = Art.OutlineMat(UIKit.Hex("3a2a55")); foreach (var r in u.ol) r.sharedMaterial = om; }
            }
        }

        // 彩虹船：船身颜色一直在七彩里转（只改材质颜色，不新建材质）
        void SpecialTick()
        {
            if (L == null || boatV == null) return;
            if (rainbowWait > 0) TrySendRainbow();
            ComboTick();
            for (int b = 0; b < boatV.Length; b++)
            {
                var u = boatV[b]; if (u == null || !u.g || b >= L.boats.Count || L.boats[b].sp != 2) continue;
                float h = (Fx.Now / 1600f + b * 0.13f) % 1f;
                var c = Color.HSVToRGB(h, 0.62f, 1);
                foreach (var m in u.hull) m.SetColor("_BaseColor", Lit(c));
                foreach (var m in u.deck) m.SetColor("_BaseColor", Lit(Color.Lerp(c, Color.white, 0.45f)));
            }
        }

        // 集合船靠岸：队伍按新的顺序重新上色（和喇叭道具一样，换了颜色的人蹦一下）
        void GatherFx(Ev e)
        {
            QueueFx(async () =>
            {
                var u = boatV[e.b]; if (u != null && u.arrive != null) await u.arrive;
                for (int k = 0; k < qGuys.Count; k++)
                {
                    int c = st.q[vq + k];
                    if (qGuys[k].c != c) { RecolorGuy(qGuys[k], c); var bd = qGuys[k].body; Fx.Run(() => Fx.Tween(300, p => bd.localPosition = new Vector3(0, Mathf.Sin(p * Mathf.PI) * 0.3f, 0))); }
                }
                sfx.Play("reveal"); FloatText(u != null ? u.g.transform.position + Vector3.up : Vector3.zero, T("集合！"));
            });
        }
    }
}
