using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.DataProtection.Abstractions
{
    /// <summary>
    /// 表示 HSM 中金鑰的當前生命週期狀態
    /// </summary>
    public enum KeyStatus
    {
        Active,     // 可正常使用，處於金鑰有效期限內
        Expired,    // 超過有效期限，嚴禁進行加密，僅允許特定的解密操作
        Revoked,    // 因安全性事件（如洩漏）被強制撤銷，嚴禁任何操作
        Compromised,// 疑似遭竄改，系統應觸發緊急告警
        Suspended   // 暫時凍結（如維護中）
    }
}
