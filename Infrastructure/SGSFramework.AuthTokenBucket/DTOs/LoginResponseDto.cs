using SGSFramework.AuthTokenBucket.Models;
using SGSFramework.Core.Abstractions.Menus;
using SGSFramework.Core.DTOs;

namespace SGSFramework.AuthTokenBucket.DTOs
{
    /// <summary>
    /// 身份驗證登入成功回應 DTO
    /// </summary>
    public class LoginResponseDto
    {
        /// <summary>
        /// 存取憑證與刷新權牌資料
        /// </summary>
        public required object TokenData { get; set; }

        /// <summary>
        /// 使用者選單結構
        /// </summary>
        public IEnumerable<object> Menus { get; set; } = [];

        /// <summary>
        /// 裝置唯一識別碼
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// 確定鎖定之目標實驗室識別碼
        /// </summary>
        public string TenantLabId { get; set; } = string.Empty;

        /// <summary>
        /// 依 Parent 母實驗室分組之可存取實驗室清單
        /// </summary>
        public List<AccessibleLabGroupDto> GroupedLabs { get; set; } = [];

        /// <summary>
        /// 狀態訊息
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
