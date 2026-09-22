using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs.PermissionTree
{
    public class PermissionDto
    {
        /// <summary>
        /// 權限唯一識別鍵 (例如: SYSTEM.PERMISSION.READ)
        /// </summary>
        public string PermissionKey { get; set; } = string.Empty;

        /// <summary>
        /// 權限顯示標題
        /// </summary>
        public string PermissionTitle { get; set; } = string.Empty;

        public string ActionName {  get; set; } = string.Empty;

        public string ActionTitle {  get; set; } = string.Empty;
        /// <summary>
        /// 權限功能描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 位元遮罩索引位置 (Bitmask 運算用)
        /// </summary>
        public int BitPosition { get; set; }
    }
}
