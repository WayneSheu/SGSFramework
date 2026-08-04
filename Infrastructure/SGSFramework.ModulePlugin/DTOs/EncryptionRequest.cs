using FluentValidation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGSFramework.ModulePlugin.DTOs
{
    /// <summary>
    /// 加密請求模型，定義必要的業務屬性
    /// </summary>
    public record EncryptionRequest
    {
        /// <summary>
        /// 加密負載，即需要加密的數據
        /// </summary>
        [Required(ErrorMessage = "Payload is required.")]
        [StringLength(10000, MinimumLength = 1, ErrorMessage = "Payload length must be between 1 and 10000 characters.")]
        public string Payload { get; init; } = string.Empty;

        /// <summary>
        /// 加密策略名稱，僅允許 "AES-256-GCM" 或 "RSA-OAEP"
        /// </summary>
        [Required(ErrorMessage = "Strategy name is required.")]
        [RegularExpression(@"^(AES-256-GCM|RSA-OAEP)$", ErrorMessage = "Invalid encryption strategy.")]
        public string Strategy { get; init; } = string.Empty;

        /// <summary>
        /// 加密金鑰別名，用於指定使用的加密金鑰
        /// </summary>
        [Required(ErrorMessage = "Key alias is required.")]
        public string KeyAlias { get; init; } = "DefaultKey";
    }

    // 獨立的驗證邏輯，保持 DTO 純淨
    public class EncryptionRequestValidator : AbstractValidator<EncryptionRequest>
    {
        public EncryptionRequestValidator()
        {
            RuleFor(x => x.Payload).NotEmpty();
            RuleFor(x => x.Strategy).Must(s => s == "AES-256-GCM" || s == "RSA-OAEP");
            RuleFor(x => x.KeyAlias).NotEmpty().Length(5, 50);
        }
    }
}
