namespace IoCTools.Abstractions.Annotations;

using System;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class AutoDepsApplyGlobAttribute<TProfile> : Attribute
    where TProfile : IAutoDepsProfile
{
    public AutoDepsApplyGlobAttribute(string pattern)
    {
        Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
    }

    public string Pattern { get; }
    public AutoDepScope Scope { get; set; } = AutoDepScope.Assembly;
}
