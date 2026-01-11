namespace Product.Common.Entities;

public interface IBaseEntity<TKey>
    where TKey : new()
{
    TKey Id { get; set; }
}
