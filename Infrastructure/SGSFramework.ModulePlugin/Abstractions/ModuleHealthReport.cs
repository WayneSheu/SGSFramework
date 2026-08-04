using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ModulePlugin.Abstractions
{
    public record ModuleHealthReport(
        string ModuleName,// 模組名稱
        TimeSpan LoadDuration,// 模組載入所需時間
        bool IsDependenciesSatisfied,// 模組相依性是否滿足
        long MemoryUsageBytes// 模組記憶體使用量 (Bytes)
    );
}
