using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AbyssXLauncher
{
    public class Theme
    {
        public string Id;
        public bool Light, Rainbow;
        public Brush Win, Side, Panel, Panel2, Text, Muted, Accent, OnAccent, Line, Input, Solid, Star;
        public Color Glow;

        public static readonly string[] Ids = { "abyss", "gold", "rainbow" };
        public static readonly string[] Names = { "AbyssX", "Blanc doré", "Multicolore" };

        static Color C(string h) { return (Color)ColorConverter.ConvertFromString(h); }
        static Brush S(string h, double op = 1.0) { var b = new SolidColorBrush(C(h)); b.Opacity = op; b.Freeze(); return b; }
        static Brush G(string a, string b, double angle) { var g = new LinearGradientBrush(C(a), C(b), angle); g.Freeze(); return g; }

        public static readonly Brush[] Palette = { S("#FF6B8B"), S("#FFC857"), S("#7CFFB2"), S("#6FE3FF"), S("#B79BFF") };

        public static Theme Make(string id, bool anim = true)
        {
            if (id == "gold")
                return new Theme
                {
                    Id = id, Light = true,
                    Win = G("#FFFFFF", "#F6EDD3", 90), Side = S("#FFFFFF"), Panel = S("#FFFFFF"), Panel2 = S("#F7F0DC"),
                    Text = S("#2B2412"), Muted = S("#8A7A55"),
                    Accent = GoldGradient(), OnAccent = S("#2B2412"), Line = S("#E6D8AE"), Input = S("#FBF7EA"),
                    Solid = S("#FFFFFF"), Star = S("#D4AF37"), Glow = C("#D4AF37")
                };
            if (id == "rainbow")
            {
                var acc = RainbowBrush(36, anim);
                return new Theme
                {
                    Id = id, Rainbow = true,
                    Win = RainbowBackground(anim), Side = S("#000000", 0.38), Panel = S("#0B0B1A", 0.55), Panel2 = S("#FFFFFF", 0.10),
                    Text = S("#FFFFFF"), Muted = S("#D2CCEE"),
                    Accent = acc, OnAccent = S("#0B0B1A"), Line = S("#FFFFFF", 0.18), Input = S("#000000", 0.35),
                    Solid = S("#16122E"), Star = S("#FFFFFF"), Glow = Colors.White
                };
            }
            return new Theme
            {
                Id = "abyss",
                Win = G("#0A1330", "#04070F", 90), Side = S("#090F1E"), Panel = S("#0D1527"), Panel2 = S("#142038"),
                Text = S("#E8F0FF"), Muted = S("#8497BB"),
                Accent = S("#33E1FF"), OnAccent = S("#04111C"), Line = S("#1C2A47"), Input = S("#101A30"),
                Solid = S("#0D1527"), Star = S("#CFE8FF"), Glow = C("#33E1FF")
            };
        }

        public static Brush Preview(string id)
        {
            if (id == "gold") return G("#FFFFFF", "#E9D08A", 45);
            if (id == "rainbow")
            {
                var g = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
                g.GradientStops.Add(new GradientStop(C("#5B1AA8"), 0));
                g.GradientStops.Add(new GradientStop(C("#0E6BC9"), 0.5));
                g.GradientStops.Add(new GradientStop(C("#0FA38F"), 1));
                return g;
            }
            return G("#12306A", "#04070F", 45);
        }

        static Brush GoldGradient()
        {
            var g = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            g.GradientStops.Add(new GradientStop(C("#F2D888"), 0));
            g.GradientStops.Add(new GradientStop(C("#D4AF37"), 0.55));
            g.GradientStops.Add(new GradientStop(C("#B8891A"), 1));
            g.Freeze();
            return g;
        }

        static readonly string[] Hues = { "#FF4D6D", "#FFB84D", "#FFE94D", "#4DFF88", "#4DD8FF", "#8A6DFF", "#FF4DD8" };

        // Pinceau dont la couleur change lentement dans tout l'arc-en-ciel (boutons, curseur)
        public static SolidColorBrush RainbowBrush(double seconds, bool anim = true)
        {
            var b = new SolidColorBrush(C(Hues[0]));
            var a = new ColorAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(seconds), RepeatBehavior = RepeatBehavior.Forever };
            for (int i = 0; i <= Hues.Length; i++)
                a.KeyFrames.Add(new LinearColorKeyFrame(C(Hues[i % Hues.Length]), KeyTime.FromPercent((double)i / Hues.Length)));
            if (anim) b.BeginAnimation(SolidColorBrush.ColorProperty, a);
            return b;
        }

        // Fond dégradé multicolore qui évolue très lentement
        static Brush RainbowBackground(bool anim)
        {
            string[] cols = { "#2B0A4F", "#0A2C5C", "#07494A", "#4A0A3A", "#4F2A08" };
            var g = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            for (int k = 0; k < 3; k++)
            {
                var stop = new GradientStop(C(cols[k]), k / 2.0);
                var a = new ColorAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(60), RepeatBehavior = RepeatBehavior.Forever };
                for (int i = 0; i <= cols.Length; i++)
                    a.KeyFrames.Add(new LinearColorKeyFrame(C(cols[(k * 2 + i) % cols.Length]), KeyTime.FromPercent((double)i / cols.Length)));
                g.GradientStops.Add(stop);
                if (anim) stop.BeginAnimation(GradientStop.ColorProperty, a);
            }
            return g;
        }
    }

    // Petits composants réutilisés partout
    public static class Ui
    {
        public static Theme T = Theme.Make("abyss");
        static ControlTemplate tpl;

        static ControlTemplate Tpl()
        {
            if (tpl != null) return tpl;
            var t = new ControlTemplate(typeof(Button));
            var bd = new FrameworkElementFactory(typeof(Border));
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            bd.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
            bd.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
            bd.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = RelativeSource.TemplatedParent });
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            cp.SetBinding(FrameworkElement.HorizontalAlignmentProperty, new Binding("HorizontalContentAlignment") { RelativeSource = RelativeSource.TemplatedParent });
            cp.SetBinding(FrameworkElement.MarginProperty, new Binding("Padding") { RelativeSource = RelativeSource.TemplatedParent });
            bd.AppendChild(cp);
            t.VisualTree = bd;
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(UIElement.OpacityProperty, 0.86));
            t.Triggers.Add(hover);
            var off = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            off.Setters.Add(new Setter(UIElement.OpacityProperty, 0.5));
            t.Triggers.Add(off);
            tpl = t;
            return t;
        }

        public static TextBlock Txt(string s, double size = 14, bool bold = false, Brush fg = null, bool wrap = false)
        {
            return new TextBlock
            {
                Text = s, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = fg ?? T.Text,
                TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
                TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis
            };
        }

        public static Button Btn(string text, Action click, bool primary = true, double size = 14)
        {
            var b = new Button
            {
                Content = text, FontSize = size, FontWeight = FontWeights.SemiBold, Template = Tpl(),
                Padding = new Thickness(18, 9, 18, 9), HorizontalAlignment = HorizontalAlignment.Left,
                Foreground = primary ? T.OnAccent : T.Text,
                Background = primary ? T.Accent : T.Panel2,
                BorderBrush = T.Line, BorderThickness = new Thickness(primary ? 0 : 1)
            };
            b.Click += (s, e) => click();
            return b;
        }

        public static Button Chip(string text, bool on, Action click)
        {
            var b = Btn(text, click, on, 13);
            b.Padding = new Thickness(14, 6, 14, 6);
            b.Margin = new Thickness(0, 0, 8, 8);
            return b;
        }

        public static Button Link(string text, Action click, double size = 12)
        {
            var b = new Button
            {
                Content = text, FontSize = size, Template = Tpl(), Padding = new Thickness(6, 3, 6, 3),
                Foreground = T.Muted, Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            b.Click += (s, e) => click();
            return b;
        }

        public static Button Nav(string text, bool on, Action click)
        {
            var b = new Button
            {
                Content = text, FontSize = 14, Template = Tpl(), Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 2), HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = on ? T.Accent : T.Text, Background = on ? T.Panel2 : Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            b.Click += (s, e) => click();
            return b;
        }

        public static Border Tag(string text)
        {
            return new Border
            {
                Background = T.Panel2, CornerRadius = new CornerRadius(6), Padding = new Thickness(9, 3, 9, 3), Margin = new Thickness(0, 0, 8, 0),
                Child = Txt(text, 12, false, T.Muted)
            };
        }

        public static Border Card(UIElement child, double pad = 18)
        {
            return new Border
            {
                Background = T.Panel, BorderBrush = T.Line, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12), Padding = new Thickness(pad), Child = child
            };
        }

        public static TextBox Input(string text = "", bool multi = false)
        {
            return new TextBox
            {
                Text = text ?? "", FontSize = 14, Padding = new Thickness(10, 8, 10, 8),
                Background = T.Input, Foreground = T.Text, BorderBrush = T.Line, BorderThickness = new Thickness(1),
                AcceptsReturn = multi, TextWrapping = multi ? TextWrapping.Wrap : TextWrapping.NoWrap,
                MinHeight = multi ? 90 : 0,
                VerticalScrollBarVisibility = multi ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled
            };
        }

        public static PasswordBox Pass()
        {
            return new PasswordBox
            {
                FontSize = 14, Padding = new Thickness(10, 8, 10, 8),
                Background = T.Input, Foreground = T.Text, BorderBrush = T.Line, BorderThickness = new Thickness(1)
            };
        }

        public static StackPanel Field(string label, FrameworkElement input)
        {
            var s = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            s.Children.Add(Txt(label, 12, false, T.Muted));
            input.Margin = new Thickness(0, 5, 0, 0);
            s.Children.Add(input);
            return s;
        }

        public static CheckBox Check(string text, bool on)
        {
            return new CheckBox { Content = text, IsChecked = on, Foreground = T.Text, FontSize = 13.5, Margin = new Thickness(0, 4, 0, 8) };
        }
    }
}
