using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using OlegShilo.MoveTypeToFile;

namespace TestPad
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
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

    /// <summary>
    /// Delegate Test
    /// </summary>
    delegate void Test();
    class MyClass
    {

    }

    class MyClass1
    {

    }

    /// <summary>
    /// Class MyClass2
    /// </summary>
    public class MyClass2
    {

        public class MyClass2A
        {

        }   
    }
}
";
            var file = "program.cs";
            var result = Parser.FindTypeDeclarations(code).ToArray();

            //remove properly named and nested classes 

            var rootTypes = result.ToArray()
                                  .Where(x => string.Compare(x.TypeName, System.IO.Path.GetFileNameWithoutExtension(file), true) != 0)
                                  .ToArray();


            var dilaog = new OlegShilo.MoveTypeToFile.TypeToMoveSelection(rootTypes);
            dilaog.ShowDialog();

            if (dilaog.SelectedItem.Any())
            {
                //   MessageBox.Show(string.Join("\n", dilaog.SelectedItem.Select(x => x.TypeName).ToArray()));
                var types = dilaog.SelectedItem.OrderByDescending(x => x.StartLine);

                foreach (var item in types)
                {
                    //ExecuteSingle(item.StartLine, project, false);
                    var ttt = Parser.FindTypeDeclaration(code, item.StartLine);
                }
            }
            else
                MessageBox.Show("Canceled");
        }
    }
}
