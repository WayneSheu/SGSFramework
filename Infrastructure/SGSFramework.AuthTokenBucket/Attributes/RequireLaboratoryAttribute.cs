using Microsoft.AspNetCore.Mvc;
using SGSFramework.AuthTokenBucket.Filters;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Attributes
{

    /// <summary>
    /// 此屬性用於指定特定操作或控制器是否需要實驗室存取權限。
    /// 此屬性可以應用於控制器或方法，以確保只有具有實驗室存取權限的用戶才能執行這些操作。
    /// 這有助於確保只有授權使用者才能執行某些需要實驗室存取權限的操作。
    /// 此屬性使用了DynamicRuleAuthorizationFilter來實現存取權限檢查。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class RequireLaboratoryAttribute : TypeFilterAttribute
    {
        public RequireLaboratoryAttribute(params string[] allowedCategories) : base(typeof(DynamicRuleAuthorizationFilter))
        {
            Arguments = new object[] { allowedCategories ?? Array.Empty<string>() };
        }
    }
}
