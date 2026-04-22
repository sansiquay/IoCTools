namespace IoCTools.Generator.Shared;

using System.Collections.Generic;
using Microsoft.CodeAnalysis;

public static partial class AutoDepsResolver
{
    /// <summary>
    /// Carries an auto-deps assembly attribute together with the assembly that declared it and
    /// whether the attribute is transitive (i.e. contributed by a referenced assembly).
    /// </summary>
    internal readonly struct EnumeratedAutoDepAttribute
    {
        public EnumeratedAutoDepAttribute(AttributeData attribute, IAssemblySymbol declaringAssembly, bool isTransitive)
        {
            Attribute = attribute;
            DeclaringAssembly = declaringAssembly;
            IsTransitive = isTransitive;
        }

        public AttributeData Attribute { get; }
        public IAssemblySymbol DeclaringAssembly { get; }
        public bool IsTransitive { get; }
    }

    /// <summary>
    /// Yields every auto-deps assembly-targeted attribute visible to <paramref name="compilation"/>.
    /// Local attributes (from the compilation's own assembly) are always yielded regardless of their
    /// <c>Scope</c>. Transitive attributes (from referenced assemblies that themselves reference
    /// <c>IoCTools.Abstractions</c>) are yielded only when <paramref name="includeTransitive"/> is
    /// <c>true</c> and their <c>Scope</c> named property equals <c>AutoDepScope.Transitive</c>.
    /// Referenced assemblies that do not transitively reference <c>IoCTools.Abstractions</c> are
    /// skipped entirely.
    /// </summary>
    internal static IEnumerable<EnumeratedAutoDepAttribute> EnumerateAutoDepAttributes(
        Compilation compilation,
        bool includeTransitive)
    {
        // Local assembly — always included, never transitive.
        foreach (var attr in compilation.Assembly.GetAttributes())
        {
            if (IsAutoDepsAttribute(attr))
            {
                yield return new EnumeratedAutoDepAttribute(attr, compilation.Assembly, isTransitive: false);
            }
        }

        if (!includeTransitive)
        {
            yield break;
        }

        foreach (var referenced in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            if (!ReferencesIoCToolsAbstractions(referenced))
            {
                continue;
            }

            foreach (var attr in referenced.GetAttributes())
            {
                if (IsAutoDepsAttribute(attr) && HasTransitiveScope(attr))
                {
                    yield return new EnumeratedAutoDepAttribute(attr, referenced, isTransitive: true);
                }
            }
        }
    }

    private static bool IsAutoDepsAttribute(AttributeData attr)
    {
        var cls = attr.AttributeClass;
        if (cls is null)
        {
            return false;
        }

        var ns = cls.ContainingNamespace?.ToDisplayString();
        if (ns != "IoCTools.Abstractions.Annotations")
        {
            return false;
        }

        var name = cls.Name;
        return name == "AutoDepAttribute"
            || name == "AutoDepOpenAttribute"
            || name == "AutoDepInAttribute"
            || name == "AutoDepsApplyAttribute"
            || name == "AutoDepsApplyGlobAttribute";
    }

    private static bool HasTransitiveScope(AttributeData attr)
    {
        foreach (var named in attr.NamedArguments)
        {
            if (named.Key != "Scope")
            {
                continue;
            }

            if (named.Value.Value is int i)
            {
                return i == 1; // AutoDepScope.Transitive underlying value
            }
        }

        return false;
    }

    private static bool ReferencesIoCToolsAbstractions(IAssemblySymbol assembly)
    {
        foreach (var module in assembly.Modules)
        {
            foreach (var referenced in module.ReferencedAssemblies)
            {
                if (referenced.Name == "IoCTools.Abstractions")
                {
                    return true;
                }
            }
        }

        return false;
    }
}
