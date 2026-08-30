using QuestPDF.Infrastructure;
using SGS.Modules.ORG.Application.Reports.Dtos;
using SGSFramework.ReportEngine.Abstractions;
using SGSFramework.ReportEngine.Generators;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Application.Reports.Generators
{
    /// <summary>
    /// 實驗室列表報表產生器
    /// </summary>
    public class LaboratoryListReportGenerator : TableReportGenerator<LaboratoryListReportDto, LaboratoryItemDto>
    {
        public LaboratoryListReportGenerator(IEnumerable<LaboratoryItemDto> dataSource)
            : base(GetColumnDefinitions(), dataSource)
        {
        }

        private static List<ColumnDefine> GetColumnDefinitions()
        {
            return new List<ColumnDefine>
            {
                new ColumnDefine("實驗室代碼", nameof(LaboratoryItemDto.LabCode), 1.5f, true, HorizontalAlignment.Left),
                new ColumnDefine("實驗室名稱", nameof(LaboratoryItemDto.LabName), 3.0f, true, HorizontalAlignment.Left),
                new ColumnDefine("類別", nameof(LaboratoryItemDto.Category), 1.5f, true, HorizontalAlignment.Center),
                new ColumnDefine("位置", nameof(LaboratoryItemDto.Location), 2.0f, true, HorizontalAlignment.Left),
                new ColumnDefine("負責人", nameof(LaboratoryItemDto.Manager), 1.5f, true, HorizontalAlignment.Center),
                new ColumnDefine("狀態", nameof(LaboratoryItemDto.Status), 1.0f, true, HorizontalAlignment.Center)
            };
        }
    }
}
