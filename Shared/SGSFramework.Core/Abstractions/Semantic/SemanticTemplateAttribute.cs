namespace SGSFramework.Core.Abstractions.Semantic
{
    [AttributeUsage(AttributeTargets.Property)]
    public class SemanticTemplateAttribute : Attribute
    {
        public string Tag { get; }      // 標籤名稱，如「活動類型」
        public string Format { get; }   // 格式化字串，如 "{0:yyyy年MM月dd日}"
        public int Order { get; }       // 語句排列順序，確保生成結構一致

        public SemanticTemplateAttribute(string tag, int order, string format = "{0}")
        {
            Tag = tag;
            Order = order;
            Format = format;
        }
    }
}
