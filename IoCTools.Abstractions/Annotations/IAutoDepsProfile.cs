namespace IoCTools.Abstractions.Annotations;

/// <summary>
/// Marker interface identifying a class as an auto-deps profile.
/// Profile types must implement this interface to be referenced by
/// <c>AutoDepIn</c>, <c>AutoDepsApply</c>, <c>AutoDepsApplyGlob</c>, or <c>AutoDeps</c>.
/// </summary>
public interface IAutoDepsProfile
{
}
