// 队头的人：右手举起来挥（换成没有右手的模型 Art.GuyW，肩上挂一根衣服色的胳膊 + 肤色圆手），1 秒上下挥 2 下；身上一圈粗白描边。
// （身后原来还有两层白光，10-28 用户：去掉）
// 玩家一眼就知道现在该给谁找船（原来是队头插一面小红旗，用户觉得不直观，去掉了）。
// 手挂在关卡的 world 下面，每帧跟着 qGuys[0] 走；队头换人时白描边从上一个人身上摘下来挂到新的人身上。
using UnityEngine;

namespace Tide
{
    public partial class Game
    {
        const float ARM_LEN = 0.46f, WAVE_HZ = 2;   // 模型单位（手跟着人一起缩放）：10-28 新模型头很大（半径 0.2），胳膊从肩膀伸出去一大半在头后面，要够长、举得够直，手才高高露在头侧
        Transform headArm; Renderer[] headArmR;
        Guy headGuy; GameObject headRim;
        // 10-28 白边改成描边着色器（全局描边 Art.Outlines 是关的，队头单独挂一层）：原来是"模型放大一圈只画背面"的壳子，
        // 新模型有帽檐 / 飘带，壳子的背面会翻到脸前面，整个头一片白
        static Material headRimMat;
        static Material HeadRimMat()   // 往后推半个身位：只在人身后的背景上露白边（不推的话帽檐、细腿的描边会把脸和身子盖白）
        {
            if (!headRimMat) { headRimMat = new Material(Art.OutlineMat(Color.white, 3.6f)) { name = "headRim" }; headRimMat.SetFloat("_Push", 0.35f); }
            return headRimMat;
        }
        void HeadRimMesh(Guy g) { if (headRim) headRim.GetComponent<MeshFilter>().sharedMesh = Art.OutlineMesh(Art.GuyW(g.v).mesh); }

        // BuildScene 里调：world 刚重建，手和光跟着重建
        void HeadInit()
        {
            headGuy = null; headRim = null;
            headArm = new GameObject("headArm").transform; headArm.SetParent(world, false);
            var sleeve = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(sleeve.GetComponent<Collider>());   // 10-28 新模型：短袖是队伍色、胳膊是肤色
            sleeve.transform.SetParent(headArm, false); sleeve.transform.localPosition = new Vector3(0, 0.02f, 0); sleeve.transform.localScale = Vector3.one * 0.095f;
            var stick = GameObject.CreatePrimitive(PrimitiveType.Capsule); Destroy(stick.GetComponent<Collider>());
            stick.transform.SetParent(headArm, false); stick.transform.localPosition = new Vector3(0, ARM_LEN / 2, 0); stick.transform.localScale = new Vector3(0.06f, ARM_LEN / 2, 0.06f);
            var hand = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(hand.GetComponent<Collider>());
            hand.transform.SetParent(headArm, false); hand.transform.localPosition = new Vector3(0, ARM_LEN, 0); hand.transform.localScale = Vector3.one * 0.1f;
            headArmR = new Renderer[] { sleeve.GetComponent<Renderer>(), stick.GetComponent<Renderer>(), hand.GetComponent<Renderer>() };
            hand.GetComponent<Renderer>().sharedMaterial = Art.ColorMat("ffd9b0");   // 手是肤色（调色板 skin）；胳膊不用肤色：贴在白描边前面，奶白色混在一起就看不见了
            foreach (var r in headArmR) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            headArm.gameObject.SetActive(false);
        }

        // Update 里调（队伍走位之后）
        void HeadTick()
        {
            if (!headArm) return;
            var g = qGuys.Count > 0 && !qGuys[0].vault ? qGuys[0] : null;
            if (g != headGuy)
            {
                if (headRim) Destroy(headRim);
                if (headGuy != null && headGuy.body) GuyMesh(headGuy, Art.Guy(headGuy.v).mesh);   // 上一个队头把右手还回去
                headRim = null; headGuy = g;
                if (g != null)
                {
                    GuyMesh(g, Art.GuyW(g.v).mesh);
                    headRim = new GameObject("rim"); headRim.transform.SetParent(g.body, false);
                    headRim.AddComponent<MeshFilter>(); HeadRimMesh(g);
                    var rr = headRim.AddComponent<MeshRenderer>(); rr.sharedMaterial = HeadRimMat();
                    rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rr.receiveShadows = false;
                }
            }
            bool on = g != null && g.g && g.g.transform.localScale.x > 0.05f && g.slot == 0;
            if (headArm.gameObject.activeSelf != on) headArm.gameObject.SetActive(on);
            if (!on) return;
            float s = g.g.transform.localScale.x / GS, t = Fx.Now / 1000f;
            var basePos = g.body.position;     // 身子会一蹦一蹦，跟着身子走
            // 手：从右肩（模型上右手原来的位置）伸出去，往外侧上方 6°~34°（离竖直）之间左右挥。
            // 队头在第 1 道最左头，往外侧（屏幕左边、靠栏杆的空地）挥，不会插进后面那个人的头里（哪边由肩膀在身子哪一侧决定，不写死）
            headArm.position = g.body.TransformPoint(new Vector3(Art.GuyWaveX(g.v) * 0.1f, 0.33f, 0));   // 新模型肩膀（袖子球心）在 x = ±0.1
            float ang = 20 + 14 * Mathf.Sin(t * Mathf.PI * 2 * WAVE_HZ), side = headArm.position.x < basePos.x ? 1 : -1;
            headArm.rotation = Quaternion.Euler(0, 0, side * ang);
            headArm.localScale = Vector3.one * s * GS;
            var m = Art.TeamMat(COLS[g.c]);
            if (headArmR[0].sharedMaterial != m) headArmR[0].sharedMaterial = headArmR[1].sharedMaterial = m;   // 袖子和胳膊染队伍色，手是肤色
        }

        // 换身子的网格（描边是另一份外扩的网格，跟着一起换）
        static void GuyMesh(Guy g, Mesh m)
        {
            g.body.GetComponent<MeshFilter>().sharedMesh = m;
            if (g.ol) { var om = Art.OutlineMesh(m); if (om) g.ol.GetComponent<MeshFilter>().sharedMesh = om; }
        }
    }
}
