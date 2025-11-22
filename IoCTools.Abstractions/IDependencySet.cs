namespace IoCTools.Abstractions;

/// <summary>
///     Marker interface that identifies a type as a dependency set. Types implementing this interface are
///     metadata-only containers for `[DependsOn]` and `[DependsOnConfiguration]` declarations; they are never
///     registered as services and are flattened into consumers that reference them via `[DependsOn<Set>]`.
/// </summary>
public interface IDependencySet
{
}
