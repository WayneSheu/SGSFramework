using SGSFramework.ApiInfrastructure.DTOs.DatabaseSchemas;

namespace SGSFramework.ApiInfrastructure.Services.DatabaseSchemas;

/// <summary>
/// ERD 版面配置引擎 (分層式配置)：
/// 被參考的父表置於左側、子表依 FK 深度往右排列，未參與任何關聯的資料表以網格排列於下方。
/// 產出絕對座標，前端 SVG 與後端 PDF 共用，確保畫面與匯出一致。
/// </summary>
public static class ErdLayoutEngine
{
    public const double HeaderHeight = 28;
    public const double RowHeight = 18;
    public const double BodyPadding = 6;

    private const double MinNodeWidth = 180;
    private const double MaxNodeWidth = 380;
    private const double CharWidth = 6.6;
    private const double GapX = 90;
    private const double GapY = 36;
    private const double Margin = 24;

    /// <summary>
    /// 畫面 (橫向) 預設長寬比
    /// </summary>
    public const double LandscapeAspect = 1.5;

    /// <param name="tables">要繪製的資料表</param>
    /// <param name="keysOnly">僅顯示 PK / FK 欄位</param>
    /// <param name="targetAspect">目標整體長寬比 (寬 / 高)；PDF 直式頁面約 0.75</param>
    public static ErdDiagramDto Build(IReadOnlyList<TableSchemaDto> tables, bool keysOnly, double targetAspect = LandscapeAspect)
    {
        ArgumentNullException.ThrowIfNull(tables);

        if (tables.Count == 0)
        {
            return new ErdDiagramDto(0, 0, [], []);
        }

        var nodes = tables.ToDictionary(t => t.FullName, t => CreateNode(t, keysOnly), StringComparer.OrdinalIgnoreCase);

        // 僅保留兩端皆在圖面內的關聯
        var relations = tables
            .SelectMany(t => t.ForeignKeys.Select(fk => (Child: t.FullName, Parent: fk.ReferencedFullName, Fk: fk)))
            .Where(r => nodes.ContainsKey(r.Parent))
            .ToList();

        var neighbors = nodes.Keys.ToDictionary(k => k, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        foreach (var (child, parent, _) in relations.Where(r => !r.Child.Equals(r.Parent, StringComparison.OrdinalIgnoreCase)))
        {
            neighbors[child].Add(parent);
            neighbors[parent].Add(child);
        }

        var connected = nodes.Keys.Where(k => neighbors[k].Count > 0).ToList();
        var isolated = nodes.Keys.Where(k => neighbors[k].Count == 0).Order(StringComparer.OrdinalIgnoreCase).ToList();

        var layers = AssignLayers(connected, relations);
        OrderLayers(layers, neighbors);

        // 以多組折欄高度 / 網格寬度試排，取最接近目標長寬比者 (畫面橫向、PDF 直式)
        var totalArea = nodes.Values.Sum(n => (n.Width + GapX) * (n.Height + GapY));
        var connectedArea = connected.Sum(k => (nodes[k].Width + GapX) * (nodes[k].Height + GapY));
        var tallest = nodes.Values.Max(n => n.Height) + Margin;

        Dictionary<string, (double X, double Y)>? placed = null;
        var bestScore = double.MaxValue;
        foreach (var factor in new[] { 0.5, 0.6, 0.7, 0.85, 1.0, 1.2, 1.45, 1.75, 2.1, 2.6 })
        {
            var maxColumnHeight = Math.Max(tallest, Math.Sqrt(connectedArea / targetAspect) * factor);
            var gridWidth = Math.Sqrt(totalArea * targetAspect) * factor;
            var candidate = Place(nodes, layers, isolated, maxColumnHeight, gridWidth);

            var w = candidate.Max(kv => kv.Value.X + nodes[kv.Key].Width);
            var h = candidate.Max(kv => kv.Value.Y + nodes[kv.Key].Height);
            var score = Math.Abs(Math.Log(w / h / targetAspect));
            if (score < bestScore)
            {
                bestScore = score;
                placed = candidate;
            }
        }

        var finalNodes = nodes.Select(kv => kv.Value with { X = placed![kv.Key].X, Y = placed[kv.Key].Y })
            .ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);

        var edges = BuildEdges(relations, finalNodes);

        var width = finalNodes.Values.Max(n => n.X + n.Width) + Margin;
        var height = finalNodes.Values.Max(n => n.Y + n.Height) + Margin;
        width = Math.Max(width, edges.SelectMany(e => e.Points).Select(p => p.X).DefaultIfEmpty(0).Max() + Margin);

        return new ErdDiagramDto(Math.Ceiling(width), Math.Ceiling(height), finalNodes.Values.ToList(), edges);
    }

    /// <summary>
    /// 取得欄位列於節點內的垂直中心 (相對節點 Y)；找不到時回傳標題列中心
    /// </summary>
    public static double RowCenter(ErdNodeDto node, string column)
    {
        for (var i = 0; i < node.Columns.Count; i++)
        {
            if (node.Columns[i].Name.Equals(column, StringComparison.OrdinalIgnoreCase))
            {
                return HeaderHeight + BodyPadding / 2 + i * RowHeight + RowHeight / 2;
            }
        }

        return HeaderHeight / 2;
    }

    /// <summary>
    /// 依指定折欄高度與網格寬度排列節點：關聯資料表每一層為一欄 (過高時折成多欄)，獨立資料表以網格排列於下方
    /// </summary>
    private static Dictionary<string, (double X, double Y)> Place(
        Dictionary<string, ErdNodeDto> nodes,
        List<List<string>> layers,
        List<string> isolated,
        double maxColumnHeight,
        double gridWidth)
    {
        var placed = new Dictionary<string, (double X, double Y)>(StringComparer.OrdinalIgnoreCase);

        var x = Margin;
        double connectedBottom = Margin;
        foreach (var layer in layers)
        {
            var y = Margin;
            double columnWidth = 0;
            foreach (var key in layer)
            {
                var node = nodes[key];
                if (y > Margin && y + node.Height > maxColumnHeight)
                {
                    x += columnWidth + GapX;
                    y = Margin;
                    columnWidth = 0;
                }

                placed[key] = (x, y);
                y += node.Height + GapY;
                columnWidth = Math.Max(columnWidth, node.Width);
                connectedBottom = Math.Max(connectedBottom, y);
            }

            x += columnWidth + GapX;
        }

        gridWidth = Math.Max(x - GapX, gridWidth);
        var gx = Margin;
        var gy = layers.Count > 0 ? connectedBottom + GapY : Margin;
        double rowHeight = 0;
        foreach (var key in isolated)
        {
            var node = nodes[key];
            if (gx > Margin && gx + node.Width > gridWidth)
            {
                gx = Margin;
                gy += rowHeight + GapY;
                rowHeight = 0;
            }

            placed[key] = (gx, gy);
            gx += node.Width + GapX / 2;
            rowHeight = Math.Max(rowHeight, node.Height);
        }

        return placed;
    }

    private static ErdNodeDto CreateNode(TableSchemaDto table, bool keysOnly)
    {
        var columns = table.Columns
            .Where(c => !keysOnly || c.IsPrimaryKey || c.IsForeignKey)
            .Select(c => new ErdColumnDto(c.Name, c.FullDataType, c.IsPrimaryKey, c.IsForeignKey, c.IsNullable))
            .ToList();

        var longestRow = columns.Select(c => c.Name.Length + c.DataType.Length + 6).DefaultIfEmpty(0).Max();
        var title = table.FullName.Length + 4;
        var width = Math.Clamp(Math.Max(longestRow, title) * CharWidth + 24, MinNodeWidth, MaxNodeWidth);
        var height = HeaderHeight + BodyPadding + Math.Max(1, columns.Count) * RowHeight;

        return new ErdNodeDto(table.FullName, table.Schema, table.Name, table.Description, 0, 0, Math.Ceiling(width), height, columns);
    }

    /// <summary>
    /// 分層：沒有參考其他表者為第 0 層，其餘為 1 + 所參考父表的最大層數 (循環參考時截斷)
    /// </summary>
    private static List<List<string>> AssignLayers(List<string> connected, List<(string Child, string Parent, ForeignKeySchemaDto Fk)> relations)
    {
        var parentsOf = connected.ToDictionary(k => k,
            k => relations.Where(r => r.Child.Equals(k, StringComparison.OrdinalIgnoreCase) && !r.Parent.Equals(k, StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Parent).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            StringComparer.OrdinalIgnoreCase);

        var layerOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int Resolve(string key)
        {
            if (layerOf.TryGetValue(key, out var known))
            {
                return known;
            }

            if (!visiting.Add(key))
            {
                return 0;
            }

            var layer = parentsOf.TryGetValue(key, out var parents) && parents.Count > 0
                ? parents.Max(Resolve) + 1
                : 0;
            visiting.Remove(key);
            layerOf[key] = layer;
            return layer;
        }

        foreach (var key in connected)
        {
            Resolve(key);
        }

        return layerOf.GroupBy(kv => kv.Value)
            .OrderBy(g => g.Key)
            .Select(g => g.Select(kv => kv.Key).Order(StringComparer.OrdinalIgnoreCase).ToList())
            .ToList();
    }

    /// <summary>
    /// 以相鄰層的重心 (barycenter) 排序，降低關聯線交錯
    /// </summary>
    private static void OrderLayers(List<List<string>> layers, Dictionary<string, HashSet<string>> neighbors)
    {
        for (var sweep = 0; sweep < 4; sweep++)
        {
            var position = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var layer in layers)
            {
                for (var i = 0; i < layer.Count; i++)
                {
                    position[layer[i]] = i;
                }
            }

            var range = sweep % 2 == 0
                ? Enumerable.Range(1, Math.Max(0, layers.Count - 1))
                : Enumerable.Range(0, Math.Max(0, layers.Count - 1)).Reverse();

            foreach (var index in range)
            {
                var layer = layers[index];
                var current = layer.ToDictionary(k => k, k => position[k], StringComparer.OrdinalIgnoreCase);
                layers[index] = layer
                    .OrderBy(k =>
                    {
                        var adjacent = neighbors[k].Where(n => position.ContainsKey(n) && !layer.Contains(n)).ToList();
                        return adjacent.Count == 0 ? current[k] : adjacent.Average(n => position[n]);
                    })
                    .ThenBy(k => k, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }
    }

    private static List<ErdEdgeDto> BuildEdges(
        List<(string Child, string Parent, ForeignKeySchemaDto Fk)> relations,
        Dictionary<string, ErdNodeDto> nodes)
    {
        var edges = new List<ErdEdgeDto>();
        var laneCounter = new Dictionary<int, int>();

        foreach (var (childKey, parentKey, fk) in relations)
        {
            var child = nodes[childKey];
            var parent = nodes[parentKey];
            var fkColumn = fk.Columns.Split(',')[0].Trim();
            var pkColumn = fk.ReferencedColumns.Split(',')[0].Trim();

            var y1 = child.Y + RowCenter(child, fkColumn);
            var y2 = parent.Y + RowCenter(parent, pkColumn);
            var label = $"{fk.Columns} → {fk.ReferencedFullName}.{fk.ReferencedColumns}";
            var isSelf = childKey.Equals(parentKey, StringComparison.OrdinalIgnoreCase);

            List<ErdPointDto> points;
            if (isSelf)
            {
                var right = child.X + child.Width;
                var loop = right + 22;
                points = [new(right, y1), new(loop, y1), new(loop, y2), new(right, y2)];
            }
            else if (child.X >= parent.X + parent.Width)
            {
                // 子表在右：子表左緣 → 父表右緣
                var x1 = child.X;
                var x2 = parent.X + parent.Width;
                var mid = Lane(x2, x1, laneCounter);
                points = [new(x1, y1), new(mid, y1), new(mid, y2), new(x2, y2)];
            }
            else if (child.X + child.Width <= parent.X)
            {
                // 子表在左：子表右緣 → 父表左緣
                var x1 = child.X + child.Width;
                var x2 = parent.X;
                var mid = Lane(x1, x2, laneCounter);
                points = [new(x1, y1), new(mid, y1), new(mid, y2), new(x2, y2)];
            }
            else
            {
                // 同欄 (水平重疊)：由兩者右緣繞出
                var x1 = child.X + child.Width;
                var x2 = parent.X + parent.Width;
                var key = (int)Math.Round(Math.Max(x1, x2));
                var offset = laneCounter.TryGetValue(key, out var n) ? n : 0;
                laneCounter[key] = offset + 1;
                var outer = Math.Max(x1, x2) + 20 + (offset % 6) * 8;
                points = [new(x1, y1), new(outer, y1), new(outer, y2), new(x2, y2)];
            }

            edges.Add(new ErdEdgeDto(fk.Name, childKey, parentKey, label, isSelf, points));
        }

        return edges;
    }

    /// <summary>
    /// 計算兩欄之間的垂直走線位置，同一間隙內的線錯開以避免完全重疊
    /// </summary>
    private static double Lane(double left, double right, Dictionary<int, int> laneCounter)
    {
        var key = (int)Math.Round(left);
        var index = laneCounter.TryGetValue(key, out var n) ? n : 0;
        laneCounter[key] = index + 1;

        var gap = right - left;
        var slots = Math.Max(1, (int)((gap - 24) / 7));
        var slot = index % slots;
        var start = left + 12;
        return gap <= 24 ? left + gap / 2 : start + slot * 7 + (gap - 24 - (slots - 1) * 7) / 2;
    }
}
