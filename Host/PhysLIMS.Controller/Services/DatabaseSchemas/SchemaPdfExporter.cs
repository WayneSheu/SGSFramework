using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SGSFramework.ApiInfrastructure.DTOs.DatabaseSchemas;
using SkiaSharp;

namespace SGSFramework.ApiInfrastructure.Services.DatabaseSchemas;

/// <summary>
/// SSD Table Schema PDF 匯出 (QuestPDF)：
/// 文件摘要與資料表清單 → ERD (A3 橫向，向量繪製) → 各資料表欄位 / 索引 / 外鍵明細。
/// </summary>
public static class SchemaPdfExporter
{
    private const string DateFormat = "yyyy-MM-dd HH:mm";
    // Noto Sans TC 可被子集化嵌入 (PDF 僅數十 KB)；微軟正黑體為 TTC 會整檔嵌入 (約 14 MB)，僅作後備
    private const string FontFamily = "Noto Sans TC";
    private const string FallbackFontFamily = "Microsoft JhengHei";

    private static readonly string HeaderBlue = Colors.Blue.Darken3;
    private static readonly string GridGrey = Colors.Grey.Lighten2;

    static SchemaPdfExporter()
    {
        Settings.CheckIfAllTextGlyphsAreAvailable = false;
    }

    public static byte[] Export(DatabaseSchemaDto schema, bool includeErd, bool keysOnly)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var diagrams = includeErd ? BuildDiagrams(schema, keysOnly) : [];

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                ConfigurePage(page, PageSizes.A4.Landscape(), schema);
                page.Content().Column(col =>
                {
                    ComposeSummary(col, schema);
                });
            });

            foreach (var (title, diagram) in diagrams)
            {
                container.Page(page =>
                {
                    ConfigurePage(page, PageSizes.A3.Landscape(), schema);
                    page.Content().Column(col =>
                    {
                        col.Item().Text(title).FontSize(13).Bold().FontColor(HeaderBlue);
                        col.Item().PaddingTop(2).Text($"資料表 {diagram.Nodes.Count} 個、關聯 {diagram.Edges.Count} 條；鴉爪端 (多) 為子表 FK 欄位、雙槓端 (一) 為父表參考欄位；欄位名稱後 * 表示 NOT NULL。")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                        col.Item().PaddingTop(6).Extend().Canvas((canvas, size) => DrawDiagram(canvas, size, diagram));
                    });
                });
            }

            container.Page(page =>
            {
                ConfigurePage(page, PageSizes.A4.Landscape(), schema);
                page.Content().Column(col =>
                {
                    col.Item().Text("3. 資料表結構明細").FontSize(14).Bold().FontColor(HeaderBlue);
                    var index = 1;
                    foreach (var table in schema.Tables)
                    {
                        ComposeTable(col, table, index++);
                    }
                });
            });
        }).GeneratePdf();
    }

    private static List<(string Title, ErdDiagramDto Diagram)> BuildDiagrams(DatabaseSchemaDto schema, bool keysOnly)
    {
        var result = new List<(string, ErdDiagramDto)>();
        if (schema.Tables.Count == 0)
        {
            return result;
        }

        result.Add(("2. 實體關聯圖 (ERD) — 全部資料表", ErdLayoutEngine.Build(schema.Tables, keysOnly)));

        // 多個 Schema 時另附各 Schema 的分圖，避免單張圖過密
        var groups = schema.Tables.GroupBy(t => t.Schema).ToList();
        if (groups.Count > 1)
        {
            foreach (var group in groups)
            {
                result.Add(($"2. 實體關聯圖 (ERD) — Schema：{group.Key}", ErdLayoutEngine.Build(group.ToList(), keysOnly)));
            }
        }

        return result;
    }

    private static void ConfigurePage(PageDescriptor page, PageSize size, DatabaseSchemaDto schema)
    {
        page.Size(size);
        page.Margin(1.2f, Unit.Centimetre);
        page.DefaultTextStyle(x => x.FontFamily(FontFamily).Fallback(y => y.FontFamily(FallbackFontFamily)).FontSize(8).FontColor(Colors.Grey.Darken4));

        page.Header().Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Text("資料庫結構規格書 (SSD Table Schema)").FontSize(15).Bold().FontColor(HeaderBlue);
                row.ConstantItem(260).AlignRight().AlignBottom().Text($"{schema.DatabaseName}　產出：{schema.GeneratedAt.ToString(DateFormat)}")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            });
            col.Item().PaddingTop(3).PaddingBottom(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });

        page.Footer().AlignCenter().Text(text =>
        {
            text.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1));
            text.Span("第 ");
            text.CurrentPageNumber();
            text.Span(" / ");
            text.TotalPages();
            text.Span(" 頁");
        });
    }

    private static void ComposeSummary(ColumnDescriptor col, DatabaseSchemaDto schema)
    {
        col.Item().Text("1. 文件摘要").FontSize(14).Bold().FontColor(HeaderBlue);
        col.Item().PaddingTop(4).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(110);
                c.RelativeColumn();
            });

            void Info(string label, string value)
            {
                table.Cell().Background(Colors.Grey.Lighten4).Border(0.5f).BorderColor(GridGrey).Padding(4).Text(label).SemiBold();
                table.Cell().Border(0.5f).BorderColor(GridGrey).Padding(4).Text(value);
            }

            Info("資料庫名稱", schema.DatabaseName);
            Info("伺服器版本", schema.ServerVersion);
            Info("Schema", string.Join(", ", schema.Tables.Select(t => t.Schema).Distinct()));
            Info("資料表 / 欄位數", $"{schema.Tables.Count} 個資料表、{schema.Tables.Sum(t => t.Columns.Count)} 個欄位、{schema.Tables.Sum(t => t.ForeignKeys.Count)} 條外鍵關聯");
            Info("產出時間", schema.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss zzz"));
        });

        col.Item().PaddingTop(12).Text("資料表清單").FontSize(11).Bold().FontColor(HeaderBlue);
        col.Item().PaddingTop(4).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(28);
                c.ConstantColumn(50);
                c.RelativeColumn(3);
                c.RelativeColumn(4);
                c.ConstantColumn(78);
                c.ConstantColumn(78);
                c.ConstantColumn(58);
                c.ConstantColumn(36);
            });

            table.Header(h =>
            {
                foreach (var title in new[] { "#", "Schema", "資料表名稱", "說明", "建立日期", "最後異動日期", "資料筆數", "欄位數" })
                {
                    HeaderCell(h.Cell(), title);
                }
            });

            var i = 1;
            foreach (var t in schema.Tables)
            {
                BodyCell(table.Cell(), (i++).ToString(), center: true);
                BodyCell(table.Cell(), t.Schema);
                BodyCell(table.Cell(), t.Name, bold: true);
                BodyCell(table.Cell(), t.Description ?? string.Empty);
                BodyCell(table.Cell(), t.CreatedAt.ToString(DateFormat));
                BodyCell(table.Cell(), t.ModifiedAt.ToString(DateFormat));
                BodyCell(table.Cell(), t.RowCount.ToString("N0"), right: true);
                BodyCell(table.Cell(), t.Columns.Count.ToString(), center: true);
            }
        });
    }

    private static void ComposeTable(ColumnDescriptor col, TableSchemaDto t, int index)
    {
        col.Item().PaddingTop(14).ShowEntire().Column(head =>
        {
            head.Item().Text($"3.{index}  {t.FullName}").FontSize(11).Bold().FontColor(HeaderBlue);
            if (!string.IsNullOrWhiteSpace(t.Description))
            {
                head.Item().PaddingTop(1).Text(t.Description).FontSize(9);
            }

            var meta = $"建立日期：{t.CreatedAt.ToString(DateFormat)}　最後異動日期：{t.ModifiedAt.ToString(DateFormat)}　資料筆數：{t.RowCount:N0}";
            if (t.TemporalType is not null)
            {
                meta += $"　系統版本控制：{t.TemporalType}{(t.HistoryTable is null ? string.Empty : $" ({t.HistoryTable})")}";
            }

            if (t.LedgerType is not null)
            {
                meta += $"　帳本：{t.LedgerType}";
            }

            head.Item().PaddingTop(2).Text(meta).FontSize(8).FontColor(Colors.Grey.Darken1);
        });

        col.Item().PaddingTop(4).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(22);
                c.RelativeColumn(3);
                c.RelativeColumn(2);
                c.ConstantColumn(30);
                c.ConstantColumn(22);
                c.ConstantColumn(22);
                c.ConstantColumn(30);
                c.RelativeColumn(2);
                c.RelativeColumn(4);
            });

            table.Header(h =>
            {
                foreach (var title in new[] { "#", "欄位名稱", "資料型別", "Null", "PK", "FK", "UQ", "預設值 / Identity", "說明 / 參考" })
                {
                    HeaderCell(h.Cell(), title);
                }
            });

            foreach (var c in t.Columns)
            {
                BodyCell(table.Cell(), c.Ordinal.ToString(), center: true);
                BodyCell(table.Cell(), c.Name, bold: c.IsPrimaryKey);
                BodyCell(table.Cell(), c.FullDataType);
                BodyCell(table.Cell(), c.IsNullable ? "Y" : "N", center: true);
                BodyCell(table.Cell(), c.IsPrimaryKey ? "●" : string.Empty, center: true);
                BodyCell(table.Cell(), c.IsForeignKey ? "●" : string.Empty, center: true);
                BodyCell(table.Cell(), c.IsUnique ? "●" : string.Empty, center: true);
                BodyCell(table.Cell(), JoinNonEmpty(c.IdentityInfo, c.DefaultValue, c.ComputedDefinition is null ? null : $"AS {c.ComputedDefinition}"));
                BodyCell(table.Cell(), JoinNonEmpty(c.Description, c.ForeignKeyReference is null ? null : $"→ {c.ForeignKeyReference}"));
            }
        });

        if (t.Indexes.Count > 0)
        {
            col.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);
                    c.ConstantColumn(90);
                    c.ConstantColumn(40);
                    c.RelativeColumn(3);
                    c.RelativeColumn(2);
                });

                table.Header(h =>
                {
                    foreach (var title in new[] { "索引名稱", "類型", "Unique", "索引欄位", "Include / 篩選" })
                    {
                        HeaderCell(h.Cell(), title, Colors.Grey.Darken1);
                    }
                });

                foreach (var i in t.Indexes)
                {
                    BodyCell(table.Cell(), i.IsPrimaryKey ? $"{i.Name} (PK)" : i.Name);
                    BodyCell(table.Cell(), i.Type);
                    BodyCell(table.Cell(), i.IsUnique ? "Y" : string.Empty, center: true);
                    BodyCell(table.Cell(), i.Columns);
                    BodyCell(table.Cell(), JoinNonEmpty(i.IncludedColumns, i.Filter is null ? null : $"WHERE {i.Filter}"));
                }
            });
        }

        if (t.ForeignKeys.Count > 0)
        {
            col.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);
                    c.RelativeColumn(2);
                    c.RelativeColumn(3);
                    c.ConstantColumn(70);
                    c.ConstantColumn(70);
                });

                table.Header(h =>
                {
                    foreach (var title in new[] { "外鍵名稱", "欄位", "參考資料表 (欄位)", "ON DELETE", "ON UPDATE" })
                    {
                        HeaderCell(h.Cell(), title, Colors.Grey.Darken1);
                    }
                });

                foreach (var fk in t.ForeignKeys)
                {
                    BodyCell(table.Cell(), fk.IsDisabled ? $"{fk.Name} (停用)" : fk.Name);
                    BodyCell(table.Cell(), fk.Columns);
                    BodyCell(table.Cell(), $"{fk.ReferencedFullName} ({fk.ReferencedColumns})");
                    BodyCell(table.Cell(), fk.OnDelete);
                    BodyCell(table.Cell(), fk.OnUpdate);
                }
            });
        }
    }

    private static void HeaderCell(IContainer cell, string text, string? background = null) =>
        cell.Background(background ?? HeaderBlue).Border(0.5f).BorderColor(GridGrey).Padding(3)
            .Text(text).FontColor(Colors.White).SemiBold();

    private static void BodyCell(IContainer cell, string text, bool bold = false, bool center = false, bool right = false)
    {
        var container = cell.Border(0.5f).BorderColor(GridGrey).Padding(3);
        container = center ? container.AlignCenter() : right ? container.AlignRight() : container.AlignLeft();
        var span = container.Text(text);
        if (bold)
        {
            span.SemiBold();
        }
    }

    private static string JoinNonEmpty(params string?[] values) =>
        string.Join("\n", values.Where(v => !string.IsNullOrWhiteSpace(v)));

    // ── ERD 繪製 (SkiaSharp 向量) ───────────────────────────────────────

    private static void DrawDiagram(SKCanvas canvas, Size size, ErdDiagramDto diagram)
    {
        if (diagram.Nodes.Count == 0)
        {
            return;
        }

        var scale = (float)Math.Min(1.4, Math.Min(size.Width / diagram.Width, size.Height / diagram.Height));
        canvas.Save();
        canvas.Scale(scale);

        using var typeface = ResolveTypeface();
        using var edgePaint = new SKPaint { Color = SKColor.Parse("#8A8886"), StrokeWidth = 1.1f, IsStroke = true, IsAntialias = true };
        using var borderPaint = new SKPaint { Color = SKColor.Parse("#8A8886"), StrokeWidth = 1f, IsStroke = true, IsAntialias = true };
        using var headerPaint = new SKPaint { Color = SKColor.Parse("#0F6CBD"), IsAntialias = true };
        using var bodyPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var stripePaint = new SKPaint { Color = SKColor.Parse("#F5F5F5"), IsAntialias = true };
        using var titleText = new SKPaint { Color = SKColors.White, TextSize = 11, Typeface = typeface, IsAntialias = true, FakeBoldText = true };
        using var columnText = new SKPaint { Color = SKColor.Parse("#242424"), TextSize = 10, Typeface = typeface, IsAntialias = true };
        using var typeText = new SKPaint { Color = SKColor.Parse("#707070"), TextSize = 9, Typeface = typeface, IsAntialias = true, TextAlign = SKTextAlign.Right };
        using var keyText = new SKPaint { Color = SKColor.Parse("#C19C00"), TextSize = 8, Typeface = typeface, IsAntialias = true, FakeBoldText = true };
        using var fkText = new SKPaint { Color = SKColor.Parse("#0F6CBD"), TextSize = 8, Typeface = typeface, IsAntialias = true, FakeBoldText = true };

        foreach (var edge in diagram.Edges)
        {
            using var path = new SKPath();
            path.MoveTo((float)edge.Points[0].X, (float)edge.Points[0].Y);
            foreach (var p in edge.Points.Skip(1))
            {
                path.LineTo((float)p.X, (float)p.Y);
            }

            canvas.DrawPath(path, edgePaint);
            DrawManyMarker(canvas, edge.Points[0], edge.Points[1], edgePaint);
            DrawOneMarker(canvas, edge.Points[^1], edge.Points[^2], edgePaint);
        }

        foreach (var node in diagram.Nodes)
        {
            var x = (float)node.X;
            var y = (float)node.Y;
            var w = (float)node.Width;
            var h = (float)node.Height;
            var rect = new SKRoundRect(new SKRect(x, y, x + w, y + h), 4);

            canvas.DrawRoundRect(rect, bodyPaint);
            canvas.Save();
            canvas.ClipRoundRect(rect);
            canvas.DrawRect(x, y, w, (float)ErdLayoutEngine.HeaderHeight, headerPaint);
            for (var i = 0; i < node.Columns.Count; i += 2)
            {
                var rowTop = y + (float)(ErdLayoutEngine.HeaderHeight + ErdLayoutEngine.BodyPadding / 2 + i * ErdLayoutEngine.RowHeight);
                canvas.DrawRect(x, rowTop, w, (float)ErdLayoutEngine.RowHeight, stripePaint);
            }

            canvas.Restore();
            canvas.DrawRoundRect(rect, borderPaint);
            canvas.DrawText(node.Id, x + 8, y + 18, titleText);

            for (var i = 0; i < node.Columns.Count; i++)
            {
                var c = node.Columns[i];
                var baseline = y + (float)(ErdLayoutEngine.HeaderHeight + ErdLayoutEngine.BodyPadding / 2 + i * ErdLayoutEngine.RowHeight + 13);
                if (c.IsPrimaryKey)
                {
                    canvas.DrawText("PK", x + 6, baseline, keyText);
                }
                else if (c.IsForeignKey)
                {
                    canvas.DrawText("FK", x + 6, baseline, fkText);
                }

                columnText.FakeBoldText = c.IsPrimaryKey;
                canvas.DrawText(c.IsNullable ? c.Name : $"{c.Name} *", x + 24, baseline, columnText);
                canvas.DrawText(c.DataType, x + w - 6, baseline, typeText);
            }
        }

        canvas.Restore();
    }

    private static SKTypeface ResolveTypeface()
    {
        foreach (var family in new[] { FontFamily, FallbackFontFamily })
        {
            var typeface = SKTypeface.FromFamilyName(family);
            if (typeface is not null && typeface.FamilyName.Equals(family, StringComparison.OrdinalIgnoreCase))
            {
                return typeface;
            }

            typeface?.Dispose();
        }

        return SKTypeface.FromFamilyName(null);
    }

    /// <summary>
    /// 子表端 (多)：鴉爪符號
    /// </summary>
    private static void DrawManyMarker(SKCanvas canvas, ErdPointDto end, ErdPointDto next, SKPaint paint)
    {
        var (ux, uy) = Direction(end, next);
        var tipX = (float)(end.X + ux * 10);
        var tipY = (float)(end.Y + uy * 10);
        var px = -uy * 5;
        var py = ux * 5;
        canvas.DrawLine(tipX, tipY, (float)(end.X + px), (float)(end.Y + py), paint);
        canvas.DrawLine(tipX, tipY, (float)(end.X - px), (float)(end.Y - py), paint);
    }

    /// <summary>
    /// 父表端 (一)：雙短槓
    /// </summary>
    private static void DrawOneMarker(SKCanvas canvas, ErdPointDto end, ErdPointDto previous, SKPaint paint)
    {
        var (ux, uy) = Direction(end, previous);
        var px = -uy * 5;
        var py = ux * 5;
        foreach (var d in new[] { 6.0, 10.0 })
        {
            var cx = end.X + ux * d;
            var cy = end.Y + uy * d;
            canvas.DrawLine((float)(cx + px), (float)(cy + py), (float)(cx - px), (float)(cy - py), paint);
        }
    }

    private static (double X, double Y) Direction(ErdPointDto from, ErdPointDto to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        return len < 0.001 ? (1, 0) : (dx / len, dy / len);
    }
}
