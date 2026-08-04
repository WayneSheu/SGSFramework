namespace SGSFramework.Core.Abstractions.Attributes
{
    // 自動索引標記 (簡單範例：標記在屬性上)
    /// <summary>
    ///  唯一索引 (Unique Index)
    ///  [AutoIndex(IsUnique = true)]
    ///  public string ProductCode { get; set; }
    /// 
    /// 包含欄位 (Included Columns)
    /// 例如 經常根據 Email 查詢使用者，且查詢結果總是需要 DisplayName。將 DisplayName 放入 Include，
    /// SQL Server 就不需要回表查詢原始資料列，查詢速度會大幅提升。
    /// [AutoIndex(IncludeProperties = new[] { "DisplayName", "LastLoginUtc" })]
    /// public string Email { get; set; }
    /// public string DisplayName { get; set; }
    /// public DateTime? LastLoginUtc { get; set; }
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class AutoIndexAttribute : Attribute
    {
        public bool IsUnique { get; set; } = false;

        // 用於 SQL Server 的 INCLUDE 語法，優化索引涵蓋查詢 (Covering Index)
        public string[] IncludeProperties { get; set; } = Array.Empty<string>();

        public AutoIndexAttribute() { }

        public AutoIndexAttribute(bool isUnique)
        {
            IsUnique = isUnique;
        }
    }
}
