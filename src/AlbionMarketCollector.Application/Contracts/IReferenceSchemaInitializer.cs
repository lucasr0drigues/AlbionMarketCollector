namespace AlbionMarketCollector.Application.Contracts;

public interface IReferenceSchemaInitializer
{
    Task EnsureInitializedAsync(CancellationToken cancellationToken);
}
