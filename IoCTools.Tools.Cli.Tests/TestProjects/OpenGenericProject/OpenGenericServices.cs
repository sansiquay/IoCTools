using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Logging;

namespace OpenGenericProject.Services;

public interface IOpenGenericRepository<T> where T : class
{
    T Create();
}

[Scoped]
[RegisterAsAll]
[DependsOn<ILogger<OpenGenericRepository<T>>>]
public partial class OpenGenericRepository<T> : IOpenGenericRepository<T> where T : class, new()
{
    public T Create()
    {
        _logger.LogInformation("Creating {EntityType}", typeof(T).Name);
        return new T();
    }
}

public sealed class AuditRecord
{
    public string Id { get; init; } = string.Empty;
}
