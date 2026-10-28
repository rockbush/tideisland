// 动画调度：和网页版一样用 "tween(时长, p=>...)" + await 串动画。
// 所有回调都在主线程的 Update 里推进（不用 Task.Delay），所以暂停/切后台时动画也跟着停。
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Tide
{
    public class Fx : MonoBehaviour
    {
        class Tw { public float t0, dur; public Action<float> fn; public TaskCompletionSource<bool> tcs; }
        class Sl { public float until; public TaskCompletionSource<bool> tcs; }
        static readonly List<Tw> tweens = new List<Tw>();
        static readonly List<Sl> sleeps = new List<Sl>();
        // 自动测试拍慢动作用（-tideSlow 0.25）：换速度时接着当前时间走，不会跳
        public static float Speed { get; private set; } = 1;
        static double baseMs, baseReal;
        public static void SetSpeed(float s) { baseMs = Now; baseReal = Time.unscaledTimeAsDouble; Speed = s; }
        public static float Now => (float)(baseMs + (Time.unscaledTimeAsDouble - baseReal) * 1000 * Speed);   // 毫秒，和 performance.now() 一样用
        public static float Dt => Time.unscaledDeltaTime * Speed;

        public static Task Tween(float ms, Action<float> fn)
        {
            if (float.IsNaN(ms) || float.IsInfinity(ms)) { Debug.LogWarning("[Tide] 动画时长非法 " + ms + "\n" + Environment.StackTrace); ms = 1; }
            var t = new Tw { t0 = Now, dur = Mathf.Max(1, ms), fn = fn, tcs = new TaskCompletionSource<bool>() };
            tweens.Add(t);
            return t.tcs.Task;
        }

        public static int TweenCount => tweens.Count;
        public static Task Sleep(float ms)
        {
            var s = new Sl { until = Now + ms, tcs = new TaskCompletionSource<bool>() };
            sleeps.Add(s);
            return s.tcs.Task;
        }

        // 自动测试用的等待：换关的 Clear() 不会丢掉它（不然测试脚本会永远等下去）
        static readonly List<Sl> keep = new List<Sl>();
        public static Task SleepKeep(float ms)
        {
            var s = new Sl { until = Now + ms, tcs = new TaskCompletionSource<bool>() };
            keep.Add(s);
            return s.tcs.Task;
        }

        public static void Clear()
        {
            // 换关时：没跑完的动画直接丢掉（等它们的 await 永远不会回来；回调里都有 tok 检查）
            tweens.Clear();
            sleeps.Clear();
        }

        void Update()
        {
            float now = Now;
            for (int k = tweens.Count - 1; k >= 0; k--)
            {
                if (k >= tweens.Count) continue;
                var t = tweens[k];
                float p = Mathf.Min(1, (now - t.t0) / t.dur);
                try { t.fn(p); } catch (Exception e) { Debug.LogException(e); }
                if (p >= 1)
                {
                    int i = tweens.IndexOf(t);
                    if (i >= 0) tweens.RemoveAt(i);
                    t.tcs.TrySetResult(true);
                }
            }
            for (int k = keep.Count - 1; k >= 0; k--) if (now >= keep[k].until) { var s = keep[k]; keep.RemoveAt(k); s.tcs.TrySetResult(true); }
            for (int k = sleeps.Count - 1; k >= 0; k--)
            {
                if (k >= sleeps.Count) continue;
                var s = sleeps[k];
                if (now >= s.until) { sleeps.RemoveAt(k); s.tcs.TrySetResult(true); }
            }
        }

        public static float BackOut(float t) { const float c = 1.9f; return 1 + (c + 1) * Mathf.Pow(t - 1, 3) + c * Mathf.Pow(t - 1, 2); }

        public static float LerpAng(float a, float b, float t)
        {
            float d = ((b - a) % (Mathf.PI * 2) + Mathf.PI * 3) % (Mathf.PI * 2) - Mathf.PI;
            return a + d * t;
        }

        // 不想等结果的异步调用：出错时打日志，而不是悄悄吞掉
        public static async void Run(Func<Task> f)
        {
            try { await f(); } catch (Exception e) { Debug.LogException(e); }
        }
    }
}
