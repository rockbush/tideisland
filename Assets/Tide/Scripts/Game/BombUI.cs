// 火药船的界面提示（2026-11-06 用户：船头上的牌子不够醒目，界面上要有专门的提示）
//   ① 刚出现：屏幕中间弹一块黄黑警戒条的警告「危险！火药船」+「再开走 N 艘船它就爆炸，炸了就输！」，停一下缩小飞到风暴条下面
//   ② 风暴条下面常驻一条警示条：炸弹图标 +「再开走 N 艘船就爆炸！」；剩 2 艘变红闪、晃；剩 1 艘「最后 1 艘！快开走它！」闪得更快
//   ③ 火药船周围的水面一圈红色光环一直往外扩（不管它埋在哪儿都一眼找得到）；点警示条 → 光环和船一起跳一下
//   ④ 剩 2 艘起屏幕四周泛红，剩 1 艘一下一下地闪
//   拆弹：警示条变绿「拆弹成功！」再收起；炸了：都收掉（后面是整个港湾炸烂）
using UnityEngine;
using UnityEngine.UI;
using static Tide.Loc;

namespace Tide
{
    public partial class Game
    {
        RectTransform bombHud, bombRing; Image bombHudBg, bombVig; Text bombHudT; int bombHudB = -1, bombHudN = -1; float bombAlertAt;
        public bool BombAlertShown => bombAlertAt > 0;
        static Texture2D stripeTex, vigTex;

        void BombHudClear()
        {
            if (bombHud) Destroy(bombHud.gameObject);
            if (bombRing) Destroy(bombRing.gameObject);
            if (bombVig) Destroy(bombVig.gameObject);
            bombHud = bombRing = null; bombVig = null; bombHudT = null; bombHudBg = null; bombHudB = -1; bombHudN = -1; bombAlertAt = 0;
        }

        // 一颗小炸弹图标：黑球 + 高光 + 引信 + 火星
        RectTransform BombIcon(Transform parent, float s)
        {
            var r = UIKit.Rect("bomb", parent); r.sizeDelta = new Vector2(s, s);
            var fuse = UIKit.Img(r, "fuse", UIKit.Round, UIKit.Hex("d9b47a")).rectTransform;
            fuse.anchorMin = fuse.anchorMax = new Vector2(0.5f, 0.5f); fuse.sizeDelta = new Vector2(s * 0.14f, s * 0.36f); fuse.anchoredPosition = new Vector2(s * 0.3f, s * 0.42f); fuse.localRotation = Quaternion.Euler(0, 0, -35);
            var ball = UIKit.Img(r, "ball", CircleSprite(false), UIKit.Hex("2a2a30")).rectTransform; ball.anchorMin = ball.anchorMax = new Vector2(0.5f, 0.5f); ball.sizeDelta = new Vector2(s * 0.86f, s * 0.86f); ball.anchoredPosition = new Vector2(0, -s * 0.05f);
            var hl = UIKit.Img(r, "hl", CircleSprite(false), new Color(1, 1, 1, 0.4f)).rectTransform; hl.anchorMin = hl.anchorMax = new Vector2(0.5f, 0.5f); hl.sizeDelta = new Vector2(s * 0.22f, s * 0.22f); hl.anchoredPosition = new Vector2(-s * 0.18f, s * 0.12f);
            var sp = UIKit.Img(r, "spark", CircleSprite(false), UIKit.Hex("ffd23a")).rectTransform; sp.anchorMin = sp.anchorMax = new Vector2(0.5f, 0.5f); sp.sizeDelta = new Vector2(s * 0.28f, s * 0.28f); sp.anchoredPosition = new Vector2(s * 0.42f, s * 0.6f);
            foreach (var g in r.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            return r;
        }
        static Texture2D StripeTex()
        {
            if (stripeTex) return stripeTex;
            int W = 32; stripeTex = new Texture2D(W, W, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var y = UIKit.Hex("ffc61a"); var k = UIKit.Hex("1c1c1c");
            for (int i = 0; i < W; i++) for (int j = 0; j < W; j++) stripeTex.SetPixel(i, j, ((i + j) % W) < W / 2 ? y : k);
            stripeTex.Apply(); return stripeTex;
        }
        static Texture2D VigTex()
        {
            if (vigTex) return vigTex;
            int W = 64, H = 128; vigTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int i = 0; i < W; i++) for (int j = 0; j < H; j++)
            {
                float dx = Mathf.Min(i, W - 1 - i) / (W * 0.5f), dy = Mathf.Min(j, H - 1 - j) / (H * 0.5f);
                float d = Mathf.Min(dx * 1.0f, dy * 2.0f);   // 离最近的边多远（0 = 贴边）
                float a = Mathf.Clamp01(1 - d / 0.38f); a *= a;
                vigTex.SetPixel(i, j, new Color(1, 0.1f, 0.05f, a));
            }
            vigTex.Apply(); return vigTex;
        }
        string BombHudText(int n) => n <= 1 ? T("最后 1 艘！快把它开走！") : n <= 2 ? F("只剩 {0} 艘！快把它开走！", "<size=24><color=#ffe14a>" + n + "</color></size>") : F("再开走 {0} 艘船就爆炸！", "<size=24><color=#ffd23a>" + n + "</color></size>");

        // 火药船出现了（BombMake 调）
        void BombAlert(int b, int left)
        {
            BombHudClear();
            bombHudB = b; bombHudN = left;
            // 风暴条下面的警示条（先藏着，警告飞过来再出现）
            bombHud = UIKit.Rect("bombHud", ui.top).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, -2), new Vector2(262, 42));
            bombHudBg = UIKit.Img(bombHud, "bg", UIKit.Round, UIKit.Hex("231818", 0.94f), true); bombHudBg.rectTransform.Stretch();
            var edge = UIKit.Rect("edge", bombHud).Stretch(-2, -2, -2, -2); edge.SetAsFirstSibling(); UIKit.Img(edge, "e", UIKit.Round, UIKit.Hex("ffc61a")).rectTransform.Stretch();
            var ic = BombIcon(bombHud, 34); ic.anchorMin = ic.anchorMax = new Vector2(0, 0.5f); ic.anchoredPosition = new Vector2(24, -1);
            bombHudT = UIKit.Txt(bombHud, "t", BombHudText(left), 15, Color.white, TextAnchor.MiddleLeft); bombHudT.rectTransform.Stretch(46, 0, 8, 0); bombHudT.fontStyle = FontStyle.Bold; bombHudT.supportRichText = true;
            Styled(bombHudT, new Color(0, 0, 0, 0.7f), 1.3f, 1f);
            var btn = bombHud.gameObject.AddComponent<Button>(); btn.transition = Selectable.Transition.None; btn.onClick.AddListener(BombPoint);
            bombHud.localScale = Vector3.one * 0.01f; bombHud.gameObject.SetActive(false);
            // 船周围的红光环
            bombRing = OnCanvas("bombRing"); bombRing.SetAsFirstSibling();
            var ri = UIKit.Img(bombRing, "o", CircleSprite(true), UIKit.Hex("ff2a1a")); ri.rectTransform.Stretch(); ri.raycastTarget = false;
            var ri2 = UIKit.Img(bombRing, "i", CircleSprite(true), UIKit.Hex("ffd23a")); ri2.rectTransform.Stretch(14, 8, 14, 8); ri2.raycastTarget = false;
            // 屏幕四周泛红
            bombVig = UIKit.Rect("bombVig", ui.fx).Stretch().gameObject.AddComponent<Image>();
            bombVig.sprite = Sprite.Create(VigTex(), new Rect(0, 0, 64, 128), new Vector2(0.5f, 0.5f)); bombVig.raycastTarget = false; bombVig.color = new Color(1, 1, 1, 0); bombVig.transform.SetAsFirstSibling();
            int t = tok, start = st.gone;
            Fx.Run(async () =>
            {
                await Fx.Sleep(start == 0 ? 1500 : 350); if (t != tok || bombHudB != b) return;
                while (OnScreen || ModalOpen) { await Fx.Sleep(200); if (t != tok || bombHudB != b) return; }
                await BombAlertFx(Rules.BombLeft(st, b), t, b);
            });
        }

        // 屏幕中间的大警告：黄黑警戒条 + 炸弹 +「危险！火药船」，停 1.5 秒，缩小飞到警示条的位置
        async System.Threading.Tasks.Task BombAlertFx(int left, int t, int b)
        {
            var box = OnCanvas("bombAlert"); box.sizeDelta = new Vector2(350, 132); box.anchoredPosition = new Vector2(0, 90);
            UIKit.Img(box, "bg", UIKit.Round, UIKit.Hex("231818", 0.96f)).rectTransform.Stretch();
            foreach (var top in new[] { true, false })
            {
                var s = UIKit.Rect("stripe", box); s.anchorMin = new Vector2(0, top ? 1 : 0); s.anchorMax = new Vector2(1, top ? 1 : 0); s.pivot = new Vector2(0.5f, top ? 1 : 0); s.sizeDelta = new Vector2(-12, 14); s.anchoredPosition = new Vector2(0, top ? -6 : 6);
                var ri = s.gameObject.AddComponent<RawImage>(); ri.texture = StripeTex(); ri.uvRect = new Rect(0, 0, 330 / 28f, 0.5f); ri.raycastTarget = false;
            }
            var ic = BombIcon(box, 64); ic.anchorMin = ic.anchorMax = new Vector2(0, 0.5f); ic.anchoredPosition = new Vector2(52, -2);
            var title = UIKit.Txt(box, "t", T("危险！火药船"), 30, UIKit.Hex("ffd23a"), TextAnchor.MiddleLeft); title.fontStyle = FontStyle.Bold;
            title.rectTransform.Stretch(96, 58, 8, 22); Styled(title, UIKit.Hex("5a0a00"), 2f, 2f);
            var sub = UIKit.Txt(box, "s", F("再开走 {0} 艘船它就爆炸\n炸了就输，快把它开出港湾！", "<color=#ffd23a>" + left + "</color>"), 15, Color.white, TextAnchor.UpperLeft); sub.supportRichText = true;
            sub.rectTransform.Stretch(96, 20, 8, 66); Styled(sub, new Color(0, 0, 0, 0.7f), 1.2f, 0);
            foreach (var g in box.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            bombAlertAt = Fx.Now;
            sfx.Play("warn", 0.8f, 1f); sfx.Play("warn", 1.2f, 0.7f); Haptics.Play(Haptics.Kind.Medium); shake = Mathf.Max(shake, 0.25f);
            Debug.Log("[火药船] 界面警告：再开走 " + left + " 艘就炸");
            var cg = box.gameObject.AddComponent<CanvasGroup>();
            await Fx.Tween(260, p => { if (box) { box.localScale = Vector3.one * Mathf.Lerp(0.3f, 1, Fx.BackOut(p)); cg.alpha = p; } });
            float hold = 1600, t0 = Fx.Now;
            while (Fx.Now - t0 < hold)
            {
                if (t != tok || !box) { if (box) Destroy(box.gameObject); return; }
                float k = (Fx.Now - t0) / 1000f;
                foreach (var ri in box.GetComponentsInChildren<RawImage>()) ri.uvRect = new Rect(-k * 1.2f, 0, 330 / 28f, 0.5f);   // 警戒条往一边走
                if (ic) ic.localScale = Vector3.one * (1 + 0.08f * Mathf.Sin(k * 18));
                await Fx.Sleep(16);
            }
            if (t != tok || !box) { if (box) Destroy(box.gameObject); return; }
            // 缩小飞到警示条
            Vector2 from = box.anchoredPosition, to = from;
            if (bombHud) { bombHud.gameObject.SetActive(true); var c = new Vector3[4]; bombHud.GetWorldCorners(c); to = ToCanvas(RectTransformUtility.WorldToScreenPoint(null, (c[0] + c[2]) * 0.5f)); }
            await Fx.Tween(340, p => { if (!box) return; float e = p * p; box.anchoredPosition = Vector2.Lerp(from, to, e); box.localScale = Vector3.one * Mathf.Lerp(1, 0.5f, e); cg.alpha = 1 - e * 0.8f; });
            if (box) Destroy(box.gameObject);
            if (t != tok || bombHud == null || bombHudB != b) return;
            sfx.Play("pop"); var h = bombHud;
            await Fx.Tween(260, p => { if (h) h.localScale = Vector3.one * Mathf.Max(0.01f, Fx.BackOut(p)); });
        }

        // 点警示条：光环和船一起跳一下（告诉玩家是哪艘）
        void BombPoint()
        {
            if (bombHudB < 0 || boatV[bombHudB] == null || st.status != "play") return;
            sfx.Play("click"); ShakeBoat(bombHudB, Vector3.up);
            if (bombRing) { var r = bombRing; Fx.Run(() => Fx.Tween(400, p => { if (r) r.localScale = Vector3.one * (1 + 0.6f * Mathf.Sin(p * Mathf.PI)); })); }
        }

        // 每帧：光环跟着船、往外扩；警示条剩 2 艘起变红闪；屏幕四周泛红
        void BombHudTick()
        {
            if (bombHudB < 0) return;
            int n = bombHudN; bool live = st != null && st.status == "play" && !wreck;
            var u = bombHudB < boatV.Length ? boatV[bombHudB] : null;
            if (bombRing)
            {
                bool on = live && u != null && u.g && u.state == "lot";
                bombRing.gameObject.SetActive(on);
                if (on)
                {
                    var sp = cam.WorldToScreenPoint(u.g.transform.position);
                    bombRing.anchoredPosition = ToCanvas(sp);
                    float per = n <= 1 ? 450 : n <= 2 ? 700 : 1100, p = (Fx.Now % per) / per;
                    float w = Mathf.Lerp(60, 150, p);
                    bombRing.sizeDelta = new Vector2(w, w * 0.62f);
                    foreach (var im in bombRing.GetComponentsInChildren<Image>()) { var c = im.color; c.a = 0.95f * (1 - p); im.color = c; }
                }
            }
            if (bombHud && bombHudBg)
            {
                float per = n <= 1 ? 140 : 240, k = 0.5f + 0.5f * Mathf.Sin(Fx.Now / per);
                bombHudBg.color = n <= 2 ? Color.Lerp(UIKit.Hex("231818", 0.94f), UIKit.Hex("d0201a", 0.96f), k) : UIKit.Hex("231818", 0.94f);
                if (n <= 2 && live) bombHud.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Fx.Now / 45f) * (n <= 1 ? 2.5f : 1.2f));
                else bombHud.localRotation = Quaternion.identity;
                bombHud.anchoredPosition = new Vector2(0, btyCard ? -64 : -2);   // 悬赏卡片在左上角同一高度：有它就往下让
            }
            if (bombVig)
            {
                float a = !live ? 0 : n <= 1 ? 0.25f + 0.35f * (0.5f + 0.5f * Mathf.Sin(Fx.Now / 110f)) : n <= 2 ? 0.12f + 0.12f * (0.5f + 0.5f * Mathf.Sin(Fx.Now / 220f)) : 0;
                bombVig.color = new Color(1, 1, 1, Mathf.MoveTowards(bombVig.color.a, a, Fx.Dt * 3));
            }
        }

        // 数字变了（NmSync 调）
        void BombHudNum(int n)
        {
            if (bombHudB < 0 || n == bombHudN) return;
            bombHudN = n;
            if (bombHudT) bombHudT.text = BombHudText(n);
            if (bombHud) Bump(bombHud);
            if (n <= 1) { sfx.Play("warn", 1.5f, 0.8f); Haptics.Play(Haptics.Kind.Medium); }
        }

        // 火药船没了：拆弹 → 警示条变绿「拆弹成功！」再收起；炸了 → 直接收掉
        void BombHudDone(bool ok)
        {
            if (bombRing) Destroy(bombRing.gameObject);
            if (bombVig) Destroy(bombVig.gameObject);
            bombRing = null; bombVig = null; bombHudB = -1;
            var h = bombHud; bombHud = null;
            if (!h) return;
            if (!ok) { Destroy(h.gameObject); return; }
            if (bombHudBg) bombHudBg.color = UIKit.Hex("2f9a3a", 0.96f);
            if (bombHudT) bombHudT.text = T("拆弹成功！");
            h.localRotation = Quaternion.identity; h.gameObject.SetActive(true);
            Fx.Run(async () =>
            {
                await Fx.Tween(300, p => { if (h) h.localScale = Vector3.one * (1 + 0.2f * Mathf.Sin(p * Mathf.PI)); });
                await Fx.Sleep(900);
                await Fx.Tween(250, p => { if (h) h.localScale = Vector3.one * Mathf.Max(0.01f, 1 - p); });
                if (h) Destroy(h.gameObject);
            });
        }
    }
}
