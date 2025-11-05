using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TelegramLauncher.Models;

namespace TelegramLauncher.Services
{
    public static class ClientStore
    {
        private static string ConfigDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TelegramLauncher");
        private static string ConfigPath => Path.Combine(ConfigDir, "clients.json");

        public static List<ClientConfig> Load()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return new List<ClientConfig>();
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<List<ClientConfig>>(json) ?? new List<ClientConfig>();
            }
            catch { return new List<ClientConfig>(); }
        }

        public static void Save(IEnumerable<ClientConfig> clients)
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(clients, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
    }
}
