// ==========================================
// 檔案路徑: Infrastructure/SGSFramework.AuditLog/Helpers/PermissionAuditHelper.cs
// 架構層級: Audit Log Helper
// ==========================================

namespace SGSFramework.AuditLog.Helpers;

using System;
using System.Collections.Generic;

public static class PermissionAuditHelper
{
    /// <summary>
    /// 計算舊與新 Bitmask 之間的差異 (新增位元與刪除位元)
    /// </summary>
    public static (long AddedMask, long RemovedMask) CalculateBitmaskDiff(long oldMask, long newMask)
    {
        long changed = oldMask ^ newMask;
        long added = changed & newMask;
        long removed = changed & oldMask;
        return (added, removed);
    }

    /// <summary>
    /// 將位元遮罩轉換為包含的 Bit 位元列表 (例如 5 -> [0, 2])
    /// </summary>
    public static List<int> GetBitPositions(long mask)
    {
        var positions = new List<int>();
        for (int i = 0; i < 64; i++)
        {
            if ((mask & (1L << i)) != 0)
            {
                positions.Add(i);
            }
        }
        return positions;
    }

    /// <summary>
    /// 根據定義的 Bit Flags 解析出可讀的變更摘要文字
    /// </summary>
    public static string FormatPermissionChanges(long oldMask, long newMask, IDictionary<long, string>? actionMap = null)
    {
        var (addedMask, removedMask) = CalculateBitmaskDiff(oldMask, newMask);
        var changes = new List<string>();

        // 預設常見位元定義 (若專案有特定 Bit 映射表可由 actionMap 傳入)
        var defaultMap = actionMap ?? new Dictionary<long, string>
        {
            { 1L << 0, "檢視(Read)" },
            { 1L << 1, "新增(Create)" },
            { 1L << 2, "修改(Edit)" },
            { 1L << 3, "刪除(Delete)" },
            { 1L << 4, "匯出(Export)" },
            { 1L << 5, "審核(Approve)" }
        };

        foreach (var (flag, name) in defaultMap)
        {
            if ((addedMask & flag) != 0)
            {
                changes.Add($"[+] 授予: {name}");
            }
            if ((removedMask & flag) != 0)
            {
                changes.Add($"[-] 移除: {name}");
            }
        }

        if (changes.Count == 0)
        {
            var addedPositions = GetBitPositions(addedMask);
            var removedPositions = GetBitPositions(removedMask);
            if (addedPositions.Count > 0) changes.Add($"[+] 授予位元: {string.Join(',', addedPositions)}");
            if (removedPositions.Count > 0) changes.Add($"[-] 移除位元: {string.Join(',', removedPositions)}");
        }

        return string.Join(" | ", changes);
    }
}