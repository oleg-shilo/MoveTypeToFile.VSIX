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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;

namespace OlegShilo.VSX
{
    public class Config
    {
        public bool ShowFormattingWarning { get; set; } = true;

        public void Save()
            => File.WriteAllText(MoveTypeToFile.ConfigFile.Value, this.ToJson());

        public static Config Load()
             => File.ReadAllText(MoveTypeToFile.ConfigFile.Value).FromJson<Config>();
    }

    class MoveTypeToFile
    {
        internal static Lazy<string> ConfigFile = new Lazy<string>(() =>
            {
                var file = Assembly.GetExecutingAssembly()
                             .Location
                             .ParentDir()
                             .ParentDir()
                             .PathCombine("MoveTypeToFile.config.txt");

                if (!File.Exists(file))
                {
                    File.WriteAllText(file, new Config().ToJson());
                }

                return file;
            });

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

            DTE2 dte = Global.GetDTE2();

            string file = dte.ActiveDocument.FullName;
            var declarations = Parser.FindTypeDeclarations(code);

            //remove properly named and nested classes

            var rootTypes = declarations.Where(x => string.Compare(x.TypeName, System.IO.Path.GetFileNameWithoutExtension(file), true) != 0)
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

            var newFiles = new List<string>();

            var dialog = new ProgressDialog();
            dialog.Run = () =>
                        {
                            int i = 0;
                            foreach (var item in types)
                            {
                                newFile = WriteNewDefinition(item, project);
                                newFiles.Add(newFile);

                                if (newFile != null)
                                    dialog.Dispatcher.Invoke(() =>
                                        DeleteExistingDefinition(snapshot, item, deleteSeparationLines: false));
                                else
                                    break;

                                dialog.OnProgress(++i, types.Count(), Path.GetFileName(newFile));
                            }

                            dialog.Dispatcher.Invoke(dialog.Close);
                        };

            ShowDialog(dialog);

            if (Config.Load().ShowFormattingWarning)
                new MsgBox().ShowDialog();

            if (newFiles.Count() == 1)
            {
                dte.ItemOperations.OpenFile(newFile);
                var IDE = Global.GetDTE2();

                foreach (var item in newFiles)
                {
                    Document document = IDE.ActiveDocument;
                    while (document == null || document.FullName != newFile)
                    {
                        System.Threading.Thread.Sleep(200);
                    }
                    FormatActiveDocument();
                }
            }
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

            Parser.Result result = Parser.FindTypeDeclaration(code, caretLineNumber);

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
                    dte.ItemOperations.OpenFile(file);
                    Task.Run(() =>
                    {
                        System.Threading.Thread.Sleep(1300);
                        FormatActiveDocument();
                    });
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

        static void DeleteExistingDefinition(ITextSnapshot snapshot, Parser.Result result, bool deleteSeparationLines = true)
        {
            DTE2 dte = Global.GetDTE2();
            ITextEdit edit = snapshot.TextBuffer.CreateEdit();
            try
            {
                ITextSnapshotLine currentLine;
                for (int i = result.StartLine - 1; i <= result.EndLine - 1; i++)
                {
                    currentLine = snapshot.GetLineFromLineNumber(i);
                    var lineText = currentLine.GetText();
                    edit.Delete(currentLine.Start.Position, currentLine.LengthIncludingLineBreak);
                }

                //remove separating empty line if found
                if (deleteSeparationLines)
                    if (snapshot.LineCount > result.EndLine)
                    {
                        currentLine = snapshot.GetLineFromLineNumber(result.EndLine);
                        var lineText = currentLine.GetText();
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

            IVsUIShell uiShell = (IVsUIShell)Global.GetService(typeof(SVsUIShell));
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

            IVsUIShell uiShell = (IVsUIShell)Global.GetService(typeof(SVsUIShell));
            IntPtr mainWnd;
            uiShell.GetDialogOwnerHwnd(out mainWnd);

            WindowInteropHelper helper = new WindowInteropHelper(dialog);
            helper.Owner = mainWnd;
            dialog.ShowDialog();

            return dialog.SelectedItem;
        }

        public static void ShowDialog(System.Windows.Window dialog)
        {
            IVsUIShell uiShell = (IVsUIShell)Global.GetService(typeof(SVsUIShell));
            IntPtr mainWnd;
            uiShell.GetDialogOwnerHwnd(out mainWnd);

            var helper = new WindowInteropHelper(dialog);
            helper.Owner = mainWnd;
            dialog.ShowDialog();
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
                    var dte = (DTE)Global.GetService(typeof(DTE));

                    dte.ExecuteCommand("Edit.FormatDocument");
                    dte.ExecuteCommand("Edit.RemoveAndSort");
                }
            }
            catch { }
        }
    }
}