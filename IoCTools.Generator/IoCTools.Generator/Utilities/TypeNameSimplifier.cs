namespace IoCTools.Generator.Utilities;

internal static class TypeNameSimplifier
{
    public static string SimplifySystemTypesForServiceRegistration(string fullyQualifiedTypeName)
    {
        if (string.IsNullOrEmpty(fullyQualifiedTypeName)) return fullyQualifiedTypeName;
        return fullyQualifiedTypeName
            .Replace("global::System.Collections.Generic.List<", "List<")
            .Replace("global::System.Collections.Generic.Dictionary<", "Dictionary<")
            .Replace("global::System.Collections.Generic.IEnumerable<", "IEnumerable<")
            .Replace("global::System.Collections.Generic.ICollection<", "ICollection<")
            .Replace("global::System.Collections.Generic.IList<", "IList<")
            .Replace("global::System.Collections.Generic.HashSet<", "HashSet<")
            // Primitive type mappings
            .Replace("global::System.String", "string")
            .Replace("global::System.Int32", "int")
            .Replace("global::System.Int64", "long")
            .Replace("global::System.Int16", "short")
            .Replace("global::System.Byte", "byte")
            .Replace("global::System.SByte", "sbyte")
            .Replace("global::System.Boolean", "bool")
            .Replace("global::System.Double", "double")
            .Replace("global::System.Single", "float")
            .Replace("global::System.Decimal", "decimal")
            .Replace("global::System.Char", "char")
            .Replace("global::System.UInt32", "uint")
            .Replace("global::System.UInt64", "ulong")
            .Replace("global::System.UInt16", "ushort")
            // Common value types
            .Replace("global::System.DateTime", "DateTime")
            .Replace("global::System.TimeSpan", "TimeSpan")
            .Replace("global::System.Guid", "Guid")
            .Replace("global::System.Object", "object")
            .Replace("global::System.Void", "void");
    }

    public static string SimplifyTypesForConditionalServices(string fullyQualifiedTypeName)
    {
        if (string.IsNullOrEmpty(fullyQualifiedTypeName)) return fullyQualifiedTypeName;
        var simplified = SimplifySystemTypesForServiceRegistration(fullyQualifiedTypeName);
        return simplified.StartsWith("global::") ? simplified.Substring("global::".Length) : simplified;
    }
}
