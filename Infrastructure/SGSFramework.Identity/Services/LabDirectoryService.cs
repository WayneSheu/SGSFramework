#nullable enable

namespace SGSFramework.Identity.Services
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using SGSFramework.Core.Abstractions.Entities.Identities;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>區域實驗室成員（UserLabMapping）</summary>
    public sealed record LabMemberDto(
        string UserId,
        string UserName,
        string FullName,
        string? EmployeeNo,
        string? Email,
        bool IsPrimary,
        string? JobTitle,
        DateTimeOffset EffectiveDate,
        DateTimeOffset? ExpiryDate,
        bool IsActive,
        bool IsManager);

    /// <summary>區域實驗室名錄：主管與成員</summary>
    public sealed record LabDirectoryDto(
        Guid TenantLabId,
        string? ManagerUserId,
        string? ManagerName,
        string? ManagerEmail,
        DateTimeOffset? ManagerUpdatedAt,
        string? ManagerUpdatedBy,
        IReadOnlyList<LabMemberDto> Members);

    public interface ILabDirectoryService
    {
        Task<LabDirectoryDto> GetAsync(Guid tenantLabId, CancellationToken cancellationToken = default);

        /// <summary>設定或清除（userId 為 null）區域實驗室主管</summary>
        Task<(bool Succeeded, string Message)> SetManagerAsync(Guid tenantLabId, Guid? userId, CancellationToken cancellationToken = default);
    }

    public sealed class LabDirectoryService(
        DbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        ILogger<LabDirectoryService> logger) : ILabDirectoryService
    {
        public async Task<LabDirectoryDto> GetAsync(Guid tenantLabId, CancellationToken cancellationToken = default)
        {
            var manager = await dbContext.Set<LabManager>().AsNoTracking()
                .FirstOrDefaultAsync(m => m.TenantLabId == tenantLabId, cancellationToken).ConfigureAwait(false);

            var managerUser = manager is null
                ? null
                : await dbContext.Set<ApplicationUser>().AsNoTracking()
                    .Where(u => u.Id == manager.ManagerUserId)
                    .Select(u => new { u.Id, u.FullName, u.UserName, u.Email })
                    .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            var members = await (
                    from m in dbContext.Set<UserLabMapping>().AsNoTracking()
                    join u in dbContext.Set<ApplicationUser>().AsNoTracking() on m.UserId equals u.Id
                    where m.TenantLabId == tenantLabId && !u.IsDeleted
                    select new
                    {
                        u.Id, u.UserName, u.FullName, u.EmployeeNo, u.Email,
                        m.IsPrimary, m.JobTitle, m.EffectiveDate, m.ExpiryDate, m.IsActive
                    })
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            var now = DateTimeOffset.UtcNow;
            var memberDtos = members
                .Select(x => new LabMemberDto(
                    x.Id.ToString(), x.UserName ?? string.Empty, x.FullName, x.EmployeeNo, x.Email,
                    x.IsPrimary, x.JobTitle, x.EffectiveDate, x.ExpiryDate,
                    x.IsActive && (!x.ExpiryDate.HasValue || x.ExpiryDate.Value >= now),
                    manager is not null && x.Id == manager.ManagerUserId))
                .OrderByDescending(x => x.IsManager)
                .ThenByDescending(x => x.IsActive)
                .ThenByDescending(x => x.IsPrimary)
                .ThenBy(x => x.FullName, StringComparer.CurrentCulture)
                .ToList();

            return new LabDirectoryDto(
                tenantLabId,
                managerUser?.Id.ToString(),
                managerUser is null ? null : (string.IsNullOrWhiteSpace(managerUser.FullName) ? managerUser.UserName : managerUser.FullName),
                managerUser?.Email,
                manager?.UpdatedAt,
                manager?.UpdatedByName,
                memberDtos);
        }

        public async Task<(bool Succeeded, string Message)> SetManagerAsync(Guid tenantLabId, Guid? userId, CancellationToken cancellationToken = default)
        {
            if (tenantLabId == Guid.Empty) return (false, "實驗室識別碼無效。");

            var set = dbContext.Set<LabManager>();
            var existing = await set.FirstOrDefaultAsync(m => m.TenantLabId == tenantLabId, cancellationToken).ConfigureAwait(false);
            var actorName = httpContextAccessor.HttpContext?.User?.Identity?.Name
                            ?? httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value
                            ?? "未知使用者";

            if (userId is null)
            {
                if (existing is null) return (true, "此實驗室尚未設定主管。");
                set.Remove(existing);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                logger.LogInformation("[LabDirectory] {Actor} 清除實驗室 {LabId} 主管。", actorName, tenantLabId);
                return (true, "已清除實驗室主管。");
            }

            var user = await dbContext.Set<ApplicationUser>().AsNoTracking()
                .Where(u => u.Id == userId.Value && !u.IsDeleted)
                .Select(u => new { u.Id, u.FullName, u.UserName })
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (user is null) return (false, "找不到指定的使用者。");

            // 主管須為此實驗室的有效成員，避免指派與實驗室無關的人員
            var now = DateTimeOffset.UtcNow;
            bool isMember = await dbContext.Set<UserLabMapping>().AsNoTracking()
                .AnyAsync(m => m.TenantLabId == tenantLabId && m.UserId == userId.Value && m.IsActive &&
                               (!m.ExpiryDate.HasValue || m.ExpiryDate.Value >= now), cancellationToken).ConfigureAwait(false);
            if (!isMember) return (false, "主管必須是此實驗室的有效成員，請先於使用者管理指派其歸屬實驗室。");

            if (existing is null)
            {
                existing = new LabManager { TenantLabId = tenantLabId };
                set.Add(existing);
            }

            existing.ManagerUserId = userId.Value;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.UpdatedByName = actorName;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var name = string.IsNullOrWhiteSpace(user.FullName) ? user.UserName : user.FullName;
            logger.LogInformation("[LabDirectory] {Actor} 設定實驗室 {LabId} 主管為 {User}。", actorName, tenantLabId, name);
            return (true, $"已設定主管為「{name}」。");
        }
    }
}
