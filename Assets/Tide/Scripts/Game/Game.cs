// 游戏主体：boat.html 表现层的翻译。规则全在 Rules.cs，这里只管"看起来怎么样"。
// 坐标：布局计算沿用网页版的坐标（x 向右，z 朝屏幕下方），摆到 Unity 里时用 W(x, z) 换成 (x, y, -z)，所以屏幕上方是 Unity 的 +Z。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using static Tide.Loc;

namespace Tide
{
    public partial class Game : MonoBehaviour
    {
        public static Game I;
        // ---- 常量（和网页版一致）----
        const float BW = 1.1f, DEEP = 2.3f;
        // 10-28 用户草图：画面分成固定的几块（顶栏 / 排队区 15.6~37% / 码头 / 港湾 49~89.6% / 道具），每关都一样大，不再按网格大小取景。
        // 港湾固定 REG_W × REG_H 米，网格按关卡缩放（lay.gs）塞进去；排队区比原来深一倍多（道距 1.0 → 2.3），人、栏杆跟着放大 QS 倍
        const float REG_W = 8f, REG_H = 10.04f, GS_MAX = 1.1f, QS = 1.6f, HSC = 1.3f;   // HSC：出人小屋放大倍数
        // 10-28 用户：队伍里能看到 50 人左右。房子右移、左栏杆外扩，通道总长 ≈ 27.3，人距 0.55 → 50 人；
        // 人 1.4 倍（新模型头比老的大 15%，看上去和上一版差不多大），人挨人站、帽檐之间留一点缝
        const float QSP = 0.55f, QLG = 2.3f, GS = 1.4f;   // QLG：排队通道两道之间的距离（0.52 用户嫌挤→0.75→1.0→10-28 草图 2.3）
        // 镜头偏离正上方的角度（弧度）：0.6 ≈ 俯角 56°，能看到船身侧面（奶油色腰带、舷窗），像标题界面那艘船；原来 0.4（67°）几乎是正俯视
        const float PHI = 0.6f;
        const int MAXB = 7, NAD = 2;
        static readonly string[] COLS = { "ff5548", "3d8bff", "ffc530", "3dbd58", "9a68ff", "ff7ac0", "22c7c7", "ff9a3c" };
        const string MYS = "b4b9c4";
        static readonly Dictionary<string, int> COST = new Dictionary<string, int> { { "crane", 60 }, { "sort", 40 }, { "berth", 80 }, { "cont", 60 } };
        static float BX(int k) => (k - (MAXB - 1) / 2f) * BW;
        static float BSC(int len) => Mathf.Min(0.86f, 2.2f / (len - 0.16f));
        static Vector3 W(double x, double z, float y = 0) => new Vector3((float)x, y, -(float)z);
        Vector3 G(double x, double z, float y = 0) => W(x * lay.gs, z * lay.gs, y);   // 网格坐标（Rules.CellXZ 那套）→ 世界：网格中心在原点，按 gs 缩放
        static float YawOf(Vector3 d) => Mathf.Atan2(d.x, d.z);     // Unity 里的朝向角（弧度），0 = +Z

        // 每种船：箭头 [尾 z, 头 z, 箭头半宽, 杆半宽, 箭头长, 高度]，模型名
        static readonly Dictionary<int, float[]> ARW = new Dictionary<int, float[]> {
            { 1, new[] { -0.14f, 0.32f, 0.17f, 0.052f, 0.26f, 0.2f } }, { 2, new[] { -0.3f, 0.62f, 0.18f, 0.055f, 0.3f, 0.43f } },
            { 3, new[] { -0.3f, 1.2f, 0.18f, 0.055f, 0.34f, 0.34f } }, { 4, new[] { -1.3f, 1.55f, 0.2f, 0.062f, 0.4f, 0.315f } } };
        const float NUM_GAP = 0.17f;    // 人数数字离箭头尾巴多远（米）；竹筏箭头尾巴从 -0.3 缩到 -0.14 给数字腾地方（10-26 用户：数字太大、竹筏上超出去了）
        static readonly Dictionary<int, string> KIND =new Dictionary<int, string> { { 1, "boat_raft" }, { 2, "boat_speed" }, { 3, "boat_yacht" }, { 4, "boat_carrier" } };

        LevelEntry[] LEVELS;
        Lot L;
        State st;
        const bool SHARKS = false, HOUSES = false;   // 鲨鱼、船坞开关（10-30 暂时不用）
        const float HEAD_S = 0.12f, HEAD_WAIT = 350;   // 离队头多近才出发（米）、最多等多久（毫秒）
        int aRuns; float aMax;   // 自动测试看：A 兜底用了几次、最远从多远跑过来
        int boardQ;   // 排着等上船的人数（BoardFx 还没开始的）
        int levelIdx, bumpTold, combo, chain, lastEarn, tok, pipeN, vq, hintB = -1;
        bool demo, intro, craneMode, usedRevive, usedBooster, warned1;
        float idleT, lastBoard, lastDepart;
        SaveData save;
        Synth sfx;
        Camera cam;
        Light sun, fill;
        Transform world;
        float camDist = 30, camZ = 0, shake = 0;
        Task pipe = Task.CompletedTask;

        class Lay
        {
            public float x0, x1, z0, z1, zCh, zP, zQ1, zQ2, xQ, xl, xr, zb, gs = 1; public int VIS;   // x0..z1：网格缩放以后在世界里的范围；gs：网格缩放
            public float xT, hx, hz, qLen; public Vector2 door; public List<Vector2> path = new List<Vector2>();   // 排队通道：拐弯处 x、小屋中心、路径（队头 → 门口）
        }
        Lay lay = new Lay();

        class BoatV
        {
            public GameObject g; public int b; public string state = "lot"; public bool bob = true, bumping;
            public float ph, yaw; public List<Material> hull = new List<Material>(), deck = new List<Material>();
            public Color hullC, deckC; public List<Guy> riders = new List<Guy>(); public List<Task> dash = new List<Task>();
            public Task arrive; public GameObject[] arrows; public Transform[] seats; public List<GameObject> seatM = new List<GameObject>();
            public Label q; public Collider col; public List<MeshRenderer> ol; public List<Guy> dashG = new List<Guy>();
        }
        class Guy { public GameObject g; public Transform body; public int c, slot, v; public float ph, walk, yaw, s; public MeshRenderer r, ol; public string dbg = "";
            public byte x; public bool vault, hi; public GameObject pole, gold; }   // x：功能（Rules.POLE…）；vault：被撑杆跳叫到了，原地等着起跳（Pole.cs）
        class Gate { public TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>(); public Task p => tcs.Task; public int owner = -1; public string stage = ""; }   // owner/stage：排查卡死用

        BoatV[] boatV = new BoatV[0];
        List<Guy> qGuys = new List<Guy>();
        GameObject[] lockV = new GameObject[MAXB];
        List<(GameObject g, Label n)> houseV = new List<(GameObject, Label)>();
        Gate[] gates = new Gate[MAXB];
        readonly List<Part> parts = new List<Part>();
        class Part { public Transform m; public Vector3 v; public float life, g; }
        static Mesh cubeMesh;

        // ======================================================================
        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            Input.multiTouchEnabled = false;
            gameObject.AddComponent<Fx>();
            sfx = gameObject.AddComponent<Synth>();
            save = SaveData.Load();
            var forceLang = AutoTest.Arg("-tideLang"); if (forceLang != null) save.lang = forceLang;   // 测试时指定语言
            Loc.Set(save.lang);
            Analytics.Init();
            UIKit.Clicked = () => Haptics.Play(Haptics.Kind.Light);
            var ta = Resources.Load<TextAsset>("levels");
            LEVELS = JsonUtility.FromJson<LevelList>(ta.text).levels;
            HomeInit();
            Art.Init();
            BuildStage();
            BuildUI();
        }

        void Start()
        {
            ApplySettings();
            save.sessions++; save.Persist();
            Analytics.Track("session_start", "n", save.sessions, "day", (int)(SaveData.Today - save.installDay), "lang", En ? "en" : "zh", "top", save.top);
            // 先盖上开始界面，再把要打的那关摆在后面
            ShowTitle();
            LoadLevel(CurLevel);
            InvokeRepeating(nameof(TutorTick), 0.4f, 0.4f);
            if (AutoTest.Wanted) gameObject.AddComponent<AutoTest>();
        }

        // ---- 舞台：镜头、灯、海 ----
        void BuildStage()
        {
            var cg = new GameObject("Camera");
            cam = cg.AddComponent<Camera>();
            cam.fieldOfView = 30; cam.nearClipPlane = 0.3f; cam.farClipPlane = 400;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UIKit.Hex("9fe0f0");
            cg.tag = "MainCamera";
            cg.AddComponent<AudioListener>();
            var sg = new GameObject("Sun");
            sun = sg.AddComponent<Light>();
            sun.type = LightType.Directional; sun.color = UIKit.Hex("fff0cf"); sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.45f;
            // 补光：从另一边打一点冷色的光，背光面不会死黑（照 Blender 预览的打光，标题界面那艘船就是这么渲染的）
            var fg = new GameObject("Fill"); fill = fg.AddComponent<Light>();
            fill.type = LightType.Directional; fill.color = UIKit.Hex("cfe0ff"); fill.intensity = 0.35f; fill.shadows = LightShadows.None;
            fg.transform.rotation = Quaternion.LookRotation(new Vector3(-7, -9, -5));
            sg.transform.rotation = Quaternion.LookRotation(new Vector3(6, -16, 6));
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = UIKit.Hex("dff4ff"); RenderSettings.ambientEquatorColor = UIKit.Hex("b5e2f0");
            RenderSettings.ambientGroundColor = UIKit.Hex("8fc7e0");
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (urp) { urp.shadowDistance = 90; urp.msaaSampleCount = 4; }
            // 海：中间浅、四周深的径向渐变
            var tex = new Texture2D(256, 256, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp };
            Color a = UIKit.Hex("62d0e6"), b = UIKit.Hex("3cb4e0"), c = UIKit.Hex("2a93d0");
            for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
                {
                    float d = Mathf.Min(1, Vector2.Distance(new Vector2(x, y), new Vector2(128, 128)) / 128f);
                    tex.SetPixel(x, y, d < 0.45f ? Color.Lerp(a, b, d / 0.45f) : Color.Lerp(b, c, (d - 0.45f) / 0.55f));
                }
            tex.Apply();
            var sea = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Destroy(sea.GetComponent<Collider>());
            sea.name = "Sea"; sea.transform.localScale = Vector3.one * 16; sea.transform.position = new Vector3(0, -0.03f, 0);
            var sm = new Material(Art.Unlit); sm.SetTexture("_BaseMap", tex); sm.SetColor("_BaseColor", Color.white);
            sea.GetComponent<MeshRenderer>().sharedMaterial = sm;
            seaT = sea.transform;
            cubeMesh = Art_Cube();
            StormStage();
        }
        Transform seaT;
        static Mesh Art_Cube() { var g = GameObject.CreatePrimitive(PrimitiveType.Cube); var m = g.GetComponent<MeshFilter>().sharedMesh; Destroy(g); return m; }

        // ======================================================================
        // 读关
        void LoadLevel(int i)
        {
            levelIdx = i; tok++; pipe = Task.CompletedTask; pipeN = 0; Fx.Clear();
            ClearUIFx();   // 动画都丢掉了，飘在屏幕上的彩纸、飞金币、提示字也得一起删，不然会定在画面上
            save.level = i; save.Persist();
            var lv = LEVELS[i];
            if (!HOUSES && lv.cfg.houses > 0) { var hc = CloneCfg(lv.cfg); hc.houses = 0; lv = new LevelEntry { cfg = hc, seed = lv.seed, p = lv.p }; }   // 10-30 用户：船坞先屏蔽。船坞生成时就占格子、带自己的船，只能出关前去掉（这几关的摆法会变）
            int tw = TideCount(lv.cfg);   // 港湾大到船要缩小的关拆成几波，涨潮一波一波送进来（TideWave.cs）
            L = tw > 0 ? TideLevel(lv, tw) : Rules.LoadLevel(lv);
            if (!SHARKS) { L.sharks.Clear(); if (L.waves != null) foreach (var w in L.waves) w.sharks.Clear(); }   // 10-30 用户：鲨鱼先屏蔽（关卡照常生成，只是不放鲨鱼，摆法不变）
            tideN = 0;
            RecolorForTry(i);
            if (i >= POLE_LV) Rules.MarkSpecial(L, Rules.POLE, new Rng(i * 7919 + save.Tries(i) * 131 + 17));   // 撑杆跳功能人，各种颜色随机（Pole.cs）；每次重玩换位置
            st = Rules.NewState(L);
            boosterUse[0] = boosterUse[1] = 0;
            vq = 0; boardQ = 0; usedRevive = usedBooster = false; craneMode = false; hintB = -1; combo = 0; chain = 0; spMade = 0; rainbowWait = 0; run = 0; runT = runBigT = -9999; warned1 = false; idleT = 0;
            for (int k = 0; k < MAXB; k++) gates[k] = new Gate { };
            for (int k = 0; k < MAXB; k++) gates[k].tcs.TrySetResult(true);
            ClearLabels();
            HideHand();
            LayoutLevel();
            BuildScene();
            boatV = new BoatV[L.boats.Count];
            qGuys.Clear();
            MechReset();
            for (int b = 0; b < L.boats.Count; b++) if (st.inl[b] != 0) { AddBoatView(b, true); AddMechView(b); }
            for (int k = 0; k < Mathf.Min(lay.VIS, st.q.Count); k++)
            {
                var g = MakeGuy(st.q[k], QX(k)); var p = QPos(k);
                g.g.transform.position = W(p.x, p.z, 0.25f); g.yaw = YawOf(new Vector3(p.fx, 0, -p.fz)); g.slot = k; g.s = SlotS(k);
                g.g.transform.localScale = Vector3.one * 0.001f; qGuys.Add(g);
            }
            HouseCount();
            StormReset();
            var cards = new List<string>();
            if (L.cfg.storm > 0) cards.Add("storm");
            if (L.boats.Any(B => B.len == 3)) cards.Add("yacht");
            if (L.boats.Any(B => B.len == 1)) cards.Add("raft");
            if (L.boats.Any(B => B.len == 4)) cards.Add("carrier");
            if (L.ins.Any(x => x == 2)) cards.Add("rock");
            if (L.houses.Count > 0) cards.Add("house");
            if (L.boats.Any(B => B.mys != 0)) cards.Add("mys");
            if (L.boats.Any(B => B.weed > 0)) cards.Add("weed");
            if (L.boats.Any(B => B.lockBy >= 0)) cards.Add("lock");
            if (L.sharks.Count > 0) cards.Add("shark");
            if (L.qx != null && L.qx.Contains(Rules.POLE)) cards.Add("pole");
            var fresh = OnScreen ? new List<string>() : cards.Where(k => !save.seen.Contains(k)).ToList();   // 开始界面/地图后面摆着的关不弹卡片
            if (!OnScreen)
            {
                levelT0 = Time.realtimeSinceStartup;
                Analytics.Track("level_start", "lv", i + 1, "try", save.AddTry(i)); save.Persist();
            }
            ui.tut.text = TUT.TryGetValue(i, out var tt) ? T(tt) : "";
            ui.tutBox.gameObject.SetActive(ui.tut.text.Length > 0);   // 没有教学字就别占地方
            HideModal(); UpdateHUD(); FitCam();
            Fx.Run(IntroFx);
            ui.lvName.color = L.cfg.tag == 2 ? UIKit.Hex("d5392a") : L.cfg.tag == 1 ? UIKit.Hex("7a3dd6") : UIKit.Ink;
            if (L.cfg.tag > 0 && !OnScreen)
            {
                int t0 = tok;   // 困难 / 超难关：开局提醒一下
                Fx.Run(async () => { await Fx.Sleep(450); if (t0 == tok) { Banner(T(L.cfg.tag == 2 ? "超难关！" : "困难关！"), L.cfg.tag == 2 ? "super" : "hard"); Haptics.Play(Haptics.Kind.Medium); } });
            }
            if (!OnScreen && L.waves != null && L.waves.Count > 0 && !save.seen.Contains("tide")) { save.seen.Add("tide"); save.Persist(); int t1 = tok, nw = L.waves.Count + 1; Fx.Run(async () => { await Fx.Sleep(1500); if (t1 == tok) Toast(string.Format(T("这关有 {0} 波船：港湾清空就会涨潮，送来下一波"), nw)); }); }
            if (!OnScreen) { GuideLevel(); StreakGift(); }   // 先 GuideLevel：它会清掉上一关剩下的步骤，连胜卡片要排在它后面 else { gCrane = gSort = 0; GuideClear(); }
            if (fresh.Count > 0)
            {
                string k = fresh[0]; save.seen.Add(k); save.Persist(); int t = tok;
                Fx.Run(async () => { await Fx.Sleep(700); if (t == tok && levelIdx == i) ShowCard(k); });
            }
        }

        // 网格（中心在原点）缩放到塞得进固定的港湾；小关最多放大到 GS_MAX
        static float GridScale(Lot L, out float x0, out float x1, out float z0, out float z1)
        {
            x0 = 1e9f; x1 = -1e9f; z0 = 1e9f; z1 = -1e9f; float e = L.rot != 0 ? 0.72f : 0.5f;
            for (int i = 0; i < L.ins.Length; i++)
                if (L.ins[i] != 0)
                {
                    Rules.CellXZ(L, i, out double x, out double z);
                    x0 = Mathf.Min(x0, (float)x - e); x1 = Mathf.Max(x1, (float)x + e); z0 = Mathf.Min(z0, (float)z - e); z1 = Mathf.Max(z1, (float)z + e);
                }
            x0 -= 0.2f; x1 += 0.2f; z0 -= 0.2f; z1 += 0.2f;
            return Mathf.Min(GS_MAX, (REG_W - 0.3f) / (x1 - x0), (REG_H - 0.3f) / (z1 - z0));
        }
        void LayoutLevel()
        {
            float gs = GridScale(L, out float x0, out float x1, out float z0, out float z1);
            lay = new Lay { gs = gs, x0 = x0 * gs, x1 = x1 * gs, z0 = z0 * gs, z1 = z1 * gs };
            // 下面这些都是固定的，和关卡无关（数值由草图比例反算：scratchpad lay2.js）
            lay.zP = -REG_H / 2 - 1.0f - DEEP;           // 码头前沿：港湾上沿往上 1 米航道 + 泊位
            lay.zCh = lay.zP + DEEP + 0.5f;             // 航道：泊位口外面
            lay.zQ1 = lay.zP - 0.6f - QLG / 2; lay.zQ2 = lay.zQ1 - 2 * QLG;   // zQ1 = 最靠码头那道（队头在它左端），zQ2 = 最里面那道
            lay.xQ = 5.3f - 0.3f * QS;                  // 左边栏杆在 x = -5.3（10-28 为了排 50 人从 -4.8 外扩，屏幕 ≈ 6%）
            lay.xl = lay.x0 - 0.7f * gs; lay.xr = lay.x1 + 0.7f * gs; lay.zb = lay.z1 + 0.7f * gs;
            // 景区那种来回折的通道（2026-10-22 用户画的草图）：第 1 道从队头往右 → 拐进第 2 道往左 → 拐进第 3 道往右 →
            // 直通右上角小屋的侧门。人从侧门一个接一个走出来，小屋前的牌子是屋里还剩多少人
            // （2026-10-22 门从小屋正面挪到左侧墙：原来第 3 道走到头还要拐到屋前，队尾的人站在屋外）
            lay.hx = 3.45f + HOUSE_HW * HSC; lay.hz = lay.zQ2;           // 小屋中心：侧门正对第 3 道；左墙在 x = 3.45（草图 72% → 10-28 右移到 ≈ 79%）
            lay.xT = 2.5f;                                               // 第 1、2 道右头拐弯处（10-28 从 1.89 右移；再往右连击牌就没地方放了）
            lay.door = new Vector2(lay.hx - HOUSE_HW * HSC - 0.12f * QS, lay.zQ2);  // 侧门门槛：队尾最后一个人站这儿
            float zB = lay.zQ1 - QLG;
            lay.path = new List<Vector2> { new Vector2(-lay.xQ, lay.zQ1), new Vector2(lay.xT, lay.zQ1), new Vector2(lay.xT, zB), new Vector2(-lay.xQ, zB),
                new Vector2(-lay.xQ, lay.zQ2), lay.door };
            lay.qLen = 0; for (int i = 1; i < lay.path.Count; i++) lay.qLen += Vector2.Distance(lay.path[i - 1], lay.path[i]);
            lay.VIS = Mathf.FloorToInt((lay.qLen - 0.15f) / QSP) + 1;
        }

        struct QP { public float x, z, fx, fz; }
        // 队伍里第 k 个人站的位置（沿通道从队头量 k 格），朝向队头那边（队头自己面朝屏幕）；超出通道的都站在门口
        QP QPos(int k)
        {
            float s = k * QSP;
            for (int i = 1; i < lay.path.Count; i++)
            {
                Vector2 a = lay.path[i - 1], b = lay.path[i]; float d = Vector2.Distance(a, b);
                if (s <= d || i == lay.path.Count - 1)
                {
                    var p = Vector2.Lerp(a, b, Mathf.Clamp01(s / d)); var f = k == 0 ? new Vector2(0, 1) : (a - b) / d;   // 队头面朝屏幕（向玩家挥手），其余朝着队头
                    return new QP { x = p.x, z = p.y, fx = f.x, fz = f.y };
                }
                s -= d;
            }
            return new QP { x = lay.door.x, z = lay.door.y, fx = -1, fz = 0 };
        }
        // 沿通道量 s 米处的点（10-28 用户：点得快的时候人直线奔向新位置，会穿过栏杆 → 一律沿通道走）
        // s 超过通道总长 = 还在侧门里面：门槛 → 屋里那一段是直线
        Vector2 Inside => new Vector2(lay.hx - 0.3f, lay.door.y);
        float PathEnd => lay.qLen + Vector2.Distance(lay.door, Inside);
        float SlotS(int k) => Mathf.Min(k * QSP, lay.qLen);   // 和 QPos 一样：排不下的都站在门口
        Vector2 PathAt(float s)
        {
            for (int i = 1; i < lay.path.Count; i++)
            {
                Vector2 a = lay.path[i - 1], b = lay.path[i]; float d = Vector2.Distance(a, b);
                if (s <= d) return Vector2.Lerp(a, b, Mathf.Max(0, s) / d);
                s -= d;
            }
            return Vector2.MoveTowards(lay.door, Inside, s);
        }
        Vector3 BerthPos(int k, int len) => W(BX(k), lay.zP + 0.1f + (len - 0.16f) * BSC(len) / 2);

        GameObject Add(string model, Vector3 pos, float yawDeg, bool boatFrame, float scale = 1)
        {
            var g = Art.Spawn(model, world, boatFrame);
            g.transform.position = pos; g.transform.rotation = Quaternion.Euler(0, yawDeg, 0); g.transform.localScale = Vector3.one * scale;
            Art.AddOutline(g, Art.OutlineMat(Art.Ink, model == "env_harbor" ? 1.1f : 1.3f));
            return g;
        }

        void BuildScene()
        {
            if (world) Destroy(world.gameObject);
            world = new GameObject("World").transform;
            parts.Clear();
            // 港湾：浅一点的水，固定大小（10-28 草图：每关一样大，网格缩放后摆在正中）
            //（四周原来有一圈红白浮球和四个角的黄浮标，用户要画面干净，去掉了；存档"海上复杂版本"里还有）
            var lag = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(lag.GetComponent<Collider>()); lag.name = "Lagoon";
            lag.transform.SetParent(world); lag.transform.position = W(0, 0, 0.0f); lag.transform.rotation = Quaternion.Euler(90, 0, 0);
            lag.transform.localScale = new Vector3(REG_W, REG_H, 1);
            lag.GetComponent<MeshRenderer>().sharedMaterial = Art.UnlitMat(UIKit.Hex("8fe3ef", 0.55f), true);
            BuildDeco();
            // 背景（沙滩、草地、椰子树…），原点在栈桥前沿
            Add("env_harbor", W(0, lay.zP), 0, false);
            // 栈桥：7 个泊位 + 右边收尾 + 两头接到岸上的木板
            for (int k = 0; k < MAXB; k++) Add("dock_berth", W(BX(k), lay.zP), 0, false);
            Add("dock_end", W(BX(MAXB - 1) + BW / 2, lay.zP), 0, false);
            foreach (int sx in new[] { -1, 1 }) Add("dock_walk", W(sx * (BX(MAXB - 1) + BW), lay.zP), 0, false);   // 两头只是栈道：以前用泊位模型，看起来像一个永远空着的泊位
            QueueScene();   // 排队通道的栏杆 + 出人的小屋（QueueHouse.cs），原来是一排小圆点铺的路
            HeadInit();   // 队头的人举手挥 + 白描边（QueueHead.cs），原来这里是一面小红旗
            LiveScene();  // 椰子树、草屋、灌木、遮阳伞单独摆，连击全队跳时跟着摇、弹（Cheer.cs）
            // 锁着的泊位：📺 看广告 / 🪙 花金币
            for (int k = 0; k < MAXB; k++) lockV[k] = null;
            for (int k = L.berths; k < MAXB; k++) lockV[k] = LockModel(k, k < L.berths + NAD);   // 开局几个泊位看关卡（前 10 关 3 个，之后 4 个），后面紧跟 2 个广告位，再往后金币
            StormScene();
        }

        // 礁石、船坞（单独挂在 deco 下面：涨潮分波每波换一套，TideWave.cs）
        Transform deco;
        void BuildDeco(bool tide = false)   // tide：涨潮换下一波，船坞牌子按已经出来的船算
        {
            foreach (var (g0, n0) in houseV) RemoveLabel(n0);
            houseV.Clear();
            if (deco) Destroy(deco.gameObject);
            deco = new GameObject("deco").transform; deco.SetParent(world, false);
            for (int i = 0; i < L.ins.Length; i++)
            {
                if (L.ins[i] != 2) continue;
                Rules.CellXZ(L, i, out double x, out double z);
                var r = new Rng(i + 3);
                Add(i % 2 == 0 ? "rock_a" : "rock_b", G(x, z), (float)r.Next() * 360, false, 0.95f * lay.gs).transform.SetParent(deco, true);
            }
            for (int h = 0; h < L.houses.Count; h++)
            {
                var H = L.houses[h];
                Rules.CellXZ(L, H.cell, out double x, out double z); Rules.DirXZ(L, H.d, out double dx, out double dz);
                var g = Add("boathouse", G(x, z), YawOf(W(dx, dz)) * Mathf.Rad2Deg, true, 1.05f * lay.gs); g.transform.SetParent(deco, true);
                int left = H.list.Count - (tide ? st.hq[h] : 0);
                var n = NewLabel(g.transform, new Vector3(0, 1.3f, 0), left.ToString(), UIKit.Cream, 20, 30); n.hide = left <= 0;
                houseV.Add((g, n));
            }
        }

        GameObject LockModel(int k, bool ad)
        {
            var g = new GameObject("lock" + k); g.transform.SetParent(world);
            g.transform.position = W(BX(k), lay.zP + DEEP / 2);
            var pl = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(pl.GetComponent<Collider>());
            pl.transform.SetParent(g.transform, false); pl.transform.localPosition = new Vector3(0, 0.1f, 0); pl.transform.localRotation = Quaternion.Euler(90, 0, 0);
            pl.transform.localScale = new Vector3(BW - 0.2f, DEEP - 0.1f, 1);
            pl.GetComponent<MeshRenderer>().sharedMaterial = Art.UnlitMat(UIKit.Hex("5d6b78", 0.45f), true);
            var bc = g.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.3f, 0); bc.size = new Vector3(BW - 0.1f, 0.6f, DEEP - 0.1f);
            // 只挂一把锁（用户要求：广告图标、金币价钱都不显示，点了再看是广告还是花金币）
            if (Skin.Has("icon_lock")) IconLabel(g.transform, new Vector3(0, 0.3f, 0), "icon_lock", 40);
            else NewLabel(g.transform, new Vector3(0, 0.3f, 0), T("解锁"), UIKit.Hex("281c10", 0.8f), 13, 0, Color.white);
            return g;
        }

        // ---- 船 ----
        void AddBoatView(int b, bool fresh)
        {
            var B = L.boats[b];
            var u = new BoatV { b = b, ph = UnityEngine.Random.value * 6.28f };
            var g = Art.Spawn(KIND[B.len], world, true);
            u.g = g;
            bool hidden = B.mys != 0 && st.seen[b] == 0;
            string hex = hidden ? MYS : COLS[B.c];
            foreach (var r in g.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                {
                    if (!ms[i]) continue;
                    if (ms[i].name.StartsWith("M_BoatHull")) { ms[i] = new Material(Art.Hull); u.hull.Add(ms[i]); }
                    else if (ms[i].name.StartsWith("M_BoatDeck")) { ms[i] = new Material(Art.Deck); u.deck.Add(ms[i]); }
                }
                r.sharedMaterials = ms;
            }
            u.ol = Art.AddOutline(g, Art.OutlineMat(Color.black));
            SetBoatColor(u, hex);
            // 箭头：一张贴图（见 Art.ArrowFor），永远画在船上面
            var a = ARW[B.len];
            var arw = Art.ArrowFor(a[0], a[1], a[2], a[3], a[4]);
            var ag = new GameObject("arrow"); ag.transform.SetParent(g.transform, false);
            ag.transform.localPosition = new Vector3(0, a[5], 0);
            ag.AddComponent<MeshFilter>().sharedMesh = arw.mesh;
            var ar = ag.AddComponent<MeshRenderer>(); ar.sharedMaterial = arw.mat; ar.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; ar.receiveShadows = false;
            u.arrows = new[] { ag };
            // 箭头尾巴后面标这条船坐几个人（10-26 用户要求）；挂在箭头下面，箭头藏了数字也跟着藏
            var na = new GameObject("num").transform; na.SetParent(ag.transform, false); na.localPosition = new Vector3(0, 0, a[0] - NUM_GAP);
            TextLabel(na, Vector3.zero, B.cap.ToString(), B.len == 1 ? 14 : 17);
            u.seats = Enumerable.Range(0, B.cap).Select(k => Art.Find(g.transform, "Seat_" + k)).ToArray();
            var col = g.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.3f, 0); col.size = new Vector3(0.86f, 0.6f, B.len - 0.12f);
            u.col = col;
            // 白船头上原来顶一个"?"圆牌；10-28 用户：不用打问号，白色就够了，第一次出现时弹卡片介绍（ShowCard "mys"）
            var h = BoatHome(b);
            g.transform.position = h.pos; u.yaw = h.yaw; g.transform.rotation = Quaternion.Euler(0, u.yaw * Mathf.Rad2Deg, 0);
            if (fresh) g.transform.localScale = Vector3.one * 0.001f;
            boatV[b] = u;
        }

        void SetBoatColor(BoatV u, string hex)
        {
            u.hullC = UIKit.Hex(hex); u.deckC = Color.Lerp(u.hullC, Color.white, 0.35f);
            foreach (var m in u.hull) m.SetColor("_BaseColor", Lit(u.hullC));
            foreach (var m in u.deck) m.SetColor("_BaseColor", Lit(u.deckC));
            if (u.ol != null) { var om = Art.OutlineMat(Art.OutlineColor(Lit(u.hullC))); foreach (var r in u.ol) r.sharedMaterial = om; }
        }
        static Color Lit(Color c) => Art.Lit(c);
        void ColorBoat(BoatV u, int c) { SetBoatColor(u, COLS[c]); if (u.q != null) { RemoveLabel(u.q); u.q = null; } }

        (Vector3 pos, float yaw) BoatHome(int b)
        {
            var B = L.boats[b]; double x = 0, z = 0;
            if (B.cells.Length == 0) return (W(-14, lay.zCh + 0.2f), YawOf(W(1, 0)));   // 送的彩虹船：不在港湾里，从左边海上开进来（Special.cs）
            foreach (int j in B.cells) { Rules.CellXZ(L, j, out double cx, out double cz); x += cx; z += cz; }
            Rules.DirXZ(L, B.d, out double dx, out double dz);
            return (G(x / B.cells.Length, z / B.cells.Length), YawOf(W(dx, dz)));
        }
        Vector3 DirU(int b) { Rules.DirXZ(L, L.boats[b].d, out double dx, out double dz); return W(dx, dz); }

        // ---- 小人 ----
        Guy MakeGuy(int c, byte x = 0)
        {
            // 头饰跟着颜色走（v = c），帽子和衣服是队伍色
            var u = new Guy { c = c, ph = UnityEngine.Random.value * 6.28f, v = c };
            u.g = new GameObject("guy"); u.g.transform.SetParent(world, false);
            var body = new GameObject("body"); body.transform.SetParent(u.g.transform, false);
            u.body = body.transform;
            body.AddComponent<MeshFilter>().sharedMesh = Art.Guy(u.v).mesh;
            u.r = body.AddComponent<MeshRenderer>();
            u.r.sharedMaterials = Art.GuyMats(u.v, COLS[c]);
            var ol = Art.AddOutline(body, GuyOutline(c)); u.ol = ol.Count > 0 ? ol[0] : null;
            if (x != 0) SetSpecial(u, x);
            return u;
        }
        static Material GuyOutline(int c) => Art.OutlineMat(Art.OutlineColor(Art.Lit(UIKit.Hex(COLS[c]))), 1.2f);
        void RecolorGuy(Guy g, int c)   // 换颜色 = 换人（头饰也跟着换）；队头那个换成没右手的模型，白描边一起换
        {
            g.c = c; g.v = c; bool head = g == headGuy;
            GuyMesh(g, (head ? Art.GuyW(c) : Art.Guy(c)).mesh);
            if (head) HeadRimMesh(g);
            g.r.sharedMaterials = Art.GuyMats(c, COLS[c]); if (g.ol) g.ol.sharedMaterial = GuyOutline(c);
            if (g.x != 0) SetSpecial(g, g.x);   // 杆子、旗子、金边跟着新模型、新颜色重做
        }

        // ======================================================================
        async Task IntroFx()
        {
            intro = true; int t = tok;
            var list = boatV.Where(x => x != null).OrderByDescending(x => x.g.transform.position.z).ToList();
            for (int k = 0; k < list.Count; k++)
            {
                var g = list[k].g; int kk = k;
                Fx.Run(async () => { await Fx.Sleep(kk * 14); if (t != tok) return; await Fx.Tween(260, p => g.transform.localScale = Vector3.one * Mathf.Max(0.001f, Fx.BackOut(p) * lay.gs)); });
            }
            for (int k = 0; k < qGuys.Count; k++)
            {
                var g = qGuys[k]; int kk = k;
                Fx.Run(async () => { await Fx.Sleep(120 + kk * 12); if (t != tok) return; await Fx.Tween(220, p => g.g.transform.localScale = Vector3.one * Mathf.Max(0.001f, Fx.BackOut(p)) * GS); });
            }
            await Fx.Sleep(Mathf.Min(900, list.Count * 14 + 300));
            if (t == tok) intro = false;
        }

        // ---------- 点船 ----------
        void OnBoat(int b)
        {
            idleT = 0;
            if (demo || intro || st.status != "play") return;
            HideHand();
            if (craneMode)
            {
                SetCrane(false); ClearToasts();     // 提示条正好挡在码头上，直升机要从那儿过
                UseItem("crane", () => { var ev0 = new List<Ev>(); Rules.Crane(L, st, b, ev0); usedBooster = true; Haptics.Play(Haptics.Kind.Medium); RunEvents(ev0); });
                return;
            }
            var ev = new List<Ev>(); var r = Rules.Tap(L, st, b, ev);
            if (r == "blocked") { sfx.Play("bad"); Haptics.Play(Haptics.Kind.Medium); if (++bumpTold <= 2 || levelIdx < 2) Toast(T("前面有船挡着，开不出去")); }
            else if (r == "full") { sfx.Play("bad"); Haptics.Play(Haptics.Kind.Heavy); Toast(T("码头停满了")); ShakeBoat(b, Vector3.right); }
            else if (r == "ok") { sfx.Play("go"); Haptics.Play(Haptics.Kind.Light); }
            else if (r == "weed" || r == "locked") MechRefuse(b, r);
            else if (r == "shark") { sfx.Play("bad"); if (tipShark++ < 2) Toast(T("鲨鱼挡在出口！等它游过去再开")); }
            RunEvents(ev);
        }

        // 泊位标记（排查"第一个泊位不停船"）：自动测试时写进日志
        void BLog(string s) { if (AutoTest.Wanted) Debug.Log("[Berth] " + Mathf.RoundToInt(Fx.Now) + " " + s + " | 规则泊位=" + string.Join(",", st.berth) + " 状态=" + st.status); }

        void QueueFx(Func<Task> fn)
        {
            int t = tok; pipeN++;
            var prev = pipe;
            pipe = Chain(prev, fn, t);
        }
        async Task Chain(Task prev, Func<Task> fn, int t)
        {
            try { await prev; } catch (Exception) { }
            try { if (t == tok) await fn(); } catch (Exception e) { Debug.LogException(e); }
            if (t == tok) pipeN--;
        }

        void RunEvents(List<Ev> ev)
        {
            int boarded = 0;
            foreach (var e in ev)
            {
                var E = e;
                if (e.t == "go") { var gate = gates[e.k].p; BLog("派船 船" + E.b + " → 泊位" + E.k + (E.how == "crane" ? "（直升机）" : "") + " 门" + (gate.IsCompleted ? "开" : "关(等船" + gates[E.k].owner + ")")); Fx.Run(() => SailFx(E.b, E.k, E.how, gate)); }
                else if (e.t == "spawn") Fx.Run(() => SpawnFx(E.b, E.h));
                else if (e.t == "reveal") Fx.Run(() => RevealFx(E.b));
                else if (e.t == "tidein") TideIn(E.b);   // 涨潮时在海上等着的船，路让出来了冲进港湾（TideWave.cs）
                else if (e.t == "bump") { Fx.Run(() => BumpFx(E.b, E.n, E.hit)); if (E.hit == -3) SharkSnap(E.k); }
                else if (e.t == "board") { boardQ++; boarded++; QueueFx(() => BoardFx(E)); }
                else if (e.t == "gather") GatherFx(E);   // 集合船靠岸：队伍重新上色（Special.cs）
                else if (e.t == "vault") QueueFx(() => VaultFx(E));   // 撑杆跳功能人上船了（Pole.cs）
                else if (e.t == "leave") { var d = new Gate { owner = e.b, stage = "queued" }; gates[e.k] = d; QueueFx(() => { Fx.Run(() => LeaveFx(E, d)); return Task.CompletedTask; }); }
            }
            StormBonus(boarded);   // 每上船一个人风暴推后 0.75 秒（Storm.cs）
            TideCheck();   // 港湾空了、后面还有波 → 涨潮（TideWave.cs）
            if (st.status != "play") { var s = st.status; QueueFx(() => EndFx(s)); }
            MechSync();
            UpdateHUD();
        }

        // 船开出港湾 → 绕到航道 → 等泊位空出来 → 倒进泊位
        async Task SailFx(int b, int k, string how, Task gate)
        {
            int t = tok; var u = boatV[b]; var g = u.g.transform; var B = L.boats[b];
            u.state = "go"; u.bob = false;
            foreach (var ar in u.arrows) ar.SetActive(false);    // 开走了箭头就没用了（而且箭头总在最上面，路过码头会盖住木板）
            var arrived = new TaskCompletionSource<bool>(); u.arrive = arrived.Task;
            if (how == "crane") await HeliFx(u, k, gate);    // 直升机吊过去（Heli.cs）
            else if (how == "sea")                          // 送的彩虹船：从海上沿航道开到泊位正下方，再倒进去
            {
                await Follow(u, new List<Vector3> { g.position, W(BX(k), lay.zCh + 0.2f) }, 26, false);
                if (t != tok) return; await gate; if (t != tok) return;
                await Follow(u, new List<Vector3> { g.position, BerthPos(k, B.len) }, 15, true);
            }
            else
            {
                var pts = Route(b, k); pts.RemoveAt(pts.Count - 1);
                await Follow(u, pts, 26, false, BSC(B.len));    // 网格缩小了的关：路上慢慢长到泊位的大小
                if (t != tok) return; await gate; if (t != tok) return;
                var bp = BerthPos(k, B.len);
                await Follow(u, new List<Vector3> { g.position, bp }, 15, true);
            }
            if (t != tok) return;
            float bs = BSC(B.len);
            u.yaw = 0; g.rotation = Quaternion.identity; g.position = new Vector3(g.position.x, 0, g.position.z); g.localScale = Vector3.one * bs;
            u.state = "berth"; sfx.Play("dock"); BLog("靠岸 船" + b + " 泊位" + k);
            // 座位圈
            foreach (var s in u.seats)
            {
                if (!s) { u.seatM.Add(null); continue; }
                var d = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(d.GetComponent<Collider>());
                d.transform.SetParent(g, false); d.transform.position = s.position + Vector3.up * 0.015f;
                d.transform.localScale = new Vector3(0.3f, 0.004f, 0.3f);
                d.GetComponent<MeshRenderer>().sharedMaterial = Art.UnlitMat(new Color(1, 1, 1, 0.7f), true);
                u.seatM.Add(d);
            }
            foreach (var a in u.arrows) a.SetActive(false);
            Fx.Run(() => Fx.Tween(200, p => g.localScale = Vector3.one * bs * (1 + 0.08f * Mathf.Sin(p * Mathf.PI))));
            arrived.TrySetResult(true);
        }

        // 出港路线：先顺着船头开出港湾，再沿着港湾外圈绕到上面的航道，到泊位正下方（网页坐标算，最后换成 Unity）
        List<Vector3> Route(int b, int k)
        {
            var g = boatV[b].g.transform; var B = L.boats[b];
            Rules.DirXZ(L, B.d, out double ddx, out double ddz); float dx = (float)ddx, dz = (float)ddz;
            float px = g.position.x, pz = -g.position.z;
            float te = 1e9f;
            if (dx > 0.01f) te = Mathf.Min(te, (lay.x1 - px) / dx); if (dx < -0.01f) te = Mathf.Min(te, (lay.x0 - px) / dx);
            if (dz > 0.01f) te = Mathf.Min(te, (lay.z1 - pz) / dz); if (dz < -0.01f) te = Mathf.Min(te, (lay.z0 - pz) / dz);
            float tt = te + 0.55f * lay.gs, Ex = px + dx * tt, Ez = pz + dz * tt, tx = BX(k);
            var pts = new List<Vector3> { W(px, pz), W(Ex, Ez) };
            void V(float x, float z) => pts.Add(W(x, z));
            string side = Ez < lay.z0 ? "top" : Ex < lay.x0 ? "left" : Ex > lay.x1 ? "right" : "bottom";
            if (side == "top") V(Ex, lay.zCh);
            else if (side == "left") { V(lay.xl, Ez); V(lay.xl, lay.zCh); }
            else if (side == "right") { V(lay.xr, Ez); V(lay.xr, lay.zCh); }
            else
            {
                float sx = Mathf.Abs(lay.xl - tx) + Mathf.Abs(lay.xl - Ex) < Mathf.Abs(lay.xr - tx) + Mathf.Abs(lay.xr - Ex) ? lay.xl : lay.xr;
                V(Ex, lay.zb); V(sx, lay.zb); V(sx, lay.zCh);
            }
            V(tx, lay.zCh + 0.15f); V(tx, lay.zCh + 0.15f);
            return pts;
        }

        // 沿折线走：位置在线上，朝向看前方 0.7 处（拐角自然变圆）
        Task Follow(BoatV u, List<Vector3> pts, float speed, bool rev, float scaleTo = -1)
        {
            var g = u.g.transform; int t = tok; float sc0 = g.localScale.x;
            var segs = new List<float>(); float tot = 0;
            for (int k = 1; k < pts.Count; k++) { float l = Vector3.Distance(pts[k], pts[k - 1]); segs.Add(l); tot += l; }
            if (tot < 0.01f) return Task.CompletedTask;
            Vector3 At(float s)
            {
                s = Mathf.Clamp(s, 0, tot); int k = 0;
                while (k < segs.Count - 1 && s > segs[k]) { s -= segs[k]; k++; }
                return Vector3.Lerp(pts[k], pts[k + 1], segs[k] > 0 ? s / segs[k] : 1);
            }
            float dur = tot / speed * 1000 + 80, lastW = 0;
            return Fx.Tween(dur, p =>
            {
                if (t != tok) return;
                const float A = 0.15f;
                float e = rev ? 1 - Mathf.Pow(1 - p, 2) : (p < A ? p * p / (2 * A) : p - A / 2) / (1 - A / 2);
                float s = e * tot; var pos = At(s);
                g.position = new Vector3(pos.x, g.position.y, pos.z);
                if (!rev) { var ah = At(s + 0.7f); if ((ah - pos).sqrMagnitude > 1e-4f) u.yaw = Fx.LerpAng(u.yaw, YawOf(ah - pos), 0.35f); }
                else u.yaw = Fx.LerpAng(u.yaw, 0, 0.25f);
                g.rotation = Quaternion.Euler(0, u.yaw * Mathf.Rad2Deg, 0);
                if (scaleTo > 0) g.localScale = Vector3.one * Mathf.Lerp(sc0, scaleTo, e * e);   // 先慢后快：还在网格里的时候别长大，免得蹭到旁边的船
                if (Fx.Now - lastW > 45) { lastW = Fx.Now; Wake(pos); }
            });
        }

        // 被挡：往前冲到挡路的船跟前，撞一下，弹回来
        async Task BumpFx(int b, int n, int hit)
        {
            var u = boatV[b]; if (u == null || u.bumping) return; u.bumping = true;
            var B = L.boats[b]; var d = DirU(b); var h = BoatHome(b); float dist = (n + 0.16f) * lay.gs; var g = u.g.transform;
            await Fx.Tween(70 + n * 40, p => { float e = p * p; g.position = new Vector3(h.pos.x + d.x * dist * e, g.position.y, h.pos.z + d.z * dist * e); });
            if (hit >= 0 && boatV[hit] != null) { FlashBoat(boatV[hit]); ShakeBoat(hit, d); }
            FlashBoat(u);
            Burst(h.pos + d * (dist + (B.len - 0.16f) / 2 * lay.gs) + Vector3.up * 0.5f, Color.white, 6, 0.6f);
            await Fx.Tween(200, p => { float e = 1 - p; g.position = new Vector3(h.pos.x + d.x * dist * e, g.position.y, h.pos.z + d.z * dist * e); });
            g.position = new Vector3(h.pos.x, g.position.y, h.pos.z); u.bumping = false;
        }
        void FlashBoat(BoatV u)
        {
            var red = UIKit.Hex("ff2a1a");
            Fx.Run(async () =>
            {
                await Fx.Tween(380, p => { foreach (var m in u.hull) m.SetColor("_BaseColor", Lit(Color.Lerp(red, u.hullC, p))); });
                foreach (var m in u.hull) m.SetColor("_BaseColor", Lit(u.hullC));
            });
        }
        void ShakeBoat(int b, Vector3 dd)
        {
            var u = boatV[b]; if (u == null || u.state != "lot") return; var h = BoatHome(b); var g = u.g.transform;
            Fx.Run(async () =>
            {
                await Fx.Tween(260, p => { float s = Mathf.Sin(p * Mathf.PI * 4) * (1 - p) * 0.1f; g.position = new Vector3(h.pos.x + dd.x * s, g.position.y, h.pos.z + dd.z * s); });
                if (u.state == "lot") g.position = new Vector3(h.pos.x, g.position.y, h.pos.z);
            });
        }

        async Task SpawnFx(int b, int h)
        {
            // 画面上的船马上建好（藏在船坞门口），免得手快的时候规则层已经能点它、画面上却还没有这艘船
            int t = tok; AddBoatView(b, false); var u = boatV[b]; var H = L.houses[h];
            Rules.CellXZ(L, H.cell, out double hx, out double hz); var p0 = G(hx, hz); var hm = BoatHome(b); float gs = lay.gs;
            var g = u.g.transform; g.localScale = Vector3.one * 0.3f * gs; g.position = p0;
            await Fx.Sleep(260); if (t != tok || u.state != "lot") return;
            await Fx.Tween(320, p => { if (u.state != "lot") return; g.position = Vector3.Lerp(p0, hm.pos, p); g.localScale = Vector3.one * (0.3f + 0.7f * p) * gs; });
            int left = H.list.Count - st.hq[h];
            houseV[h].n.text.text = left.ToString(); houseV[h].n.hide = left <= 0;
            sfx.Play("pop2");
        }

        async Task RevealFx(int b)
        {
            int t = tok; await Fx.Sleep(200); if (t != tok || boatV[b] == null) return;
            var u = boatV[b]; ColorBoat(u, L.boats[b].c); sfx.Play("reveal");
            Burst(u.g.transform.position + Vector3.up * 0.8f, UIKit.Hex(COLS[L.boats[b].c]), 12, 1);
            float s0 = u.state == "berth" ? BSC(L.boats[b].len) : u.state == "lot" ? lay.gs : u.g.transform.localScale.x;
            Fx.Run(() => Fx.Tween(300, p => u.g.transform.localScale = Vector3.one * s0 * (1 + 0.15f * Mathf.Sin(p * Mathf.PI))));
        }

        // 队头的人跑上船。pipeline 里一个接一个排，彼此只隔 55ms，跑的动画并行
        async Task BoardFx(Ev e)
        {
            var u = boatV[e.b]; if (u.arrive != null) await u.arrive;
            if (qGuys.Count == 0) { boardQ = Mathf.Max(0, boardQ - 1); return; }
            // 10-29 用户：上船太快时，还在后面通道里的人直接飞过栏杆上船 →
            // B：只有走到队头的人才出发（等的时候整队按 boardQ 加速往前滚）；A：等太久还没到，就沿通道跑到队头再出去（见 Dash）
            var guy = qGuys[0]; int t0 = tok;
            if (!guy.vault) for (float w0 = Fx.Now; guy.s > HEAD_S && Fx.Now - w0 < HEAD_WAIT && qGuys.Count > 0 && qGuys[0] == guy; ) { await Fx.Sleep(16); if (t0 != tok) return; }
            boardQ = Mathf.Max(0, boardQ - 1);
            if (qGuys.Count == 0) return;
            guy = qGuys[0]; qGuys.RemoveAt(0); vq++; RefillQueue();
            var B = L.boats[e.b]; var seat = e.seat < u.seats.Length ? u.seats[e.seat] : null;
            var from = guy.g.transform.position; var pier = W(BX(e.k), lay.zP - 0.25f, 0.3f);
            combo = Fx.Now - lastBoard < 900 ? combo + 1 : 0; lastBoard = Fx.Now;
            RunHit(); BoardSound();   // 连击按上船人数算（Special.cs）；上船音按连击往上爬音阶（Fever.cs）
            var gt = guy.g.transform;
            var dash = guy.vault || guy.x == Rules.POLE ? VaultJump(guy, u, seat, e) : Dash();   // 功能人自己、被他叫到的人：撑杆跳上船
            async Task Dash()
            {
                if (guy.s > 0.02f)   // A 兜底：还没到队头，沿着通道跑过去（不抄近路穿栏杆）
                {
                    aRuns++; aMax = Mathf.Max(aMax, guy.s);
                    float s0 = guy.s, dur = Mathf.Max(60, s0 / 28f * 1000) * BoardSpeed;
                    await Fx.Tween(dur, p =>
                    {
                        var q = PathAt(Mathf.Lerp(s0, 0, p)); var np = W(q.x, q.y, 0.25f); var dv = np - gt.position;
                        if (dv.sqrMagnitude > 1e-6f) gt.rotation = Quaternion.Euler(0, YawOf(dv) * Mathf.Rad2Deg, 0);
                        gt.position = np; guy.body.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(p * dur / 40)) * 0.09f, 0);
                    });
                    guy.s = 0; from = gt.position;
                }
                var gate = W(-lay.xQ + 0.05f, lay.zQ1 + QLG / 2 + 0.2f, 0.25f);   // 先从栏杆左前角的上船口出来，别跨过红绳
                if (from.z > gate.z + 0.1f)
                {
                    var f0 = from; guy.yaw = YawOf(gate - f0); gt.rotation = Quaternion.Euler(0, guy.yaw * Mathf.Rad2Deg, 0);
                    await Fx.Tween(80 * BoardSpeed, p => gt.position = Vector3.Lerp(f0, gate, p));
                    from = gate;
                }
                guy.yaw = YawOf(pier - from); gt.rotation = Quaternion.Euler(0, guy.yaw * Mathf.Rad2Deg, 0);
                float d1 = Vector3.Distance(from, pier);
                guy.dbg = "t1 d=" + d1.ToString("0.0") + " @" + Fx.Now.ToString("0");
                await Fx.Tween(Mathf.Min(320, 120 + d1 * 28) * BoardSpeed, p => { var q = Vector3.Lerp(from, pier, p); q.y = 0.25f + Mathf.Sin(p * Mathf.PI) * 0.35f; gt.position = q; });
                Vector3 wp = seat ? seat.position : u.g.transform.position, p2 = gt.position;
                guy.dbg = "t2 @" + Fx.Now.ToString("0");
                await Fx.Tween(170 * BoardSpeed, p => { var q = Vector3.Lerp(p2, wp, p); q.y = p2.y + (wp.y - p2.y) * p + Mathf.Sin(p * Mathf.PI) * 0.45f; gt.position = q; });
                gt.SetParent(seat ? seat : u.g.transform, true);
                gt.position = wp + Vector3.down * 0.05f; gt.rotation = u.g.transform.rotation;
                gt.localScale = Vector3.one; gt.localScale = Vector3.one * (0.62f / Mathf.Max(0.01f, gt.lossyScale.x)) * 1f;
                u.riders.Add(guy); guy.dbg = "done";
                if (e.seat < u.seatM.Count && u.seatM[e.seat]) u.seatM[e.seat].SetActive(false);
            }
            u.dash.Add(dash); u.dashG.Add(guy); UpdateHUD();
            await Fx.Sleep(55 * BoardSpeed);
        }

        // 坐满：倒出泊位、开走，金币飞到右上角
        async Task LeaveFx(Ev e, Gate gate)
        {
            int t = tok; var u = boatV[e.b]; var g = u.g.transform;
            if (u.arrive != null) await u.arrive;   // 某色的人救完、船空着就开走时（彩虹船抢了人），船可能还在路上
            if (t != tok) return;
            gate.stage = "wait-dash " + u.dash.Count(x => !x.IsCompleted) + "/" + u.dash.Count + " riders=" + u.riders.Count + " boatState=" + u.state;
            await Task.WhenAll(u.dash.ToArray()); if (t != tok) return;
            gate.stage = "leaving";
            u.state = "leave"; sfx.Play("sail"); Haptics.Play(Haptics.Kind.Medium);
            chain = Fx.Now - lastDepart < 4000 ? chain + 1 : 1; lastDepart = Fx.Now; Praise(chain);
            TaskAdd("boat"); TaskAdd("chain", chain);
            foreach (var r in u.riders) { var rb = r.body; Fx.Run(() => Fx.Tween(400, p => rb.localPosition = new Vector3(0, Mathf.Sin(p * Mathf.PI) * 0.25f, 0))); }
            FloatText(g.position + Vector3.up * 1.2f, "+" + u.riders.Count);   // 船不一定坐满（某色的人救完了提前开走）
            var p0 = g.position; float outS = p0.x < 0 ? -1 : 1;
            await Fx.Tween(200, p => g.position = new Vector3(p0.x, p0.y, p0.z - p * 1.3f));
            gate.tcs.TrySetResult(true); BLog("离开 船" + e.b + " 泊位" + e.k + " 放行");
            var p1 = g.position;
            await Follow(u, new List<Vector3> { p1, W(p1.x + outS * 0.6f, lay.zCh + 0.2f), W(outS * 14, lay.zCh + 0.2f) }, 24, false);
            if (t != tok) return;
            Destroy(u.g);
        }

        void RefillQueue()
        {
            for (int k = 0; k < qGuys.Count; k++) qGuys[k].slot = k;
            int idx = vq + qGuys.Count;
            if (qGuys.Count < lay.VIS && idx < st.q.Count)
            {
                var g = MakeGuy(st.q[idx], QX(idx));
                g.g.transform.position = W(lay.hx - 0.3f, lay.door.y, 0.25f); g.g.transform.localScale = Vector3.one * GS; g.slot = qGuys.Count; g.s = PathEnd; qGuys.Add(g);   // 从屋里（侧门里面）走出来
                DoorOpen();
            }
            HouseCount();
        }

        async Task EndFx(string s)
        {
            int t = tok;
            await Task.WhenAll(boatV.Where(x => x != null && x.arrive != null).Select(x => x.arrive).ToArray()); if (t != tok) return;
            await Fx.Sleep(450); if (t != tok) return;
            if (s == "win") { await WinFx(); return; }
            if (st.status == "lose") await LoseFx();
        }

        // ======================================================================
        // 镜头
        void PlaceCam(bool raw)
        {
            float sx = raw ? 0 : (UnityEngine.Random.value - 0.5f) * shake, sy = raw ? 0 : (UnityEngine.Random.value - 0.5f) * shake;
            cam.transform.position = new Vector3(sx, camDist * Mathf.Cos(PHI) + sy, -(camZ + camDist * Mathf.Sin(PHI)));
            cam.transform.LookAt(new Vector3(sx, sy, -camZ));
        }

        // 取景：画面布局是固定的（10-28 草图），镜头也固定——排队区最上那道绳放在屏幕 15.6%，港湾底边放在 89.6%（从上往下量），
        // 每关都一样，和网格多大无关。顶栏 / 道具按钮（刘海屏的安全区）挤过来时整体往里缩
        void FitCam()
        {
            if (L == null) return;
            Canvas.ForceUpdateCanvases();
            float Wd = Screen.width, Hd = Screen.height; var c4 = new Vector3[4];
            ui.top.GetWorldCorners(c4); float hudBot = c4[0].y;      // 只看顶栏：教学字条出不出来都不能让画面变
            float yTop = Mathf.Min(Hd * (1 - 0.156f), hudBot - Hd * 0.06f), yBot = Mathf.Max(Hd * 0.104f, FreeRect().x);
            var pts = new[] { W(-REG_W / 2, REG_H / 2), W(REG_W / 2, REG_H / 2), W(-lay.xQ - 0.3f * QS, lay.zQ2 - QLG / 2), W(lay.hx, lay.zQ2 - QLG / 2) };
            float tw = Wd * 0.9f, th = yTop - yBot; var R = new Vector2(yBot, yTop);
            for (int it = 0; it < 40; it++)
            {
                PlaceCam(true);
                float x0 = 1e9f, x1 = -1e9f, y0 = 1e9f, y1 = -1e9f;
                foreach (var p in pts) { var v = cam.WorldToScreenPoint(p); x0 = Mathf.Min(x0, v.x); x1 = Mathf.Max(x1, v.x); y0 = Mathf.Min(y0, v.y); y1 = Mathf.Max(y1, v.y); }
                float s = Mathf.Min(tw / (x1 - x0), th / (y1 - y0)), dy = (R.x + R.y) / 2 - (y0 + y1) / 2;
                float a = cam.WorldToScreenPoint(W(0, camZ)).y, b2 = cam.WorldToScreenPoint(W(0, camZ + 1)).y, pz = Mathf.Max(1, a - b2);
                camDist /= Mathf.Pow(s, 0.85f); camZ += dy / pz * 0.8f;
                if (Mathf.Abs(s - 1) < 0.003f && Mathf.Abs(dy) < 0.6f) break;
            }
            seaT.position = new Vector3(0, -0.03f, -camZ);
            ComboPlace();
        }

        int lastW, lastH;
        void Update()
        {
            if (Screen.width != lastW || Screen.height != lastH) { lastW = Screen.width; lastH = Screen.height; ApplySafeArea(); FitCam(); }
            if (Input.GetKeyDown(KeyCode.Escape)) Back();
            float dt = Mathf.Min(0.05f, Fx.Dt), now = Fx.Now;
            for (int k = parts.Count - 1; k >= 0; k--)
            {
                var p = parts[k];
                if (!p.m) { parts.RemoveAt(k); continue; }
                p.v.y -= p.g * dt; p.m.position += p.v * dt; p.m.Rotate(dt * 460, 0, 0); p.life -= dt;
                p.m.localScale = Vector3.one * 0.11f * Mathf.Max(0.01f, p.life / 0.7f) * (p.g != 0 ? 1 : 1.6f);
                if (p.life <= 0) { Destroy(p.m.gameObject); parts.RemoveAt(k); }
            }
            shake *= 0.9f;
            if (homeOpen) HomeTick(dt); else PlaceCam(false);
            if (L != null)
            {
                if (wreck) WreckTick(now);   // 海啸过后一片狼藉：东西都在水里漂（Surge.cs）
                else {
                foreach (var u in boatV)
                {
                    if (u == null || !u.g) continue;
                    if (u.bob) { var p = u.g.transform.position; p.y = Mathf.Sin(now / 600 + u.ph) * 0.025f; u.g.transform.position = p; }
                }
                if (hintB >= 0 && boatV[hintB] != null && boatV[hintB].state == "lot") { var t = boatV[hintB].g.transform; var p = t.position; p.y = 0.08f + Mathf.Abs(Mathf.Sin(now / 180)) * 0.12f; t.position = p; }
                // 队伍：每个人沿着通道走到自己的位置（拐弯也跟着拐，不抄近路穿栏杆），走的时候一颠一颠
                foreach (var g in qGuys)
                {
                    if (g.vault) continue;   // 被撑杆跳叫到的人原地等着起跳
                    var p = QPos(g.slot); var tr = g.g.transform;
                    float ts = SlotS(g.slot), ds = g.s - ts;
                    if (Mathf.Abs(ds) > 0.005f)
                    {
                        g.s = Mathf.MoveTowards(g.s, ts, dt * Mathf.Min(45, Mathf.Max(5 + boardQ * 3, Mathf.Abs(ds) * (3 + boardQ))));   // 落后得多就走快点；排着好几个人等上船时整队像传送带一样往前滚（10-29）
                        var q = PathAt(g.s); var np = W(q.x, q.y, 0.25f); var dv = np - tr.position;
                        tr.position = np;
                        if (dv.sqrMagnitude > 1e-8f) g.yaw = Fx.LerpAng(g.yaw, Mathf.Atan2(dv.x, dv.z), 0.3f);
                        g.walk += dt * 16;
                        g.body.localPosition = new Vector3(0, Mathf.Max(Mathf.Abs(Mathf.Sin(g.walk)) * 0.09f, JumpY(g, now)), 0);
                    }
                    else
                    {
                        g.yaw = Fx.LerpAng(g.yaw, Mathf.Atan2(p.fx, -p.fz), 0.15f);
                        float panic = Mathf.InverseLerp(0.5f, 1f, stormK);     // 风暴越近越慌：蹦得越快越高
                        float cheer = Mathf.Clamp01(feverK);                    // 连击热起来：排队的人欢呼着蹦（Fever.cs）
                        float rate = Mathf.Lerp(Mathf.Lerp(300, 110, panic), 140, cheer), amp = Mathf.Max(Mathf.Lerp(0.03f, 0.12f, panic), 0.17f * cheer);
                        g.body.localPosition = new Vector3(0, Mathf.Max(Mathf.Max(0, Mathf.Sin(now / rate + g.ph)) * amp, JumpY(g, now)), 0);   // 连救 5 人以上：全体跳（Special.cs）
                    }
                    tr.rotation = Quaternion.Euler(0, g.yaw * Mathf.Rad2Deg, 0);
                    tr.position = new Vector3(tr.position.x, 0.25f, tr.position.z);
                }
                HeadTick(); HouseTick(dt); SpecialTick(); LiveTick(now); BlinkTick(now);
                }
                if (!intro && st.status == "play") TideCheck();   // 10-30：输了以后解锁泊位复活时港湾正好是空的，没有点船就没人叫涨潮 → 卡住；每帧看一眼兜底
                // 发呆 6 秒：最该点的那艘船跳一下
                if (st.status == "play" && !demo && !intro && !ModalOpen && !OnScreen && !GuideOn) { idleT += dt; if (idleT > 6 && hintB < 0) { idleT = 3; Nudge(); } }
                else idleT = 0;
                ui.vig.color = new Color(0.63f, 0.12f, 0.08f, sfx.tensT > 0 ? (0.35f + 0.25f * Mathf.Sin(now / 260)) * 0.55f : 0);
                StormTick(dt);
                FeverTick(dt);
                MechTick(dt);
            }
            HandleInput();
        }

        void LateUpdate() { UpdateLabels(); UpdateHand(); UpdateScreens(); GuideTick(); }

        void Nudge()
        {
            int b = Rules.BestMove(L, st, null, 0); if (b < 0) return; var u = boatV[b]; if (u == null || u.state != "lot") return;
            var g = u.g.transform; u.bob = false;
            Fx.Run(async () => { await Fx.Tween(560, p => { var q = g.position; q.y = Mathf.Abs(Mathf.Sin(p * Mathf.PI * 2)) * 0.3f; g.position = q; }); if (u.state == "lot") u.bob = true; });
        }

        // ---- 粒子 ----
        void Burst(Vector3 pos, Color c, int n, float spread)
        {
            for (int k = 0; k < n; k++)
            {
                var m = NewCube(c, pos);
                parts.Add(new Part { m = m, v = new Vector3((UnityEngine.Random.value - 0.5f) * 3 * spread, 2 + UnityEngine.Random.value * 2.6f, (UnityEngine.Random.value - 0.5f) * 3 * spread), life = 0.7f, g = 10 });
            }
        }
        void Wake(Vector3 p)
        {
            var m = NewCube(new Color(1, 1, 1, 0.85f), new Vector3(p.x + (UnityEngine.Random.value - 0.5f) * 0.3f, 0.12f, p.z + (UnityEngine.Random.value - 0.5f) * 0.3f));
            parts.Add(new Part { m = m, v = new Vector3((UnityEngine.Random.value - 0.5f) * 0.4f, 0.3f, (UnityEngine.Random.value - 0.5f) * 0.4f), life = 0.5f, g = 0 });
            FeverWake(p);
        }
        Transform NewCube(Color c, Vector3 pos)
        {
            var g = new GameObject("p"); g.transform.SetParent(world, false); g.transform.position = pos; g.transform.localScale = Vector3.one * 0.11f;
            g.AddComponent<MeshFilter>().sharedMesh = cubeMesh;
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = Art.UnlitMat(c, c.a < 1); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g.transform;
        }
        void Fireworks()
        {
            for (int k = 0; k < 9; k++)
            {
                int kk = k; int t = tok;
                Fx.Run(async () =>
                {
                    await Fx.Sleep(kk * 140); if (t != tok) return;
                    Burst(new Vector3((UnityEngine.Random.value - 0.5f) * 8, 2 + UnityEngine.Random.value * 3, -(camZ - 3 + UnityEngine.Random.value * 5)), UIKit.Hex(COLS[kk % 8]), 24, 2.4f);
                    sfx.Play("pop" + kk % 5);
                });
            }
        }

        // ---------- 输入 ----------
        void HandleInput()
        {
            if (AutoTest.Wanted) return;   // 自动测试只走代码里的 TapAt，不接真鼠标
            Vector2 pos; int id = -1;
            if (Input.touchCount > 0) { var tch = Input.GetTouch(0); if (tch.phase != TouchPhase.Began) return; pos = tch.position; id = tch.fingerId; }
            else if (Input.GetMouseButtonDown(0)) pos = Input.mousePosition;
            else return;
            if (UnityEngine.EventSystems.EventSystem.current && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(id)) return;
            TapAt(pos);
        }

        public void TapAt(Vector2 pos)
        {
            if (menuOpen) { CloseMenu(); return; }
            if (L == null) return;
            int b = PickBoat(pos);
            if (b >= 0) { OnBoat(b); return; }
            if (PickLock(pos)) TapLock();
        }

        // 点的是哪艘船。手机上后期一格只有 23~27pt，手指经常压在两艘船之间，所以：
        //   每艘船算"落点离船身多远"（射线直接打中船身 = 0），0.45 格以内都算候选；
        //   开不出去的船额外加 0.3 格的距离 —— 手指在一艘被挡的船和一艘能开的船之间时，选能开的那艘。
        //   吊船模式下不加（吊船本来就是去吊被挡的船）。
        public int PickBoat(Vector2 sp)
        {
            var ray = cam.ScreenPointToRay(sp);
            var plane = new Plane(Vector3.up, new Vector3(0, 0.25f, 0));
            bool onPlane = plane.Raycast(ray, out float en);
            var p = onPlane ? ray.GetPoint(en) : Vector3.zero;
            float gs = lay.gs, best = (0.45f + 0.3f) * gs; int bi = -1;   // 容错距离、"优先能开的船"的让分都按网格缩放（船小了不缩会点到旁边那艘）
            foreach (var u in boatV)
            {
                if (u == null || u.state != "lot") continue;
                float dist;
                if (u.col && u.col.Raycast(ray, out _, 500)) dist = 0;
                else if (onPlane)
                {
                    var B = L.boats[u.b]; var hm = BoatHome(u.b); var d = DirU(u.b); float half = (B.len - 0.16f) / 2 * lay.gs;
                    // 落点到船身矩形（长 len-0.16，宽 0.84）的距离
                    float along = (p.x - hm.pos.x) * d.x + (p.z - hm.pos.z) * d.z, side = (p.x - hm.pos.x) * d.z - (p.z - hm.pos.z) * d.x;
                    float ea = Mathf.Max(0, Mathf.Abs(along) - half), es = Mathf.Max(0, Mathf.Abs(side) - 0.42f * lay.gs);
                    dist = Mathf.Sqrt(ea * ea + es * es);
                }
                else continue;
                if (dist > 0.45f * gs) continue;
                float score = dist + (!craneMode && !Rules.PathOf(L, st, u.b).free ? 0.3f * gs : 0);
                if (score < best) { best = score; bi = u.b; }
            }
            return bi;
        }

        bool PickLock(Vector2 sp)
        {
            var ray = cam.ScreenPointToRay(sp);
            foreach (var g in lockV) if (g && g.GetComponent<Collider>().Raycast(ray, out _, 500)) return true;
            return false;
        }

        // ---------- 泊位 / 道具 ----------
        void OpenBerth()
        {
            int k = st.berth.Count; if (k >= MAXB) return;
            Rules.AddBerth(L, st);
            var g = lockV[k];
            UpdateHUD();
            if (!g) { Banner(T("多了一个泊位"), "berth"); return; }
            lockV[k] = null;
            // 锁先碎掉，再弹"多了一个泊位"（2026-10-22 用户要求；原来锁缩没和横幅同时出）
            var icons = labels.Where(x => x.anchor && x.anchor.IsChildOf(g.transform)).ToList();
            foreach (var lb in icons) labels.Remove(lb);   // 不再跟着镜头摆，碎的时候由自己管
            int t = tok;
            Fx.Run(async () =>
            {
                await LockShatter(g, icons.Select(x => x.root).Where(r => r).ToList());
                if (t == tok) Banner(T("多了一个泊位"), "berth");
            });
        }

        readonly List<GameObject> shards = new List<GameObject>();

        // 锁：左右抖三下 → 咔嚓沿锯齿缝裂成两半（先裂开一条缝停一下）→ 两半往两边歪着分开、往下沉、淡出。泊位上的灰板同时淡掉
        // （2026-10-22 第一版是碎成四块往外飞，用户嫌一般，要"抖几下、裂开、淡出"）
        async Task LockShatter(GameObject g, List<RectTransform> icons)
        {
            foreach (var r in icons) shards.Add(r.gameObject);
            var pos0 = icons.Select(r => r.anchoredPosition).ToList();
            // 1) 抖三下：每下是一次快速左右晃，下与下之间停一小会儿，一下比一下猛
            for (int n = 0; n < 3; n++)
            {
                sfx.Play("click"); Haptics.Play(Haptics.Kind.Light);
                float amp = 9 + n * 4;
                await Fx.Tween(130, p =>
                {
                    float s = Mathf.Sin(p * Mathf.PI * 2) * (1 - p);
                    for (int i = 0; i < icons.Count; i++) if (icons[i])
                    {
                        icons[i].localRotation = Quaternion.Euler(0, 0, s * amp);
                        icons[i].anchoredPosition = pos0[i] + new Vector2(s * amp * 0.25f, 0);
                    }
                });
                await Fx.Sleep(n < 2 ? 90 : 40);
            }
            sfx.Play("crack"); Haptics.Play(Haptics.Kind.Medium);
            // 2) 换成左右两半：裂缝是一条从上到下的锯齿线
            var halves = new List<(RectTransform r, float side)>();
            foreach (var r in icons)
            {
                if (!r) continue;
                r.localRotation = Quaternion.identity;
                var im = r.GetComponentInChildren<UnityEngine.UI.Image>();
                if (!im || !im.sprite || r.GetComponentInChildren<UnityEngine.UI.Text>()) continue;   // 没有锁图、用的是"解锁"文字牌时就不裂，直接淡掉
                var tx = im.sprite.texture; var tr = im.sprite.textureRect;
                var zig = new Vector2[6];
                for (int j = 0; j < 6; j++) zig[j] = new Vector2(0.5f + (j % 2 == 0 ? -1 : 1) * UnityEngine.Random.Range(0.04f, 0.09f), j / 5f);
                var left = new List<Vector2> { new Vector2(0, 0) }; left.AddRange(zig); left.Add(new Vector2(0, 1));
                var right = new List<Vector2> { new Vector2(1, 1) }; right.AddRange(zig.Reverse()); right.Add(new Vector2(1, 0));
                foreach (var (poly, side) in new[] { (left, -1f), (right, 1f) })
                {
                    var pr = UIKit.Rect("half", r.parent); pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
                    var fan = new Vector2(side < 0 ? 0.25f : 0.75f, 0.5f);
                    pr.sizeDelta = r.sizeDelta; pr.pivot = fan; pr.localScale = r.localScale;
                    pr.anchoredPosition = r.anchoredPosition + Vector2.Scale((fan - new Vector2(0.5f, 0.5f)) * r.sizeDelta.x, r.localScale);
                    var sg = pr.gameObject.AddComponent<ShardGraphic>(); sg.raycastTarget = false; sg.tex = tx; sg.fan = fan; sg.poly = poly.ToArray();
                    sg.uv = new Rect(tr.x / tx.width, tr.y / tx.height, tr.width / tx.width, tr.height / tx.height);
                    shards.Add(pr.gameObject); halves.Add((pr, side));
                }
                r.gameObject.SetActive(false);
            }
            var hp0 = halves.Select(h => h.r.anchoredPosition).ToList();
            var plates = g ? g.GetComponentsInChildren<MeshRenderer>().ToList() : new List<MeshRenderer>();
            var mats = plates.Select(m => m.material).ToList();   // 实例化一份，淡出不影响别的锁
            var ca = mats.Select(m => m.GetColor("_BaseColor").a).ToList();
            void Step(float open, float rot, float drop, float a)
            {
                for (int i = 0; i < halves.Count; i++)
                {
                    var (pr, side) = halves[i]; if (!pr) continue;
                    pr.anchoredPosition = hp0[i] + new Vector2(side * open, -drop);
                    pr.localRotation = Quaternion.Euler(0, 0, -side * rot);
                    var gr = pr.GetComponent<ShardGraphic>(); var c = gr.color; c.a = a; gr.color = c;
                }
                foreach (var r in icons) if (r && r.gameObject.activeSelf) { var cg = r.GetComponent<CanvasGroup>(); if (!cg) cg = r.gameObject.AddComponent<CanvasGroup>(); cg.alpha = a; }
            }
            // 先咔地裂开一道缝，停一下
            await Fx.Tween(90, p => Step(Fx.BackOut(p) * 2.5f, Fx.BackOut(p) * 4, 0, 1));
            await Fx.Sleep(160);
            // 3) 两半分开、歪倒、往下沉、淡出；灰板一起淡掉
            sfx.Play("unlock");
            var fade = Fx.Tween(480, p =>
            {
                float e = 1 - (1 - p) * (1 - p);
                Step(2.5f + e * 11, 4 + e * 18, p * p * 10, 1 - p);
                for (int i = 0; i < mats.Count; i++) if (mats[i]) { var c = mats[i].GetColor("_BaseColor"); c.a = ca[i] * (1 - p); mats[i].SetColor("_BaseColor", c); }
            });
            Fx.Run(async () =>
            {
                await fade;
                foreach (var h in halves) if (h.r) { shards.Remove(h.r.gameObject); Destroy(h.r.gameObject); }
                foreach (var r in icons) if (r) { shards.Remove(r.gameObject); Destroy(r.gameObject); }
                foreach (var m in mats) if (m) Destroy(m);
                if (g) Destroy(g);
            });
            await Fx.Sleep(200);   // 淡到一半横幅出来
        }

        void TapLock()
        {
            if (st.status != "play" || demo) return; int k = st.berth.Count; if (k >= MAXB) return;
            void Ad() => WatchAd("berth", () => { Analytics.Track("booster", "lv", levelIdx + 1, "id", "berth", "pay", "ad"); TaskAdd("boost"); OpenBerth(); });
            if (save.berthFree > 0) { save.berthFree--; save.Persist(); Analytics.Track("booster", "lv", levelIdx + 1, "id", "berth", "pay", "free"); TaskAdd("boost"); OpenBerth(); }   // 解锁时送的那次
            else if (k < L.berths + NAD) Ad();
            else if (save.coins >= COST["berth"]) { save.coins -= COST["berth"]; save.Persist(); usedBooster = true; Analytics.Track("booster", "lv", levelIdx + 1, "id", "berth", "pay", "coin"); TaskAdd("boost"); OpenBerth(); }
            else Ad();
        }

        void UseItem(string id, Action fn)
        {
            int have = id == "crane" ? save.crane : save.sort;
            void Tr(string pay) { Analytics.Track("booster", "lv", levelIdx + 1, "id", id, "pay", pay); TaskAdd("boost"); }
            boosterUse[id == "crane" ? 0 : 1]++;      // 每关上限：不管怎么来的都算一次
            if (id == "crane" ? gCrane > 0 : gSort > 0) { if (id == "crane") gCrane--; else gSort--; Tr("streak"); fn(); UpdateHUD(); }   // 连胜送的先用
            else if (have > 0) { if (id == "crane") save.crane--; else save.sort--; save.Persist(); Tr("free"); fn(); UpdateHUD(); }
            else if (save.coins >= COST[id]) { save.coins -= COST[id]; save.Persist(); Tr("coin"); fn(); UpdateHUD(); }
            else WatchAd(id, () => { Tr("ad"); fn(); UpdateHUD(); });
        }

        void DoCrane()
        {
            if (st.status != "play" || demo) return;
            if (craneMode) { SetCrane(false); return; }
            SetCrane(true); sfx.Play("click"); if (!GuideOn && guideQ.Count == 0) Toast(T("点任意一艘船（被挡着的也行），直升机直接吊到码头"));   // 引导里鹦鹉已经说了
        }

        void DoSort()
        {
            if (st.status != "play" || demo) return;
            if (pipeN > 0) { Toast(T("等大家上完船再理")); return; }
            var s2 = st.Clone();
            bool same = true;
            if (Rules.SortQ(L, s2, 30) > 0) for (int i = 0; i < s2.q.Count; i++) if (s2.q[i] != st.q[i]) { same = false; break; }
            if (same) { Toast(T("现在理队没有用")); return; }
            UseItem("sort", () =>
            {
                usedBooster = true; Rules.SortQ(L, st, 30);
                SyncGuys(true);
                sfx.Play("reveal"); Banner(T("喇叭一喊，大家排好了"), "sorted");
                var ev = new List<Ev>(); Rules.Board(L, st, ev); RunEvents(ev);
            });
        }

        void DoBerth() { if (st.berth.Count >= MAXB) { Toast(T("泊位已经全开了")); return; } TapLock(); }

        // 演示：认真玩家自己点
        async Task PlayDemo()
        {
            if (demo) { demo = false; return; }
            HideModal(); LoadLevel(levelIdx); HideModal(); demo = true; int t = tok;
            await Fx.Sleep(1000);
            while (demo && t == tok && st.status == "play")
            {
                int b = Rules.BestMove(L, st, null, 0); if (b < 0) break;
                var ev = new List<Ev>(); Rules.Tap(L, st, b, ev); sfx.Play("go"); RunEvents(ev);
                await Fx.Sleep(650);
            }
        }

        // 第 1 关：手指指着最该点的船
        int TutorBoat()
        {
            int c = st.q[st.qi];
            foreach (int b in Rules.InLot(L, st)) if (L.boats[b].c == c && Rules.CanGo(L, st, b)) return b;
            return Rules.BestMove(L, st, null, 0);
        }
        void TutorTick()
        {
            if (L != null && levelIdx == 0 && st.status == "play" && !intro && !demo && pipeN == 0 && !ModalOpen && !OnScreen && !GuideOn && guideQ.Count == 0)
            {
                int b = TutorBoat();
                if (b >= 0 && b != hintB) { HideHand(); ShowHand(b); }
            }
        }
    }
}
