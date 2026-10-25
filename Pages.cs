using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Shp = System.Windows.Shapes;

namespace AbyssXLauncher
{
    public partial class MainWindow
    {
        static readonly string[] Genres = { "Action", "Aventure", "RPG", "Simulation", "Stratégie", "Course", "Sport", "Puzzle", "Horreur", "Battle Royale", "Autre" };
        static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

        Game editing;
        string editCover;
        TextBox eName, eDesc, eVer, ePrice, eUrl, eExe;
        CheckBox ePub, eFeat;

        // ------------------------------------------------------------------ petits outils
        static void Put(Grid g, UIElement e, int col)
        {
            Grid.SetColumn(e, col);
            g.Children.Add(e);
        }

        UIElement Head(string title, string sub = null)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 10, 0, 16) };
            sp.Children.Add(Ui.Txt(title, 28, true));
            if (sub != null) sp.Children.Add(Ui.Txt(sub, 13, false, Ui.T.Muted));
            return sp;
        }

        UIElement Empty(string title, string sub, UIElement action = null)
        {
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 70, 0, 0) };
            var t = Ui.Txt(title, 20, true); t.HorizontalAlignment = HorizontalAlignment.Center;
            var s = Ui.Txt(sub, 13, false, Ui.T.Muted, true); s.HorizontalAlignment = HorizontalAlignment.Center; s.TextAlignment = TextAlignment.Center; s.MaxWidth = 420;
            sp.Children.Add(t);
            sp.Children.Add(s);
            if (action != null)
            {
                var fe = action as FrameworkElement;
                if (fe != null) { fe.HorizontalAlignment = HorizontalAlignment.Center; fe.Margin = new Thickness(0, 16, 0, 0); }
                sp.Children.Add(action);
            }
            return sp;
        }

        bool Match(Game g)
        {
            if (query.Length == 0) return true;
            string hay = (g.Name ?? "") + " " + (g.Description ?? "") + " " + (g.Genre ?? "");
            return hay.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static string Price(Game g)
        {
            return g.Price <= 0 ? "Gratuit" : g.Price.ToString("0.00", Fr) + " €";
        }

        static string Dur(long s)
        {
            if (s < 60) return "moins d'1 min";
            if (s < 3600) return (s / 60) + " min";
            return (s / 3600.0).ToString("0.0", Fr) + " h";
        }

        static string Trunc(string s, int n)
        {
            s = s ?? "";
            return s.Length <= n ? s : s.Substring(0, n).TrimEnd() + "…";
        }

        static Game Copy(Game g)
        {
            return JsonSerializer.Deserialize<Game>(JsonSerializer.Serialize(g, JsonOpt.O), JsonOpt.O);
        }

        Grid CoverImg(Game g, double w, double h, double r)
        {
            var gr = new Grid { Width = w, Height = h, Background = Ui.T.Panel2, Clip = new RectangleGeometry(new Rect(0, 0, w, h), r, r) };
            var bi = Img.Load(g.CoverUrl, (int)(w * 2));
            if (bi != null) gr.Children.Add(new Image { Source = bi, Stretch = Stretch.UniformToFill });
            else
                gr.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrEmpty(g.Name) ? "?" : g.Name.Substring(0, 1).ToUpperInvariant(),
                    FontSize = h / 3, FontWeight = FontWeights.Bold, Foreground = Ui.T.Muted,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                });
            return gr;
        }

        // ------------------------------------------------------------------ actions sur un jeu
        UIElement Actions(Game g)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            bool owned = lib.Owned.Contains(g.Id), inst = lib.Installed.ContainsKey(g.Id);
            int p;
            if (prog.TryGetValue(g.Id, out p))
            {
                sp.Children.Add(Ui.Txt("Installation : " + p + " %", 13, true, Ui.T.Accent));
                sp.Children.Add(new ProgressBar { Height = 6, Minimum = 0, Maximum = 100, Value = p, Margin = new Thickness(0, 6, 0, 0) });
            }
            else if (!owned) sp.Children.Add(Ui.Btn(g.Price > 0 && !me.IsAdmin ? "Acheter  " + Price(g) : "Obtenir", () => Obtain(g)));
            else if (!inst) sp.Children.Add(Ui.Btn("Installer", () => InstallGame(g)));
            else
            {
                bool run = running.Contains(g.Id);
                var b = Ui.Btn(run ? "En cours…" : "Jouer", () => Play(g));
                b.IsEnabled = !run;
                sp.Children.Add(b);
            }
            return sp;
        }

        void Obtain(Game g)
        {
            if (g.Price > 0 && !me.IsAdmin)
            {
                Open("mailto:" + cfg.ContactEmail + "?subject=" + Uri.EscapeDataString("Achat du jeu " + g.Name));
                Toast("Le paiement en ligne n'est pas encore branché : un e-mail s'ouvre pour finaliser l'achat.");
                return;
            }
            if (!lib.Owned.Contains(g.Id)) lib.Owned.Add(g.Id);
            SaveLib();
            Toast(g.Name + " est dans ta bibliothèque.");
            ShowPage(true);
        }

        async void InstallGame(Game g)
        {
            if (prog.ContainsKey(g.Id)) return;
            prog[g.Id] = 0;
            ShowPage(true);
            int shown = -10;
            try
            {
                var inst = await Setup.Install(g, p => Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (p - shown >= 5 || p >= 100) { shown = p; prog[g.Id] = p; ShowPage(true); }
                })));
                lib.Installed[g.Id] = inst;
                SaveLib();
                Toast(g.Name + " est installé.");
            }
            catch (Exception ex) { Toast("Installation impossible : " + ex.Message, true); }
            prog.Remove(g.Id);
            ShowPage(true);
        }

        void Play(Game g)
        {
            Inst i;
            if (!lib.Installed.TryGetValue(g.Id, out i)) return;
            try
            {
                var proc = Setup.Launch(i);
                if (proc == null) { Toast("Jeu lancé."); return; }
                running.Add(g.Id);
                ShowPage(true);
                DateTime t0 = DateTime.UtcNow;
                proc.EnableRaisingEvents = true;
                proc.Exited += (s, e) => Dispatcher.BeginInvoke(new Action(() =>
                {
                    running.Remove(g.Id);
                    i.Sec += (long)(DateTime.UtcNow - t0).TotalSeconds;
                    SaveLib();
                    ShowPage(true);
                }));
            }
            catch (Exception ex) { Toast(ex.Message, true); }
        }

        void Uninstall(Game g)
        {
            if (MessageBox.Show(this, "Désinstaller « " + g.Name + " » ? Il restera dans ta bibliothèque.", "AbyssX Launcher", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            Inst i;
            if (lib.Installed.TryGetValue(g.Id, out i)) { Setup.Uninstall(i); lib.Installed.Remove(g.Id); SaveLib(); }
            ShowPage(true);
        }

        // ------------------------------------------------------------------ boutique
        UIElement Card(Game g, bool inLib)
        {
            bool owned = lib.Owned.Contains(g.Id);
            var sp = new StackPanel { Width = 190 };
            sp.Children.Add(CoverImg(g, 190, 250, 12));
            var body = new StackPanel { Margin = new Thickness(2, 8, 2, 0) };
            body.Children.Add(Ui.Txt(g.Name, 14, true));
            var meta = new StackPanel { Orientation = Orientation.Horizontal };
            meta.Children.Add(Ui.Txt(g.Genre ?? "", 12, false, Ui.T.Muted));
            string right = !g.Published ? "Brouillon" : owned ? "Dans ta bibliothèque" : Price(g);
            meta.Children.Add(Ui.Txt("    " + right, 12, g.Price > 0 && !owned && g.Published, g.Price > 0 && !owned && g.Published ? Ui.T.Accent : Ui.T.Muted));
            body.Children.Add(meta);
            if (inLib) body.Children.Add(Actions(g));
            sp.Children.Add(body);
            var card = new Border { Child = sp, Margin = new Thickness(0, 0, 20, 22), Background = Brushes.Transparent };
            card.MouseEnter += (s, e) => card.Opacity = 0.85;
            card.MouseLeave += (s, e) => card.Opacity = 1;
            card.MouseLeftButtonUp += (s, e) => Go("game", g.Id);
            return card;
        }

        UIElement Hero(Game g)
        {
            var gr = new Grid();
            gr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 24, 0) };
            var tag = Ui.Tag(g.Featured ? "À la une" : "Nouveauté");
            tag.HorizontalAlignment = HorizontalAlignment.Left;
            tag.Margin = new Thickness(0, 0, 0, 10);
            left.Children.Add(tag);
            left.Children.Add(Ui.Txt(g.Name, 34, true));
            var d = Ui.Txt(Trunc(g.Description, 170), 14, false, Ui.T.Muted, true);
            d.Margin = new Thickness(0, 8, 0, 6);
            left.Children.Add(d);
            left.Children.Add(Ui.Txt(Price(g), 16, true, Ui.T.Accent));
            var b = Ui.Btn("Voir le jeu", () => Go("game", g.Id));
            b.Margin = new Thickness(0, 14, 0, 0);
            left.Children.Add(b);
            Put(gr, left, 0);
            Put(gr, CoverImg(g, 190, 250, 14), 1);
            var c = Ui.Card(gr, 26);
            c.Margin = new Thickness(0, 6, 0, 0);
            return c;
        }

        UIElement PageStore()
        {
            var sp = new StackPanel();
            var visible = games.Where(g => g.Published || me.IsAdmin).ToList();
            var feat = visible.FirstOrDefault(g => g.Published && g.Featured) ?? visible.FirstOrDefault(g => g.Published);
            if (feat != null && query.Length == 0) sp.Children.Add(Hero(feat));
            else sp.Children.Add(Head("Boutique", "Tous les jeux AbyssX."));

            var chips = new WrapPanel { Margin = new Thickness(0, 18, 0, 16) };
            chips.Children.Add(Ui.Chip("Tous", genre == "", () => { genre = ""; ShowPage(true); }));
            foreach (var gn in visible.Select(g => g.Genre ?? "Autre").Distinct().OrderBy(q => q))
            {
                string x = gn;
                chips.Children.Add(Ui.Chip(x, genre == x, () => { genre = genre == x ? "" : x; ShowPage(true); }));
            }
            sp.Children.Add(chips);

            var list = visible.Where(g => Match(g) && (genre == "" || g.Genre == genre)).ToList();
            if (list.Count == 0)
            {
                if (games.Count == 0 && me.IsAdmin)
                    sp.Children.Add(Empty("Ton catalogue est vide", "Ouvre le Studio pour publier ton premier jeu.", Ui.Btn("Ouvrir le Studio", () => Go("admin"))));
                else if (games.Count == 0)
                    sp.Children.Add(Empty("Les jeux arrivent bientôt", "Les jeux publiés par l'administrateur apparaîtront ici."));
                else
                    sp.Children.Add(Empty("Aucun résultat", "Essaie un autre mot-clé ou un autre genre."));
            }
            else
            {
                var wp = new WrapPanel();
                foreach (var g in list) wp.Children.Add(Card(g, false));
                sp.Children.Add(wp);
            }
            return sp;
        }

        UIElement PageGame()
        {
            var g = games.FirstOrDefault(x => x.Id == arg);
            if (g == null) return Empty("Jeu introuvable", "Ce jeu n'est plus disponible.", Ui.Btn("Retour à la boutique", () => Go("store")));
            var sp = new StackPanel();
            var back = Ui.Btn("Retour", () => Go("store"), false, 13);
            back.Margin = new Thickness(0, 10, 0, 0);
            sp.Children.Add(back);

            var row = new Grid { Margin = new Thickness(0, 18, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Put(row, CoverImg(g, 260, 347, 16), 0);

            var info = new StackPanel { Margin = new Thickness(30, 0, 0, 0), MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Left };
            info.Children.Add(Ui.Txt(g.Name, 32, true, null, true));
            var tags = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 14) };
            tags.Children.Add(Ui.Tag(g.Genre ?? "Autre"));
            tags.Children.Add(Ui.Tag("Version " + g.Version));
            if (!g.Published) tags.Children.Add(Ui.Tag("Brouillon"));
            info.Children.Add(tags);
            info.Children.Add(Ui.Txt(Price(g), 22, true, Ui.T.Accent));
            info.Children.Add(Actions(g));
            var desc = Ui.Txt(string.IsNullOrWhiteSpace(g.Description) ? "Aucune description pour le moment." : g.Description, 14, false, Ui.T.Muted, true);
            desc.Margin = new Thickness(0, 18, 0, 0);
            info.Children.Add(desc);

            Inst i;
            if (lib.Installed.TryGetValue(g.Id, out i))
            {
                var t = Ui.Txt("Temps de jeu : " + Dur(i.Sec), 13, false, Ui.T.Muted);
                t.Margin = new Thickness(0, 18, 0, 4);
                info.Children.Add(t);
                info.Children.Add(Ui.Link("Désinstaller", () => Uninstall(g)));
            }
            Put(row, info, 1);
            sp.Children.Add(row);
            return sp;
        }

        UIElement PageLib()
        {
            var sp = new StackPanel();
            sp.Children.Add(Head("Bibliothèque", "Tes jeux, prêts à être installés et lancés."));
            var mine = games.Where(g => lib.Owned.Contains(g.Id) && Match(g)).ToList();
            if (mine.Count == 0)
            {
                sp.Children.Add(lib.Owned.Count == 0
                    ? Empty("Ta bibliothèque est vide", "Obtiens des jeux depuis la boutique pour les retrouver ici.", Ui.Btn("Ouvrir la boutique", () => Go("store")))
                    : Empty("Aucun résultat", "Essaie un autre mot-clé."));
                return sp;
            }
            var wp = new WrapPanel();
            foreach (var g in mine) wp.Children.Add(Card(g, true));
            sp.Children.Add(wp);
            return sp;
        }

        // ------------------------------------------------------------------ profil
        UIElement PageProfile()
        {
            var sp = new StackPanel { MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Left };
            sp.Children.Add(Head("Mon profil"));
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
            row.Children.Add(Avatar(me, 86));
            var info = new StackPanel { Margin = new Thickness(18, 10, 0, 0) };
            info.Children.Add(Ui.Txt(me.Username, 22, true));
            info.Children.Add(Ui.Txt(me.Email, 13, false, Ui.T.Muted));
            info.Children.Add(Ui.Txt(me.IsAdmin ? "Administrateur" : "Joueur", 12, true, Ui.T.Accent));
            row.Children.Add(info);
            sp.Children.Add(row);
            var photo = Ui.Btn("Importer une photo depuis mon PC", ChangeAvatar, false);
            photo.Margin = new Thickness(0, 0, 0, 22);
            sp.Children.Add(photo);

            var name = Ui.Input(me.Username);
            sp.Children.Add(Ui.Field("Identifiant affiché", name));
            var save = Ui.Btn("Enregistrer", async () =>
            {
                string n = name.Text.Trim();
                if (n.Length < 3 || n.Length > 20) { Toast("L'identifiant doit faire 3 à 20 caractères.", true); return; }
                try
                {
                    me = await Retry(() => be.UpdateProfile(me, n, me.AvatarUrl));
                    SaveSession();
                    RefreshSide();
                    ShowPage(true);
                    Toast("Profil enregistré.");
                }
                catch (Exception ex) { Toast(ex.Message, true); }
            });
            save.Margin = new Thickness(0, 0, 0, 26);
            sp.Children.Add(save);

            sp.Children.Add(Ui.Txt("Sécurité et données", 16, true));
            var line = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            var reset = Ui.Btn("Changer mon mot de passe par e-mail", async () =>
            {
                try { await be.ResetPassword(me.Email); Toast("E-mail envoyé : suis le lien reçu."); }
                catch (Exception ex) { Toast(ex.Message, true); }
            }, false, 13);
            reset.Margin = new Thickness(0, 0, 10, 10);
            line.Children.Add(reset);
            var del = Ui.Btn("Supprimer mon compte", () =>
            {
                Open("mailto:" + cfg.ContactEmail + "?subject=" + Uri.EscapeDataString("Suppression de mon compte AbyssX Launcher") + "&body=" + Uri.EscapeDataString("Adresse du compte : " + me.Email));
                Toast("Un e-mail s'ouvre : envoie-le pour demander la suppression de ton compte.");
            }, false, 13);
            line.Children.Add(del);
            sp.Children.Add(line);
            var out2 = Ui.Btn("Se déconnecter", Logout, false, 13);
            out2.Margin = new Thickness(0, 14, 0, 0);
            sp.Children.Add(out2);
            return sp;
        }

        async void ChangeAvatar()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif", Title = "Choisir une photo de profil" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                string tmp = Img.Shrink(dlg.FileName, 256);
                string url = await Retry(() => be.UploadImage(me, "avatars", me.UserId + "/avatar.png", tmp));
                me = await Retry(() => be.UpdateProfile(me, me.Username, url));
                SaveSession();
                RefreshSide();
                ShowPage(true);
                Toast("Photo de profil mise à jour.");
            }
            catch (Exception ex) { Toast(ex.Message, true); }
        }

        // ------------------------------------------------------------------ réglages
        UIElement PageSettings()
        {
            var sp = new StackPanel { MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };
            sp.Children.Add(Head("Paramètres", "Choisis ton ambiance. Tout est enregistré automatiquement."));
            sp.Children.Add(Ui.Txt("Ambiance", 16, true));
            var wrap = new WrapPanel { Margin = new Thickness(0, 10, 0, 14) };
            for (int i = 0; i < Theme.Ids.Length; i++)
            {
                string id = Theme.Ids[i], nm = Theme.Names[i];
                var tile = new StackPanel();
                tile.Children.Add(new Border { Width = 200, Height = 96, CornerRadius = new CornerRadius(10), Background = Theme.Preview(id) });
                var label = Ui.Txt(nm, 14, true);
                label.Margin = new Thickness(2, 8, 0, 0);
                tile.Children.Add(label);
                var b = new Border
                {
                    Child = tile, Padding = new Thickness(8), Margin = new Thickness(0, 0, 14, 14), CornerRadius = new CornerRadius(14),
                    Background = Brushes.Transparent, BorderThickness = new Thickness(2), BorderBrush = st.Theme == id ? Ui.T.Accent : Ui.T.Line
                };
                b.MouseLeftButtonUp += (s, e) => { st.Theme = id; Disk.Save("settings.json", st); ApplyTheme(); ShowShell(); };
                wrap.Children.Add(b);
            }
            sp.Children.Add(wrap);

            sp.Children.Add(Ui.Txt("Effets", 16, true));
            var stars = Ui.Check("Petites étoiles scintillantes", st.Stars);
            stars.Click += (s, e) => { st.Stars = stars.IsChecked == true; Disk.Save("settings.json", st); MakeStars(); };
            var cur = Ui.Check("Curseur stylé (étoile lumineuse ; Ctrl+Maj+C pour l'activer ou le couper)", st.Cursor);
            cur.Click += (s, e) => { st.Cursor = cur.IsChecked == true; Disk.Save("settings.json", st); ApplyCursor(); };
            var anim = Ui.Check("Animations (scintillement, couleurs qui évoluent)", st.Anim);
            anim.Click += (s, e) => { st.Anim = anim.IsChecked == true; Disk.Save("settings.json", st); ApplyTheme(); ShowShell(); };
            stars.Margin = new Thickness(0, 10, 0, 4);
            sp.Children.Add(stars);
            sp.Children.Add(cur);
            sp.Children.Add(anim);

            var about = new StackPanel { Margin = new Thickness(0, 22, 0, 0) };
            about.Children.Add(Ui.Txt("À propos", 16, true));
            about.Children.Add(Ui.Txt("AbyssX Launcher 1.0.0, mode " + be.Mode + ".", 13, false, Ui.T.Muted));
            about.Children.Add(Ui.Txt("Contact : " + cfg.ContactEmail, 13, false, Ui.T.Muted));
            about.Children.Add(Ui.Txt("Tes réglages et ta bibliothèque sont enregistrés dans %LOCALAPPDATA%\\AbyssX Launcher.", 13, false, Ui.T.Muted, true));
            sp.Children.Add(about);
            return sp;
        }

        UIElement PageLegal()
        {
            var sp = new StackPanel();
            sp.Children.Add(Head("Informations légales", "Les documents qui s'appliquent au Launcher et à ton compte."));
            sp.Children.Add(LegalView(() => ShowPage()));
            return sp;
        }

        // ------------------------------------------------------------------ Studio (administrateur)
        UIElement PageAdmin()
        {
            return editing != null ? AdminEditor() : AdminList();
        }

        UIElement AdminList()
        {
            var sp = new StackPanel();
            var head = new DockPanel { Margin = new Thickness(0, 10, 0, 18) };
            var add = Ui.Btn("Publier un nouveau jeu", () => { editing = new Game { Genre = "Autre" }; editCover = null; ShowPage(); });
            add.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(add, Dock.Right);
            head.Children.Add(add);
            var t = new StackPanel();
            t.Children.Add(Ui.Txt("Studio", 28, true));
            t.Children.Add(Ui.Txt("Seul l'administrateur peut publier au catalogue officiel.", 13, false, Ui.T.Muted));
            head.Children.Add(t);
            sp.Children.Add(head);

            if (games.Count == 0)
            {
                sp.Children.Add(Empty("Aucun jeu pour le moment", "Publie ton premier jeu : nom, description, couverture et fichier."));
                return sp;
            }
            foreach (var g in games)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Put(row, CoverImg(g, 48, 64, 8), 0);
                var n = Ui.Txt(g.Name, 15, true); n.VerticalAlignment = VerticalAlignment.Center;
                Put(row, n, 1);
                var pr = Ui.Txt(Price(g), 13, false, Ui.T.Muted); pr.VerticalAlignment = VerticalAlignment.Center;
                Put(row, pr, 2);
                var stt = Ui.Txt(g.Published ? "Publié" : "Brouillon", 13, true, g.Published ? Ui.T.Accent : Ui.T.Muted); stt.VerticalAlignment = VerticalAlignment.Center;
                Put(row, stt, 3);
                var acts = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                var bPub = Ui.Btn(g.Published ? "Dépublier" : "Publier", () => TogglePublish(g), false, 13);
                var bEdit = Ui.Btn("Modifier", () => { editing = Copy(g); editCover = null; ShowPage(); }, false, 13);
                var bDel = Ui.Btn("Supprimer", () => RemoveGame(g), false, 13);
                bPub.Margin = new Thickness(0, 0, 8, 0); bEdit.Margin = new Thickness(0, 0, 8, 0);
                acts.Children.Add(bPub); acts.Children.Add(bEdit); acts.Children.Add(bDel);
                Put(row, acts, 4);
                var card = Ui.Card(row, 10);
                card.Margin = new Thickness(0, 0, 0, 10);
                sp.Children.Add(card);
            }
            return sp;
        }

        async void TogglePublish(Game g)
        {
            try
            {
                var c = Copy(g);
                c.Published = !c.Published;
                await Retry(() => be.SaveGame(me, c));
                await LoadGames();
                Toast(c.Published ? "Jeu publié dans le catalogue." : "Jeu retiré du catalogue.");
            }
            catch (Exception ex) { Toast(ex.Message, true); }
        }

        async void RemoveGame(Game g)
        {
            if (MessageBox.Show(this, "Supprimer « " + g.Name + " » du catalogue ?", "AbyssX Launcher", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try
            {
                await Retry(async () => { await be.DeleteGame(me, g.Id); return 0; });
                await LoadGames();
                Toast("Jeu supprimé.");
            }
            catch (Exception ex) { Toast(ex.Message, true); }
        }

        void SyncEditor()
        {
            if (editing == null || eName == null) return;
            editing.Name = eName.Text.Trim();
            editing.Description = eDesc.Text;
            editing.Version = string.IsNullOrWhiteSpace(eVer.Text) ? "1.0.0" : eVer.Text.Trim();
            double p;
            editing.Price = double.TryParse(ePrice.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out p) && p >= 0 ? Math.Round(p, 2) : 0;
            editing.DownloadUrl = string.IsNullOrWhiteSpace(eUrl.Text) ? null : eUrl.Text.Trim();
            editing.ExePath = string.IsNullOrWhiteSpace(eExe.Text) ? null : eExe.Text.Trim();
            editing.Published = ePub.IsChecked == true;
            editing.Featured = eFeat.IsChecked == true;
        }

        UIElement AdminEditor()
        {
            var g = editing;
            var sp = new StackPanel { MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left };
            sp.Children.Add(Head(string.IsNullOrEmpty(g.Id) ? "Nouveau jeu" : "Modifier le jeu", "Cette fiche s'affiche dans la boutique."));

            eName = Ui.Input(g.Name);
            eDesc = Ui.Input(g.Description, true);
            eVer = Ui.Input(g.Version);
            ePrice = Ui.Input(g.Price.ToString("0.##", CultureInfo.InvariantCulture));
            eUrl = Ui.Input(g.DownloadUrl);
            eExe = Ui.Input(g.ExePath);
            ePub = Ui.Check("Publié : visible dans la boutique pour tous les comptes", g.Published);
            eFeat = Ui.Check("À la une : affiché en haut de la boutique", g.Featured);

            sp.Children.Add(Ui.Field("Nom du jeu", eName));
            sp.Children.Add(Ui.Field("Description", eDesc));

            sp.Children.Add(Ui.Txt("Genre", 12, false, Ui.T.Muted));
            var chips = new WrapPanel { Margin = new Thickness(0, 6, 0, 8) };
            foreach (var gn in Genres)
            {
                string x = gn;
                chips.Children.Add(Ui.Chip(x, g.Genre == x, () => { SyncEditor(); editing.Genre = x; ShowPage(true); }));
            }
            sp.Children.Add(chips);

            var two = new Grid();
            two.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            two.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            two.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Put(two, Ui.Field("Version", eVer), 0);
            Put(two, Ui.Field("Prix en euros (0 = gratuit)", ePrice), 2);
            sp.Children.Add(two);

            sp.Children.Add(Ui.Field("Lien de téléchargement (https) ou fichier de ce PC", eUrl));
            var browse = Ui.Btn("Choisir un fichier .exe ou .zip sur ce PC", () =>
            {
                var d = new Microsoft.Win32.OpenFileDialog { Filter = "Jeu (.exe, .zip)|*.exe;*.zip|Tous les fichiers|*.*", Title = "Choisir le fichier du jeu" };
                if (d.ShowDialog() == true) { SyncEditor(); editing.DownloadUrl = d.FileName; ShowPage(true); }
            }, false, 13);
            browse.Margin = new Thickness(0, -4, 0, 14);
            sp.Children.Add(browse);
            sp.Children.Add(Ui.Field("Fichier à lancer dans l'archive (facultatif, ex. MonJeu.exe)", eExe));

            sp.Children.Add(Ui.Txt("Couverture", 12, false, Ui.T.Muted));
            var cv = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 16) };
            cv.Children.Add(CoverImg(new Game { Name = g.Name, CoverUrl = editCover ?? g.CoverUrl }, 120, 160, 10));
            var pick = Ui.Btn("Importer une image depuis mon PC", () =>
            {
                var d = new Microsoft.Win32.OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif", Title = "Choisir la couverture" };
                if (d.ShowDialog() == true) { SyncEditor(); editCover = d.FileName; ShowPage(true); }
            }, false, 13);
            pick.VerticalAlignment = VerticalAlignment.Center;
            pick.Margin = new Thickness(18, 0, 0, 0);
            cv.Children.Add(pick);
            sp.Children.Add(cv);

            sp.Children.Add(ePub);
            sp.Children.Add(eFeat);

            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 18, 0, 0) };
            var save = Ui.Btn("Enregistrer le jeu", () => SaveEditor());
            save.Margin = new Thickness(0, 0, 10, 0);
            bar.Children.Add(save);
            bar.Children.Add(Ui.Btn("Annuler", () => { editing = null; editCover = null; ShowPage(); }, false));
            sp.Children.Add(bar);
            return sp;
        }

        async void SaveEditor()
        {
            try
            {
                SyncEditor();
                var g = editing;
                if (string.IsNullOrWhiteSpace(g.Name)) throw new Exception("Le nom du jeu est obligatoire.");
                if (editCover != null)
                {
                    string tmp = Img.Shrink(editCover, 900);
                    g.CoverUrl = await Retry(() => be.UploadImage(me, "covers", Guid.NewGuid().ToString("N") + ".png", tmp));
                }
                await Retry(() => be.SaveGame(me, g));
                editing = null;
                editCover = null;
                await LoadGames();
                Toast("Jeu enregistré.");
            }
            catch (Exception ex) { Toast(ex.Message, true); }
        }
    }
}
