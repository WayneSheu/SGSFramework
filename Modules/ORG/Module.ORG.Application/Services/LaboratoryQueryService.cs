using Microsoft.EntityFrameworkCore;
using SGS.Modules.ORG.Application.Reports.Dtos;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Application.Services
{
    public class LaboratoryQueryService : ILaboratoryQueryService
    {
        private readonly ORGDbContext _context;

        public LaboratoryQueryService(ORGDbContext context)
        {
            _context = context;
        }

        public async Task<LaboratoryListReportDto> GetReportDataAsync(string operatorName,string targetLabId, LaboratoryQueryRequest request)
        {
            // 透過 ORGDbContext 進行資料庫查詢
            var items = await _context.Organizations
                .AsNoTracking()
                .Select(l => new LaboratoryItemDto
                {
                    LabCode = l.Code,
                    LabName = l.Name,
                    //Category = l.Category,
                    Location = l.Location,
                    //Manager = l.Manager,
                    Status = l.IsActive? "Active": "Inactive"
                })
                .ToListAsync();

            return new LaboratoryListReportDto
            {
                OperatorName = operatorName,
                QueryDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Details = items
            };
        }
    }
}
