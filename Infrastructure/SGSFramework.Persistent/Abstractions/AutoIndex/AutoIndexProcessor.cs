using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using SGSFramework.Persistent.MSSQL;

namespace SGSFramework.Persistent.Abstractions.AutoIndex
{
    // 2. 非叢集索引處理器
    // 註：通常索引在 CreateTable 之後透過 CreateIndexOperation 處理，
    // 但若要在 CreateTable 內定義，可在此處理。
    public class AutoIndexProcessor : ICreateTableSqlProcessor
    {
        public void Process(CreateTableOperation operation, MigrationCommandListBuilder builder)
        {
            // 範例邏輯：若偵測到特定標記，可在此處記錄或生成額外語法
            // 實務上多用於處理 Table-Level 的索引定義
        }
    }
}
