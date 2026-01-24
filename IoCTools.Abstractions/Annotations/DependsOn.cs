namespace IoCTools.Abstractions.Annotations;

using System;

using Enumerations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
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
        string? memberName1 = null)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
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
        string? memberName1 = null,
        string? memberName2 = null)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
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
        string? memberName1 = null,
        string? memberName2 = null,
        string? memberName3 = null)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
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
        string? memberName1 = null,
        string? memberName2 = null,
        string? memberName3 = null,
        string? memberName4 = null)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
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
        string? memberName1 = null,
        string? memberName2 = null,
        string? memberName3 = null,
        string? memberName4 = null,
        string? memberName5 = null)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
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
        string? memberName1 = null,
        string? memberName2 = null,
        string? memberName3 = null,
        string? memberName4 = null,
        string? memberName5 = null,
        string? memberName6 = null)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
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
        string? memberName1 = null,
        string? memberName2 = null,
        string? memberName3 = null,
        string? memberName4 = null,
        string? memberName5 = null,
        string? memberName6 = null,
        string? memberName7 = null)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
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
        string? memberName1 = null,
        string? memberName2 = null,
        string? memberName3 = null,
        string? memberName4 = null,
        string? memberName5 = null,
        string? memberName6 = null,
        string? memberName7 = null,
        string? memberName8 = null)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
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
        string? memberName1 = null,
        string? memberName2 = null,
        string? memberName3 = null,
        string? memberName4 = null,
        string? memberName5 = null,
        string? memberName6 = null,
        string? memberName7 = null,
        string? memberName8 = null,
        string? memberName9 = null)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
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
        string? memberName1 = null,
        string? memberName2 = null,
        string? memberName3 = null,
        string? memberName4 = null,
        string? memberName5 = null,
        string? memberName6 = null,
        string? memberName7 = null,
        string? memberName8 = null,
        string? memberName9 = null,
        string? memberName10 = null)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
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
        string? memberName1 = null,
        string? memberName2 = null,
        string? memberName3 = null,
        string? memberName4 = null,
        string? memberName5 = null,
        string? memberName6 = null,
        string? memberName7 = null,
        string? memberName8 = null,
        string? memberName9 = null,
        string? memberName10 = null,
        string? memberName11 = null)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
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
        string? memberName1 = null,
        string? memberName2 = null,
        string? memberName3 = null,
        string? memberName4 = null,
        string? memberName5 = null,
        string? memberName6 = null,
        string? memberName7 = null,
        string? memberName8 = null,
        string? memberName9 = null,
        string? memberName10 = null,
        string? memberName11 = null,
        string? memberName12 = null)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
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
        string? memberName1 = null,
        string? memberName2 = null,
        string? memberName3 = null,
        string? memberName4 = null,
        string? memberName5 = null,
        string? memberName6 = null,
        string? memberName7 = null,
        string? memberName8 = null,
        string? memberName9 = null,
        string? memberName10 = null,
        string? memberName11 = null,
        string? memberName12 = null,
        string? memberName13 = null)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
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
        string? memberName1 = null,
        string? memberName2 = null,
        string? memberName3 = null,
        string? memberName4 = null,
        string? memberName5 = null,
        string? memberName6 = null,
        string? memberName7 = null,
        string? memberName8 = null,
        string? memberName9 = null,
        string? memberName10 = null,
        string? memberName11 = null,
        string? memberName12 = null,
        string? memberName13 = null,
        string? memberName14 = null)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
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
        string? memberName1 = null,
        string? memberName2 = null,
        string? memberName3 = null,
        string? memberName4 = null,
        string? memberName5 = null,
        string? memberName6 = null,
        string? memberName7 = null,
        string? memberName8 = null,
        string? memberName9 = null,
        string? memberName10 = null,
        string? memberName11 = null,
        string? memberName12 = null,
        string? memberName13 = null,
        string? memberName14 = null,
        string? memberName15 = null)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
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
        string? memberName1 = null,
        string? memberName2 = null,
        string? memberName3 = null,
        string? memberName4 = null,
        string? memberName5 = null,
        string? memberName6 = null,
        string? memberName7 = null,
        string? memberName8 = null,
        string? memberName9 = null,
        string? memberName10 = null,
        string? memberName11 = null,
        string? memberName12 = null,
        string? memberName13 = null,
        string? memberName14 = null,
        string? memberName15 = null,
        string? memberName16 = null)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
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
        string? memberName1 = null,
        string? memberName2 = null,
        string? memberName3 = null,
        string? memberName4 = null,
        string? memberName5 = null,
        string? memberName6 = null,
        string? memberName7 = null,
        string? memberName8 = null,
        string? memberName9 = null,
        string? memberName10 = null,
        string? memberName11 = null,
        string? memberName12 = null,
        string? memberName13 = null,
        string? memberName14 = null,
        string? memberName15 = null,
        string? memberName16 = null,
        string? memberName17 = null)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
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
        string? memberName1 = null,
        string? memberName2 = null,
        string? memberName3 = null,
        string? memberName4 = null,
        string? memberName5 = null,
        string? memberName6 = null,
        string? memberName7 = null,
        string? memberName8 = null,
        string? memberName9 = null,
        string? memberName10 = null,
        string? memberName11 = null,
        string? memberName12 = null,
        string? memberName13 = null,
        string? memberName14 = null,
        string? memberName15 = null,
        string? memberName16 = null,
        string? memberName17 = null,
        string? memberName18 = null)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
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
        string? memberName1 = null,
        string? memberName2 = null,
        string? memberName3 = null,
        string? memberName4 = null,
        string? memberName5 = null,
        string? memberName6 = null,
        string? memberName7 = null,
        string? memberName8 = null,
        string? memberName9 = null,
        string? memberName10 = null,
        string? memberName11 = null,
        string? memberName12 = null,
        string? memberName13 = null,
        string? memberName14 = null,
        string? memberName15 = null,
        string? memberName16 = null,
        string? memberName17 = null,
        string? memberName18 = null,
        string? memberName19 = null)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
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
        string? memberName1 = null,
        string? memberName2 = null,
        string? memberName3 = null,
        string? memberName4 = null,
        string? memberName5 = null,
        string? memberName6 = null,
        string? memberName7 = null,
        string? memberName8 = null,
        string? memberName9 = null,
        string? memberName10 = null,
        string? memberName11 = null,
        string? memberName12 = null,
        string? memberName13 = null,
        string? memberName14 = null,
        string? memberName15 = null,
        string? memberName16 = null,
        string? memberName17 = null,
        string? memberName18 = null,
        string? memberName19 = null,
        string? memberName20 = null)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}
