using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace SGSFramework.Persistent.MSSQL
{
    /// <summary>
    /// 實體配置SQL處理器
    /// </summary>
    public interface ICreateTableSqlProcessor
    {
        void Process(CreateTableOperation operation, MigrationCommandListBuilder builder);
    }
}
