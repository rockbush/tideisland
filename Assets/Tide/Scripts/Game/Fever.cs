// 连击"热潮"（10-26 用户：连击多了要有爽感、能保持住，玩家才会去争取点快一点）。
// 现在的连击奖励（Special.cs）都是一次性的；这里加一个持续的状态：连着的时候整个游戏"热"起来，一断就回到平常。
//   热起来 FEVER1（连击 10 人）：上船音阶往上爬（一直都有）、音乐加一层鼓点、排队的人欢呼着蹦
//   顺风   FEVER2（25 人）：屏幕四周一圈暖金色光、开走的船尾浪带金色亮片、上船动画加快、上船音多一个高八度的亮音、音乐加贝斯和琶音
//   狂潮   FEVER3（45 人）：音乐全开（铜管）、乌云往后退、阳光变暖变亮
// 第 2 步（还没做）：快断提醒、升档停顿 + 大字；第 3 步：断了结算金币。
using UnityEngine;

namespace Tide
{
    public partial class Game
    {
        const int FEVER1 = 10, FEVER2 = 25, FEVER3 = 45;
        float feverK;          // 平滑过的档位（0~3），画面和音乐都跟着它慢慢变
        int feverTier;         // 上一帧的档位：升档那一下响一声
        UnityEngine.UI.Image feverGlow;

        // 现在在第几档：连击还没断才算
        int FeverTier => Fx.Now - runT >= RUN_WIN ? 0 : run >= FEVER3 ? 3 : run >= FEVER2 ? 2 : run >= FEVER1 ? 1 : 0;
        // 上船动画的时长倍数：顺风 0.8、狂潮 0.7（越连越顺手）
        float BoardSpeed => FeverTier >= 3 ? 0.7f : FeverTier >= 2 ? 0.8f : 1f;

        // 上船音：C 大调一个音一个音往上爬，爬到顶（满 10 人）绕回来再爬
        static readonly int[] LADDER = { 0, 2, 4, 5, 7, 9, 11, 12, 14, 16 };
        const float LADDER_BASE = 0.8435f;    // "board" 音本来是 620Hz，乘这个 = C5，和背景音乐同调
        float ComboPitch() => LADDER_BASE * Mathf.Pow(2, LADDER[Mathf.Max(0, run - 1) % LADDER.Length] / 12f);

        void BoardSound()
        {
            float p = ComboPitch();
            sfx.Play("board", p);
            if (FeverTier >= 2) sfx.Play("board", p * 2, 0.3f);   // 顺风起：再叠一个高八度的轻音，亮晶晶的
        }

        void FeverTick(float dt)
        {
            int tier = st != null && st.status == "play" ? FeverTier : 0;
            if (tier > feverTier && tier >= 2) { sfx.Play("star3", 1 + 0.08f * tier); Haptics.Play(Haptics.Kind.Medium); }   // 升到顺风 / 狂潮（热起来那一下 Special.cs 满 10 人已经响了）
            feverTier = tier;
            // 升得快（0.6 秒到位），降得慢一点（断了 1 秒多才完全退）
            feverK = Mathf.MoveTowards(feverK, tier, dt * (tier > feverK ? 1.7f : 2.2f));
            sfx.fever = feverK;
            if (!feverGlow)
            {
                feverGlow = UIKit.Img(ui.vig.transform.parent, "fever", GlowSprite(), new Color(1, 0.78f, 0.3f, 0));
                feverGlow.type = UnityEngine.UI.Image.Type.Sliced; feverGlow.rectTransform.Stretch(0, 0, 0, 0);
                feverGlow.transform.SetSiblingIndex(ui.vig.transform.GetSiblingIndex() + 1);
                feverGlow.raycastTarget = false;
            }
            float g = Mathf.Clamp01(feverK - 1);                           // 顺风才开始亮
            float pulse = 0.85f + 0.15f * Mathf.Sin(Fx.Now / 320);
            feverGlow.color = new Color(1, 0.8f, 0.32f, g * (0.55f + 0.3f * Mathf.Clamp01(feverK - 2)) * pulse);
        }

        // 金光贴图：只亮在最外面一圈（离边 GLOW 像素内，越靠边越亮），九宫格拉伸，四条边一样粗
        //（第一版借用 UIKit.Soft：圆形渐变从半径 45% 就开始变实，竖屏上整个画面像蒙了一层黄雾）
        static Sprite glowSp;
        static Sprite GlowSprite()
        {
            if (glowSp) return glowSp;
            const int N = 128, GLOW = 56;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float e = Mathf.Min(Mathf.Min(x, N - 1 - x), Mathf.Min(y, N - 1 - y));
                    float a = Mathf.Pow(1 - Mathf.Clamp01(e / GLOW), 2.4f);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px); t.Apply();
            return glowSp = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(GLOW + 2, GLOW + 2, GLOW + 2, GLOW + 2));
        }

        // 顺风起，开走的船尾浪里夹着金色亮片（往上飘、比白浪花活得久一点）
        void FeverWake(Vector3 p)
        {
            if (FeverTier < 2 || Random.value > 0.6f) return;
            var m = NewCube(UIKit.Hex(Random.value < 0.5f ? "ffd34d" : "fff3b0"), new Vector3(p.x + (Random.value - 0.5f) * 0.4f, 0.2f, p.z + (Random.value - 0.5f) * 0.4f));
            m.localScale = Vector3.one * 0.08f;
            parts.Add(new Part { m = m, v = new Vector3((Random.value - 0.5f) * 0.6f, 1.2f + Random.value, (Random.value - 0.5f) * 0.6f), life = 0.8f, g = 1.5f });
        }

        // 狂潮：乌云往后退（只是画面，风暴计时不变）
        float FeverStormEase => 0.45f * Mathf.Clamp01(feverK - 2);
    }
}
