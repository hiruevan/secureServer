using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SecureServerCommand
{
    public class User
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("first_name")]
        public string FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string LastName { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("phone")]
        public string Phone {  get; set; }

        [JsonPropertyName("preferred_contact_method")]
        public string PreferredContact { get; set; }

        [JsonPropertyName("admin")]
        public bool AppAdmin { get; set; }

        [JsonPropertyName("dev_admin")]
        public bool DevAdmin { get; set; }

        [JsonPropertyName("frozen")]
        public bool Disabled { get; set; }

        [JsonPropertyName("vault_len")]
        public int VaultSize { get; set; }

        [JsonPropertyName("failed_attempts")]
        public int FailedAttempts { get; set; }

        [JsonPropertyName("2fa_enabled")]
        public bool _TwoFAEnabledRaw { set { twoFAEnabledRaw = value; } }
        private bool twoFAEnabledRaw;

        [JsonPropertyName("root_auth")]
        public bool _RootAuthRaw { set { rootAuthRaw = value; } }
        private bool rootAuthRaw;

        public bool RootAuth { get {return rootAuthRaw; } }


        public bool IsAdmin => AppAdmin || DevAdmin;
        public bool TwoFAEnabled => twoFAEnabledRaw || rootAuthRaw;
        
        public string Name => $"{FirstName} {LastName}";
    }
}
