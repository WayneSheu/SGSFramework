using Microsoft.AspNetCore.Mvc;
using SGSFramework.AuthTokenBucket.Filters;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Attributes
{
    /// <summary>
    /// 此屬性用於指定特定操作或控制器是否需要特定的 SGS 報表類別存取權限。
    /// 透過 DynamicRuleAuthorizationFilter 實現動態規則授權檢查。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class RequireReportCategoryAttribute : TypeFilterAttribute
    {
        /// <summary>
        /// 初始化 <see cref="RequireReportCategoryAttribute"/> 類別的新執行個體。
        /// </summary>
        /// <param name="allowedCategories">允許存取的 SGS 報表類別代碼陣列</param>
        public RequireReportCategoryAttribute(params string[] allowedCategories) : base(typeof(DynamicRuleAuthorizationFilter))
        {
            Arguments = new object[] { allowedCategories ?? Array.Empty<string>() };
        }
    }
}
