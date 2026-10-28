// 每关的几条规矩：
//   道具上限：直升机、喇叭每关各最多用 BOOST_CAP 次（免费的、连胜送的、看广告的、花金币的都算），临时码头不限
//   重来换色：第一次打用关卡原本的配色；之后每次重来，把这一关用到的几种颜色互相对调（船和排队的人一起换），
//            摆放、顺序、难度完全不变，只是看起来像新的一关。同一次尝试（开始界面后面摆着的那关 → 点开始）颜色一样
using UnityEngine;

namespace Tide
{
    public partial class Game
    {
        const int BOOST_CAP = 2;
        readonly int[] boosterUse = new int[2];      // 这一关已经用了几次：[0] 直升机 [1] 喇叭

        void RecolorForTry(int lv)
        {
            int n = 0;
            foreach (var B in L.boats) n = Mathf.Max(n, B.c + 1);
            int tries = save.Tries(lv);                // 还没算这一次（AddTry 在后面）
            if (n < 2 || tries == 0) return;
            var r = new Rng(lv * 1009 + tries * 7919 + 13);
            var map = new int[n];
            for (int k = 0; k < 20; k++)
            {
                for (int i = 0; i < n; i++) map[i] = i;
                for (int i = n - 1; i > 0; i--) { int j = (int)(r.Next() * (i + 1)); if (j > i) j = i; int t = map[i]; map[i] = map[j]; map[j] = t; }
                bool same = true; for (int i = 0; i < n; i++) if (map[i] != i) { same = false; break; }
                if (!same) break;       // 别正好洗回原样
            }
            Rules.Recolor(L, map);
        }
    }
}
