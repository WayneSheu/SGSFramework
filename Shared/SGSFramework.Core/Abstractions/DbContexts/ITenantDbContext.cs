namespace SGSFramework.Core.Abstractions.DbContexts
{
    public interface ITenantDbContext
    {
        string Schema { get; }
    }
}
