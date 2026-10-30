// 多语言：代码里直接写中文，T("中文") 在英文模式下查表换成英文；查不到就原样返回中文（测试时会记下来，见 Missing）。
// 带参数的用 F("第 {0} 关", n)。语言跟系统走（中文系统 → 中文，其他 → 英文），设置里可以改。
using System.Collections.Generic;
using UnityEngine;

namespace Tide
{
    public static class Loc
    {
        public static bool En = true;
        public static readonly HashSet<string> Missing = new HashSet<string>();

        public static void Set(string lang)
        {
            if (lang == "zh") En = false;
            else if (lang == "en") En = true;
            else
            {
                var l = Application.systemLanguage;
                En = !(l == SystemLanguage.Chinese || l == SystemLanguage.ChineseSimplified || l == SystemLanguage.ChineseTraditional);
            }
        }

        public static string T(string zh)
        {
            if (!En || string.IsNullOrEmpty(zh)) return zh;
            if (EN.TryGetValue(zh, out var s)) return s;
            Missing.Add(zh);
            return zh;
        }
        public static string F(string zh, params object[] a) => string.Format(T(zh), a);

        static readonly Dictionary<string, string> EN = new Dictionary<string, string>
        {
            // ---- 教学 / 新机制卡片 ----
            { "点船，它顺着<b>箭头</b>开到码头\n岛民自己上<b>同色</b>的船", "Tap a boat: it sails along its <b>arrow</b> to the dock\nIslanders board boats of <b>their own color</b>" },
            { "船头前面<b>有船挡着</b>就开不出去", "A boat <b>can't get out</b> if something is in front of it" },
            { "码头只有 <b>3 个泊位</b>，停满就输\n先看队头要什么颜色", "The dock has only <b>3 berths</b>. Fill them all and you lose\nCheck what color the front of the line needs" },
            { "游艇", "Yacht" }, { "游艇坐 <b>6 个人</b>，快艇坐 4 个。", "A yacht seats <b>6 people</b>, a speedboat seats 4." },
            { "木筏", "Raft" }, { "小木筏只坐 <b>2 个人</b>\n一下就坐满了\n它是方的，<b>四面</b>都能朝", "A little raft seats only <b>2</b>, so it fills up fast.\nIt's square, so it can face <b>any direction</b>." },
            { "航母", "Carrier" }, { "航母最长，一次能装 <b>10 个人</b>！", "The carrier is the longest boat. It takes <b>10 people</b> at once!" },
            { "礁石", "Rocks" }, { "礁石挡路，永远不会动。", "Rocks block the way and never move." },
            { "船坞", "Boathouse" }, { "船坞里还停着船\n<b>门口的船开走了</b>\n下一艘才出来\n屋顶数字 = 里面还剩几艘", "More boats wait inside. <b>When the boat at the door leaves</b>, the next one comes out.\nThe number on the roof = boats left inside." },
            { "神秘白船", "Mystery boat" }, { "白色的船看不出是什么颜色：<b>船头前面的路一通</b>，就露出颜色。", "A white boat hides its color until <b>the way in front of it is clear</b>." },
            { "知道了", "Got it" }, { "输入密码", "Enter password" }, { "密码", "Password" }, { "密码错误", "Wrong password" }, { "确定", "OK" },
{ "新机制！", "New!" },
            // ---- 营地 ----
            { "篝火", "Campfire" }, { "营地的第一样东西", "The first thing every camp needs" },
            { "小木屋", "Hut" }, { "有地方睡觉了", "A place to sleep" },
            { "码头", "Pier" }, { "可以停船", "Somewhere to moor the boats" },
            { "灯塔", "Lighthouse" }, { "照亮回家的路", "Lights the way home" },
            { "我的营地", "My Camp" }, { "每关救下来的人都住在这儿。", "Everyone you rescue lives here." },
            { "居民 <b>{0}</b> 人　星星 {1}　金币 {2}", "Residents <b>{0}</b> · Stars {1} · Coins {2}" },
            { "居民满 {0} 人的礼物到了！", "Your gift for {0} residents is here!" },
            { "居民满 {0} 人送 {1} 金币（还差 {2} 人）", "{1} coins at {0} residents ({2} to go)" },
            { "领取 {0} 金币", "Claim {0} coins" }, { "建筑 {0} / {1}", "Buildings {0} / {1}" },
            { "建 {0}（{1} 金币）", "Build {0} ({1} coins)" },
            { "岛民图鉴 {0} / {1}　每关第一次通关得一位", "Islanders {0} / {1}. Each first clear brings a new one" },
            { "免费宝箱 +40 金币", "Free chest +40 coins" },
            { "回去闯关", "Back to the level" }, { "关闭", "Close" },
            // ---- 岛民 ----
            { "农夫", "Farmer" }, { "厨师", "Chef" }, { "医生", "Doctor" }, { "画家", "Painter" }, { "消防员", "Firefighter" },
            { "老师", "Teacher" }, { "科学家", "Scientist" }, { "机长", "Pilot" }, { "歌手", "Singer" }, { "警察", "Police Officer" },
            { "侦探", "Detective" }, { "宇航员", "Astronaut" }, { "魔法师", "Wizard" }, { "小仙子", "Fairy" }, { "美人鱼", "Mermaid" },
            { "精灵", "Elf" }, { "超级英雄", "Superhero" }, { "忍者", "Ninja" }, { "公主", "Princess" }, { "圣诞老人", "Santa" },
            { "小天使", "Angel" }, { "牛仔", "Cowboy" }, { "小狗", "Puppy" }, { "小猫", "Kitten" }, { "兔子", "Bunny" },
            { "狐狸", "Fox" }, { "熊猫", "Panda" }, { "考拉", "Koala" }, { "小老虎", "Tiger Cub" }, { "狮子", "Lion" },
            { "青蛙", "Frog" }, { "猴子", "Monkey" }, { "企鹅", "Penguin" }, { "鹦鹉", "Parrot" }, { "海龟", "Sea Turtle" },
            { "海豚", "Dolphin" }, { "鲸鱼", "Whale" }, { "螃蟹", "Crab" }, { "章鱼", "Octopus" }, { "海豹", "Seal" },
            { "水獭", "Otter" }, { "火烈鸟", "Flamingo" }, { "独角兽", "Unicorn" },
            { "渔夫", "Fisherman" }, { "船长", "Captain" }, { "潜水员", "Diver" }, { "救生员", "Lifeguard" }, { "冲浪手", "Surfer" },
            { "邮递员", "Mail Carrier" }, { "面包师", "Baker" }, { "木匠", "Carpenter" }, { "园丁", "Gardener" }, { "理发师", "Barber" },
            { "摄影师", "Photographer" }, { "小丑", "Clown" }, { "舞蹈家", "Dancer" }, { "钢琴家", "Pianist" }, { "探险家", "Explorer" },
            { "考古学家", "Archaeologist" }, { "天文学家", "Astronomer" }, { "发明家", "Inventor" }, { "机器人", "Robot" }, { "海盗", "Pirate" },
            { "骑士", "Knight" }, { "国王", "King" }, { "王后", "Queen" }, { "女巫", "Witch" }, { "雪人", "Snowman" },
            { "稻草人", "Scarecrow" }, { "外星人", "Alien" }, { "大象", "Elephant" }, { "长颈鹿", "Giraffe" }, { "斑马", "Zebra" },
            { "河马", "Hippo" }, { "袋鼠", "Kangaroo" }, { "北极熊", "Polar Bear" }, { "浣熊", "Raccoon" }, { "小鹿", "Fawn" },
            { "羊驼", "Alpaca" }, { "小鸭", "Duckling" }, { "天鹅", "Swan" }, { "孔雀", "Peacock" }, { "啄木鸟", "Woodpecker" },
            { "蜜蜂", "Bee" }, { "瓢虫", "Ladybug" }, { "蜗牛", "Snail" }, { "海马", "Seahorse" }, { "水母", "Jellyfish" },
            { "海星", "Starfish" }, { "寄居蟹", "Hermit Crab" }, { "小丑鱼", "Clownfish" }, { "海狮", "Sea Lion" }, { "锤头鲨", "Hammerhead" }, { "小龙", "Baby Dragon" }, { "恐龙", "Dinosaur" },
            { "蝴蝶", "Butterfly" }, { "刺猬", "Hedgehog" }, { "树懒", "Sloth" }, { "松鼠", "Squirrel" }, { "猫头鹰", "Owl" },
            // ---- 夸奖 ----
            { "不错！", "Nice!" }, { "漂亮！", "Great!" }, { "太棒了！", "Awesome!" }, { "停不下来！", "Unstoppable!" }, { "神了！", "Legendary!" },
            // ---- 顶栏 / 道具 ----
            { "第 {0} 关", "LEVEL {0}" }, { "剩 <b><size=20>{0}</size></b> 人", "<b><size=20>{0}</size></b> left" },
            { "吊船", "Crane" }, { "理队", "Sort" }, { "加泊位", "+Berth" },
            { "{0}金", "{0}" }, { "广告", "AD" }, { "满", "MAX" }, { "解锁", "Unlock" }, { "队头", "Front" },
            { "只剩 1 个空泊位了！", "Only 1 free berth left!" },
            { "前面有船挡着，开不出去", "Something's in the way. It can't get out" },
            { "码头停满了", "The dock is full" },
            { "多了一个泊位", "Extra berth!" },
            { "点任意一艘船（被挡着的也行），直接吊到码头", "Tap any boat (even a blocked one) to lift it to the dock" },
            { "等大家上完船再理", "Wait until everyone has boarded" },
            { "现在理队没有用", "Sorting won't help right now" },
            { "队伍理好了", "Line sorted!" },
            { "泊位已经全开了", "All berths are open" },
            // ---- 菜单 ----
            { "重来这一关", "Restart level" }, { "关卡地图", "MAP" }, { "营地", "Camp" }, { "设置", "Settings" },
            // ---- 胜利 / 失败 ----
            { "全员上船！", "All aboard!" }, { "新岛民上岛：<b>{0}</b>", "New islander: <b>{0}</b>" },
            { "获得 <b>{0}</b> 金币", "+<b>{0}</b> coins" },
            { "救出 <b>{0}</b> 人　营地居民 <b>{1}</b> 人", "Rescued <b>{0}</b> · Camp residents <b>{1}</b>" },
            { "人数到了，去营地领礼物！", "Milestone reached! Claim your gift at camp" },
            { "再来 {0} 人，营地有礼物", "{0} more residents for a camp gift" },
            { "金币翻倍 +{0}", "Double coins +{0}" }, { "已领取 +{0}", "Claimed +{0}" },
            { "下一关", "Next level" }, { "继续", "Continue" }, { "去营地看看", "Visit camp" }, { "地图", "Map" }, { "宝箱", "Chest" }, { "小岛", "Island" }, { "连胜 ×{0}", "Streak ×{0}" },
            { "全部通关！", "You beat every level!" }, { "更多小岛正在建造中，敬请期待。", "More islands are on the way. Stay tuned!" },
            { "演示结束", "Demo finished" }, { "我来试试", "Let me try" },
            { "码头停满了！", "The dock is full!" }, { "还有 {0} 人没上船", "{0} islanders still waiting" },
            { "队头的人要的颜色，码头上一艘都没有", "No boat at the dock matches the color at the front of the line" },
            { "连胜 {0} 会中断", "You'll lose your {0}-win streak" },
            { "复活：多开一个泊位接着玩", "Continue with 1 extra berth" },
            { "看广告复活", "Continue (watch ad)" },
            { "{0} 金币复活", "Continue ({0} coins)" },
            { "放弃，重新开始", "Give up and retry" },
            { "广告播放中… {0}", "Ad playing… {0}" }, { "（演示占位：正式版这里是激励视频）", "(Placeholder: a rewarded video will play here)" },
            // ---- 开始界面 / 地图 / 设置 ----
            { "潮汐岛", "Tide Island" }, { "把每一位岛民都救上船", "Get every islander aboard!" }, { "开始", "PLAY" },
            { "第 {0} 岛 · {1}", "Island {0} · {1}" }, { "通关第 {0} 关解锁", "Beat level {0} to unlock" },
            { "椰林湾", "Palm Bay" }, { "珊瑚湾", "Coral Cove" }, { "海龟礁", "Turtle Reef" }, { "火山岛", "Volcano Isle" }, { "灯塔角", "Lighthouse Point" },
            { "鲨鱼湾", "Shark Bay" }, { "迷雾岛", "Misty Isle" }, { "礁石滩", "Rocky Shoals" }, { "锁链港", "Chain Harbor" }, { "风暴角", "Storm Cape" },
            { "测试：全部关卡已解锁", "Test mode: all levels unlocked" },
            { "音乐", "Music" }, { "音效", "Sound" }, { "震动", "Vibration" }, { "语言", "Language" },
            { "开", "On" }, { "关", "Off" },
            { "清空进度", "Reset progress" }, { "再点一次确认清空", "Tap again to confirm" },
            { "版本 {0}", "Version {0}" },
            // ---- 风暴 / 难度 / 生存主题 ----
            { "风暴", "Storm" }, { "秒", "s" }, { "风暴倒计时", "Storm timer" }, { "风暴快到了！", "The storm is almost here!" },
            { "风暴推迟 {0} 秒", "Storm delayed {0}s" }, { "风暴来了！", "The storm hit!" },
            { "还有 {0} 人没救出来", "{0} islanders left behind" }, { "风暴来之前没能把大家都送走", "Not everyone got off the island in time" },
            { "复活：风暴推迟 {0} 秒", "Continue: delay the storm {0}s" }, { "复活：风暴倒计时回到 {0} 秒", "Continue: storm timer back to {0}s" },
            { "风暴要来了", "A storm is coming" },
            { "上面的<b>风暴条走完就输</b>\n每救一个人上船\n风暴就<b>往后推一点</b>\n手要快，也要准！", "When the <b>storm bar runs out, you lose</b>.\nEvery islander who boards <b>pushes the storm back</b> a little.\nBe quick, but be smart!" },
            { "困难关！", "Hard level!" }, { "超难关！", "Super hard!" }, { "困难", "Hard" }, { "超难", "Super" },
            { "直升机", "Heli" }, { "喇叭", "Shout" }, { "临时码头", "+Dock" },
            { "点任意一艘船（被挡着的也行），直升机直接吊到码头", "Tap any boat (even a blocked one) and the helicopter lifts it to the dock" },
            { "喇叭一喊，大家排好了", "Everyone lined up!" },
            { "海藻缠船", "Seaweed" }, { "钥匙和锁链", "Key & Chain" }, { "鲨鱼", "Shark" }, { "撑杆跳", "Pole Vault" }, { "撑杆跳！", "Pole vault!" }, { "涨潮啦！", "High tide!" }, { "这关有 {0} 波船：港湾清空就会涨潮，送来下一波", "{0} waves of boats: clear the harbor and the tide brings the next!" },
            { "扛着<b>长杆</b>、一闪一闪的小人\n一上船，队伍里<b>和他同色</b>的人\n都撑杆跳上去，把空座补满！", "When the flashing islander\nwith the <b>pole</b> boards, everyone in line\n<b>of the same color</b> vaults aboard!" },
            { "船被<b>海藻缠住</b>了，先开别的船\n数字 = 还要先开走几艘\n到 0 海藻就松开", "This boat is <b>tangled in seaweed</b>.\nThe number = boats that must leave first.\nAt 0 it breaks free." },
            { "被<b>锁链</b>锁住的船开不走\n先把<b>挂着同色钥匙</b>的船开走\n锁就打开了", "A <b>chained</b> boat can't move.\nSail the boat with the <b>key</b> first\nand the lock opens." },
            { "鲨鱼在港湾边上<b>来回游</b>\n船正撞上它，会被吓回来\n<b>看准时机</b>再点！", "A shark <b>patrols</b> the edge.\nBoats that run into it turn back.\n<b>Time your tap!</b>" },
            { "被海藻缠住了：再开走 {0} 艘别的船就松开", "Tangled! Sail {0} more boats to free it" },
            { "被锁住了：先把挂钥匙的那艘船开走", "Locked! Sail the boat with the key first" },
            { "鲨鱼挡在出口！等它游过去再开", "Shark ahead! Wait for it to pass" },
            { "道具说明", "Booster" }, { "马上用", "Use it now" }, { "先不用", "Not now" },
            { "先点它，再点<b>任意一艘船</b>\n直升机把它吊到码头\n被挡着的也行，堵死时用它",
              "Tap this, then tap <b>any boat</b>,\neven a blocked one.\nThe helicopter flies it to the dock." },
            { "喊一声，<b>能马上上船的人</b>\n先站到队头\n队头的人上不了船时用它",
              "Islanders who can <b>board right now</b>\nmove to the front of the line.\nUse it when the front can't board." },
            { "码头<b>多开一个泊位</b>\n这一关一直有效\n码头快停满时用它",
              "Opens <b>one extra berth</b>\nfor the rest of this level.\nUse it when the dock is almost full." },
            { "你还有 <b>{0}</b> 次免费", "You have <b>{0}</b> free" }, { "每关最多用 {0} 次，这关还能用 <b>{1}</b> 次", "Max {0} per level · <b>{1}</b> left this level" }, { "每次 <b>{0}</b> 金币", "<b>{0}</b> coins each" },
            { "看一段广告就能开", "Watch a short ad to open it" }, { "花 <b>{0}</b> 金币开", "Open for <b>{0}</b> coins" },
            { "救援基地", "Rescue Base" }, { "去救援基地看看", "Visit the base" },
            // 家园（Home.cs）
            { "海滩营地", "Beach Camp" }, { "渔村", "Fishing Village" }, { "港口小镇", "Harbor Town" }, { "灯塔岬", "Lighthouse Cape" },
            { "帐篷", "Tent" }, { "求救旗", "SOS Flag" }, { "椰子树", "Palm Trees" }, { "吊床", "Hammock" }, { "小栈桥", "Jetty" }, { "遮阳伞", "Beach Umbrella" },
            { "晒鱼架", "Fish Rack" }, { "水井", "Well" }, { "菜园", "Veggie Patch" }, { "渔夫小屋", "Fisher's House" }, { "小渔船", "Fishing Boat" }, { "直升机坪", "Helipad" },
            { "水果摊", "Fruit Stand" }, { "喷泉", "Fountain" }, { "仓库", "Warehouse" }, { "风车", "Windmill" }, { "小餐馆", "Diner" }, { "花坛", "Flower Beds" },
            { "石阶", "Stone Steps" }, { "大游艇", "Yacht" }, { "岛民雕像", "Islander Statue" },
            { "每天来这里领 1 架免费直升机", "Come back daily for a free helicopter" }, { "每天来这里领 1 个免费喇叭", "Come back daily for a free shout" },
            { "以后每关通关金币 +10%", "+10% coins on every level from now on" },
            { "连胜 ×{0}！送你：{1}", "{0}-win streak! Bonus: {1}" }, { "泊位", "Berth" }, { "我的小岛", "My Island" },
            { "点星星牌子，用星星把小岛建起来！", "Tap a star sign to rebuild the island!" },
            { "整座岛都建好了！", "The whole island is rebuilt!" }, { "第 {0} 区 · {1}  {2}/{3}", "Area {0} · {1}  {2}/{3}" },
            { "测试：送 10 颗星", "Test: +10 stars" }, { "开宝箱！", "Open!" }, { "图鉴", "Album" }, { "建完「{0}」解锁", "Finish {0} to unlock" },
            { "领取", "Claim" }, { "还差 {0} 颗星，去闯关拿星星吧", "{0} more stars needed. Play levels to earn them!" },
            { "{0} 建好了！", "{0} built!" }, { "{0} 完工！", "{0} complete!" }, { "完工奖励", "Completion reward" },
            { "金币 <b>+{0}</b>　直升机 <b>+{1}</b>　喇叭 <b>+{2}</b>", "Coins <b>+{0}</b>  Heli <b>+{1}</b>  Shout <b>+{2}</b>" },
            { "新的区域解锁了：<b>{0}</b>", "New area unlocked: <b>{0}</b>" }, { "整座潮汐岛都重建好了！岛民们谢谢你！", "Tide Island is fully rebuilt! The islanders thank you!" },
            { "太好了", "Awesome!" }, { "再拿 {0} 颗星就能开宝箱", "{0} more stars to open the chest" },
            { "大宝箱！", "Big Chest!" }, { "星星宝箱", "Star Chest" }, { "金币 <b>+{0}</b>", "Coins <b>+{0}</b>" },
            { "直升机 <b>+{0}</b>", "Heli <b>+{0}</b>" }, { "喇叭 <b>+{0}</b>", "Shout <b>+{0}</b>" },
            { "每拿 10 颗星开一个，第 3 个是大宝箱", "One chest every 10 stars. Every 3rd is a big one" }, { "收下", "Collect" },
            { "直升机 +1（明天再来领）", "Heli +1 (come back tomorrow)" }, { "喇叭 +1（明天再来领）", "Shout +1 (come back tomorrow)" },
            { "岛民图鉴", "Islander Album" }, { "救下来的岛民 <b>{0}</b> / {1}", "Islanders rescued <b>{0}</b> / {1}" },
            { "每关第一次通关，会有一位新岛民上岛", "Each first clear brings a new islander" }, { "还有 <b>{0}</b> 位岛民在等你去救", "<b>{0}</b> more islanders are waiting for rescue" },
            { "这关的星星已经拿过了", "Stars for this level already collected" },
            { "星星 <b>{0}</b>　<color=#b5371f>可以去岛上建造了！</color>", "Stars <b>{0}</b>  <color=#b5371f>Ready to build!</color>" },
            { "星星 <b>{0}</b>　再拿 {1} 颗能建下一样", "Stars <b>{0}</b>  {1} more for the next build" }, { "星星 <b>{0}</b>", "Stars <b>{0}</b>" },
            { "星星宝箱可以开了！", "Star chest ready!" }, { "星星宝箱 {0}/{1}", "Star chest {0}/{1}" },
            { "连胜 ×{0}！下一关开局送：{1}", "{0}-win streak! Next level starts with: {1}" }, { "去岛上建造", "Build on the island" },
            { "放弃会断掉 {0} 连胜", "Giving up ends your {0}-win streak" },
            { "区域完工！", "Area complete!" }, { "建好了！", "Built!" }, { "连胜 ×{0}！", "{0}-win streak!" },
            // 新手引导（Guide.cs）
            { "举手的人是<b>队头</b>\n他要坐<b>同样颜色</b>的船", "The one waving is <b>first in line</b>.\nThey need a boat of <b>the same color</b>." },
            { "点这艘船\n它会顺着<b>箭头</b>开到码头", "Tap this boat.\nIt follows its <b>arrow</b> to the dock." },
            { "岛民会自己上船\n<b>坐满就开走</b>，下一个接着来", "Islanders board by themselves.\n<b>Full boats sail away</b>, then the next one." },
            { "船头前面<b>有船挡着</b>就开不出去\n先把挡路的船开走", "A boat <b>in the way</b> blocks the exit.\nMove the blocking boat first." },
            { "码头只剩 <b>1 个空位</b>了\n<b>停满就输</b>，先看队头要什么颜色", "Only <b>1 spot</b> left at the dock!\n<b>If it fills up, you lose.</b> Check the color first." },
            { "点<b>锁</b>能多开一个泊位\n前两个看广告，再往后花金币", "Tap a <b>lock</b> to open another berth.\nFirst two: watch an ad. After that: coins." },
            { "点<b>直升机</b>", "Tap the <b>helicopter</b>" }, { "点<b>喇叭</b>，喊一声", "Tap the <b>shout</b>" }, { "点这里<b>多开一个泊位</b>", "Tap here for an <b>extra berth</b>" },
            { "再点<b>任意一艘船</b>\n被挡着的也能吊走", "Now tap <b>any boat</b>,\neven a blocked one." },
            { "新道具！", "New booster!" }, { "送你 <b>2 次</b>免费！", "<b>2 free</b> uses for you!" }, { "送你 <b>1 次</b>免费！", "<b>1 free</b> use for you!" },
            { "试试看", "Try it" }, { "第 {0} 关解锁", "Unlocks at level {0}" },
            { "用<b>星星</b>把小岛建起来\n点这个牌子", "Use <b>stars</b> to rebuild the island.\nTap this sign." },
            { "每关<b>第一次通关</b>都给星星\n困难关、超难关给得更多", "Every <b>first clear</b> gives stars.\nHard levels give more." },
            { "攒满 10 颗星\n<b>星星宝箱</b>可以开了！", "10 stars collected!\nThe <b>star chest</b> is ready!" },
            { "这里<b>每天</b>都能领一份免费道具\n点它领取", "A free booster here <b>every day</b>.\nTap to claim." },
            { "新功能：<b>每日签到</b>\n每天来领一份奖励", "New: <b>Daily login</b>\nCollect a reward every day." },
            { "还有<b>每日任务</b>\n每天 3 个，做完领奖", "And <b>daily tasks</b>:\n3 a day, each with a reward." },
            { "去<b>我的小岛</b>\n用星星盖房子", "Visit <b>My Island</b>\nand build with your stars." },
            { "连胜奖励", "Win Streak Bonus" }, { "连胜 {0}", "{0} wins" },
            { "连着赢，下一关<b>开局就送道具</b>\n赢得越多送得越多", "Keep winning and the next level\n<b>starts with free boosters</b>." },
            { "输了放弃、或者打到一半退出\n连胜就<b>清零</b>", "Give up after a loss or quit a level\nand the streak <b>resets</b>." },
            { "用完", "Used up" }, { "每关{0}最多用 {1} 次", "{0}: max {1} per level" },
            // 每日（Daily.cs）
            { "金币 +{0}", "Coins +{0}" }, { "直升机 +{0}", "Heli +{0}" }, { "喇叭 +{0}", "Shout +{0}" },
            { "每日签到", "Daily Login" }, { "连着签到，奖励一天比一天多", "Log in every day for bigger rewards" },
            { "第 {0} 天", "Day {0}" }, { "大礼包", "Big gift" }, { "已领", "Claimed" }, { "今天：<b>{0}</b>", "Today: <b>{0}</b>" },
            { "看广告领双倍", "Watch an ad for double" }, { "明天再来领第 {0} 天：{1}", "Come back tomorrow for Day {0}: {1}" },
            { "断一天就要从第 1 天重新开始哦", "Miss a day and it starts over from Day 1" }, { "签到成功：{0}", "Claimed: {0}" },
            { "通关 {0} 关", "Clear {0} levels" }, { "开走 {0} 艘船", "Sail {0} boats" }, { "救出 {0} 位岛民", "Rescue {0} islanders" },
            { "使用 {0} 次道具", "Use {0} boosters" }, { "通关 {0} 个困难或超难关", "Clear {0} hard or super hard level" },
            { "不用道具不复活通关 {0} 关", "Clear {0} level, no boosters or revives" }, { "一口气连着开走 {0} 艘船", "Sail {0} boats in a row" },
            { "在小岛上建 {0} 样东西", "Build {0} thing on your island" }, { "每日任务完成：{0}", "Daily task done: {0}" },
            // 连击 + 特殊船（Special.cs）
            { "连击 ×{0}", "Combo ×{0}" }, { "风暴 +{0} 秒", "Storm +{0}s" }, { "集", "ALL" }, { "集合！", "Gather!" },
            { "连击奖励：集合船！一靠岸，同色的人全部插队上船", "Combo reward: Gather Boat! Everyone of its color jumps the line" },
            { "连击奖励：彩虹船！什么颜色的人都能上", "Combo reward: Rainbow Boat! Any color can board" },
            { "连击奖励：集合船！", "Combo reward: Gather Boat!" }, { "连击奖励：彩虹船！", "Combo reward: Rainbow Boat!" },
            { "每日任务", "Daily Tasks" }, { "{0} 小时 {1} 分后换新任务", "New tasks in {0}h {1}m" }, { "三个都完成再送", "Finish all 3 for a bonus" },
            { "签到", "Daily" }, { "任务", "Tasks" }, { "每日任务 {0}/3", "Daily tasks {0}/3" }, { "（有奖励可以领）", " (rewards waiting!)" },
        };
    }
}
