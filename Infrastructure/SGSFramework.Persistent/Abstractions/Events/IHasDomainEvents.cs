namespace SGSFramework.Persistent.Abstractions.Events
{
    // 讓實體繼承此介面以支援事件收集
    public interface IHasDomainEvents
    {
        IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
        void ClearDomainEvents();
    }
}
