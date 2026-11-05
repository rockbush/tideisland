// 混合模式自动测试（-tideMix）：先不开画面，13 座岛 × 8 关逐波生成、机器人打一遍；再截地图、弹窗、关卡、涨潮、复活、过关。
using System.Threading.Tasks;
using UnityEngine;

namespace Tide
{
    public partial class Game
    {
        async Task MixShots(string dir)
        {
            MixLoad();
            string keep = PlayerPrefs.GetString("tide_mix2", ""); int keepAll = PlayerPrefs.GetInt("tide_mix_all", 0);
            string only = AutoTest.Arg("-tideMix"); bool quick = only != null && only.Contains("q");
            if (only != null && only.Contains("d"))
            {
                foreach (int i in new[] { 0, 3, 5, 6, 7, 11, 12 }) foreach (int k in new[] { 4, 5, 7 })
                {
                    mixI = i; mixK = k; int fail = 0; string why = "";
                    for (int sd = 0; sd < 12; sd++)
                    {
                        var s = SpSim.MixBuild(MixCfgFor(1), 7000 + sd * 31); if (s == null) { fail++; continue; }
                        for (int n = 0; n < 60 * 400; n++) { s.BotTick(1 / 60f); s.Tick(1 / 60f); s.evs.Clear(); if (s.status == 1 || s.status == 2) break; }
                        if (s.status != 1) { fail++; why = s.why; }
                    }
                    TLog("60帧 " + MIX_N[i] + " 第" + (k + 1) + "关 12 张图失败 " + fail + " " + why);
                    await Fx.SleepKeep(5);
                }
                return;
            }
            if (only != null && only.Contains("m")) { PlayLevel(30); await Fx.SleepKeep(2500); await Shot(dir, "m_level"); HideModal(); ShowMap(true); await Fx.SleepKeep(900); await Shot(dir, "m_map"); return; }
            if (only == null || !only.Contains("s"))
            {
                int bad = 0, total = 0;
                for (int i = 0; i < MIX_NI; i++)
                {
                    for (int k = 0; k < 8; k++)
                    {
                        mixI = i; mixK = k; int waves = quick ? 1 : Mathf.Min(MIX_T[k].waves, 3), ppl = 0, ok = 0; float t0 = Time.realtimeSinceStartup;
                        string extra = "";
                        for (int w = 0; w < waves; w++)
                        {
                            var c = MixCfgFor(w); var s = SpSim.MixBuild(c, 1000 + i * 100 + k * 10 + w);
                            if (s == null) { extra += " 第" + (w + 1) + "波生成失败"; continue; }
                            ppl += s.guys.Count; int poles = 0, mys = 0, wd = 0, lk = 0; foreach (var g in s.guys) if (g.pole) poles++; foreach (var b in s.boats) { if (b.mys) mys++; if (b.weed > 0) wd++; if (b.lockBy >= 0) lk++; }
                            if (w == 0) extra += " 船" + s.boats.Count + " 石" + s.rocks.Count + " 撑杆" + poles + " 白" + mys + " 藻" + wd + " 锁" + lk + (s.clock ? " 计时" + s.timer.ToString("0") : "");
                            if (SpSim.MixRun(s)) ok++;
                        }
                        total++; if (ok < waves) bad++;
                        TLog("混合 " + MIX_N[i] + " 第" + (k + 1) + "关：" + ok + "/" + waves + " 波打通，人 " + ppl + "（计划每波 " + MixPlanWave() + "）" + extra + " 耗时 " + (Time.realtimeSinceStartup - t0).ToString("0.0") + "s");
                        await Fx.SleepKeep(5);
                    }
                }
                TLog("混合验证完：" + total + " 关里 " + bad + " 关有波没打通");
            }
            if (only != null && only.Contains("v")) return;
            // 画面
            for (int i = 0; i < MIX_NI; i++) mixE[i] = mixH[i] = mixX[i] = 0;
            mixE[0] = 2; mixAll = false; mixZoom = MIX_ZDEF;
            ShowSpecList(); await Fx.SleepKeep(400); await Shot(dir, "x_list");
            ShowMixMap(0); await Fx.SleepKeep(700); await Shot(dir, "x_map_start");
            MixZoomToggle(); await Fx.SleepKeep(900); await Shot(dir, "x_map_startfar"); MixZoomToggle(); await Fx.SleepKeep(900);
            // 模拟打通起点岛第 3 关：周围 4 座岛一起开 → 合并弹窗 → 镜头一座座飞过去、云散开
            mixI = 0; mixK = 2; mixOn = true; mixSaved = 0; sp = null;
            MixWin(); await Fx.SleepKeep(600); await Shot(dir, "x_win");
            ClickPop(); await Fx.SleepKeep(900); await Shot(dir, "x_pop1");
            ClickPop(); await Fx.SleepKeep(1100); await Shot(dir, "x_reveal_a"); await Fx.SleepKeep(2200); await Shot(dir, "x_reveal_b"); await Fx.SleepKeep(5500); await Shot(dir, "x_reveal_c");
            mixE[0] = 3; mixE[1] = 3; mixE[2] = 3; mixE[3] = 2; MixSave();
            ShowMixMap(3); await Fx.SleepKeep(700); await Shot(dir, "x_map_early");
            MixDetail(3); await Fx.SleepKeep(500); await Shot(dir, "x_detail"); HideModal();
            MixZoomToggle(); await Fx.SleepKeep(900); await Shot(dir, "x_map_far"); MixZoomToggle(); await Fx.SleepKeep(900);
            for (int i = 0; i < MIX_NI; i++) mixE[i] = 3;
            ShowMixMap(0, null, null, new System.Collections.Generic.List<Vector2Int> { new Vector2Int(2, 0) }); await Fx.SleepKeep(1400); await Shot(dir, "x_pop2"); ClickPop();
            mixH[0] = 4; mixH[1] = 4; mixX[0] = 1;
            ShowMixMap(1, null, null, new System.Collections.Generic.List<Vector2Int> { new Vector2Int(3, 1) }); await Fx.SleepKeep(1400); await Shot(dir, "x_pop3"); ClickPop(); await Fx.SleepKeep(800); await Shot(dir, "x_map_late");
            // 进关：几个岛的新手关、困难关、地狱关
            int[][] lv = { new[] { 2, 0 }, new[] { 0, 4 }, new[] { 3, 5 }, new[] { 0, 7 }, new[] { 6, 7 }, new[] { 12, 6 } };
            foreach (var p in lv)
            {
                PlayerPrefs.SetInt("tide_mix_new" + p[1], 1);
                MixPlay(p[0], p[1]); HideModal(); await Fx.SleepKeep(700);
                await Shot(dir, "x_lv" + p[0] + "_" + p[1] + "_a");
                spBot = true; spSteps = 3; float tt = Fx.Now; int w0 = mixWave; bool shotT = false;
                while (Fx.Now - tt < 45000 && mixOn)
                {
                    await Fx.SleepKeep(100);
                    if (!shotT && mixBusy && !ModalOpen) { shotT = true; await Fx.SleepKeep(250); await Shot(dir, "x_lv" + p[0] + "_" + p[1] + "_tide"); }
                    if (sp != null && (sp.status == 2)) break;
                    if (ModalOpen) break;
                    if (mixWave >= w0 + 2) break;
                }
                TLog("进关 " + MIX_N[p[0]] + " 第" + (p[1] + 1) + "关：到第 " + (mixWave + 1) + " 波，status=" + (sp != null ? sp.status : -1) + " " + (sp != null ? sp.why : "") + " 剩 " + (sp != null ? sp.PeopleLeft() + MixLater() : 0));
                spBot = false; spSteps = 1;
                await Shot(dir, "x_lv" + p[0] + "_" + p[1] + "_b");
                HideModal(); MixLeave();
            }
            // 复活：风暴
            MixPlay(0, 5); HideModal(); await Fx.SleepKeep(500);
            var b0 = sp.Hint(); if (b0 != null) sp.Tap(b0);
            sp.timer = 0.2f; await Fx.SleepKeep(1500); await Shot(dir, "x_lose");
            TLog("风暴输 弹窗=" + ModalOpen);
            // 道具
            HideModal(); sp.Revive(STORM_START, true); await Fx.SleepKeep(300);
            MixBoost(2); await Fx.SleepKeep(500); MixBoost(0); SpBoat any = null; foreach (var b in sp.boats) if (b.st == SpSim.LOT && sp.Probe(b).res != SpSim.OK) { any = b; break; }
            if (any != null) MixCraneTap(any); await Fx.SleepKeep(600); await Shot(dir, "x_crane");
            TLog("道具后 码头数=" + sp.docks.Count + " 直升机吊的船状态=" + (any != null ? any.st : -1));
            MixLeave(); ShowMixMap(0); await Fx.SleepKeep(500);
            PlayerPrefs.SetString("tide_mix2", keep); PlayerPrefs.SetInt("tide_mix_all", keepAll); for (int k = 0; k < 8; k++) PlayerPrefs.DeleteKey("tide_mix_new" + k); PlayerPrefs.Save();
            mixLoaded = false; MixLoad();
        }
        void ClickPop()
        {
            if (!ModalOpen) return;
            foreach (var b in ui.box.GetComponentsInChildren<UnityEngine.UI.Button>()) if (b.name == "btn") { b.onClick.Invoke(); return; }
        }
    }
}
