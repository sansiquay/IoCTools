namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using System;
using System.Collections.Generic;
using System.Linq;

using IoCTools.Generator.Analysis;
using IoCTools.Generator.Diagnostics;
using IoCTools.Generator.Models;
using IoCTools.Generator.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class ConfigurationRedundancyValidator
{
    internal static void Validate(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        SemanticModel semanticModel)
    {
        var configs = CollectConfigurationBindings(classSymbol, semanticModel);
        if (configs.Count == 0) return;

        WarnDuplicateSections(context, classDeclaration, classSymbol, configs);
        WarnOptionsOverlappingFields(context, classDeclaration, classSymbol, configs);
        WarnMixedOptionsAndPrimitives(context, classDeclaration, classSymbol, configs);
    }

    private static List<(ConfigurationInjectionInfo Info, INamedTypeSymbol DeclaringType, int Level)>
        CollectConfigurationBindings(INamedTypeSymbol classSymbol, SemanticModel semanticModel)
    {
        var result = new List<(ConfigurationInjectionInfo, INamedTypeSymbol, int)>();
        var level = 0;
        var current = classSymbol;

        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            var configs = ConfigurationFieldAnalyzer.GetConfigurationInjectedFieldsForType(current, semanticModel);
            foreach (var cfg in configs)
                result.Add((cfg, current, level));

            current = current.BaseType;
            level++;
        }

        return result;
    }

    private static void WarnDuplicateSections(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        List<(ConfigurationInjectionInfo Info, INamedTypeSymbol DeclaringType, int Level)> configs)
    {
        var bySection = configs.GroupBy(c => c.Info.GetSectionName(), StringComparer.OrdinalIgnoreCase);

        foreach (var sectionGroup in bySection)
        {
            var byType = sectionGroup.GroupBy(c => c.Info.FieldType!,
                SymbolEqualityComparer.Default as IEqualityComparer<ITypeSymbol>);
            foreach (var group in byType)
            {
                if (group.Key is null) continue;
                var entries = group.ToList();
                if (entries.Count <= 1) continue;

                var details = string.Join(
                    ", ",
                    entries.Select(e => $"{DescribeSource(e)}"));

                var location = ResolveLocation(entries.First(), classDeclaration, classSymbol, group.Key);
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.RedundantDependencyDeclarations,
                    location,
                    group.Key.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    details,
                    classSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static void WarnOptionsOverlappingFields(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        List<(ConfigurationInjectionInfo Info, INamedTypeSymbol DeclaringType, int Level)> configs)
    {
        var optionsBindings = configs.Where(c => c.Info.IsOptionsPattern || c.Info.GeneratedField)
            .ToList();
        if (optionsBindings.Count == 0) return;

        foreach (var optionsBinding in optionsBindings)
        {
            var section = optionsBinding.Info.GetSectionName();
            var conflicting = configs
                .Where(c => !ReferenceEquals(c.Info, optionsBinding.Info))
                .Where(c => IsSectionNested(section, c.Info.GetSectionName()))
                .Where(c => !c.Info.IsOptionsPattern) // only flag options vs field/config bindings
            .ToList();

            if (!conflicting.Any()) continue;

            foreach (var conflict in conflicting)
            {
                var location = ResolveLocation(conflict, classDeclaration, classSymbol, conflict.Info.FieldType);
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.ConfigurationOverlap,
                    location,
                    section,
                    optionsBinding.Info.FieldType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    DescribeSource(conflict),
                    classSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static void WarnMixedOptionsAndPrimitives(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        List<(ConfigurationInjectionInfo Info, INamedTypeSymbol DeclaringType, int Level)> configs)
    {
        var optionsBindings = configs.Where(c => IsOptionsLike(c.Info)).ToList();
        if (!optionsBindings.Any()) return;

        foreach (var options in optionsBindings)
        {
            var section = options.Info.GetSectionName();
            var optionsTypeName = options.Info.FieldType?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                                   ?? "Options";

            var conflicts = configs
                .Where(c => !ReferenceEquals(c.Info, options.Info))
                .Where(c => IsSectionNested(section, c.Info.GetSectionName()))
                .Where(c => !IsOptionsLike(c.Info))
                .ToList();

            foreach (var conflict in conflicts)
            {
                var location = ResolveLocation(conflict, classDeclaration, classSymbol, conflict.Info.FieldType);
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.MixedOptionsAndPrimitiveBindings,
                    location,
                    section,
                    optionsTypeName,
                    classSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static bool IsSectionNested(string parent, string candidate)
    {
        if (candidate.Equals(parent, StringComparison.OrdinalIgnoreCase)) return true;
        return candidate.StartsWith(parent + ":", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOptionsLike(ConfigurationInjectionInfo info) =>
        info.IsOptionsPattern || (info.GeneratedField && !info.IsDirectValueBinding);

    private static string DescribeSource((ConfigurationInjectionInfo Info, INamedTypeSymbol DeclaringType, int Level) entry)
    {
        var source = entry.Info.GeneratedField ? "[DependsOnConfiguration]" : "[InjectConfiguration]";
        var locationTag = entry.Level == 0 ? "current" : "base";
        return $"{source} in {entry.DeclaringType.Name} ({locationTag})";
    }

    private static Location ResolveLocation((ConfigurationInjectionInfo Info, INamedTypeSymbol DeclaringType, int Level) entry,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        ITypeSymbol targetType)
    {
        if (!entry.Info.GeneratedField && !string.IsNullOrEmpty(entry.Info.FieldName))
        {
            // Try to find the user-declared field
            var field = entry.DeclaringType.GetMembers().OfType<IFieldSymbol>()
                .FirstOrDefault(f => string.Equals(f.Name, entry.Info.FieldName, StringComparison.Ordinal));
            var loc = field?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()?.GetLocation();
            if (loc != null) return loc;
        }

        // Fallback: attribute location
        var attrs = entry.DeclaringType.GetAttributes().Where(AttributeParser.IsDependsOnConfigurationAttribute)
            .Concat(entry.DeclaringType.GetAttributes().Where(attr =>
                attr.AttributeClass?.Name == "InjectConfigurationAttribute"));

        foreach (var attr in attrs)
        {
            var loc = attr.ApplicationSyntaxReference?.GetSyntax().GetLocation();
            if (loc != null) return loc;
        }

        return classDeclaration.Identifier.GetLocation();
    }
}
