using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SGSFramework.ReportEngine.Abstractions;
using System.Reflection;

namespace SGSFramework.ReportEngine.Generators
{
    /// <summary>
    /// 通用表格報表產生器實作
    /// </summary>
    /// <typeparam name="TData">報證明細 DTO 型別</typeparam>
    public class TableReportGenerator<TReport, TData> : ReportGeneratorBase<TReport>
        where TReport : IReportData, ITableReportData<TData>
        where TData : class
    {
        private readonly List<ColumnDefine> _columns;
        private readonly IEnumerable<TData> _dataSource;

        public TableReportGenerator(List<ColumnDefine> columns, IEnumerable<TData> dataSource)
        {
            _columns = columns ?? throw new ArgumentNullException(nameof(columns));
            _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        }

        protected override void ConfigurePage(PageDescriptor page)
        {
            base.ConfigurePage(page);
            // 預設橫向 A4，適合多欄位報表
            page.Size(PageSizes.A4.Landscape());
            page.Margin(1, Unit.Centimetre);
        }

        protected override void ComposeContent(IContainer container)
        {
            container.Table(table =>
            {
                // 1. 定義欄位比例
                table.ColumnsDefinition(columns =>
                {
                    foreach (var col in _columns)
                    {
                        columns.RelativeColumn(col.Width);
                    }
                });

                // 2. 繪製表頭 (Header)
                table.Header(header =>
                {
                    foreach (var col in _columns)
                    {
                        header.Cell()
                            .Background(Color_Grid_Grey)
                            .Padding(4)
                            .Element(c => ApplyAlignment(c, col.Alignment))
                            .Text(col.Title)
                            .FontColor(Color_Text_White)
                            .SemiBold();
                    }
                });

                // 3. 繪製資料列 (Rows)
                var propertyInfos = typeof(TData).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (var item in _dataSource)
                {
                    foreach (var col in _columns)
                    {
                        // 透過反射取得屬性值
                        var prop = propertyInfos.FirstOrDefault(p => p.Name.Equals(col.FieldName, StringComparison.OrdinalIgnoreCase));
                        var rawValue = prop?.GetValue(item);
                        var displayText = rawValue?.ToString() ?? string.Empty;

                        table.Cell()
                            .BorderBottom(0.5f)
                            .BorderColor(Color_Grid_Grey)
                            .Padding(4)
                            .Element(c => ApplyAlignment(c, col.Alignment))
                            .Element(cellContainer =>
                            {
                                var textBlock = cellContainer.Text(displayText);
                                if (col.AutoShrink)
                                {
                                    textBlock.FontSize(7); // 內容過長自動縮小字體基準
                                }
                            });
                    }
                }
            });
        }

        private static IContainer ApplyAlignment(IContainer container, HorizontalAlignment alignment)
        {
            return alignment switch
            {
                HorizontalAlignment.Center => container.AlignCenter(),
                HorizontalAlignment.Right => container.AlignRight(),
                _ => container.AlignLeft()
            };
        }
    }

    /// <summary>
    /// 擴充介面以支援表格資料來源繫結
    /// </summary>
    public interface ITableReportData<TData>
    {
        IEnumerable<TData> Details { get; }
    }
}