namespace IoCTools.Abstractions.Annotations;

using System;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class AutoDepsApplyAttribute<TProfile, TBase> : Attribute
    where TProfile : IAutoDepsProfile
{
    public AutoDepScope Scope { get; set; } = AutoDepScope.Assembly;
}
