using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OlegShilo.MoveTypeToFile
{
    /// <summary>
    /// Interaction logic for ProjectSelection.xaml
    /// </summary>
    public partial class ProjectSelection : Window
    {
        public class ProjectItem
        {
            public string Name { get; set; }

            public EnvDTE.Project Project { get; set; }
        }

        public ObservableCollection<ProjectItem> Items { get; private set; }

        public ProjectSelection()
        {
            InitializeComponent();
        }

        public ProjectSelection(IEnumerable<EnvDTE.Project> items)
        {
            Items = new ObservableCollection<ProjectItem>();

            InitializeComponent();


            foreach (EnvDTE.Project item in items)
            {
                var listItem = new ProjectItem
                    {
                        Project = item
                    };

                try
                {
                    listItem.Name = Path.GetFileNameWithoutExtension(item.FullName);
                    Items.Add(listItem);
                }
                catch
                {
                    Debug.Assert(false); //can happen
                }
            }
            DataContext = this;
        }

        private void projects_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            CaptureSelection();
            this.Close();
        }

        private void projects_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.Return)
            {
                if (e.Key == Key.Return)
                    CaptureSelection();

                this.Close();
            }
        }

        void CaptureSelection()
        {
            SelectedItem = (this.ProjectList.SelectedItem as ProjectItem).Project;
        }

        public EnvDTE.Project SelectedItem { get; private set; }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            int errStepsCount = 0;
            try
            {
                errStepsCount++;
                ProjectList.SelectedIndex = 0;
                errStepsCount++;
                this.Focus();
                errStepsCount++;
                this.ProjectList.Focus();
                errStepsCount++;
                if (this.ProjectList.Items != null && !this.ProjectList.Items.IsEmpty)
                {
                    if (this.ProjectList.Items[0] is ListBoxItem)
                        ((ListBoxItem)this.ProjectList.Items[0]).Focus();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Solution structure cannot be explored\nErrorStep=" + errStepsCount + ".\n" + ex);
            }
        }
    }
}