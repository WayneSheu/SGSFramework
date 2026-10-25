using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SGSFramework.ApiInfrastructure.DTOs.DatabaseSchemas;
using SkiaSharp;

namespace SGSFramework.ApiInfrastructure.Services.DatabaseSchemas;

/// <summary>
/// SSD Table Schema PDF 匯出 (QuestPDF)，整份文件可選 A4 直式或 A4 橫式：
/// 封面 → 1. 文件摘要 → 2. 資料庫更新履歷 (Migration) → 3. ERD (依可閱讀比例分頁、不切斷資料表) → 4. 資料表結構明細 (依 Schema 分節)。
/// 標題分三層：章 (N.)、節 (N.M)、小節 (N.M.K)；章號依實際包含的內容連續編號。
/// </summary>
public static class SchemaPdfExporter
{
    private const string DateFormat = "yyyy-MM-dd HH:mm";
    // Noto Sans TC 可被子集化嵌入 (PDF 僅數十 KB)；微軟正黑體為 TTC 會整檔嵌入 (約 14 MB)，僅作後備
    private const string FontFamily = "Noto Sans TC";
    private const string FallbackFontFamily = "Microsoft JhengHei";

    // ERD 版面目標長寬比與每頁可用繪圖區 (pt，已扣除頁首、章節標題與圖說的保守估計)
    private const double ErdPortraitAspect = 0.75;
    private const double ErdLandscapeAspect = 1.7;
    private const double ErdPortraitWidth = 527;
    private const double ErdPortraitHeight = 600;
    private const double ErdLandscapeWidth = 774;
    private const double ErdLandscapeHeight = 360;

    // 低於此縮放比例 (欄位字級約 6pt) 即分頁
    private const double ErdMinReadableScale = 0.6;

    private static readonly string HeaderBlue = Colors.Blue.Darken3;
    private static readonly string GridGrey = Colors.Grey.Lighten2;

    static SchemaPdfExporter()
    {
        Settings.CheckIfAllTextGlyphsAreAvailable = false;
    }

    public static byte[] Export(
        DatabaseSchemaDto schema,
        bool includeErd,
        bool keysOnly,
        PdfOrientation orientation = PdfOrientation.Portrait,
        bool includeHistory = true)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var portrait = orientation == PdfOrientation.Portrait;
        var pageSize = portrait ? PageSizes.A4 : PageSizes.A4.Landscape();
        var schemaGroups = schema.Tables.GroupBy(t => t.Schema).ToList();

        // 章號依實際包含的內容連續編號
        var chapter = 1;
        var historyChapter = includeHistory && schema.Migrations.Count > 0 ? ++chapter : 0;
        var erdChapter = includeErd && schema.Tables.Count > 0 ? ++chapter : 0;
        var detailChapter = ++chapter;
        var erdSections = erdChapter > 0 ? BuildErdSections(schema, keysOnly, portrait, erdChapter) : [];

        return Document.Create(container =>
        {
            // 封面
            container.Page(page =>
            {
                page.Size(pageSize);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontFamily(FontFamily).Fallback(y => y.FontFamily(FallbackFontFamily)).FontSize(10).FontColor(Colors.Grey.Darken4));
                page.Content().Element(c => ComposeCover(c, schema, portrait));
            });

            // 1. 文件摘要
            container.Page(page =>
            {
                ConfigurePage(page, pageSize, schema);
                page.Content().Column(col => ComposeSummary(col, schema, portrait));
            });

            // 2. 資料庫更新履歷
            if (historyChapter > 0)
            {
                container.Page(page =>
                {
                    ConfigurePage(page, pageSize, schema);
                    page.Content().Column(col => ComposeHistory(col, schema, historyChapter, portrait));
                });
            }

            // 3. ERD：每個 ERD 頁獨立一頁，章標題僅出現在第一頁
            var erdPageIndex = 0;
            foreach (var section in erdSections)
            {
                for (var i = 0; i < section.Pages.Count; i++)
                {
                    var erdPage = section.Pages[i];
                    var isFirstPage = erdPageIndex++ == 0;
                    var pageLabel = section.Pages.Count > 1 ? $"（第 {i + 1} / {section.Pages.Count} 頁）" : string.Empty;
                    var crossRefs = section.CrossReferences[i];

                    container.Page(page =>
                    {
                        ConfigurePage(page, pageSize, schema);
                        page.Content().Column(col =>
                        {
                            if (isFirstPage)
                            {
                                Chapter(col, $"{erdChapter}.　實體關聯圖 (ERD)");
                                col.Item().PaddingBottom(4).Text("圖例：鴉爪端 (多) 為子表 FK 欄位、雙槓端 (一) 為父表參考欄位；欄位名稱後 * 表示 NOT NULL。資料表過多時依關聯分頁，跨頁關聯列於各頁圖說。")
                                    .FontSize(8).FontColor(Colors.Grey.Darken1);
                            }

                            Section(col, $"{section.Number}　{section.Title}{pageLabel}", topPadding: isFirstPage ? 4 : 0);
                            col.Item().PaddingTop(3).Text($"本頁資料表 {erdPage.Diagram.Nodes.Count} 個、關聯 {erdPage.Diagram.Edges.Count} 條")
                                .FontSize(8).FontColor(Colors.Grey.Darken1);
                            if (crossRefs.Count > 0)
                            {
                                col.Item().PaddingTop(2).Text($"跨頁關聯：{string.Join("；", crossRefs)}").FontSize(7).FontColor(Colors.Grey.Darken1);
                            }

                            col.Item().PaddingTop(6).Extend().Canvas((canvas, size) => DrawDiagram(canvas, size, erdPage.Diagram));
                        });
                    });
                }
            }

            // 4. 資料表結構明細：依 Schema 分節，每節由新頁開始
            container.Page(page =>
            {
                ConfigurePage(page, pageSize, schema);
                page.Content().Column(col =>
                {
                    Chapter(col, $"{detailChapter}.　資料表結構明細");

                    for (var s = 0; s < schemaGroups.Count; s++)
                    {
                        if (s > 0)
                        {
                            col.Item().PageBreak();
                        }

                        var group = schemaGroups[s];
                        var sectionNumber = $"{detailChapter}.{s + 1}";
                        Section(col, $"{sectionNumber}　Schema：{group.Key}（{group.Count()} 個資料表）", topPadding: s == 0 ? 4 : 0);

                        var index = 1;
                        foreach (var table in group)
                        {
                            ComposeTable(col, table, $"{sectionNumber}.{index++}");
                        }
                    }
                });
            });
        }).GeneratePdf();
    }

    /// <summary>
    /// ERD 節：整份資料表在可閱讀比例下可放一頁時為單一節「全部資料表」；
    /// 否則依 Schema 分節，各節再依關聯分頁 (不切斷資料表)，並計算跨頁 / 跨 Schema 關聯說明。
    /// </summary>
    private static List<ErdSection> BuildErdSections(DatabaseSchemaDto schema, bool keysOnly, bool portrait, int chapter)
    {
        if (schema.Tables.Count == 0)
        {
            return [];
        }

        var aspect = portrait ? ErdPortraitAspect : ErdLandscapeAspect;
        var width = portrait ? ErdPortraitWidth : ErdLandscapeWidth;
        var height = portrait ? ErdPortraitHeight : ErdLandscapeHeight;

        var groups = new List<(string Title, IReadOnlyList<TableSchemaDto> Tables)>();
        var whole = ErdPaginator.Paginate(schema.Tables, keysOnly, aspect, width, height, ErdMinReadableScale);
        var schemaGroups = schema.Tables.GroupBy(t => t.Schema).ToList();
        if (whole.Count == 1 || schemaGroups.Count == 1)
        {
            groups.Add((schemaGroups.Count == 1 ? $"Schema：{schemaGroups[0].Key}" : "全部資料表", schema.Tables));
        }
        else
        {
            groups.AddRange(schemaGroups.Select(g => ($"Schema：{g.Key}", (IReadOnlyList<TableSchemaDto>)g.ToList())));
        }

        var sections = groups
            .Select((g, i) => new ErdSection(
                $"{chapter}.{i + 1}",
                g.Title,
                groups.Count == 1 ? whole : ErdPaginator.Paginate(g.Tables, keysOnly, aspect, width, height, ErdMinReadableScale),
                []))
            .ToList();

        // 資料表 → 所在節與頁碼，用於跨頁關聯說明
        var location = new Dictionary<string, (string Section, int Page, int PageCount)>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in sections)
        {
            for (var p = 0; p < section.Pages.Count; p++)
            {
                foreach (var id in section.Pages[p].TableIds)
                {
                    location[id] = (section.Number, p + 1, section.Pages.Count);
                }
            }
        }

        string Where(string id) => location.TryGetValue(id, out var loc)
            ? loc.PageCount > 1 ? $"{loc.Section} 第 {loc.Page} 頁" : loc.Section
            : "未納入";

        var byId = schema.Tables.ToDictionary(t => t.FullName, StringComparer.OrdinalIgnoreCase);
        foreach (var section in sections)
        {
            foreach (var page in section.Pages)
            {
                var onPage = page.TableIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var refs = new List<string>();
                foreach (var id in page.TableIds)
                {
                    // 本頁子表 → 他頁父表
                    refs.AddRange(byId[id].ForeignKeys
                        .Where(fk => !onPage.Contains(fk.ReferencedFullName) && location.ContainsKey(fk.ReferencedFullName))
                        .Select(fk => $"{id}.{fk.Columns} → {fk.ReferencedFullName}（{Where(fk.ReferencedFullName)}）"));
                }

                // 他頁子表 → 本頁父表
                refs.AddRange(schema.Tables
                    .Where(t => !onPage.Contains(t.FullName) && location.ContainsKey(t.FullName))
                    .SelectMany(t => t.ForeignKeys.Where(fk => onPage.Contains(fk.ReferencedFullName))
                        .Select(fk => $"{t.FullName}.{fk.Columns}（{Where(t.FullName)}） → {fk.ReferencedFullName}")));

                section.CrossReferences.Add(refs.Distinct().ToList());
            }
        }

        return sections;
    }

    private sealed record ErdSection(string Number, string Title, IReadOnlyList<ErdPage> Pages, List<IReadOnlyList<string>> CrossReferences);

    // ── 章節標題 (三層樣式) ─────────────────────────────────────────────

    /// <summary>
    /// 章：大字、底部粗線
    /// </summary>
    private static void Chapter(ColumnDescriptor col, string text) =>
        col.Item().PaddingBottom(8).BorderBottom(2).BorderColor(HeaderBlue).PaddingBottom(4)
            .Text(text).FontSize(17).Bold().FontColor(HeaderBlue);

    /// <summary>
    /// 節：淡藍底、左側色條
    /// </summary>
    private static void Section(ColumnDescriptor col, string text, float topPadding = 12) =>
        col.Item().PaddingTop(topPadding).BorderLeft(4).BorderColor(HeaderBlue).Background(Colors.Blue.Lighten5)
            .PaddingVertical(4).PaddingLeft(8).Text(text).FontSize(12.5f).Bold().FontColor(HeaderBlue);

    private static void ConfigurePage(PageDescriptor page, PageSize size, DatabaseSchemaDto schema)
    {
        page.Size(size);
        page.Margin(1.2f, Unit.Centimetre);
        page.DefaultTextStyle(x => x.FontFamily(FontFamily).Fallback(y => y.FontFamily(FallbackFontFamily)).FontSize(8).FontColor(Colors.Grey.Darken4));

        page.Header().Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().AlignBottom().Text("資料庫結構規格書 (SSD Table Schema)").FontSize(10).SemiBold().FontColor(Colors.Grey.Darken2);
                row.ConstantItem(260).AlignRight().AlignBottom().Text($"{schema.DatabaseName}　產出：{schema.GeneratedAt.ToString(DateFormat)}")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            });
            col.Item().PaddingTop(3).PaddingBottom(8).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
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

    /// <summary>
    /// 封面：文件名稱、資料庫資訊、目前資料庫版本 (最新已套用 Migration) 與產出時間
    /// </summary>
    private static void ComposeCover(IContainer container, DatabaseSchemaDto schema, bool portrait)
    {
        var latest = schema.Migrations.FirstOrDefault(m => m.IsApplied);

        container.Column(col =>
        {
            col.Item().Height(portrait ? 150 : 40);
            col.Item().Text("資料庫結構規格書").FontSize(32).Bold().FontColor(HeaderBlue);
            col.Item().PaddingTop(4).Text("SSD Table Schema Specification").FontSize(14).FontColor(Colors.Grey.Darken1);
            col.Item().PaddingTop(14).Width(140).LineHorizontal(3).LineColor(HeaderBlue);

            col.Item().PaddingTop(portrait ? 60 : 30).Width(portrait ? 420 : 520).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(120);
                    c.RelativeColumn();
                });

                void Info(string label, string value)
                {
                    table.Cell().BorderBottom(0.5f).BorderColor(GridGrey).PaddingVertical(7).Text(label).FontColor(Colors.Grey.Darken1);
                    table.Cell().BorderBottom(0.5f).BorderColor(GridGrey).PaddingVertical(7).Text(value).SemiBold();
                }

                Info("資料庫名稱", schema.DatabaseName);
                Info("伺服器版本", $"SQL Server {schema.ServerVersion}");
                Info("Schema", string.Join(", ", schema.Tables.Select(t => t.Schema).Distinct()));
                Info("資料表 / 欄位", $"{schema.Tables.Count} 個資料表、{schema.Tables.Sum(t => t.Columns.Count)} 個欄位");
                Info("資料庫版本", latest is null ? "—" : $"{latest.Name}（{latest.CreatedAt?.ToString(DateFormat) ?? latest.MigrationId}）");
                Info("產出時間", schema.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            });

            col.Item().Extend().AlignBottom().Text("本文件由系統依資料庫實際結構與 EF Core Migration 自動產生。")
                .FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }

    /// <summary>
    /// 資料庫更新履歷：每支 Migration 的建立時間、名稱、所屬 DbContext、套用狀態與變更摘要 (新→舊)
    /// </summary>
    private static void ComposeHistory(ColumnDescriptor col, DatabaseSchemaDto schema, int chapter, bool portrait)
    {
        var applied = schema.Migrations.Count(m => m.IsApplied);

        Chapter(col, $"{chapter}.　資料庫更新履歷");
        col.Item().PaddingBottom(6).Text($"取自 EF Core Migration 檔與 __EFMigrationsHistory，依建立時間新→舊排列。共 {schema.Migrations.Count} 筆，已套用 {applied} 筆。")
            .FontSize(8).FontColor(Colors.Grey.Darken1);

        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(24);
                c.ConstantColumn(portrait ? 58 : 72);
                c.RelativeColumn(3);
                c.ConstantColumn(portrait ? 40 : 90);
                c.ConstantColumn(36);
                c.RelativeColumn(6);
            });

            table.Header(h =>
            {
                foreach (var title in new[] { "#", "建立時間", "Migration", portrait ? "Schema" : "DbContext", "狀態", "變更內容" })
                {
                    HeaderCell(h.Cell(), title);
                }
            });

            var i = 1;
            foreach (var m in schema.Migrations)
            {
                var status = !m.HasMigrationFile ? "缺檔" : m.IsApplied ? "已套用" : "未套用";
                BodyCell(table.Cell(), (i++).ToString(), center: true);
                BodyCell(table.Cell(), m.CreatedAt?.ToString(portrait ? "yyyy-MM-dd\nHH:mm" : DateFormat) ?? "—");
                BodyCell(table.Cell(), m.Name, bold: true);
                // 直式頁寬有限，僅顯示 Schema；橫式顯示 DbContext 與 Schema
                BodyCell(table.Cell(), portrait ? m.Schema ?? m.Context : m.Schema is null ? m.Context : $"{m.Context}\n({m.Schema})");
                table.Cell().Border(0.5f).BorderColor(GridGrey).Padding(3).AlignCenter()
                    .Text(status).FontColor(m.IsApplied && m.HasMigrationFile ? Colors.Green.Darken2 : Colors.Red.Darken2).SemiBold();
                BodyCell(table.Cell(), string.Join("\n", m.Changes));
            }
        });
    }

    private static void ComposeSummary(ColumnDescriptor col, DatabaseSchemaDto schema, bool portrait)
    {
        // 直式頁寬較窄：日期僅顯示年月日，空間留給資料表名稱
        var listDateFormat = portrait ? "yyyy-MM-dd" : DateFormat;
        var dateWidth = portrait ? 56 : 78;

        Chapter(col, "1.　文件摘要");
        Section(col, "1.1　文件資訊", topPadding: 4);
        col.Item().PaddingTop(6).Table(table =>
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

        Section(col, "1.2　資料表清單");
        col.Item().PaddingTop(6).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(portrait ? 24 : 28);
                c.ConstantColumn(portrait ? 40 : 50);
                c.RelativeColumn(portrait ? 4 : 3);
                c.RelativeColumn(portrait ? 3 : 4);
                c.ConstantColumn(dateWidth);
                c.ConstantColumn(dateWidth);
                c.ConstantColumn(portrait ? 48 : 58);
                c.ConstantColumn(portrait ? 32 : 36);
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
                BodyCell(table.Cell(), t.CreatedAt.ToString(listDateFormat));
                BodyCell(table.Cell(), t.ModifiedAt.ToString(listDateFormat));
                BodyCell(table.Cell(), t.RowCount.ToString("N0"), right: true);
                BodyCell(table.Cell(), t.Columns.Count.ToString(), center: true);
            }
        });
    }

    /// <summary>
    /// 小節：單一資料表 (編號 N.M.K)
    /// </summary>
    private static void ComposeTable(ColumnDescriptor col, TableSchemaDto t, string number)
    {
        col.Item().PaddingTop(14).ShowEntire().Column(head =>
        {
            head.Item().BorderBottom(0.75f).BorderColor(Colors.Blue.Lighten3).PaddingBottom(2).Text(text =>
            {
                text.Span($"{number}　").FontSize(11).Bold().FontColor(HeaderBlue);
                text.Span(t.Name).FontSize(11).Bold().FontColor(Colors.Grey.Darken4);
                text.Span($"　{t.Schema}").FontSize(8).FontColor(Colors.Grey.Darken1);
            });
            if (!string.IsNullOrWhiteSpace(t.Description))
            {
                head.Item().PaddingTop(3).Text(t.Description).FontSize(9);
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

        // 小圖不放大超過原尺寸，並水平置中
        var scale = (float)Math.Min(1.0, Math.Min(size.Width / diagram.Width, size.Height / diagram.Height));
        canvas.Save();
        canvas.Translate((float)Math.Max(0, (size.Width - diagram.Width * scale) / 2), 0);
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
