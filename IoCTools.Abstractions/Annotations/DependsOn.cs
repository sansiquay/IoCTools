namespace IoCTools.Abstractions.Annotations;

using System;

using Enumerations;

// Each attribute keeps the original parameterless constructor for backwards compatibility
// and exposes an advanced constructor with optional params-style member names.

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1> : Attribute
{
    public DependsOnAttribute()
    {
    }

    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false,
        params string[] memberNames)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
        MemberNames = memberNames ?? Array.Empty<string>();
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
    public string[] MemberNames { get; set; } = Array.Empty<string>();
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2> : Attribute
{
    public DependsOnAttribute()
    {
    }

    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false,
        params string[] memberNames)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
        MemberNames = memberNames ?? Array.Empty<string>();
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
    public string[] MemberNames { get; set; } = Array.Empty<string>();
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3> : Attribute
{
    public DependsOnAttribute()
    {
    }

    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false,
        params string[] memberNames)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
        MemberNames = memberNames ?? Array.Empty<string>();
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
    public string[] MemberNames { get; set; } = Array.Empty<string>();
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4> : Attribute
{
    public DependsOnAttribute()
    {
    }

    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false,
        params string[] memberNames)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
        MemberNames = memberNames ?? Array.Empty<string>();
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
    public string[] MemberNames { get; set; } = Array.Empty<string>();
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5> : Attribute
{
    public DependsOnAttribute()
    {
    }

    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false,
        params string[] memberNames)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
        MemberNames = memberNames ?? Array.Empty<string>();
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
    public string[] MemberNames { get; set; } = Array.Empty<string>();
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6> : Attribute
{
    public DependsOnAttribute()
    {
    }

    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false,
        params string[] memberNames)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
        MemberNames = memberNames ?? Array.Empty<string>();
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
    public string[] MemberNames { get; set; } = Array.Empty<string>();
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7> : Attribute
{
    public DependsOnAttribute()
    {
    }

    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false,
        params string[] memberNames)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
        MemberNames = memberNames ?? Array.Empty<string>();
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
    public string[] MemberNames { get; set; } = Array.Empty<string>();
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8> : Attribute
{
    public DependsOnAttribute()
    {
    }

    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false,
        params string[] memberNames)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
        MemberNames = memberNames ?? Array.Empty<string>();
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
    public string[] MemberNames { get; set; } = Array.Empty<string>();
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9> : Attribute
{
    public DependsOnAttribute()
    {
    }

    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false,
        params string[] memberNames)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
        MemberNames = memberNames ?? Array.Empty<string>();
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
    public string[] MemberNames { get; set; } = Array.Empty<string>();
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10> : Attribute
{
    public DependsOnAttribute()
    {
    }

    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false,
        params string[] memberNames)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
        MemberNames = memberNames ?? Array.Empty<string>();
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
    public string[] MemberNames { get; set; } = Array.Empty<string>();
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11> : Attribute
{
    public DependsOnAttribute()
    {
    }

    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false,
        params string[] memberNames)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
        MemberNames = memberNames ?? Array.Empty<string>();
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
    public string[] MemberNames { get; set; } = Array.Empty<string>();
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11, TDep12> : Attribute
{
    public DependsOnAttribute()
    {
    }

    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false,
        params string[] memberNames)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
        MemberNames = memberNames ?? Array.Empty<string>();
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
    public string[] MemberNames { get; set; } = Array.Empty<string>();
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11, TDep12, TDep13> : Attribute
{
    public DependsOnAttribute()
    {
    }

    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false,
        params string[] memberNames)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
        MemberNames = memberNames ?? Array.Empty<string>();
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
    public string[] MemberNames { get; set; } = Array.Empty<string>();
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11, TDep12, TDep13, TDep14> : Attribute
{
    public DependsOnAttribute()
    {
    }

    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false,
        params string[] memberNames)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
        MemberNames = memberNames ?? Array.Empty<string>();
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
    public string[] MemberNames { get; set; } = Array.Empty<string>();
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11, TDep12, TDep13, TDep14, TDep15> : Attribute
{
    public DependsOnAttribute()
    {
    }

    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false,
        params string[] memberNames)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
        MemberNames = memberNames ?? Array.Empty<string>();
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
    public string[] MemberNames { get; set; } = Array.Empty<string>();
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11, TDep12, TDep13, TDep14, TDep15, TDep16> : Attribute
{
    public DependsOnAttribute()
    {
    }

    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false,
        params string[] memberNames)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
        MemberNames = memberNames ?? Array.Empty<string>();
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
    public string[] MemberNames { get; set; } = Array.Empty<string>();
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11, TDep12, TDep13, TDep14, TDep15, TDep16, TDep17> : Attribute
{
    public DependsOnAttribute()
    {
    }

    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false,
        params string[] memberNames)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
        MemberNames = memberNames ?? Array.Empty<string>();
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
    public string[] MemberNames { get; set; } = Array.Empty<string>();
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11, TDep12, TDep13, TDep14, TDep15, TDep16, TDep17, TDep18> : Attribute
{
    public DependsOnAttribute()
    {
    }

    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false,
        params string[] memberNames)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
        MemberNames = memberNames ?? Array.Empty<string>();
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
    public string[] MemberNames { get; set; } = Array.Empty<string>();
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11, TDep12, TDep13, TDep14, TDep15, TDep16, TDep17, TDep18, TDep19> : Attribute
{
    public DependsOnAttribute()
    {
    }

    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false,
        params string[] memberNames)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
        MemberNames = memberNames ?? Array.Empty<string>();
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
    public string[] MemberNames { get; set; } = Array.Empty<string>();
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11, TDep12, TDep13, TDep14, TDep15, TDep16, TDep17, TDep18, TDep19, TDep20> : Attribute
{
    public DependsOnAttribute()
    {
    }

    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false,
        params string[] memberNames)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
        MemberNames = memberNames ?? Array.Empty<string>();
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
    public string[] MemberNames { get; set; } = Array.Empty<string>();
}
