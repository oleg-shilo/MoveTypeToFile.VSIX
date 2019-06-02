using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using OlegShilo.VSX;
using Task = System.Threading.Tasks.Task;

namespace OlegShilo.MoveTypeToFile
{
    // [PackageRegistration(UseManagedResourcesOnly = true)]
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(GuidList.guidMoveTypeToFilePkgString)]
    public sealed class MoveTypeToFilePackage : AsyncPackage
    {
        public MoveTypeToFilePackage()
        {
            Global.GetService = GetService;
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this.ToString()));
        }

        /////////////////////////////////////////////////////////////////////////////
        // Overridden Package Implementation

        #region Package Members

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            //Global.Package = this;

            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            Register();
        }

        protected void Register()
        {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
            // base.Initialize();

            // Add our command handlers for menu (commands must exist in the .vsct file)
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null != mcs)
            {
                // Create the command for the tool window
                {
                    CommandID toolwndCommandID = new CommandID(GuidList.guidMoveTypeToFileCmdSet, (int)PkgCmdIDList.cmdidMoveToFileTool);
                    MenuCommand menuToolWin = new MenuCommand(MenuItemCallback, toolwndCommandID);
                    mcs.AddCommand(menuToolWin);
                }

                {
                    CommandID toolwndCommandID = new CommandID(GuidList.guidMoveTypeToFileCmdSet, (int)PkgCmdIDList.cmdidMoveToFileSelectTool);
                    MenuCommand menuToolWin = new MenuCommand(MenuItemSelectCallback, toolwndCommandID);
                    mcs.AddCommand(menuToolWin);
                }

                {
                    CommandID toolwndCommandID = new CommandID(GuidList.guidMoveTypeToFileConfigCmdSet, (int)PkgCmdIDList.cmdidMoveToFileConfig);
                    MenuCommand menuToolWin = new MenuCommand(ShowConfigWindow, toolwndCommandID);
                    mcs.AddCommand(menuToolWin);
                }
            }
        }

        #endregion Package Members

        /// <summary>
        /// This function is the callback used to execute a command when the a menu item is clicked.
        /// See the Initialize method to see how the menu item is associated to this function using
        /// the OleMenuCommandService service and the MenuCommand class.
        /// </summary>
        private void MenuItemCallback(object sender, EventArgs e)
        {
            try
            {
                var plugin = new VSX.MoveTypeToFile();
                plugin.SynchActiveDocument(); //must be synch first
                plugin.Execute();

                // breaks the async formatting  in `MoveToFile.FormatActiveDocument`
                // thus comment it out if formatting is desired.
                plugin.SynchActiveDocument();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void MenuItemSelectCallback(object sender, EventArgs e)
        {
            try
            {
                var plugin = new VSX.MoveTypeToFile();
                plugin.SynchActiveDocument(); //must be synch first
                plugin.ExecuteWithSelect();
                plugin.SynchActiveDocument();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void ShowConfigWindow(object sender, EventArgs e)
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