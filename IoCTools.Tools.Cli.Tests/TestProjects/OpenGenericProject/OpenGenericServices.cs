using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace OpenGenericProject.Services;

public interface IOpenGenericRepository<T> where T : class
{
    T Create();
}

[Scoped]
[RegisterAsAll]
public partial class OpenGenericRepository<T> : IOpenGenericRepository<T> where T : class, new()
{
    public T Create() => new();
}

public sealed class AuditRecord
{
    public string Id { get; init; } = string.Empty;
}
