using SGSFramework.ApiInfrastructure.DTOs.DatabaseSchemas;

namespace SGSFramework.ApiInfrastructure.Services.DatabaseSchemas;

/// <summary>
/// 資料庫結構 (SSD Table Schema) 讀取服務
/// </summary>
public interface IDatabaseSchemaService
{
    /// <summary>
    /// 自 SQL Server 系統目錄讀取資料表、欄位、索引、外鍵與異動日期
    /// </summary>
    Task<DatabaseSchemaDto> GetSchemaAsync(SchemaFilterRequest filter, CancellationToken cancellationToken = default);
}
