// 风暴海浪（2026-10-30 用户）：椰子树后面那片变成海，碎浪一段段往岸上拍；风暴倒计时越少，海越往海滩上漫（淹过草地、树脚、排队区），
// 倒计时加回来（救人上船 / 复活 / 连击狂潮）海就退回去；倒计时走完 → 超级海啸从屏幕顶上压下来吞掉整个画面 →
// 水幕冲走后不退，留下一片惨状（10-30 用户：浪又退回去效果不好）：浑水淹了整个画面，船翻的翻沉的沉，人只露脑袋，椰子树倒了，杂物漂着 → 失败弹窗；复活时水幕一盖全部还原。
// 原来从码头往排队区漫的那块"潮水"去掉了，换成这个。界面（关卡名、倒计时、金币、道具按钮）永远压在浪上面；
// 平时最多漫到码头后沿，不盖港湾。
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Tide
{
    public partial class Game
    {
        const float SEA_Y = 0.42f, SEA_BACK = 2.6f, SEA_STOP = 0.35f, SEA_FAR = 40, FOAM_D = 1.4f;
        const int BREAKERS = 9;
        Transform surgeSea, surgeSwell, surgeFoam; Material surgeSeaM, surgeSwellM, surgeFoamM, crestM;
        class Breaker { public Transform t; public float x, w, ph, per, dist, last; }
        readonly List<Breaker> breakers = new List<Breaker>();
        bool surgeHold;            // 海啸动画在摆弄海，SurgeTick 别管
        float surgeEdge;           // 现在的海岸线（世界 z，越大越靠屏幕上面）
        float seaLift;             // 海啸时整片海抬高，把岸上的东西都吞掉
        RawImage surgeWave; Image surgeVeil; RectTransform surgeFx;

        // 海岸线：平时在椰子树后面，倒计时走完时到码头后沿
        float SurgeZ0 => -(lay.zP - ENV_G - SEA_BACK);
        float SurgeZ1 => -(lay.zP - SEA_STOP);

        void SurgeScene()
        {
            if (wreck) UnWreck();   // 上一关输在海啸里，直接重开 / 换关
            if (ui.labels) ui.labels.gameObject.SetActive(true);
            if (surgeWave) surgeWave.gameObject.SetActive(false);
            breakers.Clear(); surgeHold = false; seaLift = 0; if (stormOn || L.cfg.storm > 0) TsunamiTex();   // 巨浪贴图提前生成，免得倒计时走完那一下卡
            surgeSeaM = SurgeMat(SeaTex(), 3000); surgeSea = SurgeQuad("surgeSea", surgeSeaM);
            surgeSwellM = SurgeMat(SwellTex(), 3001); surgeSwell = SurgeQuad("surgeSwell", surgeSwellM);
            surgeFoamM = SurgeMat(FoamTex(), 3002); surgeFoamM.SetTextureScale("_BaseMap", new Vector2(6, 1)); surgeFoam = SurgeQuad("surgeFoam", surgeFoamM);
            crestM = SurgeMat(WallTex(), 3003);
            // 碎浪：一段一段错开，各拍各的（一整条横贯的浪看起来像一根白杠）
            for (int i = 0; i < BREAKERS; i++)
            {
                var c = new GameObject("breaker" + i).transform; c.SetParent(world, false);
                c.gameObject.AddComponent<MeshFilter>().sharedMesh = WallMesh();
                var r = c.gameObject.AddComponent<MeshRenderer>(); r.sharedMaterial = crestM; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                c.rotation = Quaternion.Euler(0, 180, 0);   // 浪头卷向镜头（往岸上扑）
                breakers.Add(new Breaker { t = c, x = Mathf.Lerp(-8.5f, 8.5f, (i + Random.Range(-0.3f, 0.3f)) / (BREAKERS - 1)), w = Random.Range(3.2f, 5.5f),
                    ph = Random.value, per = Random.Range(2200f, 3200f), dist = Random.Range(1.8f, 3.2f) });
            }
            PlaceSea(SurgeZ0, 0);
        }
        Material SurgeMat(Texture2D tex, int q)
        {
            var m = new Material(Art.UnlitT); m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", Color.white); m.renderQueue = q; return m;
        }
        Transform SurgeQuad(string name, Material m)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>());
            q.name = name; q.transform.SetParent(world, false); q.transform.rotation = Quaternion.Euler(90, 0, 0);
            var r = q.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return q.transform;
        }
        // 海面从岸线 z 一直铺到画面外；浪沫带压在岸线上
        void PlaceSea(float z, float swash)
        {
            if (!surgeSea) return;
            surgeEdge = z;
            float far = SurgeZ0 + SEA_FAR, len = Mathf.Max(1, far - z), z0 = z + FOAM_D * 0.6f;
            surgeSea.position = new Vector3(0, SEA_Y + seaLift, z0 + len / 2); surgeSea.localScale = new Vector3(80, len, 1);
            surgeSwell.position = new Vector3(0, SEA_Y + seaLift + 0.005f, z0 + len / 2); surgeSwell.localScale = new Vector3(80, len, 1);
            surgeSwellM.SetTextureScale("_BaseMap", new Vector2(10, len / 2.2f));
            surgeFoam.position = new Vector3(0, SEA_Y + seaLift + 0.01f, z - swash + FOAM_D / 2); surgeFoam.localScale = new Vector3(80, FOAM_D, 1);
        }

        // StormTick 里每帧调：岸线跟着风暴走，碎浪一段段往岸上卷、拍碎
        void SurgeTick(float dt, float now)
        {
            if (!surgeSea || surgeHold) return;
            float k = stormOn ? stormK : 0;
            float swash = 0.15f * Mathf.Sin(now / 2600 * Mathf.PI * 2) * (1 + k);
            PlaceSea(Mathf.Lerp(SurgeZ0, SurgeZ1, k), swash);
            surgeFoamM.SetTextureOffset("_BaseMap", new Vector2(now * 0.00002f, 0));
            surgeSwellM.SetTextureOffset("_BaseMap", new Vector2(now * 0.000015f, now * (0.00035f + 0.0004f * k)));   // 涌浪纹往岸上推，风暴越近推得越快
            foreach (var b in breakers)
            {
                float f = Mathf.Repeat(now / b.per + b.ph, 1);
                float grow = Mathf.SmoothStep(0, 1, f / 0.8f), fall = f < 0.8f ? 1 : 1 - (f - 0.8f) / 0.2f;
                float hs = Mathf.Lerp(0.06f, 0.3f + 0.35f * k, grow) * Mathf.Max(0.02f, fall);
                float zt = surgeEdge + Mathf.Lerp(b.dist, 0.1f, f) - swash * 0.5f;
                b.t.position = new Vector3(b.x, SEA_Y - 0.05f, zt + 0.5f * hs); b.t.localScale = new Vector3(b.w * Mathf.Lerp(0.7f, 1.1f, grow), hs, hs);
                // 拍碎那一下溅浪花（风暴越近越大）
                if (f > 0.82f && b.last <= 0.82f && stormOn && k > 0.1f) Burst(new Vector3(b.x + Random.Range(-1f, 1f), SEA_Y + 0.12f, surgeEdge + 0.2f), Color.white, 2 + Mathf.RoundToInt(3 * k), 0.4f + 0.5f * k);
                b.last = f;
            }
        }

        // 屏幕坐标 y（像素）对应海面上哪个 z
        float ScreenToSeaZ(float sy)
        {
            var r = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, sy, 0));
            if (r.direction.y > -0.001f) return SurgeZ0 + SEA_FAR;
            return r.origin.z + (SEA_Y + seaLift - r.origin.y) / r.direction.y * r.direction.z;
        }

        // 风暴走完：超级海啸 → 失败弹窗
        //   ① 蓄力 0.9s：海往后一吸露出湿沙，天暗、打雷、隆隆抖，屏幕顶上压过来一堵深蓝的巨浪墙，浪尖翻着白沫
        //   ② 扑 1s：巨浪从上往下扫过整个画面（海滩、排队区、码头、港湾），浪尖后面的海跟着淹过来，浪尖一路喷水花，船被掀起来
        //   ③ 拍屏：画面被水吞掉（白闪、大震、气泡往上冒），停一下
        //   ④ 水幕往下冲走，露出灾后的样子，停 1.5 秒让人看清，再弹失败
        async Task TsunamiFx()
        {
            int t = tok;
            if (!surgeSea) { await LoseFx(); return; }
            surgeHold = true;
            foreach (var b in breakers) if (b.t) b.t.localScale = Vector3.one * 0.001f;
            var root = (RectTransform)ui.labels.parent; float H = root.rect.height, Wc = root.rect.width;
            SurgeLayer(root);
            var wr = surgeWave.rectTransform; surgeWave.gameObject.SetActive(true); surgeWave.color = Color.white;
            float WH = H * 1.35f;   // 浪墙图比屏幕高一截：浪尖扫到底时整屏都是水
            wr.sizeDelta = new Vector2(0, WH);
            float lipV = 0.1f;      // 贴图里浪尖大概在下沿往上 10% 的地方
            float scrollU = 0;
            void Wave(float lipY, float sway)   // lipY：浪尖离屏幕顶多远（画布单位，往下为正）
            {
                wr.anchoredPosition = new Vector2(0, -lipY + lipV * WH);
                scrollU += 0.004f + sway; surgeWave.uvRect = new Rect(scrollU, 0, 1.6f, 1);
            }
            float LipScreenY() { var c = new Vector3[4]; wr.GetWorldCorners(c); return c[0].y + (c[1].y - c[0].y) * lipV; }
            float z0 = surgeEdge, zBack = z0 + 1.8f;

            // ① 蓄力
            sfx.Play("rumble"); sfx.Play("thunder", 0.8f, 0.6f); Haptics.Play(Haptics.Kind.Medium); flashT = 1;
            await Fx.Tween(900, p =>
            {
                if (t != tok) return;
                PlaceSea(Mathf.Lerp(z0, zBack, Fx.BackOut(Mathf.Min(1, p * 1.5f))), 0);   // 海往后吸
                Wave(Mathf.Lerp(-0.05f * H, 0.2f * H, p * p) + Mathf.Sin(p * 18) * 6 * p, 0.002f);
                shake = Mathf.Max(shake, 0.12f + 0.3f * p);
            });
            if (t != tok) { TsunamiClean(); return; }

            // ② 扑下来
            sfx.Play("wave"); sfx.Play("wave", 0.7f); Haptics.Play(Haptics.Kind.Heavy);
            jumpT = Fx.Now;   // 排队的人吓得一起跳（Special.cs）
            float spray = 0;
            await Fx.Tween(1000, p =>
            {
                if (t != tok) return;
                float lip = Mathf.Lerp(0.2f * H, 1.08f * H, Mathf.Pow(p, 1.5f));
                Wave(lip, 0.006f); seaLift = 1.5f * p;
                PlaceSea(Mathf.Min(zBack, ScreenToSeaZ(Mathf.Max(1, LipScreenY()))), 0);   // 浪尖后面整片都是海
                shake = Mathf.Max(shake, 0.35f);
                if (Fx.Now - spray > 40) { spray = Fx.Now; for (int j = 0; j < 4; j++) Spray(root, new Vector2(Random.Range(-Wc / 2, Wc / 2), H / 2 - lip)); }
            });
            if (t != tok) { TsunamiClean(); return; }

            // ③ 拍屏：整屏是水——趁看不见，把场景换成灾后的样子
            sfx.Play("crash"); sfx.Play("thunder", 0.7f, 0.8f); Haptics.Play(Haptics.Kind.Heavy); shake = 1.0f;
            TideFlash();
            seaLift = WRECK_LIFT; PlaceSea(-REG_H / 2 - 12, 0);
            BuildWreck();
            for (int j = 0; j < 26; j++) Bubble(root, j);
            await Fx.Tween(550, p => { if (t == tok) Wave(1.08f * H + 0.1f * H * p, 0.003f); });
            if (t != tok) { TsunamiClean(); return; }

            // ④ 水幕往下冲走，露出一片狼藉：岸上全淹了，船翻的翻、沉的沉，人只剩脑袋露在水面上扑腾，椰子树倒了，杂物漂得到处都是
            await Fx.Tween(900, p =>
            {
                if (t != tok) return;
                surgeWave.color = new Color(1, 1, 1, 1 - p * p);
                Wave(1.18f * H + 0.5f * H * p * p, 0.002f);
            });
            if (surgeWave) surgeWave.gameObject.SetActive(false);
            if (t != tok) { TsunamiClean(); return; }
            flashT = 1; sfx.Play("thunder", 0.9f, 0.5f);
            await Fx.Sleep(1500); if (t != tok) { TsunamiClean(); return; }   // 先让玩家看清这一片惨状
            if (surgeFx) foreach (Transform c in surgeFx) Destroy(c.gameObject);
            await LoseFx();
        }
        void TsunamiClean()
        {
            if (surgeWave) surgeWave.gameObject.SetActive(false);
            if (surgeFx) foreach (Transform c in surgeFx) Destroy(c.gameObject);
            if (wreck) UnWreck();
            seaLift = 0; surgeHold = false;
        }

        // ---- 灾后：大浪过后的惨状。只改各个东西的位置/朝向，原样记下来，复活时一键还原 ----
        const float WRECK_LIFT = 0.38f;
        bool wreck;
        class Wrecked { public Transform t; public Vector3 p, s; public Quaternion r; public bool on; }
        class Floater { public Transform t; public Vector3 p; public Quaternion r; public float ph, amp, spd; }
        readonly List<Wrecked> wrecked = new List<Wrecked>();
        readonly List<Floater> floaters = new List<Floater>();
        readonly List<(BoatV u, bool bob)> wreckBob = new List<(BoatV, bool)>();
        Transform wreckRoot;
        float WreckY => SEA_Y + WRECK_LIFT;

        void Keep(Transform t) { if (t) wrecked.Add(new Wrecked { t = t, p = t.position, r = t.rotation, s = t.localScale, on = t.gameObject.activeSelf }); }
        // 让一个东西泡在水里：露出水面的高度占它自己高度的 above
        void Float(Transform t, float above, float amp = 0.035f, float spd = 1)
        {
            var rs = t.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            t.position += Vector3.up * (WreckY - b.size.y * (1 - above) - b.min.y);
            floaters.Add(new Floater { t = t, p = t.position, r = t.rotation, ph = Random.value * 6.28f, amp = amp, spd = spd });
        }

        void BuildWreck()
        {
            wreck = true; wrecked.Clear(); floaters.Clear(); wreckBob.Clear();
            ui.labels.gameObject.SetActive(false);   // 船上的人数、"剩 N 人"这些字先藏起来
            surgeSeaM.SetColor("_BaseColor", new Color(0.8f, 0.76f, 0.64f, 1));   // 浑水
            surgeSwellM.SetColor("_BaseColor", new Color(0.9f, 0.85f, 0.75f, 1));
            // 船：翻过来底朝天的、侧翻半沉的，都被冲得歪七扭八
            foreach (var u in boatV)
            {
                if (u == null || !u.g || !u.g.activeSelf) continue;
                var t = u.g.transform; Keep(t); wreckBob.Add((u, u.bob)); u.bob = false;
                t.position += new Vector3(Random.Range(-0.9f, 0.9f), 0, Random.Range(-0.7f, 0.7f));
                bool flip = Random.value < 0.4f;
                t.rotation = Quaternion.Euler(0, t.eulerAngles.y + Random.Range(-50f, 50f), 0) * (flip ? Quaternion.Euler(Random.Range(-12f, 12f), 0, 180 + Random.Range(-15f, 15f)) : Quaternion.Euler(Random.Range(-25f, 25f), 0, Random.Range(35f, 70f) * (Random.value < 0.5f ? 1 : -1)));
                Float(t, flip ? 0.4f : 0.5f, 0.04f);
            }
            // 排队的人：冲散了，只剩脑袋露在水面上扑腾
            foreach (var g in qGuys)
            {
                if (!g.g) continue; var t = g.g.transform; Keep(t);
                t.position += new Vector3(Random.Range(-0.9f, 0.9f), 0, Random.Range(-0.8f, 0.8f));
                t.rotation = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0, 360f), Random.Range(-15f, 15f));
                Float(t, Random.Range(0.32f, 0.45f), 0.06f, Random.Range(2.2f, 3.4f));
            }
            // 椰子树倒了、草屋歪了半截泡在水里、遮阳伞翻了，出人的小屋也歪了
            foreach (var l in lives)
            {
                if (!l.t) continue; var t = l.t; Keep(t);
                string m = t.name;
                if (m.StartsWith("palm")) { t.rotation = Quaternion.Euler(Random.Range(55f, 75f) * (Random.value < 0.5f ? 1 : -1), Random.Range(0, 360f), Random.Range(-20f, 20f)); t.position += Vector3.up * -0.15f; floaters.Add(new Floater { t = t, p = t.position, r = t.rotation, ph = Random.value * 6.28f, amp = 0.01f, spd = 0.6f }); }
                else if (m == "umbrella") { t.rotation = Quaternion.Euler(Random.Range(70f, 100f), Random.Range(0, 360f), 0); Float(t, 0.35f, 0.04f); }
                else if (m == "bush") t.position += Vector3.down * 0.6f;
                else { t.rotation = Quaternion.Euler(Random.Range(-18f, 18f), t.eulerAngles.y + Random.Range(-15f, 15f), Random.Range(12f, 22f) * (Random.value < 0.5f ? 1 : -1)); Float(t, 0.5f, 0.015f, 0.5f); }
            }
            for (int k = 0; k < MAXB; k++) if (lockV[k]) { Keep(lockV[k].transform); lockV[k].SetActive(false); }
            // 港湾那块浅水是透明的，会盖在浑水上面——藏掉；排队的绳子冲没了，木桩东倒西歪
            foreach (Transform c in world)
            {
                if (c.name == "Lagoon" || c.name == "ropes") { Keep(c); c.gameObject.SetActive(false); }
                else if (c.name == "qpost" && Random.value < 0.75f) { Keep(c); c.rotation = Quaternion.Euler(Random.Range(-35f, 35f), Random.Range(0, 360f), Random.Range(-35f, 35f)); c.position += Vector3.down * Random.Range(0.1f, 0.3f); }
            }
            // 漂着的杂物：木箱、木桶、救生圈、浮标
            wreckRoot = new GameObject("wreck").transform; wreckRoot.SetParent(world, false);
            string[] junk = { "crate", "crate", "barrel", "crate", "barrel", "lifering", "crate", "barrel", "buoy_red", "crate", "lifering", "barrel", "buoy_yellow" };
            float zTop = SurgeZ0 - 0.5f, zBot = -REG_H / 2 - 1.5f;
            for (int i = 0; i < 34; i++)
            {
                var m = junk[i % junk.Length];
                var g = Add(m, new Vector3(Random.Range(-6.5f, 6.5f), 0, Mathf.Lerp(zBot, zTop, Random.value)), Random.Range(0, 360f), false, Random.Range(0.8f, 1.15f));
                g.transform.SetParent(wreckRoot, true);
                g.transform.rotation = Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(0, 360f), Random.Range(-20f, 20f)) * (m == "lifering" ? Quaternion.identity : Quaternion.Euler(0, 0, m == "barrel" && Random.value < 0.6f ? 90 : 0));
                Float(g.transform, m == "lifering" ? 0.5f : 0.45f, 0.04f, Random.Range(0.8f, 1.4f));
            }
        }
        // Update 里代替平时那套（人沿通道走、船轻晃、树摇……）：所有东西在水里一起一伏地漂
        void WreckTick(float now)
        {
            float s = now / 1000;
            foreach (var f in floaters)
            {
                if (!f.t) continue;
                float a = s * f.spd + f.ph;
                f.t.position = f.p + new Vector3(Mathf.Sin(a * 0.4f) * 0.12f, Mathf.Sin(a * 2.2f) * f.amp, Mathf.Cos(a * 0.3f) * 0.08f);
                f.t.rotation = Quaternion.Euler(Mathf.Sin(a * 1.7f) * 5, Mathf.Sin(a * 0.2f) * 8, Mathf.Cos(a * 1.3f) * 5) * f.r;
            }
        }
        // 复活：一道水幕盖住画面，趁机把东西都摆回去，水幕往上退走，海慢慢退回椰子树后面
        void UnWreck()
        {
            foreach (var w in wrecked) { if (!w.t) continue; w.t.position = w.p; w.t.rotation = w.r; w.t.localScale = w.s; w.t.gameObject.SetActive(w.on); }
            foreach (var (u, bob) in wreckBob) if (u != null) u.bob = bob;
            wrecked.Clear(); floaters.Clear(); wreckBob.Clear();
            if (wreckRoot) Destroy(wreckRoot.gameObject);
            if (ui.labels) ui.labels.gameObject.SetActive(true);
            if (surgeSeaM) { surgeSeaM.SetColor("_BaseColor", Color.white); surgeSwellM.SetColor("_BaseColor", Color.white); }
            wreck = false;
        }
        async Task UnWreckFx()
        {
            if (!wreck) return;
            int t = tok; intro = true;
            var root = (RectTransform)ui.labels.parent; float H = root.rect.height;
            SurgeLayer(root); var wr = surgeWave.rectTransform; surgeWave.gameObject.SetActive(true);
            wr.sizeDelta = new Vector2(0, H * 1.35f);
            sfx.Play("wave");
            await Fx.Tween(320, p => { if (!surgeWave) return; surgeWave.color = new Color(1, 1, 1, p); wr.anchoredPosition = new Vector2(0, -1.08f * H + 0.135f * H); });
            if (t != tok) return;
            UnWreck(); seaLift = 0; PlaceSea(SurgeZ1, 0); surgeHold = false;
            sfx.Play("cheer"); jumpT = Fx.Now;
            await Fx.Tween(700, p => { if (!surgeWave) return; surgeWave.color = new Color(1, 1, 1, 1 - p); wr.anchoredPosition = new Vector2(0, -1.08f * H + 0.135f * H + 1.3f * H * p * p); });
            if (surgeWave) surgeWave.gameObject.SetActive(false);
            if (t == tok) intro = false;
        }

        // 巨浪墙 + 浪花、气泡挂的那层：在场景标签上面、界面下面（关卡名、倒计时、金币、按钮都压在浪上）
        void SurgeLayer(RectTransform root)
        {
            if (surgeWave) return;
            surgeWave = UIKit.Rect("tsunami", root).gameObject.AddComponent<RawImage>(); surgeWave.texture = TsunamiTex(); surgeWave.raycastTarget = false;
            var wr = surgeWave.rectTransform; wr.anchorMin = new Vector2(0, 1); wr.anchorMax = new Vector2(1, 1); wr.pivot = new Vector2(0.5f, 0); wr.offsetMin = wr.offsetMax = Vector2.zero;
            wr.SetSiblingIndex(ui.labels.GetSiblingIndex() + 1);
            surgeFx = UIKit.Rect("tsunamiFx", root).Stretch(); surgeFx.SetSiblingIndex(wr.GetSiblingIndex() + 1);
            surgeWave.gameObject.SetActive(false);
        }
        // 浪尖甩出来的水花（画布坐标，原点在屏幕中心）
        void Spray(RectTransform root, Vector2 at)
        {
            var d = UIKit.Img(surgeFx, "sp", UIKit.Circle, Random.value < 0.3f ? UIKit.Hex("c8f4ff") : Color.white).rectTransform;
            d.anchorMin = d.anchorMax = new Vector2(0.5f, 0.5f); float s = Random.Range(8f, 22f); d.sizeDelta = new Vector2(s, s);
            var v = new Vector2(Random.Range(-160f, 160f), Random.Range(-520f, -240f)); var p0 = at; var im = d.GetComponent<Image>();
            Fx.Run(async () =>
            {
                await Fx.Tween(520, q => { if (!d) return; d.anchoredPosition = p0 + v * q + new Vector2(0, 300 * q * q); im.color = new Color(im.color.r, im.color.g, im.color.b, 1 - q); });
                if (d) Destroy(d.gameObject);
            });
        }
        // 被水吞掉时往上冒的气泡
        void Bubble(RectTransform root, int j)
        {
            var cv = root.rect;
            var d = UIKit.Img(surgeFx, "bb", UIKit.Ring, new Color(1, 1, 1, 0.8f)).rectTransform;
            d.anchorMin = d.anchorMax = new Vector2(0.5f, 0.5f); float s = Random.Range(14f, 44f); d.sizeDelta = new Vector2(s, s);
            var p0 = new Vector2(Random.Range(-cv.width / 2, cv.width / 2), Random.Range(-cv.height / 2, cv.height * 0.1f)); float rise = Random.Range(260f, 620f), wob = Random.Range(0, 6f); var im = d.GetComponent<Image>();
            Fx.Run(async () =>
            {
                await Fx.Sleep(j * 18);
                await Fx.Tween(1100, q => { if (!d) return; d.anchoredPosition = p0 + new Vector2(Mathf.Sin(q * 9 + wob) * 12, rise * q); im.color = new Color(1, 1, 1, 0.8f * (1 - q * q)); });
                if (d) Destroy(d.gameObject);
            });
        }

        // 海面：靠岸浅青 → 往外深蓝（v = 0 是岸线那头）
        static Texture2D seaTex;
        static Texture2D SeaTex()
        {
            if (seaTex) return seaTex;
            const int TH = 256; var tx = new Texture2D(4, TH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            Color a = UIKit.Hex("56cfea"), b = UIKit.Hex("2b9ad6"), c = UIKit.Hex("2172b4");
            for (int y = 0; y < TH; y++)
            {
                float v = y / (TH - 1f); var col = v < 0.05f ? Color.Lerp(a, b, v / 0.05f) : Color.Lerp(b, c, Mathf.Clamp01((v - 0.05f) / 0.3f));
                col.a = 0.97f; for (int x = 0; x < 4; x++) tx.SetPixel(x, y, col);
            }
            tx.Apply(); return seaTex = tx;
        }
        // 涌浪纹（四方连续）：一道道淡淡的弯白线，平铺在海面上往岸边推
        static Texture2D swellTex;
        static Texture2D SwellTex()
        {
            if (swellTex) return swellTex;
            const int N = 128; var tx = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N, v = y / (float)N;
                    float l1 = Mathf.Repeat(v + 0.06f * Mathf.Sin(u * Mathf.PI * 2) + 0.025f * Mathf.Sin(u * Mathf.PI * 6 + 1), 1);
                    float a1 = Mathf.Clamp01(1 - Mathf.Abs(l1 - 0.5f) / 0.035f) * (0.55f + 0.45f * Mathf.Sin(u * Mathf.PI * 4));
                    float l2 = Mathf.Repeat(v + 0.5f + 0.05f * Mathf.Sin(u * Mathf.PI * 2 + 2), 1);
                    float a2 = Mathf.Clamp01(1 - Mathf.Abs(l2 - 0.5f) / 0.025f) * (0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 2 + 3));
                    tx.SetPixel(x, y, new Color(1, 1, 1, 0.32f * Mathf.Max(0, Mathf.Max(a1, a2))));
                }
            tx.Apply(); return swellTex = tx;
        }
        // 浪沫带（左右可以无缝平铺）：靠岸那头是参差的白浪沫边，往海里过渡到海面的浅青
        static Texture2D foamTex;
        static Texture2D FoamTex()
        {
            if (foamTex) return foamTex;
            const int TW = 256, TH = 64; var tx = new Texture2D(TW, TH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            var r = new Rng(11); Color sea = UIKit.Hex("56cfea");
            for (int x = 0; x < TW; x++)
            {
                float u = x / (float)TW, e = 0.3f + 0.14f * Mathf.Sin(u * Mathf.PI * 2 * 3) + 0.06f * Mathf.Sin(u * Mathf.PI * 2 * 7 + 1);
                for (int y = 0; y < TH; y++)
                {
                    float v = y / (TH - 1f); Color c;
                    if (v < e - 0.05f) c = new Color(1, 1, 1, 0);
                    else if (v < e) c = new Color(1, 1, 1, 0.6f * (v - e + 0.05f) / 0.05f);
                    else if (v < e + 0.14f) { c = new Color(1, 1, 1, 0.95f); if (r.Next() < 0.12) c = new Color(0.82f, 0.95f, 1, 0.95f); }
                    else { float k = Mathf.Clamp01((v - e - 0.14f) / 0.3f); c = Color.Lerp(Color.white, sea, k); c.a = 0.96f; if (k < 0.7f && r.Next() < 0.1) c = new Color(1, 1, 1, 0.95f); }
                    tx.SetPixel(x, y, c);
                }
            }
            tx.Apply(); return foamTex = tx;
        }
        // 海啸巨浪墙（v = 0 在下沿，左右无缝）：卡通浪头——下沿是一串往下鼓的圆浪沫（中间尖尖往上收），白浪沫里有淡青的泡泡，
        // 往上是亮青浪脸 → 深蓝 → 墨蓝，带几道弯弯的浪纹
        static Texture2D tsunamiTex;
        static Texture2D TsunamiTex()
        {
            if (tsunamiTex) return tsunamiTex;
            const int TW = 512, TH = 768; const float ASP = 2.4f;   // 一个像素在屏幕上大概是宽的 ASP 倍高（画泡泡用）
            var px = new Color32[TW * TH];
            Color face = UIKit.Hex("8ae8f6"), mid = UIKit.Hex("2a95d3"), deep = UIKit.Hex("174f86"), top = UIKit.Hex("10345e"), bub = UIKit.Hex("cdf3fb");
            float Lobe(float u, float n) { float f = Mathf.Repeat(u * n, 1) * 2 - 1; return Mathf.Sqrt(Mathf.Max(0, 1 - f * f)); }
            var bot = new float[TW]; var fo = new float[TW];
            for (int x = 0; x < TW; x++)
            {
                float u = x / (float)TW;
                bot[x] = 0.075f - 0.045f * Lobe(u, 5) - 0.012f * Lobe(u + 0.37f, 11);                // 浪沫下沿：5 个大圆鼓 + 小圆鼓
                fo[x] = bot[x] + 0.075f + 0.02f * Mathf.Sin(u * Mathf.PI * 2 * 3 + 0.7f) + 0.01f * Mathf.Sin(u * Mathf.PI * 2 * 8);   // 浪沫上沿
            }
            var r = new Rng(21); var holes = new List<Vector3>();
            for (int i = 0; i < 70; i++) { float u = (float)r.Next(); int x = (int)(u * TW); holes.Add(new Vector3(u, Mathf.Lerp(bot[x] + 0.012f, fo[x] - 0.008f, (float)r.Next()), Mathf.Lerp(0.006f, 0.016f, (float)r.Next()))); }
            for (int y = 0; y < TH; y++)
                for (int x = 0; x < TW; x++)
                {
                    float u = x / (float)TW, v = y / (TH - 1f); Color c;
                    if (v < bot[x] - 0.004f) c = new Color(1, 1, 1, 0);
                    else if (v < bot[x]) c = new Color(1, 1, 1, (v - bot[x] + 0.004f) / 0.004f);
                    else if (v < fo[x])
                    {
                        c = Color.white;
                        foreach (var h in holes)
                        {
                            float du = Mathf.Abs(u - h.x); du = Mathf.Min(du, 1 - du);
                            float dx = du * TW / ASP / TH, d = Mathf.Sqrt(dx * dx + (v - h.y) * (v - h.y));
                            if (d < h.z) { c = Color.Lerp(bub, Color.white, Mathf.Clamp01((d / h.z - 0.75f) * 4)); break; }
                        }
                    }
                    else
                    {
                        float k = v - fo[x];
                        c = k < 0.05f ? Color.Lerp(UIKit.Hex("d8f8fd"), face, k / 0.05f) : k < 0.22f ? Color.Lerp(face, mid, (k - 0.05f) / 0.17f) : k < 0.55f ? Color.Lerp(mid, deep, (k - 0.22f) / 0.33f) : Color.Lerp(deep, top, Mathf.Clamp01((k - 0.55f) / 0.3f));
                        float band = Mathf.Repeat(v * 7 + 0.05f * Mathf.Sin(u * Mathf.PI * 2 * 2 + v * 5), 1);
                        c = Color.Lerp(c, Color.white, 0.22f * Mathf.Clamp01(1 - Mathf.Abs(band - 0.5f) / 0.03f) * Mathf.Clamp01(1.2f - k * 1.6f));
                    }
                    c.a *= c.a > 0.99f ? 0.97f : 1;
                    px[y * TW + x] = c;
                }
            var tx = new Texture2D(TW, TH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            tx.SetPixels32(px); tx.Apply(); return tsunamiTex = tx;
        }
    }
}
