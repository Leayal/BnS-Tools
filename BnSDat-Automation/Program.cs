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
                        if (args[1].IsEqual("/p", true))
                            myProgressMenu.Operation = Operation.Patch;
                        else if (args[1].IsEqual("/c", true))
                            myProgressMenu.Operation = Operation.Compress;
                        else if (args[1].IsEqual("/e", true))
                            myProgressMenu.Operation = Operation.Extract;

                        List<string> orphanArgs = new List<string>(5);

                        string switch_o = null,
                            switch_tmp = null;

                        for (int i = 2; i < args.Length; i++)
                        {
                            if (args[i].IsEqual("-o", true))
                            {
                                if ((i + 1) < args.Length)
                                    switch_o = args[++i];
                            }
                            if (args[i].IsEqual("-temp", true))
                            {
                                if ((i + 1) < args.Length)
                                    switch_tmp = args[++i];
                            }
                            else if (args[i].IsEqual("-64", true))
                            {
                                myProgressMenu.Is64 = true;
                            }
                            else
                                orphanArgs.Add(args[i]);
                        }

                        switch (myProgressMenu.Operation)
                        {
                            case Operation.Patch:
                                if (orphanArgs.Count < 2)
                                {
                                    this.MainForm = myProgressMenu;
                                    this.ShowUsageHint();
                                    return false;
                                }
                                else
                                {
                                    myProgressMenu.OriginalXML = orphanArgs[0];
                                    myProgressMenu.DirectoryOfPatchcingInfo = orphanArgs[1];

                                    if (!string.IsNullOrWhiteSpace(switch_o) && !Directory.Exists(switch_o))
                                        myProgressMenu.OutputXML = switch_o;
                                    else
                                        myProgressMenu.OutputXML = myProgressMenu.OriginalXML + ".modded";

                                    if (!string.IsNullOrWhiteSpace(switch_tmp) && Directory.Exists(switch_tmp))
                                        myProgressMenu.TemporaryFolder = switch_tmp;
                                }
                                break;
                            case Operation.Extract:
                                if (orphanArgs.Count < 1)
                                {
                                    this.MainForm = myProgressMenu;
                                    this.ShowUsageHint();
                                    return false;
                                }
                                else
                                {
                                    myProgressMenu.OriginalXML = orphanArgs[0];
                                    if (!string.IsNullOrWhiteSpace(switch_o) && !File.Exists(switch_o))
                                    {
                                        myProgressMenu.OutputXML = switch_o;
                                    }
                                    else
                                    {
                                        string outputPath = myProgressMenu.OriginalXML + ".files";
                                        if (File.Exists(outputPath))
                                            outputPath = myProgressMenu.OriginalXML + ".datafiles";
                                        if (File.Exists(outputPath))
                                            outputPath = myProgressMenu.OriginalXML + $".{DateTime.Now.ToBinary().ToString()}" + ".files";
                                        myProgressMenu.OutputXML = outputPath;
                                    }
                                }
                                break;
                            case Operation.Compress:
                                if (orphanArgs.Count < 2)
                                {
                                    this.MainForm = myProgressMenu;
                                    this.ShowUsageHint();
                                    return false;
                                }
                                else
                                {
                                    myProgressMenu.OriginalXML = orphanArgs[0];
                                    myProgressMenu.DirectoryOfPatchcingInfo = orphanArgs[1];
                                    if (!string.IsNullOrWhiteSpace(switch_tmp) && Directory.Exists(switch_tmp))
                                        myProgressMenu.TemporaryFolder = switch_tmp;
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
