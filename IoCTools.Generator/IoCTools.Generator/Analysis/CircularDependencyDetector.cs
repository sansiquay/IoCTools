namespace IoCTools.Generator.Analysis;

internal static class CircularDependencyDetector
{
    /// <summary>
    ///     Detects circular dependencies in a service dependency graph.
    /// </summary>
    /// <param name="dependencyGraph">Dictionary mapping service names to their dependencies.</param>
    /// <returns>List of circular dependency paths as strings.</returns>
    public static List<string> DetectCircularDependencies(Dictionary<string, List<string>> dependencyGraph)
    {
        var cycles = new List<string>();
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();
        var circularDependencyPath = new List<string>();

        foreach (var service in dependencyGraph.Keys)
        {
            if (!visited.Contains(service))
            {
                var cycle = DetectCycleFromNode(service, dependencyGraph, visited, recursionStack, circularDependencyPath);
                if (cycle != null) cycles.Add(cycle);
            }
        }

        return cycles;
    }

    private static string? DetectCycleFromNode(
        string node,
        Dictionary<string, List<string>> dependencyGraph,
        HashSet<string> visited,
        HashSet<string> recursionStack,
        List<string> circularDependencyPath)
    {
        if (recursionStack.Contains(node))
        {
            // Found a cycle, construct the cycle path
            var cycleStart = circularDependencyPath.IndexOf(node);
            var cyclePath = circularDependencyPath.Skip(cycleStart).Concat(new[] { node });
            return string.Join(" → ", cyclePath);
        }

        if (visited.Contains(node))
            return null;

        visited.Add(node);
        recursionStack.Add(node);
        circularDependencyPath.Add(node);

        if (dependencyGraph.ContainsKey(node))
        {
            foreach (var dependency in dependencyGraph[node])
            {
                var cycle = DetectCycleFromNode(dependency, dependencyGraph, visited, recursionStack, circularDependencyPath);
                if (cycle != null) return cycle;
            }
        }

        recursionStack.Remove(node);
        circularDependencyPath.Remove(node);
        return null;
    }
}
