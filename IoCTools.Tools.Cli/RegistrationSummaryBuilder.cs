namespace IoCTools.Tools.Cli;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class RegistrationSummaryBuilder
{
    public static RegistrationSummary Build(string extensionFilePath)
    {
        var sourceText = File.ReadAllText(extensionFilePath);
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        var records = new List<RegistrationRecord>();
        foreach (var invocation in invocations)
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) continue;
            if (memberAccess.Expression is not IdentifierNameSyntax identifier ||
                !string.Equals(identifier.Identifier.Text, "services", StringComparison.Ordinal))
                continue;

            var methodName = GetMethodName(memberAccess.Name);
            var record = methodName switch
            {
                var name when name.StartsWith("Add", StringComparison.Ordinal) =>
                    ParseServiceInvocation(name, memberAccess.Name, invocation),
                "Configure" => ParseConfigureInvocation(memberAccess.Name, invocation),
                _ => null
            };

            if (record == null) continue;

            var conditionalParent = invocation.Ancestors().OfType<IfStatementSyntax>().FirstOrDefault();
            if (conditionalParent != null)
                record = record with
                {
                    IsConditional = true, ConditionExpression = conditionalParent.Condition.ToString()
                };

            records.Add(record);
        }

        return new RegistrationSummary(extensionFilePath, records);
    }

    private static string GetMethodName(SimpleNameSyntax nameSyntax) =>
        nameSyntax switch
        {
            GenericNameSyntax generic => generic.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            _ => nameSyntax.ToString()
        };

    private static RegistrationRecord? ParseServiceInvocation(string methodName,
        SimpleNameSyntax memberName,
        InvocationExpressionSyntax invocation)
    {
        string? serviceType = null;
        string? implementationType = null;
        var usesFactory = invocation.ArgumentList.Arguments.Any(arg => arg.Expression is LambdaExpressionSyntax);

        if (memberName is GenericNameSyntax generic)
        {
            var args = generic.TypeArgumentList.Arguments;
            if (args.Count == 1)
            {
                serviceType = args[0].ToString();
                implementationType = TryExtractFactoryType(invocation.ArgumentList.Arguments) ?? serviceType;
            }
            else if (args.Count >= 2)
            {
                serviceType = args[0].ToString();
                implementationType = args[1].ToString();
            }
        }
        else
        {
            serviceType = TryExtractTypeFromArgument(invocation.ArgumentList.Arguments.FirstOrDefault());
            implementationType =
                TryExtractTypeFromArgument(invocation.ArgumentList.Arguments.Skip(1).FirstOrDefault()) ?? serviceType;
        }

        serviceType ??= implementationType;
        implementationType ??= serviceType;

        if (serviceType == null && implementationType == null) return null;

        var lifetime = DeriveLifetime(methodName);
        return new RegistrationRecord(RegistrationKind.Service,
            methodName,
            lifetime,
            serviceType,
            implementationType,
            usesFactory,
            false,
            null,
            invocation.ToString().Trim());
    }

    private static RegistrationRecord ParseConfigureInvocation(SimpleNameSyntax memberName,
        InvocationExpressionSyntax invocation)
    {
        string? optionsType = null;
        if (memberName is GenericNameSyntax generic && generic.TypeArgumentList.Arguments.Count > 0)
            optionsType = generic.TypeArgumentList.Arguments[0].ToString();
        optionsType ??= TryExtractTypeFromArgument(invocation.ArgumentList.Arguments.FirstOrDefault());

        return new RegistrationRecord(RegistrationKind.Configuration,
            "Configure",
            null,
            optionsType,
            null,
            false,
            false,
            null,
            invocation.ToString().Trim());
    }

    private static string? DeriveLifetime(string methodName)
    {
        if (methodName.Contains("Singleton", StringComparison.OrdinalIgnoreCase)) return "Singleton";
        if (methodName.Contains("Scoped", StringComparison.OrdinalIgnoreCase)) return "Scoped";
        if (methodName.Contains("Transient", StringComparison.OrdinalIgnoreCase)) return "Transient";
        if (string.Equals(methodName, "AddHostedService", StringComparison.Ordinal)) return "HostedService";
        return null;
    }

    private static string? TryExtractFactoryType(SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        foreach (var argument in arguments)
            if (argument.Expression is LambdaExpressionSyntax lambda)
            {
                var bodyExpression = lambda.Body switch
                {
                    InvocationExpressionSyntax invocation => invocation,
                    BlockSyntax block => block.Statements.OfType<ReturnStatementSyntax>().FirstOrDefault()?.Expression
                        as InvocationExpressionSyntax,
                    _ => null
                };

                var type = ExtractTypeFromFactoryInvocation(bodyExpression);
                if (type != null) return type;
            }

        return null;
    }

    private static string? ExtractTypeFromFactoryInvocation(InvocationExpressionSyntax? invocation)
    {
        if (invocation?.Expression is MemberAccessExpressionSyntax member && member.Name is GenericNameSyntax generic &&
            generic.Identifier.Text.Contains("GetRequiredService", StringComparison.Ordinal))
            return generic.TypeArgumentList.Arguments.FirstOrDefault()?.ToString();
        return null;
    }

    private static string? TryExtractTypeFromArgument(ArgumentSyntax? argument)
    {
        if (argument == null) return null;
        return argument.Expression switch
        {
            TypeOfExpressionSyntax typeOfExpr => typeOfExpr.Type.ToString(),
            InvocationExpressionSyntax invocation => ExtractTypeFromFactoryInvocation(invocation),
            _ => null
        };
    }

    public static RegistrationSummary FilterByType(RegistrationSummary summary, string typeFilter)
    {
        if (string.IsNullOrWhiteSpace(typeFilter))
            return summary;

        var filtered = summary.Records
            .Where(r => TypeMatchesFilter(r.ServiceType, typeFilter) ||
                        TypeMatchesFilter(r.ImplementationType, typeFilter))
            .ToArray();

        return new RegistrationSummary(summary.ExtensionPath, filtered);
    }

    private static bool TypeMatchesFilter(string? typeName, string filter)
    {
        if (typeName == null)
            return false;

        // Exact match: "MyService" == "MyService"
        if (string.Equals(typeName, filter, StringComparison.Ordinal))
            return true;

        // Qualified match: "MyNamespace.MyService" ends with ".MyService"
        // This handles the case where user provides simple name but code has qualified type
        var qualifiedPattern = "." + filter;
        if (typeName.EndsWith(qualifiedPattern, StringComparison.Ordinal))
            return true;

        return false;
    }
}

internal sealed record RegistrationSummary(string ExtensionPath, IReadOnlyList<RegistrationRecord> Records);

internal sealed record RegistrationRecord(
    RegistrationKind Kind,
    string MethodName,
    string? Lifetime,
    string? ServiceType,
    string? ImplementationType,
    bool UsesFactory,
    bool IsConditional,
    string? ConditionExpression,
    string RawExpression);

internal enum RegistrationKind
{
    Service,
    Configuration
}
