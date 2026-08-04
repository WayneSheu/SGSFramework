using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SGSFramework.SystemLog.Options.Models;

namespace SGSFramework.SystemLog.Services
{
    /// <summary>
    /// 
    /// </summary>
    public class LogManagerService
    {
        private readonly IOptionsMonitor<AppSettingsOptions> _options;
        private readonly IConfigurationRoot _config;
        private readonly string _settingsPath = "appsettings.json";

        public LogManagerService(IOptionsMonitor<AppSettingsOptions> options, IConfiguration config)
        {
            _options = options;
            _config = (IConfigurationRoot)config;
        }

        // 獲取當前所有的日誌設定
        public AppSettingsOptions GetCurrentSettings() => _options.CurrentValue;

        // 動態修改最低日誌級別並存回 JSON
        public void UpdateDefaultLevel(string newLevel)
        {
            var json = File.ReadAllText(_settingsPath);
            dynamic jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject(json)!;

            // 修改 JSON 物件中的值
            jsonObj["Serilog"]["MinimumLevel"]["Default"] = newLevel;

            string output = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(_settingsPath, output);

            // 觸發配置重新載入
            _config.Reload();

        }

        //檔案大小與保留期管理：透過 Args 字典，您可以輕鬆讀取 104857600 (100MB)
        //與 14 天這兩個數值，並在管理介面上提供修改功能。
        public WriteToFileArgsInfo DisplayWriteToFileArgs()
        {
            var args = new WriteToFileArgsInfo();
            var options = GetCurrentSettings().Serilog.WriteTo
                .FirstOrDefault(w => w.Name == "File");
            if (options != null)
            {
                // 日誌保留策略
                args.path = options.Args["path"].ToString() ?? string.Empty;
                args.rollingInterval = options.Args["rollingInterval"].ToString() ?? string.Empty;
                args.rollOnFileSizeLimit = Convert.ToBoolean(options.Args["rollOnFileSizeLimit"]);
                args.retainedFileCountLimit = Convert.ToInt32(
                    options.Args["retainedFileCountLimit"]);
                args.fileSizeLimitBytes = Convert.ToInt32(
                   options.Args["fileSizeLimitBytes"]);
                args.formatter = options.Args["formatter"].ToString() ?? string.Empty;
            }

            return args;

        }



    }
}
