// Path: src/SGSFramework/Infrastructure/SGSFramework.SPAModulePlugin/Application/DTOs/SPAModuleUploadRequest.cs
namespace SGSFramework.SPAModulePlugin.Application.DTOs;

using Microsoft.AspNetCore.Http;
using SGSFramework.SPAModulePlugin.Domain.Enums;
using System.ComponentModel.DataAnnotations;

/// <summary>
/// SPA 模組上傳表單請求 DTO
/// </summary>
public sealed record SPAModuleUploadRequest
{
    /// <summary>
    /// SPA 前端框架類型 (1: Vue3, 2: BlazorWASM)
    /// </summary>
    [Required(ErrorMessage = "必須指定前端框架類型")]
    public SPAFrameworkType FrameworkType { get; init; } = SPAFrameworkType.Vue3;

    /// <summary>
    /// 上傳的 SPA 模組檔案列表 (.zip 套件或單一靜態資源)
    /// </summary>
    [Required(ErrorMessage = "必須上傳至少一個模組檔案")]
    public IReadOnlyList<IFormFile> Files { get; init; } = Array.Empty<IFormFile>();
}