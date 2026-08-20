using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Helpers
{
    /// <summary>
    /// 提供物化路徑 (NodePath) 解析與 Level 計算之領域幫助類別
    /// </summary>
    public static class NodePathHelper
    {
        /// <summary>
        /// 定義物化路徑的分隔符號，這裡使用 '/' 作為層級分隔符
        /// </summary>
        private static readonly char[] PathSeparators = ['/'];

        /// <summary>
        /// 計算給定物化路徑的層級數量
        /// </summary>
        /// <param name="nodePath"></param>
        /// <returns></returns>
        public static int CalculateLevel(string? nodePath)
        {
            if (string.IsNullOrWhiteSpace(nodePath))
            {
                return 0;
            }

            var segments = nodePath.Split(PathSeparators, StringSplitOptions.RemoveEmptyEntries);
            return segments.Length;
        }

        /// <summary>
        /// 建構新的物化路徑，將父節點的 NodePath 與當前節點的 Id 組合起來
        /// </summary>
        /// <param name="parentNodePath"></param>
        /// <param name="currentId"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static string BuildNodePath(string? parentNodePath, int currentId)
        {
            if (currentId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(currentId), "Id 必須大於 0 方能構建 NodePath。");
            }

            if (string.IsNullOrWhiteSpace(parentNodePath))
            {
                return $"/{currentId}/";
            }

            var trimmedParent = parentNodePath.TrimEnd('/');
            return $"{trimmedParent}/{currentId}/";
        }
    }
}
