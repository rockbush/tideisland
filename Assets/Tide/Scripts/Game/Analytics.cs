// 数据埋点。现在只记在本地（persistentDataPath/events.jsonl，一行一个事件），
// 接 Firebase / GameAnalytics 时只要给 Sink 挂一个转发函数，游戏代码不用改。
// 事件一览：
//   session_start {n, day}            第几次打开、装好后第几天（0 = 安装当天，算留存用）
//   level_start   {lv, try}           第几关、这关第几次打
//   level_win     {lv, try, stars, sec, taps, booster, revive, streak}   stars = 这次拿到的星星（第一次通关才有）
//   level_fail    {lv, try, sec, taps, left}      停满了（还没选复活/放弃）
//   level_quit    {lv, try, sec, taps, why}       打到一半重来 / 回地图
//   revive        {lv, pay}           pay = ad / coin
//   booster       {lv, id, pay}       id = crane / sort / berth，pay = free / coin / ad / streak（连胜送的）
//   booster_tip   {lv, id}            第一次点道具弹的说明
//   ad_rewarded   {place, lv}         看完激励广告（现在是占位）
//   screen        {name}              title / map / home / album / settings
//   home_build    {id, area, cost, left, lv}   家园建了一样东西（left = 剩下的星星）
//   home_area     {area, lv}          建完一个区
//   chest_open    {n, big}            开星星宝箱
//   perk_claim    {id}                领每日礼物（heli 停机坪 / horn 仓库）
//   streak_gift   {lv, streak}        连胜奖励发了
//   streak_lost   {lv, streak}        中途退出丢了连胜
//   tutorial      {id, lv}            新手引导某一步出现（Guide.cs；g_l1a/b/c 第 1 关、g_l2、g_full1/2、unlock_<道具>_card/_btn）
//   booster_unlock {lv, id}           道具解锁卡片（直升机第 5 关、喇叭第 7 关、临时码头第 9 关）
//   sign          {day, double}       每日签到（第几天、看没看广告翻倍）
//   task_done     {id, lv}            每日任务做完了 / task_claim {id} 领了 / task_bonus 三个全领的大奖
//   screen        sign {step} / tasks {done}
//   setting       {key, value}
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Tide
{
    public static class Analytics
    {
        public static event Action<string, Dictionary<string, object>> Sink;
        static string path;
        static readonly StringBuilder buf = new StringBuilder();
        static float lastFlush;

        public static void Init()
        {
            try
            {
                path = Path.Combine(Application.persistentDataPath, "events.jsonl");
                if (File.Exists(path) && new FileInfo(path).Length > 2 * 1024 * 1024) File.Delete(path);   // 本地只留最近 2MB
            }
            catch (Exception) { path = null; }
        }

        // Track("level_win", "lv", 3, "stars", 2)
        public static void Track(string ev, params object[] kv)
        {
            var d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[kv[i].ToString()] = kv[i + 1];
            var sb = new StringBuilder();
            sb.Append("{\"t\":").Append(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).Append(",\"e\":\"").Append(ev).Append('"');
            foreach (var p in d)
            {
                sb.Append(",\"").Append(p.Key).Append("\":");
                var v = p.Value;
                if (v is bool b) sb.Append(b ? "true" : "false");
                else if (v is int || v is long) sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture));
                else if (v is float f) sb.Append(f.ToString("0.##", CultureInfo.InvariantCulture));
                else sb.Append('"').Append(v.ToString().Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
            }
            sb.Append('}');
            string line = sb.ToString();
            buf.Append(line).Append('\n');
            if (AutoTest.Wanted) Debug.Log("[Ana] " + line);
            try { Sink?.Invoke(ev, d); } catch (Exception e) { Debug.LogException(e); }
            if (Time.realtimeSinceStartup - lastFlush > 5) Flush();
        }

        public static void Flush()
        {
            lastFlush = Time.realtimeSinceStartup;
            if (path == null || buf.Length == 0) return;
            try { File.AppendAllText(path, buf.ToString()); } catch (Exception) { }
            buf.Clear();
        }
    }
}
