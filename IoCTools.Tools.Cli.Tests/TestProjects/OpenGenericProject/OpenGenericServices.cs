using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace OpenGenericProject.Services;

public interface IOpenGenericRepository<T> where T : class
{
    T? GetById(int id);
}

public interface IOpenGenericLookup<T> where T : class
{
    IEnumerable<T> GetAll();
}

[Scoped]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
public partial class OpenGenericRepository<T> : IOpenGenericRepository<T>, IOpenGenericLookup<T> where T : class
{
    public T? GetById(int id) => default;

    public IEnumerable<T> GetAll() => Array.Empty<T>();
}

public sealed class AuditRecord
{
    public string Id { get; init; } = string.Empty;
}
