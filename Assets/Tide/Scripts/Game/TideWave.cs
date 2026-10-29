// 涨潮分波（2026-10-29，方案 B）：
//   用户：画面比例调完以后，手机上 2 人竹筏太小，手指不好点；后面有 100 条船也想让船别缩太小。
//   → 港湾大到要缩小才塞得进屏幕的关（GridScale < 1.0，船比前 11 关小一成以上），不再整张缩小，而是拆成几波小港湾：
//     横平竖直的关每波 6×8、斜放的关每波 5×9 斜，都和前 11 关一样大（gs ≈ 1.1）。
//   港湾里的船全开走了 → 一道浪从屏幕下面扫上来，下一波的船被浪甩进港湾。规则见 Rules.Tide
// 波数 = 原来港湾的格子数 ÷ 每波的格子数（四舍五入），船的总数和原来差不多；算出来只有 1 波 = 直接换成小港湾。
// 每波都照这关的配置摆（礁石按面积缩、船坞 / 海藻 / 锁链 / 鲨鱼 / 白船照搬），levels.json 没动
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using static Tide.Loc;

namespace Tide
{
    public partial class Game
    {
        const float TIDE_GS = 1.0f;    // 整张港湾的缩放小于这个就分波
        int tideN;                     // 这一局涨了几次潮（自动测试看）

        static int WaterCells(Lot x) { int n = 0; foreach (var v in x.ins) if (v == 1) n++; return n; }
        static Cfg CloneCfg(Cfg c) => JsonUtility.FromJson<Cfg>(JsonUtility.ToJson(c));
        static Cfg WaveCfg(Cfg cfg)
        {
            var c = CloneCfg(cfg);
            if (cfg.rot != 0) { c.w = 5; c.h = 9; } else { c.w = 6; c.h = 8; }
            return c;
        }
        // 0 = 不分波（港湾本来就够大）
        static int TideCount(Cfg cfg)
        {
            var big = Rules.MakeLot(cfg);
            if (GridScale(big, out _, out _, out _, out _) >= TIDE_GS) return 0;
            return Mathf.Max(1, Mathf.RoundToInt(WaterCells(big) / (float)WaterCells(Rules.MakeLot(WaveCfg(cfg)))));
        }

        // 几波连着打，每波都有一半机会输的话 3 波就只剩 1/8 → 每波从 8 个 seed 里挑：
        // 普通关挑电脑随手点 6 局赢得最多的；困难关挑赢 3 局左右的，超难关挑赢 2 局左右的（还是要难，只是别叠成打不过）
        static Lot TideLevel(LevelEntry lv, int n)
        {
            var c = WaveCfg(lv.cfg);
            c.rocks = Mathf.RoundToInt(lv.cfg.rocks * WaterCells(Rules.MakeLot(c)) / (float)WaterCells(Rules.MakeLot(lv.cfg)));   // 礁石按面积缩
            int want = lv.cfg.tag == 2 ? 2 : lv.cfg.tag == 1 ? 3 : 6;
            var w = new List<Lot>();
            for (int k = 0; k < n; k++)
            {
                Lot best = null; int bd = 99;
                for (int j = 0; j < 8; j++)
                {
                    var x = Rules.LoadLevel(new LevelEntry { cfg = c, seed = lv.seed + k * 101 + j * 7, p = lv.p }); if (x == null) continue;
                    var r = new Rng(31 + j); int win = 0;
                    for (int m = 0; m < 6; m++) if (Rules.Playout(x, r, 40)) win++;
                    int d = Mathf.Abs(win - want);
                    if (d < bd) { bd = d; best = x; }
                }
                if (best != null) w.Add(best);
            }
            return w.Count > 0 ? Rules.Waves(w) : Rules.LoadLevel(lv);
        }

        // 浪的贴图：上面是波浪形的白浪头（带一点泡沫点），往下越来越淡；左右两边也淡出去，看不出是一块方片
        static Texture2D waveTex;
        static Texture2D WaveTex()
        {
            if (waveTex) return waveTex;
            const int TW = 256, TH = 128;
            var tx = new Texture2D(TW, TH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var r = new Rng(5); Color foam = Color.white, body = UIKit.Hex("dff8ff"), tail = UIKit.Hex("9fe8f5");
            for (int x = 0; x < TW; x++)
            {
                float u = x / (TW - 1f), edge = 0.86f + 0.05f * Mathf.Sin(u * Mathf.PI * 2 * 5) + 0.025f * Mathf.Sin(u * Mathf.PI * 2 * 13 + 1);
                float side = Mathf.Clamp01(Mathf.Min(u, 1 - u) / 0.07f);
                for (int y = 0; y < TH; y++)
                {
                    float v = y / (TH - 1f); Color c;
                    if (v > edge) c = new Color(1, 1, 1, 0);
                    else if (v > edge - 0.07f) c = new Color(foam.r, foam.g, foam.b, 0.95f);
                    else { float k = Mathf.Clamp01(v / (edge - 0.07f)); c = Color.Lerp(tail, body, k); c.a = 0.55f * Mathf.Pow(k, 1.6f); if (v > edge - 0.3f && r.Next() < 0.08) c = new Color(1, 1, 1, 0.75f); }
                    c.a *= side; tx.SetPixel(x, y, c);
                }
            }
            tx.Apply(); return waveTex = tx;
        }

        // RunEvents 最后调：港湾空了、后面还有波，就涨潮
        readonly List<(Transform t, Vector3 s)> decoPop = new List<(Transform, Vector3)>();
        void TideCheck()
        {
            if (!Rules.TideDue(L, st)) return;
            var ev = new List<Ev>(); Rules.Tide(L, st, ev); var e = ev[0];
            System.Array.Resize(ref boatV, L.boats.Count); System.Array.Resize(ref mechV, L.boats.Count);
            for (int b = e.b; b < e.b + e.n; b++) if (st.inl[b] != 0) { AddBoatView(b, true); AddMechView(b); boatV[b].bob = false; boatV[b].g.SetActive(false); }   // 浪头过来之前先藏着（也点不到）；船坞里还没出来的船等 SpawnFx
            // 这一波的礁石、船坞、鲨鱼：礁石船坞先缩成 0，浪头过去时弹出来
            BuildDeco(true); SharkViews();
            decoPop.Clear(); foreach (Transform c in deco) { decoPop.Add((c, c.localScale)); c.localScale = Vector3.one * 0.001f; }
            intro = true; tideN++; hintB = -1;
            Fx.Run(() => TideFx(e));
        }

        // 10-29 用户：浪太平淡，要明显、酷炫一点 → 三拍：
        //   ① 蓄力 0.65s：港湾的水被吸走（变暗变透明），镜头隆隆抖，屏幕下沿涌起一道浪墙
        //   ② 巨浪 1s：往前卷的弧形浪墙扫过港湾，浪头一路喷水花，新船从浪头里被甩出来、转着圈落到格子上，砸出水花和水圈
        //   ③ 拍岸：浪墙拍在码头前沿碎掉——白屏一闪、大震、一排水花，横幅「涨潮啦！」，排队的人全体欢呼跳起来
        async Task TideFx(Ev e)
        {
            int t = tok;
            await Fx.Sleep(250); if (t != tok) return;   // 最后一艘船先开出港湾
            var lag = world.Find("Lagoon"); Material lagM = null; Color lagC = Color.white;
            if (lag) { var lr = lag.GetComponent<MeshRenderer>(); lagM = new Material(lr.sharedMaterial); lr.sharedMaterial = lagM; lagC = lagM.GetColor("_BaseColor"); }
            // 浪墙 + 后面拖着的一片浪沫
            var wall = new GameObject("tideWall").transform; wall.SetParent(world, false);
            wall.gameObject.AddComponent<MeshFilter>().sharedMesh = WallMesh();
            var wm = new Material(Art.UnlitT); wm.SetTexture("_BaseMap", WallTex()); wm.SetColor("_BaseColor", Color.white);
            var wr = wall.gameObject.AddComponent<MeshRenderer>(); wr.sharedMaterial = wm; wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var cq = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(cq.GetComponent<Collider>());
            var carpet = cq.transform; carpet.SetParent(world, false); carpet.rotation = Quaternion.Euler(90, 0, 0);
            const float D = 3.6f; carpet.localScale = new Vector3(REG_W + 0.4f, D, 1);
            var cm = new Material(Art.UnlitT); cm.SetTexture("_BaseMap", WaveTex()); cm.SetColor("_BaseColor", Color.white);
            cq.GetComponent<MeshRenderer>().sharedMaterial = cm;
            float crestY = 0;
            void Place(float zc, float hs)   // zc：浪头（卷起来那个尖）的位置；hs：浪墙多高
            {
                wall.position = new Vector3(0, 0, zc - 0.5f * hs); wall.localScale = new Vector3(REG_W + 0.4f, hs, hs);
                carpet.position = new Vector3(0, 0.3f, zc - 0.9f * hs - D / 2);
                crestY = 1.72f * hs;
            }
            float zS = -REG_H / 2 - 2.2f, zA = -REG_H / 2 - 0.4f, z1 = REG_H / 2 + 0.3f;

            // ① 蓄力
            sfx.Play("rumble"); Haptics.Play(Haptics.Kind.Light);
            await Fx.Tween(650, p =>
            {
                shake = Mathf.Max(shake, 0.1f + 0.22f * p);
                if (lagM) lagM.SetColor("_BaseColor", new Color(lagC.r * 0.75f, lagC.g * 0.85f, lagC.b, lagC.a * (1 - 0.75f * p)));
                Place(Mathf.Lerp(zS, zA, Fx.BackOut(p) * 0.9f + p * 0.1f), Mathf.Lerp(0.4f, 1.0f, p));
            });
            if (t != tok) { TideClean(wall, carpet, wm, cm); return; }

            // ② 巨浪
            sfx.Play("wave"); Haptics.Play(Haptics.Kind.Medium);
            var todo = new List<int>(); for (int b = e.b; b < e.b + e.n; b++) if (boatV[b] != null) todo.Add(b);
            var dp = new List<(Transform t, Vector3 s)>(decoPop);
            var pops = new List<Task>(); float spray = 0; int popN = 0;
            await Fx.Tween(1000, p =>
            {
                float zc = Mathf.Lerp(zA, z1, Mathf.Pow(p, 1.15f)), hs = Mathf.Lerp(1.0f, 1.8f, p);
                Place(zc, hs);
                shake = Mathf.Max(shake, 0.16f);
                if (lagM) lagM.SetColor("_BaseColor", Color.Lerp(new Color(lagC.r * 0.75f, lagC.g * 0.85f, lagC.b, lagC.a * 0.25f), lagC, p));
                if (Fx.Now - spray > 30)
                {
                    spray = Fx.Now;
                    for (int j = 0; j < 2; j++) Burst(new Vector3(Random.Range(-REG_W / 2 + 0.3f, REG_W / 2 - 0.3f), crestY, zc), j == 0 ? Color.white : UIKit.Hex("c8f4ff"), 3, 0.9f);
                }
                for (int i = dp.Count - 1; i >= 0; i--)
                {
                    var (dt, ds) = dp[i]; if (!dt) { dp.RemoveAt(i); continue; }
                    if (dt.position.z > zc - 0.2f) continue;
                    dp.RemoveAt(i); Fx.Run(() => Fx.Tween(300, q => { if (dt) dt.localScale = ds * Mathf.Max(0.001f, Fx.BackOut(q)); }));
                }
                for (int i = todo.Count - 1; i >= 0; i--)
                {
                    int b = todo[i]; var u = boatV[b]; if (u == null || !u.g) { todo.RemoveAt(i); continue; }
                    var home = BoatHome(b);
                    if (home.pos.z > zc + 1.0f) continue;   // 浪头离它还有 1 米以上
                    todo.RemoveAt(i);
                    pops.Add(TideToss(u, new Vector3(home.pos.x, crestY, zc), home.pos, home.yaw, popN++, t));
                }
            });
            foreach (int b in todo) if (boatV[b] != null && boatV[b].g) { var h = BoatHome(b); pops.Add(TideToss(boatV[b], new Vector3(h.pos.x, crestY, z1), h.pos, h.yaw, popN++, t)); }
            foreach (var (dt, ds) in dp) if (dt) dt.localScale = ds;
            if (t != tok) { TideClean(wall, carpet, wm, cm); return; }

            // ③ 拍岸
            sfx.Play("crash"); Haptics.Play(Haptics.Kind.Heavy); shake = 0.75f;
            TideFlash();
            for (int j = 0; j < 30; j++) Burst(new Vector3(Mathf.Lerp(-REG_W / 2, REG_W / 2, j / 29f), crestY * 0.8f, z1), j % 3 == 0 ? UIKit.Hex("c8f4ff") : Color.white, 3, 1.5f);
            Banner(T("涨潮啦！") + "  " + (e.k + 1) + "/" + (L.waves.Count + 1));
            jumpT = Fx.Now; sfx.Play("cheer");   // 排队的人全体跳起来（Special.cs），树和房子也跟着晃（Cheer.cs）
            float hs0 = 1.8f;
            await Fx.Tween(320, p =>
            {
                wall.position = new Vector3(0, 0, z1 - 0.5f * hs0 + 0.6f * p); wall.localScale = new Vector3(REG_W + 0.4f, hs0 * (1 - p), hs0 * (1 + 0.4f * p));   // 浪墙往前拍扁
                wm.SetColor("_BaseColor", new Color(1, 1, 1, 1 - p)); cm.SetColor("_BaseColor", new Color(1, 1, 1, 1 - p));
                if (lagM) lagM.SetColor("_BaseColor", Color.Lerp(new Color(1, 1, 1, lagC.a), lagC, p));   // 港湾的水亮一下
            });
            TideClean(wall, carpet, wm, cm);
            await Task.WhenAll(pops);
            if (t == tok) intro = false;
        }
        void TideClean(Transform wall, Transform carpet, Material wm, Material cm)
        {
            if (wall) Destroy(wall.gameObject);
            if (carpet) Destroy(carpet.gameObject);
            Destroy(wm); Destroy(cm);
        }

        // 一艘船被浪头甩出来：从浪尖往前抛，转一圈落到自己的格子上，砸出水花和一圈水波，压扁弹一下
        async Task TideToss(BoatV u, Vector3 from, Vector3 home, float yaw, int n, int t)
        {
            var g = u.g.transform; u.g.SetActive(true); g.localScale = Vector3.one * 0.7f * lay.gs;
            float spin = (n % 2 == 0 ? 1 : -1) * 360, dur = 420 + Random.Range(0, 120);
            await Fx.Tween(dur, p =>
            {
                if (t != tok || !g) return;
                var q = Vector3.Lerp(from, home, p); q.y = Mathf.Lerp(from.y, 0, p * p) + Mathf.Sin(p * Mathf.PI) * 0.9f;
                float sp = p * p * (3 - 2 * p);
                g.position = q; g.rotation = Quaternion.Euler(Mathf.Sin(p * Mathf.PI) * -25, yaw * Mathf.Rad2Deg + spin * (1 - sp), 0);
                g.localScale = Vector3.one * Mathf.Lerp(0.7f, 1, p) * lay.gs;
            });
            if (t != tok || !g) return;
            g.position = home; g.rotation = Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0);
            Burst(home + Vector3.up * 0.2f, Color.white, 6, 0.8f); Ripple(home);
            if (n % 3 == 0) sfx.Play("pop" + (n / 3) % 5);
            if (n % 4 == 0) Haptics.Play(Haptics.Kind.Light);
            await Fx.Tween(220, p =>
            {
                if (!g) return; float k = Mathf.Sin(p * Mathf.PI) * (1 - p);
                g.localScale = new Vector3(1 + 0.35f * k, 1 - 0.4f * k, 1 + 0.35f * k) * lay.gs;
            });
            if (t != tok || !g) return;
            g.localScale = Vector3.one * lay.gs; u.bob = true;
        }

        // 落水的一圈水波
        void Ripple(Vector3 at)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>());
            var tr = q.transform; tr.SetParent(world, false); tr.position = new Vector3(at.x, 0.06f, at.z); tr.rotation = Quaternion.Euler(90, 0, 0);
            var m = new Material(Art.UnlitT); m.SetTexture("_BaseMap", RingTex()); q.GetComponent<MeshRenderer>().sharedMaterial = m;
            Fx.Run(async () =>
            {
                await Fx.Tween(520, p => { if (!tr) return; tr.localScale = Vector3.one * Mathf.Lerp(0.4f, 2.2f, 1 - (1 - p) * (1 - p)) * lay.gs; m.SetColor("_BaseColor", new Color(1, 1, 1, 0.9f * (1 - p))); });
                if (tr) Destroy(tr.gameObject);
                Destroy(m);
            });
        }

        // 拍岸那一下白屏一闪
        UnityEngine.UI.Image tideFlash;
        void TideFlash()
        {
            if (!tideFlash)
            {
                tideFlash = UIKit.Img((RectTransform)ui.labels.parent, "tideFlash", null, new Color(1, 1, 1, 0)); tideFlash.rectTransform.Stretch(-50, -50, -50, -50);
                tideFlash.raycastTarget = false; tideFlash.transform.SetSiblingIndex(ui.labels.GetSiblingIndex() + 1);
            }
            var im = tideFlash;
            Fx.Run(() => Fx.Tween(420, p => { if (im) im.color = new Color(1, 1, 1, 0.75f * (1 - p) * (1 - p)); }));
        }

        // 浪墙：横截面是一条往前卷的弧（z, y），沿 x 拉宽；正反两面都画（镜头从浪的背后看）
        static Mesh wallMesh;
        static Mesh WallMesh()
        {
            if (wallMesh) return wallMesh;
            var pf = new[] { new Vector2(-1.4f, 0), new Vector2(-0.8f, 0.42f), new Vector2(-0.38f, 0.92f), new Vector2(-0.06f, 1.38f), new Vector2(0.22f, 1.66f), new Vector2(0.46f, 1.72f), new Vector2(0.64f, 1.56f), new Vector2(0.7f, 1.38f) };
            const int NX = 12; var vs = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int i = 0; i < pf.Length; i++) for (int j = 0; j <= NX; j++) { vs.Add(new Vector3(j / (float)NX - 0.5f, pf[i].y, pf[i].x)); uv.Add(new Vector2(j / (float)NX, i / (pf.Length - 1f))); }
            for (int i = 0; i < pf.Length - 1; i++) for (int j = 0; j < NX; j++)
                {
                    int a = i * (NX + 1) + j, b = a + 1, c = a + NX + 1, d = c + 1;
                    tri.AddRange(new[] { a, c, b, b, c, d, a, b, c, b, d, c });
                }
            wallMesh = new Mesh(); wallMesh.SetVertices(vs); wallMesh.SetUVs(0, uv); wallMesh.SetTriangles(tri, 0); wallMesh.RecalculateBounds();
            return wallMesh;
        }
        // 浪墙贴图（v 从下往上）：底下深蓝半透明 → 亮青 → 顶上白浪沫（下沿参差），左右两头淡出去
        static Texture2D wallTex;
        static Texture2D WallTex()
        {
            if (wallTex) return wallTex;
            const int TW = 128, TH = 128;
            var tx = new Texture2D(TW, TH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var r = new Rng(9); Color deep = UIKit.Hex("1f86c8"), mid = UIKit.Hex("4fd2ee"), foam = Color.white;
            for (int x = 0; x < TW; x++)
            {
                float u = x / (TW - 1f), fe = 0.62f + 0.06f * Mathf.Sin(u * Mathf.PI * 2 * 6) + 0.03f * Mathf.Sin(u * Mathf.PI * 2 * 17 + 2);
                float side = Mathf.Clamp01(Mathf.Min(u, 1 - u) / 0.05f);
                for (int y = 0; y < TH; y++)
                {
                    float v = y / (TH - 1f); Color c;
                    if (v > fe) c = new Color(foam.r, foam.g, foam.b, 0.97f);
                    else { float k = v / fe; c = Color.Lerp(deep, mid, k); c.a = Mathf.Lerp(0.6f, 0.95f, k); if (v > fe - 0.12f && r.Next() < 0.18) c = new Color(1, 1, 1, 0.9f); }
                    c.a *= side; tx.SetPixel(x, y, c);
                }
            }
            tx.Apply(); return wallTex = tx;
        }
        static Texture2D ringTex;
        static Texture2D RingTex()
        {
            if (ringTex) return ringTex;
            const int N = 64; var tx = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(N / 2f - 0.5f, N / 2f - 0.5f)) / (N / 2f);
                    tx.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1 - Mathf.Abs(d - 0.82f) / 0.12f)));
                }
            tx.Apply(); return ringTex = tx;
        }
    }
}
