using MediatR;
using Microsoft.EntityFrameworkCore;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGS.Modules.ORG.Infrastructure.Entities.Org;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Queries
{
    /// <summary>
    /// 依據 UserId 取得該使用者所有直接指派之 UserLabMapping 關聯清單 Query
    /// </summary>
    public sealed record GetUserLabMappingsByUserIdQuery(Guid UserId) : IRequest<Result<List<UserLabMappingDto>>>;

    /// <summary>
    /// GetUserLabMappingsByUserIdQuery 處理常式
    /// 依循 Clean Architecture 規範，透過 DbContext / IQueryable 進行高效率 Read-Only 查詢與 DTO 投影
    /// </summary>
    public sealed class GetUserLabMappingsByUserIdQueryHandler
        : IRequestHandler<GetUserLabMappingsByUserIdQuery, Result<List<UserLabMappingDto>>>
    {
        private readonly DbContext _context;

        public GetUserLabMappingsByUserIdQueryHandler(DbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<Result<List<UserLabMappingDto>>> Handle(
            GetUserLabMappingsByUserIdQuery request,
            CancellationToken cancellationToken)
        {
            if (request.UserId == Guid.Empty)
            {
                return Result.Failure<List<UserLabMappingDto>>(
                    Error.Validation("USERLAB_INVALID_USERID", "使用者識別碼不能為 Empty Guid。"));
            }

            try
            {
                // 進行 Read-Only 投影查詢 ( Join 實驗室主檔取得 LabName 與 LabCode )
                var result = await (
                    from mapping in _context.Set<UserLabMapping>().AsNoTracking()
                    where mapping.UserId == request.UserId
                    join lab in _context.Set<Organization>().AsNoTracking()
                        on mapping.LabId equals lab.Id into labGroup
                    from lab in labGroup.DefaultIfEmpty()
                    orderby mapping.IsPrimary descending, mapping.CreatedAtUtc descending
                    select new UserLabMappingDto
                    {
                        UserId = mapping.UserId,
                        LabId = mapping.LabId,
                        TenantLabId = mapping.TenantLabId,
                        LabName = lab != null ? lab.Name : string.Empty,
                        LabCode = lab != null ? lab.Code : string.Empty,
                        IsPrimary = mapping.IsPrimary,
                        JobTitle = mapping.JobTitle,
                        EffectiveDate = mapping.EffectiveDate,
                        ExpiryDate = mapping.ExpiryDate,
                        IsActive = mapping.IsActive,
                        CreatedAtUtc = mapping.CreatedAtUtc,
                        CreatedBy = mapping.CreatedBy,
                        UpdatedAtUtc = mapping.UpdatedAtUtc,
                        UpdatedBy = mapping.UpdatedBy
                    }
                ).ToListAsync(cancellationToken);

                return Result.Success(result);
            }
            catch (Exception ex)
            {
                return Result.Failure<List<UserLabMappingDto>>(
                    Error.Failure("USERLAB_QUERY_ERROR", $"查詢使用者實驗室關聯時發生未預期錯誤：{ex.Message}"));
            }
        }
    }
}
