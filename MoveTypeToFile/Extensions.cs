using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace OlegShilo.VSX
{
    internal static class Global
    {
        static public Func<Type, object> GetService;

        public static IWpfTextViewHost GetViewHost()
        {
            object holder;
            Guid guidViewHost = DefGuidList.guidIWpfTextViewHost;
            GetUserData().GetData(ref guidViewHost, out holder);
            return (IWpfTextViewHost)holder;
        }

        public static IWpfTextView GetTextView()
        {
            return Global.GetViewHost().TextView;
        }

        public static IVsUserData GetUserData()
        {
            int mustHaveFocus = 1;//means true
            IVsTextView currentTextView;
            (GetService(typeof(SVsTextManager)) as IVsTextManager).GetActiveView(mustHaveFocus, null, out currentTextView);

            if (currentTextView is IVsUserData)
                return currentTextView as IVsUserData;
            else
                throw new ApplicationException("No text view is currently open");
        }

        public static DTE2 GetDTE2()
        {
            DTE dte = (DTE)GetService(typeof(DTE));
            DTE2 dte2 = dte as DTE2;

            if (dte2 == null)
            {
                return null;
            }

            return dte2;
        }

        public static double IDEVersion { get { return Convert.ToDouble(GetDTE2().Version, CultureInfo.InvariantCulture); } }

        public static string GetValue(this EnvDTE.Properties source, string name)
        {
            string result = string.Empty;

            if (source == null || System.String.IsNullOrEmpty(name))
                return result;

            EnvDTE.Property property = source.Item(name);
            if (property != null)
            {
                return property.Value.ToString();
            }
            return result;
        }

        public static bool ContainsFile(this Project project, string file)
        {
            try
            {
                int iterator = 0;
                var projectList = new List<ProjectItem>();

                foreach (ProjectItem item in project.ProjectItems)
                    projectList.Add(item);

                while (iterator < projectList.Count)
                {
                    var projectItem = projectList[iterator];

                    try
                    {
                        if (0 == string.Compare(file, projectItem.Properties.GetValue("FullPath"), true))
                            return true;
                    }
                    catch { }

                    foreach (ProjectItem item in projectItem.ProjectItems)
                        projectList.Add(item);

                    iterator++;
                }
            }
            catch { } //does not matter why
            return false;
        }

        public static void SetSelection(this IWpfTextView obj, int start, int length)
        {
            SnapshotPoint selectionStart = new SnapshotPoint(obj.TextSnapshot, start);
            var selectionSpan = new SnapshotSpan(selectionStart, length);

            obj.Selection.Select(selectionSpan, false);
        }

        public static void MoveCaretTo(this IWpfTextView obj, int position)
        {
            obj.Caret.MoveTo(new SnapshotPoint(obj.TextSnapshot, position));
        }

        public static ITextSnapshotLine GetLine(this IWpfTextView obj, int lineNumber)
        {
            return obj.TextSnapshot.GetLineFromLineNumber(lineNumber);
        }

        public static ITextSnapshotLine GetLineFromPosition(this IWpfTextView obj, int position)
        {
            return obj.TextSnapshot.GetLineFromPosition(position);
        }

        public static int GetCaretPosition(this IWpfTextView obj)
        {
            return obj.Caret.Position.BufferPosition;
        }

        public static bool IsCaretCloseToHorizontalEdge(this IWpfTextView textView, bool top)
        {
            var caretPos = textView.Caret.Position.BufferPosition;
            var charBounds = textView.GetTextViewLineContainingBufferPosition(caretPos)
                                     .GetCharacterBounds(caretPos);

            if (top)
                return (charBounds.Top - textView.ViewportTop) < 50;
            else
                return (textView.ViewportBottom - charBounds.Bottom) < 50;
        }

        public static ITextViewLine GetCaretLine(this IWpfTextView obj)
        {
            return obj.Caret.ContainingTextViewLine;
        }

        public static void Insert(this IWpfTextView obj, int position, string text)
        {
            ITextEdit edit = obj.TextSnapshot.TextBuffer.CreateEdit();
            edit.Insert(position, text);
            edit.Apply();
        }

        public static string GetText(this IWpfTextView obj)
        {
            return obj.TextSnapshot.GetText();
        }

        public static string ParentDir(this string path)
        {
            return Path.GetDirectoryName(path);
        }

        public static string PathCombine(this string path, string path2)
        {
            return Path.Combine(path, path2);
        }

        public static string ToWhiteSpaceString(this string obj)
        {
            StringBuilder sb = new StringBuilder();

            foreach (char c in obj.ToCharArray())
                if (c == '\n' || c == '\r' || c == '\t' || c == ' ')
                    sb.Append(c);
                else
                    sb.Append(' ');

            return sb.ToString();
        }

        //static private IEnumerable<Project> getProjectsRecursive(IEnumerable<Project> iEnumerable)
        //{
        //    foreach (var item in iEnumerable)
        //    {
        //        yield return item;
        //        //if (project.Kind == EnvDTE.Constants.vsProjectKindSolutionItems)
        //        foreach (var child in getProjectsRecursive(item.ProjectItems.OfType<Project>()))
        //        {
        //            yield return child;
        //        }
        //    }

        //}
        //public static IEnumerable<Project> EveryProject { get { return getProjectsRecursive(Global.GetDTE2().Solution.Projects.OfType<Project>()); } }
        //}

        //public static class SolutionProjects
        //{
        //public static DTE2 GetActiveIDE()
        //{
        //    // Get an instance of the currently running Visual Studio IDE.
        //    DTE2 dte2;
        //    dte2 = (DTE2)System.Runtime.InteropServices.Marshal.GetActiveObject("VisualStudio.DTE.10.0");
        //    return dte2;
        //}

        public static IList<Project> GetSolutionProjects()
        {
            Projects projects = Global.GetDTE2().Solution.Projects;
            List<Project> list = new List<Project>();
            foreach (var item in projects)
            {
                var project = item as Project;
                if (project == null)
                    continue;

                if (project.Kind == ProjectKinds.vsProjectKindSolutionFolder)
                {
                    list.AddRange(GetSolutionFolderProjects(project));
                }
                else
                {
                    list.Add(project);
                }
            }

            return list;
        }

        private static IEnumerable<Project> GetSolutionFolderProjects(Project solutionFolder)
        {
            List<Project> list = new List<Project>();
            for (var i = 1; i <= solutionFolder.ProjectItems.Count; i++)
            {
                var subProject = solutionFolder.ProjectItems.Item(i).SubProject;
                if (subProject == null)
                {
                    continue;
                }

                // If this is another solution folder, do a recursive call, otherwise add
                if (subProject.Kind == ProjectKinds.vsProjectKindSolutionFolder)
                {
                    list.AddRange(GetSolutionFolderProjects(subProject));
                }
                else
                {
                    list.Add(subProject);
                }
            }

            return list;
        }
    }
}