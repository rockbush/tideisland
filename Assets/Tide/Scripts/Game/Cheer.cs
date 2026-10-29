// 连救 5 人以上全队跳的时候（Special.cs 的 jumpT），背景也跟着热闹起来（2026-10-28 用户：房子、树木也跟着跳动、摆动，有氛围一点）：
// 椰子树、遮阳伞左右摇，草屋、灌木、出人小屋踩着人跳的节拍一蹲一弹（人落地时压扁、人跳到最高时拉长）。平时椰子树也随风轻轻晃。
// 树和房子原来烘在 env_harbor 里一起不能动，10-28 拆出来单独摆（make_env.py 的 LIVE），位置和那边的 deco 表一一对上。
using System.Collections.Generic;
using UnityEngine;

namespace Tide
{
    public partial class Game
    {
        class Live { public Transform t; public int kind; public float ph, yaw, sc; }   // kind：0 摇（树、伞）1 弹（房子）2 弹得更狠（灌木）
        readonly List<Live> lives = new List<Live>();
        const float ENV_G = 7.9f, ENV_YQ1 = 0.95f;   // make_env.py 的 G（草地起点离码头前沿多远）和 yQ1
        // (模型, x, 离码头前沿多远, 离地高度, Blender 里的 rz, 缩放)：照抄 make_env.py 的 deco
        static readonly (string m, float x, float y, float h, float rz, float s)[] LIVE = {
            ("palm_a", -5.6f, ENV_G + 0.5f, 0.3f, 0.2f, 1f), ("palm_b", -3.9f, ENV_G + 1.4f, 0.3f, 1.4f, 1f), ("palm_a", 5.5f, ENV_G + 0.4f, 0.3f, 2.9f, 1.05f),
            ("palm_b", 3.4f, ENV_G + 1.7f, 0.3f, 2.0f, 0.95f), ("palm_a", 0.8f, ENV_G + 2.4f, 0.3f, 4.0f, 1f), ("palm_b", -1.7f, ENV_G + 2.8f, 0.3f, 0.7f, 0.9f),
            ("palm_a", -7.3f, ENV_G + 1.2f, 0.3f, 1.1f, 1.1f), ("palm_b", 7.4f, ENV_G + 1.3f, 0.3f, 0.3f, 1f),
            ("hut", -2.6f, ENV_G + 1.3f, 0.3f, 0.25f, 1.1f), ("hut", -5.6f, ENV_G + 2.4f, 0.3f, -0.2f, 1f),
            ("bush", -0.4f, ENV_G + 0.6f, 0.3f, 0, 1f), ("bush", 4.1f, ENV_G + 0.7f, 0.3f, 1, 0.9f), ("bush", -4.6f, ENV_G + 2.3f, 0.3f, 2, 1.1f),
            ("bush", 5.0f, ENV_G + 2.6f, 0.3f, 3, 1f), ("bush", -6.4f, ENV_G + 0.4f, 0.3f, 4, 0.9f), ("bush", 6.6f, ENV_G + 0.5f, 0.3f, 5, 0.9f),
            ("umbrella", -6.5f, ENV_YQ1 - 0.1f, 0.24f, 0.5f, 1f), ("umbrella", 6.6f, ENV_YQ1 + 0.3f, 0.24f, -0.4f, 1f) };

        // BuildScene 里调（QueueScene 之后：出人小屋也跟着弹）
        void LiveScene()
        {
            lives.Clear();
            foreach (var d in LIVE)
            {
                float yaw = -d.rz * Mathf.Rad2Deg;   // Blender 是右手系，绕竖轴的角度到 Unity 里反过来
                var g = Add(d.m, W(d.x, lay.zP - d.y, d.h), yaw, false, d.s);
                int kind = d.m == "hut" ? 1 : d.m == "bush" ? 2 : 0;
                lives.Add(new Live { t = g.transform, kind = kind, ph = Random.value * 6.28f, yaw = yaw, sc = d.s });
            }
            if (qHouse) lives.Add(new Live { t = qHouse, kind = 1, yaw = qHouse.eulerAngles.y, sc = HSC });
        }

        void LiveTick(float now)
        {
            float e = now - jumpT, env = e < 0 || e > JUMP_MS ? 0 : Mathf.Clamp01(e / 200) * Mathf.Clamp01((JUMP_MS - e) / 300);
            float c = Mathf.Cos(e / JUMP_HOP * Mathf.PI * 2);   // 人落地时 = 1（压扁），跳到最高时 = -1（拉长）
            foreach (var l in lives)
            {
                if (!l.t) continue;
                if (l.kind == 0)
                {
                    // 平时随风晃 1.2°；跳起来时两跳摆一个来回，树 7°、伞 4°（伞的枢轴在杆底，摆多了像要倒）
                    float big = l.t.name == "umbrella" ? 4 : 7;
                    float a = 1.2f * Mathf.Sin(now / 900 + l.ph) + env * big * Mathf.Sin(e / JUMP_HOP * Mathf.PI + l.ph * 0.15f);
                    l.t.rotation = Quaternion.Euler(0, 0, a) * Quaternion.Euler(0, l.yaw, 0);
                }
                else
                {
                    float k = (l.kind == 2 ? 0.12f : 0.07f) * env * c;
                    l.t.localScale = new Vector3(1 + k * 0.5f, 1 - k, 1 + k * 0.5f) * l.sc;
                }
            }
        }
    }
}
