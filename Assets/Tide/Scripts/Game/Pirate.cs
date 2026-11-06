// 海盗石化（2026-11-05，三玩法融入主线方案第二步；设置里开发开关 save.devPirate：0 关 / 1 海盗关 / 2 第 8 关起每关都有，关着时主线完全不变）。
// 海盗关 = 30（教学）、40、50…100，海盗湾 52/54/56/58/59/60。间隔、解封时间等都在 Assets/Tide/Resources/pirate.json（越难来得越勤）。
// 一次来访：海盗船（港湾左边）给港湾里一艘"现在能开或再开一艘就能开"的船画红圈，右上角数字 = 你还剩几下。
//   这几下里把红圈船派出去（直升机吊走也算）= 救下；派了别的船、次数用完 = 海盗开一炮，这艘船石化（Rules.Petrify）：
//   变成石头留在原地，照样挡路，点不动。
// 解封：每艘石船头上一个倒计时（很长，按难度 30~40 秒），到了自动解封；顶栏下面「立刻解封」花金币（不够看广告）马上解最快到期的那艘；
//   直升机能把石船直接吊走（吊走就解封）。永远不会无解，只会亏风暴时间、被堵路。
// 11-05 用户改版史：先是"拖走船押在海盗船上、要用时赎回"——用户：拖走反而腾出了路、作用不大 → 改成石化。
// 还没做：热潮开炮解封、复活时全解封、地图海盗旗、海盗船正式模型（现在是方块拼的）。
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
        // ---------- 配置：Assets/Tide/Resources/pirate.json ----------
        [System.Serializable] public class PirLv { public int lv; public float every, stone; }
        [System.Serializable] public class PirCfg
        {
            public float[] every = { 12, 9, 7 }, stone = { 30, 35, 40 };
            public float perIsland = 0.5f, minEvery = 6, first = 6, stoneLate = 45, tutorialEvery = 14, tutorialStone = 15;
            public int taps = 2, lateFrom = 91, unlockCost = 30, maxStones = 3, tutorialLevel = 30, tutorialVisits = 2;
            public PirLv[] levels = new PirLv[0];
        }
        static PirCfg pirCfg;
        static PirCfg PC
        {
            get
            {
                if (pirCfg != null) return pirCfg;
                pirCfg = new PirCfg();
                var ta = Resources.Load<TextAsset>("pirate");
                if (ta) try { JsonUtility.FromJsonOverwrite(ta.text, pirCfg); } catch (System.Exception e) { Debug.LogError("pirate.json 读不出来：" + e.Message); }
                return pirCfg;
            }
        }
        PirLv PirOverride => PC.levels == null ? null : PC.levels.FirstOrDefault(x => x.lv == levelIdx + 1);
        int PirTag => L == null || L.cfg == null ? 0 : Mathf.Clamp(L.cfg.tag, 0, 2);
        bool PirTutorial => levelIdx + 1 == PC.tutorialLevel;
        // 这关海盗多少秒来一次：按难度，再按第几座岛往下减，不低于 minEvery
        float PirEvery
        {
            get
            {
                var o = PirOverride; if (o != null && o.every > 0) return o.every;
                if (PirTutorial) return PC.tutorialEvery;
                int island = levelIdx / 10 + 1;
                float e = PC.every[Mathf.Min(PirTag, PC.every.Length - 1)] - PC.perIsland * Mathf.Max(0, island - 3);
                return Mathf.Max(PC.minEvery, e);
            }
        }
        // 石化多久自动解封
        float StoneSecs
        {
            get
            {
                var o = PirOverride; if (o != null && o.stone > 0) return o.stone;
                if (PirTutorial) return PC.tutorialStone;
                if (levelIdx + 1 >= PC.lateFrom) return PC.stoneLate;
                return PC.stone[Mathf.Min(PirTag, PC.stone.Length - 1)];
            }
        }
        int PIR_TAPS => Mathf.Max(1, PC.taps);
        int UNLOCK_COST => PC.unlockCost;
        int PirMaxVisits => PirTutorial ? PC.tutorialVisits : 999;

        static bool PirLevel(int n) => n == 30 || (n >= 40 && n % 10 == 0) || (n >= 52 && n <= 58 && n % 2 == 0) || n == 59;
        bool PirOn => L != null && !mixOn && !specOpen && (save.devPirate == 2 ? levelIdx >= BOUNTY_LV : save.devPirate == 1 && PirLevel(levelIdx + 1));
        bool PirPaused => BountyPaused;

        class StoneV { public float left, dur; public RectTransform r; public Image f; public Text n; }
        readonly Dictionary<int, StoneV> stones = new Dictionary<int, StoneV>();
        float pirT; int pirMark = -1, pirTaps, pirVisits, pirSaved, pirStoned, pirFreed; bool pirCome;
        float pirDark, pirFlash;                                   // 劈雷时天暗多少 / 白闪多亮（Storm.cs 每帧用）
        readonly List<GameObject> pirFx = new List<GameObject>();  // 激光、雷电这些临时物件：换关时一起删
        GameObject pirShip;
        RectTransform pirRing, pirBox; Text pirRingN, pirBoxN, pirBoxTitle; Image pirBoxT;
        public int PirSaved => pirSaved;
        const string STONE = "5c6169";

        void PirReset()
        {
            pirT = PC.first; pirMark = -1; pirTaps = 0; pirVisits = 0; pirSaved = pirStoned = pirFreed = 0; pirCome = false;
            pirDark = pirFlash = 0; foreach (var o in pirFx) if (o) Destroy(o); pirFx.Clear();
            if (pirShip) Destroy(pirShip); pirShip = null;
            foreach (var s in stones.Values) if (s.r) Destroy(s.r.gameObject);
            stones.Clear();
            if (pirRing) Destroy(pirRing.gameObject); if (pirBox) Destroy(pirBox.gameObject);
            pirRing = pirBox = null;
        }

        // ---------- 海盗船（方块拼的占位模型）：港湾左边，船身竖着 ----------
        Vector3 ShipHome()
        {
            float ya = cam.WorldToScreenPoint(W(0, lay.z0)).y, yb = cam.WorldToScreenPoint(W(0, lay.z1)).y;
            float sy = Mathf.Lerp(Mathf.Max(ya, yb), Mathf.Min(ya, yb), 0.32f);
            var r = cam.ScreenPointToRay(new Vector3(Screen.width * 0.075f, sy, 0));
            float k = -r.origin.y / r.direction.y; var p = r.origin + r.direction * k; p.y = 0; return p;
        }

        void ShipBuild()
        {
            pirShip = new GameObject("pirateShip"); var t = new GameObject("body").transform; t.SetParent(pirShip.transform, false); t.localScale = Vector3.one * 0.85f;
            var wood = Art.ColorMat("3a2414"); var dark = Art.ColorMat("1c130b"); var gold = Art.ColorMat("b8862e"); var cloth = Art.ColorMat("26262e"); var bone = Art.ColorMat("f4efe2");
            Transform P(PrimitiveType ty, Material m, Vector3 pos, Vector3 sc, float ry = 0) { var o = Prim(ty, t, m); o.localPosition = pos; o.localScale = sc; o.localRotation = Quaternion.Euler(0, ry, 0); return o; }
            P(PrimitiveType.Cube, wood, new Vector3(0, 0.22f, 0), new Vector3(0.95f, 0.46f, 2.5f));
            P(PrimitiveType.Cube, wood, new Vector3(0, 0.22f, 1.25f), new Vector3(0.67f, 0.46f, 0.67f), 45);
            foreach (float sx in new[] { -0.47f, 0.47f }) P(PrimitiveType.Cube, gold, new Vector3(sx, 0.5f, 0), new Vector3(0.07f, 0.1f, 2.5f));   // 两边金色船舷
            P(PrimitiveType.Cube, gold, new Vector3(0, 0.5f, -1.22f), new Vector3(0.95f, 0.1f, 0.07f));
            P(PrimitiveType.Cylinder, dark, new Vector3(0, 1.25f, 0.35f), new Vector3(0.08f, 0.85f, 0.08f));
            P(PrimitiveType.Cube, cloth, new Vector3(0, 1.45f, 0.35f), new Vector3(1.25f, 0.95f, 0.05f));
            foreach (float zz in new[] { 0.31f, 0.39f })   // 帆两面都画骷髅：白圆头 + 交叉骨头
            {
                P(PrimitiveType.Sphere, bone, new Vector3(0, 1.55f, zz), new Vector3(0.32f, 0.3f, 0.04f));
                var a = P(PrimitiveType.Cube, bone, new Vector3(0, 1.25f, zz), new Vector3(0.55f, 0.07f, 0.03f)); a.localRotation = Quaternion.Euler(0, 0, 30);
                var b = P(PrimitiveType.Cube, bone, new Vector3(0, 1.25f, zz), new Vector3(0.55f, 0.07f, 0.03f)); b.localRotation = Quaternion.Euler(0, 0, -30);
            }
            P(PrimitiveType.Cube, Art.ColorMat("d5392a"), new Vector3(0.18f, 2.1f, 0.35f), new Vector3(0.36f, 0.2f, 0.03f));
            foreach (float zz in new[] { -0.5f, 0.75f }) { var c = P(PrimitiveType.Cylinder, dark, new Vector3(0.55f, 0.42f, zz), new Vector3(0.14f, 0.2f, 0.14f)); c.localRotation = Quaternion.Euler(0, 0, 90); }   // 朝港湾的两门炮
            pirShip.transform.position = ShipHome() + new Vector3(-3.5f, 0, 0);
        }

        async Task ShipIn()
        {
            if (!pirShip) ShipBuild();
            var to = ShipHome(); var from = pirShip.transform.position;
            sfx.Play("sail");
            await Fx.Tween(900, p => { if (pirShip) pirShip.transform.position = Vector3.Lerp(from, to, 1 - (1 - p) * (1 - p)); });
        }

        // ---------- 每帧 ----------
        void PirTick(float dt)
        {
            if (!PirOn || st == null) return;
            if (pirShip) { var p = pirShip.transform.position; pirShip.transform.position = new Vector3(p.x, Mathf.Sin(Fx.Now / 600f) * 0.04f, p.z); pirShip.transform.rotation = Quaternion.Euler(Mathf.Sin(Fx.Now / 800f) * 2, 0, Mathf.Sin(Fx.Now / 700f) * 3); }
            PirRingTick(); StoneUITick();
            if (PirPaused) return;
            // 石船倒计时，到了自动解封
            foreach (var b in stones.Keys.ToList())
            {
                var s = stones[b]; s.left -= dt;
                if (s.left <= 0) Unstone(b, false);
            }
            // 来访
            if (pirMark >= 0)
            {
                if (st.inl[pirMark] == 0 && boatV[pirMark] != null && boatV[pirMark].state == "lot") PirClear();   // 被涨潮之类弄走了
                return;
            }
            if (pirVisits >= PirMaxVisits || st.status != "play") return;
            if (stones.Count >= PC.maxStones) return;   // 石船已经够多了：等解封一艘再来，免得整个港湾都堵死
            pirT -= dt;
            if (pirT > 0) return;
            int nb = PirPick();
            if (nb < 0) { pirT = 2; return; }
            pirVisits++; pirMark = nb; pirTaps = PIR_TAPS;
            Debug.Log("[海盗] 第 " + pirVisits + " 次来（难度" + PirTag + " 间隔" + PirEvery.ToString("0.#") + "秒 石化" + StoneSecs + "秒），红圈 船" + nb + " 色" + L.boats[nb].c);
            if (!pirCome) { pirCome = true; Banner(T("海盗来了！")); Fx.Run(ShipIn); }
            PirRingShow();
            sfx.Play("thunder", 1.4f, 0.25f); Haptics.Play(Haptics.Kind.Medium);
        }

        // 红圈挑哪艘：现在能开、或者挡它的那艘现在能开；不挑特殊船、没揭开的问号船、已经卡住的船
        int PirPick()
        {
            var lot = Rules.InLot(L, st).Where(b => L.boats[b].sp == 0 && !(L.boats[b].mys != 0 && st.seen[b] == 0) && boatV[b] != null && boatV[b].state == "lot" && !Rules.Stuck(L, st, b)).ToList();
            if (lot.Count < 3) return -1;
            var ok = new List<int>();
            foreach (int b in lot)
            {
                var w = Rules.PathOf(L, st, b);
                if (w.free || (w.hit >= 0 && Rules.CanGo(L, st, w.hit))) ok.Add(b);
            }
            return ok.Count == 0 ? -1 : ok[Random.Range(0, ok.Count)];
        }

        // RunEvents 里每个事件调一次
        void PirEv(Ev e)
        {
            if (e.t != "go" || e.how == "sea") return;
            if (stones.ContainsKey(e.b)) { StoneDrop(e.b); ColorBoatBack(e.b); Debug.Log("[海盗] 直升机吊走石船 船" + e.b); }   // 直升机吊走石船（规则层 Lift 已经解封）
            if (pirMark < 0) return;
            if (e.b == pirMark) { PirSave(); return; }
            pirTaps--;
            PirRingNum();
            if (pirTaps <= 0) { int b = pirMark; Fx.Run(async () => { await Fx.Sleep(250); PirFire(b); }); }
        }

        void PirSave()
        {
            pirSaved++; Debug.Log("[海盗] 救下 船" + pirMark);
            if (boatV[pirMark] != null) FloatText(boatV[pirMark].g.transform.position, T("救下！"));
            sfx.Play("help", 1.2f);
            BountyPirSave();
            PirClear();
        }

        void PirClear()
        {
            pirMark = -1; pirT = PirEvery;
            if (pirRing) { var r = pirRing; pirRing = null; Fx.Run(async () => { await Fx.Tween(200, p => { if (r) r.localScale = Vector3.one * (1 + p * 0.5f); }); if (r) Destroy(r.gameObject); }); }
        }

        // 没救下：海盗开一炮，炮弹落到船上 → 石化
        void PirFire(int b)
        {
            if (pirMark != b) return;
            PirClear();
            if (st.status != "play" || st.inl[b] == 0) return;
            var ev = new List<Ev>();
            if (!Rules.Petrify(L, st, b, ev)) return;
            pirStoned++;
            stones[b] = new StoneV { left = StoneSecs, dur = StoneSecs };
            Debug.Log("[海盗] 石化 船" + b + " 色" + L.boats[b].c + "，" + StoneSecs + " 秒后自动解封，现在石船 " + stones.Count + " 艘");
            Fx.Run(() => StoneFx(b));
        }

        // 11-05 用户：要更酷炫 —— 船头蓄能 → 红色激光锁定 → 天上劈下一道雷 → 船从白光里变成石头（一共 1.5 秒左右）
        //   规则层在 PirFire 里已经石化了；这里只是画面。中途被直升机吊走 / 花钱解封了就停。
        async Task StoneFx(int b)
        {
            int t = tok; var u = boatV[b]; if (u == null) return;
            if (!pirShip) { ShipBuild(); pirCome = true; await ShipIn(); if (t != tok) return; }
            var g = u.g.transform;
            bool Gone() => t != tok || !g || !stones.ContainsKey(b);
            Vector3 Hit() => g ? g.position + Vector3.up * 0.35f : Vector3.zero;
            Vector3 Gun() => pirShip ? pirShip.transform.position + new Vector3(0.55f, 0.95f, 0.3f) : Vector3.zero;

            // ① 蓄能 0.5 秒：天暗下来，船头一颗红光球越涨越大、火星往里吸；目标船上准星往里收
            sfx.Play("rumble", 1.3f, 0.8f); sfx.Play("warn", 0.7f, 0.5f); Haptics.Play(Haptics.Kind.Light);
            var orb = FxPrim(PrimitiveType.Sphere, Art.UnlitMat(UIKit.Hex("ffe3ea")));
            var halo = FxPrim(PrimitiveType.Sphere, Art.UnlitMat(UIKit.Hex("ff2d55", 0.45f), true));
            var lockR = LockRing();
            await Fx.Tween(520, p =>
            {
                pirDark = Mathf.Max(pirDark, p);
                float s = Mathf.Lerp(0.05f, 0.45f, p) * (1 + 0.15f * Mathf.Sin(Fx.Now / 30f));
                var a = Gun();
                if (orb) { orb.position = a; orb.localScale = Vector3.one * s * 0.8f; }
                if (halo) { halo.position = a; halo.localScale = Vector3.one * s * 2.1f; }
                if (lockR && g) { lockR.anchoredPosition = ToCanvas(cam.WorldToScreenPoint(Hit())); float k = 1 - (1 - p) * (1 - p); lockR.sizeDelta = Vector2.one * Mathf.Lerp(170, 72, k); lockR.localRotation = Quaternion.Euler(0, 0, -p * 220); }
                if (pirShip && Random.value < 0.35f) Spark(a + Random.onUnitSphere * 0.7f, a, UIKit.Hex(Random.value < 0.5f ? "ff6b88" : "ffd0dc"), 0.22f);
            });
            if (Gone()) { FxDone(orb, halo, lockR); StoneAbort(b, t); return; }

            // ② 激光 0.42 秒：一束红光打在船上，船被烧红，火星乱溅
            sfx.Play("warn", 1.9f, 0.7f); sfx.Play("crack", 1.7f, 0.5f); Haptics.Play(Haptics.Kind.Medium);
            var core = FxPrim(PrimitiveType.Cylinder, Art.UnlitMat(UIKit.Hex("fff2f5")));
            var glow = FxPrim(PrimitiveType.Cylinder, Art.UnlitMat(UIKit.Hex("ff1f4b", 0.62f), true));
            var hot = UIKit.Hex("ff3a30");
            await Fx.Tween(420, p =>
            {
                if (Gone()) return;
                var a = Gun(); var z = Hit(); float w = 1 + 0.35f * Mathf.Sin(Fx.Now / 18f);
                if (core) PoleSet(core, a, z, 0.075f * w);
                if (glow) PoleSet(glow, a, z, 0.25f * w * Mathf.Min(1, p * 4));
                if (orb) { orb.position = a; orb.localScale = Vector3.one * 0.38f * w; }
                if (halo) { halo.position = a; halo.localScale = Vector3.one * 0.9f * w; }
                foreach (var m in u.hull) m.SetColor("_BaseColor", Lit(Color.Lerp(u.hullC, hot, p * 0.8f)));
                if (lockR) { lockR.anchoredPosition = ToCanvas(cam.WorldToScreenPoint(z)); lockR.localRotation = Quaternion.Euler(0, 0, -220 - p * 400); }
                if (Random.value < 0.6f) Spark(z, z + new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(0.5f, 1.8f), Random.Range(-1.2f, 1.2f)), UIKit.Hex(Random.value < 0.5f ? "ffd23a" : "ff4d6d"), 0.3f);
                shake = Mathf.Max(shake, 0.06f);
            });
            FxDone(core, glow, orb, halo, lockR);
            if (Gone()) { StoneAbort(b, t); return; }

            // ③ 天上劈下一道雷：白闪 + 震屏 + 一圈冲击波，船先变白再凝成石头
            var hp = Hit();
            pirFlash = 0.8f; shake = 0.5f; Haptics.Play(Haptics.Kind.Heavy);
            sfx.Play("thunder", 1.15f, 1f); sfx.Play("crash", 0.75f, 0.9f);
            var bolt = Bolt(hp);
            Fx.Run(async () =>
            {
                foreach (int ms in new[] { 70, -45, 90, -40, 170 })
                {
                    if (!bolt) return;
                    bolt.SetActive(ms > 0); if (ms > 0 && ms != 70) pirFlash = Mathf.Max(pirFlash, 0.45f);
                    await Fx.Sleep(Mathf.Abs(ms));
                }
                if (bolt) { pirFx.Remove(bolt); Destroy(bolt); }
            });
            ShockRing(hp, UIKit.Hex("cfeeff"), 0); ShockRing(hp, UIKit.Hex(STONE), 120);
            Burst(hp, UIKit.Hex("bfe6ff"), 10, 1.3f);
            Burst(hp + Vector3.up * 0.1f, UIKit.Hex("8a9099"), 16, 1.3f);
            foreach (var ar in u.arrows) ar.SetActive(false); u.bob = false;
            FloatText(g.position, T("石化！"));
            var white = UIKit.Hex("f2fbff"); var stoneC = UIKit.Hex(STONE); var p0 = g.position;
            await Fx.Tween(560, p =>
            {
                if (Gone()) return;
                var c = p < 0.18f ? white : Color.Lerp(white, stoneC, (p - 0.18f) / 0.82f);
                foreach (var m in u.hull) m.SetColor("_BaseColor", Lit(c));
                foreach (var m in u.deck) m.SetColor("_BaseColor", Lit(Color.Lerp(c, Color.white, 0.3f)));
                g.position = p0 + new Vector3(Mathf.Sin(p * Mathf.PI * 7) * 0.09f * (1 - p), 0, 0);
            });
            DarkOff();
            if (Gone()) { StoneAbort(b, t); return; }
            g.position = p0;
            SetBoatColor(u, STONE);
            sfx.Play("crack", 0.55f, 0.7f);
            Burst(g.position + Vector3.up * 0.2f, UIKit.Hex("6d737c"), 8, 0.6f);
            StoneUIMake(b);
            PirBoxSync();
        }
        // 动画半路船没了 / 解封了：天亮回来，船的颜色交给解封那边（这里补一次免得停在烧红的颜色）
        void StoneAbort(int b, int t)
        {
            DarkOff();
            if (t == tok && !stones.ContainsKey(b) && boatV[b] != null && boatV[b].g) ColorBoatBack(b);
        }
        void DarkOff() { float d0 = pirDark; Fx.Run(() => Fx.Tween(700, p => pirDark = Mathf.Min(pirDark, d0 * (1 - p)))); }
        Transform FxPrim(PrimitiveType ty, Material m) { var o = Prim(ty, world, m); pirFx.Add(o.gameObject); return o; }
        void FxDone(params Component[] cs) { foreach (var c in cs) if (c) { pirFx.Remove(c.gameObject); Destroy(c.gameObject); } }
        // 火星：从 a 飞到 z（life 秒），不受重力
        void Spark(Vector3 a, Vector3 z, Color c, float life)
        {
            var m = NewCube(c, a); m.localScale = Vector3.one * 0.07f;
            parts.Add(new Part { m = m, v = (z - a) / life, life = life, g = 0 });
        }
        // 准星：红圈 + 四根往里指的短棒，跟着船，不接点击
        RectTransform LockRing()
        {
            var r = OnCanvas("pirLock"); r.sizeDelta = new Vector2(170, 170);
            var red = UIKit.Hex("ff2d3d");
            UIKit.Img(r, "o", CircleSprite(true), red).rectTransform.Stretch();
            UIKit.Img(r, "i", CircleSprite(true), UIKit.Hex("ff2d3d", 0.5f)).rectTransform.Stretch(14, 14, 14, 14);
            for (int k = 0; k < 4; k++)
            {
                var dir = new Vector2(k == 1 ? 1 : k == 3 ? -1 : 0, k == 0 ? 1 : k == 2 ? -1 : 0);
                var tk = UIKit.Img(r, "t" + k, null, red).rectTransform; tk.anchorMin = tk.anchorMax = new Vector2(0.5f, 0.5f) + dir * 0.5f;
                tk.sizeDelta = new Vector2(4, 20); tk.localRotation = Quaternion.Euler(0, 0, k * 90); tk.anchoredPosition = -dir * 4;
            }
            foreach (var gr in r.GetComponentsInChildren<Graphic>()) gr.raycastTarget = false;
            pirFx.Add(r.gameObject);
            return r;
        }
        // 水面冲击波：画面上一个压扁的圆环往外扩、变淡
        void ShockRing(Vector3 at, Color c, int delay)
        {
            int t = tok;
            Fx.Run(async () =>
            {
                if (delay > 0) await Fx.Sleep(delay);
                if (t != tok) return;
                var r = OnCanvas("pirShock"); var im = UIKit.Img(r, "o", CircleSprite(true), c); im.rectTransform.Stretch(); im.raycastTarget = false;
                r.anchoredPosition = ToCanvas(cam.WorldToScreenPoint(at)); pirFx.Add(r.gameObject);
                await Fx.Tween(480, p =>
                {
                    if (!r) return; float e = 1 - (1 - p) * (1 - p);
                    r.sizeDelta = new Vector2(Mathf.Lerp(30, 260, e), Mathf.Lerp(16, 140, e));
                    im.color = new Color(c.r, c.g, c.b, 0.9f * (1 - p));
                });
                if (r) { pirFx.Remove(r.gameObject); Destroy(r.gameObject); }
            });
        }
        // 闪电：从头顶很高的地方折线劈到船上；白芯 + 淡蓝光晕，再分两根小叉
        GameObject Bolt(Vector3 z)
        {
            var root = new GameObject("bolt"); root.transform.SetParent(world, false); pirFx.Add(root);
            var core = Art.UnlitMat(Color.white); var glow = Art.UnlitMat(UIKit.Hex("8fd3ff", 0.45f), true);
            var top = z + new Vector3(Random.Range(-1.5f, 1.5f), 18, Random.Range(1f, 3f));
            var pts = Jag(top, z, 10, 1.0f);
            for (int k = 0; k + 1 < pts.Count; k++) { Seg(root.transform, core, pts[k], pts[k + 1], 0.06f); Seg(root.transform, glow, pts[k], pts[k + 1], 0.22f); }
            for (int n = 0; n < 2; n++)
            {
                var a = pts[Random.Range(2, pts.Count - 3)];
                var e = a + new Vector3(Random.Range(-2.5f, 2.5f), -Random.Range(2f, 4f), Random.Range(-1.5f, 1.5f));
                var br = Jag(a, e, 4, 0.5f);
                for (int k = 0; k + 1 < br.Count; k++) { Seg(root.transform, core, br[k], br[k + 1], 0.03f); Seg(root.transform, glow, br[k], br[k + 1], 0.1f); }
            }
            return root;
        }
        static List<Vector3> Jag(Vector3 a, Vector3 z, int n, float amp)
        {
            var r = new List<Vector3> { a };
            for (int k = 1; k < n; k++) { float f = (float)k / n; r.Add(Vector3.Lerp(a, z, f) + new Vector3(Random.Range(-amp, amp), 0, Random.Range(-amp, amp)) * (1 - f * 0.7f)); }
            r.Add(z); return r;
        }
        static void Seg(Transform root, Material m, Vector3 a, Vector3 b, float r) { var o = Prim(PrimitiveType.Cylinder, root, m); PoleSet(o, a, b, r); }

        // 解封（paid = 花钱 / 看广告；否则是时间到）
        void Unstone(int b, bool paid)
        {
            if (!stones.ContainsKey(b)) return;
            var ev = new List<Ev>();
            Rules.Unpetrify(L, st, b, ev);
            StoneDrop(b); pirFreed++;
            Debug.Log("[海盗] 解封 船" + b + (paid ? "（花钱）" : "（时间到）"));
            var u = boatV[b];
            if (u != null && u.g)
            {
                ColorBoatBack(b);
                foreach (var ar in u.arrows) ar.SetActive(true);
                if (u.state == "lot") u.bob = true;
                Burst(u.g.transform.position + Vector3.up * 0.4f, UIKit.Hex("d8d8d8"), 12, 1f);
                var g = u.g.transform; float s0 = g.localScale.x;
                Fx.Run(() => Fx.Tween(300, p => { if (g) g.localScale = Vector3.one * s0 * (1 + 0.15f * Mathf.Sin(p * Mathf.PI)); }));
            }
            sfx.Play("reveal"); Haptics.Play(Haptics.Kind.Light);
        }
        void ColorBoatBack(int b) { var u = boatV[b]; if (u != null) ColorBoat(u, L.boats[b].c); }
        void StoneDrop(int b)
        {
            if (stones.TryGetValue(b, out var s) && s.r) Destroy(s.r.gameObject);
            stones.Remove(b);
            PirBoxSync();
        }
        // 点到石船：抖一下、顶上的解封牌跳一下
        void StoneRefuse(int b)
        {
            sfx.Play("bad"); Haptics.Play(Haptics.Kind.Medium); ShakeBoat(b, Vector3.right);
            if (pirBox) Bump(pirBox);
            if (stones.TryGetValue(b, out var s) && s.r) Bump(s.r);
        }
        void UnlockBuy()
        {
            if (stones.Count == 0) return;
            int b = stones.OrderBy(kv => kv.Value.left).First().Key;
            sfx.Play("click");
            if (save.coins >= UNLOCK_COST) { save.coins -= UNLOCK_COST; save.Persist(); UpdateHUD(); Analytics.Track("unstone", "lv", levelIdx + 1, "pay", "coin"); Unstone(b, true); }
            else WatchAd("unstone", () => { Analytics.Track("unstone", "lv", levelIdx + 1, "pay", "ad"); Unstone(b, true); });
        }
        void Bump3D(Transform tr) { Fx.Run(() => Fx.Tween(300, p => { if (tr) tr.localScale = Vector3.one * (1 + 0.1f * Mathf.Sin(p * Mathf.PI)); })); }

        // ---------- 界面：红圈（跟着船走）+ 剩几下 ----------
        void PirRingShow()
        {
            if (pirRing) Destroy(pirRing.gameObject);
            pirRing = OnCanvas("pirRing"); pirRing.sizeDelta = new Vector2(84, 84);
            var ring = UIKit.Img(pirRing, "ring", CircleSprite(true), UIKit.Hex("e8312a")); ring.rectTransform.Stretch();
            var ring2 = UIKit.Img(pirRing, "ring2", CircleSprite(true), UIKit.Hex("e8312a")); ring2.rectTransform.Stretch(4, 4, 4, 4);
            var badge = UIKit.Img(pirRing, "badge", CircleSprite(false), UIKit.Hex("1e1e24")); badge.rectTransform.Place(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-8, -8), new Vector2(28, 28));
            UIKit.Img(badge.rectTransform, "rim", CircleSprite(true), UIKit.Hex("e8312a")).rectTransform.Stretch(-1, -1, -1, -1);
            pirRingN = UIKit.Txt(badge.rectTransform, "n", "", 17, Color.white); pirRingN.fontStyle = FontStyle.Bold; pirRingN.rectTransform.Stretch(0, 1, 0, 0);
            foreach (var gr in pirRing.GetComponentsInChildren<Graphic>()) gr.raycastTarget = false;
            PirRingNum();
            var r0 = pirRing; Fx.Run(() => Fx.Tween(300, p => { if (r0) r0.localScale = Vector3.one * Mathf.Lerp(2.5f, 1, 1 - (1 - p) * (1 - p)); }));
        }
        void PirRingNum() { if (pirRingN) pirRingN.text = Mathf.Max(0, pirTaps).ToString(); if (pirRing && pirTaps < PIR_TAPS) Bump(pirRing); }
        void PirRingTick()
        {
            if (!pirRing || pirMark < 0 || boatV[pirMark] == null) return;
            pirRing.anchoredPosition = ToCanvas(cam.WorldToScreenPoint(boatV[pirMark].g.transform.position));
            float k = 0.5f + 0.5f * Mathf.Sin(Fx.Now / 140f);
            foreach (var im in pirRing.GetComponentsInChildren<Image>()) if (im.name.StartsWith("ring")) im.color = new Color(0.91f, 0.19f, 0.16f, 0.65f + 0.35f * k);
        }

        // ---------- 界面：每艘石船上一个解封倒计时（只是看的，不接点击）----------
        void StoneUIMake(int b)
        {
            if (!stones.TryGetValue(b, out var s)) return;
            if (s.r) Destroy(s.r.gameObject);
            s.r = OnCanvas("stone"); s.r.sizeDelta = new Vector2(28, 28);
            UIKit.Img(s.r, "bg", CircleSprite(false), UIKit.Hex("3a3e46", 0.9f)).rectTransform.Stretch();
            s.f = UIKit.Img(s.r, "t", CircleSprite(true), UIKit.Hex("bfe3ff")); s.f.type = Image.Type.Filled; s.f.fillMethod = Image.FillMethod.Radial360; s.f.fillOrigin = (int)Image.Origin360.Top; s.f.fillClockwise = false; s.f.rectTransform.Stretch();
            s.n = UIKit.Txt(s.r, "n", "", 13, Color.white); s.n.fontStyle = FontStyle.Bold; s.n.rectTransform.Stretch();
            foreach (var gr in s.r.GetComponentsInChildren<Graphic>()) gr.raycastTarget = false;
            StoneUITick();
        }
        void StoneUITick()
        {
            foreach (var kv in stones)
            {
                var s = kv.Value; if (!s.r || boatV[kv.Key] == null) continue;
                s.r.anchoredPosition = ToCanvas(cam.WorldToScreenPoint(boatV[kv.Key].g.transform.position)) + new Vector2(0, 30);   // 挂在船上方，别盖住石船（木筏很小，一盖就看不见了）
                s.f.fillAmount = Mathf.Clamp01(s.left / Mathf.Max(0.1f, s.dur)); s.n.text = Mathf.CeilToInt(Mathf.Max(0, s.left)).ToString();
            }
            if (pirBox && stones.Count > 0)
            {
                var s = stones.Values.OrderBy(x => x.left).First();
                pirBoxT.fillAmount = Mathf.Clamp01(s.left / Mathf.Max(0.1f, s.dur)); pirBoxN.text = Mathf.CeilToInt(Mathf.Max(0, s.left)).ToString();
                pirBox.anchoredPosition = Vector2.Lerp(pirBox.anchoredPosition, PirBoxPos(), 0.25f);
            }
        }

        // ---------- 界面：顶栏下面左边（悬赏木牌下面）一条「石化 N 艘 · 立刻解封」横牌 ----------
        // 11-05 用户：框别挡海面上的船 → 放在队尾那几排人上面（那里点了本来就没反应）
        static readonly Vector2 PB_SIZE = new Vector2(196, 56);
        Vector2 PirBoxPos() => btyCard ? BTY_POS + new Vector2(0, -BTY_SIZE.y - 6) : BTY_POS;
        void PirBoxSync()
        {
            if (stones.Count == 0)
            {
                if (pirBox) { var bx = pirBox; pirBox = null; Fx.Run(async () => { await Fx.Tween(200, p => { if (bx) bx.localScale = new Vector3(1, 1 - p, 1); }); if (bx) Destroy(bx.gameObject); }); }
                return;
            }
            if (!pirBox)
            {
                pirBox = UIKit.Rect("unstone", ui.top.parent).Place(new Vector2(0, 1), new Vector2(0, 1), PirBoxPos(), PB_SIZE);
                UIKit.Panel(pirBox, "bg", UIKit.Hex("2b2f36", 0.94f), 3).Stretch();
                var ring = UIKit.Rect("ring", pirBox).Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(28, 0), new Vector2(40, 40));
                UIKit.Img(ring, "bg", CircleSprite(false), UIKit.Hex("000000", 0.35f)).rectTransform.Stretch();
                pirBoxT = UIKit.Img(ring, "t", CircleSprite(true), UIKit.Hex("bfe3ff")); pirBoxT.type = Image.Type.Filled; pirBoxT.fillMethod = Image.FillMethod.Radial360; pirBoxT.fillOrigin = (int)Image.Origin360.Top; pirBoxT.fillClockwise = false; pirBoxT.rectTransform.Stretch();
                pirBoxN = UIKit.Txt(ring, "n", "", 17, Color.white); pirBoxN.fontStyle = FontStyle.Bold; pirBoxN.rectTransform.Stretch();
                pirBoxTitle = UIKit.Txt(pirBox, "tt", "", 14, UIKit.Hex("e6ecf5"), TextAnchor.MiddleLeft); pirBoxTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(54, -17), new Vector2(70, 20));
                var tt2 = UIKit.Txt(pirBox, "tt2", T("挡着路"), 12, UIKit.Hex("a9b2bf"), TextAnchor.MiddleLeft); tt2.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(54, -38), new Vector2(66, 18));
                var btn = UIKit.Panel(pirBox, "buy", UIKit.Hex("3fb950"), 3).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-6, 0), new Vector2(74, 44));
                var bt = UIKit.Txt(btn, "t", T("立刻解封"), 13, Color.white, TextAnchor.MiddleCenter); bt.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -3), new Vector2(74, 20)); Styled(bt, UIKit.Hex("1d5e26"), 1.4f, 1f);
                var bc = UIKit.Txt(btn, "c", UNLOCK_COST.ToString(), 14, UIKit.Hex("fff3a0"), TextAnchor.MiddleRight); bc.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 2), new Vector2(40, 20)); Styled(bc, UIKit.Hex("1d5e26"), 1.4f, 1f);
                CoinIcon(btn, new Vector2(51, -9), 15);
                UIKit.Btn(btn, UnlockBuy);
                var r0 = pirBox; Fx.Run(() => Fx.Tween(260, p => { if (r0) r0.localScale = new Vector3(1, Mathf.Lerp(0.05f, 1, 1 - (1 - p) * (1 - p)), 1); }));
            }
            pirBoxTitle.text = F("石化 {0} 艘", stones.Count);
            StoneUITick();
        }
    }
}
