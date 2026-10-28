// 风暴（第 4 关起）：倒计时条 + 天气演出。规则见 Rules.cs 开头（cfg.storm 秒，开走一艘满船 +cfg.bonus 秒）。
// 计时：点第一下才开始走；弹窗、菜单、开始界面/地图、看广告时停。走完 = 输（可以看广告/花金币 +20 秒）。
// 紧张感全靠画面慢慢压上来，不用红屏和警报：天变暗、下雨、偶尔打雷、潮水漫上沙滩、岛民越来越慌、最后变成紧张的音乐。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static Tide.Loc;

namespace Tide
{
    public partial class Game
    {
        const float STORM_REVIVE = 20;
        float stormLeft, stormMax, stormCap, stormK, flashT, thunderT;
        bool stormOn, stormLose, stormWarned;
        RectTransform stormRow, rainRoot, stormCloud; Image stormFill, flashImg; Text stormTxt;
        readonly List<RectTransform> rain = new List<RectTransform>();
        Transform flood; Material floodMat, seaMat;
        // 晴天时的灯光（风暴越近越往暗里拉）
        Color sun0, sky0, eq0, gnd0, bg0; float sunI0;

        void StormStage()
        {
            sun0 = sun.color; sunI0 = sun.intensity; bg0 = cam.backgroundColor;
            sky0 = RenderSettings.ambientSkyColor; eq0 = RenderSettings.ambientEquatorColor; gnd0 = RenderSettings.ambientGroundColor;
            seaMat = seaT.GetComponent<MeshRenderer>().sharedMaterial;
        }

        // ---- 界面：风暴条（顶上关卡名旁边那条，有风暴的关代替救人进度条——剩几个人队伍条上有，不再多占一行，免得港湾被挤小）+ 雨 + 闪电 ----
        void StormUI(RectTransform root)
        {
            // 照概念图：同一条进度条槽里，填充换成蓝→紫的风暴条，末端一朵生气的小乌云，秒数写在条上
            stormRow = UIKit.Rect("storm", ui.qbar).Stretch();
            if (Skin.Has("bar_track")) Skin.Img(stormRow, "bar_track", UIKit.Hex("c9d4ea")).rectTransform.Stretch();
            stormFill = Skin.Has("bar_storm") ? Skin.Img(stormRow, "bar_storm") : UIKit.Img(stormRow, "f", UIKit.Round, UIKit.Hex("8fc3ff"));
            var fr = stormFill.rectTransform; fr.anchorMin = new Vector2(0, 0); fr.anchorMax = new Vector2(0, 1); fr.pivot = new Vector2(0, 0.5f); fr.offsetMin = new Vector2(4, 4); fr.offsetMax = new Vector2(4, -4);
            stormTxt = UIKit.Txt(stormRow, "t", "", 14, Color.white, TextAnchor.MiddleCenter);
            Styled(stormTxt, UIKit.Hex("1c2a52"), 1.6f, 1.2f);
            stormTxt.rectTransform.Stretch(0, 2, 18, 0);
            if (Skin.Has("icon_cloud")) { stormCloud = Skin.Img(stormRow, "icon_cloud").rectTransform; stormCloud.Place(new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(6, 2), new Vector2(40, 40)); }
            stormRow.gameObject.SetActive(false);
            // 雨和闪电：在场景标签上面、界面下面
            rainRoot = UIKit.Rect("rain", root).Stretch(); rainRoot.SetSiblingIndex(ui.labels.GetSiblingIndex() + 1);
            rain.Clear();
            for (int k = 0; k < 70; k++)
            {
                var d = UIKit.Img(rainRoot, "drop", null, new Color(0.85f, 0.92f, 1f, 0.42f)).rectTransform;
                d.anchorMin = d.anchorMax = new Vector2(0.5f, 0.5f); d.sizeDelta = new Vector2(2, 22); d.localRotation = Quaternion.Euler(0, 0, 14);
                d.gameObject.SetActive(false); rain.Add(d);
            }
            flashImg = UIKit.Img(rainRoot, "flash", null, new Color(1, 1, 1, 0)); flashImg.rectTransform.Stretch(-50, -50, -50, -50);
        }

        // 沙滩上的潮水：一块半透明的水，风暴越近从岸边往队伍那边漫得越多（岛民脚泡在水里）
        void StormScene()
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>());
            q.name = "flood"; q.transform.SetParent(world); q.transform.rotation = Quaternion.Euler(90, 0, 0);
            floodMat = new Material(Art.UnlitT); floodMat.SetColor("_BaseColor", new Color(0.45f, 0.78f, 0.92f, 0));
            q.GetComponent<MeshRenderer>().sharedMaterial = floodMat;
            flood = q.transform;
            SetFlood(0);
        }
        void SetFlood(float d)
        {
            if (!flood) return;
            float far = lay.zQ2 - QLG / 2 - 0.2f, len = Mathf.Max(0.01f, (lay.zP - 0.15f - far) * d);
            float w = 2 * lay.xQ + 3;
            flood.position = W(0, lay.zP - 0.15f - len / 2, 0.27f);
            flood.localScale = new Vector3(w, len, 1);
            floodMat.SetColor("_BaseColor", new Color(0.45f, 0.78f, 0.92f, d > 0.01f ? 0.5f : 0));
        }

        void StormReset()
        {
            stormOn = L.cfg.storm > 0;
            stormMax = (float)L.cfg.storm; stormLeft = stormMax; stormCap = stormMax;   // 上限就是开局秒数（10-26 用户：上限 120 秒，别 +10）
            stormLose = stormWarned = false; stormK = 0; flashT = 0; thunderT = 3;
            if (stormRow) { stormRow.gameObject.SetActive(stormOn); ui.qfill.gameObject.SetActive(!stormOn); ui.gift.gameObject.SetActive(!stormOn); }
            ApplyWeather(0);
            UpdateStormBar();
        }

        bool StormPaused => !stormOn || demo || GuideOn || st == null || st.status != "play" || st.taps == 0 || ModalOpen || OnScreen || menuOpen || (ui.ad && ui.ad.gameObject.activeSelf);

        void StormTick(float dt)
        {
            if (L == null) return;
            if (!StormPaused)
            {
                stormLeft -= dt;
                if (stormLeft <= 15 && !stormWarned && stormMax > 20) { stormWarned = true; if (WordArt("storm") != null) Banner(T("风暴快到了！"), "storm"); else Toast(T("风暴快到了！")); }
                if (stormLeft <= 0) { stormLeft = 0; StormHit(); }
            }
            float target = stormOn ? Mathf.Clamp01(1 - stormLeft / Mathf.Max(1, stormMax)) : 0;
            target = Mathf.Max(0, target - FeverStormEase);   // 连击狂潮：乌云往后退（只是画面，Fever.cs）
            if (stormOn && st != null && st.status == "win") target = Mathf.Max(0, stormK - dt * 0.4f);    // 赢了天慢慢放晴
            stormK = Mathf.MoveTowards(stormK, target, dt * 0.6f);
            ApplyWeather(stormK);
            UpdateStormBar();
            // 雨
            float d = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.35f, 1f, stormK));
            var cv = ((RectTransform)ui.canvas.transform).rect;
            int n = Mathf.RoundToInt(d * rain.Count);
            for (int k = 0; k < rain.Count; k++)
            {
                var r = rain[k]; bool on = k < n;
                if (r.gameObject.activeSelf != on) { r.gameObject.SetActive(on); if (on) r.anchoredPosition = new Vector2(Random.Range(-cv.width / 2, cv.width / 2), Random.Range(-cv.height / 2, cv.height / 2)); }
                if (!on) continue;
                var p = r.anchoredPosition + new Vector2(-220, -1000) * dt;
                if (p.y < -cv.height / 2 - 20) p = new Vector2(Random.Range(-cv.width / 2, cv.width / 2 + 120), cv.height / 2 + 20);
                r.anchoredPosition = p;
            }
            // 闪电 + 雷：风暴过了六成以后，隔几秒一次（很淡，不是警报）
            if (d > 0.5f && !StormPaused)
            {
                thunderT -= dt;
                if (thunderT <= 0) { thunderT = Random.Range(5f, 9f); flashT = 1; sfx.Play("thunder", Random.Range(0.85f, 1.1f), 0.3f); }   // 音效整体放大了 3 倍（Synth.SFX_GAIN），雷声是背景气氛，压回原来的响度
            }
            flashT = Mathf.Max(0, flashT - dt * 4);
            flashImg.color = new Color(1, 1, 1, 0.22f * flashT * flashT);
        }

        void ApplyWeather(float k)
        {
            float d = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.2f, 1f, k));
            sun.intensity = Mathf.Lerp(sunI0, sunI0 * 0.5f, d);
            fill.intensity = Mathf.Lerp(0.35f, 0.2f, d);
            sun.color = Color.Lerp(sun0, UIKit.Hex("c9d5f2"), d);
            float fv = Mathf.Clamp01(feverK - 2);   // 连击狂潮：阳光更暖更亮
            sun.intensity *= 1 + 0.1f * fv; sun.color = Color.Lerp(sun.color, UIKit.Hex("ffe0a0"), 0.25f * fv);
            RenderSettings.ambientSkyColor = Color.Lerp(sky0, UIKit.Hex("7d8aa3"), d);
            RenderSettings.ambientEquatorColor = Color.Lerp(eq0, UIKit.Hex("6f8496"), d);
            RenderSettings.ambientGroundColor = Color.Lerp(gnd0, UIKit.Hex("5d7385"), d);
            cam.backgroundColor = Color.Lerp(bg0, UIKit.Hex("51657a"), d);
            if (seaMat) seaMat.SetColor("_BaseColor", Color.Lerp(Color.white, new Color(0.62f, 0.7f, 0.8f), d));
            SetFlood(Mathf.InverseLerp(0.3f, 1f, k));
            sfx.stormT = d;
        }

        void UpdateStormBar()
        {
            if (!stormRow || !stormOn) return;
            float f = Mathf.Clamp01(stormLeft / Mathf.Max(1, stormCap));
            stormFill.rectTransform.sizeDelta = new Vector2(Mathf.Max(0, (stormRow.rect.width - 8) * f), -8);
            float u = Mathf.Clamp01(stormLeft / Mathf.Max(1, stormMax));
            if (!Skin.Has("bar_storm")) stormFill.color = Color.Lerp(UIKit.Hex("7a5cff"), UIKit.Hex("8fc3ff"), u);
            stormTxt.text = Mathf.CeilToInt(stormLeft) + T("秒");
            if (stormCloud) stormCloud.localScale = Vector3.one * (u < 0.25f ? 1 + 0.08f * Mathf.Sin(Fx.Now / 120) : 1);   // 快到了小乌云抖起来
        }

        // 开走一艘满船：风暴往后推一点
        void StormBonus()
        {
            if (!stormOn || st.status == "win") return;
            stormLeft = Mathf.Min(stormCap, stormLeft + (float)L.cfg.bonus);
            var c = new Vector3[4]; stormRow.GetWorldCorners(c);
            var r = OnCanvas("sb"); r.sizeDelta = new Vector2(80, 26);
            var t = UIKit.Txt(r, "t", "+" + L.cfg.bonus.ToString("0.#") + T("秒"), 16, UIKit.Hex("bfe3ff"), TextAnchor.MiddleCenter, true); t.rectTransform.Stretch();
            var p0 = ToCanvas(new Vector3((c[0].x + c[2].x) / 2, c[0].y - 8 * ui.scale, 0));
            r.anchoredPosition = p0;
            Fx.Run(async () => { await Fx.Tween(700, p => { if (r) { r.anchoredPosition = p0 + new Vector2(0, -6 - 18 * p); t.color = new Color(0.75f, 0.89f, 1, 1 - p * p); } }); if (r) Destroy(r.gameObject); });
        }

        void StormHit()
        {
            if (st.status != "play") return;
            st.status = "lose"; stormLose = true; HideHand(); SetCrane(false);
            Fx.Run(LoseFx);
        }

        void StormRevive() { stormLose = false; stormLeft = Mathf.Min(stormCap, stormLeft + STORM_REVIVE); st.status = "play"; Banner(F("风暴推迟 {0} 秒", STORM_REVIVE), "delay"); }
    }
}
