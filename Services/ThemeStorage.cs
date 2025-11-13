using System;
using System.IO;
using System.Text.Json;

namespace TelegramLauncher.Services
{
    public static class ThemeStorage
    {
        private static readonly string FilePath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "TelegramLauncher", "theme.json");

        public static (string Base, string Accent)? Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var json = File.ReadAllText(FilePath);
                var dto = JsonSerializer.Deserialize<ThemeDto>(json);
                if (dto == null || string.IsNullOrWhiteSpace(dto.Base) || string.IsNullOrWhiteSpace(dto.Accent))
                    return null;
                return (dto.Base, dto.Accent);
            }
            catch { return null; }
        }

        public static void Save(string @base, string accent)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var json = JsonSerializer.Serialize(new ThemeDto { Base = @base, Accent = accent });
                File.WriteAllText(FilePath, json);
            }
            catch { /* ignore */ }
        }

        private class ThemeDto { public string Base { get; set; } = "Dark"; public string Accent { get; set; } = "Blue"; }
    }
}
