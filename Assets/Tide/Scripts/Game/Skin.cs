// 界面皮肤：codex 照用户定下的 4 张概念图（art/ai/concept）生成的贴图，放在 Resources/UI/skin/*.png。
//   Skin.Img(parent, "btn_green")          贴一张（九宫格的会按格子拉伸，圆角不变形）
//   Skin.Has("btn_green")                  贴图在不在（不在的话调用方退回原来的程序画法）
// 九宫格的边距不写死像素：按图的尺寸算（长条按钮的左右圆头 = 半个高度，面板的木框 = 短边的 15%），
// 换一张同风格、不同分辨率的图不用改代码。
// 字体：英文 Fredoka Bold、中文源流圆体 Heavy（Resource Han Rounded CN，都是 SIL OFL 开源可商用，Resources/Fonts）。
// 中文字体是瘦身过的（GB2312 全部 + 代码里用到的字，art/fonts_dl，scratchpad 的 subset.js），行高参数改成以汉字为中心上下对称（metrics.js），
// 不改的话 Unity 的 Text 里汉字会偏下。代码里新加了生僻字要重新瘦身一次。原来的霞鹜漫黑在 art/fonts_unused。
// 立体字：Outline8 = 加粗 + 八个方向的厚描边 + 下方一道投影（Unity 自带的 Outline 只有四个方向，粗了会露缝）。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Tide
{
    public static class Skin
    {
        // 九宫格方式：pill = 左右两头是半圆的长条（按钮、进度条、牌子），按控件高度缩放圆头；
        //             frame = 四边一样粗的框（弹窗），框在界面上固定 FRAME_UI 个单位粗；
        //             其余 = 整张图等比例贴（图标）
        static readonly Dictionary<string, string> MODE = new Dictionary<string, string> {
            { "btn_green", "pill" }, { "btn_gold", "pill" }, { "btn_purple", "pill" }, { "btn_cream", "pill" }, { "btn_grey", "pill" }, { "btn_blue", "pill" },
            { "bar_track", "pill" }, { "bar_green", "pill" }, { "bar_storm", "pill" }, { "bar_gold", "pill" },
            { "plate_cream", "pill" }, { "plate_wood", "pill" }, { "strip_cream", "pill" }, { "ribbon_gold", "ribbon" }, { "ribbon_blue", "ribbon" }, { "toast_bg", "pill" }, { "bubble", "pill" },
            { "panel_wood", "frame" } };   // btn_square 是固定形状的方钮，整张贴（当边框切开会变成菱形）
        const float FRAME_UI = 30;      // 弹窗木框在界面上的粗细

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, float> frameMul = new Dictionary<string, float>();

        public static bool Has(string name) => Get(name) != null;

        public static Sprite Get(string name)
        {
            if (cache.TryGetValue(name, out var s)) return s;
            var t = Resources.Load<Texture2D>("UI/skin/" + name);
            if (t)
            {
                MODE.TryGetValue(name, out var mode);
                Vector4 border = Vector4.zero;
                if (mode == "pill") { float r = Mathf.Min(t.height * 0.5f - 1, t.width * 0.45f); border = new Vector4(r, t.height * 0.5f - 1, r, t.height * 0.5f - 1); }
                else if (mode == "ribbon") { float r = t.width * 0.22f; border = new Vector4(r, t.height * 0.5f - 1, r, t.height * 0.5f - 1); }
                else if (mode == "frame") { float r = Mathf.Min(t.width, t.height) * 0.16f; border = new Vector4(r, r, r, r); frameMul[name] = r / FRAME_UI; }
                s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, border);
                s.name = name;
            }
            return cache[name] = s;
        }

        public static Image Img(Transform parent, string name, Color? tint = null)
        {
            var r = UIKit.Rect(name, parent);
            var im = r.gameObject.AddComponent<Image>();
            im.sprite = Get(name); im.color = tint ?? Color.white; im.raycastTarget = false;
            MODE.TryGetValue(name, out var mode);
            if (mode == "pill" || mode == "ribbon") { im.type = Image.Type.Sliced; r.gameObject.AddComponent<PillFit>(); }
            else if (mode == "frame") { im.type = Image.Type.Sliced; im.pixelsPerUnitMultiplier = frameMul.TryGetValue(name, out var m) ? m : 1; }
            else im.preserveAspect = true;
            return im;
        }

        // 长条九宫格：两头的半圆跟着控件高度走（控件多高，圆头就是多高的半圆）
        public class PillFit : MonoBehaviour
        {
            Image im; RectTransform rt;
            void Awake() { im = GetComponent<Image>(); rt = (RectTransform)transform; Fit(); }
            void OnRectTransformDimensionsChange() { if (im) Fit(); }
            void Fit() { if (im.sprite && rt.rect.height > 1) im.pixelsPerUnitMultiplier = im.sprite.rect.height / rt.rect.height; }
        }

        // ---- 字体 ----
        static Font en, zh;
        public static Font Font
        {
            get
            {
                if (!en) en = Resources.Load<Font>("Fonts/Fredoka-Bold");
                if (!zh) zh = Resources.Load<Font>("Fonts/ResourceHanRoundedCN-Heavy");
                return Loc.En ? (en ? en : zh) : zh;
            }
        }
    }

    // 加粗 + 八个方向的厚描边 + 一道下方投影（概念图里按钮字、标题字的立体感就靠这个）
    //   fat：先把字本身往八个方向各复制一份（用字自己的颜色），笔画变粗、转角变圆；描边在加粗后的外面
    //   color.a = 0 就只加粗不描边（普通的字）
    public class Outline8 : BaseMeshEffect
    {
        public Color color = new Color(0.1f, 0.25f, 0.4f, 1);
        public float size = 2.5f;
        public Color shadow = new Color(0, 0, 0, 0);
        public float drop = 3;
        public float fat = 0;

        static readonly List<UIVertex> buf = new List<UIVertex>();
        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            buf.Clear(); vh.GetUIVertexStream(buf);
            int n = buf.Count;
            var outl = new List<UIVertex>(n * 10);
            void Copy(Vector2 off, Color? c)
            {
                for (int i = 0; i < n; i++)
                {
                    var v = buf[i]; v.position += (Vector3)off;
                    if (c.HasValue) { var a = v.color.a / 255f; Color32 cc = c.Value; cc.a = (byte)(c.Value.a * a * 255); v.color = cc; }
                    outl.Add(v);
                }
            }
            float reach = size + fat;
            if (shadow.a > 0) Copy(new Vector2(0, -drop - size * 0.5f - fat), shadow);   // 投影贴着描边往下一点，偏太多会像重影
            if (color.a > 0) for (int k = 0; k < 8; k++) { float ang = k * Mathf.PI / 4; Copy(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * reach, color); }
            if (fat > 0) for (int k = 0; k < 8; k++) { float ang = k * Mathf.PI / 4 + Mathf.PI / 8; Copy(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * fat, null); }
            outl.AddRange(buf);
            vh.Clear(); vh.AddUIVertexTriangleStream(outl);
        }
    }
}
