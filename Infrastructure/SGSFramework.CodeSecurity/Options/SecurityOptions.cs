using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGSFramework.CodeSecurity.Options
{
    public class CodeSecurityOptions
    { 
        [Required(ErrorMessage = "必須指定驗證提供者 (Provider)")]
        public string Provider { get; set; } = "Windows";

        [Required(ErrorMessage = "必須指定模組發行者 (ModulePublisher)")]
        [MinLength(3, ErrorMessage = "模組發行者名稱過短")]
        public string ModulePublisher { get; set; } = string.Empty; // 階段一：開發團隊

        [Required(ErrorMessage = "必須指定系統發行者 (SystemPublisher)")]
        [MinLength(3, ErrorMessage = "系統發行者名稱過短")]
        public string SystemPublisher { get; set; } = string.Empty; // 階段二：系統封裝

        public string SigningMode { get; set; } = "tpm"; //預設使用 TPM 進行簽名

    }
}
