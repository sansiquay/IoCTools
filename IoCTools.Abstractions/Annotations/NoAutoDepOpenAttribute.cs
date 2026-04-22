namespace IoCTools.Abstractions.Annotations;

using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class NoAutoDepOpenAttribute : Attribute
{
    public NoAutoDepOpenAttribute(Type unboundGenericType)
    {
        UnboundGenericType = unboundGenericType ?? throw new ArgumentNullException(nameof(unboundGenericType));
    }

    public Type UnboundGenericType { get; }
}
