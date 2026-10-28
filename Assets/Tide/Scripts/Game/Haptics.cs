// 手机震动反馈。安卓走系统 Vibrator（Android 10+ 用系统预设的"咔哒"手感，8~9 用短震），iOS 走 Plugins/iOS/TideHaptics.mm。
// 电脑上什么都不做。连着触发时 40ms 内只震一次，免得连击时手机嗡嗡响。
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Tide
{
    public static class Haptics
    {
        public enum Kind { Light = 0, Medium = 1, Heavy = 2, Success = 3, Fail = 4 }
        public static bool On = true;
        static float last = -1;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void TideHaptic(int kind);
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vib;
        static int sdk = -1;
        static void InitAndroid()
        {
            if (sdk >= 0) return;
            sdk = 0;
            try
            {
                using (var ver = new AndroidJavaClass("android.os.Build$VERSION")) sdk = ver.GetStatic<int>("SDK_INT");
                using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
                    vib = act.Call<AndroidJavaObject>("getSystemService", "vibrator");
                if (vib != null && !vib.Call<bool>("hasVibrator")) vib = null;
            }
            catch (Exception e) { Debug.LogWarning("[Tide] 震动不可用：" + e.Message); vib = null; }
        }
        static void Android(Kind k)
        {
            InitAndroid();
            if (vib == null) return;
            try
            {
                if (sdk >= 26)
                {
                    using (var ve = new AndroidJavaClass("android.os.VibrationEffect"))
                    {
                        AndroidJavaObject eff;
                        if (k == Kind.Success) eff = ve.CallStatic<AndroidJavaObject>("createWaveform", new long[] { 0, 30, 70, 45 }, new int[] { 0, 140, 0, 255 }, -1);
                        else if (k == Kind.Fail) eff = ve.CallStatic<AndroidJavaObject>("createWaveform", new long[] { 0, 60, 60, 90 }, new int[] { 0, 255, 0, 200 }, -1);
                        // Android 10+ 预设：TICK=2 很轻，CLICK=0，HEAVY_CLICK=5
                        else if (sdk >= 29) eff = ve.CallStatic<AndroidJavaObject>("createPredefined", k == Kind.Light ? 2 : k == Kind.Medium ? 0 : 5);
                        else eff = ve.CallStatic<AndroidJavaObject>("createOneShot", (long)(k == Kind.Light ? 12 : k == Kind.Medium ? 22 : 40), k == Kind.Light ? 90 : k == Kind.Medium ? 170 : 255);
                        vib.Call("vibrate", eff);
                        eff.Dispose();
                    }
                }
                else if (k != Kind.Light) Handheld.Vibrate();   // 老系统只有一种很长的震动，轻的就不震了；这一行也让 Unity 自动加上 VIBRATE 权限
            }
            catch (Exception e) { Debug.LogWarning("[Tide] 震动失败：" + e.Message); vib = null; }
        }
#endif

        public static void Play(Kind k)
        {
            if (!On) return;
            float now = Time.unscaledTime;
            if (now - last < 0.04f && k == Kind.Light) return;
            last = now;
#if UNITY_ANDROID && !UNITY_EDITOR
            Android(k);
#elif UNITY_IOS && !UNITY_EDITOR
            TideHaptic((int)k);
#endif
        }
    }
}
