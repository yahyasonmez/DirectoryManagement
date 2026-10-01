using System.Windows;
using System.Windows.Media.Animation;

namespace DirectoryManagement;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        Opacity = 1;
    }

    public Task GrowToAsync(double targetWidth, double targetHeight, TimeSpan duration)
    {
        var tcs = new TaskCompletionSource();
        var work = SystemParameters.WorkArea;
        var targetLeft = work.Left + (work.Width - targetWidth) / 2;
        var targetTop = work.Top + (work.Height - targetHeight) / 2;
        var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };

        var widthAnim = new DoubleAnimation(Width, targetWidth, new Duration(duration)) { EasingFunction = easing };
        var heightAnim = new DoubleAnimation(Height, targetHeight, new Duration(duration)) { EasingFunction = easing };
        var leftAnim = new DoubleAnimation(Left, targetLeft, new Duration(duration)) { EasingFunction = easing };
        var topAnim = new DoubleAnimation(Top, targetTop, new Duration(duration)) { EasingFunction = easing };

        widthAnim.Completed += (_, _) => tcs.TrySetResult();

        BeginAnimation(WidthProperty, widthAnim);
        BeginAnimation(HeightProperty, heightAnim);
        BeginAnimation(LeftProperty, leftAnim);
        BeginAnimation(TopProperty, topAnim);
        return tcs.Task;
    }

    public Task FadeOutAsync(TimeSpan duration)
    {
        var tcs = new TaskCompletionSource();

        var animation = new DoubleAnimation
        {
            From = Opacity,
            To = 0,
            Duration = duration,
            FillBehavior = FillBehavior.Stop
        };

        animation.Completed += (_, _) =>
        {
            Opacity = 0;
            tcs.TrySetResult();
        };

        BeginAnimation(OpacityProperty, animation);
        return tcs.Task;
    }
}
