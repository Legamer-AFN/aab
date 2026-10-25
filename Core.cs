using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace AbyssXLauncher
{
    public static class JsonOpt
    {
        public static readonly JsonSerializerOptions O = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public static class Paths
    {
        public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AbyssX Launcher");
        public static readonly string Games = Path.Combine(Root, "Games");
    }

    // Sauvegarde sur le PC (réglages, session, bibliothèque) dans %LOCALAPPDATA%\AbyssX Launcher
    public static class Disk
    {
        public static T Load<T>(string file) where T : new()
        {
            try
            {
                string p = Path.Combine(Paths.Root, file);
                if (File.Exists(p))
                {
                    var v = JsonSerializer.Deserialize<T>(File.ReadAllText(p), JsonOpt.O);
                    if (v != null) return v;
                }
            }
            catch { }
            return new T();
        }

        public static void Save(string file, object o)
        {
            try
            {
                Directory.CreateDirectory(Paths.Root);
                File.WriteAllText(Path.Combine(Paths.Root, file), JsonSerializer.Serialize(o, JsonOpt.O));
            }
            catch { }
        }

        public static void Delete(string file)
        {
            try { File.Delete(Path.Combine(Paths.Root, file)); } catch { }
        }

        public static Config LoadConfig()
        {
            try
            {
                string p = Path.Combine(AppContext.BaseDirectory, "launcher.config.json");
                if (File.Exists(p))
                {
                    var c = JsonSerializer.Deserialize<Config>(File.ReadAllText(p), JsonOpt.O);
                    if (c != null) return c;
                }
            }
            catch { }
            return new Config();
        }
    }

    // Téléchargement, installation, lancement et désinstallation des jeux
    public static class Setup
    {
        static readonly HttpClient dl = new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };

        public static async Task<Inst> Install(Game g, Action<int> progress)
        {
            string src = (g.DownloadUrl ?? "").Trim();
            if (src.Length == 0) throw new Exception("Ce jeu n'a pas encore de fichier à installer.");
            string dir = Path.Combine(Paths.Games, g.Id);
            string tmp = null, name;
            try
            {
                if (src.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    var uri = new Uri(src);
                    name = Path.GetFileName(Uri.UnescapeDataString(uri.AbsolutePath));
                    if (string.IsNullOrEmpty(name)) name = "jeu.zip";
                    tmp = Path.Combine(Path.GetTempPath(), "abyssx_" + Guid.NewGuid().ToString("N") + "_" + name);
                    using (var res = await dl.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                    {
                        res.EnsureSuccessStatusCode();
                        long total = res.Content.Headers.ContentLength ?? -1;
                        long done = 0;
                        int last = -1;
                        using (var ins = await res.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var outs = File.Create(tmp))
                        {
                            var buf = new byte[81920];
                            int n;
                            while ((n = await ins.ReadAsync(buf, 0, buf.Length).ConfigureAwait(false)) > 0)
                            {
                                await outs.WriteAsync(buf, 0, n).ConfigureAwait(false);
                                done += n;
                                if (total > 0)
                                {
                                    int p = (int)(done * 100 / total);
                                    if (p != last) { last = p; progress(Math.Min(p, 99)); }
                                }
                            }
                        }
                    }
                    src = tmp;
                }
                else
                {
                    name = Path.GetFileName(src.TrimEnd('\\', '/'));
                }

                string s = src;
                string fname = name;
                return await Task.Run(() =>
                {
                    if (!File.Exists(s) && !Directory.Exists(s)) throw new Exception("Fichier du jeu introuvable : " + s);
                    if (Directory.Exists(dir)) Directory.Delete(dir, true);
                    Directory.CreateDirectory(dir);
                    if (Directory.Exists(s)) CopyDir(s, dir);
                    else if (s.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) ZipFile.ExtractToDirectory(s, dir, true);
                    else File.Copy(s, Path.Combine(dir, fname), true);
                    string exe = FindExe(dir, g.ExePath, fname);
                    progress(100);
                    return new Inst { Dir = dir, Exe = exe, Version = g.Version };
                }).ConfigureAwait(false);
            }
            finally
            {
                if (tmp != null) { try { File.Delete(tmp); } catch { } }
            }
        }

        static void CopyDir(string from, string to)
        {
            foreach (var d in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, d)));
            foreach (var f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
                File.Copy(f, Path.Combine(to, Path.GetRelativePath(from, f)), true);
        }

        static string FindExe(string dir, string hint, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(hint) && File.Exists(Path.Combine(dir, hint))) return hint;
            if (fallback != null && fallback.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(dir, fallback))) return fallback;
            var all = Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileName(f).StartsWith("unins", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f.Count(c => c == Path.DirectorySeparatorChar))
                .ToList();
            if (all.Count == 0) throw new Exception("Aucun fichier .exe trouvé dans ce jeu. Indique le fichier à lancer dans le Studio.");
            return Path.GetRelativePath(dir, all[0]);
        }

        public static Process Launch(Inst i)
        {
            string exe = Path.Combine(i.Dir, i.Exe);
            if (!File.Exists(exe)) throw new Exception("Fichiers du jeu introuvables : réinstalle le jeu.");
            var psi = new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe) };
            return Process.Start(psi);
        }

        public static void Uninstall(Inst i)
        {
            try { if (i != null && Directory.Exists(i.Dir)) Directory.Delete(i.Dir, true); } catch { }
        }
    }

    // Images : chargement (avec cache) et réduction avant envoi
    public static class Img
    {
        static readonly Dictionary<string, BitmapImage> cache = new Dictionary<string, BitmapImage>();

        public static BitmapImage Load(string src, int decodeW = 0)
        {
            if (string.IsNullOrWhiteSpace(src)) return null;
            string key = src + "|" + decodeW;
            BitmapImage bi;
            if (cache.TryGetValue(key, out bi)) return bi;
            try
            {
                bool remote = src.StartsWith("http", StringComparison.OrdinalIgnoreCase);
                bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(src);
                bi.CacheOption = remote ? BitmapCacheOption.Default : BitmapCacheOption.OnLoad;
                if (decodeW > 0) bi.DecodePixelWidth = decodeW;
                bi.EndInit();
                cache[key] = bi;
                return bi;
            }
            catch { return null; }
        }

        public static string Shrink(string file, int maxW)
        {
            var probe = BitmapFrame.Create(new Uri(file), BitmapCreateOptions.None, BitmapCacheOption.Default);
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource = new Uri(file);
            bi.CacheOption = BitmapCacheOption.OnLoad;
            if (probe.PixelWidth > maxW) bi.DecodePixelWidth = maxW;
            bi.EndInit();
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bi));
            string tmp = Path.Combine(Path.GetTempPath(), "abyssx_" + Guid.NewGuid().ToString("N") + ".png");
            using (var fs = File.Create(tmp)) enc.Save(fs);
            return tmp;
        }
    }
}
