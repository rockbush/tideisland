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
        Transform qDoor; Label qLbl; float qDoorT = -9999, qDoorAng;
        const float ROPE_Y = 0.55f, ROPE_SAG = 0.06f, POST_GAP = 0.85f, HOUSE_HW = 0.78f;   // HOUSE_HW：小屋半宽（make_queue.py 的 HW）

        // BuildScene 里调
        void QueueScene()
        {
            float h = QLG / 2, zA = lay.zQ1, zB = lay.zQ1 - QLG, zC = lay.zQ2;
            float xLw = -lay.xQ - 0.3f, xRw = lay.xT + 0.3f, xHL = lay.hx - HOUSE_HW - 0.06f;
            var ropes = new (Vector2 a, Vector2 b)[] {
                (new Vector2(-lay.xQ + 0.38f, zA + h), new Vector2(xRw, zA + h)),   // 最前面一道；左头空出来是上船口
                (new Vector2(xRw, zA + h), new Vector2(xRw, zB - h)),               // 第 1、2 道的右头
                (new Vector2(xLw, zA - h), new Vector2(lay.xT - 0.32f, zA - h)),     // 1|2 道之间，右头留口拐弯
                (new Vector2(-lay.xQ + 0.32f, zB - h), new Vector2(xHL, zB - h)),    // 2|3 道之间，左头留口拐弯，右头接到小屋
                (new Vector2(xLw, zA + h), new Vector2(xLw, zC - h)),               // 左边
                (new Vector2(xLw, zC - h), new Vector2(xHL, zC - h)) };             // 最里面，接到小屋
            var rope = Art.ColorMat("d8392c");
            var posts = new List<Vector2>();
            foreach (var (a, b) in ropes)
            {
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / POST_GAP));
                for (int i = 0; i <= n; i++)
                {
                    var p = Vector2.Lerp(a, b, i / (float)n);
                    if (!posts.Exists(q => Vector2.Distance(q, p) < 0.1f)) posts.Add(p);
                    if (i == n) break;
                    var q2 = Vector2.Lerp(a, b, (i + 1) / (float)n);
                    Vector3 p0 = W(p.x, p.y, ROPE_Y), p1 = W(q2.x, q2.y, ROPE_Y), mid = (p0 + p1) / 2 + Vector3.down * ROPE_SAG;
                    RopeSeg(p0, mid, rope); RopeSeg(mid, p1, rope);
                }
            }
            foreach (var p in posts) Add("qpost", W(p.x, p.y, 0.24f), 0, false);
            // 小屋 + 侧门（门的原点就是门轴；转 90° 贴在左侧墙上，门轴在靠里那边，往外、往前开）
            var house = Add("qhouse", W(lay.hx, lay.hz, 0.24f), 0, false);
            var d = Art.Spawn("qdoor", house.transform, false);
            d.transform.localPosition = new Vector3(-HOUSE_HW - 0.02f, 0.19f, 0.18f); d.transform.localRotation = Quaternion.Euler(0, 90, 0);
            Art.AddOutline(d, Art.OutlineMat(Art.Ink, 1.3f));
            qDoor = d.transform; qDoorAng = 0; qDoorT = -9999;
            // 屋前的牌子：屋里还剩多少人（顶上原来那块"剩 N 人"去掉了）
            qLbl = NewLabel(null, W(lay.hx - 0.1f, lay.zQ1 - 0.45f * QLG, 0.3f), "", UIKit.Cream, 13, 0, UIKit.Hex("6b4a2a"));
        }

        void RopeSeg(Vector3 a, Vector3 b, Material m)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(c.GetComponent<Collider>());
            c.name = "rope"; c.transform.SetParent(world, false);
            c.transform.position = (a + b) / 2; c.transform.rotation = Quaternion.FromToRotation(Vector3.up, b - a);
            c.transform.localScale = new Vector3(0.045f, Vector3.Distance(a, b) / 2, 0.045f);
            var r = c.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
