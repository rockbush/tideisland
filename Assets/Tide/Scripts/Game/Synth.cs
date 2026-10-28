// 声音：照网页版的 WebAudio 合成（振荡器 + 指数衰减），启动时一次性渲染成 AudioClip。
// 外部音频（放进去就用，没有就用合成的）：
//   Resources/Audio/music_calm、music_tense   背景音乐（平时 / 风暴快到、泊位快满），循环播放
//   Resources/Audio/voice/<名>                 英文语音：praise1~5 amazing win hard super storm berth delay sorted lose
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tide
{
    public class Synth : MonoBehaviour
    {
        const int SR = 44100;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        AudioSource[] pool;
        int next;
        AudioSource music, voice;
        readonly Dictionary<string, AudioClip> voices = new Dictionary<string, AudioClip>();
        AudioClip calm, tense;
        public bool muted, musicOff;   // muted = 音效关，musicOff = 音乐关
        public float tensT, stormT;   // stormT：风暴进度（0 晴 → 1 风暴到了），过了七成半换紧张的音乐
        public float fever;           // 连击热潮档位（0~3，平滑过的，Fever.cs）：平常的音乐上一层层加乐器
        AudioSource[] layers;         // 热潮三层：鼓 / 贝斯 + 琶音 / 铜管；和合成的平常音乐同一个循环长度，对齐着一起放
        static readonly float[] LAYER_VOL = { 0.9f, 0.8f, 0.7f };
        System.Random rnd = new System.Random(7);

        class Buf
        {
            public float[] d;
            public Buf(float sec) { d = new float[Mathf.CeilToInt(sec * SR)]; }
        }

        static float Osc(string type, double ph)
        {
            double f = ph - Math.Floor(ph);
            switch (type)
            {
                case "triangle": return (float)(f < 0.5 ? 4 * f - 1 : 3 - 4 * f);
                case "square": return f < 0.5 ? 1f : -1f;
                case "sawtooth": return (float)(2 * f - 1);
                default: return (float)Math.Sin(ph * 2 * Math.PI);
            }
        }

        // 和 JS 的 tone(f,d,type,v,t,f2) 一样：频率可以指数滑到 f2，音量从 v 指数掉到 0.001
        static void Tone(Buf b, float f, float d, string type = "sine", float v = 0.12f, float t = 0, float f2 = 0)
        {
            int s0 = (int)(t * SR), n = (int)((d + 0.03f) * SR);
            double ph = 0;
            for (int i = 0; i < n && s0 + i < b.d.Length; i++)
            {
                float x = i / (float)SR, k = Mathf.Min(1, x / d);
                float fr = f2 > 0 ? f * Mathf.Pow(f2 / f, k) : f;
                ph += fr / SR;
                float g = v * Mathf.Pow(0.001f / v, k);
                if (i < 40) g *= i / 40f;           // 去掉起音的爆音
                b.d[s0 + i] += Osc(type, ph) * g;
            }
        }

        void Noise(Buf b, float d, float v, float f1, float f2, float t = 0)
        {
            int s0 = (int)(t * SR), n = (int)(d * SR);
            float y = 0;
            for (int i = 0; i < n && s0 + i < b.d.Length; i++)
            {
                float k = i / (float)n, fc = f1 * Mathf.Pow(f2 / f1, k), a = 1 - Mathf.Exp(-2 * Mathf.PI * fc / SR);
                y += a * ((float)rnd.NextDouble() * 2 - 1 - y);     // 一阶低通
                b.d[s0 + i] += y * v * Mathf.Pow(0.001f / v, k) * 1.6f;
            }
        }

        // 合成音效整体放大（10-26 用户：按钮、弹窗、特效的声音太小——合成的峰值只有 0.05~0.16，换上真的主题曲以后更显得小）。
        // 超过 0.7 的部分柔和压住，几个音叠在一起（过关号角的和弦）也不会爆音
        const float SFX_GAIN = 3f;
        public static float sfxPeakMax;   // 测试时看放大后最响的一下有多响
        AudioClip Make(string name, Buf b)
        {
            for (int i = 0; i < b.d.Length; i++)
            {
                float y = b.d[i] * SFX_GAIN, a = Mathf.Abs(y);
                if (a > 0.7f) y = Mathf.Sign(y) * (0.7f + 0.28f * (float)Math.Tanh((a - 0.7f) / 0.28f));
                b.d[i] = y; sfxPeakMax = Mathf.Max(sfxPeakMax, Mathf.Abs(y));
            }
            var c = AudioClip.Create(name, b.d.Length, 1, SR, false);
            c.SetData(b.d, 0);
            return c;
        }

        void Add(string name, float sec, Action<Buf> fn)
        {
            var ext = Resources.Load<AudioClip>("Audio/sfx/" + name);   // 放了同名的音效文件就用文件，不合成
            if (ext) { clips[name] = ext; return; }
            var b = new Buf(sec); fn(b); clips[name] = Make(name, b);
        }

        // 铜管感的一个音：锯齿 + 三角叠在一起，带一点颤音，起音稍慢（不是"叮"一下）
        static void Brass(Buf b, float f, float d, float v, float t)
        {
            int s0 = (int)(t * SR), n = (int)(d * SR); double p1 = 0, p2 = 0;
            for (int i = 0; i < n && s0 + i < b.d.Length; i++)
            {
                float x = i / (float)SR, vib = 1 + 0.006f * Mathf.Sin(x * 2 * Mathf.PI * 5.5f) * Mathf.Min(1, x * 4);
                p1 += f * vib / SR; p2 += f * 2.003f * vib / SR;
                float env = Mathf.Min(1, x / 0.03f) * (x > d - 0.12f ? Mathf.Max(0, (d - x) / 0.12f) : 1);
                b.d[s0 + i] += (Osc("sawtooth", p1) * 0.55f + Osc("triangle", p2) * 0.3f + Osc("sine", p1) * 0.4f) * v * env;
            }
        }
        // 人群欢呼：几段带通噪声，音量一起一伏，像很多人在喊"耶——"
        void Cheer(Buf b, float d, float v, float t)
        {
            int s0 = (int)(t * SR), n = (int)(d * SR);
            float y1 = 0, y2 = 0, ph = 0;
            for (int i = 0; i < n && s0 + i < b.d.Length; i++)
            {
                float x = i / (float)SR, k = x / d;
                float w = (float)rnd.NextDouble() * 2 - 1;
                y1 += 0.18f * (w - y1); y2 += 0.04f * (y1 - y2);            // 两级低通 → 减掉更低的 → 人声那一段频率
                ph += (6 + 3 * Mathf.Sin(x * 1.3f)) / SR;
                float trem = 0.75f + 0.25f * Mathf.Sin(ph * 2 * Mathf.PI);
                float env = Mathf.Min(1, x / 0.25f) * (k > 0.6f ? (1 - k) / 0.4f : 1);
                b.d[s0 + i] += (y1 - y2) * v * env * trem * 3.2f;
            }
        }

        // 直升机：低通噪声被旋翼一下一下"拍"出来（每秒 17 下），再垫一点低频，开头渐强、结尾渐弱（飞走）
        void Rotor(Buf b, float d, float v)
        {
            int n = Mathf.Min(b.d.Length, (int)(d * SR)); float y = 0, y2 = 0;
            for (int i = 0; i < n; i++)
            {
                float x = i / (float)SR, k = x / d;
                float w = (float)rnd.NextDouble() * 2 - 1;
                y += 0.12f * (w - y); y2 += 0.02f * (y - y2);
                float c = 0.5f + 0.5f * Mathf.Cos(x * 2 * Mathf.PI * 17);
                float chop = c * c * c * c;
                float env = Mathf.Min(1, x / 0.35f) * (k > 0.7f ? (1 - k) / 0.3f : 1);
                b.d[i] += ((y - y2 * 0.6f) * 2.4f * (0.25f + chop) + Mathf.Sin(x * 2 * Mathf.PI * 62) * chop * 0.35f) * v * env;
            }
        }

        void Awake()
        {
            pool = new AudioSource[12];
            for (int i = 0; i < pool.Length; i++) { pool[i] = gameObject.AddComponent<AudioSource>(); pool[i].playOnAwake = false; }
            Add("go", 0.5f, b => { Tone(b, 300, 0.16f, "triangle", 0.1f, 0, 620); Noise(b, 0.35f, 0.08f, 1500, 400); });
            Add("board", 0.2f, b => { Tone(b, 620, 0.07f, "triangle", 0.1f); Tone(b, 930, 0.1f, "triangle", 0.07f, 0.04f); });
            Add("sail", 0.8f, b => { float[] fs = { 523, 659, 784, 1047 }; for (int i = 0; i < 4; i++) Tone(b, fs[i], 0.2f, "triangle", 0.13f, i * 0.07f); Noise(b, 0.5f, 0.1f, 600, 1800); });
            Add("dock", 0.2f, b => Tone(b, 180, 0.12f, "sine", 0.14f, 0, 120));
            Add("bad", 0.3f, b => { Tone(b, 170, 0.12f, "square", 0.07f); Tone(b, 140, 0.14f, "square", 0.06f, 0.08f); });
            Add("warn", 0.3f, b => { Tone(b, 920, 0.08f, "square", 0.07f); Tone(b, 920, 0.08f, "square", 0.07f, 0.14f); });
            Add("win", 0.9f, b => { float[] fs = { 523, 659, 784, 1047, 1319, 1568 }; for (int i = 0; i < 6; i++) Tone(b, fs[i], 0.26f, "triangle", 0.16f, i * 0.1f); });
            // 过关号角：小鼓滚奏 → 上行三连音 → 长长的大三和弦 + 镲片；后面接人群欢呼
            Add("fanfare", 2.8f, b =>
            {
                for (int k = 0; k < 10; k++) Noise(b, 0.05f, 0.05f + k * 0.006f, 2500, 900, k * 0.035f);          // 小鼓滚奏
                float[] up = { 392, 523, 659 };
                for (int i = 0; i < 3; i++) Brass(b, up[i], 0.15f, 0.07f, 0.36f + i * 0.13f);
                float c = 0.75f;
                foreach (var f in new float[] { 523, 659, 784, 1047 }) Brass(b, f, 1.5f, 0.055f, c);           // C 大三和弦
                Tone(b, 131, 1.4f, "sine", 0.16f, c);                                                         // 低音
                Noise(b, 1.6f, 0.11f, 9000, 3500, c);                                                         // 镲片
                Tone(b, 90, 0.25f, "sine", 0.22f, c, 45);                                                     // 大鼓
            });
            Add("cheer", 2.6f, b => Cheer(b, 2.5f, 0.12f, 0));
            Add("fail", 0.65f, b => Tone(b, 420, 0.55f, "sawtooth", 0.1f, 0, 110));
            Add("click", 0.1f, b => Tone(b, 640, 0.05f, "triangle", 0.09f));
            Add("coin", 0.25f, b => { Tone(b, 1250, 0.07f, "square", 0.05f); Tone(b, 1700, 0.13f, "square", 0.05f, 0.06f); });
            Add("reveal", 0.25f, b => { Tone(b, 700, 0.08f, "triangle", 0.12f); Tone(b, 1050, 0.14f, "triangle", 0.12f, 0.07f); });
            Add("help", 0.4f, b => { for (int k = 0; k < 3; k++) Tone(b, 950 + k * 130, 0.1f, "triangle", 0.08f, k * 0.12f, 1350 + k * 100); });
            for (int k = 0; k < 5; k++) { int kk = k; Add("pop" + k, 0.2f, b => { Tone(b, 500 + kk * 90, 0.09f, "triangle", 0.1f, 0, 900 + kk * 90); Noise(b, 0.12f, 0.08f, 3500, 900); }); }
            Add("crane", 0.6f, b => { Tone(b, 260, 0.5f, "sawtooth", 0.05f, 0, 520); Noise(b, 0.5f, 0.06f, 900, 2400); });
            Add("heli", 2.2f, b => Rotor(b, 2.2f, 0.16f));
            Add("unlock", 0.45f, b => { float[] fs = { 660, 880, 1320 }; for (int i = 0; i < 3; i++) Tone(b, fs[i], 0.16f, "triangle", 0.12f, i * 0.08f); });
            Add("crack", 0.35f, b => { Noise(b, 0.09f, 0.2f, 6000, 1800); Tone(b, 1900, 0.12f, "square", 0.04f, 0.01f, 900); Tone(b, 2600, 0.2f, "triangle", 0.05f, 0.03f, 2300); });   // 锁碎掉：咔嚓一声 + 一点金属叮
            for (int j = 1; j <= 3; j++) { int jj = j; Add("star" + j, 0.3f, b => { Tone(b, 660 + jj * 220, 0.18f, "triangle", 0.12f); Tone(b, 500 + (jj + 1) * 90, 0.09f, "triangle", 0.1f, 0, 900 + (jj + 1) * 90); }); }
            Add("thunder", 1.8f, b => { Noise(b, 1.7f, 0.22f, 380, 50); Noise(b, 0.5f, 0.12f, 1600, 300, 0.05f); Tone(b, 52, 1.4f, "sine", 0.12f, 0, 38); });
            var fc = Resources.Load<AudioClip>("Audio/music_calm"); var ft = Resources.Load<AudioClip>("Audio/music_tense");
            calm = fc ? fc : MusicLoop(false);
            tense = ft ? ft : fc ? fc : MusicLoop(true);
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true; music.clip = calm; music.volume = fc ? 0.55f : 1; music.Play();
            voice = gameObject.AddComponent<AudioSource>(); voice.playOnAwake = false;
            // 热潮音乐层：只在平常音乐是合成的时候加（换成外部音乐文件以后拍子对不上，就不加）
            if (!fc)
            {
                layers = new AudioSource[3];
                for (int i = 0; i < 3; i++) { var a = gameObject.AddComponent<AudioSource>(); a.loop = true; a.playOnAwake = false; a.volume = 0; a.clip = FeverLayer(i); layers[i] = a; }
            }
        }

        // 热潮音乐层：节拍、和弦走向和 MusicLoop(false) 一模一样（每步 0.29 秒，32 步，C - Am - F - G）
        AudioClip FeverLayer(int layer)
        {
            const float step = 0.29f;
            var b = new Buf(step * 32 + 0.6f);
            for (int ms = 0; ms < 32; ms++)
            {
                int bar = ms / 8 % 4, k = ms % 8; float t = ms * step; var c = CHORDS[bar];
                if (layer == 0)          // 鼓：底鼓在 1、3 拍，拍手在 2、4 拍，每半拍一下踩镲
                {
                    if (k % 4 == 0) Tone(b, 120, 0.16f, "sine", 0.2f, t, 45);
                    if (k % 4 == 2) Noise(b, 0.12f, 0.07f, 2600, 900, t);
                    Noise(b, 0.035f, k % 2 == 1 ? 0.03f : 0.018f, 9500, 6500, t);
                }
                else if (layer == 1)     // 贝斯（低八度根音，切分）+ 高八度的琶音
                {
                    if (k == 0 || k == 3 || k == 6) Tone(b, c[0] / 2f, 0.26f, "triangle", 0.075f, t);
                    Tone(b, c[PAT[(k + 3) % 8]] * 2, 0.11f, "triangle", 0.022f, t);
                }
                else                     // 铜管和弦（每小节第 1、4 步两下）+ 一条高音小旋律
                {
                    if (k == 0 || k == 3) { Brass(b, c[1], 0.22f, 0.035f, t); Brass(b, c[2], 0.22f, 0.03f, t); }
                    if (k == 4 || k == 6) Tone(b, c[3] * 2, 0.18f, "sine", 0.03f, t);
                }
            }
            int L = Mathf.RoundToInt(step * 32 * SR);
            var o = new float[L];
            for (int i = 0; i < b.d.Length; i++) o[i % L] += b.d[i];
            var clip = AudioClip.Create("fever" + layer, L, 1, SR, false);
            clip.SetData(o, 0);
            return clip;
        }

        static readonly int[][] CHORDS = { new[] { 262, 330, 392, 523 }, new[] { 220, 262, 330, 440 }, new[] { 175, 220, 262, 349 }, new[] { 196, 247, 294, 392 } };
        static readonly int[][] TENSE = { new[] { 220, 262, 330, 440 }, new[] { 175, 208, 262, 349 }, new[] { 147, 175, 220, 294 }, new[] { 165, 208, 247, 330 } };
        static readonly int[] PAT = { 0, 2, 1, 3, 2, 1, 3, 2 };

        AudioClip MusicLoop(bool tz)
        {
            float step = tz ? 0.29f - 0.06f * 0.7f : 0.29f;
            var b = new Buf(step * 32 + 0.01f);
            for (int ms = 0; ms < 32; ms++)
            {
                int bar = ms / 8 % 4, k = ms % 8; float t = ms * step;
                if (!tz)
                {
                    var c = CHORDS[bar];
                    Tone(b, c[PAT[k]], 0.55f, "triangle", 0.03f, t);
                    if (k == 0) Tone(b, c[0] / 2f, 1.6f, "sine", 0.045f, t);
                }
                else
                {
                    var c = TENSE[bar];
                    Tone(b, c[0] / 2f, 0.22f, "triangle", 0.06f, t);
                    if (k % 2 == 0) Tone(b, c[PAT[k]], 0.3f, "triangle", 0.03f, t);
                    if (k % 4 == 0) Tone(b, 95, 0.17f, "sine", 0.14f, t, 50);
                }
            }
            // 循环接缝：把超出一圈的尾巴叠回开头
            int L = Mathf.RoundToInt(step * 32 * SR);
            var o = new float[L];
            for (int i = 0; i < b.d.Length; i++) o[i % L] += b.d[i];
            var clip = AudioClip.Create(tz ? "tense" : "calm", L, 1, SR, false);
            clip.SetData(o, 0);
            return clip;
        }

        public void Play(string name, float pitch = 1, float vol = 1)
        {
            if (muted || !clips.TryGetValue(name, out var c)) return;
            var s = pool[next++ % pool.Length];
            s.pitch = pitch; s.volume = vol; s.clip = c; s.Play();
        }

        // 英文语音（播报员）：同时只说一句，新的打断旧的
        public void Voice(string key, float vol = 1)
        {
            if (muted || string.IsNullOrEmpty(key)) return;
            if (!voices.TryGetValue(key, out var c)) voices[key] = c = Resources.Load<AudioClip>("Audio/voice/" + key);
            if (!c) return;
            voice.Stop(); voice.clip = c; voice.volume = vol; voice.Play();
        }

        void Update()
        {
            music.mute = musicOff;
            var want = tensT >= 0.5f || stormT >= 0.75f ? tense : calm;
            if (music.clip != want) { music.clip = want; music.Play(); }
            if (layers != null)
            {
                bool on = music.clip == calm && music.isPlaying && !musicOff;   // 紧张音乐的时候不叠（拍子不一样）
                for (int i = 0; i < layers.Length; i++)
                {
                    var a = layers[i];
                    float want2 = on ? Mathf.Clamp01(fever - i) * LAYER_VOL[i] : 0;
                    a.volume = Mathf.MoveTowards(a.volume, want2, Time.unscaledDeltaTime * 1.4f);
                    if (a.volume > 0 && !a.isPlaying) { a.timeSamples = music.timeSamples; a.Play(); }
                    else if (a.volume <= 0 && a.isPlaying) a.Stop();
                    if (a.isPlaying && Mathf.Abs(a.timeSamples - music.timeSamples) > 1500) a.timeSamples = music.timeSamples;   // 跑偏了就对齐回去
                }
            }
        }
    }
}
