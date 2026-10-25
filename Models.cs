using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AbyssXLauncher
{
    public class Config
    {
        public string SupabaseUrl { get; set; } = "";
        public string SupabaseKey { get; set; } = "";
        public string AdminEmail { get; set; } = "anassjerroudi28100@gmail.com";
        public string ContactEmail { get; set; } = "tasawarafn@gmail.com";
    }

    public class Settings
    {
        public string Theme { get; set; } = "abyss";
        public bool Stars { get; set; } = true;
        public bool Cursor { get; set; } = true;
        public bool Anim { get; set; } = true;
    }

    public class Session
    {
        public string UserId { get; set; }
        public string Email { get; set; }
        public string Username { get; set; }
        public string AvatarUrl { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public bool IsAdmin { get; set; }
    }

    public class Inst
    {
        public string Dir { get; set; }
        public string Exe { get; set; }
        public string Version { get; set; }
        public long Sec { get; set; }
    }

    public class Lib
    {
        public List<string> Owned { get; set; } = new List<string>();
        public Dictionary<string, Inst> Installed { get; set; } = new Dictionary<string, Inst>();
    }

    public class Game
    {
        [JsonPropertyName("id")] public string Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("description")] public string Description { get; set; } = "";
        [JsonPropertyName("genre")] public string Genre { get; set; } = "Autre";
        [JsonPropertyName("version")] public string Version { get; set; } = "1.0.0";
        [JsonPropertyName("price")] public double Price { get; set; }
        [JsonPropertyName("cover_url")] public string CoverUrl { get; set; }
        [JsonPropertyName("download_url")] public string DownloadUrl { get; set; }
        [JsonPropertyName("exe_path")] public string ExePath { get; set; }
        [JsonPropertyName("published")] public bool Published { get; set; }
        [JsonPropertyName("featured")] public bool Featured { get; set; }
        [JsonPropertyName("created_at")] public string CreatedAt { get; set; }

        public Dictionary<string, object> Editable()
        {
            return new Dictionary<string, object>
            {
                ["name"] = Name,
                ["description"] = Description ?? "",
                ["genre"] = Genre ?? "Autre",
                ["version"] = Version ?? "1.0.0",
                ["price"] = Price,
                ["cover_url"] = CoverUrl,
                ["download_url"] = DownloadUrl,
                ["exe_path"] = ExePath,
                ["published"] = Published,
                ["featured"] = Featured
            };
        }
    }
}
