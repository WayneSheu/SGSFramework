using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SGS.Modules.ORG.Application.Abstractions;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Command;

/// <summary>
/// 編輯實驗室/組織名稱與描述指令
/// </summary>
public sealed record EditLaboratoryCommand : IRequest<Result<LaboratoryDto>>
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    [Required(ErrorMessage = "實驗室名稱為必填欄位。")]
    [StringLength(50, ErrorMessage = "實驗室名稱長度不能超過 50 個字元。")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    [StringLength(200, ErrorMessage = "描述說明長度不能超過 200 個字元。")]
    public string? Description { get; init; }

    [JsonPropertyName("location")]
    [StringLength(100, ErrorMessage = "位置長度不能超過 100 個字元。")]
    public string? Location { get; init; }

    public EditLaboratoryCommand(int id, string name, string? description = null, string? location = null)
    {
        Id = id;
        Name = name;
        Description = description;
        Location = location;
    }
}

/// <summary>
/// 編輯實驗室/組織指令處理器
/// </summary>
public class EditLaboratoryCommandHandler : IRequestHandler<EditLaboratoryCommand, Result<LaboratoryDto>>
{
    private readonly ORGDbContext _dbContext;

    public EditLaboratoryCommandHandler(ORGDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<Result<LaboratoryDto>> Handle(EditLaboratoryCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<LaboratoryDto>(
                Error.Validation("ORG_INVALID_NAME", "實驗室名稱不可為空。")
            );
        }

        // 1. 查詢實體 (明確進行 Tracking)
        var entity = await _dbContext.Organizations
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity == null)
        {
            return Result.Failure<LaboratoryDto>(
                Error.NotFound("ORG_LAB_NOT_FOUND", $"找不到 ID 為 {request.Id} 的實驗室。")
            );
        }

        // 2. 執行領域更新 (Domain Logic)
        entity.UpdateDetails(request.Name, request.Description, request.Location);

        // 3. 寫入資料庫
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 4. 回傳最新更新結果 DTO
        var resultDto = new LaboratoryDto
        {
            Id = entity.Id,
            ParentId = entity.ParentId,
            Code = entity.Code,
            Name = entity.Name,
            Location = entity.Location,
            Description = entity.Description,
            NodePath = entity.NodePath,
            Level = entity.Level,
            TenantLabId = entity.TenantLabId,
            EffectiveTenantLabId = entity.TenantLabId
        };

        return Result.Success(resultDto);
    }
}