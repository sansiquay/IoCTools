using IoCTools.Generator.Utilities;
using Xunit;

namespace IoCTools.Generator.Tests;

public class TypeHelpersTests
{
    public class IsConstructedGenericTypeSimple
    {
        [Fact]
        public void NonGenericType_ReturnsFalse()
        {
            Assert.False(TypeHelpers.IsConstructedGenericTypeSimple("String"));
            Assert.False(TypeHelpers.IsConstructedGenericTypeSimple("System.String"));
            Assert.False(TypeHelpers.IsConstructedGenericTypeSimple("MyClass"));
        }

        [Fact]
        public void SimpleGeneric_ReturnsTrue()
        {
            Assert.True(TypeHelpers.IsConstructedGenericTypeSimple("List<String>"));
            Assert.True(TypeHelpers.IsConstructedGenericTypeSimple("IService<int>"));
            Assert.True(TypeHelpers.IsConstructedGenericTypeSimple("MyClass<T>"));
        }

        [Fact]
        public void NestedGeneric_ReturnsTrue()
        {
            Assert.True(TypeHelpers.IsConstructedGenericTypeSimple("List<List<String>>"));
            Assert.True(TypeHelpers.IsConstructedGenericTypeSimple("Dictionary<String, List<int>>"));
        }

        [Fact]
        public void EmptyGeneric_ReturnsFalse()
        {
            Assert.False(TypeHelpers.IsConstructedGenericTypeSimple("MyClass<>"));
        }

        [Fact]
        public void OpenBracketsWithoutClosing_ReturnsFalse()
        {
            Assert.False(TypeHelpers.IsConstructedGenericTypeSimple("MyClass<String"));
        }

        [Fact]
        public void ClosingBracketsWithoutOpening_ReturnsFalse()
        {
            Assert.False(TypeHelpers.IsConstructedGenericTypeSimple("MyClassString>"));
        }
    }

    public class ExtractBaseTypeNameFromConstructed
    {
        [Fact]
        public void NonGenericType_ReturnsOriginal()
        {
            var result = TypeHelpers.ExtractBaseTypeNameFromConstructed("String");
            Assert.Equal("String", result);
        }

        [Fact]
        public void SimpleGenericType_ReturnsBaseName()
        {
            var result = TypeHelpers.ExtractBaseTypeNameFromConstructed("List<String>");
            Assert.Equal("List", result);
        }

        [Fact]
        public void GenericWithMultipleTypeArgs_ReturnsBaseName()
        {
            var result = TypeHelpers.ExtractBaseTypeNameFromConstructed("Dictionary<String, Int32>");
            Assert.Equal("Dictionary", result);
        }

        [Fact]
        public void NestedGeneric_ReturnsOuterBaseName()
        {
            var result = TypeHelpers.ExtractBaseTypeNameFromConstructed("List<List<String>>");
            Assert.Equal("List", result);
        }

        [Fact]
        public void NamespaceQualified_ReturnsFullNamespaceBase()
        {
            var result = TypeHelpers.ExtractBaseTypeNameFromConstructed("System.Collections.Generic.List<String>");
            Assert.Equal("System.Collections.Generic.List", result);
        }

        [Fact]
        public void GenericAtStart_ReturnsEmpty()
        {
            var result = TypeHelpers.ExtractBaseTypeNameFromConstructed("<String>");
            Assert.Equal("", result);
        }
    }

    public class ExtractBaseGenericInterface
    {
        [Fact]
        public void NonGenericType_ReturnsNull()
        {
            var result = TypeHelpers.ExtractBaseGenericInterface("String");
            Assert.Null(result);
        }

        [Fact]
        public void SimpleGeneric_ReturnsOpenGeneric()
        {
            var result = TypeHelpers.ExtractBaseGenericInterface("IService<String>");
            Assert.Equal("IService<T>", result);
        }

        [Fact]
        public void SingleTypeParameter_ReturnsT()
        {
            var result = TypeHelpers.ExtractBaseGenericInterface("List<int>");
            Assert.Equal("List<T>", result);
        }

        [Fact]
        public void MultipleTypeParameters_ReturnsT1T2Etc()
        {
            var result = TypeHelpers.ExtractBaseGenericInterface("Dictionary<String, Int32>");
            Assert.Equal("Dictionary<T, T2>", result);

            var result2 = TypeHelpers.ExtractBaseGenericInterface("Tuple<int, string, bool>");
            Assert.Equal("Tuple<T, T2, T3>", result2);
        }

        [Fact]
        public void NestedGeneric_ReturnsCorrectArity()
        {
            var result = TypeHelpers.ExtractBaseGenericInterface("IProcessor<List<String>>");
            Assert.Equal("IProcessor<T>", result);
        }

        [Fact]
        public void ComplexNested_ReturnsCorrectArity()
        {
            var result = TypeHelpers.ExtractBaseGenericInterface("IDictionary<String, List<int>>");
            Assert.Equal("IDictionary<T, T2>", result);
        }

        [Fact]
        public void EightTypeParameters_GeneratesTThroughT8()
        {
            var result = TypeHelpers.ExtractBaseGenericInterface("Tuple<A, B, C, D, E, F, G, H>");
            Assert.Equal("Tuple<T, T2, T3, T4, T5, T6, T7, T8>", result);
        }

        [Fact]
        public void EmptyGeneric_ReturnsEmptyArityGeneric()
        {
            var result = TypeHelpers.ExtractBaseGenericInterface("MyClass<>");
            // Empty type args string results in paramCount=0, which goes to else branch
            // but the for loop doesn't execute, resulting in "MyClass<>"
            Assert.Equal("MyClass<>", result);
        }

        [Fact]
        public void DeeplyNestedTypeParameters_CountsTopLevel()
        {
            var result = TypeHelpers.ExtractBaseGenericInterface("Processor<Dictionary<String, List<int>>, String>");
            Assert.Equal("Processor<T, T2>", result);
        }
    }

    public class CountTopLevelTypeParameters
    {
        [Fact]
        public void EmptyString_ReturnsZero()
        {
            var result = TypeHelpers.CountTopLevelTypeParameters("");
            Assert.Equal(0, result);
        }

        [Fact]
        public void WhitespaceOnly_ReturnsZero()
        {
            var result = TypeHelpers.CountTopLevelTypeParameters("   ");
            Assert.Equal(0, result);
        }

        [Fact]
        public void SingleParameter_ReturnsOne()
        {
            var result = TypeHelpers.CountTopLevelTypeParameters("String");
            Assert.Equal(1, result);
        }

        [Fact]
        public void TwoParameters_ReturnsTwo()
        {
            var result = TypeHelpers.CountTopLevelTypeParameters("String, Int32");
            Assert.Equal(2, result);
        }

        [Fact]
        public void ThreeParameters_ReturnsThree()
        {
            var result = TypeHelpers.CountTopLevelTypeParameters("A, B, C");
            Assert.Equal(3, result);
        }

        [Fact]
        public void NestedGenericInParameter_CountsOne()
        {
            var result = TypeHelpers.CountTopLevelTypeParameters("List<String>");
            Assert.Equal(1, result);
        }

        [Fact]
        public void NestedGenericWithMultipleParameters_CountsOne()
        {
            var result = TypeHelpers.CountTopLevelTypeParameters("Dictionary<String, Int32>");
            Assert.Equal(1, result);
        }

        [Fact]
        public void MultipleParametersWithNested_CountsCorrectly()
        {
            var result = TypeHelpers.CountTopLevelTypeParameters("List<String>, Dictionary<int, bool>");
            Assert.Equal(2, result);
        }

        [Fact]
        public void DeeplyNestedGenerics_CountsTopLevel()
        {
            var result = TypeHelpers.CountTopLevelTypeParameters("Dictionary<String, List<int>>");
            Assert.Equal(1, result);
        }

        [Fact]
        public void ComplexNested_CountsTopLevel()
        {
            // This method receives the extracted string BETWEEN angle brackets
            // So for "Tuple<A, B, C, D, E, F, G, H>", it gets "A, B, C, D, E, F, G, H"
            var result = TypeHelpers.CountTopLevelTypeParameters("A, B, C, D, E, F, G, H");
            Assert.Equal(8, result);
        }

        [Fact]
        public void NestedWithCommasInside_CountsTopLevel()
        {
            // For "Func<A, B<C, D>, E>", the method gets "A, B<C, D>, E"
            var result = TypeHelpers.CountTopLevelTypeParameters("A, B<C, D>, E");
            Assert.Equal(3, result);
        }

        [Fact]
        public void TrailingComma_DoesNotCreateExtraParameter()
        {
            var result = TypeHelpers.CountTopLevelTypeParameters("A, B, ");
            Assert.Equal(2, result);
        }

        [Fact]
        public void LeadingComma_CreatesEmptyParameter()
        {
            var result = TypeHelpers.CountTopLevelTypeParameters(", A");
            Assert.Equal(2, result);
        }

        [Fact]
        public void SpacesAroundParameters_HandledCorrectly()
        {
            var result = TypeHelpers.CountTopLevelTypeParameters(" String , Int32 ");
            Assert.Equal(2, result);
        }

        [Fact]
        public void MultipleNestedAngles_BalanceCorrectly()
        {
            // For "Func<A, Func<B, C>, D>", the method gets "A, Func<B, C>, D"
            // The nested Func<B, C> has its own angle brackets which increase/decrease depth
            var result = TypeHelpers.CountTopLevelTypeParameters("A, Func<B, C>, D");
            Assert.Equal(3, result);
        }
    }

    public class ExtractSimpleTypeNameFromFullName
    {
        [Fact]
        public void SimpleType_ReturnsTypeName()
        {
            var result = TypeHelpers.ExtractSimpleTypeNameFromFullName("System.String");
            Assert.Equal("String", result);
        }

        [Fact]
        public void NestedNamespace_ReturnsLastSegment()
        {
            var result = TypeHelpers.ExtractSimpleTypeNameFromFullName("System.Collections.Generic.List");
            Assert.Equal("List", result);
        }

        [Fact]
        public void NoNamespace_ReturnsOriginal()
        {
            var result = TypeHelpers.ExtractSimpleTypeNameFromFullName("MyClass");
            Assert.Equal("MyClass", result);
        }

        [Fact]
        public void GenericType_ReturnsSimpleTypeName()
        {
            // ExtractSimpleTypeNameFromFullName strips the generic part
            var result = TypeHelpers.ExtractSimpleTypeNameFromFullName("System.Collections.Generic.List<String>");
            Assert.Equal("List", result);
        }

        [Fact]
        public void EmptyString_ReturnsEmpty()
        {
            var result = TypeHelpers.ExtractSimpleTypeNameFromFullName("");
            Assert.Equal("", result);
        }
    }

    public class EnumerableTypeInfo
    {
        [Fact]
        public void Constructor_CreatesInstance()
        {
            var info = new TypeHelpers.EnumerableTypeInfo("String", "IEnumerable<String>");
            Assert.Equal("String", info.InnerType);
            Assert.Equal("IEnumerable<String>", info.FullEnumerableType);
        }

        [Fact]
        public void Properties_AreReadOnly()
        {
            var info = new TypeHelpers.EnumerableTypeInfo("int", "IEnumerable<int>");
            Assert.Equal("int", info.InnerType);
            Assert.Equal("IEnumerable<int>", info.FullEnumerableType);
        }

        [Fact]
        public void NullInnerType_Allowed()
        {
            var info = new TypeHelpers.EnumerableTypeInfo(null!, "IEnumerable<>");
            Assert.Null(info.InnerType);
            Assert.Equal("IEnumerable<>", info.FullEnumerableType);
        }

        [Fact]
        public void EmptyInnerType_Allowed()
        {
            var info = new TypeHelpers.EnumerableTypeInfo("", "IEnumerable<>");
            Assert.Equal("", info.InnerType);
            Assert.Equal("IEnumerable<>", info.FullEnumerableType);
        }
    }
}
