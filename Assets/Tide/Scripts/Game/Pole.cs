// 功能人：撑杆跳（2026-10-28 用户：功能人各种颜色都会随机出现，先做撑杆跳这一种功能）。规则在 Rules.Vault / MarkSpecial。
// 长相：手里竖着扛一根长杆（杆头挂一面同色小旗），身上一圈金边，整个人明暗交替闪（亮 0.5 秒、暗 0.5 秒）——一眼就知道"这个小人不一样"。
// 他一上船（他自己也是撑杆跳上去的），队伍里同色的人（不管排在哪，还在屋里的从侧门冲出来）一个接一个
// 抱杆助跑 → 杆子插地、人被撑起来荡过去 → 到最高点松手，飞上船；杆子往回倒、变小消失。
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using static Tide.Loc;

namespace Tide
{
    public partial class Game
    {
        const int POLE_LV = 10;   // 第 11 关起（10-28 用户：10 关以后出现，原来是 21 关）有撑杆跳功能人（Rules.MarkSpecial：每关 1~2 个，颜色随机）
        const string POLE_WOOD = "d9a24a";
        int vaultN;   // 这局撑杆跳触发了几次（自动测试看）
        byte QX(int i) => st.qx == null || i >= st.qx.Count ? (byte)0 : st.qx[i];

        static Material goldRimMat;
        static Material GoldRimMat()   // 和队头白边一样的做法（往后推，只在背景上露一圈），金色
        {
            if (!goldRimMat) { goldRimMat = new Material(Art.OutlineMat(UIKit.Hex("ffc72e"), 3.2f)) { name = "goldRim" }; goldRimMat.SetFloat("_Push", 0.35f); }
            return goldRimMat;
        }

        // 换上 / 摘掉功能人的长相（x = 0 就是摘掉）
        void SetSpecial(Guy g, byte x)
        {
            g.x = x;
            if (g.r) g.r.sharedMaterials = Art.GuyMats(g.v, COLS[g.c]); g.hi = false;   // 闪到一半摘掉的，颜色还原
            if (g.pole) Destroy(g.pole); if (g.gold) Destroy(g.gold);
            g.pole = g.gold = null;
            if (x != Rules.POLE || !g.body) return;
            // 杆子竖在招手那只手的另一边（队头招手时不打架），比人高一截；杆头一面同色小旗
            float side = -Art.GuyWaveX(g.v);
            g.pole = new GameObject("pole"); g.pole.transform.SetParent(g.body, false);
            var stick = Prim(PrimitiveType.Cylinder, g.pole.transform, Art.ColorMat(POLE_WOOD));
            stick.localPosition = new Vector3(side * 0.2f, 0.72f, 0.03f); stick.localScale = new Vector3(0.05f, 0.72f, 0.05f);
            // 小旗两片交叉（人侧着站时单片旗正好侧对镜头，看不见），杆顶一个金色圆头
            for (int i = 0; i < 2; i++)
            {
                var flag = Prim(PrimitiveType.Cube, g.pole.transform, Art.TeamMat(COLS[g.c]));
                flag.name = "flag"; flag.localPosition = new Vector3(side * 0.2f, 1.3f, 0.03f); flag.localScale = new Vector3(0.24f, 0.15f, 0.02f);
                flag.localRotation = Quaternion.Euler(0, 45 + i * 90, 0);
            }
            var knob = Prim(PrimitiveType.Sphere, g.pole.transform, Art.ColorMat("ffc72e"));
            knob.localPosition = new Vector3(side * 0.2f, 1.46f, 0.03f); knob.localScale = Vector3.one * 0.1f;
            g.gold = new GameObject("gold"); g.gold.transform.SetParent(g.body, false);
            g.gold.AddComponent<MeshFilter>().sharedMesh = Art.OutlineMesh(Art.Guy(g.v).mesh);
            var r = g.gold.AddComponent<MeshRenderer>(); r.sharedMaterial = GoldRimMat();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        }
        // 10-28 用户：功能人要突出，明暗交替闪烁 0.5 秒。亮的时候整个人往白色提 45%（帽子、衣服都是队伍色，一起亮）
        const float BLINK_MS = 500;
        void BlinkTick(float now)
        {
            bool hi = (int)(now / BLINK_MS) % 2 == 0;
            foreach (var g in qGuys)
            {
                if (g.x == 0 || g.vault || !g.r || g.hi == hi) continue;
                g.hi = hi;
                string hex = hi ? ColorUtility.ToHtmlStringRGB(Color.Lerp(UIKit.Hex(COLS[g.c]), Color.white, 0.45f)).ToLower() : COLS[g.c];
                g.r.sharedMaterials = Art.GuyMats(g.v, hex);
            }
        }
        static Transform Prim(PrimitiveType t, Transform parent, Material m)
        {
            var o = GameObject.CreatePrimitive(t); Destroy(o.GetComponent<Collider>());
            o.transform.SetParent(parent, false);
            var r = o.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return o.transform;
        }
        // 圆柱摆在 a→b 之间（Unity 的圆柱高 2、半径 0.5）
        static void PoleSet(Transform p, Vector3 a, Vector3 b, float r)
        {
            var d = b - a; p.position = (a + b) / 2;
            p.rotation = Quaternion.FromToRotation(Vector3.up, d.sqrMagnitude > 1e-6f ? d : Vector3.up);
            p.localScale = new Vector3(r * 2, d.magnitude / 2, r * 2);
        }

        // 队伍里每个人的颜色、功能跟规则层对齐（集合船、喇叭、撑杆跳把队伍重排以后调）；bump = 换了的人蹦一下
        void SyncGuys(bool bump)
        {
            for (int k = 0; k < qGuys.Count; k++)
            {
                int i = vq + k; if (i >= st.q.Count) break;
                var g = qGuys[k]; bool ch = false;
                if (g.c != st.q[i]) { RecolorGuy(g, st.q[i]); ch = true; }
                if (g.x != QX(i)) { SetSpecial(g, QX(i)); ch = true; }
                if (ch && bump && !g.vault) { var bd = g.body; Fx.Run(() => Fx.Tween(300, p => bd.localPosition = new Vector3(0, Mathf.Sin(p * Mathf.PI) * 0.3f, 0))); }
            }
        }

        // 规则层的 vault 事件（pipeline 里，排在功能人自己那次 board 后面）：等他落座，把被叫到的人挪到队伍最前面、标上 vault，
        // 接下来的 board 事件一个个把他们撑杆跳送上船（BoardFx → VaultJump）
        async Task VaultFx(Ev e)
        {
            int t = tok; var u = boatV[e.b];
            if (u != null && u.dash.Count > 0) await u.dash[u.dash.Count - 1];
            if (t != tok) return;
            vaultN++; if (AutoTest.Wanted) Debug.Log("[Pole] 撑杆跳 船" + e.b + " 叫到 " + e.n + " 人：队头后第 " + string.Join(",", e.list) + " 个（画面上排着 " + qGuys.Count + " 人）");
            sfx.Play("reveal"); Haptics.Play(Haptics.Kind.Medium);
            if (u != null) FloatText(u.g.transform.position + Vector3.up, T("撑杆跳！"));
            var picked = new List<Guy>(); var set = new HashSet<int>(e.list);
            foreach (int r in e.list)
            {
                Guy g;
                if (r < qGuys.Count) g = qGuys[r];
                else
                {
                    // 还在屋里没出来的：从侧门冲出来就跳
                    g = MakeGuy(e.h); g.g.transform.position = W(Inside.x, Inside.y, 0.25f); g.g.transform.localScale = Vector3.one * GS; g.s = PathEnd;
                    DoorOpen();
                }
                g.vault = true; picked.Add(g);
            }
            var rest = new List<Guy>(); for (int k = 0; k < qGuys.Count; k++) if (!set.Contains(k)) rest.Add(qGuys[k]);
            qGuys.Clear(); qGuys.AddRange(picked); qGuys.AddRange(rest);
            for (int k = 0; k < qGuys.Count; k++) qGuys[k].slot = k;
            SyncGuys(false); HouseCount();
        }

        // 一个人撑杆跳上船（功能人自己、被叫到的人都走这个）
        async Task VaultJump(Guy guy, BoatV u, Transform seat, Ev e)
        {
            int t = tok; var gt = guy.g.transform; float sc = gt.localScale.x;
            SetSpecial(guy, 0);
            Vector3 from = gt.position, to = seat ? seat.position : u.g.transform.position;
            var dir = to - from; dir.y = 0; float dist = dir.magnitude; dir = dist > 0.01f ? dir / dist : Vector3.forward;
            float run = Mathf.Min(0.7f, dist * 0.12f);
            Vector3 runEnd = from + dir * run, plant = runEnd + dir * 0.9f; plant.y = 0.05f;   // 杆子插地点：助跑终点前面 0.9
            var pole = Prim(PrimitiveType.Cylinder, world, Art.ColorMat(POLE_WOOD)); float pr = 0.035f * sc;
            var face = Quaternion.LookRotation(dir); guy.yaw = YawOf(dir);
            Vector3 hand = Vector3.up * 0.45f * sc;   // 手的高度（人身高 ≈ 0.9 × 缩放）
            float T0 = Mathf.Clamp(700 + dist * 30, 700, 1100) * BoardSpeed;
            // 三段：0~0.22 抱杆助跑（杆子从平端着慢慢压下去插地）/ 0.22~0.58 绕插地点荡起来（杆子越撑越直）/ 0.58~1 松手飞上船
            Vector3 rel = runEnd + hand; float phi0 = Mathf.Atan2(rel.y - plant.y, 0.9f), r0 = Vector2.Distance(new Vector2(0.9f, rel.y - plant.y), Vector2.zero);
            float r1 = 1.75f * sc, phi1 = 100 * Mathf.Deg2Rad;
            Vector3 Swing(float k) { float ph = Mathf.Lerp(phi0, phi1, k), rr = Mathf.Lerp(r0, r1, Mathf.Sqrt(k)); return plant - dir * Mathf.Cos(ph) * rr + Vector3.up * Mathf.Sin(ph) * rr; }
            Vector3 rel1 = Swing(1) - hand;   // 松手那一刻人的位置
            float arc = 0.5f + dist * 0.04f;
            sfx.Play("go", 1.25f);
            await Fx.Tween(T0, p =>
            {
                if (!gt || !pole) return;
                float pitch;
                if (p < 0.22f)
                {
                    float k = p / 0.22f; var pos = Vector3.Lerp(from, runEnd, k); pos.y = from.y + Mathf.Abs(Mathf.Sin(k * Mathf.PI * 3)) * 0.06f;
                    gt.position = pos; pitch = 8;
                    var top = pos + hand - dir * 0.25f * sc;
                    var tipCarry = pos + hand + dir * 1.6f * sc + Vector3.down * 0.1f;   // 平端着
                    PoleSet(pole, top, Vector3.Lerp(tipCarry, plant, k * k), pr);
                }
                else if (p < 0.58f)
                {
                    float k = (p - 0.22f) / 0.36f; var hp = Swing(k);
                    gt.position = hp - hand; pitch = Mathf.Lerp(-10, -40, k);   // 荡起来：身子往后仰，脚朝前
                    PoleSet(pole, plant, hp + (hp - plant).normalized * 0.12f * sc, pr);
                }
                else
                {
                    float k = (p - 0.58f) / 0.42f;
                    var pos = Vector3.Lerp(rel1, to, k); pos.y += Mathf.Sin(k * Mathf.PI) * arc; gt.position = pos;
                    pitch = Mathf.Lerp(-40, 0, Mathf.Sqrt(k));
                    // 松了手的杆子：往回倒、越来越小
                    float fall = Mathf.Lerp(10, 80, k) * Mathf.Deg2Rad, len = r1 * (1 - k * 0.7f);
                    PoleSet(pole, plant, plant + (Vector3.up * Mathf.Cos(fall) - dir * Mathf.Sin(fall)) * len, pr * (1 - k * 0.6f));
                }
                gt.rotation = face * Quaternion.Euler(pitch, 0, 0);
            });
            if (pole) Destroy(pole.gameObject);
            if (t != tok || !gt) return;
            // 落座（和 BoardFx 里跑上船的结尾一样）
            gt.SetParent(seat ? seat : u.g.transform, true);
            gt.position = to + Vector3.down * 0.05f; gt.rotation = u.g.transform.rotation;
            gt.localScale = Vector3.one; gt.localScale = Vector3.one * (0.62f / Mathf.Max(0.01f, gt.lossyScale.x));
            u.riders.Add(guy); guy.dbg = "done"; guy.vault = false;
            if (e.seat < u.seatM.Count && u.seatM[e.seat]) u.seatM[e.seat].SetActive(false);
            sfx.Play("pop2"); Burst(to + Vector3.up * 0.3f, UIKit.Hex(COLS[guy.c]), 6, 0.6f);
        }
    }
}
