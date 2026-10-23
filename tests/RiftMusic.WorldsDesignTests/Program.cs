using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using RiftMusic.App.Services;

internal static class Program
{
    [STAThread] static int Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var root = Path.Combine(Path.GetTempPath(), "RiftMusic-Worlds-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var passed = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); passed++; }
        try
        {
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/RiftMusic;component/Styles.xaml") });
            var status = WorldsTypography.Configure(app.Resources, Path.Combine(root, "Fonts"), includeSystemFonts:false);
            Check(!status.Contains("Manuka") && status.Contains("替代"), "fallback never claims original commercial typefaces");
            foreach (var entry in new[] { ("WorldsDisplayFont", FontWeights.Bold, "Teko-Bold.ttf"), ("WorldsSansFont", FontWeights.Normal, "Barlow-Regular.ttf"), ("WorldsMonoFont", FontWeights.Normal, "IBMPlexMono-Regular.ttf"), ("ChineseDisplayFont", FontWeights.Bold, "AlimamaShuHeiTi-Bold.ttf") })
            {
                var face = new Typeface((FontFamily)app.Resources[entry.Item1], FontStyles.Normal, entry.Item2, FontStretches.Normal);
                var found=face.TryGetGlyphTypeface(out var glyph);Console.WriteLine(entry.Item1+" => "+(found?glyph.FontUri.ToString():"no glyph")+" / "+face.FontFamily.Source);
                Check(found && glyph.FontUri.ToString().Contains(entry.Item3, StringComparison.OrdinalIgnoreCase), "actual embedded face resolves: " + entry.Item1);
            }
            var drawing=new DrawingVisual();
            using(var context=drawing.RenderOpen())context.DrawText(new FormattedText("WORLDS 全球总决赛",System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface((FontFamily)app.Resources["MusicFont"],FontStyles.Normal,FontWeights.Normal,FontStretches.Normal),18,Brushes.Black,1),new Point());
            var glyphs=new List<string>();
            void Inspect(Drawing d){if(d is DrawingGroup group)foreach(var child in group.Children)Inspect(child);else if(d is GlyphRunDrawing run)glyphs.Add(run.GlyphRun.GlyphTypeface.FontUri.ToString());}
            Inspect(drawing.Drawing);
            Check(glyphs.Any(x=>x.Contains("barlow-regular.ttf",StringComparison.OrdinalIgnoreCase)) && glyphs.Any(x=>x.Contains("msyh",StringComparison.OrdinalIgnoreCase) || x.Contains("sourcehansanscn-regular.otf",StringComparison.OrdinalIgnoreCase)),"mixed body text uses actual Latin and Chinese regular faces");
            var appearance = new AppearanceStore(root);
            Check(appearance.Load().MotionEnabled, "first launch enables bounded interface motion");
            appearance.Save(new AppearanceOptions { MotionEnabled=false });
            Check(!appearance.Load().MotionEnabled, "reduced motion preference survives restart independently of playback settings");
            File.WriteAllText(Path.Combine(root,"appearance.json"), "{broken json");
            Check(appearance.Load().MotionEnabled, "corrupt appearance file cannot prevent app startup");
            var element = new Border();
            var preview=new Window{Content=element,Width=360,Height=200,ShowInTaskbar=false};preview.Show();
            WorldsMotion.Reconfigure(element, true, 24, 8);
            WorldsMotion.Reconfigure(element, false, 24, 8);
            Check(element.Opacity==1 && element.RenderTransform is TranslateTransform t && t.X==0 && t.Y==0 && !t.HasAnimatedProperties, "turning motion off cancels in-flight transforms and keeps content visible");
            var result = 1;
            Dispatcher.CurrentDispatcher.InvokeAsync(async () => {
                try {
                    WorldsMotion.Reconfigure(element,true,24,8);
                    await Task.Delay(450);
                    Check(element.Opacity==1 && element.RenderTransform is TranslateTransform settled && settled.X==0 && settled.Y==0 && !settled.HasAnimatedProperties, "reconfiguration settles without permanent animation clocks");
                    var originalClip=new RectangleGeometry(new Rect(0,0,100,100));element.Clip=originalClip;
                    WorldsMotion.Reveal(element,true,12);
                    Check(element.Clip is RectangleGeometry reveal && reveal.HasAnimatedProperties,"Worlds title reveal uses an actual animated clipping rectangle");
                    await Task.Delay(100);
                    WorldsMotion.Reconfigure(element,false);
                    Check(ReferenceEquals(element.Clip,originalClip) && element.Opacity==1,"interrupting a reveal restores the original crop and visible content");
                    WorldsMotion.Reveal(element,true,12);await Task.Delay(420);
                    Check(ReferenceEquals(element.Clip,originalClip),"completed reveal restores an existing image crop");
                    var viewport=new Border{Child=new Border{Height=160},Width=180,Height=0};preview.Content=viewport;preview.UpdateLayout();
                    WorldsMotion.Expand(viewport,true,true);await Task.Delay(80);preview.UpdateLayout();
                    Check(viewport.ActualHeight>0 && viewport.ActualHeight<160 && viewport.HasAnimatedProperties,"year panel progressively reveals its layout height");
                    WorldsMotion.Expand(viewport,false,true);await Task.Delay(40);WorldsMotion.Expand(viewport,true,true);await Task.Delay(350);
                    Check(viewport.Visibility==Visibility.Visible && viewport.Height==160 && !viewport.HasAnimatedProperties,"rapid collapse and reopen cannot leave an expanded panel hidden");
                    WorldsMotion.Expand(viewport,false,false);
                    Check(viewport.Height==0 && viewport.Visibility==Visibility.Collapsed && !viewport.HasAnimatedProperties,"reduced motion completes collapse immediately without clocks");
                    result=0;
                } catch(Exception error) { Console.Error.WriteLine(error); }
                finally { app.Shutdown(result); }
            });
            app.Run();
            Console.WriteLine($"WORLDS DESIGN TESTS: {passed} passed");
            return result;
        }
        catch(Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { Directory.Delete(root,true); }
    }
}
