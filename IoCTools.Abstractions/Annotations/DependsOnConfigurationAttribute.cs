namespace IoCTools.Abstractions.Annotations;

using System;

using Enumerations;

/// <summary>
///     Declares configuration dependencies at the class level, mirroring the ergonomics of [DependsOn].
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public abstract class DependsOnConfigurationAttributeBase : Attribute
{
    protected DependsOnConfigurationAttributeBase(params Type[] configurationTypes)
    {
        ConfigurationTypes = configurationTypes ?? Array.Empty<Type>();
    }

    public Type[] ConfigurationTypes { get; }
    public string?[]? ConfigurationKeys { get; set; }
    public string?[]? MemberNames { get; set; }
    public object? DefaultValue { get; set; }
    public object?[]? DefaultValues { get; set; }
    public bool Required { get; set; } = true;
    public bool[]? RequiredFlags { get; set; }
    public bool SupportsReloading { get; set; }
    public bool[]? SupportsReloadingFlags { get; set; }
    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool StripSettingsSuffix { get; set; } = true;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public sealed class DependsOnConfigurationAttribute<TValue1> : DependsOnConfigurationAttributeBase
{
    public DependsOnConfigurationAttribute(params string?[] configurationKeys) : base(typeof(TValue1))
    {
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }

    public DependsOnConfigurationAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool stripSettingsSuffix = true,
        params string?[] configurationKeys) : base(typeof(TValue1))
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        StripSettingsSuffix = stripSettingsSuffix;
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public sealed class DependsOnConfigurationAttribute<TValue1, TValue2> : DependsOnConfigurationAttributeBase
{
    public DependsOnConfigurationAttribute(params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2))
    {
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }

    public DependsOnConfigurationAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool stripSettingsSuffix = true,
        params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2))
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        StripSettingsSuffix = stripSettingsSuffix;
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public sealed class DependsOnConfigurationAttribute<TValue1, TValue2, TValue3> : DependsOnConfigurationAttributeBase
{
    public DependsOnConfigurationAttribute(params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3))
    {
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }

    public DependsOnConfigurationAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool stripSettingsSuffix = true,
        params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3))
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        StripSettingsSuffix = stripSettingsSuffix;
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public sealed class DependsOnConfigurationAttribute<TValue1, TValue2, TValue3, TValue4> :
    DependsOnConfigurationAttributeBase
{
    public DependsOnConfigurationAttribute(params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4))
    {
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }

    public DependsOnConfigurationAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool stripSettingsSuffix = true,
        params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4))
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        StripSettingsSuffix = stripSettingsSuffix;
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public sealed class DependsOnConfigurationAttribute<TValue1, TValue2, TValue3, TValue4, TValue5> :
    DependsOnConfigurationAttributeBase
{
    public DependsOnConfigurationAttribute(params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5))
    {
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }

    public DependsOnConfigurationAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool stripSettingsSuffix = true,
        params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5))
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        StripSettingsSuffix = stripSettingsSuffix;
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public sealed class DependsOnConfigurationAttribute<TValue1, TValue2, TValue3, TValue4, TValue5, TValue6> :
    DependsOnConfigurationAttributeBase
{
    public DependsOnConfigurationAttribute(params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6))
    {
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }

    public DependsOnConfigurationAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool stripSettingsSuffix = true,
        params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6))
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        StripSettingsSuffix = stripSettingsSuffix;
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public sealed class DependsOnConfigurationAttribute<TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, TValue7> :
    DependsOnConfigurationAttributeBase
{
    public DependsOnConfigurationAttribute(params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7))
    {
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }

    public DependsOnConfigurationAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool stripSettingsSuffix = true,
        params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7))
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        StripSettingsSuffix = stripSettingsSuffix;
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public sealed class DependsOnConfigurationAttribute<TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, TValue7,
    TValue8>
    : DependsOnConfigurationAttributeBase
{
    public DependsOnConfigurationAttribute(params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7), typeof(TValue8))
    {
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }

    public DependsOnConfigurationAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool stripSettingsSuffix = true,
        params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7), typeof(TValue8))
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        StripSettingsSuffix = stripSettingsSuffix;
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public sealed class DependsOnConfigurationAttribute<TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, TValue7,
    TValue8,
    TValue9> : DependsOnConfigurationAttributeBase
{
    public DependsOnConfigurationAttribute(params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7), typeof(TValue8), typeof(TValue9))
    {
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }

    public DependsOnConfigurationAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool stripSettingsSuffix = true,
        params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7), typeof(TValue8), typeof(TValue9))
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        StripSettingsSuffix = stripSettingsSuffix;
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public sealed class DependsOnConfigurationAttribute<TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, TValue7,
    TValue8,
    TValue9, TValue10> : DependsOnConfigurationAttributeBase
{
    public DependsOnConfigurationAttribute(params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7), typeof(TValue8), typeof(TValue9), typeof(TValue10))
    {
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }

    public DependsOnConfigurationAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool stripSettingsSuffix = true,
        params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7), typeof(TValue8), typeof(TValue9), typeof(TValue10))
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        StripSettingsSuffix = stripSettingsSuffix;
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public sealed class DependsOnConfigurationAttribute<TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, TValue7,
    TValue8,
    TValue9, TValue10, TValue11> : DependsOnConfigurationAttributeBase
{
    public DependsOnConfigurationAttribute(params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7), typeof(TValue8), typeof(TValue9), typeof(TValue10), typeof(TValue11))
    {
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }

    public DependsOnConfigurationAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool stripSettingsSuffix = true,
        params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7), typeof(TValue8), typeof(TValue9), typeof(TValue10), typeof(TValue11))
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        StripSettingsSuffix = stripSettingsSuffix;
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public sealed class DependsOnConfigurationAttribute<TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, TValue7,
    TValue8,
    TValue9, TValue10, TValue11, TValue12> : DependsOnConfigurationAttributeBase
{
    public DependsOnConfigurationAttribute(params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7), typeof(TValue8), typeof(TValue9), typeof(TValue10), typeof(TValue11), typeof(TValue12))
    {
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }

    public DependsOnConfigurationAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool stripSettingsSuffix = true,
        params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7), typeof(TValue8), typeof(TValue9), typeof(TValue10), typeof(TValue11), typeof(TValue12))
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        StripSettingsSuffix = stripSettingsSuffix;
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public sealed class DependsOnConfigurationAttribute<TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, TValue7,
    TValue8,
    TValue9, TValue10, TValue11, TValue12, TValue13> : DependsOnConfigurationAttributeBase
{
    public DependsOnConfigurationAttribute(params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7), typeof(TValue8), typeof(TValue9), typeof(TValue10), typeof(TValue11), typeof(TValue12),
            typeof(TValue13))
    {
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }

    public DependsOnConfigurationAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool stripSettingsSuffix = true,
        params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7), typeof(TValue8), typeof(TValue9), typeof(TValue10), typeof(TValue11), typeof(TValue12),
            typeof(TValue13))
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        StripSettingsSuffix = stripSettingsSuffix;
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public sealed class DependsOnConfigurationAttribute<TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, TValue7,
    TValue8,
    TValue9, TValue10, TValue11, TValue12, TValue13, TValue14> : DependsOnConfigurationAttributeBase
{
    public DependsOnConfigurationAttribute(params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7), typeof(TValue8), typeof(TValue9), typeof(TValue10), typeof(TValue11), typeof(TValue12),
            typeof(TValue13), typeof(TValue14))
    {
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }

    public DependsOnConfigurationAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool stripSettingsSuffix = true,
        params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7), typeof(TValue8), typeof(TValue9), typeof(TValue10), typeof(TValue11), typeof(TValue12),
            typeof(TValue13), typeof(TValue14))
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        StripSettingsSuffix = stripSettingsSuffix;
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public sealed class DependsOnConfigurationAttribute<TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, TValue7,
    TValue8,
    TValue9, TValue10, TValue11, TValue12, TValue13, TValue14, TValue15> : DependsOnConfigurationAttributeBase
{
    public DependsOnConfigurationAttribute(params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7), typeof(TValue8), typeof(TValue9), typeof(TValue10), typeof(TValue11), typeof(TValue12),
            typeof(TValue13), typeof(TValue14), typeof(TValue15))
    {
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }

    public DependsOnConfigurationAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool stripSettingsSuffix = true,
        params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7), typeof(TValue8), typeof(TValue9), typeof(TValue10), typeof(TValue11), typeof(TValue12),
            typeof(TValue13), typeof(TValue14), typeof(TValue15))
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        StripSettingsSuffix = stripSettingsSuffix;
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public sealed class DependsOnConfigurationAttribute<TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, TValue7,
    TValue8,
    TValue9, TValue10, TValue11, TValue12, TValue13, TValue14, TValue15, TValue16> : DependsOnConfigurationAttributeBase
{
    public DependsOnConfigurationAttribute(params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7), typeof(TValue8), typeof(TValue9), typeof(TValue10), typeof(TValue11), typeof(TValue12),
            typeof(TValue13), typeof(TValue14), typeof(TValue15), typeof(TValue16))
    {
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }

    public DependsOnConfigurationAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool stripSettingsSuffix = true,
        params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7), typeof(TValue8), typeof(TValue9), typeof(TValue10), typeof(TValue11), typeof(TValue12),
            typeof(TValue13), typeof(TValue14), typeof(TValue15), typeof(TValue16))
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        StripSettingsSuffix = stripSettingsSuffix;
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public sealed class DependsOnConfigurationAttribute<TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, TValue7,
    TValue8,
    TValue9, TValue10, TValue11, TValue12, TValue13, TValue14, TValue15, TValue16, TValue17> :
    DependsOnConfigurationAttributeBase
{
    public DependsOnConfigurationAttribute(params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7), typeof(TValue8), typeof(TValue9), typeof(TValue10), typeof(TValue11), typeof(TValue12),
            typeof(TValue13), typeof(TValue14), typeof(TValue15), typeof(TValue16), typeof(TValue17))
    {
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }

    public DependsOnConfigurationAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool stripSettingsSuffix = true,
        params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7), typeof(TValue8), typeof(TValue9), typeof(TValue10), typeof(TValue11), typeof(TValue12),
            typeof(TValue13), typeof(TValue14), typeof(TValue15), typeof(TValue16), typeof(TValue17))
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        StripSettingsSuffix = stripSettingsSuffix;
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public sealed class DependsOnConfigurationAttribute<TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, TValue7,
    TValue8,
    TValue9, TValue10, TValue11, TValue12, TValue13, TValue14, TValue15, TValue16, TValue17, TValue18> :
    DependsOnConfigurationAttributeBase
{
    public DependsOnConfigurationAttribute(params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7), typeof(TValue8), typeof(TValue9), typeof(TValue10), typeof(TValue11), typeof(TValue12),
            typeof(TValue13), typeof(TValue14), typeof(TValue15), typeof(TValue16), typeof(TValue17), typeof(TValue18))
    {
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }

    public DependsOnConfigurationAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool stripSettingsSuffix = true,
        params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7), typeof(TValue8), typeof(TValue9), typeof(TValue10), typeof(TValue11), typeof(TValue12),
            typeof(TValue13), typeof(TValue14), typeof(TValue15), typeof(TValue16), typeof(TValue17), typeof(TValue18))
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        StripSettingsSuffix = stripSettingsSuffix;
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public sealed class DependsOnConfigurationAttribute<TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, TValue7,
    TValue8,
    TValue9, TValue10, TValue11, TValue12, TValue13, TValue14, TValue15, TValue16, TValue17, TValue18, TValue19> :
    DependsOnConfigurationAttributeBase
{
    public DependsOnConfigurationAttribute(params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7), typeof(TValue8), typeof(TValue9), typeof(TValue10), typeof(TValue11), typeof(TValue12),
            typeof(TValue13), typeof(TValue14), typeof(TValue15), typeof(TValue16), typeof(TValue17), typeof(TValue18),
            typeof(TValue19))
    {
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }

    public DependsOnConfigurationAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool stripSettingsSuffix = true,
        params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7), typeof(TValue8), typeof(TValue9), typeof(TValue10), typeof(TValue11), typeof(TValue12),
            typeof(TValue13), typeof(TValue14), typeof(TValue15), typeof(TValue16), typeof(TValue17), typeof(TValue18),
            typeof(TValue19))
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        StripSettingsSuffix = stripSettingsSuffix;
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public sealed class DependsOnConfigurationAttribute<TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, TValue7,
    TValue8,
    TValue9, TValue10, TValue11, TValue12, TValue13, TValue14, TValue15, TValue16, TValue17, TValue18, TValue19,
    TValue20>
    : DependsOnConfigurationAttributeBase
{
    public DependsOnConfigurationAttribute(params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7), typeof(TValue8), typeof(TValue9), typeof(TValue10), typeof(TValue11), typeof(TValue12),
            typeof(TValue13), typeof(TValue14), typeof(TValue15), typeof(TValue16), typeof(TValue17), typeof(TValue18),
            typeof(TValue19), typeof(TValue20))
    {
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }

    public DependsOnConfigurationAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool stripSettingsSuffix = true,
        params string?[] configurationKeys)
        : base(typeof(TValue1), typeof(TValue2), typeof(TValue3), typeof(TValue4), typeof(TValue5), typeof(TValue6),
            typeof(TValue7), typeof(TValue8), typeof(TValue9), typeof(TValue10), typeof(TValue11), typeof(TValue12),
            typeof(TValue13), typeof(TValue14), typeof(TValue15), typeof(TValue16), typeof(TValue17), typeof(TValue18),
            typeof(TValue19), typeof(TValue20))
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        StripSettingsSuffix = stripSettingsSuffix;
        if (configurationKeys is { Length: > 0 }) ConfigurationKeys = configurationKeys;
    }
}
