using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;

namespace SGSFramework.Persistent.Interceptors
{
    /// <summary>
    /// 不可變更攔截器
    /// </summary>
    public class EditableAttributeInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            UpdateEditableProperties(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            UpdateEditableProperties(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private static void UpdateEditableProperties(DbContext? context)
        {
            if (context == null) return;

            // 找出所有狀態為 Modified 的實體
            var modifiedEntries = context.ChangeTracker
                .Entries()
                .Where(e => e.State == EntityState.Modified);

            foreach (var entry in modifiedEntries)
            {
                // 掃描該實體類型中所有掛有 [Editable(false)] 的屬性
                var nonEditableProperties = entry.Entity.GetType()
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(prop => prop.GetCustomAttribute<EditableAttribute>()?.AllowEdit == false);

                foreach (var prop in nonEditableProperties)
                {
                    var efProperty = entry.Property(prop.Name);
                    if (efProperty != null && efProperty.IsModified)
                    {
                        // 強制恢復為未修改狀態，使其不參與 SQL UPDATE 產生
                        efProperty.IsModified = false;
                    }
                }
            }
        }
    }
}
