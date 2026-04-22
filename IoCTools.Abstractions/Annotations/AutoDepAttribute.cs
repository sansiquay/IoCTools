namespace IoCTools.Abstractions.Annotations;

using System;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class AutoDepAttribute<T> : Attribute
{
    public AutoDepScope Scope { get; set; } = AutoDepScope.Assembly;
}
