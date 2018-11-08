using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using OlegShilo.MoveTypeToFile;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;

namespace OlegShilo.VSX
{
    internal class MoveTypeToFile
    {
        internal static string TemplateFilePrompt = "//content of this file will be inserted at the top of the newly create .cs file";
        internal static string TemplateFileNamePattern = "MoveTypeToFile.template*.txt";
        internal static Lazy<string> TemplateFile = new Lazy<string>(() =>
            {
                var asm = Assembly.GetExecutingAssembly();
                return asm.Location
                          .ParentDir()
                          .ParentDir()
                          .PathCombine(TemplateFileNamePattern.Replace("*", "." + asm.GetName().Version));
            });

        internal static string GetTemplateFileLocation()
        {
            if (!File.Exists(TemplateFile.Value))
            {
                var oldTemplates = Directory.GetFiles(Path.GetDirectoryName(TemplateFile.Value), TemplateFileNamePattern);
                if (oldTemplates.Any())
                {
                    File.Copy(oldTemplates.First(), TemplateFile.Value, true);
                    foreach (var item in oldTemplates)
                        try
                        {
                            File.Delete(item);
                        }
                        catch
                        {
                        }
                }
                else
                {
                    File.WriteAllText(TemplateFile.Value, TemplateFilePrompt + @"
using System;
using System.Linq;
using System.Collections.Generic;");
                }

                ShowReleaseNotes();
            }

            return TemplateFile.Value;
        }

        static void ShowReleaseNotes()
        {
            try
            {

                System.Diagnostics.Process.Start("www.csscript.net/movetypetofile/release." + Assembly.GetExecutingAssembly().GetName().Version + ".html");
            }
            catch { }
        }

        internal static string GetUserDefinedHeader()
        {
            string content = "";
            content = File.ReadAllText(GetTemplateFileLocation()).Replace(TemplateFilePrompt, "").Trim();
            return content == "" ? content : content + Environment.NewLine;
        }

        public void Execute()
        {
            ExecuteSingle();
        }

        public void ExecuteWithSelect()
        {
            IWpfTextView textView = Global.GetTextView();

            ITextSnapshot snapshot = textView.TextSnapshot;

            if (snapshot != snapshot.TextBuffer.CurrentSnapshot)
                return;

            if (!textView.Selection.IsEmpty)
                return;

            int caretLineNumber = snapshot.GetLineNumberFromPosition(textView.Caret.Position.BufferPosition);

            string code = snapshot.GetText();

            string template = GetUserDefinedHeader();

            DTE2 dte = Global.GetDTE2();

            string file = dte.ActiveDocument.FullName;
            var declarations = Parser.FindTypeDeclarations(code);

            //remove properly named and nested classes

            var rootTypes = declarations//.Where(x => !declarations.Any(y => y.StartLine < x.StartLine && y.EndLine > x.EndLine))
                                        .Where(x => string.Compare(x.TypeName, System.IO.Path.GetFileNameWithoutExtension(file), true) != 0)
                                        .OrderBy(x => x.StartLine)
                                        .ToArray();
            if (!rootTypes.Any())
            {
                System.Windows.MessageBox.Show("Cannot find any type definitions to extract.", "'Move Type To File' Extension");
                return;
            }

            var selectedTypes = SelectTypesManually(rootTypes);

            if (!selectedTypes.Any())
                return;


            Project[] containingProjects = GetParentProject(dte, dte.ActiveDocument.FullName);

            Project project;

            if (containingProjects.Length == 1)
                project = containingProjects.First();
            else
                project = SelectProjectManually();

            if (project == null) //operation was canceled by the user
                return;


            var types = selectedTypes.OrderByDescending(x => x.StartLine);

            string newFile = null;

            foreach (var item in types)
            {
                var drclaration = Parser.FindTypeDeclaration(code, item.StartLine, template);
                newFile = WriteNewDefinition(item, project);

                if (newFile != null)
                {
                    DeleteExistingDefinition(snapshot, item);
                }
                else
                    break;
            }

            if (newFile != null)
                dte.ItemOperations.OpenFile(newFile);
        }

        public void ExecuteSingle(int line = -1, Project destProject = null, bool openWhenDone = true)
        {
            IWpfTextView textView = Global.GetTextView();

            ITextSnapshot snapshot = textView.TextSnapshot;

            if (snapshot != snapshot.TextBuffer.CurrentSnapshot)
                return;

            if (!textView.Selection.IsEmpty)
                return;

            int caretLineNumber = line;

            if (caretLineNumber == -1)
                caretLineNumber = snapshot.GetLineNumberFromPosition(textView.Caret.Position.BufferPosition);

            string code = snapshot.GetText();

            string header = GetUserDefinedHeader();

            caretLineNumber = caretLineNumber + 1; //parser is 1-based
            // Parser.Result result = Parser.FindTypeDeclarationNRefactory(code, caretLineNumber, header);
            Parser.Result result = Parser.FindTypeDeclaration(code, caretLineNumber, header);

            if (!result.Success)
            {
                System.Windows.MessageBox.Show("Cannot find any type definition.\nPlease ensure that the document has no errors and the cursor is placed inside of the type definition.",
                   "'Move Type To File' Extension");
                return;
            }

            DTE2 dte = Global.GetDTE2();
            Project project = destProject;

            if (project == null)
            {
                Project[] containingProjects = GetParentProject(dte, dte.ActiveDocument.FullName);


                if (containingProjects.Length == 1)
                    project = containingProjects.First();
                else
                    project = SelectProjectManually();
            }

            if (project == null) //operation was canceled by the user
                return;

            string file = WriteNewDefinition(result, project);

            if (file != null)
            {
                DeleteExistingDefinition(snapshot, result);
                if (openWhenDone)
                {
                    //FormatActiveDocument();
                    dte.ItemOperations.OpenFile(file);
                }
            }
        }

        static string WriteNewDefinition(Parser.Result result, Project project)
        {
            DTE2 dte = Global.GetDTE2();

            string dir = Path.GetDirectoryName(dte.ActiveDocument.FullName);

            string fileName = result.TypeName + ".cs";
            fileName = Path.Combine(dir, fileName);
            fileName = fileName.Replace("<", "")
                               .Replace(">", "");

            if (File.Exists(fileName))
            {
                string newFileName = fileName;
                for (int i = 2; i <= 10; i++)
                {
                    newFileName = Path.Combine(dir, Path.GetFileNameWithoutExtension(fileName) + "_" + i + ".cs");
                    if (!File.Exists(newFileName))
                        break;
                }

                if (File.Exists(newFileName))
                {
                    using (var dialog = new SaveFileDialog())
                    {
                        dialog.InitialDirectory = dir;
                        dialog.FileName = fileName;
                        if (dialog.ShowDialog() == DialogResult.OK)
                            newFileName = dialog.FileName;
                        else
                            return null; //user canceled everything

                    }
                }
                fileName = newFileName;
            }

            File.WriteAllText(fileName, result.TypeDefinition);

            project.ProjectItems.AddFromFile(fileName);

            return fileName;
        }

        static void DeleteExistingDefinition(ITextSnapshot snapshot, Parser.Result result)
        {
            DTE2 dte = Global.GetDTE2();
            ITextEdit edit = snapshot.TextBuffer.CreateEdit();
            try
            {
                ITextSnapshotLine currentLine;
                for (int i = result.StartLine - 1; i <= result.EndLine - 1; i++)
                {
                    currentLine = snapshot.GetLineFromLineNumber(i);
                    edit.Delete(currentLine.Start.Position, currentLine.LengthIncludingLineBreak);
                }

                //remove separating empty line if found
                if (snapshot.LineCount > result.EndLine)
                {
                    currentLine = snapshot.GetLineFromLineNumber(result.EndLine);
                    if (string.IsNullOrWhiteSpace(currentLine.GetText()))
                        edit.Delete(currentLine.Start.Position, currentLine.LengthIncludingLineBreak);
                }

                edit.Apply();
            }
            catch
            {
                edit.Cancel();
            }

            dte.ActiveDocument.Save();
        }

        internal static Project[] GetParentProject(DTE2 dte, string containedFile)
        {
            var projects = Global.GetSolutionProjects();

            var query = from p in projects
                        where p.ContainsFile(containedFile)
                        select p;

            return query.ToArray();
        }

        internal static Project SelectProjectManually()
        {
            if (Global.GetSolutionProjects().Count == 0)
            {
                System.Windows.MessageBox.Show("No projects can be identified in the solution", "Error");
                return null;
            }

            var dialog = new ProjectSelection(Global.GetSolutionProjects());

            IVsUIShell uiShell = (IVsUIShell) Global.GetService(typeof(SVsUIShell));
            IntPtr mainWnd;
            uiShell.GetDialogOwnerHwnd(out mainWnd);

            WindowInteropHelper helper = new WindowInteropHelper(dialog);
            helper.Owner = mainWnd;
            dialog.ShowDialog();

            return dialog.SelectedItem;
        }

        public static IEnumerable<Parser.Result> SelectTypesManually(IEnumerable<Parser.Result> items)
        {
            var dialog = new TypeToMoveSelection(items);

            IVsUIShell uiShell = (IVsUIShell) Global.GetService(typeof(SVsUIShell));
            IntPtr mainWnd;
            uiShell.GetDialogOwnerHwnd(out mainWnd);

            WindowInteropHelper helper = new WindowInteropHelper(dialog);
            helper.Owner = mainWnd;
            dialog.ShowDialog();

            return dialog.SelectedItem;
        }

        /// <summary>
        /// Syncs the active document.
        /// </summary>
        public void SynchActiveDocument()
        {
            try
            {
                var IDE = Global.GetDTE2();

                Document document = IDE.ActiveDocument;
                if (document != null)
                {
                    if (Global.IDEVersion >= 11)
                    {
                        IDE.ExecuteCommand("SolutionExplorer.SyncWithActiveDocument", String.Empty);
                    }
                    else
                    {
                        IDE.ExecuteCommand("View.TrackActivityinSolutionExplorer", String.Empty);
                        IDE.ExecuteCommand("View.TrackActivityinSolutionExplorer", String.Empty);
                        IDE.ExecuteCommand("View.SolutionExplorer", String.Empty);
                    }
                }
            }
            catch { }
        }

        public void FormatActiveDocument()
        {
            try
            {
                var IDE = Global.GetDTE2();

                Document document = IDE.ActiveDocument;
                if (document != null)
                {
                    document.DTE.ExecuteCommand("Edit.FormatDocument");
                    document.DTE.ExecuteCommand("Edit.RemoveAndSort");
                }
            }
            catch { }
        }
    }
}