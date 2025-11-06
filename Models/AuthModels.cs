using System.Collections.Generic;
using System.Text.Json.Serialization;


namespace TelegramLauncher.Models
{
    public class AuthUser
    {
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = "User"; // Admin/User
        public string PasswordHash { get; set; } = string.Empty;


        [JsonIgnore]
        public bool IsAdmin => string.Equals(Role, "Admin", System.StringComparison.OrdinalIgnoreCase);
    }


    public class UserDatabase
    {
        public string Salt { get; set; } = "tllaunchsalt";
        public List<AuthUser> Users { get; set; } = new();
    }
}