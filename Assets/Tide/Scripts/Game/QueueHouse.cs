// 沙滩排队：景区那种立柱 + 红绳围出来的 3 道来回折的通道，右上角一间小屋，人从屋门一个接一个走出来排进去（2026-10-22 用户草图）。
// 通道的路径在 Game.LayoutLevel 里（lay.path），这里只摆栏杆、小屋、门，管开门动画和屋前"剩 N 人"的牌子。
// 门在小屋左侧墙上，正对最里面那道（第 3 道），人出门就直接进通道。
// 模型：art/blender/make_queue.py → qhouse / qdoor / qpost
using System.Collections.Generic;
using UnityEngine;
using static Tide.Loc;

namespace Tide
{
    public partial class Game
    {
        Transform qDoor, qHouse; Label qLbl; float qDoorT = -9999, qDoorAng;
        const float ROPE_Y = 0.55f * QS, ROPE_SAG = 0.08f * QS, ROPE_R = 0.0675f * QS, POST_GAP = 0.85f * QS, HOUSE_HW = 0.78f;   // HOUSE_HW：小屋半宽（make_queue.py 的 HW，没乘 HSC）；栏杆跟着人放大 QS 倍

        // BuildScene 里调
        void QueueScene()
        {
            float h = QLG / 2, zA = lay.zQ1, zB = lay.zQ1 - QLG, zC = lay.zQ2;
            float xLw = -lay.xQ - 0.3f * QS, xRw = lay.xT + 0.3f * QS, xHL = lay.hx - HOUSE_HW * HSC - 0.06f;
            var ropes = new (Vector2 a, Vector2 b)[] {
                (new Vector2(-lay.xQ + 0.38f * QS, zA + h), new Vector2(xRw, zA + h)),   // 最前面一道；左头空出来是上船口
                (new Vector2(xRw, zA + h), new Vector2(xRw, zB - h)),               // 第 1、2 道的右头
                (new Vector2(xLw, zA - h), new Vector2(lay.xT - 0.32f * QS, zA - h)),     // 1|2 道之间，右头留口拐弯
                (new Vector2(-lay.xQ + 0.32f * QS, zB - h), new Vector2(xHL, zB - h)),    // 2|3 道之间，左头留口拐弯，右头接到小屋
                (new Vector2(xLw, zA + h), new Vector2(xLw, zC - h)),               // 左边
                (new Vector2(xLw, zC - h), new Vector2(xHL, zC - h)) };             // 最里面，接到小屋
            var posts = new List<Vector2>(); var rv = new List<Vector3>(); var rt = new List<int>();
            foreach (var (a, b) in ropes)
            {
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / POST_GAP));
                for (int i = 0; i <= n; i++)
                {
                    var p = Vector2.Lerp(a, b, i / (float)n);
                    if (!posts.Exists(q => Vector2.Distance(q, p) < 0.1f * QS)) posts.Add(p);
                    if (i == n) break;
                    var q2 = Vector2.Lerp(a, b, (i + 1) / (float)n);
                    RopeSpan(rv, rt, W(p.x, p.y, ROPE_Y), W(q2.x, q2.y, ROPE_Y));
                }
            }
            foreach (var p in posts) { Add("qpost", W(p.x, p.y, 0.24f), 0, false, QS); RopeKnot(rv, rt, W(p.x, p.y, ROPE_Y)); }
            RopeMesh(rv, rt);
            // 小屋 + 侧门（门的原点就是门轴；转 90° 贴在左侧墙上，门轴在靠里那边，往外、往前开）
            var house = Add("qhouse", W(lay.hx, lay.hz, 0.24f), 0, false, HSC);
            var d = Art.Spawn("qdoor", house.transform, false);
            d.transform.localPosition = new Vector3(-HOUSE_HW - 0.02f, 0.19f, 0.18f); d.transform.localRotation = Quaternion.Euler(0, 90, 0);
            Art.AddOutline(d, Art.OutlineMat(Art.Ink, 1.3f));
            qDoor = d.transform; qDoorAng = 0; qDoorT = -9999; qHouse = house.transform;
            // 屋前的牌子：屋里还剩多少人（顶上原来那块"剩 N 人"去掉了；10-28 草图：贴在小屋正下方）
            qLbl = NewLabel(null, W(lay.hx - 0.15f, lay.zQ1 - 1.4f * QLG, 0.3f), "", UIKit.Cream, 15, 0, UIKit.Hex("6b4a2a"));
        }

        // 红绳（10-28 用户：粗 3 倍、圆润一点）：原来每一档是两根细圆柱折成 V 字；现在是一整根圆管，顺着抛物线垂下来，
        // 每根立柱上包一个圆球当绳结，拐角也是圆的。所有绳子拼成一个网格，加描边
        const int ROPE_SIDES = 12, ROPE_STEPS = 10;
        void RopeSpan(List<Vector3> v, List<int> t, Vector3 a, Vector3 b)
        {
            int b0 = v.Count; var side = Vector3.Cross(b - a, Vector3.up).normalized;
            for (int i = 0; i <= ROPE_STEPS; i++)
            {
                float u = i / (float)ROPE_STEPS;
                var c = Vector3.Lerp(a, b, u) + Vector3.down * (ROPE_SAG * 4 * u * (1 - u));
                var tan = ((b - a) + Vector3.down * (ROPE_SAG * 4 * (1 - 2 * u))).normalized;
                var up = Vector3.Cross(side, tan).normalized;
                for (int j = 0; j < ROPE_SIDES; j++)
                {
                    float an = j * Mathf.PI * 2 / ROPE_SIDES;
                    v.Add(c + (side * Mathf.Cos(an) + up * Mathf.Sin(an)) * ROPE_R);
                }
            }
            for (int i = 0; i < ROPE_STEPS; i++)
                for (int j = 0; j < ROPE_SIDES; j++)
                {
                    int j2 = (j + 1) % ROPE_SIDES, a0 = b0 + i * ROPE_SIDES, a1 = a0 + ROPE_SIDES;
                    t.Add(a0 + j); t.Add(a1 + j); t.Add(a1 + j2);
                    t.Add(a0 + j); t.Add(a1 + j2); t.Add(a0 + j2);
                }
        }
        void RopeKnot(List<Vector3> v, List<int> t, Vector3 c)   // 立柱顶上的绳结：经纬球，比绳子粗一点点
        {
            const int LA = 6, LO = 12; int b0 = v.Count; float r = ROPE_R * 1.15f;
            for (int i = 0; i <= LA; i++)
            {
                float th = i * Mathf.PI / LA;
                for (int j = 0; j < LO; j++) { float ph = j * Mathf.PI * 2 / LO; v.Add(c + new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)) * r); }
            }
            for (int i = 0; i < LA; i++)
                for (int j = 0; j < LO; j++)
                {
                    int j2 = (j + 1) % LO, a0 = b0 + i * LO, a1 = a0 + LO;
                    t.Add(a0 + j); t.Add(a0 + j2); t.Add(a1 + j2);
                    t.Add(a0 + j); t.Add(a1 + j2); t.Add(a1 + j);
                }
        }
        void RopeMesh(List<Vector3> v, List<int> t)
        {
            var m = new Mesh { name = "ropes", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            var g = new GameObject("ropes"); g.transform.SetParent(world, false);
            g.AddComponent<MeshFilter>().sharedMesh = m;
            var col = UIKit.Hex("d8392c");
            g.AddComponent<MeshRenderer>().sharedMaterial = Art.ColorMat("d8392c");
            Art.AddOutline(g, Art.OutlineMat(Art.OutlineColor(Art.Lit(col)), 1.2f));
        }

        void DoorOpen() => qDoorT = Fx.Now;

        // 出来一个人门就开一下，过 0.35 秒关上；连着出人就一直开着
        void HouseTick(float dt)
        {
            if (!qDoor) return;
            float target = Fx.Now - qDoorT < 350 ? 100 : 0;
            qDoorAng = Mathf.MoveTowards(qDoorAng, target, dt * 700);
            qDoor.localRotation = Quaternion.Euler(0, 90 + qDoorAng, 0);
        }

        void HouseCount()
        {
            if (qLbl == null || st == null) return;
            int n = Mathf.Max(0, st.q.Count - vq - qGuys.Count);
            qLbl.text.text = F("剩 <b><size=20>{0}</size></b> 人", n);
            qLbl.root.sizeDelta = new Vector2(qLbl.text.preferredWidth + 18, 30);
        }
    }
}
