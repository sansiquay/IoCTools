namespace IoCTools.Testing.Analysis;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using IoCTools.Testing.Utilities;
using Microsoft.CodeAnalysis;

/// <summary>
/// Plans fixture members for a service's constructor parameters.
/// Handles naming, collision disambiguation, and special role detection
/// (logger, options, configuration, time, validator).
/// </summary>
internal static class FixtureMemberPlanner
{
    /// <summary>
    /// Plans fixture members for all constructor parameters.
    /// </summary>
    public static ImmutableArray<FixtureMember> Plan(
        ImmutableArray<IParameterSymbol> parameters,
        CodeGeneration.LoggerProfile loggerProfile = CodeGeneration.LoggerProfile.Mock)
    {
        var members = parameters.Select(p => CreateMember(p, loggerProfile)).ToArray();

        // Detect and resolve naming collisions
        ResolveCollisions(members);

        return members.ToImmutableArray();
    }

    /// <summary>
    /// Adjusts role based on logger profile.
    /// When NullLogger profile is active, logger params produce NullLogger role.
    /// </summary>
    private static ParameterRole AdjustRoleForProfile(
        IParameterSymbol parameter,
        ParameterRole role,
        CodeGeneration.LoggerProfile loggerProfile)
    {
        if (role == ParameterRole.Logger && loggerProfile == CodeGeneration.LoggerProfile.NullLogger)
        {
            return ParameterRole.NullLogger;
        }
        return role;
    }

    private static FixtureMember CreateMember(
        IParameterSymbol parameter,
        CodeGeneration.LoggerProfile loggerProfile)
    {
        var role = DetectRole(parameter);
        role = AdjustRoleForProfile(parameter, role, loggerProfile);
        var fieldName = GetFieldName(parameter, role);
        var setupMethodName = GetSetupMethodName(parameter, role);
        var constructorArgExpression = GetConstructorArgExpression(parameter, fieldName, role);

        return new FixtureMember(
            parameter,
            role,
            fieldName,
            setupMethodName,
            constructorArgExpression);
    }

    /// <summary>
    /// Detects the special role of a constructor parameter based on its type.
    /// </summary>
    public static ParameterRole DetectRole(IParameterSymbol parameter)
    {
        var type = parameter.Type;
        var typeName = type.ToDisplayString();

        // Logger detection
        if (typeName.StartsWith("Microsoft.Extensions.Logging.ILogger<") ||
            type.Name == "ILogger" && type is INamedTypeSymbol namedLogger &&
            namedLogger.IsGenericType && namedLogger.TypeArguments.Length == 1)
        {
            return ParameterRole.Logger;
        }

        // Configuration detection
        if (typeName == "Microsoft.Extensions.Configuration.IConfiguration")
        {
            return ParameterRole.Configuration;
        }

        // Options detection
        if (typeName.StartsWith("Microsoft.Extensions.Options.IOptions<") ||
            typeName.StartsWith("Microsoft.Extensions.Options.IOptionsSnapshot<") ||
            typeName.StartsWith("Microsoft.Extensions.Options.IOptionsMonitor<"))
        {
            return ParameterRole.Options;
        }

        // TimeProvider detection
        if (typeName == "System.TimeProvider")
        {
            return ParameterRole.TimeProvider;
        }

        // FluentValidation IValidator<T> detection
        if (type is INamedTypeSymbol namedType &&
            namedType.IsGenericType &&
            namedType.TypeArguments.Length == 1 &&
            namedType.Name == "IValidator" &&
            namedType.ContainingNamespace?.ToDisplayString() == "FluentValidation")
        {
            return ParameterRole.Validator;
        }

        if (CanUseConcreteInstance(type))
        {
            return ParameterRole.ConcreteInstance;
        }

        return ParameterRole.Normal;
    }

    private static string GetFieldName(IParameterSymbol parameter, ParameterRole role)
    {
        if (role == ParameterRole.TimeProvider)
        {
            return "TimeProvider";
        }

        if (role == ParameterRole.ConcreteInstance)
            return GetStrippedTypeName(parameter.Type);

        var baseName = GetStrippedTypeName(parameter.Type);
        return $"_mock{baseName}";
    }

    private static string GetSetupMethodName(IParameterSymbol parameter, ParameterRole role)
    {
        var baseName = GetStrippedTypeName(parameter.Type);
        if (role == ParameterRole.ConcreteInstance)
        {
            return $"Configure{baseName}";
        }

        return $"Setup{baseName}";
    }

    private static string GetConstructorArgExpression(IParameterSymbol parameter, string fieldName, ParameterRole role)
    {
        if (role is ParameterRole.NullLogger or ParameterRole.TimeProvider or ParameterRole.ConcreteInstance)
        {
            // These are not Mock<T>, use field directly
            return fieldName;
        }
        return $"{fieldName}.Object";
    }

    /// <summary>
    /// Strips interface prefix and resolves generic type names to a simple name.
    /// </summary>
    private static string GetStrippedTypeName(ITypeSymbol type)
    {
        return TypeNameUtilities.GetSimpleTypeName(type);
    }

    private static bool CanUseConcreteInstance(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return false;

        if (namedType.TypeKind != TypeKind.Class || namedType.IsAbstract)
            return false;

        if (namedType.SpecialType == SpecialType.System_String)
            return false;

        return namedType.InstanceConstructors.Any(ctor =>
            ctor.Parameters.Length == 0 &&
            ctor.DeclaredAccessibility == Accessibility.Public);
    }

    private static void ResolveCollisions(FixtureMember[] members)
    {
        var indexesByOriginalField = members
            .Select((m, i) => (Member: m, Index: i))
            .GroupBy(x => x.Member.FieldName, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.Select(x => x.Index))
            .ToHashSet();

        var usedFieldNames = new HashSet<string>(StringComparer.Ordinal);
        var usedSetupNames = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < members.Length; i++)
        {
            var member = members[i];
            var param = member.Parameter;
            var fieldName = member.FieldName;
            var setupName = member.SetupMethodName;

            if (indexesByOriginalField.Contains(i))
            {
                fieldName = ResolveDisambiguatedFieldName(param, member.FieldName, member.Role)
                    ?? member.FieldName;
                setupName = ResolveDisambiguatedSetupName(param, member.SetupMethodName, member.Role)
                    ?? member.SetupMethodName;
            }

            fieldName = MakeUniqueIdentifier(fieldName, usedFieldNames, i);
            setupName = MakeUniqueIdentifier(setupName, usedSetupNames, i);

            members[i] = new FixtureMember(
                param,
                member.Role,
                fieldName,
                setupName,
                GetConstructorArgExpression(param, fieldName, member.Role));
        }
    }

    private static string? ResolveDisambiguatedFieldName(IParameterSymbol param, string currentFieldName, ParameterRole role)
    {
        // Try using the containing type's namespace to disambiguate
        var type = param.Type;
        var containingNs = type.ContainingNamespace?.ToString();
        if (!string.IsNullOrEmpty(containingNs) && containingNs != "System")
        {
            var nsParts = containingNs.Split('.');
            var lastPart = nsParts.Length > 0 ? nsParts[^1] : null;
            if (lastPart != null && !currentFieldName.Contains(lastPart))
            {
                var stripped = GetStrippedTypeName(type);
                if (role == ParameterRole.ConcreteInstance)
                    return $"{lastPart}{stripped}";

                return $"_mock{lastPart}{stripped}";
            }
        }

        // Try using the full qualified name
        var fullName = type.ToDisplayString()
            .Replace(".", "")
            .Replace("<", "")
            .Replace(">", "")
            .Replace(",", "");
        var prefix = fullName.Length > 0 && fullName[0] == 'I' && fullName.Length > 1 && char.IsUpper(fullName[1])
            ? fullName.Substring(1)
            : fullName;

        return role == ParameterRole.ConcreteInstance
            ? prefix
            : $"_mock{prefix}";
    }

    private static string? ResolveDisambiguatedSetupName(IParameterSymbol param, string currentSetupName, ParameterRole role)
    {
        var type = param.Type;
        var containingNs = type.ContainingNamespace?.ToString();
        if (!string.IsNullOrEmpty(containingNs) && containingNs != "System")
        {
            var nsParts = containingNs.Split('.');
            var lastPart = nsParts.Length > 0 ? nsParts[^1] : null;
            if (lastPart != null && !currentSetupName.Contains(lastPart))
            {
                var stripped = GetStrippedTypeName(type);
                if (role == ParameterRole.ConcreteInstance)
                    return $"Configure{lastPart}{stripped}";

                return $"Setup{lastPart}{stripped}";
            }
        }

        return null;
    }

    private static string MakeUniqueIdentifier(string candidate, HashSet<string> used, int index)
    {
        if (used.Add(candidate))
            return candidate;

        var suffix = index + 1;
        var unique = $"{candidate}_{suffix}";
        while (!used.Add(unique))
        {
            suffix++;
            unique = $"{candidate}_{suffix}";
        }

        return unique;
    }
}

/// <summary>
/// Describes the special role of a service constructor parameter.
/// </summary>
internal enum ParameterRole
{
    /// <summary>A plain dependency injected as a Mock&lt;T&gt;</summary>
    Normal,
    /// <summary>Microsoft.Extensions.Logging.ILogger&lt;T&gt;</summary>
    Logger,
    /// <summary>NullLogger&lt;T&gt; profile (not a Mock)</summary>
    NullLogger,
    /// <summary>Microsoft.Extensions.Configuration.IConfiguration</summary>
    Configuration,
    /// <summary>IOptions&lt;T&gt;, IOptionsSnapshot&lt;T&gt;, or IOptionsMonitor&lt;T&gt;</summary>
    Options,
    /// <summary>System.TimeProvider</summary>
    TimeProvider,
    /// <summary>FluentValidation.IValidator&lt;T&gt;</summary>
    Validator,
    /// <summary>Concrete class dependency passed as a mutable real instance, not a Mock&lt;T&gt;</summary>
    ConcreteInstance,
}

/// <summary>
/// Planned fixture member for a single constructor parameter.
/// </summary>
internal readonly struct FixtureMember
{
    public FixtureMember(
        IParameterSymbol parameter,
        ParameterRole role,
        string fieldName,
        string setupMethodName,
        string constructorArgExpression)
    {
        Parameter = parameter;
        Role = role;
        FieldName = fieldName;
        SetupMethodName = setupMethodName;
        ConstructorArgExpression = constructorArgExpression;
    }

    /// <summary>The original constructor parameter symbol.</summary>
    public IParameterSymbol Parameter { get; }

    /// <summary>The detected special role of this parameter.</summary>
    public ParameterRole Role { get; }

    /// <summary>The generated Mock&lt;T&gt; field name, e.g. _mockUserRepository.</summary>
    public string FieldName { get; }

    /// <summary>The generated setup helper method name, e.g. SetupUserRepository.</summary>
    public string SetupMethodName { get; }

    /// <summary>
    /// The expression used in the constructor call, e.g. _mockUserRepository.Object
    /// or TimeProvider.System.
    /// </summary>
    public string ConstructorArgExpression { get; }
}
