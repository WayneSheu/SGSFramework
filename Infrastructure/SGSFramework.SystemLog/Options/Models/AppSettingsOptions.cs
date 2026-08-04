namespace SGSFramework.SystemLog.Options.Models
{
    public class AppSettingsOptions
    {
        public ConnectionStringsOptions ConnectionStrings { get; set; } = new();
        public SerilogOptions Serilog { get; set; } = new();
    }

    public class ConnectionStringsOptions
    {
        public string DefaultConnection { get; set; } = string.Empty;
    }

    public class SerilogOptions
    {
        public List<string> Using { get; set; } = new();
        public MinimumLevelOptions MinimumLevel { get; set; } = new();
        public List<WriteToOptions> WriteTo { get; set; } = new();
    }


    public class MinimumLevelOptions
    {
        public string Default { get; set; } = "Information";
        public Dictionary<string, string> Override { get; set; } = new();
    }

    // 這個類別表示 Serilog 的寫入器配置，每個寫入器都有一個名稱和一組參數。
    public class WriteToOptions
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, object> Args { get; set; } = new();
    }

    // 這裡的屬性名稱與 Serilog File Sink 的參數對應，方便在管理介面上顯示和修改。
    public class WriteToFileArgsInfo
    {
        public string path { get; set; } = string.Empty;// 日誌文件的路徑
        public string rollingInterval { get; set; } = "Day";// 日誌文件的滾動間隔，這裡預設為 "Day"，表示每天生成一個新的日誌文件。
        public bool rollOnFileSizeLimit { get; set; } = true;// 當日誌文件達到指定大小限制時，是否滾動生成新的日誌文件。預設為 true，表示啟用此功能。
        public int fileSizeLimitBytes { get; set; } = 104857600;// 日誌文件的大小限制，單位為字節。預設為 104857600 字節，即 100MB。
        public int retainedFileCountLimit { get; set; } = 7;// 保留的日誌文件數量限制，當超過此數量時，最舊的日誌文件將被刪除。預設為 7，表示保留最近 7 個日誌文件。
        public string formatter { get; set; } = string.Empty;// 日誌文件的格式化器配置，這裡預設為空字符串，表示使用 Serilog 的默認格式化器。
    }
}


