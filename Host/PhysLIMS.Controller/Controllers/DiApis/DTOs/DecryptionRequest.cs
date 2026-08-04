using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGSFramework.ApiInfrastructure.Controllers.DiApis.DTOs
{
    public record DecryptionRequest
    {
        [Required]
        public string CipherText { get; init; } = string.Empty;

        [Required]
        public string Strategy { get; init; } = string.Empty;

        [Required]
        public string KeyAlias { get; init; } = string.Empty;
    }
}
