using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace OlegShilo.MoveTypeToFile
{
    /// <summary>
    /// Interaction logic for TypeToMoveSelection.xaml
    /// </summary>
    public partial class TypeToMoveSelection : Window
    {
        public IEnumerable<Parser.Result> Items { get; set; }
        public IEnumerable<Parser.Result> SelectedItem = new Parser.Result[0];

        public TypeToMoveSelection(IEnumerable<Parser.Result> items)
        {
            Items = items;
            InitializeComponent();
            DataContext = this;
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            SelectedItem = Items.Where(x=>x.Selected);
            Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
        }
    }
}
