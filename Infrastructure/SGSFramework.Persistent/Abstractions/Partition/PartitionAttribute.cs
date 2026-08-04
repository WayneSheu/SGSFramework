namespace SGSFramework.Persistent.Abstractions.Partition
{
    /// <summary>
    /// 實體類別使用範例
    /// [Partition(scheme: "psYearly", column: "CreatedAt")] //啟用資料表分割
    /// [MSSQLLedger] // 同時啟用帳本功能
    /// public class SensorLog
    /// {
    ///    public int Id { get; set; }
    ///    public DateTime CreatedAt { get; set; }
    ///    public decimal Value { get; set; }
    ///  }
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class PartitionAttribute : Attribute
    {
        public string Scheme { get; }
        public string Column { get; }

        public PartitionAttribute(string scheme, string column)
        {
            Scheme = scheme;
            Column = column;
        }
    }
}
