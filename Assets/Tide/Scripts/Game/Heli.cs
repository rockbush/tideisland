// 道具"直升机"的演出（1.25 倍速播，整段约 1.6 秒）：直升机从屏幕外飞来 → 放下吊钩勾住船 → 把船吊出水 → 吊着船飞到泊位前（船在下面荡）
// → 等泊位空出来 → 把船放进泊位 → 收钩飞走。规则层早就把船算进泊位了，这里只管好看。
// 模型：heli_body / heli_rotor / heli_tail（make_heli.py），三个文件同一个坐标，摆在原点就拼好；旋翼的转轴在这里按包围盒算。
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Tide
{
    public partial class Game
    {
        const float H_SPD = 1.25f; // 直升机演出的倍速（用户试过 5、2.5 都嫌快，定 1.25；所有时长都是 1 倍速的毫秒数除以它）
        const float H_ROPE = 0.45f, H_HANG = 0.45f, H_CARRY = 0.55f, H_WINCH = 0.22f;   // 绳长（吊着飞时）、吊钩到船、吊着飞时船的高度、绞盘离机身底

        class Heli
        {
            public Transform g, rotor, tail, rope, hook; public Transform[] sling = new Transform[2];
            public Vector3 tAxis, nose; public float s, yaw, tilt, spin;
            public Vector3 pos, lastPos, vel; public bool hasLast;
            public Vector2 off, offV;          // 吊着的船相对吊钩正下方的偏移（荡来荡去）
        }

        Heli MakeHeli(float s)
        {
            var h = new Heli { s = s };
            h.g = new GameObject("heli").transform; h.g.SetParent(world, false); h.g.localScale = Vector3.one * s;
            Art.Spawn("heli_body", h.g, false);
            h.rotor = Pivot(Art.Spawn("heli_rotor", h.g, false).transform, h.g);
            h.tail = Pivot(Art.Spawn("heli_tail", h.g, false).transform, h.g);
            // 尾旋翼最薄的那个方向就是它的转轴；尾巴在哪边，机头就在反方向
            var ext = BoxOf(h.tail).size;
            h.tAxis = ext.x < ext.z ? Vector3.right : Vector3.forward;
            var tp = h.tail.localPosition; h.nose = new Vector3(-tp.x, 0, -tp.z).normalized;
            var mat = Art.ColorMat("4d5461");
            h.rope = Stick(mat); h.hook = Stick(mat);
            for (int i = 0; i < 2; i++) h.sling[i] = Stick(mat);
            ShowRope(h, false, false);
            return h;
        }

        // 把旋翼包一层，包的那层放在旋翼中心，转它就是绕中心转
        static Transform Pivot(Transform part, Transform root)
        {
            var c = BoxOf(part).center;
            var p = new GameObject(part.name + "_pivot").transform; p.SetParent(root, false);
            p.position = c; part.SetParent(p, true);
            return p;
        }
        static Bounds BoxOf(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }
        Transform Stick(Material m)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(c.GetComponent<Collider>());
            c.transform.SetParent(world, false); c.GetComponent<MeshRenderer>().sharedMaterial = m;
            return c.transform;
        }
        static void Seg(Transform t, Vector3 a, Vector3 b, float r)
        {
            var d = b - a; float l = d.magnitude;
            t.position = (a + b) / 2; t.rotation = l > 1e-4f ? Quaternion.FromToRotation(Vector3.up, d / l) : Quaternion.identity;
            t.localScale = new Vector3(r * 2, l / 2, r * 2);
        }
        static void ShowRope(Heli h, bool rope, bool sling)
        {
            h.rope.gameObject.SetActive(rope); h.hook.gameObject.SetActive(rope);
            foreach (var x in h.sling) x.gameObject.SetActive(sling);
        }
        Vector3 Winch(Heli h) => h.g.TransformPoint(0, H_WINCH, 0);

        // 每一帧：机身朝向/前倾跟着速度走，旋翼转；吊着船时船跟着荡，绳子连上
        // ropeLen：绞盘到吊钩的长度；boat：吊着的船（null = 没勾上）
        void HeliStep(Heli h, Vector3 pos, float ropeLen, BoatV boat, float settle = 1)
        {
            float dt = Mathf.Clamp(Fx.Dt, 0.0005f, 0.05f);
            h.pos = pos;
            if (h.hasLast) h.vel = Vector3.Lerp(h.vel, (pos - h.lastPos) / dt, 0.25f);
            h.lastPos = pos; h.hasLast = true;
            var flat = new Vector3(h.vel.x, 0, h.vel.z); float sp = flat.magnitude;
            if (sp > 0.6f) h.yaw = Fx.LerpAng(h.yaw, YawOf(flat) - YawOf(h.nose), Mathf.Min(1, dt * 6 * H_SPD));
            h.tilt = Mathf.Lerp(h.tilt, Mathf.Clamp(sp * 2.6f, 0, 20), Mathf.Min(1, dt * 7 * H_SPD));
            var face = Quaternion.Euler(0, h.yaw * Mathf.Rad2Deg, 0);
            var fwd = face * h.nose; var axis = Vector3.Cross(Vector3.up, fwd);
            h.g.rotation = Quaternion.AngleAxis(h.tilt, axis) * face;
            h.g.position = pos + Vector3.up * Mathf.Sin(Fx.Now * 0.004f) * 0.03f;
            h.spin += dt * 1500;
            h.rotor.localRotation = Quaternion.AngleAxis(h.spin, Vector3.up);
            h.tail.localRotation = Quaternion.AngleAxis(h.spin * 1.6f, h.tAxis);
            if (Fx.Now % 50 < 17 && pos.y < 2.6f) Downwash(new Vector3(pos.x, 0, pos.z), 2);

            var w = Winch(h); var hk = w + Vector3.down * ropeLen;
            if (boat != null)
            {
                // 船挂在吊钩下面：直升机一加速，船往后荡，再慢慢摆回来
                var acc = new Vector2(h.vel.x, h.vel.z);
                h.offV += (-h.off * 30 * H_SPD * H_SPD - h.offV * 4.5f * H_SPD) * dt - acc * dt * 1.4f * H_SPD;   // 摆动也按倍速
                h.off += h.offV * dt; h.off = Vector2.ClampMagnitude(h.off, 0.5f) * settle;
                var g = boat.g.transform;
                hk += new Vector3(h.off.x, 0, h.off.y) * 0.45f;
                g.position = hk + new Vector3(h.off.x, -H_HANG, h.off.y);
                var sw = new Vector3(h.off.y, 0, -h.off.x);
                g.rotation = Quaternion.AngleAxis(-h.off.magnitude * 28 * settle, sw.sqrMagnitude > 1e-6f ? sw.normalized : Vector3.right) * Quaternion.Euler(0, boat.yaw * Mathf.Rad2Deg, 0);
                var B = L.boats[boat.b]; float half = (B.len - 0.16f) * 0.3f;
                for (int i = 0; i < 2; i++) Seg(h.sling[i], hk, g.TransformPoint(0, 0.32f, i == 0 ? half : -half), 0.02f);
            }
            Seg(h.rope, w, hk, 0.026f);
            h.hook.position = hk; h.hook.rotation = Quaternion.identity; h.hook.localScale = Vector3.one * 0.09f;
        }

        // 旋翼往下吹的风：水面上往外散的一圈白点
        void Downwash(Vector3 p, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float a = Random.value * Mathf.PI * 2; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var m = NewCube(new Color(1, 1, 1, 0.8f), p + d * 0.35f + Vector3.up * 0.08f);
                parts.Add(new Part { m = m, v = d * (1.6f + Random.value) + Vector3.up * 0.15f, life = 0.45f, g = 0 });
            }
        }
        void Splash(Vector3 p, int n, float up)
        {
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2 / n + Random.value * 0.4f; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var m = NewCube(new Color(1, 1, 1, 0.9f), new Vector3(p.x, 0.1f, p.z) + d * 0.4f);
                parts.Add(new Part { m = m, v = d * (1.2f + Random.value * 0.8f) + Vector3.up * up * (0.7f + Random.value * 0.6f), life = 0.6f, g = up > 0 ? 9 : 0 });
            }
        }

        static float SmoothIO(float p) => p * p * (3 - 2 * p);
        static float OutCubic(float p) => 1 - Mathf.Pow(1 - p, 3);

        async Task HeliFx(BoatV u, int k, Task gate)
        {
            int t = tok; var g = u.g.transform; var B = L.boats[u.b];
            float s = Mathf.Clamp(1.15f + 0.08f * (B.len - 2), 1.1f, 1.4f);
            var h = MakeHeli(s);
            float hoverY = H_CARRY + H_HANG + H_ROPE - H_WINCH * s;        // 吊着飞时机身的高度
            Vector3 p0 = g.position, up0 = W(0, lay.zCh);
            float sx = p0.x >= 0 ? 1 : -1, sz = Mathf.Sign(up0.z - p0.z);
            sfx.Play("heli");
            bool alive() => t == tok && h.g && u.g;

            // 1) 从屏幕外飞到船正上方（稍微高一点，最后减速停稳）
            var from = p0 + new Vector3(sx * 6f, 0, sz * 1.5f) + Vector3.up * (hoverY + 1.4f);
            var over = p0 + Vector3.up * (hoverY + 0.25f);
            h.yaw = YawOf(over - from) - YawOf(h.nose);
            await Fx.Tween(480 / H_SPD, p => { if (alive()) HeliStep(h, Vector3.Lerp(from, over, OutCubic(p)), 0, null); });
            if (!alive()) { if (h.g) Destroy(h.g.gameObject); return; }

            // 2) 放钩：吊钩落到船顶上
            ShowRope(h, true, false);
            float down = Winch(h).y - H_HANG - 0.02f;
            await Fx.Tween(170 / H_SPD, p => { if (alive()) HeliStep(h, over, down * OutCubic(p), null); });
            if (!alive()) return;
            ShowRope(h, true, true); sfx.Play("board", 0.8f); Haptics.Play(Haptics.Kind.Light);

            // 3) 吊起来：绳子收短，船出水（溅起水花，往下滴水），船头慢慢转正
            Splash(p0, 10, 2.2f);
            float y0 = u.yaw; var lift = p0 + Vector3.up * hoverY;
            await Fx.Tween(300 / H_SPD, p =>
            {
                if (!alive()) return; float e = SmoothIO(p);
                u.yaw = Fx.LerpAng(y0, 0, e * 0.4f);
                HeliStep(h, Vector3.Lerp(over, lift, e), Mathf.Lerp(down, H_ROPE, e), u, e);
            });
            if (!alive()) return;

            // 4) 吊着船飞到泊位前面的航道上
            var hold = W(BX(k), lay.zCh + 0.2f) + Vector3.up * hoverY; var a0 = lift; float y1 = u.yaw;
            float dur = Mathf.Clamp(Vector3.Distance(a0, hold) * 85 + 260, 420, 820) / H_SPD;
            int drip = 0;
            await Fx.Tween(dur, p =>
            {
                if (!alive()) return; float e = SmoothIO(p);
                u.yaw = Fx.LerpAng(y1, 0, e);
                var c = Vector3.Lerp(a0, hold, e) + Vector3.up * Mathf.Sin(p * Mathf.PI) * 0.35f;
                HeliStep(h, c, H_ROPE, u);
                if (drip++ % 4 == 0 && p < 0.5f)
                {
                    var bp0 = u.g.transform.position;
                    parts.Add(new Part { m = NewCube(new Color(0.85f, 0.95f, 1, 0.9f), bp0 + new Vector3(Random.value - 0.5f, -0.1f, Random.value - 0.5f) * 0.6f), v = Vector3.zero, life = 0.35f, g = 9 });
                }
            });
            if (!alive()) return;

            // 5) 泊位还没空（前一艘没走）：原地悬停等着
            while (!gate.IsCompleted)
            {
                await Fx.Tween(120 / H_SPD, p => { if (alive()) HeliStep(h, hold, H_ROPE, u); });
                if (!alive()) return;
            }

            // 6) 往前飞到泊位上方，把船放下去（绳子放长），船缩到泊位的大小
            var bp = BerthPos(k, B.len); var top = new Vector3(bp.x, hoverY, bp.z); var h0 = h.pos;
            float bs = BSC(B.len), sc0 = g.localScale.x;
            await Fx.Tween(380 / H_SPD, p =>
            {
                if (!alive()) return; float e = SmoothIO(p);
                u.yaw = Fx.LerpAng(u.yaw, 0, 0.3f);
                float rl = Mathf.Lerp(H_ROPE, H_ROPE + H_CARRY, e);
                HeliStep(h, Vector3.Lerp(h0, top, Mathf.Min(1, e * 1.6f)), rl, u, 1 - e);
                g.localScale = Vector3.one * Mathf.Lerp(sc0, bs, e);
            });
            if (!alive()) return;
            Splash(bp, 8, 0);

            // 7) 松钩、收绳、飞走（不用等，船已经到了）
            ShowRope(h, true, false);
            Fx.Run(async () =>
            {
                var w0 = h.pos; float rl0 = H_ROPE + H_CARRY;
                var away = w0 + new Vector3((w0.x >= 0 ? 1 : -1) * 7f, 1.6f, -sz * 1.2f);   // 从近的那边飞出屏幕（往上飞会被顶上的界面挡住）
                await Fx.Tween(220 / H_SPD, p => { if (alive()) HeliStep(h, w0 + Vector3.up * 0.15f * p, rl0 * (1 - p), null); });
                if (alive()) ShowRope(h, false, false);
                await Fx.Tween(650 / H_SPD, p => { if (alive()) HeliStep(h, Vector3.Lerp(w0 + Vector3.up * 0.15f, away, p * p), 0, null); });
                if (h.g) { Destroy(h.g.gameObject); foreach (var x in new[] { h.rope, h.hook, h.sling[0], h.sling[1] }) if (x) Destroy(x.gameObject); }
            });
        }
    }
}
