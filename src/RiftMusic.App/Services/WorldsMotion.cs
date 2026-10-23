using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Runtime.CompilerServices;

namespace RiftMusic.App.Services;

public static class WorldsMotion
{
    private sealed class Generation { public int Value; public Geometry? OriginalClip; public bool OwnsClip; }
    private static readonly ConditionalWeakTable<FrameworkElement,Generation> generations=new();
    public static void Reconfigure(FrameworkElement element,bool enabled,double x=0,double y=12)
    {
        var generation=generations.GetOrCreateValue(element);var current=++generation.Value;
        if(generation.OwnsClip){element.Clip=generation.OriginalClip;generation.OwnsClip=false;}
        element.BeginAnimation(UIElement.OpacityProperty,null);element.Opacity=1;
        if(element.RenderTransform is not TranslateTransform transform) element.RenderTransform=transform=new TranslateTransform();
        transform.BeginAnimation(TranslateTransform.XProperty,null);transform.BeginAnimation(TranslateTransform.YProperty,null);
        transform.X=transform.Y=0;
        if(!enabled)return;
        var easing=new CubicEase{EasingMode=EasingMode.EaseOut};
        // FillBehavior.Stop releases clocks; the base state is already the final visible state.
        DoubleAnimation Move(double from)=>new(from,0,TimeSpan.FromMilliseconds(260)){EasingFunction=easing,FillBehavior=FillBehavior.Stop};
        var vertical=Move(y);
        vertical.Completed+=(_,_)=>{
            if(generation.Value!=current)return;
            transform.BeginAnimation(TranslateTransform.XProperty,null);transform.BeginAnimation(TranslateTransform.YProperty,null);
            element.BeginAnimation(UIElement.OpacityProperty,null);
        };
        transform.BeginAnimation(TranslateTransform.XProperty,Move(x));transform.BeginAnimation(TranslateTransform.YProperty,vertical);
        element.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(.65,1,TimeSpan.FromMilliseconds(180)){FillBehavior=FillBehavior.Stop});
    }
    public static void Reveal(FrameworkElement element,bool enabled,double x=0)
    {
        Reconfigure(element,enabled,x,0);
        if(!enabled || element.ActualWidth<=0 || element.ActualHeight<=0)return;
        var generation=generations.GetOrCreateValue(element);var current=generation.Value;
        generation.OriginalClip=element.Clip;generation.OwnsClip=true;
        var bounds=new Rect(0,0,element.ActualWidth,element.ActualHeight);
        var clip=new RectangleGeometry(bounds);element.Clip=clip;
        var reveal=new RectAnimation(new Rect(0,0,0,bounds.Height),bounds,TimeSpan.FromMilliseconds(320)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop};
        reveal.Completed+=(_,_)=>{if(generation.Value!=current)return;clip.BeginAnimation(RectangleGeometry.RectProperty,null);element.Clip=generation.OriginalClip;generation.OwnsClip=false;};
        clip.BeginAnimation(RectangleGeometry.RectProperty,reveal);
    }
    public static void Expand(System.Windows.Controls.Border viewport,bool expanded,bool enabled)
    {
        var generation=generations.GetOrCreateValue(viewport);var current=++generation.Value;
        var previous=viewport.ActualHeight;
        viewport.BeginAnimation(FrameworkElement.HeightProperty,null);
        viewport.Visibility=Visibility.Visible;viewport.IsHitTestVisible=expanded;
        double target=0;
        if(expanded && viewport.Child is FrameworkElement content) {
            content.Measure(new Size(Math.Max(174,viewport.ActualWidth),double.PositiveInfinity));
            target=Math.Min(184,content.DesiredSize.Height);
        }
        viewport.Height=target;
        if(!enabled){if(!expanded)viewport.Visibility=Visibility.Collapsed;return;}
        var animation=new DoubleAnimation(previous,target,TimeSpan.FromMilliseconds(expanded?240:180)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop};
        animation.Completed+=(_,_)=>{if(generation.Value!=current)return;viewport.BeginAnimation(FrameworkElement.HeightProperty,null);if(!expanded)viewport.Visibility=Visibility.Collapsed;};
        viewport.BeginAnimation(FrameworkElement.HeightProperty,animation);
    }
}
