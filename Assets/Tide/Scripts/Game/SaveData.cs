// 存档（PlayerPrefs 里一串 JSON，对应网页版 localStorage 的 tideBoatV1）
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tide
{
    [Serializable]
    public class SaveData
    {
        public int coins, streak, pop, mile, crane = 1, sort = 1, level;
        public List<string> built = new List<string>(), seen = new List<string>();
        public List<int> stars = new List<int>(), coll = new List<int>();     // stars[i] = 第 i 关第一次通关拿到的星星数（拿过就不再给）
        // 家园（Home.cs）：star = 手上能花的星星，starGot = 一共拿过多少（星星宝箱按它算），chests = 开过几个宝箱
        // built 里是建好的任务 id，areasDone 是领过完工奖励的区；heliDay / hornDay = 停机坪、仓库的每日礼物哪天领的
        public int star, starGot, chests, homeVer;
        public int berthFree;               // 临时码头解锁时送的免费次数（Guide.cs）
        public List<string> areasDone = new List<string>();
        public long heliDay = -1, hornDay = -1;
        // 每日（Daily.cs）：signDay = 哪天签的到，signStep = 这一轮 7 天签了几天；taskDay = 今天的任务是哪天抽的，taskId / taskProg / taskGot 三个任务的 id、进度、领没领，taskBonus = 三个全领完的大奖领了没
        public long signDay = -1, taskDay = -1;
        public int signStep;
        public List<string> taskId = new List<string>();
        public List<int> taskProg = new List<int>(), taskGot = new List<int>();
        public bool taskBonus;
        // 解锁到第几关（下标；= 关卡总数 表示全通了）、每关打了几次
        public int top = -1;
        public List<int> tries = new List<int>();
        // 设置
        public bool music = true, sfx = true, vibe = true;
        public string lang = "";            // "" = 跟系统，"en"，"zh"
        // 留存统计
        public long installDay;             // 安装那天（UTC 天数）
        public int sessions;

        // 自动测试用另一份存档：测试会清空进度，不能碰到玩家（包括用户自己在试玩）的存档
        static string KEY => AutoTest.Wanted ? "tideBoatV1_test" : "tideBoatV1";

        public static SaveData Load()
        {
            var d = new SaveData();
            try
            {
                var s = PlayerPrefs.GetString(KEY, "");
                if (s.Length > 0) JsonUtility.FromJsonOverwrite(s, d);   // 老存档里没有的字段保留默认值
            }
            catch (Exception e) { Debug.LogWarning("存档读不出来，重新开始：" + e.Message); d = new SaveData(); }
            if (d.top < 0) d.top = Mathf.Max(d.level, d.stars.Count);  // 老存档：按打过的关推算
            if (d.installDay == 0) d.installDay = Today;
            return d;
        }

        // 清空进度：设置和留存统计留着
        public SaveData Reset() => new SaveData { top = 0, music = music, sfx = sfx, vibe = vibe, lang = lang, installDay = installDay, sessions = sessions };

        public static long Today => DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 86400;
        // 每日签到、任务、家园每日礼物用的"第几天"：按手机本地时间零点换天（DayShift 只给自动测试模拟过了几天）
        public static int DayShift;
        public static long Day => (long)(DateTime.Now.Date - new DateTime(1970, 1, 1)).TotalDays + DayShift;

        public void Persist() { PlayerPrefs.SetString(KEY, JsonUtility.ToJson(this)); PlayerPrefs.Save(); }

        public int Star(int lv) => lv < stars.Count ? stars[lv] : 0;
        public void SetStar(int lv, int v) { while (stars.Count <= lv) stars.Add(0); stars[lv] = Mathf.Max(stars[lv], v); }
        public bool Got(int lv) => coll.Contains(lv);
        public int TotalStars() { int t = 0; foreach (int s in stars) t += s; return t; }
        public int Tries(int lv) => lv < tries.Count ? tries[lv] : 0;
        public int AddTry(int lv) { while (tries.Count <= lv) tries.Add(0); return ++tries[lv]; }
    }
}
