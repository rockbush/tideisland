// 家园：用星星重建潮汐岛。四个区（海滩营地 → 渔村 → 港口小镇 → 灯塔岬），每个区几项建造任务。
// 布局表 Resources/home.json（x, y = 世界的 x, z），模型和预览在 art/blender/make_home.py。
// 星星：每关第一次通关给（普通 1、困难 2、超难 3），只能花在这里；50 关一共 65 颗，建完整座岛要 61 颗。
// 每拿满 10 颗星开一个星星宝箱；建完一个区领一份完工奖励。
// 有用的建筑：直升机坪每天送 1 架直升机，仓库每天送 1 个喇叭，小餐馆让通关金币 +10%。
// 连胜奖励也在这里（下一关开局送道具，输了 / 中途退出就清零）。
// 3D 场景摆在离关卡很远的地方（HOFF），开家园时镜头挪过去，关卡原封不动留在原地。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Tide.Loc;

namespace Tide
{
    [Serializable] public class HomePart { public string m, tint, spin; public float x, y, h, r, s; }
    [Serializable] public class HomeTask { public string id, name, perk; public int cost; public float ruin; public HomePart[] parts; }
    [Serializable] public class HomeArea { public string id, name; public float z; public int coins, crane, sort; public HomeTask[] tasks; }
    [Serializable] public class HomeData { public HomeArea[] areas; }

    // 家园界面上拖动看岛
    public class HomeDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Action<Vector2> drag; public Action<bool> hold;
        public void OnBeginDrag(PointerEventData e) => hold?.Invoke(true);
        public void OnDrag(PointerEventData e) => drag?.Invoke(e.delta);
        public void OnEndDrag(PointerEventData e) => hold?.Invoke(false);
    }

    public partial class Game
    {
        static readonly Vector3 HOFF = new Vector3(300, 0, 0);
        const int CHEST_EVERY = 10;
        // 岛中间那条沙路（和 make_home.py 一样），岛民沿着它走
        static readonly Vector2[] HOME_PATH = { new Vector2(0.0f, -1.0f), new Vector2(0.0f, 3.0f), new Vector2(-0.1f, 7.0f), new Vector2(0.0f, 10.5f), new Vector2(0.05f, 14.0f),
            new Vector2(0.1f, 17.5f), new Vector2(0.1f, 21.6f), new Vector2(0.35f, 26.0f), new Vector2(0.65f, 29.5f), new Vector2(0.9f, 32.3f) };
        static readonly Dictionary<string, string> PERK_TIP = new Dictionary<string, string> {
            { "heli", "每天来这里领 1 架免费直升机" }, { "horn", "每天来这里领 1 个免费喇叭" }, { "coin", "以后每关通关金币 +10%" } };

        HomeData HOME;
        bool homeOpen; string homeFrom; int homeTok;
        Transform homeRoot;
        float homeZ, homeZT; bool homeHold;
        int gCrane, gSort;                 // 连胜送的道具：只在这一关有效
        class Spot { public HomeTask t; public int a; public GameObject ruin; public RectTransform mark; public Vector3 at; }
        readonly List<Spot> spots = new List<Spot>();
        readonly List<(Transform t, string kind, float ph)> spins = new List<(Transform, string, float)>();
        class Folk { public Transform g, body; public Vector3 to; public float wait, walk, yaw; }
        readonly List<Folk> folks = new List<Folk>();
        RectTransform homeMarks, homeFly, homeStarPill, homeCoinPill, homeChest, homeBar, homePlay;
        Text homeStarT, homeCoinT, homeAreaT, homeChestT;
        Image homeBarFill;
        readonly List<(RectTransform r, Vector3 at)> homeLocks = new List<(RectTransform, Vector3)>();
        readonly List<(RectTransform r, Vector3 at, string perk)> homePerks = new List<(RectTransform, Vector3, string)>();

        // ---------------------------------------------------------------- 数据
        void HomeInit()
        {
            HOME = JsonUtility.FromJson<HomeData>(Resources.Load<TextAsset>("home").text);
            if (save.homeVer >= 1) return;
            // 老存档：以前打过的关按新规则补星星；以前花金币盖的四个营地建筑退钱（篝火 40 / 小木屋 90 / 码头 150 / 灯塔 240）
            var refund = new Dictionary<string, int> { { "fire", 40 }, { "hut", 90 }, { "dock", 150 }, { "tower", 240 } };
            foreach (var kv in refund) if (save.built.Remove(kv.Key)) save.coins += kv.Value;
            int got = 0;
            for (int i = 0; i < Mathf.Min(save.top, LEVELS.Length); i++) { got += LevelStars(i); while (save.stars.Count <= i) save.stars.Add(0); save.stars[i] = LevelStars(i); }
            save.star = got; save.starGot = got; save.chests = got / CHEST_EVERY;    // 补的星星不补宝箱（不然老玩家一进来连开好几个）
            save.homeVer = 1; save.Persist();
        }

        int LevelStars(int i) => LEVELS[i].cfg.tag == 2 ? 3 : LEVELS[i].cfg.tag == 1 ? 2 : 1;
        bool Built(string id) => save.built.Contains(id);
        bool AreaDone(int a) => HOME.areas[a].tasks.All(t => Built(t.id));
        int CurArea { get { for (int a = 0; a < HOME.areas.Length; a++) if (!AreaDone(a)) return a; return HOME.areas.Length; } }
        int NextCost { get { int a = CurArea; return a >= HOME.areas.Length ? 0 : HOME.areas[a].tasks.Where(t => !Built(t.id)).Min(t => t.cost); } }
        bool CanBuild => CurArea < HOME.areas.Length && save.star >= NextCost;
        bool HasPerk(string p) => HOME.areas.Any(A => A.tasks.Any(t => t.perk == p && Built(t.id)));
        bool PerkReady(string p) => HasPerk(p) && Unlocked(p == "heli" ? 0 : 1) && (p == "heli" ? save.heliDay : save.hornDay) != SaveData.Day;   // 道具没解锁就先不让领
        int ChestReady => save.starGot / CHEST_EVERY - save.chests;
        bool HomeNews => CanBuild || ChestReady > 0 || PerkReady("heli") || PerkReady("horn");   // 开始界面家园按钮上的红点

        // 通关拿星星（只有第一次）：返回这次拿了几颗
        int GrantStars()
        {
            if (save.Star(levelIdx) > 0) return 0;
            int n = LevelStars(levelIdx);
            save.SetStar(levelIdx, n); save.star += n; save.starGot += n;
            return n;
        }

        // ---------------------------------------------------------------- 连胜
        // 连胜 1：喇叭；2：再加一个泊位；3 以上：再加一架直升机。都只在这一关有效
        // 2026-10-23 用户：连胜什么都不送（开局多一个泊位太简单，抵消了关卡的泊位压力）。连胜数还照记（埋点用），界面上不再提
        static readonly bool STREAK_GIFT_ON = false;
        string StreakGiftText(int n)
        {
            if (n <= 0 || !STREAK_GIFT_ON) return "";
            var l = new List<string>();
            if (Unlocked(1)) l.Add(T("喇叭"));            // 还没解锁的道具不送（第 5/7 关才有）
            if (n >= 2) l.Add(T("泊位"));
            if (n >= 3 && Unlocked(0)) l.Add(T("直升机"));
            return string.Join(En ? " + " : "＋", l);
        }
        void StreakGift()
        {
            gCrane = gSort = 0;
            int n = save.streak; if (n <= 0 || demo || !STREAK_GIFT_ON) return;
            gSort = Unlocked(1) ? 1 : 0; gCrane = n >= 3 && Unlocked(0) ? 1 : 0;
            if (gSort + gCrane == 0 && n < 2) return;
            Analytics.Track("streak_gift", "lv", levelIdx + 1, "streak", n);
            GuideStreak();
            int t = tok;
            Fx.Run(async () =>
            {
                await Fx.Sleep(650); if (t != tok) return;
                Banner(F("连胜 ×{0}！", n), "streak"); Toast(F("连胜 ×{0}！送你：{1}", n, StreakGiftText(n)));
                sfx.Play("reveal"); Bump(ui.bb[1]); if (n >= 3) Bump(ui.bb[0]);
                if (n >= 2 && st.berth.Count < MAXB) { await Fx.Sleep(1300); if (t == tok) OpenBerth(); }   // 等连胜横幅放完再开泊位（它也有横幅）
                UpdateHUD();
            });
        }
        // 中途退出（重来 / 回地图选别的关）算输：连胜清零
        void BreakStreak() { if (save.streak > 0) { Analytics.Track("streak_lost", "lv", levelIdx + 1, "streak", save.streak); save.streak = 0; save.Persist(); } }

        // ---------------------------------------------------------------- 打开 / 关上
        void ShowHome(string from)
        {
            Analytics.Track("screen", "name", "home", "from", from, "star", save.star);
            homeFrom = from; homeTok++;
            HideTitle(); HideMap(); HideModal(); CloseMenu(); SetHud(false); ClearToasts();
            homeOpen = true;
            if (rainRoot) rainRoot.gameObject.SetActive(false);
            BuildHome();
            int a = Mathf.Min(CurArea, HOME.areas.Length - 1);
            homeZ = homeZT = HOME.areas[a].z;
            BuildHomeUI();
            HomeCam(0);
            GuideHome();      // 第一次能建东西：手指带着建（Guide.cs），原来这里是一条提示文字
        }

        void HideHome()
        {
            homeOpen = false; homeTok++;
            if (homeRoot) Destroy(homeRoot.gameObject);
            homeRoot = null; spots.Clear(); spins.Clear(); folks.Clear(); homeLocks.Clear(); homePerks.Clear();
            foreach (Transform c in ui.home) Destroy(c.gameObject);
            ui.home.gameObject.SetActive(false);
            if (rainRoot) rainRoot.gameObject.SetActive(true);
            PlaceCam(true);
        }

        void HomeBack()
        {
            string from = homeFrom; HideHome();
            if (from == "title") ShowTitle();
            else if (from == "game") SetHud(true);
            else ShowMap(true);       // 从通关弹窗进来的：回地图（下一关在那儿等着解锁）
        }
        void HomePlay()
        {
            if (homeOpen) HideHome();
            if (save.top >= LEVELS.Length) ShowMap(true); else PlayLevel(CurLevel);
        }

        // ---------------------------------------------------------------- 3D 场景
        void BuildHome()
        {
            if (homeRoot) Destroy(homeRoot.gameObject);
            homeRoot = new GameObject("Home").transform; homeRoot.position = HOFF;
            spots.Clear(); spins.Clear(); folks.Clear();
            var sea = GameObject.CreatePrimitive(PrimitiveType.Plane); Destroy(sea.GetComponent<Collider>());
            sea.transform.SetParent(homeRoot, false); sea.transform.localPosition = new Vector3(0, -0.03f, 16); sea.transform.localScale = Vector3.one * 26;
            sea.GetComponent<MeshRenderer>().sharedMaterial = seaT.GetComponent<MeshRenderer>().sharedMaterial;
            Art.Spawn("home_isle", homeRoot, false);
            for (int a = 0; a < HOME.areas.Length; a++)
                foreach (var t in HOME.areas[a].tasks)
                {
                    var p0 = t.parts[0];
                    var s = new Spot { t = t, a = a, at = HOFF + new Vector3(p0.x, p0.h, p0.y) };
                    if (Built(t.id)) foreach (var p in t.parts) SpawnPart(p);
                    else s.ruin = SpawnRuin(t);
                    spots.Add(s);
                }
            int nf = Mathf.Min(18, save.built.Count + 1);
            if (save.built.Count > 0) for (int k = 0; k < nf; k++) AddFolk(k);
        }

        GameObject SpawnRuin(HomeTask t)
        {
            var p = t.parts[0];
            var g = Art.Spawn("home_ruin", homeRoot, false);
            g.transform.position = HOFF + new Vector3(p.x, p.h, p.y); g.transform.rotation = Quaternion.Euler(0, p.x * 40, 0);
            g.transform.localScale = Vector3.one * t.ruin * 1.25f;
            return g;
        }

        GameObject SpawnPart(HomePart p)
        {
            var w = new GameObject(p.m); w.transform.SetParent(homeRoot, false);
            w.transform.position = HOFF + new Vector3(p.x, p.h, p.y); w.transform.rotation = Quaternion.Euler(0, p.r, 0); w.transform.localScale = Vector3.one * p.s;
            if (p.m == "heli")
            {
                Art.Spawn("heli_body", w.transform, false);
                spins.Add((Pivot(Art.Spawn("heli_rotor", w.transform, false).transform, w.transform), "rotor", 0));
                Art.Spawn("heli_tail", w.transform, false);
                return w;
            }
            var g = Art.Spawn(p.m, w.transform, false);
            if (!string.IsNullOrEmpty(p.tint))
                foreach (var r in g.GetComponentsInChildren<Renderer>())
                {
                    var ms = r.sharedMaterials;
                    for (int i = 0; i < ms.Length; i++)
                    {
                        if (!ms[i]) continue;
                        if (ms[i].name.StartsWith("M_BoatHull")) ms[i] = Art.TeamMat(p.tint);
                        else if (ms[i].name.StartsWith("M_BoatDeck")) ms[i] = Art.ColorMat(ColorUtility.ToHtmlStringRGB(Color.Lerp(UIKit.Hex(p.tint), Color.white, 0.45f)).ToLower());
                    }
                    r.sharedMaterials = ms;
                }
            if (p.spin == "mill") spins.Add((Pivot(g.transform, w.transform), "mill", 0));
            else if (p.spin == "flame") spins.Add((g.transform, "flame", 0));
            else if (!string.IsNullOrEmpty(p.tint)) spins.Add((g.transform, "bob", p.x));
            return w;
        }

        float HomeGround(Vector3 p) => p.z - HOFF.z < 3.4f ? 0.22f : 0.3f;

        // 岛民在建好的地方之间走来走去
        List<Vector3> FolkSpots()
        {
            var l = new List<Vector3>();
            float zMax = HOME.areas[Mathf.Min(CurArea, HOME.areas.Length - 1)].z + 5;
            foreach (var p in HOME_PATH) if (p.y < zMax) l.Add(HOFF + new Vector3(p.x, 0, p.y));
            foreach (var s in spots) if (Built(s.t.id) && s.t.parts[0].h > 0 && s.t.parts[0].h < 0.5f) l.Add(s.at + new Vector3(0, 0, -0.85f * s.t.parts[0].s));
            for (int i = 0; i < l.Count; i++) { var v = l[i]; v.y = HomeGround(v); l[i] = v; }
            return l;
        }
        void AddFolk(int k)
        {
            var pts = FolkSpots(); if (pts.Count == 0) return;
            var f = new Folk { yaw = UnityEngine.Random.value * 6.28f, wait = UnityEngine.Random.value * 2 };
            f.g = new GameObject("folk").transform; f.g.SetParent(homeRoot, false);
            var body = new GameObject("body"); body.transform.SetParent(f.g, false); f.body = body.transform;
            int v = (k * 3 + UnityEngine.Random.Range(0, 8)) % COLS.Length;   // 头饰跟着颜色走
            body.AddComponent<MeshFilter>().sharedMesh = Art.Guy(v).mesh;
            body.AddComponent<MeshRenderer>().sharedMaterials = Art.GuyMats(v, COLS[v]);
            f.g.localScale = Vector3.one * 0.8f;
            var p = pts[UnityEngine.Random.Range(0, pts.Count)] + new Vector3((UnityEngine.Random.value - 0.5f) * 0.8f, 0, (UnityEngine.Random.value - 0.5f) * 0.8f);
            p.y = HomeGround(p); f.g.position = p; f.to = p;
            folks.Add(f);
        }

        // ---------------------------------------------------------------- 每帧
        void HomeCam(float dt)
        {
            // 岛宽约 7 米：镜头远近按屏幕宽度定，左右各露一点海
            float tanH = Mathf.Tan(15 * Mathf.Deg2Rad) * Mathf.Min(cam.aspect, 0.62f);
            float dist = Mathf.Clamp(4.5f / tanH, 18, 60);
            int a = Mathf.Min(CurArea, HOME.areas.Length - 1);
            float zMin = -1.5f, zMax = CurArea >= HOME.areas.Length ? HOME.areas[a].z + 1 : HOME.areas[a].z + 4;
            if (!homeHold) homeZT = Mathf.Clamp(homeZT, zMin, zMax);
            homeZ = dt <= 0 ? homeZT : Mathf.Lerp(homeZ, homeZT, Mathf.Min(1, dt * (homeHold ? 30 : 6)));
            var target = HOFF + new Vector3(0, 0, homeZ + 1.2f);
            var sh = new Vector3((UnityEngine.Random.value - 0.5f) * shake, (UnityEngine.Random.value - 0.5f) * shake, 0);
            cam.transform.position = target + sh + new Vector3(0, dist * Mathf.Cos(PHI), -dist * Mathf.Sin(PHI));
            cam.transform.LookAt(target + sh);
        }

        void HomeDragBy(Vector2 d)
        {
            float a = cam.WorldToScreenPoint(HOFF + new Vector3(0, 0, homeZ)).y, b = cam.WorldToScreenPoint(HOFF + new Vector3(0, 0, homeZ + 1)).y;
            float ppu = Mathf.Max(1, b - a);
            homeZT -= d.y / ppu;
            int ai = Mathf.Min(CurArea, HOME.areas.Length - 1);
            homeZT = Mathf.Clamp(homeZT, -3f, HOME.areas[ai].z + 6);
        }

        void HomeTick(float dt)
        {
            HomeCam(dt);
            float now = Fx.Now;
            foreach (var (t, kind, ph) in spins)
            {
                if (!t) continue;
                if (kind == "mill") t.localRotation = Quaternion.Euler(0, 0, now * 0.06f);
                else if (kind == "rotor") t.localRotation = Quaternion.Euler(0, now * 0.12f, 0);
                else if (kind == "flame") t.localScale = new Vector3(1 + 0.08f * Mathf.Sin(now / 70), 0.85f + 0.2f * Mathf.Abs(Mathf.Sin(now / 110)), 1 + 0.08f * Mathf.Cos(now / 90));
                else if (kind == "bob") { var p = t.localPosition; p.y = Mathf.Sin(now / 650 + ph) * 0.03f; t.localPosition = p; t.localRotation = Quaternion.Euler(Mathf.Sin(now / 800 + ph) * 2, 0, Mathf.Sin(now / 700 + ph) * 3); }
            }
            List<Vector3> pts = null;
            foreach (var f in folks)
            {
                if (!f.g) continue;
                var p = f.g.position; var d = f.to - p; d.y = 0;
                if (d.magnitude > 0.05f)
                {
                    float s = Mathf.Min(d.magnitude, 0.9f * dt);
                    p += d.normalized * s; p.y = HomeGround(p); f.g.position = p;
                    f.yaw = Fx.LerpAng(f.yaw, Mathf.Atan2(d.x, d.z), 0.2f); f.walk += dt * 14;
                    f.body.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(f.walk)) * 0.08f, 0);
                }
                else
                {
                    f.body.localPosition = Vector3.zero;
                    f.wait -= dt;
                    if (f.wait <= 0)
                    {
                        pts = pts ?? FolkSpots();
                        var near = pts.Where(q => Mathf.Abs(q.z - p.z) < 6).ToList(); if (near.Count == 0) near = pts;
                        f.to = near[UnityEngine.Random.Range(0, near.Count)] + new Vector3((UnityEngine.Random.value - 0.5f) * 0.7f, 0, (UnityEngine.Random.value - 0.5f) * 0.5f);
                        f.wait = 1 + UnityEngine.Random.value * 3;
                    }
                }
                f.g.rotation = Quaternion.Euler(0, f.yaw * Mathf.Rad2Deg, 0);
            }
            // 牌子跟着 3D 位置走
            float bob = Mathf.Abs(Mathf.Sin(now / 300)) * 8;
            foreach (var s in spots)
            {
                if (!s.mark) continue;
                var sp = cam.WorldToScreenPoint(s.at + Vector3.up * (0.9f + 0.25f * s.t.ruin));
                s.mark.gameObject.SetActive(sp.z > 0);
                s.mark.anchoredPosition = ToCanvas(sp) + new Vector2(0, save.star >= s.t.cost ? bob : 0);
            }
            foreach (var (r, at) in homeLocks)
            {
                if (!r) continue;
                var sp = cam.WorldToScreenPoint(at);
                r.gameObject.SetActive(sp.z > 0 && sp.y < Screen.height * 0.78f);    // 跑到顶栏那块就不显示
                r.anchoredPosition = ToCanvas(sp);
            }
            foreach (var (r, at, _) in homePerks) if (r) r.anchoredPosition = ToCanvas(cam.WorldToScreenPoint(at)) + new Vector2(0, bob);
            if (homeChest) homeChest.localScale = Vector3.one * (ChestReady > 0 ? 1 + 0.07f * Mathf.Sin(now / 140) : 1);
            if (homePlay) homePlay.localScale = Vector3.one * (!CanBuild ? 1 + 0.035f * Mathf.Sin(now / 220) : 1);
        }

        // ---------------------------------------------------------------- 界面
        void BuildHomeUI()
        {
            var R = ui.home; foreach (Transform c in R) Destroy(c.gameObject);
            R.gameObject.SetActive(true); R.SetAsLastSibling(); ui.modal.SetAsLastSibling(); ui.ad.SetAsLastSibling();
            homeLocks.Clear(); homePerks.Clear();
            // 整屏透明的一层接拖动
            var catcher = UIKit.Img(R, "drag", null, new Color(0, 0, 0, 0), true); catcher.rectTransform.Stretch(-50, -50, -50, -50);
            var hd = catcher.gameObject.AddComponent<HomeDrag>(); hd.drag = HomeDragBy; hd.hold = h => homeHold = h;
            homeMarks = UIKit.Rect("marks", R).Stretch();
            var S = SafeRect(R, "safe");
            // ---- 顶栏：返回 · 区名丝带 · 星星 · 金币 ----
            var top = UIKit.Rect("top", S).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(370, 84));
            IconBtn(top, "icon_back", new Vector2(0, 1), new Vector2(26, -28), 52, () => { sfx.Play("click"); HomeBack(); }, "<");
            homeStarPill = Pill(top, new Vector2(-100, -27), out homeStarT);
            var si = UIKit.Rect("star", homeStarPill).Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(8, 1), new Vector2(38, 38));
            if (Skin.Has("star_gold")) Skin.Img(si, "star_gold").rectTransform.Stretch(); else UIKit.Txt(si, "t", "★", 26, UIKit.Hex("ffb81f")).rectTransform.Stretch();
            homeCoinPill = Pill(top, new Vector2(-2, -27), out homeCoinT);
            CoinIcon(homeCoinPill, new Vector2(8, 1), 32);
            // 区名 + 进度条
            RectTransform rib;
            if (Skin.Has("ribbon_blue")) { rib = Skin.Img(S, "ribbon_blue").rectTransform; rib.raycastTargetOn(); rib.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -100), new Vector2(300, 69)); }
            else rib = UIKit.Panel(S, "rib", UIKit.Hex("14608f"), 3, 3).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -100), new Vector2(260, 44));
            homeAreaT = UIKit.Txt(rib, "t", "", 18, Color.white); homeAreaT.rectTransform.Stretch(40, 29, 40, 5);   // 蓝丝带也是拱形，带面中心在上面 31% 处
            homeAreaT.resizeTextForBestFit = true; homeAreaT.resizeTextMinSize = 11; homeAreaT.resizeTextMaxSize = 18;
            Styled(homeAreaT, UIKit.Hex("123f7a"), 1.8f, 1.5f);
            // 测试用：3 秒内连点区名 5 下，送 10 颗星
            UIKit.Btn(rib, () =>
            {
                if (Time.realtimeSinceStartup - devT > 3) devTaps = 0;
                devT = Time.realtimeSinceStartup;
                if (++devTaps >= 5) { devTaps = 0; save.star += 10; save.starGot += 10; save.Persist(); HomeRefresh(); Toast(T("测试：送 10 颗星")); }
            });
            homeBar = UIKit.Rect("bar", S).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -140), new Vector2(200, 20));
            if (Skin.Has("bar_track")) Skin.Img(homeBar, "bar_track").rectTransform.Stretch(); else UIKit.Panel(homeBar, "track", UIKit.Hex("e3d6b8"), 0, 2).Stretch();
            homeBarFill = Skin.Has("bar_green") ? Skin.Img(homeBar, "bar_green") : UIKit.Img(homeBar, "f", UIKit.Round, UIKit.Green);
            var fr = homeBarFill.rectTransform; fr.anchorMin = new Vector2(0, 0); fr.anchorMax = new Vector2(0, 1); fr.pivot = new Vector2(0, 0.5f); fr.offsetMin = new Vector2(4, 4); fr.offsetMax = new Vector2(4, -4);
            // ---- 底部：宝箱 · 开始闯关 · 图鉴 ----
            homePlay = SkinBtn(S, "3fb950", "", 30, () => { sfx.Play("click"); HomePlay(); }, out var pl);
            homePlay.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 62), new Vector2(200, 76));
            pl.text = save.top >= LEVELS.Length ? T("关卡地图") : F("第 {0} 关", CurLevel + 1);
            pl.resizeTextForBestFit = true; pl.resizeTextMinSize = 14; pl.resizeTextMaxSize = 30;
            homeChest = RoundBtn(S, "card:chest", new Vector2(-136, 64), () => { sfx.Play("click"); OpenChest(); }, out homeChestT);
            RoundBtn(S, "card:folks", new Vector2(136, 64), () => { sfx.Play("click"); ShowAlbum(); }, out var at);
            at.text = T("图鉴");
            homeFly = UIKit.Rect("fly", R).Stretch();
            HomeRefresh();
        }

        RectTransform Pill(Transform parent, Vector2 pos, out Text t)
        {
            RectTransform r;
            if (Skin.Has("plate_wood")) { r = Skin.Img(parent, "plate_wood").rectTransform; r.Place(new Vector2(1, 1), new Vector2(1, 0.5f), pos, new Vector2(92, 38)); }
            else r = UIKit.Panel(parent, "pill", UIKit.Cream, 4).Place(new Vector2(1, 1), new Vector2(1, 0.5f), pos, new Vector2(92, 38));
            t = UIKit.Txt(r, "n", "0", 19, UIKit.Hex("fff3d6"), TextAnchor.MiddleCenter); t.rectTransform.Stretch(26, 2, 6, 0);
            Styled(t, UIKit.Hex("4a2a12"), 1.6f, 1.5f);
            return r;
        }

        // 底部的圆形按钮：奶白圆盘 + 图标 + 下面一行小字
        RectTransform RoundBtn(Transform parent, string icon, Vector2 pos, Action fn, out Text label)
        {
            RectTransform b;
            if (Skin.Has("disc_booster")) { b = Skin.Img(parent, "disc_booster").rectTransform; b.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), pos, new Vector2(82, 82)); }
            else b = UIKit.Disc(parent, "rb", UIKit.Cream, 5).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), pos, new Vector2(72, 72));
            if (Deco(icon)) DecoImg(b, icon, new Vector2(0.5f, 0.5f), new Vector2(0, 4), 62);
            label = UIKit.Txt(b, "l", "", 14, UIKit.Hex("fff3d6")); label.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(110, 24));
            Styled(label, UIKit.Hex("4a2a12"), 1.6f, 1.2f);
            UIKit.Btn(b, fn);
            return b;
        }

        // 星星数、区名、进度条、牌子按当前存档重摆
        void HomeRefresh()
        {
            homeStarT.text = save.star.ToString(); homeCoinT.text = save.coins.ToString();
            int a = CurArea;
            if (a >= HOME.areas.Length)
            {
                homeAreaT.text = T("整座岛都建好了！");
                homeBarFill.rectTransform.sizeDelta = new Vector2(homeBar.rect.width - 8, -8);
            }
            else
            {
                var A = HOME.areas[a]; int done = A.tasks.Count(t => Built(t.id));
                homeAreaT.text = F("第 {0} 区 · {1}  {2}/{3}", a + 1, T(A.name), done, A.tasks.Length);
                homeBarFill.rectTransform.sizeDelta = new Vector2(Mathf.Max(0, (homeBar.rect.width - 8) * done / (float)A.tasks.Length), -8);
                homeBarFill.gameObject.SetActive(done > 0);
            }
            int ready = ChestReady;
            homeChestT.text = ready > 0 ? T("开宝箱！") : (save.starGot % CHEST_EVERY) + "/" + CHEST_EVERY;
            // 牌子：只给当前这个区没建的；后面的区挂一把锁
            foreach (Transform c in homeMarks) Destroy(c.gameObject);
            homeLocks.Clear(); homePerks.Clear();
            foreach (var s in spots) s.mark = null;
            foreach (var s in spots)
            {
                if (Built(s.t.id) || s.a != a) continue;
                s.mark = Mark(s);
            }
            for (int k = a + 1; k < Mathf.Min(a + 2, HOME.areas.Length); k++)     // 只挂下一个区的锁，再往后的太远，会跑到顶上的丝带后面
            {
                var A = HOME.areas[k];
                var r = UIKit.Rect("lock", homeMarks); r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(200, 64);
                UIKit.Img(r, "bg", UIKit.Round, UIKit.Hex("1d2c3c", 0.72f)).rectTransform.Stretch();
                if (Skin.Has("icon_lock")) Skin.Img(r, "icon_lock").rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(40, 40));
                var t = UIKit.Txt(r, "t", T(A.name) + "\n<size=12>" + F("建完「{0}」解锁", T(HOME.areas[k - 1].name)) + "</size>", 16, Color.white);
                t.rectTransform.Stretch(50, 4, 8, 4);
                homeLocks.Add((r, HOFF + new Vector3(0, 0.5f, A.z)));
            }
            // 每日礼物泡泡
            foreach (var s in spots)
            {
                string pk = s.t.perk; if (string.IsNullOrEmpty(pk) || pk == "coin" || !Built(s.t.id) || !PerkReady(pk)) continue;
                var r = UIKit.Rect("perk", homeMarks); r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(70, 70);
                if (Skin.Has("ball_white")) Skin.Img(r, "ball_white", UIKit.Hex("fff3c4")).rectTransform.Stretch(); else UIKit.Disc(r, "d", UIKit.Cream, 0, 3).Stretch();
                DecoImg(r, pk == "heli" ? "ai_heli" : "ai_horn", new Vector2(0.5f, 0.5f), new Vector2(0, 2), 56);
                var tag = UIKit.Rect("tag", r).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(64, 22));
                if (Skin.Has("btn_green")) Skin.Img(tag, "btn_green").rectTransform.Stretch(); else UIKit.Panel(tag, "p", UIKit.Green, 0, 2).Stretch();
                UIKit.Txt(tag, "t", T("领取"), 13, Color.white).rectTransform.Stretch(0, 1, 0, 0);
                var ss = s; UIKit.Btn(r, () => ClaimPerk(ss, pk));
                homePerks.Add((r, s.at + Vector3.up * 1.9f, pk));
            }
            GuideHomeExtras();      // 放最后：要等礼物泡泡建好
        }

        // 一个建造牌子：星星 + 价钱，下面写名字。星星够就跳、颜色亮；不够就灰
        RectTransform Mark(Spot s)
        {
            bool can = save.star >= s.t.cost;
            var r = UIKit.Rect("mark", homeMarks); r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(84, 66);
            var plate = UIKit.Rect("plate", r).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(78, 40));
            if (Skin.Has("plate_cream")) Skin.Img(plate, "plate_cream", can ? Color.white : UIKit.Hex("cfc8bc")).rectTransform.Stretch();
            else UIKit.Panel(plate, "p", can ? UIKit.Cream : UIKit.Hex("cfc8bc"), 3).Stretch();
            var st_ = UIKit.Rect("s", plate).Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(22, 1), new Vector2(32, 32));
            if (Skin.Has(can ? "star_gold" : "star_grey")) Skin.Img(st_, can ? "star_gold" : "star_grey").rectTransform.Stretch(); else UIKit.Txt(st_, "t", "★", 22, UIKit.Hex("ffb81f")).rectTransform.Stretch();
            var n = UIKit.Txt(plate, "n", s.t.cost.ToString(), 22, can ? UIKit.Hex("6b3d14") : UIKit.Hex("8a8174")); n.rectTransform.Stretch(38, 2, 6, 0);
            var nm = UIKit.Txt(r, "nm", T(s.t.name), 14, Color.white); nm.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(120, 24));
            Styled(nm, UIKit.Hex("3b2a1a"), 1.6f, 1.2f);
            UIKit.Btn(r, () => TapSpot(s));
            return r;
        }

        void TapSpot(Spot s)
        {
            if (Built(s.t.id)) return;
            if (save.star < s.t.cost)
            {
                sfx.Play("bad"); Bump(homeStarPill); Haptics.Play(Haptics.Kind.Light);
                Toast(F("还差 {0} 颗星，去闯关拿星星吧", s.t.cost - save.star));
                return;
            }
            sfx.Play("click");
            BuildTask(s);
        }

        // ---------------------------------------------------------------- 建造
        void BuildTask(Spot s)
        {
            save.star -= s.t.cost; save.built.Add(s.t.id); save.Persist(); TaskAdd("build");
            Analytics.Track("home_build", "id", s.t.id, "area", s.a + 1, "cost", s.t.cost, "left", save.star, "lv", save.top);
            var mark = s.mark; s.mark = null;
            Vector2 to = mark ? mark.anchoredPosition : ToCanvas(cam.WorldToScreenPoint(s.at));
            if (mark) Destroy(mark.gameObject);
            homeStarT.text = save.star.ToString();
            int t = homeTok; homeBuilding = true;
            Fx.Run(async () =>
            {
                // 星星从左上角飞过去
                var c = new Vector3[4]; homeStarPill.GetWorldCorners(c);
                var from = ToCanvas(new Vector3(c[0].x + 10 * ui.scale, (c[0].y + c[1].y) / 2, 0));
                int n = Mathf.Min(5, s.t.cost + 1);
                for (int k = 0; k < n; k++) { int kk = k; Fx.Run(() => FlyStar(from, to, kk * 70)); }
                await Fx.Sleep(420 + n * 70); if (t != homeTok) return;
                // 废墟扬一下灰没了
                if (s.ruin)
                {
                    var rt = s.ruin.transform; Burst(rt.position + Vector3.up * 0.2f, UIKit.Hex("e8cf9a"), 14, 1.2f);
                    await Fx.Tween(200, p => { if (rt) rt.localScale = Vector3.one * s.t.ruin * 1.25f * (1 - p); });
                    if (s.ruin) Destroy(s.ruin); s.ruin = null;
                }
                if (t != homeTok) return;
                // 建筑从天上掉下来，落地压扁一下再弹回来
                sfx.Play("unlock"); Haptics.Play(Haptics.Kind.Medium);
                var gs = s.t.parts.Select(SpawnPart).ToList();
                var p0 = gs.Select(g => g.transform.position).ToList(); var s0 = gs.Select(g => g.transform.localScale).ToList();
                await Fx.Tween(520, p =>
                {
                    for (int i = 0; i < gs.Count; i++)
                    {
                        if (!gs[i]) continue;
                        float fall = p < 0.45f ? 1 - (p / 0.45f) * (p / 0.45f) : 0;
                        gs[i].transform.position = p0[i] + Vector3.up * fall * 4;
                        float q = p < 0.45f ? 0 : (p - 0.45f) / 0.55f;
                        float sq = p < 0.45f ? 0 : Mathf.Sin(q * Mathf.PI * 2) * (1 - q) * 0.28f;
                        gs[i].transform.localScale = new Vector3(s0[i].x * (1 + sq), s0[i].y * (1 - sq) * (p < 0.45f ? 1.15f : 1), s0[i].z * (1 + sq));
                    }
                    if (p >= 0.45f && p < 0.47f) { shake = 0.25f; }
                });
                if (t != homeTok) return;
                Burst(s.at + Vector3.up * 0.3f, UIKit.Hex("ffe27a"), 16, 1.4f);
                Burst(s.at + Vector3.up * 0.3f, UIKit.Hex(COLS[s.t.id.Length % 8]), 10, 1.2f);
                sfx.Play("star3"); sfx.Play("cheer", 1, 0.5f);
                if (folks.Count < 18) AddFolk(folks.Count);
                string msg = F("{0} 建好了！", T(s.t.name));
                if (!string.IsNullOrEmpty(s.t.perk)) msg += "\n" + T(PERK_TIP[s.t.perk]);
                Toast(msg); Banner(T("建好了！"), "built");
                homeBuilding = false;
                HomeRefresh();
                if (AreaDone(s.a) && !save.areasDone.Contains(HOME.areas[s.a].id)) { await Fx.Sleep(900); if (t == homeTok) AreaComplete(s.a); }
            });
        }

        async Task FlyStar(Vector2 from, Vector2 to, float delay)
        {
            await Fx.Sleep(delay);
            if (!homeFly) return;
            var r = UIKit.Rect("star", homeFly); r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(34, 34);
            if (Skin.Has("star_gold")) Skin.Img(r, "star_gold").rectTransform.Stretch(); else UIKit.Txt(r, "t", "★", 28, UIKit.Hex("ffb81f")).rectTransform.Stretch();
            sfx.Play("coin", 1.2f);
            await Fx.Tween(420, p =>
            {
                if (!r) return;
                float e = p * p * (3 - 2 * p);
                r.anchoredPosition = Vector2.Lerp(from, to, e) + new Vector2(0, Mathf.Sin(p * Mathf.PI) * 60);
                r.localScale = Vector3.one * (1 + 0.4f * Mathf.Sin(p * Mathf.PI));
                r.localRotation = Quaternion.Euler(0, 0, p * 360);
            });
            if (r) Destroy(r.gameObject);
        }

        void AreaComplete(int a)
        {
            var A = HOME.areas[a];
            var (ac, acr, aso) = SafeGift(A.coins, A.crane, A.sort);   // 道具还没解锁就换成金币
            save.areasDone.Add(A.id); save.coins += ac; save.crane += acr; save.sort += aso; save.Persist();
            Analytics.Track("home_area", "area", a + 1, "lv", save.top);
            sfx.Play("fanfare"); sfx.Play("cheer"); Confetti(); Haptics.Play(Haptics.Kind.Success);
            Banner(T("区域完工！"), "done");
            Modal(false);
            ModalTitle(F("{0} 完工！", T(A.name)), "done");
            MText(T(A.name), 22, UIKit.Brown);
            var fr = UIKit.Rect("pic", boxContent); fr.gameObject.AddComponent<LayoutElement>().preferredHeight = 110;
            var im = DecoImg(fr, "card:chest", new Vector2(0.5f, 0.5f), Vector2.zero, 120);
            Fx.Run(() => Fx.Tween(900, p => { if (im) im.rectTransform.localScale = Vector3.one * (1 + 0.08f * Mathf.Sin(p * Mathf.PI * 2)); }));
            MText(T("完工奖励"), 16, UIKit.Hex("7a5a3a"));
            var al = new List<string> { F("金币 <b>+{0}</b>", ac) };
            if (acr > 0) al.Add(F("直升机 <b>+{0}</b>", acr));
            if (aso > 0) al.Add(F("喇叭 <b>+{0}</b>", aso));
            MText(string.Join("　", al), 17, UIKit.Hex("2f6b2a"));
            bool more = a + 1 < HOME.areas.Length;
            MText(more ? F("新的区域解锁了：<b>{0}</b>", T(HOME.areas[a + 1].name)) : T("整座潮汐岛都重建好了！岛民们谢谢你！"), 15, UIKit.Hex("b5371f"));
            MButton(T("太好了"), "3fb950", () =>
            {
                sfx.Play("click"); HideModal();
                homeStarT.text = save.star.ToString(); homeCoinT.text = save.coins.ToString(); Bump(homeCoinPill);
                if (more) homeZT = HOME.areas[a + 1].z;
                HomeRefresh();
            });
            FinishModal();
        }

        // ---------------------------------------------------------------- 宝箱、每日礼物、图鉴
        void OpenChest()
        {
            int ready = ChestReady;
            if (ready <= 0) { Toast(F("再拿 {0} 颗星就能开宝箱", CHEST_EVERY - save.starGot % CHEST_EVERY)); Bump(homeChest); return; }
            int n = save.chests++;
            // 每个宝箱：金币 + 一个道具（直升机、喇叭轮流）；每第 3 个是大宝箱，两样都给、金币翻倍
            bool big = n % 3 == 2; int coins = big ? 150 : 70, cr = big || n % 2 == 0 ? 1 : 0, so = big || n % 2 == 1 ? 1 : 0;
            (coins, cr, so) = SafeGift(coins, cr, so);   // 道具还没解锁就换成金币
            save.coins += coins; save.crane += cr; save.sort += so; save.Persist();
            Analytics.Track("chest_open", "n", n + 1, "big", big);
            sfx.Play("reveal"); sfx.Play("fanfare", 1, 0.6f); Haptics.Play(Haptics.Kind.Success);
            Modal();
            ModalTitle(T(big ? "大宝箱！" : "星星宝箱"), big ? "bigchest" : "chest");
            var fr = UIKit.Rect("pic", boxContent); fr.gameObject.AddComponent<LayoutElement>().preferredHeight = 120;
            var im = DecoImg(fr, "card:chest", new Vector2(0.5f, 0.5f), Vector2.zero, 128);
            Fx.Run(() => Fx.Tween(500, p => { if (im) im.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1, Fx.BackOut(p)); }));
            var l = new List<string> { F("金币 <b>+{0}</b>", coins) };
            if (cr > 0) l.Add(F("直升机 <b>+{0}</b>", cr));
            if (so > 0) l.Add(F("喇叭 <b>+{0}</b>", so));
            MText(string.Join("　", l), 18, UIKit.Hex("2f6b2a"));
            MText(T("每拿 10 颗星开一个，第 3 个是大宝箱"), 13, UIKit.Hex("7a5a3a"));
            MButton(T("收下"), "3fb950", () => { sfx.Play("coin"); HideModal(); if (homeOpen) HomeRefresh(); });
            FinishModal();
        }

        void ClaimPerk(Spot s, string pk)
        {
            if (!PerkReady(pk)) return;
            if (pk == "heli") { save.heliDay = SaveData.Day; save.crane++; } else { save.hornDay = SaveData.Day; save.sort++; }
            save.Persist();
            Analytics.Track("perk_claim", "id", pk);
            sfx.Play("coin"); Haptics.Play(Haptics.Kind.Light); Burst(s.at + Vector3.up * 1.5f, UIKit.Hex("ffe27a"), 12, 1);
            Toast(pk == "heli" ? T("直升机 +1（明天再来领）") : T("喇叭 +1（明天再来领）"));
            HomeRefresh();
        }

        void ShowAlbum()
        {
            Analytics.Track("screen", "name", "album");
            int total = Mathf.Min(COLLECT.Length, LEVELS.Length);
            int gotN = Enumerable.Range(0, total).Count(k => save.Got(k));
            Modal();
            ModalTitle(T("岛民图鉴"), "album");
            MText(F("救下来的岛民 <b>{0}</b> / {1}", gotN, total), 16);
            MText(T("每关第一次通关，会有一位新岛民上岛"), 13, UIKit.Hex("7a5a3a"));
            var names = Enumerable.Range(0, total).Where(k => save.Got(k)).Select(k => T(COLLECT[k])).ToList();
            if (names.Count > 0) MText(string.Join(En ? ", " : "、", names), 14, UIKit.Hex("2f6b2a"));
            if (gotN < total) MText(F("还有 <b>{0}</b> 位岛民在等你去救", total - gotN), 13, UIKit.Hex("b5371f"));
            MButton(T("关闭"), "3fb950", () => { sfx.Play("click"); HideModal(); }, true, "ffffff", 17, 46);
            FinishModal();
        }
    }
}
