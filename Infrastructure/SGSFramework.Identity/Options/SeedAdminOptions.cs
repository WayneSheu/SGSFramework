using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGSFramework.Identity.Options
{
    public sealed class SeedAdminOptions
    {
        public const string SectionName = "SeedAdmin";

        [Required]
        public string Username { get; set; } = "admin";

        [Required]
        [EmailAddress]
        public string Email { get; set; } = "admin@sgs.com";

        [Required]
        public string Password { get; set; } = string.Empty;

        public string RoleName { get; set; } = "SuperAdmin";

        public bool EnableAutoSeed { get; set; } = true;
    }
}
