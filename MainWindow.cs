using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Shp = System.Windows.Shapes;

namespace AbyssXLauncher
{
    public partial class MainWindow : Window
    {
        Config cfg;
        Settings st;
        IBackend be;
        Session me;
        Lib lib = new Lib();
        bool remember = true, suppress;
        List<Game> games = new List<Game>();
        string page = "store", arg, query = "", genre = "", legalTab = "cgu", authTab = "in";
        Grid root, overlay;
        Canvas starLayer, curLayer;
        Shp.Path curShape;
        TranslateTransform curTt;
        StackPanel toastBox;
        ContentControl main, viewHost;
        Border side, searchHost;
        TextBox searchBox;
        readonly List<Tuple<Shp.Ellipse, double, double>> starList = new List<Tuple<Shp.Ellipse, double, double>>();
        readonly Random rnd = new Random();
        readonly Dictionary<string, int> prog = new Dictionary<string, int>();
        readonly HashSet<string> running = new HashSet<string>();

        public MainWindow()
        {
            Title = "AbyssX Launcher";
            Width = 1280; Height = 800; MinWidth = 1000; MinHeight = 660;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = new FontFamily("Segoe UI");
            try { Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/logo.png")); } catch { }

            cfg = Disk.LoadConfig();
            st = Disk.Load<Settings>("settings.json");
            bool online = !string.IsNullOrWhiteSpace(cfg.SupabaseUrl) && !string.IsNullOrWhiteSpace(cfg.SupabaseKey);
            be = online ? (IBackend)new SupaBackend(cfg) : new LocalBackend(cfg);

            root = new Grid();
            starLayer = new Canvas { IsHitTestVisible = false };
            main = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
            toastBox = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 26), IsHitTestVisible = false };
            curLayer = new Canvas { IsHitTestVisible = false };
            root.Children.Add(starLayer);
            root.Children.Add(main);
            root.Children.Add(toastBox);
            root.Children.Add(curLayer);
            Content = root;

            ApplyTheme();
            SizeChanged += (s, e) => PlaceStars();
            PreviewMouseMove += OnMove;
            MouseLeave += (s, e) => { if (curShape != null) curShape.Visibility = Visibility.Collapsed; };
            PreviewKeyDown += OnKey;
            Closing += (s, e) => { Mouse.OverrideCursor = null; };
            Loaded += async (s, e) => await Start();
        }

        // ------------------------------------------------------------------ ambiance
        void ApplyTheme()
        {
            Ui.T = Theme.Make(st.Theme, st.Anim);
            Background = Ui.T.Win;
            MakeStars();
            ApplyCursor();
        }

        void MakeStars()
        {
            starLayer.Children.Clear();
            starList.Clear();
            if (!st.Stars) return;
            for (int i = 0; i < 90; i++)
            {
                double size = 1 + rnd.NextDouble() * 2;
                var e = new Shp.Ellipse { Width = size, Height = size, Opacity = 0.25 };
                e.Fill = Ui.T.Rainbow ? Theme.Palette[rnd.Next(Theme.Palette.Length)] : Ui.T.Star;
                starLayer.Children.Add(e);
                starList.Add(Tuple.Create(e, rnd.NextDouble(), rnd.NextDouble()));
                if (st.Anim)
                {
                    var a = new DoubleAnimation(0.12, 1.0, TimeSpan.FromSeconds(1.2 + rnd.NextDouble() * 3))
                    {
                        AutoReverse = true,
                        RepeatBehavior = RepeatBehavior.Forever,
                        BeginTime = TimeSpan.FromSeconds(rnd.NextDouble() * 3)
                    };
                    e.BeginAnimation(UIElement.OpacityProperty, a);
                }
            }
            PlaceStars();
        }

        void PlaceStars()
        {
            double w = ActualWidth, h = ActualHeight;
            foreach (var t in starList)
            {
                Canvas.SetLeft(t.Item1, t.Item2 * w);
                Canvas.SetTop(t.Item1, t.Item3 * h);
            }
        }

        void ApplyCursor()
        {
            curLayer.Children.Clear();
            curShape = null;
            if (!st.Cursor) { Mouse.OverrideCursor = null; return; }
            curShape = new Shp.Path
            {
                Data = Geometry.Parse("M0,-12 L3.2,-3.2 L12,0 L3.2,3.2 L0,12 L-3.2,3.2 L-12,0 L-3.2,-3.2 Z"),
                Fill = Ui.T.Accent,
                Visibility = Visibility.Collapsed,
                Effect = new DropShadowEffect { Color = Ui.T.Glow, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.95 }
            };
            curTt = new TranslateTransform();
            curShape.RenderTransform = curTt;
            curLayer.Children.Add(curShape);
            Mouse.OverrideCursor = Cursors.None;
        }

        void OnMove(object sender, MouseEventArgs e)
        {
            if (curShape == null) return;
            Point p = e.GetPosition(curLayer);
            curTt.X = p.X;
            curTt.Y = p.Y;
            curShape.Visibility = Visibility.Visible;
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && overlay != null) { CloseOverlay(); e.Handled = true; }
            else if (e.Key == Key.C && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                st.Cursor = !st.Cursor;
                Disk.Save("settings.json", st);
                ApplyCursor();
                e.Handled = true;
            }
            else if (e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.Control && searchBox != null) { searchBox.Focus(); e.Handled = true; }
        }

        public void Toast(string msg, bool bad = false)
        {
            var b = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(238, 16, 24, 40)),
                BorderBrush = bad ? new SolidColorBrush(Color.FromRgb(255, 107, 125)) : new SolidColorBrush(Color.FromRgb(70, 90, 130)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 10, 16, 10),
                Margin = new Thickness(0, 6, 0, 0), MaxWidth = 560,
                Child = new TextBlock { Text = msg, Foreground = Brushes.White, FontSize = 14, TextWrapping = TextWrapping.Wrap }
            };
            toastBox.Children.Add(b);
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.8) };
            t.Tick += (s, e) => { t.Stop(); toastBox.Children.Remove(b); };
            t.Start();
        }

        // ------------------------------------------------------------------ session
        async Task Start()
        {
            var saved = Disk.Load<Session>("session.json");
            if (!string.IsNullOrEmpty(saved.RefreshToken))
            {
                try { me = await be.Refresh(saved); await AfterLogin(true); return; }
                catch (Exception) { me = null; }
            }
            ShowAuth();
        }

        async Task AfterLogin(bool keep)
        {
            remember = keep;
            SaveSession();
            lib = Disk.Load<Lib>("lib_" + me.UserId + ".json");
            page = "store"; arg = null; query = ""; genre = ""; editing = null;
            ShowShell();
            await LoadGames();
        }

        void SaveSession()
        {
            if (remember && me != null) Disk.Save("session.json", me); else Disk.Delete("session.json");
        }

        void SaveLib()
        {
            if (me != null) Disk.Save("lib_" + me.UserId + ".json", lib);
        }

        void Logout()
        {
            me = null;
            Disk.Delete("session.json");
            games = new List<Game>();
            ShowAuth();
        }

        // Renouvelle la session si le jeton a expiré (les sessions durent environ 1 heure)
        async Task<T> Retry<T>(Func<Task<T>> f)
        {
            try { return await f(); }
            catch (Exception ex)
            {
                string m = ex.Message ?? "";
                bool expired = m.IndexOf("jwt", StringComparison.OrdinalIgnoreCase) >= 0 || m.IndexOf("expired", StringComparison.OrdinalIgnoreCase) >= 0;
                if (expired && me != null && !string.IsNullOrEmpty(me.RefreshToken))
                {
                    me = await be.Refresh(me);
                    SaveSession();
                    return await f();
                }
                throw;
            }
        }

        async Task LoadGames()
        {
            try { games = await Retry(() => be.ListGames(me)); }
            catch (Exception ex) { Toast(ex.Message, true); }
            ShowPage(true);
        }

        // ------------------------------------------------------------------ connexion / inscription
        void ShowAuth()
        {
            CloseOverlay();
            side = null; viewHost = null; searchBox = null; searchHost = null;
            bool reg = authTab == "up";

            var card = new StackPanel { Width = 400 };
            var head = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 18) };
            var logo = Img.Load("pack://application:,,,/Assets/logo.png");
            if (logo != null)
                head.Children.Add(new Image { Source = logo, Width = 96, Height = 96, Effect = new DropShadowEffect { Color = Ui.T.Glow, BlurRadius = 34, ShadowDepth = 0, Opacity = 0.6 } });
            var title = Ui.Txt("AbyssX Launcher", 26, true);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.Margin = new Thickness(0, 10, 0, 2);
            var sub = Ui.Txt("Tous les jeux AbyssX, au même endroit.", 13, false, Ui.T.Muted);
            sub.HorizontalAlignment = HorizontalAlignment.Center;
            head.Children.Add(title);
            head.Children.Add(sub);
            card.Children.Add(head);

            var form = new StackPanel();
            var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
            tabs.Children.Add(Ui.Chip("Connexion", !reg, () => { authTab = "in"; ShowAuth(); }));
            tabs.Children.Add(Ui.Chip("Créer un compte", reg, () => { authTab = "up"; ShowAuth(); }));
            form.Children.Add(tabs);

            var email = Ui.Input();
            var user = Ui.Input();
            var pw = Ui.Pass();
            var pw2 = Ui.Pass();
            var keep = Ui.Check("Rester connecté", true);
            var age = Ui.Check("J'ai 15 ans ou plus, ou l'accord de mon représentant légal.", false);
            var ok = Ui.Check("J'accepte les conditions d'utilisation et la politique de confidentialité.", false);
            var err = Ui.Txt("", 13, false, new SolidColorBrush(Color.FromRgb(255, 107, 125)), true);
            err.Margin = new Thickness(0, 0, 0, 10);

            form.Children.Add(Ui.Field("Adresse e-mail", email));
            if (reg) form.Children.Add(Ui.Field("Identifiant affiché", user));
            form.Children.Add(Ui.Field("Mot de passe", pw));
            if (reg)
            {
                form.Children.Add(Ui.Field("Confirmer le mot de passe", pw2));
                form.Children.Add(age);
                form.Children.Add(ok);
            }
            else form.Children.Add(keep);
            form.Children.Add(err);

            Button go = null;
            go = Ui.Btn(reg ? "Créer mon compte" : "Se connecter", async () =>
            {
                err.Text = "";
                go.IsEnabled = false;
                try
                {
                    string em = email.Text.Trim();
                    if (!reg)
                    {
                        me = await be.SignIn(em, pw.Password);
                        await AfterLogin(keep.IsChecked == true);
                        return;
                    }
                    string un = user.Text.Trim();
                    if (!Regex.IsMatch(em, @"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$")) throw new Exception("Adresse e-mail invalide.");
                    if (!Regex.IsMatch(un, @"^[A-Za-z0-9_]{3,20}$")) throw new Exception("Identifiant : 3 à 20 caractères (lettres, chiffres, _).");
                    string p = pw.Password;
                    if (p.Length < 8 || !p.Any(char.IsLetter) || !p.Any(char.IsDigit)) throw new Exception("Mot de passe : 8 caractères minimum, avec des lettres et des chiffres.");
                    if (p != pw2.Password) throw new Exception("Les deux mots de passe ne correspondent pas.");
                    if (age.IsChecked != true) throw new Exception("Confirme ton âge pour continuer.");
                    if (ok.IsChecked != true) throw new Exception("Accepte les conditions et la politique de confidentialité.");
                    var s = await be.SignUp(em, un, p);
                    if (s == null)
                    {
                        authTab = "in";
                        ShowAuth();
                        Toast("Compte créé. Ouvre l'e-mail de confirmation reçu, puis connecte-toi.");
                        return;
                    }
                    me = s;
                    await AfterLogin(true);
                }
                catch (Exception ex) { err.Text = ex.Message; }
                go.IsEnabled = true;
            });
            go.HorizontalAlignment = HorizontalAlignment.Stretch;
            go.HorizontalContentAlignment = HorizontalAlignment.Center;
            go.Padding = new Thickness(18, 12, 18, 12);
            KeyEventHandler enter = (s, e) => { if (e.Key == Key.Enter) go.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };
            email.KeyDown += enter; pw.KeyDown += enter; pw2.KeyDown += enter; user.KeyDown += enter;
            form.Children.Add(go);
            if (!reg)
            {
                var forgot = Ui.Link("Mot de passe oublié ?", () => ForgotPassword(email.Text));
                forgot.Margin = new Thickness(0, 8, 0, 0);
                form.Children.Add(forgot);
            }
            card.Children.Add(Ui.Card(form, 22));

            var legal = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 0) };
            legal.Children.Add(Ui.Link("Mentions légales", () => ShowLegal("mentions")));
            legal.Children.Add(Ui.Link("Confidentialité", () => ShowLegal("privacy")));
            legal.Children.Add(Ui.Link("Conditions", () => ShowLegal("cgu")));
            card.Children.Add(legal);
            if (be.Mode == "local")
            {
                var m = Ui.Txt("Mode local : comptes et jeux enregistrés sur ce PC. Renseigne launcher.config.json (Supabase) pour le mode en ligne.", 12, false, Ui.T.Muted, true);
                m.TextAlignment = TextAlignment.Center;
                m.Margin = new Thickness(0, 6, 0, 0);
                card.Children.Add(m);
            }

            var holder = new Border { Child = card, Padding = new Thickness(0, 30, 0, 30), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            main.Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = holder };
            email.Focus();
        }

        async void ForgotPassword(string email)
        {
            try
            {
                if (!Regex.IsMatch((email ?? "").Trim(), @"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$")) throw new Exception("Saisis d'abord ton adresse e-mail dans le champ ci-dessus.");
                await be.ResetPassword(email.Trim());
                Toast("E-mail envoyé : suis le lien pour choisir un nouveau mot de passe.");
            }
            catch (Exception ex) { Toast(ex.Message, true); }
        }

        // ------------------------------------------------------------------ textes légaux
        UIElement LegalView(Action refresh)
        {
            var sp = new StackPanel();
            var tabs = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
            foreach (var t in Legal.Tabs)
            {
                string id = t.Item1;
                tabs.Children.Add(Ui.Chip(t.Item2, legalTab == id, () => { legalTab = id; refresh(); }));
            }
            sp.Children.Add(tabs);
            sp.Children.Add(Legal.Doc(legalTab, cfg));
            return sp;
        }

        void ShowLegal(string tab)
        {
            legalTab = tab;
            CloseOverlay();
            overlay = new Grid { Background = new SolidColorBrush(Color.FromArgb(205, 3, 6, 14)) };
            var box = new DockPanel();
            var bar = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            var close = Ui.Btn("Fermer", CloseOverlay, false, 13);
            DockPanel.SetDock(close, Dock.Right);
            bar.Children.Add(close);
            bar.Children.Add(Ui.Txt("Informations légales", 20, true));
            DockPanel.SetDock(bar, Dock.Top);
            box.Children.Add(bar);
            box.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = LegalView(() => ShowLegal(legalTab)) });
            overlay.Children.Add(new Border
            {
                Background = Ui.T.Solid, BorderBrush = Ui.T.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14),
                Padding = new Thickness(26), MaxWidth = 860, Margin = new Thickness(40), Child = box
            });
            root.Children.Insert(2, overlay);
        }

        void CloseOverlay()
        {
            if (overlay != null) { root.Children.Remove(overlay); overlay = null; }
        }

        // ------------------------------------------------------------------ coque (menu + barre de recherche)
        void ShowShell()
        {
            CloseOverlay();
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(236) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            side = new Border { Background = Ui.T.Side, BorderBrush = Ui.T.Line, BorderThickness = new Thickness(0, 0, 1, 0) };
            g.Children.Add(side);

            var right = new Grid();
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(right, 1);
            g.Children.Add(right);

            searchBox = Ui.Input();
            searchBox.Width = 380;
            var hint = Ui.Txt("Rechercher un jeu…  (Ctrl+K)", 14, false, Ui.T.Muted);
            hint.IsHitTestVisible = false;
            hint.Margin = new Thickness(12, 0, 0, 0);
            hint.VerticalAlignment = VerticalAlignment.Center;
            searchBox.TextChanged += (s, e) =>
            {
                hint.Visibility = searchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                if (suppress) return;
                query = searchBox.Text.Trim();
                ShowPage();
            };
            var sg = new Grid();
            sg.Children.Add(searchBox);
            sg.Children.Add(hint);
            searchHost = new Border { Child = sg, HorizontalAlignment = HorizontalAlignment.Left };

            var top = new DockPanel { Margin = new Thickness(28, 18, 28, 4) };
            var refresh = Ui.Btn("Actualiser", async () => { await LoadGames(); Toast("Catalogue actualisé."); }, false, 13);
            DockPanel.SetDock(refresh, Dock.Right);
            top.Children.Add(refresh);
            top.Children.Add(searchHost);
            right.Children.Add(top);

            viewHost = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
            Grid.SetRow(viewHost, 1);
            right.Children.Add(viewHost);

            main.Content = g;
            RefreshSide();
            ShowPage();
        }

        void RefreshSide()
        {
            if (side == null || me == null) return;
            var dock = new DockPanel();

            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(18, 20, 18, 12) };
            var logo = Img.Load("pack://application:,,,/Assets/logo.png");
            if (logo != null) head.Children.Add(new Image { Source = logo, Width = 34, Height = 34 });
            var names = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            names.Children.Add(Ui.Txt("AbyssX", 18, true));
            names.Children.Add(Ui.Txt("Launcher", 12, false, Ui.T.Muted));
            head.Children.Add(names);
            DockPanel.SetDock(head, Dock.Top);
            dock.Children.Add(head);

            var chip = new StackPanel { Margin = new Thickness(14, 10, 14, 14) };
            var line = new StackPanel { Orientation = Orientation.Horizontal };
            line.Children.Add(Avatar(me, 38));
            var who = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, MaxWidth = 140 };
            who.Children.Add(Ui.Txt(me.Username, 14, true));
            who.Children.Add(Ui.Txt(me.IsAdmin ? "Administrateur" : "Joueur", 12, false, Ui.T.Muted));
            line.Children.Add(who);
            chip.Children.Add(line);
            chip.Children.Add(Ui.Link("Se déconnecter", Logout));
            DockPanel.SetDock(chip, Dock.Bottom);
            dock.Children.Add(chip);

            var nav = new StackPanel { Margin = new Thickness(10, 4, 10, 0) };
            Action<string, string> item = (id, label) => nav.Children.Add(Ui.Nav(label, page == id || (id == "store" && page == "game"), () => Go(id)));
            Action<string> group = t => nav.Children.Add(new TextBlock { Text = t, FontSize = 12, Foreground = Ui.T.Muted, Margin = new Thickness(14, 16, 0, 6) });
            group("Découvrir"); item("store", "Boutique");
            group("Mes jeux"); item("lib", "Bibliothèque");
            group("Compte"); item("profile", "Profil"); item("settings", "Paramètres");
            group("Infos"); item("legal", "Mentions légales");
            if (me.IsAdmin) { group("Administration"); item("admin", "Studio"); }
            dock.Children.Add(nav);
            side.Child = dock;
        }

        void Go(string p, string a = null)
        {
            page = p; arg = a; query = ""; editing = null;
            suppress = true;
            if (searchBox != null) searchBox.Text = "";
            suppress = false;
            RefreshSide();
            ShowPage();
        }

        void ShowPage(bool keepScroll = false)
        {
            if (viewHost == null || me == null) return;
            UIElement el;
            switch (page)
            {
                case "game": el = PageGame(); break;
                case "lib": el = PageLib(); break;
                case "profile": el = PageProfile(); break;
                case "settings": el = PageSettings(); break;
                case "legal": el = PageLegal(); break;
                case "admin": el = me.IsAdmin ? PageAdmin() : PageStore(); break;
                default: el = PageStore(); break;
            }
            if (searchHost != null) searchHost.Visibility = (page == "store" || page == "lib") ? Visibility.Visible : Visibility.Hidden;
            double y = 0;
            var old = viewHost.Content as ScrollViewer;
            if (keepScroll && old != null) y = old.VerticalOffset;
            var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(28, 8, 28, 40), Content = el };
            viewHost.Content = sv;
            if (y > 0) Dispatcher.BeginInvoke(new Action(() => sv.ScrollToVerticalOffset(y)), DispatcherPriority.Loaded);
        }

        UIElement Avatar(Session s, double size)
        {
            var gr = new Grid { Width = size, Height = size };
            var el = new Shp.Ellipse { Width = size, Height = size, Fill = Ui.T.Accent };
            gr.Children.Add(el);
            var bi = Img.Load(s.AvatarUrl, (int)(size * 2));
            if (bi != null) el.Fill = new ImageBrush(bi) { Stretch = Stretch.UniformToFill };
            else
                gr.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrEmpty(s.Username) ? "?" : s.Username.Substring(0, 1).ToUpperInvariant(),
                    FontSize = size / 2.2, FontWeight = FontWeights.Bold, Foreground = Ui.T.OnAccent,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                });
            return gr;
        }

        static void Open(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }
    }
}
