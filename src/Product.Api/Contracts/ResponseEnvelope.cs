namespace Product.Api.Contracts;

public record ResponseEnvelope<T>(T Data, object? Meta = null);
