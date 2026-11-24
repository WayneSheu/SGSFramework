#nullable enable
namespace SGSFramework.Core.Abstractions.Entities.Identities;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// 使用者實驗室歸屬類別（取代原 UserLabMapping.IsPrimary 布林值）。
/// 實驗室歸屬（使用者管理）與實驗室支援（實驗室管理）共用此 Enum。
/// EF 以 int 儲存，請勿調整既有數值。
/// </summary>
public enum LabAssignmentType
{
    /// <summary>主要實驗室（每位使用者啟用中僅能有一個）</summary>
    [Display(Name = "主要")]
    Primary = 0,

    /// <summary>兼任實驗室</summary>
    [Display(Name = "兼任")]
    Secondary = 1,

    /// <summary>支援實驗室（於指定期間內支援，須設定結束日）</summary>
    [Display(Name = "支援")]
    Support = 2
}
