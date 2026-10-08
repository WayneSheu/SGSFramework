using System;
using System.Collections.Generic;
using System.Text;

namespace LogMcpServer
{
    public class LogMcpOptions
    {
        public const string SectionName = "LogMcpServer";

        /// <summary>
        /// 日誌檔案存放的實體路徑
        /// </summary>
        public string LogDirectory { get; set; } = "C:\\Logs";

        /// <summary>
        /// 預設搜尋的最大筆數
        /// </summary>
        public int DefaultLimit { get; set; } = 50;
    }
}
