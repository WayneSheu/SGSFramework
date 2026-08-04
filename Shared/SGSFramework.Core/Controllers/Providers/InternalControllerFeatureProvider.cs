using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using System.Reflection;

namespace SGSFramework.Core.Controllers.Providers
{
    /// <summary>
    /// 自訂的 ControllerFeatureProvider，用於識別內部控制器類型
    /// </summary>
    public class InternalControllerFeatureProvider : ControllerFeatureProvider
    {
        protected override bool IsController(TypeInfo typeInfo)
        {
            if (!typeInfo.IsClass)
            {
                return false;
            }
            if (typeInfo.IsAbstract)
            {
                return false;
            }
            if (typeInfo.ContainsGenericParameters)
            {
                return false;
            }
            if (typeInfo.IsDefined(typeof(NonControllerAttribute)))
            {
                return false;
            }
            return typeInfo.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) ||
                    typeInfo.IsDefined(typeof(ControllerAttribute));
        }
    }
}
