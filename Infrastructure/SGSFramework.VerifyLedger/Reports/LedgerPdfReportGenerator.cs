using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SGSFramework.VerifyLedger.Dtos;
using System.Text.Json;

namespace SGSFramework.VerifyLedger.Reports
{
    /// <summary>
    /// 企業級 SQL Server Ledger 驗證結果 PDF 報告產生器
    /// </summary>
    public class LedgerPdfReportGenerator
    {
        static LedgerPdfReportGenerator()
        {
            Settings.CheckIfAllTextGlyphsAreAvailable = false;
        }

        /// <summary>
        /// 根據總帳驗證結果產生標準 PDF 稽核報告（內含實體資料表追溯）
        /// </summary>
        /// <param name="result">總帳驗證強型別結果</param>
        /// <param name="entityName">受驗證的交易實體/資料表名稱</param>
        public byte[] GenerateReport(LedgerVerificationResult result, string entityName)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentException.ThrowIfNullOrEmpty(entityName);

            try
            {
                var digestInfo = ParseDigest(result.ExtractedDigest);

                return Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);

                        page.DefaultTextStyle(x => x
                            .FontFamily("Microsoft JhengHei")
                            .Fallback(y => y.FontFamily("Noto Sans TC"))
                            .FontSize(10)
                            .FontColor(Colors.Grey.Darken3));

                        // 頁首
                        page.Header().Column(headerCol =>
                        {
                            headerCol.Item().Row(row =>
                            {
                                row.RelativeItem().Text("SQL Server Ledger Table 驗證結果分析報告")
                                    .FontSize(18)
                                    .Bold()
                                    .FontColor(Colors.Blue.Darken3);

                                row.ConstantItem(60).Border(1).BorderColor(Colors.Red.Lighten2).Background(Colors.Red.Lighten5)
                                    .Padding(2).AlignCenter().Text("機密文件")
                                    .FontSize(9).Bold().FontColor(Colors.Red.Darken2);
                            });
                            headerCol.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                        });

                        // 內容
                        page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                        {
                            // 1. 驗證狀態摘要
                            col.Item().Text("1. 驗證狀態摘要").FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
                            col.Item().PaddingTop(5).Background(result.IsSuccess ? Colors.Green.Lighten5 : Colors.Red.Lighten5)
                                .Border(1).BorderColor(result.IsSuccess ? Colors.Green.Lighten2 : Colors.Red.Lighten2)
                                .Padding(10).Column(statusCol =>
                                {
                                    statusCol.Item().Text($"驗證結果 (isSuccess)：{(result.IsSuccess ? "TRUE (成功)" : "FALSE (失敗)")}")
                                        .FontSize(11).Bold().FontColor(result.IsSuccess ? Colors.Green.Darken3 : Colors.Red.Darken3);

                                    statusCol.Item().PaddingTop(4).Text(result.IsSuccess
                                        ? $"技術解讀：驗證成功。代表自該區塊建立以來，目標資料表「{entityName}」之帳本歷史資料未經任何未授權、越權的非法竄改，鏈結簽章完整。"
                                        : $"技術解讀：警告！目標資料表「{entityName}」之帳本雜湊鏈結斷裂，系統偵測到潛在的外力竄改風險！");
                                });

                            col.Item().PaddingTop(15).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

                            // 2. 驗證詳細資訊
                            col.Item().PaddingTop(10).Text("2. 驗證詳細資訊").FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
                            col.Item().PaddingTop(5).Column(detailCol =>
                            {
                                // 關鍵異動：註明受稽核標的
                                detailCol.Item().Text(x =>
                                {
                                    x.Span("受稽核資料表 (Target Table)：").Bold();
                                    x.Span(entityName).FontColor(Colors.Blue.Darken4).Bold();
                                });

                                detailCol.Item().PaddingTop(4).Text(x =>
                                {
                                    x.Span("驗證回應訊息 (verificationMessage)：").Bold();
                                    x.Span(result.VerificationMessage ?? "無回應訊息");
                                });

                                detailCol.Item().PaddingTop(4).Text(x =>
                                {
                                    x.Span("驗證時間戳記 (verifiedAt)：").Bold();
                                    x.Span($"{result.VerifiedAt:yyyy-MM-dd HH:mm:ss.fff} (UTC)");
                                });
                            });

                            col.Item().PaddingTop(15).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

                            // 3. 摘要內容分析
                            col.Item().PaddingTop(10).Text("3. 摘要內容分析 (Extracted Digest)").FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
                            col.Item().PaddingTop(2).Text("此欄位記錄了當下該帳本資料表狀態的區塊鏈快照，用於外部審計（Auditing）：");

                            col.Item().PaddingTop(8).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(160);
                                    columns.RelativeColumn();
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Background(Colors.Grey.Lighten3).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text("欄位說明").Bold();
                                    header.Cell().Background(Colors.Grey.Lighten3).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text("當前數值 / 技術意義").Bold();
                                });

                                // 新增資料列：標的物件
                                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text("target_ledger_table").Bold();
                                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text($"{entityName} (本次帳本雜湊完整性校驗之唯一標的)");

                                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text("database_name").Bold();
                                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text($"{digestInfo.DatabaseName} (驗證的目標來源資料庫名稱)");

                                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text("block_id").Bold();
                                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text($"{digestInfo.BlockId} (驗證結束的最高區塊序號)");

                                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text("hash").Bold();
                                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Column(hashCol =>
                                {
                                    hashCol.Item().Text(digestInfo.Hash).FontColor(Colors.Blue.Darken4).FontFamily("Consolas");
                                    hashCol.Item().PaddingTop(2).Text("數位指紋：這是該區塊的 SHA-256 雜湊值。任何對底層區塊數據的非法修改，均會導致此值產生變異。").FontSize(9).FontColor(Colors.Grey.Darken1);
                                });

                                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text("last_transaction_commit_time").Bold();
                                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text($"{digestInfo.LastTxCommitTime} (該區塊中最後一筆交易提交時間)");

                                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text("digest_time").Bold();
                                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text($"{digestInfo.DigestTime} (產生此筆數位摘要公報的系統時間)");
                            });
                        });

                        // 頁尾
                        page.Footer().Column(footerCol =>
                        {
                            footerCol.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                            footerCol.Item().PaddingTop(3).Row(row =>
                            {
                                row.RelativeItem().Text($"系統自動產生時間: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC | SES 安全稽核模組")
                                    .FontSize(8).FontColor(Colors.Grey.Darken1);

                                row.RelativeItem().AlignRight().Text(x =>
                                {
                                    x.Span("第 ").FontSize(8);
                                    x.CurrentPageNumber().FontSize(8);
                                    x.Span(" 頁，共 ").FontSize(8);
                                    x.TotalPages().FontSize(8);
                                    x.Span(" 頁").FontSize(8);
                                });
                            });
                        });
                    });
                }).GeneratePdf();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("QuestPDF 核心產生總帳稽核報告失敗。", ex);
            }
        }

        private static DigestModel ParseDigest(string? json)
        {
            var model = new DigestModel();
            if (string.IsNullOrWhiteSpace(json)) return model;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var targetElement = root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0 ? root[0] : root;

                if (targetElement.ValueKind == JsonValueKind.Object)
                {
                    if (targetElement.TryGetProperty("database_name", out var db)) model.DatabaseName = db.GetString() ?? "N/A";
                    if (targetElement.TryGetProperty("block_id", out var bid)) model.BlockId = bid.ValueKind == JsonValueKind.Number ? bid.GetInt64().ToString() : bid.GetString() ?? "0";
                    if (targetElement.TryGetProperty("hash", out var h)) model.Hash = h.GetString() ?? "N/A";
                    if (targetElement.TryGetProperty("last_transaction_commit_time", out var lt)) model.LastTxCommitTime = lt.GetString() ?? "N/A";
                    if (targetElement.TryGetProperty("digest_time", out var dt)) model.DigestTime = dt.GetString() ?? "N/A";
                }
            }
            catch
            {
                model.Hash = "無法解析的雜湊格式";
            }
            return model;
        }

        private class DigestModel
        {
            public string DatabaseName { get; set; } = "N/A";
            public string BlockId { get; set; } = "0";
            public string Hash { get; set; } = "N/A";
            public string LastTxCommitTime { get; set; } = "N/A";
            public string DigestTime { get; set; } = "N/A";
        }
    }
}
