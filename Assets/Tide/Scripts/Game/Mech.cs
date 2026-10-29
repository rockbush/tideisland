// 第 15 关起的新机制的画面（规则在 Rules.cs）：
//   海藻：船上缠两三道海藻 + 一个绿色数字牌（还要开走几艘别的船）；数到 0 海藻缩掉
//   钥匙锁链：被锁的船中间一道铁链 + 挂锁，钥匙船头顶飘一把同色的钥匙；钥匙船一开走，钥匙飞过去开锁
//   鲨鱼：沿港湾一边来回游（位置按 st.t 算，和规则一致）；船撞上它会被吓回来
using System.Collections.Generic;
using UnityEngine;
using static Tide.Loc;

namespace Tide
{
    public partial class Game
    {
        static readonly string[] KEY_COL = { "ffc530", "c9d3e3" };      // 第几对钥匙锁：金色 / 银色
        const float SHARK_YAW = 0;                                        // 鲨鱼模型朝向修正（度）

        // 钥匙、挂锁用圆牌（和船坞数字一样，永远朝着屏幕）：3D 钥匙转到侧面只剩一条线，放在黄船上也看不清
        class MechV { public GameObject weed, chain; public Label n, keyL, lockL; public int shown = -1; }
        MechV[] mechV = new MechV[0];
        readonly List<(Transform g, int k)> sharkV = new List<(Transform, int)>();
        int tipWeed, tipLock, tipShark;    // 提示条最多各说两次

        bool SharkPaused => demo || GuideOn || ModalOpen || OnScreen || menuOpen || (ui.ad && ui.ad.gameObject.activeSelf);

        void MechReset()
        {
            mechV = new MechV[L.boats.Count];
            SharkViews();
        }
        void SharkViews()   // 涨潮换下一波时鲨鱼也换（TideWave.cs）
        {
            foreach (var (g0, k0) in sharkV) if (g0) Destroy(g0.gameObject);
            sharkV.Clear();
            for (int k = 0; k < L.sharks.Count; k++)
            {
                var g = Art.Spawn("shark", world, false).transform; g.localScale = Vector3.one * 1.5f * lay.gs;
                sharkV.Add((g, k));
            }
            SharkTick(0);
        }

        // 船的画面建好之后挂上海藻 / 锁链 / 钥匙
        void AddMechView(int b)
        {
            var B = L.boats[b]; var u = boatV[b];
            if (B.weed <= 0 && B.lockBy < 0 && B.key < 0) return;
            var m = mechV[b] = new MechV(); var g = u.g.transform;
            if (B.weed > 0 && Rules.WeedLeft(L, st, b) > 0)
            {
                m.weed = new GameObject("weed"); m.weed.transform.SetParent(g, false);
                float half = (B.len - 0.16f) / 2;
                var at = B.len == 1 ? new[] { 0f } : B.len == 4 ? new[] { -half + 0.45f, 0, half - 0.45f } : new[] { -half + 0.4f, half - 0.4f };
                foreach (float z in at)
                {
                    var w = Art.Spawn("weed_band", m.weed.transform, true).transform;
                    w.localPosition = new Vector3(0, 0, z); w.localRotation = Quaternion.Euler(0, (z * 37) % 10 - 5, 0);
                }
                m.n = NewLabel(g, new Vector3(0, 0.95f, 0), Rules.WeedLeft(L, st, b).ToString(), UIKit.Hex("3dbd58"), 20, 30);
                m.shown = Rules.WeedLeft(L, st, b);
            }
            if (B.lockBy >= 0 && Rules.Chained(L, st, b))
            {
                m.chain = Art.Spawn("chain_band", g, true);
                m.lockL = Badge(g, "lockicon", KEY_COL[L.boats[B.lockBy].key % KEY_COL.Length]);
            }
            if (B.key >= 0 && st.arr[b] == 0) m.keyL = Badge(g, "keyicon", KEY_COL[B.key % KEY_COL.Length]);
        }

        // 船头顶的圆牌：米白底 + 染了色的图标
        Label Badge(Transform g, string icon, string hex)
        {
            var lb = NewLabel(g, new Vector3(0, 0.95f, 0), "", UIKit.Hex(hex), 20, 40);
            var inner = UIKit.Img(lb.root, "in", UIKit.Circle, UIKit.Hex("fff8e6")); inner.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(32, 32));
            var im = DecoImg(lb.root, "card:" + icon, new Vector2(0.5f, 0.5f), Vector2.zero, 28); im.color = Color.Lerp(UIKit.Hex(hex), Color.black, 0.12f);
            return lb;
        }

        // 每一步规则走完：海藻数字、锁链跟着规则状态走
        void MechSync()
        {
            for (int b = 0; b < mechV.Length; b++)
            {
                var m = mechV[b]; if (m == null || boatV[b] == null) continue;
                var u = boatV[b]; bool inLot = st.inl[b] != 0;
                if (m.weed)
                {
                    int left = inLot ? Rules.WeedLeft(L, st, b) : 0;
                    if (left != m.shown && left > 0)
                    {
                        m.shown = left; m.n.text.text = left.ToString();
                        var rt = m.n.root; Fx.Run(() => Fx.Tween(260, p => { if (rt) rt.localScale = Vector3.one * (1 + 0.35f * Mathf.Sin(p * Mathf.PI)); }));
                    }
                    if (left == 0) { var w = m.weed; m.weed = null; RemoveLabel(m.n); m.n = null; if (inLot) WeedOff(u, w); else Destroy(w); }
                }
                if (m.chain && !Rules.Chained(L, st, b))
                {
                    var c = m.chain; var lk = m.lockL; m.chain = null; m.lockL = null;
                    if (!inLot) { Destroy(c); RemoveLabel(lk); }
                    else Fx.Run(() => Unlock(b, c, lk));
                }
                if (m.lockL != null && !inLot) { Destroy(m.chain); m.chain = null; RemoveLabel(m.lockL); m.lockL = null; }   // 被直升机吊走了
            }
            // 钥匙船走了：锁那边的 Unlock 已经把钥匙拿走（飞过去开锁）；没被拿走的（锁着的船早被直升机吊走了）就地消失
            for (int b = 0; b < mechV.Length; b++)
            {
                var m = mechV[b];
                if (m != null && m.keyL != null && st.arr[b] != 0) { var k = m.keyL; m.keyL = null; Fx.Run(() => Poof(k)); }
            }
        }

        // 海藻缩掉、船抖一下
        void WeedOff(BoatV u, GameObject w)
        {
            sfx.Play("reveal"); Burst(u.g.transform.position + Vector3.up * 0.4f, UIKit.Hex("52bb3c"), 12, 1);
            var wt = w.transform;
            Fx.Run(async () => { await Fx.Tween(320, p => { if (wt) wt.localScale = new Vector3(1 + p * 0.3f, Mathf.Max(0.01f, 1 - p), 1 + p * 0.3f); }); if (w) Destroy(w); });
        }

        // 钥匙从钥匙船那儿飞到锁上 → 锁弹开 → 链子掉进水里
        async System.Threading.Tasks.Task Unlock(int b, GameObject chain, Label lockL)
        {
            int t = tok; int kb = L.boats[b].lockBy; var km = kb >= 0 ? mechV[kb] : null;
            Vector3 LockAt() => lockL != null && lockL.anchor ? lockL.anchor.position + lockL.pos : boatV[b].g.transform.position;
            if (km != null && km.keyL != null)
            {
                // 钥匙牌从钥匙船（已经开走了）飞过来，落到锁牌上
                var k = km.keyL; km.keyL = null;
                var p0 = k.follow && k.anchor ? k.anchor.position + k.pos : k.pos; k.follow = false;
                await Fx.Tween(450, p => { var q = Vector3.Lerp(p0, LockAt(), p); q.y += Mathf.Sin(p * Mathf.PI) * 1.4f; k.pos = q; if (k.root) k.root.localScale = Vector3.one * (1 + 0.3f * Mathf.Sin(p * Mathf.PI)); });
                RemoveLabel(k);
            }
            if (t != tok) return;
            sfx.Play("unlock"); Haptics.Play(Haptics.Kind.Light);
            Burst(LockAt(), UIKit.Hex(KEY_COL[0]), 12, 1);
            Transform ct = chain ? chain.transform : null; Vector3 c0 = ct ? ct.localPosition : Vector3.zero;
            await Fx.Tween(380, p =>
            {
                if (lockL != null && lockL.root) lockL.root.localScale = Vector3.one * Mathf.Max(0.01f, 1 + 0.6f * p - 1.6f * p * p);
                if (ct) { ct.localPosition = c0 + Vector3.down * p * 0.5f; ct.localScale = new Vector3(1, Mathf.Max(0.01f, 1 - p), 1); }
            });
            if (chain) Destroy(chain); RemoveLabel(lockL);
        }
        async System.Threading.Tasks.Task Poof(Label k)
        {
            await Fx.Tween(220, p => { if (k.root) k.root.localScale = Vector3.one * (1 - p); });
            RemoveLabel(k);
        }

        // 每帧：鲨鱼游（st.t 只在能玩的时候走，和风暴一样，弹窗、菜单、广告时停）
        void MechTick(float dt)
        {
            if (st != null && st.status == "play" && !SharkPaused) st.t += dt;
            SharkTick(dt);
        }

        void SharkTick(float dt)
        {
            foreach (var (g, k) in sharkV)
            {
                if (!g) continue;
                var S = L.sharks[k]; double t = st != null ? st.t : 0;
                Rules.SharkUV(L, S, t, out double u, out double v); Rules.UVtoXZ(L, u, v, out double x, out double z);
                Rules.SharkUV(L, S, t + 0.05, out double u2, out double v2); Rules.UVtoXZ(L, u2, v2, out double x2, out double z2);
                var pos = G(x, z); var dir = G(x2, z2) - pos;
                g.position = new Vector3(pos.x, Mathf.Sin(Fx.Now / 260) * 0.02f, pos.z);
                if (dir.sqrMagnitude > 1e-6f) g.rotation = Quaternion.Slerp(g.rotation, Quaternion.Euler(0, YawOf(dir) * Mathf.Rad2Deg + SHARK_YAW - 90, 0), Mathf.Min(1, dt * 8 + (dt == 0 ? 1 : 0)));
                if (dt > 0 && Fx.Now % 90 < 17) Wake(g.position - dir.normalized * 0.4f);
            }
        }

        // 鲨鱼把船吓回去：鲨鱼猛地扑一下
        void SharkSnap(int k)
        {
            if (k < 0 || k >= sharkV.Count || !sharkV[k].g) return;
            var g = sharkV[k].g; sfx.Play("bad", 0.7f); Haptics.Play(Haptics.Kind.Medium);
            Splash(g.position, 8, 1.6f);
            Fx.Run(() => Fx.Tween(300, p => { if (g) g.localScale = Vector3.one * 1.5f * lay.gs * (1 + 0.4f * Mathf.Sin(p * Mathf.PI)); }));
        }

        // 点了走不了的船：说清楚为什么
        void MechRefuse(int b, string r)
        {
            sfx.Play("bad"); Haptics.Play(Haptics.Kind.Medium); ShakeBoat(b, Vector3.right);
            if (r == "weed")
            {
                var m = mechV[b]; if (m != null && m.n != null) { var rt = m.n.root; Fx.Run(() => Fx.Tween(300, p => { if (rt) rt.localScale = Vector3.one * (1 + 0.4f * Mathf.Sin(p * Mathf.PI)); })); }
                if (tipWeed++ < 2) Toast(F("被海藻缠住了：再开走 {0} 艘别的船就松开", Rules.WeedLeft(L, st, b)));
            }
            else if (r == "locked")
            {
                int kb = L.boats[b].lockBy;
                // 钥匙牌跳两下：告诉玩家钥匙在哪艘船上
                if (kb >= 0 && mechV[kb] != null && mechV[kb].keyL != null) { var kt = mechV[kb].keyL.root; Fx.Run(() => Fx.Tween(600, p => { if (kt) kt.localScale = Vector3.one * (1 + 0.45f * Mathf.Abs(Mathf.Sin(p * Mathf.PI * 2))); })); }
                if (kb >= 0 && boatV[kb] != null) FlashBoat(boatV[kb]);
                if (tipLock++ < 2) Toast(T("被锁住了：先把挂钥匙的那艘船开走"));
            }
        }
    }
}
