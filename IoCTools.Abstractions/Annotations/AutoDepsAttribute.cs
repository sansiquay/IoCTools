namespace IoCTools.Abstractions.Annotations;

using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class AutoDepsAttribute<TProfile> : Attribute
    where TProfile : IAutoDepsProfile
{
}
