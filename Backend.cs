using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AbyssXLauncher
{
    public interface IBackend
    {
        string Mode { get; }
        Task<Session> SignUp(string email, string username, string password);
        Task<Session> SignIn(string email, string password);
        Task<Session> Refresh(Session s);
        Task ResetPassword(string email);
        Task<Session> UpdateProfile(Session s, string username, string avatarUrl);
        Task<List<Game>> ListGames(Session s);
        Task<Game> SaveGame(Session s, Game g);
        Task DeleteGame(Session s, string id);
        Task<string> UploadImage(Session s, string bucket, string path, string file);
    }

    // ---------------------------------------------------------------------------------
    // Mode EN LIGNE : comptes, catalogue et images hébergés sur Supabase (gratuit).
    // La sécurité (seul l'administrateur publie) est appliquée côté serveur par supabase/setup.sql
    // ---------------------------------------------------------------------------------
    public class SupaBackend : IBackend
    {
        readonly string url, key, admin;
        static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        public string Mode { get { return "en ligne"; } }

        public SupaBackend(Config c)
        {
            url = c.SupabaseUrl.Trim().TrimEnd('/');
            key = c.SupabaseKey.Trim();
            admin = (c.AdminEmail ?? "").Trim().ToLowerInvariant();
        }

        HttpRequestMessage Req(HttpMethod m, string path, string token)
        {
            var r = new HttpRequestMessage(m, url + path);
            r.Headers.TryAddWithoutValidation("apikey", key);
            r.Headers.TryAddWithoutValidation("Authorization", "Bearer " + (token ?? key));
            return r;
        }

        async Task<JsonElement> Send(HttpRequestMessage req)
        {
            HttpResponseMessage res;
            try { res = await http.SendAsync(req); }
            catch (Exception) { throw new Exception("Connexion impossible. Vérifie ta connexion Internet."); }
            string text = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode) throw new Exception(Fr(text));
            if (string.IsNullOrWhiteSpace(text)) return default(JsonElement);
            using (var d = JsonDocument.Parse(text)) return d.RootElement.Clone();
        }

        Task<JsonElement> Json(HttpMethod m, string path, object body, string token, string prefer = null)
        {
            var r = Req(m, path, token);
            if (prefer != null) r.Headers.TryAddWithoutValidation("Prefer", prefer);
            if (body != null) r.Content = new StringContent(JsonSerializer.Serialize(body, JsonOpt.O), Encoding.UTF8, "application/json");
            return Send(r);
        }

        static string Fr(string text)
        {
            string m = text;
            try
            {
                using (var d = JsonDocument.Parse(text))
                {
                    foreach (var k in new[] { "msg", "error_description", "message", "error" })
                    {
                        JsonElement v;
                        if (d.RootElement.ValueKind == JsonValueKind.Object && d.RootElement.TryGetProperty(k, out v) && v.ValueKind == JsonValueKind.String) { m = v.GetString(); break; }
                    }
                }
            }
            catch { }
            string l = (m ?? "").ToLowerInvariant();
            if (l.Contains("invalid login")) return "E-mail ou mot de passe incorrect.";
            if (l.Contains("not confirmed")) return "Confirme d'abord ton adresse e-mail : ouvre le message reçu.";
            if (l.Contains("already registered")) return "Cette adresse e-mail est déjà utilisée.";
            if (l.Contains("password should")) return "Mot de passe trop faible : 8 caractères minimum, avec des lettres et des chiffres.";
            if (l.Contains("rate limit")) return "Trop de demandes. Réessaie dans quelques minutes.";
            if (l.Contains("row-level security") || l.Contains("permission denied")) return "Action réservée à l'administrateur.";
            return m;
        }

        static string Str(JsonElement e, string k)
        {
            JsonElement v;
            if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out v) && v.ValueKind == JsonValueKind.String) return v.GetString();
            return null;
        }

        void Fill(Session s, JsonElement u)
        {
            s.UserId = Str(u, "id");
            s.Email = Str(u, "email");
            s.Username = null;
            s.AvatarUrl = null;
            JsonElement md;
            if (u.ValueKind == JsonValueKind.Object && u.TryGetProperty("user_metadata", out md) && md.ValueKind == JsonValueKind.Object)
            {
                s.Username = Str(md, "username");
                s.AvatarUrl = Str(md, "avatar_url");
            }
            if (string.IsNullOrEmpty(s.Username)) s.Username = (s.Email ?? "joueur").Split('@')[0];
            s.IsAdmin = admin.Length > 0 && string.Equals(s.Email, admin, StringComparison.OrdinalIgnoreCase);
        }

        Session ToSession(JsonElement r)
        {
            var s = new Session { AccessToken = Str(r, "access_token"), RefreshToken = Str(r, "refresh_token") };
            JsonElement u;
            if (!r.TryGetProperty("user", out u)) throw new Exception("Réponse inattendue du serveur.");
            Fill(s, u);
            return s;
        }

        public async Task<Session> SignUp(string email, string username, string password)
        {
            var r = await Json(HttpMethod.Post, "/auth/v1/signup", new { email = email, password = password, data = new { username = username } }, null);
            JsonElement t;
            if (r.ValueKind == JsonValueKind.Object && r.TryGetProperty("access_token", out t)) return ToSession(r);
            return null; // compte créé : confirmation par e-mail nécessaire
        }

        public async Task<Session> SignIn(string email, string password)
        {
            return ToSession(await Json(HttpMethod.Post, "/auth/v1/token?grant_type=password", new { email = email, password = password }, null));
        }

        public async Task<Session> Refresh(Session s)
        {
            return ToSession(await Json(HttpMethod.Post, "/auth/v1/token?grant_type=refresh_token", new { refresh_token = s.RefreshToken }, null));
        }

        public async Task ResetPassword(string email)
        {
            await Json(HttpMethod.Post, "/auth/v1/recover", new { email = email }, null);
        }

        public async Task<Session> UpdateProfile(Session s, string username, string avatarUrl)
        {
            var r = await Json(HttpMethod.Put, "/auth/v1/user", new { data = new { username = username, avatar_url = avatarUrl } }, s.AccessToken);
            Fill(s, r);
            return s;
        }

        public async Task<List<Game>> ListGames(Session s)
        {
            var r = await Json(HttpMethod.Get, "/rest/v1/games?select=*&order=created_at.desc", null, s.AccessToken);
            if (r.ValueKind != JsonValueKind.Array) return new List<Game>();
            return JsonSerializer.Deserialize<List<Game>>(r.GetRawText(), JsonOpt.O);
        }

        public async Task<Game> SaveGame(Session s, Game g)
        {
            JsonElement r;
            if (string.IsNullOrEmpty(g.Id))
                r = await Json(HttpMethod.Post, "/rest/v1/games", g.Editable(), s.AccessToken, "return=representation");
            else
                r = await Json(new HttpMethod("PATCH"), "/rest/v1/games?id=eq." + Uri.EscapeDataString(g.Id), g.Editable(), s.AccessToken, "return=representation");
            var list = r.ValueKind == JsonValueKind.Array ? JsonSerializer.Deserialize<List<Game>>(r.GetRawText(), JsonOpt.O) : null;
            if (list == null || list.Count == 0) throw new Exception("Enregistrement refusé : droits administrateur requis.");
            return list[0];
        }

        public async Task DeleteGame(Session s, string id)
        {
            await Json(HttpMethod.Delete, "/rest/v1/games?id=eq." + Uri.EscapeDataString(id), null, s.AccessToken);
        }

        public async Task<string> UploadImage(Session s, string bucket, string path, string file)
        {
            var r = Req(HttpMethod.Post, "/storage/v1/object/" + bucket + "/" + path, s.AccessToken);
            r.Headers.TryAddWithoutValidation("x-upsert", "true");
            var c = new ByteArrayContent(File.ReadAllBytes(file));
            c.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            r.Content = c;
            await Send(r);
            return url + "/storage/v1/object/public/" + bucket + "/" + path + "?v=" + DateTime.UtcNow.Ticks;
        }
    }

    // ---------------------------------------------------------------------------------
    // Mode LOCAL : tout reste sur ce PC (pratique pour tester sans serveur).
    // ---------------------------------------------------------------------------------
    public class LocalUser
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string Username { get; set; }
        public string Salt { get; set; }
        public string Hash { get; set; }
        public string Avatar { get; set; }
    }

    public class LocalBackend : IBackend
    {
        readonly string admin;
        readonly List<LocalUser> users;
        readonly List<Game> games;

        public string Mode { get { return "local"; } }

        public LocalBackend(Config c)
        {
            admin = (c.AdminEmail ?? "").Trim().ToLowerInvariant();
            users = Disk.Load<List<LocalUser>>("local_users.json");
            games = Disk.Load<List<Game>>("local_games.json");
        }

        static string Hash(string pw, string salt)
        {
            return Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(pw), Convert.FromBase64String(salt), 150000, HashAlgorithmName.SHA256, 32));
        }

        Session Mk(LocalUser u)
        {
            return new Session
            {
                UserId = u.Id, Email = u.Email, Username = u.Username, AvatarUrl = u.Avatar,
                AccessToken = "local", RefreshToken = u.Id,
                IsAdmin = admin.Length > 0 && string.Equals(u.Email, admin, StringComparison.OrdinalIgnoreCase)
            };
        }

        public Task<Session> SignUp(string email, string username, string password)
        {
            email = email.Trim().ToLowerInvariant();
            if (users.Any(x => x.Email == email)) throw new Exception("Cette adresse e-mail est déjà utilisée.");
            string salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            var u = new LocalUser { Id = Guid.NewGuid().ToString("N"), Email = email, Username = username, Salt = salt, Hash = Hash(password, salt) };
            users.Add(u);
            Disk.Save("local_users.json", users);
            return Task.FromResult(Mk(u));
        }

        public Task<Session> SignIn(string email, string password)
        {
            email = email.Trim().ToLowerInvariant();
            var u = users.FirstOrDefault(x => x.Email == email);
            bool ok = u != null && CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(u.Hash), Convert.FromBase64String(Hash(password, u.Salt)));
            if (!ok) throw new Exception("E-mail ou mot de passe incorrect.");
            return Task.FromResult(Mk(u));
        }

        public Task<Session> Refresh(Session s)
        {
            var u = users.FirstOrDefault(x => x.Id == s.RefreshToken);
            if (u == null) throw new Exception("Session expirée.");
            return Task.FromResult(Mk(u));
        }

        public Task ResetPassword(string email)
        {
            throw new Exception("La réinitialisation par e-mail n'existe qu'en mode en ligne.");
        }

        public Task<Session> UpdateProfile(Session s, string username, string avatarUrl)
        {
            var u = users.FirstOrDefault(x => x.Id == s.UserId);
            if (u != null)
            {
                u.Username = username;
                u.Avatar = avatarUrl;
                Disk.Save("local_users.json", users);
                return Task.FromResult(Mk(u));
            }
            return Task.FromResult(s);
        }

        public Task<List<Game>> ListGames(Session s)
        {
            return Task.FromResult(games.Where(g => s.IsAdmin || g.Published).OrderByDescending(g => g.CreatedAt).ToList());
        }

        public Task<Game> SaveGame(Session s, Game g)
        {
            if (!s.IsAdmin) throw new Exception("Action réservée à l'administrateur.");
            if (string.IsNullOrEmpty(g.Id))
            {
                g.Id = Guid.NewGuid().ToString("N");
                g.CreatedAt = DateTime.UtcNow.ToString("o");
                games.Add(g);
            }
            else
            {
                int i = games.FindIndex(x => x.Id == g.Id);
                if (i >= 0) { g.CreatedAt = games[i].CreatedAt; games[i] = g; } else games.Add(g);
            }
            Disk.Save("local_games.json", games);
            return Task.FromResult(g);
        }

        public Task DeleteGame(Session s, string id)
        {
            if (!s.IsAdmin) throw new Exception("Action réservée à l'administrateur.");
            games.RemoveAll(x => x.Id == id);
            Disk.Save("local_games.json", games);
            return Task.CompletedTask;
        }

        public Task<string> UploadImage(Session s, string bucket, string path, string file)
        {
            string dir = Path.Combine(Paths.Root, "img");
            Directory.CreateDirectory(dir);
            string dest = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".png");
            File.Copy(file, dest, true);
            return Task.FromResult(dest);
        }
    }
}
