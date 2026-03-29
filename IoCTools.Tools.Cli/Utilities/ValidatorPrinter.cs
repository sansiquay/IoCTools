namespace IoCTools.Tools.Cli;

using System.Text.Json;

/// <summary>
/// Prints validator inspection results with colored lifetime output.
/// </summary>
internal static class ValidatorPrinter
{
    public static void WriteList(IReadOnlyList<ValidatorInfo> validators, string? filter, OutputContext output)
    {
        var filtered = validators;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            filtered = validators
                .Where(v => TypeFilterUtility.Matches(v.FullName, filter!) ||
                            TypeFilterUtility.Matches(v.ModelType, filter!))
                .ToList();
        }

        if (output.IsJson)
        {
            var payload = filtered.Select(v => new
            {
                validator = v.FullName,
                modelType = v.ModelType,
                lifetime = v.Lifetime,
                hasComposition = v.HasCompositionEdges,
                compositionEdges = v.CompositionEdges.Select(e => new
                {
                    childValidator = e.ChildValidatorType,
                    method = e.CompositionMethod,
                    isDirect = e.IsDirect
                })
            });
            output.WriteJson(payload);
            return;
        }

        if (filtered.Count == 0)
        {
            output.WriteLine("No validators found.");
            return;
        }

        output.WriteLine($"Validators: {filtered.Count}");
        output.WriteLine(string.Empty);

        foreach (var v in filtered)
        {
            var lifetime = v.Lifetime != null ? AnsiColor.Lifetime(v.Lifetime) : AnsiColor.Gray("(none)");
            var composition = v.HasCompositionEdges ? $" ({v.CompositionEdges.Count} composition edges)" : "";
            output.WriteLine($"  [{lifetime}] {v.FullName} -> {v.ModelType}{composition}");
        }
    }

    public static void WriteGraph(IReadOnlyList<ValidatorInfo> validators, OutputContext output)
    {
        var tree = ValidatorInspector.BuildCompositionTree(validators);

        if (output.IsJson)
        {
            var payload = tree.Select(BuildJsonNode);
            output.WriteJson(payload);
            return;
        }

        if (tree.Count == 0)
        {
            output.WriteLine("No validators found.");
            return;
        }

        foreach (var root in tree)
        {
            PrintTreeNode(root, output, "", true);
        }
    }

    public static void WriteWhy(string validatorName, IReadOnlyList<ValidatorInfo> validators, OutputContext output)
    {
        var explanation = ValidatorInspector.TraceLifetime(validatorName, validators);

        if (output.IsJson)
        {
            output.WriteJson(new { validator = validatorName, explanation });
            return;
        }

        output.WriteLine(explanation);
    }

    private static void PrintTreeNode(ValidatorTreeNode node, OutputContext output, string indent, bool isLast)
    {
        var lifetime = node.Validator.Lifetime != null
            ? AnsiColor.Lifetime(node.Validator.Lifetime)
            : AnsiColor.Gray("(none)");

        var name = node.Validator.FullName.Split('.').Last();
        output.WriteLine($"{indent}{name} [{lifetime}] -> {node.Validator.ModelType}");

        for (var i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            var isChildLast = i == node.Children.Count - 1;
            var connector = isChildLast ? "\u2514\u2500\u2500 " : "\u251c\u2500\u2500 ";
            var childIndent = indent + (isChildLast ? "    " : "\u2502   ");

            var directWarning = child.Edge.IsDirect ? AnsiColor.Red(" (direct instantiation)") : " (injected)";
            var method = child.Edge.CompositionMethod;

            if (child.Resolved != null)
            {
                var childLifetime = child.Resolved.Validator.Lifetime != null
                    ? AnsiColor.Lifetime(child.Resolved.Validator.Lifetime)
                    : AnsiColor.Gray("(none)");
                var childName = child.Resolved.Validator.FullName.Split('.').Last();
                output.WriteLine(
                    $"{indent}{connector}{childName} [{childLifetime}] -> {child.Resolved.Validator.ModelType} (via {method}{directWarning})");

                // Recurse into child's children
                for (var j = 0; j < child.Resolved.Children.Count; j++)
                {
                    var grandChild = child.Resolved.Children[j];
                    var isGrandChildLast = j == child.Resolved.Children.Count - 1;
                    PrintChildEdge(grandChild, output, childIndent, isGrandChildLast);
                }
            }
            else
            {
                output.WriteLine(
                    $"{indent}{connector}{child.Edge.ChildValidatorType} [?] (via {method}{directWarning})");
            }
        }
    }

    private static void PrintChildEdge(ValidatorChildNode child, OutputContext output, string indent, bool isLast)
    {
        var connector = isLast ? "\u2514\u2500\u2500 " : "\u251c\u2500\u2500 ";
        var childIndent = indent + (isLast ? "    " : "\u2502   ");
        var directWarning = child.Edge.IsDirect ? AnsiColor.Red(" (direct instantiation)") : " (injected)";
        var method = child.Edge.CompositionMethod;

        if (child.Resolved != null)
        {
            var childLifetime = child.Resolved.Validator.Lifetime != null
                ? AnsiColor.Lifetime(child.Resolved.Validator.Lifetime)
                : AnsiColor.Gray("(none)");
            var childName = child.Resolved.Validator.FullName.Split('.').Last();
            output.WriteLine(
                $"{indent}{connector}{childName} [{childLifetime}] -> {child.Resolved.Validator.ModelType} (via {method}{directWarning})");

            for (var j = 0; j < child.Resolved.Children.Count; j++)
            {
                var grandChild = child.Resolved.Children[j];
                var isGrandChildLast = j == child.Resolved.Children.Count - 1;
                PrintChildEdge(grandChild, output, childIndent, isGrandChildLast);
            }
        }
        else
        {
            output.WriteLine(
                $"{indent}{connector}{child.Edge.ChildValidatorType} [?] (via {method}{directWarning})");
        }
    }

    private static object BuildJsonNode(ValidatorTreeNode node)
    {
        return new
        {
            validator = node.Validator.FullName,
            modelType = node.Validator.ModelType,
            lifetime = node.Validator.Lifetime,
            children = node.Children.Select(c => new
            {
                childValidator = c.Edge.ChildValidatorType,
                method = c.Edge.CompositionMethod,
                isDirect = c.Edge.IsDirect,
                resolved = c.Resolved != null ? BuildJsonNode(c.Resolved) : null
            })
        };
    }
}
