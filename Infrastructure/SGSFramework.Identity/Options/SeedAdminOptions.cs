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

        // 刻意不標 [Required]：機密由環境變數 SeedAdmin__Password 注入，僅首次建立管理員時需要；
        // 未設定時 AdminSeederService 會略過建立帳號 (角色仍照常初始化)，避免正式環境長期保留初始密碼。
        public string Password { get; set; } = string.Empty;

        public string RoleName { get; set; } = "SuperAdmin";

        public bool EnableAutoSeed { get; set; } = true;
    }
}
