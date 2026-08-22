using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGS.Modules.ORG.Controllers.Requests
{
    public sealed record EditLaboratoryRequest(
        [Required(ErrorMessage = "實驗室名稱為必填欄位。")]
    [StringLength(50, ErrorMessage = "實驗室名稱長度不能超過 50 個字元。")]
    string Name,

        [StringLength(200, ErrorMessage = "描述說明長度不能超過 200 個字元。")]
    string? Description = null,

        [StringLength(100, ErrorMessage = "位置長度不能超過 100 個字元。")]
    string? Location = null
    );
}
