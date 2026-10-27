using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Identity.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SGSFramework.Identity.Services
{
    /// <summary>
    /// Scope 目錄：自目前已載入的所有 Controller（含動態外掛模組）彙整 [RequiresSystemScope] 宣告，
    /// 供管理頁挑選操作範圍。其他微服務自行宣告的 Scope 不在此目錄中，仍可於管理頁手動輸入。
    /// </summary>
    public sealed class SystemScopeCatalog(IActionDescriptorCollectionProvider actionDescriptorProvider)
    {
        private readonly IActionDescriptorCollectionProvider _actionDescriptorProvider =
            actionDescriptorProvider ?? throw new ArgumentNullException(nameof(actionDescriptorProvider));

        public IReadOnlyList<SystemScopeCatalogItemDto> GetCatalog()
        {
            var entries = new Dictionary<string, (string? Description, SortedSet<string> Endpoints)>(StringComparer.Ordinal);

            foreach (var action in _actionDescriptorProvider.ActionDescriptors.Items)
            {
                var attributes = action.EndpointMetadata.OfType<RequiresSystemScopeAttribute>().Where(a => a.Scope is not null).ToList();
                if (attributes.Count == 0)
                {
                    continue;
                }

                var methods = action.ActionConstraints?.OfType<HttpMethodActionConstraint>().SelectMany(c => c.HttpMethods).ToList();
                var endpoint = $"{(methods is { Count: > 0 } ? string.Join("/", methods) : "ANY")} /{action.AttributeRouteInfo?.Template ?? action.DisplayName}";

                foreach (var attribute in attributes)
                {
                    if (!entries.TryGetValue(attribute.Scope!, out var entry))
                    {
                        entry = (attribute.Description, new SortedSet<string>(StringComparer.Ordinal));
                        entries[attribute.Scope!] = entry;
                    }
                    else if (entry.Description is null && attribute.Description is not null)
                    {
                        entries[attribute.Scope!] = entry = (attribute.Description, entry.Endpoints);
                    }

                    entry.Endpoints.Add(endpoint);
                }
            }

            return entries
                .OrderBy(e => e.Key, StringComparer.Ordinal)
                .Select(e => new SystemScopeCatalogItemDto
                {
                    Scope = e.Key,
                    Description = e.Value.Description,
                    Endpoints = e.Value.Endpoints.ToList()
                })
                .ToList();
        }
    }
}
