// 照概念图（art/ai/concept）搭界面用的小零件：贴图按钮、立体字、星星排、弹窗的丝带标题。
// 每个零件都先看 Skin.Has(贴图名)：贴图还没生成就退回原来程序画的样子，游戏照样能跑。
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Tide
{
    public partial class Game
    {
        // 按钮颜色 → 贴图、字的颜色、字的描边色（概念图：绿底奶白字深绿边、金底棕字、紫底白字、奶白底棕字）
        static readonly Dictionary<string, (string skin, string fg, string edge)> BTN = new Dictionary<string, (string, string, string)> {
            { "3fb950", ("btn_green", "fff8e6", "1f6b18") }, { "ffb81f", ("btn_gold", "6b3d0c", "") }, { "8a5cff", ("btn_purple", "ffffff", "4a2a9a") },
            { "fff8e6", ("btn_cream", "6b4a2a", "") }, { "14608f", ("btn_blue", "ffffff", "1b4f86") }, { "b9b0a0", ("btn_grey", "ffffff", "6d6a64") } };

        // 立体字：厚描边 + 下方投影
        static Text Styled(Text t, Color edge, float size = 2.4f, float drop = 2.5f)
        {
            var o = t.GetComponent<Outline8>(); if (!o) o = t.gameObject.AddComponent<Outline8>();
            o.color = edge; o.size = size; o.shadow = new Color(edge.r * 0.6f, edge.g * 0.6f, edge.b * 0.6f, 0.9f); o.drop = drop;
            return t;
        }

        // 贴图长条按钮（没有贴图就用原来的面板）。返回按钮的根节点
        RectTransform SkinBtn(Transform parent, string fill, string text, int size, Action onClick, out Text label)
        {
            var r = UIKit.Rect("btn", parent);
            BTN.TryGetValue(fill, out var s);
            if (s.skin != null && Skin.Has(s.skin))
            {
                Skin.Img(r, s.skin).rectTransform.Stretch();
                label = UIKit.Txt(r, "t", text, size, UIKit.Hex(s.fg)); label.rectTransform.Stretch(14, 4, 14, 0);
                if (s.edge.Length > 0) Styled(label, UIKit.Hex(s.edge), 2f, 1.2f);
                if (fill == "8a5cff" && Skin.Has("icon_video") && text.Length > 0)        // 看广告的按钮：字前面一个视频图标
                {
                    var ic = Skin.Img(r, "icon_video"); ic.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, 2), new Vector2(30, 30));
                    label.rectTransform.offsetMin = new Vector2(46, 4);
                }
            }
            else
            {
                var p = UIKit.Panel(r, "p", UIKit.Hex(fill), 5); p.Stretch(0, 5, 0, 0);
                label = UIKit.Txt(p, "t", text, size, UIKit.Hex(s.fg ?? "ffffff")); label.rectTransform.Stretch(6, 0, 6, 0);
            }
            UIKit.Btn(r, () => onClick?.Invoke());
            return r;
        }

        // 一排星星（金 n 颗，其余灰）
        List<Image> StarRow(Transform parent, int n, float size, float gap, Vector2 anchor, Vector2 pos)
        {
            var row = UIKit.Rect("stars", parent).Place(anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size * 3 + gap * 2, size));
            var list = new List<Image>();
            for (int k = 0; k < 3; k++)
            {
                Image im;
                if (Skin.Has("star_gold")) { im = Skin.Img(row, k < n ? "star_gold" : "star_grey"); }
                else { var t = UIKit.Txt(row, "s", "★", (int)size, k < n ? UIKit.Hex("ffb81f") : UIKit.Hex("d8cfbd")); im = null; t.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(k * (size + gap), 0), new Vector2(size, size)); continue; }
                im.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(k * (size + gap), k == 1 ? size * 0.12f : 0), new Vector2(size, size));
                list.Add(im);
            }
            return list;
        }

        // 弹窗顶上的金色丝带标题（挂在弹窗框上，不进排版）
        // word = 贴图字的名字（t_<word>_zh / _en，codex 画的奶白字深棕边）：有贴图就整张贴在丝带上，没有就写字
        void ModalTitle(string text, string word = null, string ribbon = "ribbon_gold")
        {
            if (!Skin.Has(ribbon)) { MText(text, 28, UIKit.Brown); return; }
            var r = Skin.Img(ui.box, ribbon).rectTransform;
            r.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -4), new Vector2(260, 58));
            string tn = word == null ? null : "t_" + word + (Loc.En ? "_en" : "_zh");     // 注意不是 WordArt()：那个会再加 w_ 前缀
            var sp = tn != null && Skin.Has(tn) ? Skin.Get(tn) : null;
            if (sp != null)
            {
                // 丝带是往上拱的弧形：正中那一列的带面中心在图片从上往下 31% 处（不是 50%），字要跟着带面往上挪
                var im = UIKit.Img(r, "word", sp, Color.white); im.preserveAspect = true; im.rectTransform.Stretch(36, 22, 36, 0);
            }
            else
            {
                var t = UIKit.Txt(r, "t", text, 24, UIKit.Hex("5a3410")); t.rectTransform.Stretch(34, 25, 34, 3);   // 同上：对准拱起来的带面
                t.resizeTextForBestFit = true; t.resizeTextMinSize = 14; t.resizeTextMaxSize = 24;
            }
            MSpace(18);
        }

        // 弹窗右上角的木头关闭钮
        void ModalClose()
        {
            if (!Skin.Has("btn_close")) return;
            var c = Skin.Img(ui.box, "btn_close").rectTransform;
            c.Place(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-14, -14), new Vector2(46, 46));
            UIKit.Btn(c, () => { sfx.Play("click"); HideModal(); });
        }

        // 圆形图标按钮（菜单、返回…）：有贴图就整张贴
        RectTransform IconBtn(Transform parent, string skin, Vector2 anchor, Vector2 pos, float size, Action onClick, string fallback)
        {
            RectTransform r;
            if (Skin.Has(skin)) { r = Skin.Img(parent, skin).rectTransform; r.Place(anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size)); }
            else { r = UIKit.Disc(parent, skin, UIKit.Cream, 4).Place(anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size)); UIKit.Txt(r, "t", fallback, (int)(size * 0.45f), UIKit.Brown).rectTransform.Stretch(); }
            UIKit.Btn(r, () => onClick());
            return r;
        }
    }
}
