using ICSharpCode.NRefactory.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OlegShilo.MoveTypeToFile
{
    public static class Parser
    {
        public class TypeInfo
        {
            public string Name;
            public string Modifiers;
        }

        public class Result
        {
            public bool Success { get; set; }
            public string TypeDefinition { get; set; }
            public IEnumerable<TypeInfo> ParentTypes { get; set; }
            public int StartLine { get; set; }
            public int EndLine { get; set; }
            public string TypeName { get; set; }
            public string Namespace { get; set; }
            public bool Selected { get; set; }
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
                    for (int i = ParentTypes.Count()-1; i >= 0; i--)
                    {
                        var indent = new string(' ', (i + extraIndent) * 4);
                        result.AppendLine(indent + "}");
                    }

                return result.ToString();
            }
        }

        static bool IsDefaultUsingsStyle(SyntaxTree syntaxTree)
        {
            var firstNamespace = syntaxTree.Children.DeepAll(x => x is NamespaceDeclaration)
                                                    .Cast<NamespaceDeclaration>()
                                                    .FirstOrDefault();

            if (firstNamespace != null)
            {
                return !syntaxTree.Children.DeepAll(x => x is UsingDeclaration)
                                           .Where(x => x.StartLocation.Line > firstNamespace.StartLocation.Line)
                                           .Any();
            }
            return true;
        }


        public static Result EnsureCommentsIncluded(this Result result, SyntaxTree syntaxTree, string code, string userDefinedHeader = "")
        {
            string[] userInjectedContent = userDefinedHeader.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
            string[] codeLines = code.GetLines();

            //count all type (class) comments above the type declaration
            var potentialComments = syntaxTree.Children.DeepAll(x => true)
                                         .Where(t => t.StartLocation.Line < result.StartLine)
                                         .Select(t => new { Type = t, StartLine = t.StartLocation.Line })
                                         .OrderByDescending(t => t.StartLine)
                                         .ToArray();

            int lastCocommentLine = result.StartLine;
            foreach (var item in potentialComments)
                if (item.Type is Comment)
                    lastCocommentLine = (item.Type as Comment).StartLocation.Line;
                else
                    break;

            result.StartLine = lastCocommentLine;
            result.Success = true;


            bool isUsingsOutside = IsDefaultUsingsStyle(syntaxTree);

            var extractedCode = new StringBuilder();
            var usingsCode = new StringBuilder();

            //Note: text lines are 1- based

            foreach (AstNode item in syntaxTree.Children.DeepAll(x => x is UsingDeclaration))
                for (int i = item.EndLocation.Line - 1; i <= item.EndLocation.Line - 1; i++)
                    if (!userInjectedContent.Contains(codeLines[i].Trim()))
                        usingsCode.AppendLine(codeLines[i]);

            if (isUsingsOutside)
            {
                extractedCode.AppendLine(usingsCode.ToString());
            }

            if (result.Namespace.HasText())
            {
                extractedCode.AppendFormat("namespace {0}{1}{{{1}", result.Namespace, Environment.NewLine);
            }

            if (!isUsingsOutside)
            {
                extractedCode.AppendLine(usingsCode.ToString());
            }

            extractedCode.AppendLine(result.GetParenTypesOpeningCode());

            for (int i = result.StartLine - 1; i <= result.EndLine - 1; i++)
                extractedCode.AppendLine(codeLines[i]);

            extractedCode.AppendLine(result.GetParenTypesClosingCode());

            if (result.Namespace.HasText())
                extractedCode.Append("}");


            if (!string.IsNullOrEmpty(userDefinedHeader))
                result.TypeDefinition = userDefinedHeader.Trim() + Environment.NewLine + extractedCode.ToString();
            else
                result.TypeDefinition = extractedCode.ToString();

            return result;
        }

        public static Result FindTypeDeclaration(string code, int fromLine, string userDefinedHeader = "")
        {
            var syntaxTree = new CSharpParser().Parse(code, "demo.cs");

            var result = syntaxTree.Children
                                   .DeepAll(x => x.NodeType == NodeType.TypeDeclaration)
                                   .OfType<EntityDeclaration>()
                                   .Where(t => t.StartLocation.Line <= fromLine && t.EndLocation.Line >= fromLine) //inside of the type declaration
                                   .Select(t => new { Type = t, Size = t.EndLocation.Line - t.StartLocation.Line })
                                   .OrderBy(x => x.Size)
                                   .Select(x => new Result
                                   {
                                       Namespace = x.Type.GetNamespace(),
                                       ParentTypes = x.Type.GetParentTypes(),
                                       TypeName = x.Type.Name,
                                       StartLine = x.Type.StartLocation.Line,
                                       EndLine = x.Type.EndLocation.Line,
                                       Success = true
                                   })
                                   .Select(x => x.EnsureCommentsIncluded(syntaxTree, code, userDefinedHeader))
                                   .FirstOrDefault() ?? new Result();
            return result;
        }

        ////////////////////////////
        public static IEnumerable<Result> FindTypeDeclarations(string code, string userDefinedHeader = "")
        {
            var syntaxTree = new CSharpParser().Parse(code, "demo.cs");

            var result = syntaxTree.Children
                                   .DeepAll(x => x.NodeType == NodeType.TypeDeclaration)
                                   .OfType<EntityDeclaration>()
                                   .Select(t => new Result
                                   {
                                       Namespace = t.GetNamespace(),
                                       ParentTypes = t.GetParentTypes(),
                                       TypeName = t.Name,
                                       StartLine = t.StartLocation.Line,
                                       EndLine = t.EndLocation.Line,
                                       Success = true
                                   })
                                   .Select(x => x.EnsureCommentsIncluded(syntaxTree, code))
                                   .ToArray();
            if (result.Any())
            {
                return result;
            }
            return new Result[0];
        }
        //////////////////////////////
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

        public static string GetNamespace(this EntityDeclaration node)
        {
            string result = "";

            var parent = node.Parent;
            while (parent != null)
            {
                if (parent is NamespaceDeclaration)
                {
                    if (result.HasText())
                        result += ".";
                    result += (parent as NamespaceDeclaration).Name;
                }
                parent = parent.Parent;
            }

            return result;
        }

        public static List<Parser.TypeInfo> GetParentTypes(this EntityDeclaration node)
        {
            var result = new List<Parser.TypeInfo>();

            var parent = node.Parent as TypeDeclaration;
            while (parent != null)
            {
                result.Insert(0, new Parser.TypeInfo
                {
                    Name = parent.Name,
                    Modifiers = parent.Modifiers.ToString().ToLower().Replace(", ", " ") //"Partial, Public"
                });
                parent = parent.Parent as TypeDeclaration;
            }

            return result;
        }

        public static IEnumerable<AstNode> DeepAll(this IEnumerable<AstNode> collection, Func<AstNode, bool> selector)
        {
            //pseudo recursion
            var result = new List<AstNode>();
            var queue = new Queue<AstNode>(collection);

            while (queue.Count > 0)
            {
                AstNode node = queue.Dequeue();
                if (selector(node))
                    result.Add(node);

                foreach (var subNode in node.Children)
                {
                    queue.Enqueue(subNode);
                }
            }

            return result;
        }
    }
}