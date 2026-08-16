using SGSFramework.AuthTokenBucket.Models;
using SGSFramework.Core.Abstractions.Menus;
using SGSFramework.Core.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs
{
    /// <summary>
    /// 登入回應 DTO
    /// </summary>
    public class LoginResponseDto
    {
        /// <summary>
        /// 權杖資料（Access Token & Refresh Token）
        /// </summary>
        public TokenResult TokenData { get; set; }

        /// <summary>
        /// 依據使用者權限過濾後的動態選單樹
        /// </summary>
        public IEnumerable<MenuSectionDto> Menus { get; set; }

        /// <summary>
        /// 當前登入的裝置識別碼
        /// </summary>
        public string DeviceId { get; set; }

        /// <summary>
        /// 當前生效的實驗室 ID
        /// </summary>
        public string LabId { get; set; }

        /// <summary>
        /// 使用者有權存取的所有實驗室清單 (供前端渲染切換下拉選單)
        /// </summary>
        public IEnumerable<AccessibleLabDto> AccessibleLabs { get; init; } = Array.Empty<AccessibleLabDto>();

        /// <summary>
        /// 提示訊息
        /// </summary>
        public string Message { get; set; }
    }
}
