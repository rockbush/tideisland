// 界面工具：运行时生成圆形 / 圆角矩形贴图，搭 uGUI 控件。网页版的配色：奶油底 + 棕色描边 + 下方实心阴影。
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tide
{
    public static class UIKit
    {
        public static readonly Color Brown = Hex("7a4d21"), Cream = Hex("fff8e6"), Ink = Hex("3b2a1a"), Green = Hex("3fb950"),
            Purple = Hex("8a5cff"), Gold = Hex("ffb81f"), Blue = Hex("14608f");
        static Font font;
        static Sprite circle, round, ring, soft;

        public static Color Hex(string h, float a = 1)
        {
            ColorUtility.TryParseHtmlString("#" + h, out var c);
            c.a = a;
            return c;
        }

        public static Font Font
        {
            get
            {
                if (font) return font;
                try
                {
                    font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "PingFang SC", "Heiti SC", "Noto Sans CJK SC",
                        "Noto Sans SC", "Source Han Sans SC", "Droid Sans Fallback", "Arial" }, 40);
                }
                catch (Exception) { }
                if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        static Sprite MakeSprite(int size, Func<float, float, float> alpha, int border)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(x + 0.5f, y + 0.5f)) * 255));
            t.SetPixels32(px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

        public static Sprite Circle => circle ? circle : circle = MakeSprite(128, (x, y) => 64 - Mathf.Sqrt((x - 64) * (x - 64) + (y - 64) * (y - 64)), 0);
        public static Sprite Ring => ring ? ring : ring = MakeSprite(128, (x, y) => { float d = Mathf.Sqrt((x - 64) * (x - 64) + (y - 64) * (y - 64)); return Mathf.Min(64 - d, d - 50); }, 0);
        public static Sprite Soft => soft ? soft : soft = MakeSprite(128, (x, y) => { float d = Mathf.Sqrt((x - 64) * (x - 64) + (y - 64) * (y - 64)) / 64; return Mathf.Clamp01((d - 0.45f) / 0.55f); }, 0);
        // 圆角矩形（9 宫格，圆角半径 24px）
        public static Sprite Round => round ? round : round = MakeSprite(64, (x, y) =>
        {
            float r = 24, cx = Mathf.Clamp(x, r, 64 - r), cy = Mathf.Clamp(y, r, 64 - r);
            return r - Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        }, 26);

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Place(this RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size;
            return r;
        }

        public static RectTransform Stretch(this RectTransform r, float l = 0, float b = 0, float rr = 0, float t = 0)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rr, -t);
            return r;
        }

        public static Image Img(Transform parent, string name, Sprite s, Color c, bool raycast = false)
        {
            var r = Rect(name, parent);
            var im = r.gameObject.AddComponent<Image>();
            im.sprite = s; im.color = c; im.raycastTarget = raycast;
            if (s == Round) im.type = Image.Type.Sliced;
            return im;
        }

        public static Text Txt(Transform parent, string name, string s, int size, Color c, TextAnchor a = TextAnchor.MiddleCenter, bool outline = false)
        {
            var r = Rect(name, parent);
            var t = r.gameObject.AddComponent<Text>();
            var sf = Skin.Font;
            t.font = sf ? sf : Font; t.text = s; t.fontSize = size; t.color = c; t.alignment = a; t.raycastTarget = false;
            t.fontStyle = sf ? FontStyle.Normal : FontStyle.Bold; t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            // 所有字都稍微加粗一圈（中文字体已经是特粗，只加一点点，多了笔画会糊成一团）；要描边的再加厚边和投影
            var o8 = r.gameObject.AddComponent<Outline8>(); o8.color = Color.clear; o8.fat = size * (Loc.En ? 0.02f : 0.008f);
            if (outline) { o8.color = Blue; o8.size = 2.6f; o8.shadow = Hex("0b3f63"); o8.drop = 3; }
            return t;
        }

        // 卡通面板：下方一条实心棕色阴影 + 棕色描边 + 填充色
        public static RectTransform Panel(Transform parent, string name, Color fill, float shadow = 4, float border = 3)
        {
            var root = Rect(name, parent);
            Img(root, "shadow", Round, Brown).rectTransform.Stretch(0, -shadow, 0, shadow);
            Img(root, "border", Round, Brown).rectTransform.Stretch();
            var f = Img(root, "fill", Round, fill);
            f.rectTransform.Stretch(border, border, border, border);
            return root;
        }

        public static RectTransform Disc(Transform parent, string name, Color fill, float shadow = 5, float border = 3)
        {
            var root = Rect(name, parent);
            Img(root, "shadow", Circle, Brown).rectTransform.Stretch(0, -shadow, 0, shadow);
            Img(root, "border", Circle, Brown).rectTransform.Stretch();
            Img(root, "fill", Circle, fill).rectTransform.Stretch(border, border, border, border);
            return root;
        }

        // 填充色那一层：程序画的面板/圆盘是子物体 "fill"；贴图做的就是它自己那张图
        public static Image Fill(this RectTransform panel) { var t = panel.Find("fill"); return t ? t.GetComponent<Image>() : panel.GetComponent<Image>(); }
        public static void raycastTargetOn(this RectTransform r) { var g = r.GetComponent<Graphic>(); if (g) g.raycastTarget = true; }

        public static Action Clicked;   // 任何按钮按下都会调（用来统一加震动）
        // 按下沉一下的按钮（整块面板就是热区）
        public static Button Btn(RectTransform r, Action onClick)
        {
            // 点击区：物体上已经有图（贴图按钮）就直接用它接点击，没有才加一张透明的
            var g = r.GetComponent<Graphic>();
            if (g) g.raycastTarget = true;
            else { var hit = r.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0); }
            var b = r.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { Clicked?.Invoke(); onClick(); });
            r.gameObject.AddComponent<Press>();
            return b;
        }

        public class Press : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
        {
            Vector3 s0;
            void Awake() { s0 = transform.localScale; }
            public void OnPointerDown(PointerEventData e) { transform.localScale = s0 * 0.93f; }
            public void OnPointerUp(PointerEventData e) { transform.localScale = s0; }
        }
    }
}
