// Path: src/SGSFramework/Infrastructure/SGSFramework.SPAModulePlugin/Application/DTOs/SPAModuleUploadResponseDto.cs
namespace SGSFramework.SPAModulePlugin.Application.DTOs;

/// <summary>
/// SPA 模組上傳成功回應 DTO
/// </summary>
public sealed record SPAModuleUploadResponseDto
{
    /// <summary>
    /// 模組識別名稱
    /// </summary>
    public string ModuleName { get; init; } = string.Empty;

    /// <summary>
    /// 模組顯示名稱
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 模組版本號
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// 上傳與解壓縮部署目標路徑
    /// </summary>
    public string TargetPath { get; init; } = string.Empty;

    /// <summary>
    /// 處理檔案數量
    /// </summary>
    public int ProcessedFilesCount { get; init; }

    /// <summary>
    /// 上傳部署時間戳記
    /// </summary>
    public DateTime UploadedAt { get; init; } = DateTime.UtcNow;
}