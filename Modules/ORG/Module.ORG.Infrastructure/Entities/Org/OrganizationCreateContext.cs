using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Infrastructure.Entities.Org
{
    /// <summary>
    /// 組織/實驗室建立上下文 (Context DTO)
    /// 建立組織/實驗室實體之參數 Context 封裝物件 (解決參數過多與反射長度不匹配問題)
    /// 採用 C# 11 required + init 物件初始化運算子
    /// </summary>
    public sealed record OrganizationCreateContext
    {
        public required string Name { get; init; }
        public required string Code { get; init; }
        public int? ParentId { get; init; }
        public required string ParentNodePath { get; init; }
        public Guid? TenantLabId { get; init; }
        public string? Location { get; init; }
        public string? Description { get; init; }
    }
}
