using OlegShilo.MoveTypeToFile;
using System.Linq;
using Xunit;

namespace Testing
{
    public class GenericTest
    {
        [Fact]
        public void ShouldExtractType()
        {
            string code =
@"using System;
using System.Dignostics;

class MyClassA
{

}

[Description(""test"")]
class MyClassB
    : MyClassA
{

}

class MyClassC
{
    class MyNestedClass
    {

    }
}";
            //Note: line numbering is 1-based

            Parser.Result result;

            //10: 'class MyClassB' (middle of the MyClassB class declaration)
            result = Parser.FindTypeDeclaration(code, 10);
            Assert.True(result.Success);
            Assert.Equal(9, result.StartLine);
            Assert.Equal(14, result.EndLine);

            //4: 'class MyClassA' (start of class MyClassA)
            result = Parser.FindTypeDeclaration(code, 4);
            Assert.True(result.Success);
            Assert.Equal(4, result.StartLine);
            Assert.Equal(7, result.EndLine);

            //7: '}' (end of of class MyClassA)
            result = Parser.FindTypeDeclaration(code, 7);
            Assert.True(result.Success);
            Assert.Equal(4, result.StartLine);
            Assert.Equal(7, result.EndLine);

            //15: empty line between classes
            result = Parser.FindTypeDeclaration(code, 15);
            Assert.False(result.Success);

            //16: 'class MyClassC' (start of class MyClassC)
            result = Parser.FindTypeDeclaration(code, 16);
            Assert.True(result.Success);
            Assert.Equal(16, result.StartLine);
            Assert.Equal(22, result.EndLine);

            //18: 'class MyNestedClass' (start of nested MyClassC.MyNestedClass)
            result = Parser.FindTypeDeclaration(code, 18);
            Assert.True(result.Success);
            Assert.Equal(18, result.StartLine);
            Assert.Equal(21, result.EndLine);

            Assert.Equal("MyNestedClass", result.TypeName);
            Assert.Equal(@"using System;
using System.Dignostics;

class MyNestedClass
{

}
", result.TypeDefinition);
        }

        [Fact]
        public void ShouldRespectNamespaces()
        {
            string code =
@"using System;

namespace TestNS
{
    namespace TestNS_B
    {
        class MyClassA
        {

        }

        class MyClassB
        {

        }
    }
}";
            //Note: line numbering is 1-based

            Parser.Result result;

            //7: 'class MyClassB' 
            result = Parser.FindTypeDeclaration(code, 7);
            Assert.True(result.Success);
            //Assert.Equal(9, result.StartLine);
        }

        [Fact]
        public void ShouldHandleDelegates()
        {
            string code =
@"using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConsoleApplication3
{
    class Program
    {
        static void Main(string[] args)
        {
        }
    }

    delegate void Test();
    class MyClass
    {

    }

    class MyClass1
    {

    }

    class MyClass2
    {

    }
}
";
            //Note: line numbering is 1-based

            Parser.Result result;

            //7: 'class MyClassB' 
            result = Parser.FindTypeDeclaration(code, 16);
            Assert.True(result.Success);
            //Assert.Equal(9, result.StartLine);
        }


        [Fact]
        public void ShouldHandleMultipleTypes()
        {
            string code =
@"using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConsoleApplication3
{
    class Program
    {
        static void Main(string[] args)
        {
        }
    }

    delegate void Test();
    class MyClass
    {

    }

    class MyClass1
    {

    }

    class MyClass2
    {

        class MyClass2A
        {

        }   
    }
}
";
            var result = Parser.FindTypeDeclarations(code).ToArray();

            //remove nested classes 
            var rootTypes = result.ToArray()
                                   .Where(x => !result.Any(y => y.StartLine < x.StartLine && y.EndLine > x.EndLine))
                                   .ToArray();

            var dialog = new TypeToMoveSelection(result);
            dialog.ShowDialog();

            //Assert.True(result.Success);
            //Assert.Equal(9, result.StartLine);
        }
    }
}