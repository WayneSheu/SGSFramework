using System;
using System.Collections.Generic;
using System.Text;

namespace Enterprise.HttpSdk.Dtos
{
    /// <summary>
    /// Represents a device session DTO.
    /// </summary>
    public sealed class DeviceSessionDto
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public DateTime LastActiveAt { get; set; }
        public bool IsCurrentDevice { get; set; }
    }

    public sealed class DeviceInfo
    {
        /// <summary>
        /// Gets or sets the user agent of the device.
        /// 取得或設定設備的用戶代理程式
        /// </summary>
        public string UserAgent { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the platform of the device.
        /// 取得或設定設備的平台
        /// </summary>
        public string Platform { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the language of the device.
        /// 取得或設定設備的語言
        /// </summary>
        public string Language { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the screen dimensions of the device.
        /// 取得或設定設備的畫面尺寸
        /// </summary>
        public int ScreenWidth { get; set; }
        /// <summary>
        /// Gets or sets the screen height of the device.
        /// 取得或設定設備的畫面高度
        /// </summary>
        public int ScreenHeight { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether the device is mobile.
        /// 取得或設定一個值，表示設備是否為移動裝置。
        /// </summary>
        public bool IsMobile { get; set; }



    }
}
