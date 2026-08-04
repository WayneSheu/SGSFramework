namespace SGSFramework.Core.Abstractions.Entities.Tenants
{
    public interface ITenantEntity
    {
        // 這裡可以不定義屬性，改用陰影屬性（Shadow Property）以保持 Domain Entity 乾淨
        string TenantId { get; set; }
    }
}
