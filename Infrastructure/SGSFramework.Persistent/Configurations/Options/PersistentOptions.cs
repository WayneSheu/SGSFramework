using System.ComponentModel.DataAnnotations;

namespace SGSFramework.Persistent.Configurations.Options
{
    public class PersistentOptions
    {
        //添加一個參數來避免出現特殊字串。這樣做既不增加成本，又能防止拼字錯誤。
        //單一入口映射，確保在整個應用程序中使用相同的配置節點名稱，減少錯誤風險。
        //在 .NET 的 Options 模式中，只定義最頂層的 SectionName，而子節點（如 Db、MessageBroker）則是透過類別結構自動映射。
        public const string SectionName = "PersistentSettings";
        public DatabaseOptions DatabaseSettings { get; set; } = new();
        public MessageBrokerOptions MessageBroker { get; set; } = new();
        public IdentityOptions Identity { get; set; } = new();

        public JwtOptions Jwt { get; set; } = new();

    }

    /// <summary>
    /// DatabaseSettings類別包含了與資料庫相關的設定選項，
    /// 例如連接字串、敏感資料日誌記錄、命令超時、重試策略等。
    /// 這些設定可以幫助開發人員更靈活地配置和管理資料庫連接，並確保應用程序在面對資料庫問題時能夠更穩定地運行。
    /// </summary>
    public class DatabaseOptions
    {
        [Required(AllowEmptyStrings = false)] // 確保連線字串不是空的
        public string ConnectionString { get; set; } = string.Empty;
        // 啟用敏感資料日誌記錄會在日誌中包含 SQL 查詢參數的實際值，
        // 這對於開發和除錯非常有幫助，但在生產環境中應謹慎使用以避免洩露敏感資訊。
        public bool EnableSensitiveDataLogging { get; set; } = false;
        public int CommandTimeout { get; set; } = 30;
        [Range(1, 100)] // 限制連線池或重試次數在合理範圍
        public int MaxRetryCount { get; set; } = 3;
        public int MaxRetryDelaySeconds { get; set; } = 5;
        public bool UseHierarchyId { get; set; } = false;

    }

    public class MessageBrokerOptions
    {
        public string Provider { get; set; } = "RabbitMQ"; // 決定使用哪種 Broker
        public RabbitMQOptions RabbitMQ { get; set; } = new();
        public KafkaOptions Kafka { get; set; } = new();
    }
    public class RabbitMQOptions
    {
        public string Host { get; set; } = "localhost";
        public string Username { get; set; } = "guest";
        public string Password { get; set; } = "guest";
    }

    public class KafkaOptions
    {
        public string BootstrapServers { get; set; } = "localhost:9092";
        public string GroupId { get; set; } = "default-group";
    }

    public class IdentityOptions
    {
        public bool UseIdentity { get; set; }
        public JwtOptions Jwt { get; set; } = new();
    }

    public class JwtOptions
    {
        public string Key { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public int ExpireDays { get; set; } = 7;
    }
}
