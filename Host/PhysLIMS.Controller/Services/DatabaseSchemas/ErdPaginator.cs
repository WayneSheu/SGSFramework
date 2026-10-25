using SGSFramework.ApiInfrastructure.DTOs.DatabaseSchemas;

namespace SGSFramework.ApiInfrastructure.Services.DatabaseSchemas;

/// <summary>
/// ERD 單頁內容 (Diagram 只含本頁資料表與兩端皆在本頁的關聯)
/// </summary>
public sealed record ErdPage(ErdDiagramDto Diagram, IReadOnlyList<string> TableIds);

/// <summary>
/// ERD 分頁器：在「可閱讀的最小縮放比例」下，將資料表分配到多頁，且不切斷任何資料表。
/// 有 FK 關聯的資料表以 BFS 順序相鄰分配，盡量讓相關資料表落在同一頁；
/// 單一資料表大到一頁也放不下時獨立成頁 (再縮小呈現)。
/// </summary>
public static class ErdPaginator
{
    public static IReadOnlyList<ErdPage> Paginate(
        IReadOnlyList<TableSchemaDto> tables,
        bool keysOnly,
        double targetAspect,
        double pageWidth,
        double pageHeight,
        double minScale)
    {
        ArgumentNullException.ThrowIfNull(tables);

        if (tables.Count == 0)
        {
            return [];
        }

        var whole = ErdLayoutEngine.Build(tables, keysOnly, targetAspect);
        if (Fits(whole, pageWidth, pageHeight, minScale))
        {
            return [new ErdPage(whole, tables.Select(t => t.FullName).ToList())];
        }

        var pages = new List<ErdPage>();
        var current = new List<TableSchemaDto>();
        ErdDiagramDto? currentDiagram = null;

        foreach (var table in OrderByRelation(tables))
        {
            var candidate = new List<TableSchemaDto>(current) { table };
            var diagram = ErdLayoutEngine.Build(candidate, keysOnly, targetAspect);

            if (current.Count == 0 || Fits(diagram, pageWidth, pageHeight, minScale))
            {
                current = candidate;
                currentDiagram = diagram;
                continue;
            }

            pages.Add(new ErdPage(currentDiagram!, current.Select(t => t.FullName).ToList()));
            current = [table];
            currentDiagram = ErdLayoutEngine.Build(current, keysOnly, targetAspect);
        }

        if (current.Count > 0)
        {
            pages.Add(new ErdPage(currentDiagram!, current.Select(t => t.FullName).ToList()));
        }

        return pages;
    }

    private static bool Fits(ErdDiagramDto diagram, double pageWidth, double pageHeight, double minScale) =>
        Math.Min(pageWidth / diagram.Width, pageHeight / diagram.Height) >= minScale;

    /// <summary>
    /// 排序：關聯群組 (大→小) 內以 BFS 自關聯最多的資料表展開，最後為無關聯的資料表 (依名稱)
    /// </summary>
    private static List<TableSchemaDto> OrderByRelation(IReadOnlyList<TableSchemaDto> tables)
    {
        var byId = tables.ToDictionary(t => t.FullName, StringComparer.OrdinalIgnoreCase);
        var neighbors = byId.Keys.ToDictionary(k => k, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        foreach (var table in tables)
        {
            foreach (var fk in table.ForeignKeys)
            {
                if (byId.ContainsKey(fk.ReferencedFullName) && !fk.ReferencedFullName.Equals(table.FullName, StringComparison.OrdinalIgnoreCase))
                {
                    neighbors[table.FullName].Add(fk.ReferencedFullName);
                    neighbors[fk.ReferencedFullName].Add(table.FullName);
                }
            }
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var components = new List<List<string>>();
        foreach (var start in byId.Keys.Where(k => neighbors[k].Count > 0).OrderByDescending(k => neighbors[k].Count).ThenBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            if (!visited.Add(start))
            {
                continue;
            }

            var component = new List<string>();
            var queue = new Queue<string>([start]);
            while (queue.Count > 0)
            {
                var key = queue.Dequeue();
                component.Add(key);
                foreach (var next in neighbors[key].OrderByDescending(n => neighbors[n].Count).ThenBy(n => n, StringComparer.OrdinalIgnoreCase))
                {
                    if (visited.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            components.Add(component);
        }

        var isolated = byId.Keys.Where(k => neighbors[k].Count == 0).Order(StringComparer.OrdinalIgnoreCase);

        return components
            .OrderByDescending(c => c.Count)
            .SelectMany(c => c)
            .Concat(isolated)
            .Select(k => byId[k])
            .ToList();
    }
}
