using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SGSFramework.ReportEngine.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGSFramework.ReportEngine.Generators
{
    public abstract class ReportGeneratorBase<TReportData> where TReportData : IReportData
    {
        protected TReportData _reportData;

        // 通用顏色定義
        protected static readonly string Color_Grid_Grey = "#BFBFBF";
        protected static readonly string Color_Text_White = "#FFFFFF";

        public void SetReportData(TReportData data) => _reportData = data;

        public byte[] GeneratePdf()
        {
            if (_reportData == null) throw new InvalidOperationException("尚未設定報表資料。");

            //QuestPDF.Settings.License = LicenseType.Community;

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    // 由子類別決定紙張方向
                    ConfigurePage(page);

                    // 統一的頁首 (可被子類別 override)
                    page.Header().Element(ComposeHeader);

                    // 抽象方法：由子類別實作核心內容
                    page.Content().PaddingVertical(5).Element(ComposeContent);

                    // 統一的頁尾
                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("第 "); text.CurrentPageNumber(); text.Span(" 頁 / 共 "); text.TotalPages(); text.Span(" 頁");
                    });
                });
            }).GeneratePdf();
        }

        // 預設配置，子類別可視需求覆寫
        protected virtual void ConfigurePage(PageDescriptor page)
        {
            page.Size(PageSizes.A4);
            page.Margin(0.5f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontFamily("Microsoft JhengHei").FontSize(8));
        }

        protected virtual void ComposeHeader(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Text(_reportData.ReportTitle).FontSize(16).SemiBold();
                row.RelativeItem().AlignRight().Text($"{_reportData.QueryDate} | 查詢人: {_reportData.OperatorName}");
            });
        }

        // 子類別必須實作的核心內容
        protected abstract void ComposeContent(IContainer container);
    
    }
}
