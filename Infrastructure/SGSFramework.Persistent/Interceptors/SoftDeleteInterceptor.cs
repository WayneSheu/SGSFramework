using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SGSFramework.Core.Abstractions.Entities.SoftDelet;
using SGSFramework.Core.HttpAuditProviders;

namespace SGSFramework.Persistent.Interceptors
{
    public class SoftDeleteInterceptor : SaveChangesInterceptor
    {
        private readonly IAuditProvider _currentUserService;

        public SoftDeleteInterceptor(IAuditProvider currentUserService)
        {
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            ApplySoftDelete(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ApplySoftDelete(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void ApplySoftDelete(DbContext? context)
        {
            if (context == null) return;

            var entries = context.ChangeTracker
                .Entries<ISoftDeletable>()
                .Where(e => e.State == EntityState.Deleted);

            foreach (var entry in entries)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedOnUtc= DateTimeOffset.UtcNow;
                entry.Entity.DeletedBy = _currentUserService.UserId ?? "System";
            }
        }
    }
}
