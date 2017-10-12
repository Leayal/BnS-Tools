using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualBasic.ApplicationServices;
using System.IO;
using Leayal;

namespace BnSDat_Automation
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            ResolveEventHandler ev = new ResolveEventHandler(AssemblyLoader.AssemblyResolve);
            AppDomain.CurrentDomain.AssemblyResolve += ev;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Controller controller = new Controller();
            controller.Run(Environment.GetCommandLineArgs());

            AppDomain.CurrentDomain.AssemblyResolve -= ev;
        }

        class Controller : WindowsFormsApplicationBase
        {
            public Controller() : base(AuthenticationMode.Windows)
            {
                this.IsSingleInstance = true;
                this.EnableVisualStyles = true;
                this.SaveMySettingsOnExit = false;
                this.ShutdownStyle = ShutdownMode.AfterMainFormCloses;
            }

            protected override void OnStartupNextInstance(StartupNextInstanceEventArgs eventArgs)
            {
                if (this.MainForm != null)
                    this.MainForm.Activate();
            }

            protected override bool OnStartup(StartupEventArgs eventArgs)
            {
                // Don't care about eventArgs
                string[] args = Environment.GetCommandLineArgs();
                if (args.Length < 2)
                {
                    this.MainForm = new MyProgressMenu();
                    this.ShowUsageHint();
                    return false;
                }
                else
                {
                    MyProgressMenu myProgressMenu = new MyProgressMenu();
                    if (myProgressMenu != null)
                    {
                        if (args[1].IsEqual("-p", true))
                            myProgressMenu.Operation = Operation.Patch;
                        else if (args[1].IsEqual("-c", true))
                            myProgressMenu.Operation = Operation.Compress;
                        else if (args[1].IsEqual("-e", true))
                            myProgressMenu.Operation = Operation.Extract;

                        switch (myProgressMenu.Operation)
                        {
                            case Operation.Patch:
                                if (args.Length < 5)
                                {
                                    this.MainForm = myProgressMenu;
                                    this.ShowUsageHint();
                                    return false;
                                }
                                else
                                {
                                    myProgressMenu.OriginalXML = args[2];
                                    myProgressMenu.OutputXML = args[3];
                                    myProgressMenu.DirectoryOfPatchcingInfo = args[4];
                                    if (args.Length >= 6 && Directory.Exists(args[5]))
                                        myProgressMenu.TemporaryFolder = args[5];
                                }
                                break;
                            case Operation.Extract:
                                if (args.Length < 4)
                                {
                                    this.MainForm = myProgressMenu;
                                    this.ShowUsageHint();
                                    return false;
                                }
                                else
                                {
                                    myProgressMenu.OriginalXML = args[2];
                                    myProgressMenu.OutputXML = args[3];
                                }
                                break;
                            case Operation.Compress:
                                if (args.Length < 4)
                                {
                                    this.MainForm = myProgressMenu;
                                    this.ShowUsageHint();
                                    return false;
                                }
                                else
                                {
                                    myProgressMenu.OriginalXML = args[2];
                                    myProgressMenu.DirectoryOfPatchcingInfo = args[3];
                                    if (args.Length >= 5 && Directory.Exists(args[4]))
                                        myProgressMenu.TemporaryFolder = args[4];
                                }
                                break;
                            default:
                                this.MainForm = myProgressMenu;
                                this.ShowUsageHint();
                                break;
                        }
                    }
                    this.MainForm = myProgressMenu;
                    return base.OnStartup(eventArgs);
                }
            }

            private void ShowUsageHint()
            {
                this.MainForm.Show();
                MessageBox.Show(this.MainForm, string.Format(Properties.Resources.UsageHint, Path.GetFileName(AppInfo.ProcessFullpath)), "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Environment.ExitCode = -1;
                Application.Exit();
            }
        }
    }
}
