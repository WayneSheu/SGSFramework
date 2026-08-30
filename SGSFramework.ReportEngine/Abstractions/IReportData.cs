using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGSFramework.ReportEngine.Abstractions
{
    /// <summary>
    /// 所有的報表 DTO 都應實作此介面，這讓產生器能以統一的方式處理基本資訊。
    /// </summary>
    public interface IReportData
    {
        // 報表產生器只需要讀取這些資訊來畫 Header
        string ReportTitle { get; }
        string QueryDate { get; }
        string OperatorName { get; }

        string QueryContext { get; }

    }
}
