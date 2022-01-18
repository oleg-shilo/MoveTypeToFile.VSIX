using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Task = System.Threading.Tasks.Task;

namespace MoveTypeToFile
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class MoveTypeToFileCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int MoveCommandId = 0x0100;

        public const int MoveWithSelectCommandId = 0x0102;
        public const int ConfigCommandId = 0x0101;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("75d825ea-771c-4f0b-9714-c56ee218dcb5");

        public static readonly Guid ConfigCommandSet = new Guid("c3099c8a-d492-4fd2-8b5a-dd89ba8804e8");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="MoveTypeToFileCommand"/> class. Adds our
        /// command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private MoveTypeToFileCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, MoveCommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);

            menuCommandID = new CommandID(CommandSet, MoveWithSelectCommandId);
            menuItem = new MenuCommand(this.ExecuteWithSelect, menuCommandID);
            commandService.AddCommand(menuItem);

            menuCommandID = new CommandID(ConfigCommandSet, ConfigCommandId);
            menuItem = new MenuCommand(this.ShowConfigWindow, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static MoveTypeToFileCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in MoveTypeToFileCommand's
            // constructor requires the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new MoveTypeToFileCommand(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var plugin = new OlegShilo.VSX.MoveTypeToFile();
            plugin.SynchActiveDocument(); //must be synch first
            plugin.Execute();

            // breaks the async formatting in `MoveToFile.FormatActiveDocument` thus comment it out
            // if formatting is desired.
            plugin.SynchActiveDocument();
        }

        void ExecuteWithSelect(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var plugin = new OlegShilo.VSX.MoveTypeToFile();
            plugin.SynchActiveDocument(); //must be synch first
            plugin.ExecuteWithSelect();
            plugin.SynchActiveDocument();
        }

        void ShowConfigWindow(object sender, EventArgs e)
        {
            try
            {
                // Get the instance number 0 of this tool window. This window is single instance so this instance
                // is actually the only one.
                // The last flag is set to true so that if the tool window does not exists it will be created.
                //ToolWindowPane window = this.FindToolWindow(typeof(MyToolWindow), 0, true);

                //---------------------------------

                Process.Start(OlegShilo.VSX.MoveTypeToFile.GetTemplateFileLocation());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}