using QuestPDF.Infrastructure;

namespace SGSFramework.Core.Reports
{

    /// <summary>  
    /// 報表動態欄位
    /// </summary>
    public class ColumnDefine
    {
        /// <summary>
        /// 顯示在 PDF 表頭的文字 (例如："受害人姓名")
        /// </summary>
        public string Title { get; set; }
        /// <summary>
        /// 對應 DTO 中的屬性名稱 (例如："VictimName")，用於反射取值
        /// </summary>
        public string FieldName { get; set; }
        /// <summary>
        /// 欄位比例寬度 (QuestPDF 使用 RelativeColumn)
        /// </summary>
        public float Width { get; set; }
        
        /// <summary>
        /// 文字對齊方式 (預設靠左)
        /// </summary>
        public HorizontalAlignment Alignment { get; set; } = HorizontalAlignment.Left;
        //內容過長時自動縮小字體以維持單行顯示
        public bool AutoShrink { get; set; } = true;

        // --- 建構式設計 ---
        // 1. 最簡建構式：適用於平均分配寬度的欄位
        public ColumnDefine(string title, string fieldName, float width = 1, bool autoShrink = true)
        {
            Title = title;
            FieldName = fieldName;
            Width = width;
            AutoShrink = autoShrink;
        }

        // 2. 完整建構式：支援對齊設定 (例如：金額需靠右)
        public ColumnDefine(string title, string fieldName, float width, bool autoShrink,HorizontalAlignment alignment)
            : this(title, fieldName, width, autoShrink)
        {
            Alignment = alignment;
        }
    }
}
