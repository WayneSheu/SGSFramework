using Microsoft.AspNetCore.Mvc.Infrastructure;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ModulePlugin.Systems.Controller.Providers
{
    /// <summary>
    /// 提供動態 Controller 路由變更通知之服務介面
    /// </summary>
    public interface IDynamicActionDescriptorChangeProvider : IActionDescriptorChangeProvider
    {
        /// <summary>
        /// 觸發 ActionDescriptors 重新編譯與刷新
        /// </summary>
        void NotifyChanges();
    }
}
