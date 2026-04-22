namespace IoCTools.Abstractions.Annotations;

using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class NoAutoDepsAttribute : Attribute
{
}
