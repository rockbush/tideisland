// 市场爆款三机制（2026-11-06，用户：前面自创的新玩法"放不进主线、玩起来不爽、太烧脑" → 参考 Bus Jam / Color Block Jam 验证过的障碍）。
// 都是开发开关（设置里三个，默认关），开着时第 8 关起每关都摆上；关着时主线完全不变。数值在 Assets/Tide/Resources/newmech.json。
//   火药船（Bomb）：港湾里一艘船背着炸药桶，头上红牌 = 再开走几艘船就炸。把它开走 = 拆弹（风暴 +几秒）；
//     数到 0 它还在港湾里 = 轰！整个港湾炸烂（船全熏黑掀翻、着火冒烟、岸上的人和椰子树都被掀倒）→ 失败，看广告 / 花金币复活 = 港湾修好接着玩
//     （11-06 用户：原来扣 15 秒风暴没感觉 → 改成炸了直接输，跟 Bus Jam 一样拿来接复活广告）。同一时间最多一艘，拆了隔几艘船再来下一艘。
//   封锁海湾（Locked Area）：港湾一侧用彩色浮标绳围起来，里面的船点不动；牌子上写"还差 N 人"，
//     队伍里和牌子同色的人再上船 N 个，浮标一个个炸开、解封。摆的时候拿规则层的机器人试玩几遍，过不去的方案不用（不会做出死局）。
//   时间宝箱（Time Capsule）：一两艘埋得比较深的船上放金宝箱，船开出港湾宝箱就打开，风暴 +10 秒（时间飞到风暴条上）。
// 规则：火药船、封锁海湾在 Rules.cs（State.bomb / sealB…），宝箱只在画面层。
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using static Tide.Loc;

namespace Tide
{
    public partial class Game
    {
        // ---------- 配置：Assets/Tide/Resources/newmech.json ----------
        [System.Serializable] public class NmCfg
        {
            public int[] bombSlack = { 4, 4, 3 };   // 引信 = 至少要开走几艘船才能把它开出来 + 这么多（按难度 普通/困难/超难）
            public int bombGap = 3, bombMax = 3, bombMinBoats = 5, bombMaxDepth = 4;
            public float boomSecs = 15, defuseSecs = 3, chestSecs = 10;
            public int[] chestN = { 1, 1, 2 };
            public float sealMin = 0.15f, sealMax = 0.5f, sealNeed = 0.7f, sealNoise = 25;
            public int sealMinBoats = 8, sealMinNeed = 4, sealTries = 6, sealRuns = 4, sealMs = 700;
        }
        static NmCfg nmCfg;
        static NmCfg NC
        {
            get
            {
                if (nmCfg != null) return nmCfg;
                nmCfg = new NmCfg();
                var ta = Resources.Load<TextAsset>("newmech");
                if (ta) try { JsonUtility.FromJsonOverwrite(ta.text, nmCfg); } catch (System.Exception e) { Debug.LogError("newmech.json 读不出来：" + e.Message); }
                return nmCfg;
            }
        }
        int NmTag => L == null || L.cfg == null ? 0 : Mathf.Clamp(L.cfg.tag, 0, 2);
        bool NmLv => L != null && !mixOn && !specOpen && levelIdx >= BOUNTY_LV;
        bool BombOn => save.devBomb && NmLv;
        bool SealOn => save.devSeal && NmLv;
        bool ChestOn => save.devChest && NmLv;

        // 11-06 实测：3D 炸弹/宝箱放在甲板上会被箭头、数字牌盖住，看不见 → 改成船头顶的圆牌（和海藻、锁一样永远朝着屏幕）
        class BombV { public Label n; public Image disc, spark; public int shown = -1; }
        class ChestV { public Label n; }
        Vector3 BadgeAt(int b) => boatV[b] != null && boatV[b].g ? boatV[b].g.transform.position + new Vector3(0, 1.05f, 0) : Vector3.zero;
        readonly Dictionary<int, BombV> bombV = new Dictionary<int, BombV>();
        readonly Dictionary<int, ChestV> chestV = new Dictionary<int, ChestV>();
        readonly HashSet<int> bombed = new HashSet<int>();   // 炸过的船不再背炸药
        bool bombLose, bombFull, bombCharge; int bombB = -1;   // bombLose：这次输是火药船炸的；bombFull：炸的那一下码头也正好停满了
        readonly List<(Transform o, Transform i, float s)> bombFires = new List<(Transform, Transform, float)>();
        int bombMade, bombWait, nmBoom, nmDefuse, nmChest, nmUnseal, tipBomb, tipSeal; float nmSparkT;
        // 封锁海湾的画面
        GameObject sealG; Label sealL; Material sealMat; readonly List<Transform> sealBuoys = new List<Transform>(), sealRopes = new List<Transform>();
        List<int> sealList = new List<int>(); int sealShown = -1;
        public int NmBoom => nmBoom; public int NmDefuse => nmDefuse; public int NmChest => nmChest; public int NmUnseal => nmUnseal;

        // LoadLevel 里、船的画面都建好以后调
        void NmReset()
        {
            bombV.Clear(); chestV.Clear(); bombed.Clear();   // 画面都挂在船 / World 上，换关时跟着一起删了
            bombLose = bombFull = bombCharge = false; bombB = -1; bombFires.Clear();
            BombHudClear();
            bombMade = 0; bombWait = 0; nmBoom = nmDefuse = nmChest = nmUnseal = 0;
            sealG = null; sealL = null; sealMat = null; sealBuoys.Clear(); sealRopes.Clear(); sealList = new List<int>(); sealShown = -1;
            if (L == null || st == null) return;
            if (SealOn) SealSetup();
            if (ChestOn) ChestSetup();
            NmPlant();
        }

        // ---------- 每帧：引信冒火星、宝箱闪光、牌子数字 ----------
        void NmTick(float dt)
        {
            if (st == null) return;
            nmSparkT -= dt;
            bool spark = nmSparkT <= 0; if (spark) nmSparkT = 0.09f;
            foreach (var kv in bombV)
            {
                var v = kv.Value; if (v.n == null || !v.n.root) continue;
                int left = Rules.BombLeft(st, kv.Key);
                float k = 0.5f + 0.5f * Mathf.Sin(Fx.Now / (left <= 1 ? 70f : left <= 2 ? 110f : 200f));
                if (v.spark) v.spark.rectTransform.localScale = Vector3.one * (0.8f + 0.5f * Random.value);
                if (v.disc) v.disc.color = Color.Lerp(UIKit.Hex("2a2a30"), UIKit.Hex("e0302a"), left <= 2 ? k * (left <= 1 ? 0.9f : 0.55f) : 0);
                v.n.root.localScale = Vector3.one * (1 + (left <= 2 ? 0.14f * k : 0));
                if (spark) { var a = BadgeAt(kv.Key) + new Vector3(0.15f, 0.25f, 0.1f); Spark(a, a + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(0.3f, 0.8f), Random.Range(-0.5f, 0.5f)), UIKit.Hex(Random.value < 0.5f ? "ffd23a" : "ff7a2a"), 0.22f); }
            }
            foreach (var kv in chestV)
            {
                var v = kv.Value; if (v.n == null || !v.n.root) continue;
                v.n.root.localScale = Vector3.one * (1 + 0.06f * Mathf.Sin(Fx.Now / 220f));
                if (Random.value < dt * 3f) { var a = BadgeAt(kv.Key); Spark(a + Random.insideUnitSphere * 0.35f, a + Vector3.up * 0.6f + Random.insideUnitSphere * 0.2f, UIKit.Hex("fff1a8"), 0.45f); }
            }
            BombHudTick();
            if (sealL != null && st.sealB != null && sealShown != Rules.SealLeft(st)) SealNum();
            if (st.sealB != null && !wreck) foreach (int b in sealList) if (SealDim(b)) SetDim(b, SEAL_DIM);
        }

        // RunEvents 里每个事件调一次
        void NmEv(Ev e)
        {
            if (e.t == "go" && e.how != "sea" && chestV.ContainsKey(e.b)) ChestOpen(e.b);
            else if (e.t == "boom")
            {
                nmBoom++; bombed.Add(e.b); bombWait = st.gone + NC.bombGap;
                if (st.status == "win") Fx.Run(() => BoomFx(e.b));   // 最后一下正好过关：小炸一下算了
                else
                {
                    // 输：等这一下的船靠岸、人上完（RunEvents 末尾排的 EndFx），再来那一声大爆炸 → 失败弹窗（BombLoseFx）
                    bombFull = st.status == "lose"; st.status = "lose"; bombLose = true; bombB = e.b; HideHand(); SetCrane(false);
                    if (bombV.TryGetValue(e.b, out var v) && v.n != null) { v.shown = 0; v.n.text.text = "0"; Bump(v.n.root); }
                    if (bombHudT) { bombHudT.text = T("要炸了！"); bombHudN = 0; }
                    sfx.Play("warn", 1.6f, 0.8f);
                    Debug.Log("[火药船] 引信烧完 船" + e.b + " → 整个港湾要炸了（失败）" + (bombFull ? "，码头也停满了" : ""));
                }
            }
            else if (e.t == "defuse") { nmDefuse++; bombWait = st.gone + NC.bombGap; DefuseFx(e.b); }
            else if (e.t == "unseal") { nmUnseal++; if (sealL != null) { sealL.text.text = T("解封！"); sealL.root.sizeDelta = new Vector2(sealL.text.preferredWidth + 20, 31); Bump(sealL.root); } Fx.Run(UnsealFx); }
        }
        // RunEvents 走完：牌子数字跟上规则，该来下一艘火药船就来
        void NmSync()
        {
            foreach (var kv in bombV)
            {
                var v = kv.Value; int left = Rules.BombLeft(st, kv.Key);
                if (v.n != null && left != v.shown && left > 0) { v.shown = left; BombHudNum(left); v.n.text.text = left.ToString(); Bump(v.n.root); if (left <= 2) sfx.Play("warn", 1.3f, 0.35f); }
            }
            NmPlant();
        }

        // 点了点不动的船（封锁海湾里的）
        void SealRefuse(int b)
        {
            sfx.Play("bad"); Haptics.Play(Haptics.Kind.Medium); ShakeBoat(b, Vector3.right);
            if (sealL != null && sealL.root) Bump(sealL.root);
            foreach (var bt in sealBuoys) if (bt) { var t0 = bt; float s0 = t0.localScale.x; Fx.Run(() => Fx.Tween(300, p => { if (t0) t0.localScale = Vector3.one * s0 * (1 + 0.35f * Mathf.Sin(p * Mathf.PI)); })); }
            if (tipSeal++ < 2) Toast(F("封锁海湾：再救 {0} 个和牌子同色的岛民就打开", Rules.SealLeft(st)));
        }

        // ======================================================================
        // 火药船
        void NmPlant()
        {
            if (!BombOn || st.status != "play" || bombV.Count > 0 || bombMade >= NC.bombMax || st.gone < bombWait) return;
            var lot = Rules.InLot(L, st);
            if (lot.Count < NC.bombMinBoats) return;
            var c = new List<(int b, int d, float sc)>();
            var dm = Rules.GetDemand(L, st, 40);
            foreach (int b in lot)
            {
                if (bombed.Contains(b)) continue;
                var B = L.boats[b];
                if (B.sp != 0 || B.key >= 0 || (B.mys != 0 && st.seen[b] == 0) || boatV[b] == null || boatV[b].state != "lot" || Rules.Stuck(L, st, b) || chestV.ContainsKey(b) || stones.ContainsKey(b)) continue;
                int d = Rules.Depth(L, st, b); if (d > NC.bombMaxDepth) continue;
                c.Add((b, d, (d >= 1 && d <= 3 ? 0 : 3) + (dm.first[B.c] < 20 ? 0 : 2) + Random.value * 2));   // 埋得不深不浅、队伍里快要这个颜色的优先
            }
            if (c.Count == 0) return;
            var pick = c.OrderBy(x => x.sc).First();
            int fuse = pick.d + 1 + NC.bombSlack[Mathf.Min(NmTag, NC.bombSlack.Length - 1)];
            Rules.SetBomb(L, st, pick.b, fuse);
            bombMade++;
            Debug.Log("[火药船] 第 " + bombMade + " 艘 船" + pick.b + " 色" + L.boats[pick.b].c + " 埋深" + pick.d + " 引信" + fuse);
            BombMake(pick.b);
            if (tipBomb++ < 1 && !OnScreen) Toast(F("火药船！再开走 {0} 艘船它就炸，炸了就输，快把它开出去", fuse));
        }

        // 黑圆牌 + 白数字 + 右上角一截引信冒火星；快炸了圆牌一闪一闪变红
        void BombMake(int b)
        {
            var u = boatV[b]; var g = u.g.transform;
            var v = new BombV(); bombV[b] = v;
            int left = Rules.BombLeft(st, b);
            v.n = NewLabel(g, new Vector3(0, 1.05f, 0), left.ToString(), UIKit.Hex("2a2a30"), 21, 38, Color.white); v.shown = left;
            v.disc = v.n.root.GetComponentInChildren<Image>();
            var fuse = UIKit.Img(v.n.root, "fuse", UIKit.Round, UIKit.Hex("d9b47a")).rectTransform;
            fuse.anchorMin = fuse.anchorMax = new Vector2(0.5f, 0.5f); fuse.sizeDelta = new Vector2(6, 14); fuse.anchoredPosition = new Vector2(13, 19); fuse.localRotation = Quaternion.Euler(0, 0, -35);
            v.spark = UIKit.Img(v.n.root, "spark", CircleSprite(false), UIKit.Hex("ffd23a"));
            var sr = v.spark.rectTransform; sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.5f); sr.sizeDelta = new Vector2(11, 11); sr.anchoredPosition = new Vector2(18, 26);
            var hl = UIKit.Img(v.n.root, "hl", CircleSprite(false), new Color(1, 1, 1, 0.35f)).rectTransform;
            hl.anchorMin = hl.anchorMax = new Vector2(0.5f, 0.5f); hl.sizeDelta = new Vector2(9, 9); hl.anchoredPosition = new Vector2(-9, 9);
            v.n.text.transform.SetAsLastSibling(); Styled(v.n.text, UIKit.Hex("000000"), 1.4f, 1f);
            foreach (var gr in v.n.root.GetComponentsInChildren<Graphic>()) gr.raycastTarget = false;
            var r0 = v.n.root; r0.localScale = Vector3.one * 0.01f; int t = tok;
            BombAlert(b, left);
            Fx.Run(async () => { await Fx.Sleep(OnScreen ? 0 : 450); if (t != tok) return; sfx.Play("warn", 0.9f, 0.6f); await Fx.Tween(320, p => { if (r0) r0.localScale = Vector3.one * Mathf.Max(0.01f, Fx.BackOut(p)); }); });
        }

        // 拆弹：炸弹牌从船上甩进海里，嗤一声冒烟
        void DefuseFx(int b)
        {
            if (!bombV.TryGetValue(b, out var v)) return;
            bombV.Remove(b); BombHudDone(true);
            var at = BadgeAt(b);
            FloatText(at, T("拆弹成功！")); sfx.Play("help", 1.1f);
            if (stormOn && NC.defuseSecs > 0) StormFly(at, NC.defuseSecs);
            Debug.Log("[火药船] 拆弹 船" + b);
            var lb = v.n; if (lb == null) return;
            if (v.spark) v.spark.gameObject.SetActive(false);
            if (v.disc) v.disc.color = UIKit.Hex("2a2a30");
            var side = Vector3.Cross(DirU(b), Vector3.up).normalized; if (Random.value < 0.5f) side = -side;
            Vector3 p0 = at, p1 = new Vector3(at.x, 0, at.z) + side * 1.3f * lay.gs;
            lb.follow = false; lb.pos = p0;
            Fx.Run(async () =>
            {
                await Fx.Tween(520, p => { var q = Vector3.Lerp(p0, p1, p); q.y += Mathf.Sin(p * Mathf.PI) * 0.9f; lb.pos = q; if (lb.root) lb.root.localRotation = Quaternion.Euler(0, 0, p * 400); });
                Splash(p1, 9, 1.2f); sfx.Play("crack", 0.6f, 0.4f);
                for (int i = 0; i < 4; i++) Smoke(p1 + Vector3.up * 0.1f, UIKit.Hex("e8e8e8"), 0.25f, 900, i * 60);
                await Fx.Tween(200, p => { if (lb.root) lb.root.localScale = Vector3.one * (1 - p); });
                RemoveLabel(lb);
            });
        }

        // 爆炸：憋一下（越涨越大、红白乱闪）→ 轰！白闪、震屏、火球、冲击波、碎片、黑烟，船熏黑弹起来 → 风暴提前
        async Task BoomFx(int b)
        {
            int t = tok;
            if (!bombV.TryGetValue(b, out var v)) return;
            bombV.Remove(b); BombHudDone(false);
            var u = boatV[b];
            Vector3 At() => u != null && u.g ? u.g.transform.position + Vector3.up * 0.55f : Vector3.zero;
            Debug.Log("[火药船] 过关那一下正好炸了 船" + b);
            sfx.Play("warn", 2.1f, 0.8f); Haptics.Play(Haptics.Kind.Medium);
            await Fx.Tween(380, p =>
            {
                if (v.n != null && v.n.root) v.n.root.localScale = Vector3.one * (1 + 0.7f * p * p) * (1 + 0.08f * Mathf.Sin(p * 60));
                if (v.disc) v.disc.color = Mathf.Sin(p * 40) > 0 ? UIKit.Hex("ffffff") : UIKit.Hex("ff2a1a");
            });
            if (t != tok) return;
            var at = At();
            RemoveLabel(v.n);
            // 轰！
            pirFlash = Mathf.Max(pirFlash, 0.7f); shake = Mathf.Max(shake, 0.6f); Haptics.Play(Haptics.Kind.Heavy);
            sfx.Play("crash", 0.8f, 1f); sfx.Play("thunder", 1.7f, 0.7f); sfx.Play("rumble", 0.7f, 0.8f);
            float gs = lay.gs;
            var core = FxPrim(PrimitiveType.Sphere, FxMat(UIKit.Hex("fff6c8")));
            var mid = FxPrim(PrimitiveType.Sphere, FxMat(UIKit.Hex("ffae2a", 0.85f)));
            var outer = FxPrim(PrimitiveType.Sphere, FxMat(UIKit.Hex("ff4a1a", 0.6f)));
            ShockRing(new Vector3(at.x, 0.05f, at.z), UIKit.Hex("ffd36a"), 0); ShockRing(new Vector3(at.x, 0.05f, at.z), UIKit.Hex("ff6a2a"), 110);
            Burst(at, UIKit.Hex("ff8a1a"), 18, 1.7f); Burst(at, UIKit.Hex("ffd23a"), 10, 1.3f); Burst(at, UIKit.Hex("3a3532"), 12, 1.5f);
            Splash(at, 14, 2.2f);
            for (int i = 0; i < 7; i++) Smoke(at + Random.insideUnitSphere * 0.3f * gs, UIKit.Hex(i % 2 == 0 ? "4a4542" : "6e6863"), 0.5f * gs, 1500, 120 + i * 70);
            FloatText(at, T("轰！"));
            // 船：熏黑 + 弹起来落回去，再慢慢恢复颜色（规则上它还在原地，照样能开）
            if (u != null && u.g && u.state == "lot")
            {
                var g = u.g.transform; var h0 = g.position; var hc = u.hullC; var dc = u.deckC; var soot = UIKit.Hex("2b2623");
                Fx.Run(async () =>
                {
                    await Fx.Tween(520, p =>
                    {
                        if (!g || u.state != "lot") return;
                        g.position = new Vector3(g.position.x, Mathf.Sin(p * Mathf.PI) * 0.45f * gs, g.position.z);
                        g.rotation = Quaternion.Euler(Mathf.Sin(p * Mathf.PI * 3) * 8 * (1 - p), u.yaw * Mathf.Rad2Deg, Mathf.Sin(p * Mathf.PI * 2) * 10 * (1 - p));
                        foreach (var m in u.hull) m.SetColor("_BaseColor", Lit(Color.Lerp(hc, soot, 0.85f)));
                        foreach (var m in u.deck) m.SetColor("_BaseColor", Lit(Color.Lerp(dc, soot, 0.75f)));
                    });
                    if (g && u.state == "lot") { g.position = new Vector3(g.position.x, 0, g.position.z); g.rotation = Quaternion.Euler(0, u.yaw * Mathf.Rad2Deg, 0); Splash(g.position, 8, 0.8f); }
                    await Fx.Tween(1400, p =>
                    {
                        if (!g || stones.ContainsKey(b)) return;
                        foreach (var m in u.hull) m.SetColor("_BaseColor", Lit(Color.Lerp(Color.Lerp(hc, soot, 0.85f), hc, p)));
                        foreach (var m in u.deck) m.SetColor("_BaseColor", Lit(Color.Lerp(Color.Lerp(dc, soot, 0.75f), dc, p)));
                    });
                });
            }
            // 火球：白芯 → 橙 → 红，一下涨大再散掉
            await Fx.Tween(560, p =>
            {
                float e = 1 - (1 - p) * (1 - p);
                if (core) { core.position = at; core.localScale = Vector3.one * gs * Mathf.Lerp(0.4f, 1.7f, e) * (1 - p * 0.7f); }
                if (mid) { mid.position = at + Vector3.up * 0.3f * p; mid.localScale = Vector3.one * gs * Mathf.Lerp(0.5f, 2.9f, e); FxAlpha(mid, 0.9f * (1 - p)); }
                if (outer) { outer.position = at + Vector3.up * 0.5f * p; outer.localScale = Vector3.one * gs * Mathf.Lerp(0.6f, 3.9f, e); FxAlpha(outer, 0.65f * (1 - p)); }
                if (p > 0.5f && core) core.gameObject.SetActive(false);
            });
            FxDone(core, mid, outer);
        }

        // ======================================================================
        // 火药船炸了 = 失败：整个港湾炸烂
        //   ① 憋 0.75 秒：炸弹牌越涨越大、红白乱闪，港湾里所有船跟着哆嗦，越震越厉害
        //   ② 轰！全屏白闪、大震，一个盖住半个港湾的火球，三圈冲击波；趁白闪把场景换成炸烂的样子：
        //      船全被掀开、熏黑、翻的翻歪的歪半沉在水里，离得近的几艘着火冒黑烟；岸上排队的人被掀倒，椰子树、伞、小屋东倒西歪；水面一片焦黑，木板木箱漂得到处都是
        //   ③ 别的船一艘接一艘跟着连环小爆（由近到远）
        //   ④ 停 1.7 秒让人看清惨状 → 失败弹窗（看广告 / 金币复活 = 修好港湾接着玩）
        // 摆位置用海啸那套（Surge.cs 的 Keep / floaters / UnWreck）：只改位置朝向，记下原样，复活时一键还原
        async Task BombLoseFx()
        {
            int t = tok, b = bombB;
            bombV.TryGetValue(b, out var v); if (v != null) bombV.Remove(b);
            BombHudDone(false);
            var u = b >= 0 && b < boatV.Length ? boatV[b] : null;
            var at = u != null && u.g ? u.g.transform.position + Vector3.up * 0.55f : Vector3.up * 0.55f;
            float gs = lay.gs;
            bombCharge = true;
            // ① 憋
            sfx.Play("warn", 2.1f, 0.9f); sfx.Play("rumble", 0.6f, 0.8f); Haptics.Play(Haptics.Kind.Medium);
            var lotB = boatV.Where(x => x != null && x.g && x.g.activeSelf && x.state == "lot").ToList();
            var rot0 = lotB.Select(x => x.g.transform.rotation).ToList();
            await Fx.Tween(750, p =>
            {
                if (t != tok) return;
                if (v != null && v.n != null && v.n.root) v.n.root.localScale = Vector3.one * (1 + 1.1f * p * p) * (1 + 0.1f * Mathf.Sin(p * 70));
                if (v != null && v.disc) v.disc.color = Mathf.Sin(p * 50) > 0 ? Color.white : UIKit.Hex("ff2a1a");
                for (int i = 0; i < lotB.Count; i++) if (lotB[i].g) lotB[i].g.transform.rotation = rot0[i] * Quaternion.Euler(Random.Range(-1f, 1f) * 5 * p, 0, Random.Range(-1f, 1f) * 5 * p);
                shake = Mathf.Max(shake, 0.1f + 0.4f * p);
            });
            if (t != tok) return;
            for (int i = 0; i < lotB.Count; i++) if (lotB[i].g) lotB[i].g.transform.rotation = rot0[i];
            if (v != null) RemoveLabel(v.n);
            // ② 轰！
            pirFlash = 1; shake = 1.6f; Haptics.Play(Haptics.Kind.Heavy);
            sfx.Play("crash", 0.7f, 1f); sfx.Play("crash", 0.5f, 0.9f); sfx.Play("thunder", 1.5f, 1f); sfx.Play("rumble", 0.5f, 1f);
            Debug.Log("[火药船] 轰！船" + b + " 炸了，整个港湾炸烂 → 失败");
            BombWreck(at, b);
            var core = FxPrim(PrimitiveType.Sphere, FxMat(UIKit.Hex("fff6c8")));
            var mid = FxPrim(PrimitiveType.Sphere, FxMat(UIKit.Hex("ffae2a", 0.9f)));
            var outer = FxPrim(PrimitiveType.Sphere, FxMat(UIKit.Hex("ff4a1a", 0.7f)));
            var dark = FxPrim(PrimitiveType.Sphere, FxMat(UIKit.Hex("2e2826", 0.0f)));
            BlastRing(at, UIKit.Hex("fff0b0"), 0, 900); BlastRing(at, UIKit.Hex("ffae3a"), 120, 760); BlastRing(at, UIKit.Hex("ff5a2a"), 260, 600);
            Burst(at, UIKit.Hex("ff8a1a"), 30, 3.2f); Burst(at, UIKit.Hex("ffd23a"), 20, 2.6f); Burst(at, UIKit.Hex("3a3532"), 26, 3f);
            Splash(at, 24, 3.4f);
            for (int i = 0; i < 12; i++) Smoke(at + Random.insideUnitSphere * 1.2f * gs, UIKit.Hex(i % 2 == 0 ? "3a3532" : "5e5853"), Random.Range(0.8f, 1.3f) * gs, 2200, 150 + i * 60);
            FloatText(at + Vector3.up * 0.5f, T("轰！！"));
            // ③ 连环小爆：由近到远
            var others = boatV.Where(x => x != null && x.g && x.g.activeSelf && x.b != b).OrderBy(x => (x.g.transform.position - at).sqrMagnitude).ToList();
            for (int i = 0; i < others.Count; i++)
            {
                var o = others[i]; int dl = 160 + Mathf.RoundToInt((o.g.transform.position - at).magnitude / gs * 70) + Random.Range(0, 60); int ii = i;
                Fx.Run(async () =>
                {
                    await Fx.Sleep(dl); if (t != tok || !o.g) return;
                    var p0 = o.g.transform.position + Vector3.up * 0.3f;
                    var f = FxPrim(PrimitiveType.Sphere, FxMat(UIKit.Hex(ii % 2 == 0 ? "ffa52a" : "ff7a1a", 0.85f)));
                    Burst(p0, UIKit.Hex("ff9a2a"), 8, 1.3f); Burst(p0, UIKit.Hex("3a3532"), 5, 1.1f);
                    Smoke(p0, UIKit.Hex("4a4542"), 0.6f * gs, 1600, 80);
                    if (ii % 3 == 0) sfx.Play("crash", Random.Range(1.1f, 1.5f), 0.45f);
                    if (ii % 2 == 0) shake = Mathf.Max(shake, 0.35f);
                    await Fx.Tween(380, p => { if (!f) return; float e = 1 - (1 - p) * (1 - p); f.position = p0 + Vector3.up * 0.3f * p; f.localScale = Vector3.one * gs * Mathf.Lerp(0.3f, 1.5f, e); FxAlpha(f, 0.85f * (1 - p)); });
                    FxDone(f);
                });
            }
            // 大火球：白芯 → 橙 → 红，涨到盖住半个港湾；后面跟一团黑烟球
            await Fx.Tween(900, p =>
            {
                float e = 1 - (1 - p) * (1 - p);
                if (core) { core.position = at; core.localScale = Vector3.one * gs * Mathf.Lerp(0.6f, 3.2f, e) * (1 - p * 0.8f); }
                if (mid) { mid.position = at + Vector3.up * 0.5f * p; mid.localScale = Vector3.one * gs * Mathf.Lerp(0.8f, 5.5f, e); FxAlpha(mid, 0.9f * (1 - p)); }
                if (outer) { outer.position = at + Vector3.up * 0.8f * p; outer.localScale = Vector3.one * gs * Mathf.Lerp(1f, 7.5f, e); FxAlpha(outer, 0.7f * (1 - p)); }
                if (dark) { dark.position = at + Vector3.up * (0.6f + 1.4f * p); dark.localScale = Vector3.one * gs * Mathf.Lerp(1f, 4.5f, e); FxAlpha(dark, 0.55f * Mathf.Sin(p * Mathf.PI)); }
                if (p > 0.45f && core) core.gameObject.SetActive(false);
            });
            FxDone(core, mid, outer, dark);
            if (t != tok) return;
            // ④ 看清惨状
            await Fx.Sleep(1700); if (t != tok) return;
            await LoseFx();
        }

        // 把场景换成炸烂的样子（都记在 wrecked 里，UnWreck 一键还原）
        void BombWreck(Vector3 at, int bb)
        {
            wreck = true; wrecked.Clear(); floaters.Clear(); wreckBob.Clear(); bombFires.Clear();
            ui.labels.gameObject.SetActive(false);   // 船上的人数、各种牌子先藏起来
            wreckRoot = new GameObject("wreck").transform; wreckRoot.SetParent(world, false);
            float gs = lay.gs; var soot = UIKit.Hex("2b2623"); var flat = new Vector3(at.x, 0, at.z);
            // 水面一大片焦黑
            var scM = FxMat(new Color(0.1f, 0.08f, 0.07f, 0.38f)); scM.renderQueue = 3100;
            var sc = Prim(PrimitiveType.Cylinder, wreckRoot, scM); sc.position = flat + Vector3.up * 0.03f; sc.localScale = new Vector3(5.5f * gs, 0.005f, 5.5f * gs);
            var sc2 = Prim(PrimitiveType.Cylinder, wreckRoot, scM); sc2.position = flat + Vector3.up * 0.031f; sc2.localScale = new Vector3(2.8f * gs, 0.005f, 2.8f * gs);
            // 船：被掀开，离得越近飞得越远；熏黑；翻的翻、歪的歪，半沉在水里
            var near = new List<(BoatV u, float d)>();
            foreach (var u in boatV)
            {
                if (u == null || !u.g || !u.g.activeSelf) continue;
                var tr = u.g.transform; Keep(tr); wreckBob.Add((u, u.bob)); u.bob = false;
                if (u.arrows != null) foreach (var ar in u.arrows) if (ar) { Keep(ar.transform); ar.SetActive(false); }
                var d = tr.position - at; d.y = 0; float dist = d.magnitude;
                var dir = dist > 0.05f ? d / dist : new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
                float push = Mathf.Lerp(1.1f, 0.3f, Mathf.Clamp01(dist / (6 * gs))) * gs * Random.Range(0.7f, 1.2f);
                tr.position += dir * push;
                bool flip = u.b == bb || Random.value < 0.3f;
                tr.rotation = Quaternion.Euler(0, tr.eulerAngles.y + Random.Range(-40f, 40f), 0) * (flip ? Quaternion.Euler(Random.Range(-12f, 12f), 0, 180 + Random.Range(-20f, 20f)) : Quaternion.Euler(Random.Range(-20f, 20f), 0, Random.Range(20f, 42f) * (Random.value < 0.5f ? 1 : -1)));
                Sink(tr, flip ? 0.45f : 0.6f);
                if (!stones.ContainsKey(u.b))
                {
                    foreach (var m in u.hull) m.SetColor("_BaseColor", Lit(Color.Lerp(u.hullC, soot, 0.8f)));
                    foreach (var m in u.deck) m.SetColor("_BaseColor", Lit(Color.Lerp(u.deckC, soot, 0.7f)));
                }
                floaters.Add(new Floater { t = tr, p = tr.position, r = tr.rotation, ph = Random.value * 6.28f, amp = 0.02f, spd = Random.Range(0.6f, 1f) });
                near.Add((u, dist));
            }
            // 离得近的几艘着火
            foreach (var (u, _) in near.OrderBy(x => x.d).Take(9))
            {
                var tr = u.g.transform; var rs = tr.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
                var bd = rs[0].bounds; foreach (var r in rs) bd.Encapsulate(r.bounds);
                float s0 = Random.Range(0.8f, 1.15f) * gs;
                var o = Prim(PrimitiveType.Sphere, wreckRoot, FxMat(UIKit.Hex("ff6a1a", 0.75f))); o.position = new Vector3(bd.center.x, bd.max.y, bd.center.z);
                var i = Prim(PrimitiveType.Sphere, o, FxMat(UIKit.Hex("ffd84a", 0.9f))); i.localScale = Vector3.one * 0.55f; i.localPosition = new Vector3(0, -0.1f, 0);
                o.localScale = Vector3.one * s0; bombFires.Add((o, i, s0));
            }
            // 岸上排队的人被掀倒、往外推
            foreach (var g in qGuys)
            {
                if (!g.g) continue; var tr = g.g.transform; Keep(tr);
                var d = tr.position - at; d.y = 0; var dir = d.sqrMagnitude > 0.01f ? d.normalized : Vector3.back;
                tr.position += dir * Random.Range(0.15f, 0.5f) * gs + Vector3.up * 0.04f;
                tr.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(Random.Range(70f, 95f), Random.Range(-30f, 30f), Random.Range(-20f, 20f));
            }
            // 椰子树歪倒、伞翻了、小屋歪了
            foreach (var l in lives)
            {
                if (!l.t) continue; var tr = l.t; string m = tr.name;
                var d = tr.position - at; d.y = 0; var dir = d.sqrMagnitude > 0.01f ? d.normalized : Vector3.back;
                if (m.StartsWith("palm")) { Keep(tr); tr.rotation = Quaternion.AngleAxis(Random.Range(28f, 55f), Vector3.Cross(Vector3.up, dir)) * tr.rotation; }
                else if (m == "umbrella") { Keep(tr); tr.rotation = Quaternion.Euler(Random.Range(70f, 110f), Random.Range(0, 360f), 0); tr.position += dir * 0.3f; }
                else if (m != "bush") { Keep(tr); tr.rotation = Quaternion.AngleAxis(Random.Range(8f, 16f), Vector3.Cross(Vector3.up, dir)) * tr.rotation; }
            }
            // 炸飞的木板、木箱、木桶漂得到处都是
            var plankM = FxMat(UIKit.Hex("4a3426")); var plankM2 = FxMat(UIKit.Hex("6b4a30"));
            for (int k = 0; k < 22; k++)
            {
                var p = flat + new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized * Random.Range(0.6f, 5.5f) * gs;
                var pl = Prim(PrimitiveType.Cube, wreckRoot, k % 3 == 0 ? plankM2 : plankM);
                pl.localScale = new Vector3(Random.Range(0.5f, 0.9f), 0.06f, Random.Range(0.12f, 0.2f)) * gs;
                pl.position = new Vector3(p.x, 0.05f, p.z); pl.rotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0, 360f), Random.Range(-8f, 8f));
                floaters.Add(new Floater { t = pl, p = pl.position, r = pl.rotation, ph = Random.value * 6.28f, amp = 0.02f, spd = Random.Range(0.8f, 1.4f) });
            }
            string[] junk = { "crate", "barrel", "crate", "lifering", "barrel", "crate", "barrel", "crate" };
            foreach (var m in junk)
            {
                var p = flat + new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized * Random.Range(1f, 5f) * gs;
                var g = Add(m, new Vector3(p.x, 0, p.z), Random.Range(0, 360f), false, Random.Range(0.7f, 1f)); g.transform.SetParent(wreckRoot, true);
                g.transform.rotation = Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(0, 360f), Random.Range(-25f, 25f));
                Sink(g.transform, 0.5f);
                floaters.Add(new Floater { t = g.transform, p = g.transform.position, r = g.transform.rotation, ph = Random.value * 6.28f, amp = 0.03f, spd = Random.Range(0.8f, 1.3f) });
            }
            // 着火的地方一直冒火星、黑烟，火苗一跳一跳（输了的弹窗后面也接着烧，复活 / 换关就停）
            int t = tok;
            Fx.Run(async () =>
            {
                while (t == tok && wreck && bombLose)
                {
                    foreach (var (o, _, _) in bombFires) if (o && Random.value < 0.5f) Smoke(o.position + Vector3.up * 0.2f, UIKit.Hex(Random.value < 0.5f ? "3a3532" : "5e5853"), Random.Range(0.4f, 0.7f) * lay.gs, 1800, 0);
                    await Fx.Tween(420, p =>
                    {
                        foreach (var (o, i, s0) in bombFires)
                        {
                            if (!o) continue;
                            o.localScale = new Vector3(1, 1.2f + 0.3f * Mathf.Sin(Fx.Now / 60f + s0 * 40), 1) * s0 * (0.85f + 0.25f * Mathf.PerlinNoise(Fx.Now / 90f, s0 * 10));
                            if (i) i.localScale = Vector3.one * (0.45f + 0.2f * Mathf.PerlinNoise(Fx.Now / 70f, s0 * 7));
                            if (Random.value < 0.08f) Spark(o.position, o.position + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0.5f, 1.1f), Random.Range(-0.4f, 0.4f)), UIKit.Hex(Random.value < 0.5f ? "ffd23a" : "ff7a2a"), 0.4f);
                        }
                    });
                }
            });
        }
        // 让一个东西半沉在港湾水面上：露出水面的高度占它自己高度的 above
        void Sink(Transform t, float above)
        {
            var rs = t.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            t.position += Vector3.up * (0.04f - b.size.y * (1 - above) - b.min.y);
        }
        // 大号冲击波（ShockRing 的放大版）：w = 最后多宽（画布单位）
        void BlastRing(Vector3 at, Color c, int delay, float w)
        {
            int t = tok;
            Fx.Run(async () =>
            {
                if (delay > 0) await Fx.Sleep(delay);
                if (t != tok) return;
                var r = OnCanvas("bombRing"); var im = UIKit.Img(r, "o", CircleSprite(true), c); im.rectTransform.Stretch(); im.raycastTarget = false;
                r.anchoredPosition = ToCanvas(cam.WorldToScreenPoint(at)); pirFx.Add(r.gameObject);
                await Fx.Tween(650, p =>
                {
                    if (!r) return; float e = 1 - (1 - p) * (1 - p);
                    r.sizeDelta = new Vector2(Mathf.Lerp(40, w, e), Mathf.Lerp(22, w * 0.55f, e));
                    im.color = new Color(c.r, c.g, c.b, 0.95f * (1 - p));
                });
                if (r) { pirFx.Remove(r.gameObject); Destroy(r.gameObject); }
            });
        }
        // 复活：白光一闪、一团烟盖住，趁机都摆回去、颜色恢复，船一起蹦一下
        void BombRevive()
        {
            int t = tok;
            Fx.Run(async () =>
            {
                intro = true;
                sfx.Play("reveal"); pirFlash = 0.9f;
                foreach (var (o, _, _) in bombFires) if (o) for (int k = 0; k < 3; k++) Smoke(o.position, UIKit.Hex("f2f2f2"), 0.7f * lay.gs, 900, k * 60);
                await Fx.Sleep(160); if (t != tok) return;
                bombLose = false; bombFires.Clear();
                UnWreck();
                foreach (var u in boatV)
                {
                    if (u == null || !u.g || stones.ContainsKey(u.b)) continue;
                    foreach (var m in u.hull) m.SetColor("_BaseColor", Lit(u.hullC));
                    foreach (var m in u.deck) m.SetColor("_BaseColor", Lit(u.deckC));
                }
                st.status = "play";
                if (bombFull) { bombFull = false; OpenBerth(); }
                sfx.Play("cheer"); jumpT = Fx.Now;
                Banner(T("港湾修好了！"), null);
                Debug.Log("[火药船] 复活：港湾修好了" + (st.berth.Count > 0 ? "，泊位 " + st.berth.Count : ""));
                var hop = boatV.Where(x => x != null && x.g && x.state == "lot").ToList();
                await Fx.Tween(420, p => { foreach (var x in hop) if (x.g) { var q = x.g.transform.position; q.y = Mathf.Sin(p * Mathf.PI) * 0.3f * lay.gs; x.g.transform.position = q; } });
                foreach (var x in hop) if (x.g) { var q = x.g.transform.position; q.y = 0; x.g.transform.position = q; }
                if (t == tok) intro = false;
            });
        }

        // ======================================================================
        // 时间宝箱
        void ChestSetup()
        {
            int n = NC.chestN[Mathf.Min(NmTag, NC.chestN.Length - 1)];
            var c = new List<(int b, float sc)>();
            foreach (int b in Rules.InLot(L, st))
            {
                var B = L.boats[b];
                if (B.sp != 0 || B.key >= 0 || B.weed > 0 || B.lockBy >= 0 || boatV[b] == null) continue;
                int d = Rules.Depth(L, st, b);
                c.Add((b, (d >= 1 && d <= 3 ? 0 : d == 0 ? 4 : 2) + Random.value * 3));
            }
            foreach (var x in c.OrderBy(y => y.sc).Take(n)) ChestMake(x.b);
            Debug.Log("[宝箱] 放了 " + chestV.Count + " 个：" + string.Join(",", chestV.Keys));
        }

        // 金色圆牌 + 宝箱图标，周围冒金光
        void ChestMake(int b)
        {
            var v = new ChestV(); chestV[b] = v;
            v.n = Badge(boatV[b].g.transform, "chest", "ffc72e");
            var tg = UIKit.Rect("tag", v.n.root); tg.anchorMin = tg.anchorMax = new Vector2(0.5f, 0); tg.sizeDelta = new Vector2(46, 18); tg.anchoredPosition = new Vector2(0, -2);
            UIKit.Img(tg, "bg", UIKit.Round, UIKit.Hex("e0302a")).rectTransform.Stretch();
            var tt = UIKit.Txt(tg, "t", "+" + NC.chestSecs.ToString("0") + T("秒"), 12, Color.white, TextAnchor.MiddleCenter); tt.rectTransform.Stretch(); tt.fontStyle = FontStyle.Bold;
            foreach (var gr in v.n.root.GetComponentsInChildren<Graphic>()) gr.raycastTarget = false;
        }

        // 船开出港湾：宝箱牌弹一下炸开金光，时间飞到风暴条上
        void ChestOpen(int b)
        {
            var v = chestV[b]; chestV.Remove(b); nmChest++;
            var at = BadgeAt(b);
            Debug.Log("[宝箱] 打开 船" + b + " +" + NC.chestSecs + " 秒");
            sfx.Play("unlock", 1.2f); sfx.Play("coin"); Haptics.Play(Haptics.Kind.Light);
            Burst(at, UIKit.Hex("ffd84a"), 16, 1.2f); Burst(at, UIKit.Hex("fff6c8"), 8, 0.8f);
            ShockRing(at, UIKit.Hex("ffe27a"), 0);
            if (stormOn && NC.chestSecs > 0) StormFly(at, NC.chestSecs);
            else FloatText(at, T("宝箱！"));
            var lb = v.n; lb.follow = false; lb.pos = at;
            Fx.Run(async () =>
            {
                await Fx.Tween(200, p => { if (lb.root) lb.root.localScale = Vector3.one * (1 + 0.6f * Fx.BackOut(p)); });
                await Fx.Tween(180, p => { if (lb.root) lb.root.localScale = Vector3.one * 1.6f * (1 - p); });
                RemoveLabel(lb);
            });
        }

        // 秒数从 world 飞到风暴条上，到了才加 / 减（secs < 0 = 扣时间，红字）
        void StormFly(Vector3 world, float secs)
        {
            if (!stormRow) return;
            int t = tok; bool add = secs > 0;
            var r = OnCanvas("nmTime"); r.sizeDelta = new Vector2(150, 44);
            var tx = UIKit.Txt(r, "t", (add ? "+" : "") + secs.ToString("0") + T("秒"), 30, UIKit.Hex(add ? "ffe066" : "ff4a3a"), TextAnchor.MiddleCenter);
            Styled(tx, UIKit.Hex(add ? "6b3d0c" : "4a0a06"), 2.4f, 2f); tx.rectTransform.Stretch(); tx.raycastTarget = false;
            var p0 = ToCanvas(cam.WorldToScreenPoint(world));
            var c = new Vector3[4]; stormRow.GetWorldCorners(c);
            var p1 = ToCanvas(new Vector3((c[0].x + c[2].x) / 2, (c[0].y + c[2].y) / 2, 0));
            r.anchoredPosition = p0;
            Fx.Run(async () =>
            {
                await Fx.Tween(260, p => { if (r) { r.anchoredPosition = p0 + new Vector2(0, 40 * p); r.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.3f, Fx.BackOut(p)); } });
                await Fx.Sleep(200);
                var pa = r ? r.anchoredPosition : p0;
                await Fx.Tween(420, p => { if (r) { float e = p * p; r.anchoredPosition = Vector2.Lerp(pa, p1, e) + new Vector2(0, Mathf.Sin(p * Mathf.PI) * 60); r.localScale = Vector3.one * Mathf.Lerp(1.3f, 0.7f, e); } });
                if (r) Destroy(r.gameObject);
                if (t != tok || !stormOn || st.status != "play") return;
                stormLeft = add ? Mathf.Min(stormCap, stormLeft + secs) : Mathf.Max(0.05f, stormLeft + secs);
                UpdateStormBar(); Bump(stormRow);
                sfx.Play(add ? "help" : "bad", add ? 1.3f : 0.7f);
                if (!add) { var fl = stormFill; var c0 = fl ? fl.color : Color.white; Fx.Run(async () => { await Fx.Tween(500, p => { if (fl) fl.color = Color.Lerp(UIKit.Hex("ff3a2a"), c0, p); }); if (fl) fl.color = c0; }); }
            });
        }

        // ======================================================================
        // 封锁海湾：找一条横 / 竖的分界线（不能穿过船），一边的船封起来；拿机器人试玩几遍挑过得去、封得最多的
        void SealSetup()
        {
            var lot = Rules.InLot(L, st);
            if (lot.Count < NC.sealMinBoats) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var cands = new List<(int ax, int k, bool lo, List<int> boats)>();
            for (int ax = 0; ax < 2; ax++)
            {
                int N = ax == 0 ? L.NU : L.NV;
                for (int k = 1; k < N; k++)
                    foreach (bool lo in new[] { true, false })
                    {
                        var inside = new List<int>(); bool cut = false;
                        foreach (int b in lot)
                        {
                            var cs = L.boats[b].cells; int a0 = Coord(cs[0], ax);
                            bool s0 = a0 < k, all = true;
                            foreach (int j in cs) if ((Coord(j, ax) < k) != s0) all = false;
                            if (!all) { cut = true; break; }
                            if (s0 == lo) inside.Add(b);
                        }
                        if (cut) continue;
                        float f = inside.Count / (float)lot.Count;
                        if (f < NC.sealMin || f > NC.sealMax) continue;
                        if (inside.Any(b => L.boats[b].sp != 0 || L.boats[b].h >= 0)) continue;
                        cands.Add((ax, k, lo, inside));
                    }
            }
            if (cands.Count == 0) { Debug.Log("[封锁海湾] 这关找不到分界线（船都横在中间）"); return; }
            var rng = new Rng(levelIdx * 977 + save.Tries(levelIdx) * 31 + 5);
            cands = cands.OrderBy(x => Random.value).Take(NC.sealTries).ToList();
            // 先量一下不封锁时机器人赢几把，封锁以后不能差太多
            // 机器人本身也会输（难关不封都赢不了），所以不比输赢，比"输之前救上船几个人"：封了以后不能比不封少太多，而且得真的解得开
            int Try(State s0, double noise, out bool open)
            {
                var s = s0.Clone(); s.t = -1;
                for (int k = 0; k < 3000 && s.status == "play"; k++)
                {
                    if (L.waves != null && Rules.TideDue(L, s)) { open = s.sealB == null; return open ? s.q.Count : s.qi; }   // 涨潮关只量第一波
                    int b = Rules.BestMoveLook(L, s, rng, noise);
                    if (b < 0) break;
                    Rules.Tap(L, s, b, null);
                }
                open = s.sealB == null;
                return s.status == "win" ? s.q.Count : s.qi;
            }
            int Runs(State s, out int opened)
            {
                int sum = Try(s, 0, out bool o); opened = o ? 1 : 0;
                for (int i = 1; i < NC.sealRuns; i++) { sum += Try(s, NC.sealNoise, out o); if (o) opened++; }
                return sum;
            }
            int base0 = Runs(st, out _);
            var qc = new int[16]; for (int i = st.qi; i < st.q.Count; i++) qc[st.q[i]]++;
            (int ax, int k, bool lo, List<int> boats, int c, int n, int w) best = (0, 0, false, null, -1, 0, -1);
            int tried = 0;
            foreach (var cd in cands)
            {
                if (sw.ElapsedMilliseconds > NC.sealMs) break;
                // 颜色：外面那色的船座位多（够人先上去解封），队伍里人也多
                var cols = Enumerable.Range(0, 16).Where(cc => qc[cc] > 0).Select(cc =>
                {
                    int co = lot.Where(bb => !cd.boats.Contains(bb) && L.boats[bb].c == cc).Sum(bb => L.boats[bb].cap);
                    return (cc, co);
                }).Where(y => y.co >= NC.sealMinNeed).OrderByDescending(y => y.co + Random.value * 4).Take(2).ToList();
                foreach (var (c, capOut) in cols)
                foreach (float need in new[] { NC.sealNeed, NC.sealNeed * 0.65f })
                {
                    if (best.boats != null && best.boats.Count >= cd.boats.Count) break;
                    int n = Mathf.Clamp(Mathf.RoundToInt(capOut * need), NC.sealMinNeed, qc[c]);
                    var s2 = st.Clone(); Rules.SetSeal(L, s2, cd.boats, c, n);
                    int w = Runs(s2, out int opened); tried++;
                    bool ok = w >= base0 * 0.85f && opened >= Mathf.Max(1, NC.sealRuns / 2);
                    if (ok && (best.boats == null || cd.boats.Count + n * 0.1f > best.boats.Count + best.n * 0.1f)) best = (cd.ax, cd.k, cd.lo, cd.boats, c, n, w);
                }
            }
            Debug.Log("[封锁海湾] 候选 " + cands.Count + " 条线，试了 " + tried + " 种，不封时机器人救上 " + base0 / NC.sealRuns + " 人，用时 " + sw.ElapsedMilliseconds + "ms" +
                (best.boats == null ? "，没有过得去的方案" : "，选：封 " + best.boats.Count + "/" + lot.Count + " 艘 色" + best.c + " 要 " + best.n + " 人（机器人救上 " + best.w / NC.sealRuns + " 人）"));
            if (best.boats == null) return;
            Rules.SetSeal(L, st, best.boats, best.c, best.n);
            sealList = best.boats;
            SealBuild(best.ax, best.k, best.lo, best.c);
        }
        int Coord(int cell, int ax) => ax == 0 ? cell % L.NU : cell / L.NU;
        int CellAt(int ax, int a, int o) => ax == 0 ? o * L.NU + a : a * L.NU + o;
        Vector3 UVW(double u, double v, float y) { Rules.UVtoXZ(L, u, v, out double x, out double z); return G(x, z, y); }

        void SealBuild(int ax, int k, bool lo, int c)
        {
            sealG = new GameObject("seal"); sealG.transform.SetParent(world, false);
            var col = UIKit.Hex(COLS[c]); float gs = lay.gs;
            // 封起来那一边的海面盖一层淡淡的颜色
            var tc = Color.Lerp(col, Color.black, 0.15f);
            sealMat = new Material(Art.UnlitT); sealMat.SetColor("_BaseColor", new Color(tc.r, tc.g, tc.b, 0.3f)); sealMat.renderQueue = 3100;   // 港湾水面也是半透明的，不排在它后面会被盖掉
            int No = ax == 0 ? L.NV : L.NU, Na = ax == 0 ? L.NU : L.NV;
            for (int a = 0; a < Na; a++)
            {
                if ((a < k) != lo) continue;
                for (int o = 0; o < No; o++)
                {
                    int j = CellAt(ax, a, o); if (L.ins[j] == 0) continue;
                    Rules.CellXZ(L, j, out double x, out double z);
                    var q = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(q.GetComponent<Collider>()); q.name = "tint";
                    q.transform.SetParent(sealG.transform, false); q.transform.position = G(x, z, 0.012f);
                    q.transform.localScale = new Vector3(gs * 1.0f, 0.004f, gs * 1.0f); q.transform.rotation = Quaternion.Euler(0, L.rot != 0 ? 45 : 0, 0);
                    var mr = q.GetComponent<MeshRenderer>(); mr.sharedMaterial = sealMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            // 分界线：两边都是水面的那几段，浮标 + 绳子
            var os = new List<int>();
            for (int o = 0; o < No; o++) if (L.ins[CellAt(ax, k - 1, o)] != 0 && L.ins[CellAt(ax, k, o)] != 0) os.Add(o);
            var runs = new List<List<int>>();
            foreach (int o in os) { if (runs.Count == 0 || runs[runs.Count - 1].Last() != o - 1) runs.Add(new List<int>()); runs[runs.Count - 1].Add(o); }
            var buoyM = Art.ColorMat(COLS[c]); var white = Art.ColorMat("fff8e6"); var rope = Art.ColorMat("f3e6c8");
            Vector3 mid = Vector3.zero; int midN = 0;
            foreach (var run in runs)
            {
                Vector3? prev = null;
                for (double o = run[0] - 0.5; o <= run.Last() + 0.51; o += 0.5)
                {
                    var p = ax == 0 ? UVW(k - 0.5, o, 0) : UVW(o, k - 0.5, 0);
                    bool big = Mathf.Abs((float)(o - System.Math.Floor(o)) - 0.5f) < 0.01f;   // 整格的角上放大浮标，中间放小的
                    var bt = new GameObject("buoy").transform; bt.SetParent(sealG.transform, false); bt.position = p;
                    var ball = Prim(PrimitiveType.Sphere, bt, buoyM); ball.localScale = new Vector3(1, 0.85f, 1) * (big ? 0.42f : 0.26f) * gs; ball.localPosition = new Vector3(0, 0.1f * gs, 0);
                    if (big) { var band = Prim(PrimitiveType.Cylinder, bt, white); band.localScale = new Vector3(0.43f, 0.04f, 0.43f) * gs; band.localPosition = new Vector3(0, 0.14f * gs, 0); }
                    sealBuoys.Add(bt);
                    if (prev.HasValue)
                    {
                        var rp = Prim(PrimitiveType.Cylinder, sealG.transform, rope);
                        PoleSet(rp, prev.Value + Vector3.up * 0.14f * gs, p + Vector3.up * 0.14f * gs, 0.045f * gs);
                        sealRopes.Add(rp);
                    }
                    prev = p; mid += p; midN++;
                }
            }
            if (midN > 0) mid /= midN;
            // 牌子：分界线中间，"还差 N 人"，底色 = 要救的颜色
            var an = new GameObject("sealSign").transform; an.SetParent(sealG.transform, false); an.position = mid + Vector3.up * 0.5f;
            sealL = NewLabel(an, Vector3.zero, "", col, 17, 0, Color.white);
            Styled(sealL.text, Color.Lerp(col, Color.black, 0.55f), 1.8f, 1.5f);
            SealNum();
            // 开场：浮标一个个冒出来
            int t = tok; var list = sealBuoys.ToList(); var ropes = sealRopes.ToList();
            foreach (var x in list) x.localScale = Vector3.one * 0.01f; foreach (var x in ropes) x.gameObject.SetActive(false);
            Fx.Run(async () =>
            {
                await Fx.Sleep(OnScreen ? 0 : 500);
                for (int i = 0; i < list.Count; i++)
                {
                    if (t != tok) return; var x = list[i];
                    Fx.Run(() => Fx.Tween(260, p => { if (x) x.localScale = Vector3.one * Mathf.Max(0.01f, Fx.BackOut(p)); }));
                    if (i < ropes.Count && ropes[i]) ropes[i].gameObject.SetActive(true);
                    if (i % 2 == 0 && !OnScreen) sfx.Play("pop" + (i / 2) % 5, 1, 0.4f);
                    await Fx.Sleep(40);
                }
                foreach (var x in ropes) if (x) x.gameObject.SetActive(true);
                if (!OnScreen && tipSeal++ < 1) Toast(F("封锁海湾：救够 {0} 个和牌子同色的岛民，浮标绳就会打开", st.sealN));
            });
            Debug.Log("[封锁海湾] 浮标 " + sealBuoys.Count + " 个");
        }

        void SealNum()
        {
            if (sealL == null) return;
            int left = Rules.SealLeft(st); bool changed = sealShown >= 0 && left != sealShown; sealShown = left;
            sealL.text.text = F("还差 {0} 人", left);
            sealL.root.sizeDelta = new Vector2(sealL.text.preferredWidth + 20, 17 + 14);
            if (changed) Bump(sealL.root);
        }

        // 解封：浮标一个接一个炸开、绳子沉下去、颜色褪掉，里面的船跳一下
        async Task UnsealFx()
        {
            int t = tok;
            Debug.Log("[封锁海湾] 解封");
            await Fx.Sleep(250); if (t != tok) return;
            Banner(T("海湾解封！")); sfx.Play("unlock"); Haptics.Play(Haptics.Kind.Medium);
            if (sealL != null) { var lb = sealL; sealL = null; var at = lb.anchor ? lb.anchor.position : Vector3.zero; Burst(at, Color.white, 10, 1); Fx.Run(() => Poof(lb)); }
            foreach (var rp in sealRopes) { var x = rp; if (!x) continue; var p0 = x.position; Fx.Run(() => Fx.Tween(420, p => { if (x) { x.position = p0 + Vector3.down * 0.25f * p; x.localScale = new Vector3(x.localScale.x * (1 - p * 0.1f), x.localScale.y, x.localScale.z * (1 - p * 0.1f)); } })); }
            var mat = sealMat; var c0 = mat ? mat.GetColor("_BaseColor") : Color.clear;
            Fx.Run(() => Fx.Tween(700, p => { if (mat) mat.SetColor("_BaseColor", new Color(c0.r, c0.g, c0.b, c0.a * (1 - p))); }));
            var list = sealBuoys.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                if (t != tok) return; var x = list[i]; if (!x) continue;
                var s0 = x.localScale.x; var p0 = x.position;
                Fx.Run(async () => { await Fx.Tween(160, p => { if (x) x.localScale = Vector3.one * s0 * (1 + 0.6f * p); }); if (x) { Burst(p0 + Vector3.up * 0.1f, UIKit.Hex(COLS[st.sealC >= 0 ? st.sealC : 0]), 4, 0.6f); Splash(p0, 4, 0.6f); x.gameObject.SetActive(false); } });
                if (i % 2 == 0) sfx.Play("pop" + (i / 2) % 5, 1.2f, 0.5f);
                await Fx.Sleep(35);
            }
            foreach (int b in sealList)
            {
                if (b >= boatV.Length || boatV[b] == null || boatV[b].state != "lot") continue;
                var g = boatV[b].g.transform; var u = boatV[b]; int bb = b;
                Fx.Run(() => Fx.Tween(450, p => { if (u.g && u.state == "lot" && !stones.ContainsKey(bb)) SetDim(bb, SEAL_DIM * (1 - p)); }));
                Fx.Run(async () => { await Fx.Tween(380, p => { if (g && u.state == "lot") g.position = new Vector3(g.position.x, Mathf.Sin(p * Mathf.PI) * 0.3f * lay.gs, g.position.z); }); if (g && u.state == "lot") g.position = new Vector3(g.position.x, 0, g.position.z); });
            }
            await Fx.Sleep(500);
            if (sealG) Destroy(sealG); sealG = null; sealBuoys.Clear(); sealRopes.Clear();
        }

        // 封锁里的船压成灰色（还看得出原来的颜色）；解封时慢慢恢复
        const float SEAL_DIM = 0.6f;
        bool SealDim(int b) => b < boatV.Length && boatV[b] != null && boatV[b].g && boatV[b].state == "lot" && Rules.Sealed(st, b);
        void SetDim(int b, float k)
        {
            var u = boatV[b]; var gr = UIKit.Hex("8f959e");
            foreach (var m in u.hull) m.SetColor("_BaseColor", Lit(Color.Lerp(u.hullC, gr, k)));
            foreach (var m in u.deck) m.SetColor("_BaseColor", Lit(Color.Lerp(u.deckC, gr, k * 0.8f)));
        }

        // ---------- 小工具 ----------
        Material FxMat(Color c) { var m = new Material(c.a < 1 ? Art.UnlitT : Art.Unlit); m.SetColor("_BaseColor", c); return m; }
        static void FxAlpha(Transform t, float a)
        {
            var mr = t ? t.GetComponent<MeshRenderer>() : null; if (!mr) return;
            var c = mr.sharedMaterial.GetColor("_BaseColor"); mr.sharedMaterial.SetColor("_BaseColor", new Color(c.r, c.g, c.b, a));
        }
        // 一团烟：往上飘、变大、变淡
        void Smoke(Vector3 at, Color c, float size, int ms, int delay)
        {
            int t = tok;
            Fx.Run(async () =>
            {
                if (delay > 0) await Fx.Sleep(delay);
                if (t != tok) return;
                var s = FxPrim(PrimitiveType.Sphere, FxMat(new Color(c.r, c.g, c.b, 0.7f)));
                var drift = new Vector3(Random.Range(-0.4f, 0.4f), 1.4f, Random.Range(-0.4f, 0.4f));
                await Fx.Tween(ms, p => { if (!s) return; s.position = at + drift * p * (1 + size); s.localScale = Vector3.one * size * (0.6f + 1.6f * p); FxAlpha(s, 0.7f * (1 - p)); });
                FxDone(s);
            });
        }
    }
}
