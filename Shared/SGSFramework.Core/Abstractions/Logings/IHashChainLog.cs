namespace SGSFramework.Core.Abstractions.Logings
{
    public interface IHashChainLog
    {
        long Id { get; set; }
        DateTimeOffset TimeStamp { get; set; }
        string Message { get; set; }
        string Level { get; set; }
        string? UserId { get; set; }
        string? TenantId { get; set; }
        string PrevHash { get; set; }
        string CurrentHash { get; set; }
    }
}
