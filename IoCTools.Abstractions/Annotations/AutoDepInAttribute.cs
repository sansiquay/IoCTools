namespace IoCTools.Abstractions.Annotations;

using System;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class AutoDepInAttribute<TProfile, T> : Attribute
    where TProfile : IAutoDepsProfile
{
    public AutoDepScope Scope { get; set; } = AutoDepScope.Assembly;
}
