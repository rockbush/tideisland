// 队头的人：右手举起来挥（换成没有右手的模型 Art.GuyW，右肩上挂一根和衣服同色的杆 + 圆手），1 秒上下挥 2 下；身上一圈白边 + 身后一团柔和的白光。
// 玩家一眼就知道现在该给谁找船（原来是队头插一面小红旗，用户觉得不直观，去掉了）。
// 手和光挂在关卡的 world 下面，每帧跟着 qGuys[0] 走；队头换人时白边从上一个人身上摘下来挂到新的人身上。
using UnityEngine;

namespace Tide
{
    public partial class Game
    {
        const float ARM_LEN = 0.24f, WAVE_HZ = 2;   // 比模型自己的手（0.15 长）长一截：人在屏幕上就二三十像素，太短挥起来看不见
        Transform headArm, headHalo, headHalo2; Renderer[] headArmR; Material headHaloMat, headHaloMat2;   // 两层光：里面一层亮、外面一层大而淡
        Guy headGuy; GameObject headRim;
        static Texture2D haloTex;
        static Material rimMat;
        static Material RimMat()
        {
            if (rimMat) return rimMat;
            rimMat = new Material(Art.Unlit) { name = "headRim" }; rimMat.SetColor("_BaseColor", Color.white);
            rimMat.SetFloat("_Cull", 1);   // 只画背面：放大的壳子从前面看是空的，只在身子外面露出一圈
            return rimMat;
        }

        // BuildScene 里调：world 刚重建，手和光跟着重建
        void HeadInit()
        {
            headGuy = null; headRim = null;
            headArm = new GameObject("headArm").transform; headArm.SetParent(world, false);
            var stick = GameObject.CreatePrimitive(PrimitiveType.Capsule); Destroy(stick.GetComponent<Collider>());
            stick.transform.SetParent(headArm, false); stick.transform.localPosition = new Vector3(0, ARM_LEN / 2, 0); stick.transform.localScale = new Vector3(0.078f, ARM_LEN / 2, 0.078f);
            var hand = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(hand.GetComponent<Collider>());
            hand.transform.SetParent(headArm, false); hand.transform.localPosition = new Vector3(0, ARM_LEN, 0); hand.transform.localScale = Vector3.one * 0.095f;
            headArmR = new Renderer[] { stick.GetComponent<Renderer>(), hand.GetComponent<Renderer>() };
            hand.GetComponent<Renderer>().sharedMaterial = Art.ColorMat("ffd9b0");   // 手是肤色（调色板 skin），和模型的手一样
            foreach (var r in headArmR) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // 身后的白光：一块永远朝着镜头的方片，贴一张中间亮、往外淡掉的圆
            if (!haloTex)
            {
                haloTex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f;
                        float a = Mathf.Clamp01(1 - d); a = a * a;   // 纯渐变：中间最亮，往外一路淡掉，没有实心的一块
                        haloTex.SetPixel(x, y, new Color(1, 1, 1, a));
                    }
                haloTex.Apply();
            }
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(world, false); headHalo = q.transform;
            headHaloMat = new Material(Art.UnlitT); headHaloMat.SetTexture("_BaseMap", haloTex); headHaloMat.SetColor("_BaseColor", new Color(1, 1, 1, 0.8f));
            var qr = q.GetComponent<MeshRenderer>(); qr.sharedMaterial = headHaloMat; qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // 外面再套一层：更大、更淡，光看起来厚一些（用户：一层不好认）
            var q2 = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q2.GetComponent<Collider>());
            q2.transform.SetParent(world, false); headHalo2 = q2.transform;
            headHaloMat2 = new Material(Art.UnlitT); headHaloMat2.SetTexture("_BaseMap", haloTex); headHaloMat2.SetColor("_BaseColor", new Color(1, 1, 1, 0.5f));
            var qr2 = q2.GetComponent<MeshRenderer>(); qr2.sharedMaterial = headHaloMat2; qr2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            headArm.gameObject.SetActive(false); headHalo.gameObject.SetActive(false); headHalo2.gameObject.SetActive(false);
        }

        // Update 里调（队伍走位之后）
        void HeadTick()
        {
            if (!headArm) return;
            var g = qGuys.Count > 0 ? qGuys[0] : null;
            if (g != headGuy)
            {
                if (headRim) Destroy(headRim);
                if (headGuy != null && headGuy.body) GuyMesh(headGuy, Art.Guy(headGuy.v).mesh);   // 上一个队头把右手还回去
                headRim = null; headGuy = g;
                if (g != null) GuyMesh(g, Art.GuyW(g.v).mesh);
                if (g != null)
                {
                    // 白边：同一个模型放大一点、只画背面、纯白（描边着色器沿法线外扩在棋子模型上会乱，法线不干净）
                    headRim = new GameObject("rim"); headRim.transform.SetParent(g.body, false);
                    headRim.transform.localPosition = new Vector3(0, -0.07f, 0); headRim.transform.localScale = new Vector3(1.38f, 1.2f, 1.38f);   // 人在屏幕上只有二三十像素宽，壳子要放大不少白边才看得见
                    headRim.AddComponent<MeshFilter>().sharedMesh = Art.GuyW(g.v).mesh;
                    var rr = headRim.AddComponent<MeshRenderer>(); rr.sharedMaterial = RimMat();
                    rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            bool on = g != null && g.g && g.g.transform.localScale.x > 0.05f && g.slot == 0;
            if (headArm.gameObject.activeSelf != on) { headArm.gameObject.SetActive(on); headHalo.gameObject.SetActive(on); headHalo2.gameObject.SetActive(on); }
            if (!on) return;
            float s = g.g.transform.localScale.x / GS, t = Fx.Now / 1000f;
            var basePos = g.body.position;     // 身子会一蹦一蹦，跟着身子走
            // 手：从右肩（模型上右手原来的位置）伸出去，往外侧上方 15°~75° 之间上下挥。
            // 队头面朝屏幕，右手在屏幕左边，所以往左上挥（哪边由肩膀在身子哪一侧决定，不写死）
            headArm.position = g.body.TransformPoint(new Vector3(Art.GuyWaveX(g.v) * 0.13f, 0.33f, 0));
            float ang = 45 + 30 * Mathf.Sin(t * Mathf.PI * 2 * WAVE_HZ), side = headArm.position.x < basePos.x ? 1 : -1;
            headArm.rotation = Quaternion.Euler(0, 0, side * ang);
            headArm.localScale = Vector3.one * s;
            var m = Art.TeamMat(COLS[g.c]);
            if (headArmR[0].sharedMaterial != m) headArmR[0].sharedMaterial = m;   // 只有胳膊染队伍色，手是肤色
            // 白光：在人身后一点（离镜头远一点），朝着镜头，轻轻呼吸
            headHalo.position = basePos + Vector3.up * 0.3f * s + cam.transform.forward * 0.35f;
            headHalo.rotation = cam.transform.rotation;
            headHalo.localScale = Vector3.one * (1.7f + 0.15f * Mathf.Sin(t * Mathf.PI * 2)) * s;
            headHaloMat.SetColor("_BaseColor", new Color(1, 1, 1, 0.8f + 0.2f * Mathf.Sin(t * Mathf.PI * 2)));   // 纯白，强度随渐变图从中间往外变淡
            // 外层：比里层大一倍多、再往后一点（不挡里层），呼吸慢半拍
            headHalo2.position = basePos + Vector3.up * 0.3f * s + cam.transform.forward * 0.45f;
            headHalo2.rotation = cam.transform.rotation;
            headHalo2.localScale = Vector3.one * (2.8f + 0.25f * Mathf.Sin(t * Mathf.PI * 2 - 1.2f)) * s;
            headHaloMat2.SetColor("_BaseColor", new Color(1, 1, 1, 0.55f + 0.15f * Mathf.Sin(t * Mathf.PI * 2 - 1.2f)));
        }

        // 换身子的网格（描边是另一份外扩的网格，跟着一起换）
        static void GuyMesh(Guy g, Mesh m)
        {
            g.body.GetComponent<MeshFilter>().sharedMesh = m;
            if (g.ol) { var om = Art.OutlineMesh(m); if (om) g.ol.GetComponent<MeshFilter>().sharedMesh = om; }
        }
    }
}
