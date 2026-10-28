// iOS 震动反馈（Haptics.cs 调用）。kind: 0 轻 1 中 2 重 3 成功 4 失败
#import <UIKit/UIKit.h>

extern "C" void TideHaptic(int kind)
{
    if (kind == 3 || kind == 4)
    {
        UINotificationFeedbackGenerator *g = [[UINotificationFeedbackGenerator alloc] init];
        [g notificationOccurred:(kind == 3 ? UINotificationFeedbackTypeSuccess : UINotificationFeedbackTypeError)];
        return;
    }
    UIImpactFeedbackStyle s = kind == 0 ? UIImpactFeedbackStyleLight : kind == 1 ? UIImpactFeedbackStyleMedium : UIImpactFeedbackStyleHeavy;
    UIImpactFeedbackGenerator *g = [[UIImpactFeedbackGenerator alloc] initWithStyle:s];
    [g impactOccurred];
}
