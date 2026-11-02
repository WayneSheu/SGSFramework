using Microsoft.AspNetCore.Mvc;
using SGSFramework.AuthTokenBucket.Filters;
using SGSFramework.AuthTokenBucket.RuleEngine.Abstractions;
using SGSFramework.Core.Abstractions.Entities.ReportCategories;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Attributes
{
    /// <summary>
    /// 此屬性用於指定特定操作或控制器是否需要特定的 SGS 報表類別存取權限。
    /// 透過 DynamicRuleAuthorizationFilter 執行 ReportCategoryRule：目前實驗室須承作任一指定類別，
    /// 且使用者具該類別之指定動作授權（報告類別主檔 + 類別授權）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class RequireReportCategoryAttribute : TypeFilterAttribute
    {
        /// <summary>
        /// 初始化 <see cref="RequireReportCategoryAttribute"/> 類別的新執行個體（檢核瀏覽授權）。
        /// </summary>
        /// <param name="allowedCategories">允許存取的 SGS 報表類別代碼陣列</param>
        public RequireReportCategoryAttribute(params string[] allowedCategories)
            : this(ReportActions.View, allowedCategories)
        {
        }

        /// <summary>
        /// 初始化 <see cref="RequireReportCategoryAttribute"/> 類別的新執行個體，並指定所需動作。
        /// </summary>
        /// <param name="requiredAction">所需的報告類別動作（單一動作）</param>
        /// <param name="allowedCategories">允許存取的 SGS 報表類別代碼陣列</param>
        public RequireReportCategoryAttribute(ReportActions requiredAction, params string[] allowedCategories)
            : base(typeof(DynamicRuleAuthorizationFilter))
        {
            Arguments = new object[] { allowedCategories ?? Array.Empty<string>(), LabAuthRuleKind.ReportCategory, requiredAction };
        }
    }
}
