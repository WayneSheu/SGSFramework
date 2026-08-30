using SGS.Modules.ORG.Application.Reports.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Application.Services
{
    public interface ILaboratoryQueryService
    {
        Task<LaboratoryListReportDto> GetReportDataAsync(string operatorName,string targetLabId, LaboratoryQueryRequest request);
    }
}
