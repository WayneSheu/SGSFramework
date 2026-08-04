namespace SGSFramework.Core.Abstractions.Semantic
{
    /// <summary>
    /// 標註在實體類別上，定義該類別的語義上下文標題
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class SemanticEntityAttribute : Attribute
    {
        public string DefaultContextTitle { get; }
        public SemanticEntityAttribute(string title) => DefaultContextTitle = title;
    }
}
