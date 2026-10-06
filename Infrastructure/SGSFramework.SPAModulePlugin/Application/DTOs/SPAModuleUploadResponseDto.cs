// Path: src/SGSFramework/Infrastructure/SGSFramework.SPAModulePlugin/Application/DTOs/SPAModuleUploadResponseDto.cs
namespace SGSFramework.SPAModulePlugin.Application.DTOs;

using SGSFramework.SPAModulePlugin.Domain.Enums;

/// <summary>
/// SPA 模組上傳與部署回應 DTO
/// </summary>
public sealed record SPAModuleUploadResponseDto
{
    /// <summary>
    /// 模組識別名稱
    /// </summary>
    public string ModuleName { get; init; } = string.Empty;

    /// <summary>
    /// 前端框架類型
    /// </summary>
    public SPAFrameworkType FrameworkType { get; init; }

    /// <summary>
    /// 實體部署相對路徑
    /// </summary>
    public string DeployedRelativePath { get; init; } = string.Empty;

    /// <summary>
    /// 成功解壓縮與部署的檔案總數
    /// </summary>
    public int ProcessedFilesCount { get; init; }

    /// <summary>
    /// 部署完成時間
    /// </summary>
    public DateTime UploadedAt { get; init; } = DateTime.UtcNow;
}