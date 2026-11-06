// 悬赏订单（2026-11-05，三玩法融入主线方案第一步；先做成设置里的开发开关 save.devBounty，关着时主线完全不变）。
// 第 8 关起，顶栏下面左边挂一块悬赏木牌，同一时间只有一张；外圈金环 = 还剩多少时间。
//   完成：盖章 + 金币飞进金币牌 + 风暴 +5 秒，连击不断（runT 续上）；热潮中金币翻倍。
//   超时：木牌翻走，不罚；3 秒后来下一张。一关最多 5 张；涨潮动画、弹窗、还没点第一下时都停表。
// 种类（只出当前局面做得到的）：
//   streak 同色连发：连着派 3 艘同一个颜色的船（中间派了别的颜色就从头数）            第 8 关起
//   timed  限时快送：8 秒内派 2 艘某颜色的船                                         第 8 关起
//   seq    顺序单：按 A → B → C 的颜色顺序派船（派了单子里的其他颜色就从头数，单子外的颜色不管）第 12 关起
//   vip    贵宾：队伍里有人戴上金冠，在时限内让他坐上船                               第 15 关起
//   full   满码头：泊位同时全部停满                                                  第 20 关起
//   noboost 不靠道具：20 秒内派 4 艘船，期间不用道具                                  第 20 关起
// 第 8~20 关新手时间 ×1.5。完成一张记每日任务 kind = "bounty"。
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static Tide.Loc;

namespace Tide
{
    public partial class Game
    {
        public const byte CROWN = 2;   // 贵宾：功能人那一格 qx 里的值（规则层不认它，只是画面上戴金冠）
        const int BOUNTY_LV = 7, BOUNTY_MAX = 5;
        const float BOUNTY_GAP = 3, BOUNTY_FIRST = 4, BOUNTY_STORM = 5;

        class BountyT { public string type; public int c = -1, need, prog, vip = -1; public int[] seq; public float dur, left; public int coins; public bool done; }
        BountyT bty; float bountyWait; int bountyN, bountyDone; string bountyLast;
        RectTransform btyCard, btyRow; Image btyTimer; CanvasGroup btyCg; Text btyTitle;
        public int BountyDoneN => bountyDone;   // 自动测试看

        bool BountyOn => save.devBounty && L != null && levelIdx >= BOUNTY_LV && !mixOn && !specOpen;
        bool BountyPaused => demo || GuideOn || st == null || st.status != "play" || st.taps == 0 || intro || ModalOpen || OnScreen || menuOpen || (ui.ad && ui.ad.gameObject.activeSelf);
        float BountyK => levelIdx < 20 ? 1.5f : 1f;

        void BountyReset()
        {
            bty = null; bountyN = 0; bountyDone = 0; bountyLast = null; bountyWait = BOUNTY_FIRST;
            if (btyCard) { Destroy(btyCard.gameObject); btyCard = null; }
        }

        // ---------- 规则 ----------
        // 港湾里看得见颜色的船（彩虹船、没揭开的问号船不算）按颜色数
        Dictionary<int, int> BountyColors()
        {
            var d = new Dictionary<int, int>();
            foreach (int b in Rules.InLot(L, st))
            {
                var B = L.boats[b];
                if (B.sp == 2 || (B.mys != 0 && st.seen[b] == 0)) continue;
                d[B.c] = d.TryGetValue(B.c, out var n) ? n + 1 : 1;
            }
            return d;
        }

        BountyT BountyPick()
        {
            if (PirOn)   // 海盗关只出海盗专属悬赏：救下 2 艘红圈船
            {
                if (bountyN >= 2) return null;
                var q = new BountyT { type = "pirate", need = 2, dur = 45 * BountyK }; q.left = q.dur; q.coins = Mathf.Min(20, 8 + 2 * (levelIdx / 10)) + 6; return q;
            }
            var cols = BountyColors();
            int lv = levelIdx + 1;
            var opts = new List<BountyT>();
            var three = cols.Where(kv => kv.Value >= 3).Select(kv => kv.Key).ToList();
            if (three.Count > 0) opts.Add(new BountyT { type = "streak", c = three[Random.Range(0, three.Count)], need = 3, dur = 25 });
            var two = cols.Where(kv => kv.Value >= 2).Select(kv => kv.Key).ToList();
            if (two.Count > 0) opts.Add(new BountyT { type = "timed", c = two[Random.Range(0, two.Count)], need = 2, dur = 8 });
            if (lv >= 12 && cols.Count >= 3)
            {
                var s = cols.Keys.OrderBy(_ => Random.value).Take(3).ToArray();
                opts.Add(new BountyT { type = "seq", seq = s, need = 3, dur = 25 });
            }
            if (lv >= 15) { int i = VipPick(); if (i >= 0) opts.Add(new BountyT { type = "vip", vip = i, c = st.q[i], need = 1, dur = 15 }); }
            if (lv >= 20)
            {
                int nb = st.berth.Count, lot = Rules.InLot(L, st).Count;
                if (lot >= nb + 1) opts.Add(new BountyT { type = "full", need = nb, dur = 20 });
                if (lot >= 4) opts.Add(new BountyT { type = "noboost", need = 4, dur = 20 });
            }
            if (opts.Count == 0) return null;
            var fresh = opts.Where(o => o.type != bountyLast).ToList(); if (fresh.Count > 0) opts = fresh;
            var p = opts[Random.Range(0, opts.Count)];
            p.dur *= BountyK; p.left = p.dur;
            p.coins = Mathf.Min(20, 8 + 2 * (levelIdx / 10));
            return p;
        }

        // 贵宾挑谁：队伍里排第 3~6 个、看得见、不是功能人，而且他那个颜色港湾里有能走的船或码头上有空座
        int VipPick()
        {
            var ok = new List<int>();
            int from = Mathf.Max(st.qi, vq) + 2;
            for (int i = from; i < Mathf.Min(from + 4, st.q.Count); i++)
            {
                if (i - vq >= qGuys.Count || QX(i) != 0) continue;
                int c = st.q[i];
                bool has = Rules.InLot(L, st).Any(b => L.boats[b].c == c && L.boats[b].sp != 2 && Rules.CanGo(L, st, b));
                for (int k = 0; k < st.berth.Count && !has; k++) { int b = st.berth[k]; if (b >= 0 && L.boats[b].c == c && st.load[b] < L.boats[b].cap) has = true; }
                if (has) ok.Add(i);
            }
            return ok.Count == 0 ? -1 : ok[Random.Range(0, ok.Count)];
        }

        void SetQx(int i, byte x)
        {
            if (st.qx == null) { if (x == 0) return; st.qx = new List<byte>(new byte[st.q.Count]); }
            st.qx[i] = x;
            SyncGuys(false);
        }

        // RunEvents 里每个事件调一次
        void BountyEv(Ev e)
        {
            if (bty == null || bty.done) return;
            int was = bty.prog;
            if (e.t == "go" && e.how != "sea" && e.how != "pirate")
            {
                int c = L.boats[e.b].c;
                switch (bty.type)
                {
                    case "streak": bty.prog = c == bty.c ? bty.prog + 1 : 0; break;
                    case "timed": if (c == bty.c) bty.prog++; break;
                    case "seq":
                        if (c == bty.seq[bty.prog]) bty.prog++;
                        else if (System.Array.IndexOf(bty.seq, c) >= 0) bty.prog = c == bty.seq[0] ? 1 : 0;
                        break;
                    case "noboost": bty.prog++; break;
                }
            }
            else if (e.t == "board" && bty.type == "vip" && QX(e.qi) == CROWN) bty.prog = 1;
            if (bty.prog != was) BountyDraw(bty.prog > was);
            if (bty.prog >= bty.need) BountyWin();
        }

        void BountyPirSave()   // 海盗红圈船救下了（Pirate.cs）
        {
            if (bty == null || bty.done || bty.type != "pirate") return;
            bty.prog++; BountyDraw(true);
            if (bty.prog >= bty.need) BountyWin();
        }

        void BountyBoost()   // 用了道具（UseItem）：不靠道具单直接失败
        {
            if (bty != null && !bty.done && bty.type == "noboost") BountyLose();
        }

        void BountyTick(float dt)
        {
            if (!BountyOn || st == null) return;
            if (BountyPaused) return;
            if (bty == null)
            {
                if (bountyN >= BOUNTY_MAX) return;
                bountyWait -= dt;
                if (bountyWait > 0) return;
                bty = BountyPick();
                if (bty == null) { bountyWait = 1; return; }
                bountyN++; bountyLast = bty.type;
                if (bty.type == "vip") SetQx(bty.vip, CROWN);
                BountyShow();
                return;
            }
            if (bty.done) return;
            if (bty.type == "full")
            {
                int n = st.berth.Count(b => b >= 0);
                if (n != bty.prog) { bool up = n > bty.prog; bty.prog = n; BountyDraw(up); }
                if (n >= st.berth.Count) { BountyWin(); return; }
            }
            bty.left -= dt;
            if (btyTimer) { float k = Mathf.Clamp01(bty.left / bty.dur); btyTimer.fillAmount = k; btyTimer.color = k > 0.3f ? UIKit.Hex("ffc72e") : Color.Lerp(UIKit.Hex("ff5a3c"), UIKit.Hex("ffc72e"), Mathf.PingPong(Fx.Now / 250f, 1)); }
            if (bty.left <= 0) BountyLose();
        }

        void BountyWin()
        {
            var b = bty; b.done = true; bountyDone++;
            int coins = b.coins * (FeverTier > 0 ? 2 : 1);
            if (stormOn && st.status == "play") { stormLeft = Mathf.Min(stormCap, stormLeft + BOUNTY_STORM); }
            if (run > 0) runT = Fx.Now;   // 连击续上
            TaskAdd("bounty");
            Analytics.Track("bounty", "lv", levelIdx + 1, "type", b.type, "res", "win");
            sfx.Play("coin"); Haptics.Play(Haptics.Kind.Light);
            Debug.Log("[悬赏] 完成 " + b.type + " 金币 " + coins + "（第 " + bountyN + " 张）");
            var card = btyCard; int t = tok;
            Fx.Run(async () =>
            {
                if (!card) return;
                // 盖章
                var stamp = UIKit.Rect("stamp", card); stamp.anchorMin = stamp.anchorMax = stamp.pivot = new Vector2(0.5f, 0.5f);
                stamp.anchoredPosition = new Vector2(18, 0); stamp.sizeDelta = new Vector2(110, 40); stamp.localRotation = Quaternion.Euler(0, 0, 12);
                UIKit.Img(stamp, "bd", UIKit.Round, UIKit.Hex("d5392a", 0.9f)).rectTransform.Stretch();
                UIKit.Img(stamp, "in", UIKit.Round, UIKit.Hex("fff3d6")).rectTransform.Stretch(3, 3, 3, 3);
                var st0 = UIKit.Txt(stamp, "t", T("完成！"), 22, UIKit.Hex("d5392a")); st0.fontStyle = FontStyle.Bold; st0.rectTransform.Stretch();
                await Fx.Tween(220, p => { if (stamp) stamp.localScale = Vector3.one * Mathf.Lerp(2.2f, 1, p * p); });
                if (t != tok || !card) return;
                Bump(card);
                // 金币：几枚从木牌飞进金币牌，最后一枚到了才加钱
                var c = new Vector3[4]; card.GetWorldCorners(c);
                var p0 = ToCanvas(new Vector3((c[0].x + c[2].x) / 2, (c[0].y + c[2].y) / 2, 0));
                int n = Mathf.Min(5, 2 + coins / 6);
                for (int i = 0; i < n; i++)
                {
                    bool last = i == n - 1;
                    FlyCoinFrom(p0 + new Vector2(Random.Range(-20f, 20f), Random.Range(-8f, 8f)), last ? () => { if (t != tok) return; save.coins += coins; save.Persist(); UpdateHUD(); sfx.Play("coin"); } : (System.Action)null);
                    await Fx.Sleep(70);
                }
                if (stormOn) BountyFloat(F("风暴 +{0} 秒", BOUNTY_STORM.ToString("0")));
                await Fx.Sleep(700); if (t != tok) return;
                await BountyHide(card);
                if (t != tok) return;
                if (bty == b) { bty = null; bountyWait = BOUNTY_GAP; }
            });
        }

        void BountyLose()
        {
            var b = bty; b.done = true;
            if (b.type == "vip") for (int i = st.qi; i < st.q.Count; i++) if (QX(i) == CROWN) SetQx(i, 0);   // 时间到了金冠摘掉
            Analytics.Track("bounty", "lv", levelIdx + 1, "type", b.type, "res", "timeout");
            Debug.Log("[悬赏] 超时 " + b.type + " 进度 " + b.prog + "/" + b.need);
            var card = btyCard; int t = tok;
            Fx.Run(async () =>
            {
                await BountyHide(card);
                if (t != tok) return;
                if (bty == b) { bty = null; bountyWait = BOUNTY_GAP; }
            });
        }

        void BountyFloat(string s)
        {
            if (!btyCard) return;
            var c = new Vector3[4]; btyCard.GetWorldCorners(c);
            var r = OnCanvas("bf"); r.sizeDelta = new Vector2(140, 26);
            var tx = UIKit.Txt(r, "t", s, 16, UIKit.Hex("ffe58a"), TextAnchor.MiddleCenter, true); tx.rectTransform.Stretch();
            var p0 = ToCanvas(new Vector3(c[2].x, (c[0].y + c[2].y) / 2, 0)) + new Vector2(66, 0);
            r.anchoredPosition = p0;
            Fx.Run(async () => { await Fx.Tween(1000, p => { if (r) { r.anchoredPosition = p0 + new Vector2(0, 4 + 18 * p); tx.color = new Color(1, 0.9f, 0.54f, 1 - p * p); } }); if (r) Destroy(r.gameObject); });
        }

        // ---------- 界面：顶栏下面左边一块木牌 ----------
        static readonly Vector2 BTY_POS = new Vector2(8, -92), BTY_SIZE = new Vector2(196, 58);

        void BountyShow()
        {
            if (btyCard) Destroy(btyCard.gameObject);
            var H = ui.top.parent;
            btyCard = UIKit.Rect("bounty", H).Place(new Vector2(0, 1), new Vector2(0, 1), BTY_POS, BTY_SIZE);
            btyCg = btyCard.gameObject.AddComponent<CanvasGroup>(); btyCg.blocksRaycasts = false;
            if (Skin.Has("plate_wood")) Skin.Img(btyCard, "plate_wood").rectTransform.Stretch();
            else UIKit.Panel(btyCard, "bg", UIKit.Cream, 4).Stretch();
            // 左边：金色倒计时环 + 中间金币
            var ring = UIKit.Rect("ring", btyCard).Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(30, 0), new Vector2(44, 44));
            UIKit.Img(ring, "bg", CircleSprite(false), UIKit.Hex("6b3d0c", 0.35f)).rectTransform.Stretch();
            btyTimer = UIKit.Img(ring, "timer", CircleSprite(true), UIKit.Hex("ffc72e"));
            btyTimer.type = Image.Type.Filled; btyTimer.fillMethod = Image.FillMethod.Radial360; btyTimer.fillOrigin = (int)Image.Origin360.Top; btyTimer.fillClockwise = false;
            btyTimer.rectTransform.Stretch();
            CoinIcon(ring, new Vector2(22, 0), 24);
            // 右边：第一行任务（文字 + 颜色点），第二行进度 + 奖励
            btyTitle = UIKit.Txt(btyCard, "title", "", 15, Color.white, TextAnchor.MiddleLeft); Styled(btyTitle, UIKit.Hex("4a2a10"), 1.6f, 1.2f);
            btyTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(58, -6), new Vector2(132, 24));
            btyRow = UIKit.Rect("row", btyCard).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(58, 6), new Vector2(132, 22));
            BountyDraw(false);
            if (!save.seen.Contains("bounty")) { save.seen.Add("bounty"); save.Persist(); BountyPulse(3); }
            var card = btyCard;
            Fx.Run(() => Fx.Tween(320, p => { if (!card) return; float e = 1 - (1 - p) * (1 - p); card.localScale = new Vector3(1, Mathf.Lerp(0.05f, 1, e), 1); btyCg.alpha = e; card.anchoredPosition = BTY_POS + new Vector2(0, 14 * (1 - e)); }));
            sfx.Play("click");
        }

        async System.Threading.Tasks.Task BountyHide(RectTransform card)
        {
            if (!card) return;
            var cg = card.GetComponent<CanvasGroup>();
            await Fx.Tween(300, p => { if (!card) return; card.localScale = new Vector3(1, 1 - p, 1); if (cg) cg.alpha = 1 - p; });
            if (card) Destroy(card.gameObject);
            if (btyCard == card) btyCard = null;
        }

        void BountyPulse(int n)
        {
            var card = btyCard;
            Fx.Run(async () => { await Fx.Sleep(400); for (int i = 0; i < n && card; i++) { Bump(card); await Fx.Sleep(600); } });
        }

        Image BtyDot(RectTransform p, float x, string hex, float s = 14)
        {
            var d = UIKit.Img(p, "dot", CircleSprite(false), UIKit.Hex(hex));
            d.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x, 0), new Vector2(s, s));
            var rim = UIKit.Img(d.rectTransform, "rim", CircleSprite(true), UIKit.Hex("3b2a1a", 0.55f)); rim.rectTransform.Stretch(-1, -1, -1, -1);
            return d;
        }

        // 画木牌内容（进度变了就重画）；up = 进度涨了，木牌跳一下
        void BountyDraw(bool up)
        {
            if (!btyCard || bty == null) return;
            var b = bty;
            foreach (Transform c in btyTitle.transform) Destroy(c.gameObject);
            foreach (Transform c in btyRow) Destroy(c.gameObject);
            string title = b.type switch
            {
                "streak" => F("连派 {0} 艘", b.need),
                "timed" => F("快送 {0} 艘", b.need),
                "seq" => T("按顺序派"),
                "vip" => T("送贵宾上船"),
                "full" => T("泊位全停满"),
                "pirate" => F("救下 {0} 艘红圈船", b.need),
                _ => F("不用道具派 {0} 艘", b.need),
            };
            btyTitle.text = title;
            float x = Mathf.Min(btyTitle.preferredWidth, 110) + 5;
            var tr = (RectTransform)btyTitle.transform;
            if (b.type == "streak" || b.type == "timed" || b.type == "vip") BtyDot(tr, x, COLS[b.c]);
            // 第二行：进度
            float px = 0;
            if (b.type == "seq")
            {
                for (int i = 0; i < 3; i++)
                {
                    var d = BtyDot(btyRow, px + (i == b.prog ? -1 : 1), COLS[b.seq[i]], i == b.prog ? 18 : 14);   // 下一个要派的大一圈
                    if (i < b.prog) UIKit.Img(d.rectTransform, "ck", CircleSprite(false), Color.white).rectTransform.Stretch(5, 5, 5, 5);   // 派过的：中间一个白点
                    px += 18;
                    if (i < 2) { var ar = UIKit.Txt(btyRow, "ar", "›", 16, UIKit.Hex("fff3d6"), TextAnchor.MiddleCenter); ar.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(px - 1, 1), new Vector2(10, 22)); px += 9; }
                }
            }
            else if (b.need <= 4 && b.type != "vip")
            {
                for (int i = 0; i < b.need; i++)
                {
                    var pip = UIKit.Img(btyRow, "pip", UIKit.Round, i < b.prog ? UIKit.Hex("5ee06a") : UIKit.Hex("fff3d6", 0.3f));
                    pip.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(px, 0), new Vector2(16, 12)); px += 19;
                }
            }
            else
            {
                var pt = UIKit.Txt(btyRow, "p", b.prog + "/" + b.need, 15, Color.white, TextAnchor.MiddleLeft); Styled(pt, UIKit.Hex("4a2a10"), 1.6f, 1.2f);
                pt.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(50, 22));
            }
            // 奖励：右下角 +N 金币
            var rw = UIKit.Txt(btyRow, "rw", "+" + b.coins, 15, UIKit.Hex("ffd84a"), TextAnchor.MiddleRight); Styled(rw, UIKit.Hex("4a2a10"), 1.6f, 1.2f);
            rw.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(40, 22));
            CoinIcon(btyRow, new Vector2(btyRow.rect.width - 9, 0), 16);
            if (up) { Bump(btyCard); sfx.Play("click", 1.3f); }
        }

        // 贵宾金冠（SetSpecial 里 x == CROWN 时调）：头顶一圈金箍 + 四个尖 + 金边
        void CrownOn(Guy g)
        {
            g.pole = new GameObject("crown"); g.pole.transform.SetParent(g.body, false);
            var gold = Art.ColorMat("ffc72e");
            var band = Prim(PrimitiveType.Cylinder, g.pole.transform, gold);
            band.localPosition = new Vector3(0, CROWN_Y, 0); band.localScale = new Vector3(0.37f, 0.05f, 0.37f);
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2;
                var tip = Prim(PrimitiveType.Cube, g.pole.transform, gold);
                tip.localPosition = new Vector3(Mathf.Cos(a) * 0.15f, CROWN_Y + 0.09f, Mathf.Sin(a) * 0.15f); tip.localScale = new Vector3(0.07f, 0.12f, 0.07f);
                tip.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg, 45);
            }
            var gem = Prim(PrimitiveType.Sphere, g.pole.transform, Art.ColorMat("e8413a"));
            gem.localPosition = new Vector3(0, CROWN_Y + 0.02f, -0.17f); gem.localScale = Vector3.one * 0.07f;
            g.gold = new GameObject("gold"); g.gold.transform.SetParent(g.body, false);
            g.gold.AddComponent<MeshFilter>().sharedMesh = Art.OutlineMesh(Art.Guy(g.v).mesh);
            var r = g.gold.AddComponent<MeshRenderer>(); r.sharedMaterial = GoldRimMat();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        }
        static float CROWN_Y = 0.95f;
    }
}
