namespace SGSFramework.Core.Abstractions.Entities.Base
{
    public interface IEntity<TEntityId> : IEntity
    {
        public TEntityId Id { get; set; }
    }

    public interface IEntity
    {

    }
}