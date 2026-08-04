using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ModulePlugin.Systems.Controller.Providers
{
    /// <summary>
    /// 實作 IActionDescriptorChangeProvider，透過 ChangeToken 控制 MVC Action 描述子刷新
    /// </summary>
    public sealed class DynamicActionDescriptorChangeProvider : IDynamicActionDescriptorChangeProvider
    {
        private CancellationTokenSource _cts = new();

        public static DynamicActionDescriptorChangeProvider Instance { get; } = new();

        public IChangeToken GetChangeToken()
        {
            return new CancellationChangeToken(_cts.Token);
        }

        public void NotifyChanges()
        {
            var oldCts = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
            oldCts.Cancel();
            oldCts.Dispose();
        }
    }
}
