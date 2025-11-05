using System.Text.Json.Serialization;

namespace TelegramLauncher.Models
{
    public enum ClientStatus { Active, Frozen, Crash }

    public class ClientConfig
    {
        public string Name { get; set; } = "";
        public string ExePath { get; set; } = "";
        public string? Arguments { get; set; }
        public ClientStatus Status { get; set; } = ClientStatus.Active;

        [JsonIgnore]           // галочка только для UI
        public bool IsSelected { get; set; }
    }
}
