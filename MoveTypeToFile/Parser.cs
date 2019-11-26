using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// using ICSharpCode.NRefactory.CSharp;
// using SyntaxTree = ICSharpCode.NRefactory.CSharp.SyntaxTree;

namespace OlegShilo.MoveTypeToFile
{
    public static class Parser
    {
        public class TypeInfo
        {
            public string Name;
            public string Modifiers;
        }

        public class Result : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;

            public bool Success { get; set; }
            public string RawTypeDefinition { get; set; }
            public IEnumerable<TypeInfo> ParentTypes { get; set; }
            public int StartLine { get; set; }
            public int EndLine { get; set; }
            public string[] Usings { get; set; }
            public string CustomHeader { get; set; }
            public string TypeName { get; set; }
            public string Namespace { get; set; }
            bool selected;

            public bool Selected
            {
                get => selected;

                set
                {
                    selected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
                }
            }

            public string TypeDefinition
            {
                get
                {
                    var result = RawTypeDefinition.Trim();
                    if (Namespace.HasText())
                        result = "namespace " + Namespace + "\r\n{\r\n" + result + "\r\n}";
                    if (Usings?.Any() == true)
                        result = string.Join("\r\n", Usings) + "\r\n\r\n" + result;
                    if (CustomHeader?.Any() == true)
                        result = string.Join("\r\n", CustomHeader) + "\r\n\r\n" + result;
                    return result;
                }
            }

            public string DisplayName
            {
                get
                {
                    var names = new List<string>();
                    if (ParentTypes != null && ParentTypes.Any())
                        names.AddRange(ParentTypes.Select(x => x.Name));
                    names.Add(TypeName);

                    return string.Join(".", names.ToArray());
                }
            }

            public string GetParenTypesOpeningCode()
            {
                var result = new StringBuilder();

                var level = 0;
                if (Namespace.HasText())
                    level++;

                if (ParentTypes != null)

                    foreach (var item in ParentTypes)
                    {
                        var indent = new string(' ', level * 4);
                        result.AppendLine(indent + item.Modifiers + " class " + item.Name);
                        result.AppendLine(indent + "{");
                        level++;
                    }

                return result.ToString();
            }

            public string GetParenTypesClosingCode()
            {
                var result = new StringBuilder();

                int extraIndent = 0;
                if (Namespace.HasText())
                    extraIndent = 1;

                if (ParentTypes != null)
                    for (int i = ParentTypes.Count() - 1; i >= 0; i--)
                    {
                        var indent = new string(' ', (i + extraIndent) * 4);
                        result.AppendLine(indent + "}");
                    }

                return result.ToString();
            }
        }

        static int StartLineIncludingComments(this BaseTypeDeclarationSyntax declaration)
        {
            var startLine = declaration.GetLocation().GetLineSpan().StartLinePosition.Line;
            var typeWithoutComments = declaration.ToString();
            var typeWithComments = declaration.GetText().ToString();

            // -1 to exclude the current line
            var commentsLinesCount = typeWithComments.Substring(0, typeWithComments.IndexOf(typeWithoutComments)).Split('\n').Count() - 1;

            return startLine - commentsLinesCount;
        }

        static string[] Usings(this IEnumerable<SyntaxNode> nodes)
        {
            return nodes
                .OfType<UsingDirectiveSyntax>()
                .Select(x => x.GetText().ToString().Trim())
                .ToArray();
        }

        static string Namespace(this BaseTypeDeclarationSyntax declaration)
        {
            string name = null;

            var node = declaration.Parent;
            do
            {
                if (node is NamespaceDeclarationSyntax nm)
                    if (name == null)
                        name = nm.Name.ToFullString().Trim();
                    else
                        name = nm.Name.ToFullString().Trim() + "." + name;

                node = node?.Parent;
            }
            while (node != null);
            return name;
        }

        static List<TypeInfo> ParentTypes(this BaseTypeDeclarationSyntax declaration)
        {
            var name = new List<TypeInfo>();

            var node = declaration.Parent;
            do
            {
                if (node is TypeDeclarationSyntax type)
                    name.Add(new TypeInfo
                    {
                        Name = type.Identifier.ValueText,
                        Modifiers = type.Modifiers.ToString()
                    });

                node = node?.Parent;
            }
            while (node != null);
            return name;
        }

        public static Result FindTypeDeclaration(string code, int fromLine, string userDefinedHeader = "")
        {
            var tree = CSharpSyntaxTree.ParseText(code);
            var fromLineSpan = tree.GetText().Lines[fromLine].Span;
            var root = tree.GetRoot();
            var nodes = root.DescendantNodes();

            var result = nodes
                .OfType<BaseTypeDeclarationSyntax>()
                .Where(t => t.Span.IntersectsWith(fromLineSpan)) //inside of the type declaration
                .Select(x => new Result
                {
                    Namespace = x.Namespace(),
                    ParentTypes = x.ParentTypes(),
                    RawTypeDefinition = x.GetText().ToString(),
                    TypeName = x.Identifier.ValueText,
                    Usings = nodes.Usings(),
                    CustomHeader = userDefinedHeader,
                    StartLine = x.StartLineIncludingComments() + 1,
                    EndLine = x.GetLocation().GetLineSpan().EndLinePosition.Line + 1,
                    Success = true
                })
                .OrderBy(x => x.EndLine - x.StartLine)
                .FirstOrDefault() ?? new Result();

            return result;
        }

        public static IEnumerable<Result> FindTypeDeclarations(string code, string userDefinedHeader = "")
        {
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var nodes = root.DescendantNodes();

            var result = nodes.OfType<BaseTypeDeclarationSyntax>()
                              .Select(x => new Result
                              {
                                  Namespace = x.Namespace(),
                                  ParentTypes = x.ParentTypes(),
                                  RawTypeDefinition = x.GetText().ToString(),
                                  TypeName = x.Identifier.ValueText,
                                  Usings = nodes.Usings(),
                                  CustomHeader = userDefinedHeader,
                                  StartLine = x.StartLineIncludingComments() + 1,
                                  EndLine = x.GetLocation().GetLineSpan().EndLinePosition.Line + 1,
                                  Success = true
                              })
                              .OrderBy(x => x.EndLine - x.StartLine)
                              .ToArray();

            return result;
        }
    }

    public static class Extensions
    {
        public static bool HasText(this string text)
        {
            return !string.IsNullOrEmpty(text);
        }

        public static string[] GetLines(this string text)
        {
            return text.Replace(Environment.NewLine, "\n").Split('\n');
        }
    }
}