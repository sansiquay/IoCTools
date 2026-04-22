namespace IoCTools.Abstractions.Annotations;

using System;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
[Obsolete("Use [DependsOn<T>] instead. See docs/migration.md#migrating-from-15x-to-16x. A code fix is available (IOC095).")]
public sealed class InjectAttribute : Attribute
{
}
