using SGSFramework.Core.Abstractions.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Dtos
{
    public class LaboratoryDto
    {
        public int? Id { get; set; }           // 使用 int 確保索引效能

        public string? Code { get; set; } = null; // 實驗室編碼

        public int? ParentId { get; set; }    // 維持 int 以便直接關聯

        public string Name { get; set; }      // 節點名稱

        public string? Description { get; set; } // 描述信息

        
        public string? NodePath { get; set; }  // 存儲路徑 (如 "/1/5/10/")

        public int? Level { get; set; }// 樹狀層級
    }
}
