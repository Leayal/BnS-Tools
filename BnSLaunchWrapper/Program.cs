using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using Leayal;
using Microsoft.VisualBasic.ApplicationServices;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace BnSLaunchWrapper
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

            ConsoleController controller = new ConsoleController();
            controller.Run(Environment.GetCommandLineArgs());

            AppDomain.CurrentDomain.AssemblyResolve -= ev;
        }

        private class ConsoleController : WindowsFormsApplicationBase
        {
            private static Leayal.Ini.IniFile config;

            public ConsoleController() : base(AuthenticationMode.Windows)
            {
                this.EnableVisualStyles = false;
                this.IsSingleInstance = false;
            }

            protected override bool OnStartup(StartupEventArgs eventArgs)
            {
                this._wrapperform = new WrapperForm();
                this.MainForm = this._wrapperform;
                try
                {
                    config = new Leayal.Ini.IniFile(Path.Combine(this.Info.DirectoryPath, "BnSLaunchWrapper.ini"));

                    if (config.IsEmpty)
                    {
                        config.SetValue("Wrapper", "bKeepRunning", "0");
                        config.SetValue("Wrapper", "bCheckNullParams", "1");
                        config.SetValue("Wrapper", "sExecutable", "RealClient.exe");
                        config.SetValue("Wrapper", "sAdditionParams", string.Empty);

                        config.SetValue("UnrealEngineParams", "bUnattended", "1");
                        config.SetValue("UnrealEngineParams", "bNoTextureStreaming", "0");
                        config.SetValue("UnrealEngineParams", "bUseAllAvailableCores", "1");

                        config.SetValue("Mods", "Enable xml.dat swap", "0");
                        config.SetValue("Mods", "Optimize loading", "0");
                        config.SetValue("Mods", "Enable xml.dat auto-patching", "0");

                        config.SetValue("xml.dat swap", "ignore warning", "0");
                        config.SetValue("xml.dat swap", "original xml.dat", string.Empty);
                        config.SetValue("xml.dat swap", "modded xml.dat", string.Empty);
                        config.SetValue("xml.dat swap", "original sha256 hash", string.Empty);

                        config.SetValue("xml.dat patching", "patcher path", string.Empty);
                        config.SetValue("xml.dat patching", "patch info folder", string.Empty);
                        config.SetValue("xml.dat patching", "working directory", string.Empty);

                        config.SetValue("Optimize Loading", "GameFolder", string.Empty);
                        config.SetValue("Optimize Loading", "BackupFolder", string.Empty);
                        config.SetValue("Optimize Loading", "Files", "00009368.upk;Loading.pkg");
                    }

                    List<string> cmdlines = new List<string>(eventArgs.CommandLine);
                    if (Path.IsPathRooted(cmdlines[0]) && cmdlines[0].EndsWith(Path.GetFileName(AppInfo.ProcessFullpath), StringComparison.OrdinalIgnoreCase) && Leayal.StringHelper.IsEqual(Path.GetFullPath(cmdlines[0]), AppInfo.ProcessFullpath, true))
                        cmdlines.RemoveAt(0);

                    if (config.GetValue("Wrapper", "bCheckNullParams", "1") != "0")
                        if (cmdlines.Count == 0)
                            this.ExitProgram(1);

                    string fullexepath = config.GetValue("Wrapper", "sExecutable", string.Empty);
                    if (string.IsNullOrWhiteSpace(fullexepath))
                        this.ExitProgram(2);
                    else
                        fullexepath = Path.GetFullPath(fullexepath);

                    if (File.Exists(fullexepath))
                    {
                        if (config.GetValue("UnrealEngineParams", "bUnattended", "1") != "0")
                            if (!cmdlines.Contains("-UNATTENDED", StringComparer.OrdinalIgnoreCase))
                                cmdlines.Add("-UNATTENDED");
                        if (config.GetValue("UnrealEngineParams", "bNoTextureStreaming", "0") != "0")
                            if (!cmdlines.Contains("-NOTEXTURESTREAMING", StringComparer.OrdinalIgnoreCase))
                                cmdlines.Add("-NOTEXTURESTREAMING");
                        if (config.GetValue("UnrealEngineParams", "bUseAllAvailableCores", "1") != "0")
                            if (!cmdlines.Contains("-USEALLAVAILABLECORES", StringComparer.OrdinalIgnoreCase))
                                cmdlines.Add("-USEALLAVAILABLECORES");

                        StringBuilder strBuilder = new StringBuilder(ProcessHelper.TableStringToArgs(cmdlines));

                        string configArgs = config.GetValue("Wrapper", "sAdditionParams", string.Empty);
                        if (!string.IsNullOrWhiteSpace(configArgs))
                        {
                            strBuilder.Append(" ");
                            strBuilder.Append(configArgs);
                        }

                        using (Process proc = new Process()
                        {
                            StartInfo = new ProcessStartInfo()
                            {
                                Arguments = strBuilder.ToString(),
                                FileName = fullexepath,
                                Verb = "runas"
                            },
                            EnableRaisingEvents = false
                        })
                        {
                            bool keeprunning = (config.GetValue("Wrapper", "bKeepRunning", "0") != "0"),
                                swap_ignorewarning = (config.GetValue("xml.dat swap", "ignore warning", "0") != "0"),
                                swapfile_swapped = false,
                                optimizeLoading = (config.GetValue("Mods", "Optimize loading", "0") != "0"),
                                autopatching = (config.GetValue("Mods", "Enable xml.dat auto-patching", "0") != "0");
                            string xml_ori = config.GetValue("xml.dat swap", "original xml.dat", string.Empty),
                                xml_modded = config.GetValue("xml.dat swap", "modded xml.dat", string.Empty),
                                original_hash = config.GetValue("xml.dat swap", "original sha256 hash", string.Empty),

                                optLoading_from = config.GetValue("Optimize Loading", "GameFolder", string.Empty),
                                optLoading_to = config.GetValue("Optimize Loading", "BackupFolder", string.Empty),
                                optLoading_filelist = config.GetValue("Optimize Loading", "Files", string.Empty),

                                patcher_path = config.GetValue("xml.dat patching", "patcher path", string.Empty),
                                patcher_pathinfo = config.GetValue("xml.dat patching", "patch info folder", string.Empty),
                                patcher_workingdir = config.GetValue("xml.dat patching", "working directory", string.Empty);
                            List<string> filelist = null;

                            SwapFileResult swapFileResult = null;

                            if (keeprunning)
                            {
                                if (config.GetValue("Mods", "enable xml.dat swap", "0") != "0")
                                    if (!string.IsNullOrWhiteSpace(xml_ori) && File.Exists(xml_ori))
                                        if (!string.IsNullOrWhiteSpace(xml_modded) && File.Exists(xml_modded))
                                        {
                                            xml_ori = Path.GetFullPath(xml_ori);
                                            xml_modded = Path.GetFullPath(xml_modded);
                                            bool safetoswap = false;

                                            if (swap_ignorewarning)
                                                safetoswap = true;
                                            else
                                            {
                                                string currenthash = Leayal.Security.Cryptography.SHA256Wrapper.FromFile(xml_ori);
                                                if (string.IsNullOrWhiteSpace(original_hash))
                                                {
                                                    config.SetValue("xml.dat swap", "original sha256 hash", currenthash);
                                                    safetoswap = true;
                                                }
                                                else
                                                {
                                                    if (currenthash.IsEqual(original_hash, true))
                                                        safetoswap = true;
                                                    else
                                                    {
                                                        // Throw warning and exit.
                                                        this._wrapperform.Show();

                                                        bool patchsuccess = false;
                                                        if (File.Exists(patcher_path) && Directory.Exists(patcher_pathinfo))
                                                        {
                                                            if (MessageBox.Show(this.GetWrapperForm(), "The original xml.dat has been changed. Do you want to patch it?", "Question", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
                                                            {
                                                                using (Process patchingProc = new Process())
                                                                {
                                                                    patchingProc.StartInfo.FileName = patcher_path;
                                                                    List<string> paramList = new List<string>(5);
                                                                    paramList.Add("-p");
                                                                    paramList.Add(xml_ori);
                                                                    paramList.Add(xml_modded);
                                                                    paramList.Add(patcher_pathinfo);
                                                                    if (!string.IsNullOrWhiteSpace(patcher_workingdir))
                                                                    {
                                                                        Microsoft.VisualBasic.FileIO.FileSystem.CreateDirectory(patcher_workingdir);
                                                                        paramList.Add(patcher_workingdir);
                                                                    }
                                                                    patchingProc.StartInfo.Arguments = ProcessHelper.TableStringToArgs(paramList);

                                                                    patchingProc.Start();
                                                                    patchingProc.WaitForExit();
                                                                    if (patchingProc.ExitCode == 0)
                                                                    {
                                                                        config.SetValue("xml.dat swap", "original sha256 hash", currenthash);
                                                                        safetoswap = true;
                                                                        patchsuccess = true;
                                                                    }
                                                                }
                                                            }
                                                        }

                                                        if (!patchsuccess)
                                                        {
                                                            if (MessageBox.Show(this.GetWrapperForm(), "The original xml.dat has been changed. You should rebuild/update your modded xml.dat file, otherwise unexpected results may happen.\nIgnore hash check for current version and continue?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
                                                            {
                                                                config.SetValue("xml.dat swap", "original sha256 hash", currenthash);
                                                                safetoswap = true;
                                                            }
                                                            else
                                                                this.ExitProgram(1);
                                                        }
                                                    }
                                                }
                                            }

                                            if (safetoswap)
                                            {
                                                swapFileResult = SwapFile(xml_ori, xml_modded, Path.ChangeExtension(xml_ori, ".original"));
                                                if (swapFileResult.Error == null)
                                                    swapfile_swapped = true;
                                                else
                                                {
                                                    this._wrapperform.Show();
                                                    if (MessageBox.Show(this.GetWrapperForm(), "An error has been occured. Start game anyway???\nError detail: " + swapFileResult.Error.ToString(), "Error while swapping file.", MessageBoxButtons.YesNo, MessageBoxIcon.Error) != DialogResult.Yes)
                                                        this.ExitProgram(2);
                                                }
                                            }
                                        }

                                if (optimizeLoading)
                                {
                                    if (!string.IsNullOrWhiteSpace(optLoading_from) && !string.IsNullOrWhiteSpace(optLoading_to) && !string.IsNullOrWhiteSpace(optLoading_filelist))
                                    {
                                        optLoading_from = Path.GetFullPath(optLoading_from);
                                        optLoading_to = Path.GetFullPath(optLoading_to);

                                        if (Directory.Exists(optLoading_from))
                                        {
                                            string[] files;
                                            if (optLoading_filelist.IndexOf(';') > -1)
                                                files = optLoading_filelist.Split(';');
                                            else
                                                files = new string[] { optLoading_filelist };

                                            Microsoft.VisualBasic.FileIO.FileSystem.CreateDirectory(optLoading_to);

                                            if (files.Length > 0)
                                            {
                                                filelist = new List<string>(files.Length);
                                                string tmpstrFrom, tmpstrTo;
                                                for (int i = 0; i < files.Length; i++)
                                                {
                                                    tmpstrFrom = Path.Combine(optLoading_from, files[i]);
                                                    tmpstrTo = Path.Combine(optLoading_to, files[i] + ".bak");
                                                    if (File.Exists(tmpstrFrom))
                                                    {
                                                        if (File.Exists(tmpstrTo))
                                                            File.Delete(tmpstrTo);
                                                        File.Move(tmpstrFrom, tmpstrTo);
                                                        filelist.Add(files[i]);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            this._wrapperform.Hide();
                            proc.Start();

                            if (keeprunning)
                            {
                                proc.WaitForExit();

                                if (swapFileResult != null)
                                    if (swapfile_swapped)
                                    {
                                        if (File.Exists(swapFileResult.BackupFilename))
                                        {
                                            if (File.Exists(swapFileResult.FilenameToSwap))
                                                File.Delete(swapFileResult.FilenameToSwap);
                                            if (File.Exists(swapFileResult.FilenameToBeSwapped))
                                                File.Move(swapFileResult.FilenameToBeSwapped, swapFileResult.FilenameToSwap);

                                            File.Move(swapFileResult.BackupFilename, swapFileResult.FilenameToBeSwapped);
                                        }
                                    }

                                if (optimizeLoading)
                                {
                                    if (Directory.Exists(optLoading_to) && (filelist != null))
                                    {
                                        string tmpstrFrom, tmpstrTo;
                                        for (int i = 0; i < filelist.Count; i++)
                                        {
                                            tmpstrFrom = Path.Combine(optLoading_from, filelist[i]);
                                            tmpstrTo = Path.Combine(optLoading_to, filelist[i] + ".bak");
                                            if (File.Exists(tmpstrTo))
                                            {
                                                if (File.Exists(tmpstrFrom))
                                                    File.Delete(tmpstrFrom);
                                                File.Move(tmpstrTo, tmpstrFrom);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        this.ExitProgram(0);
                    }
                    else
                        this.ExitProgram(-1);
                }
                catch (Exception ex)
                {
                    this._wrapperform.Show();
                    MessageBox.Show(this.GetWrapperForm(), ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.ExitProgram(-2);
                }
                return false;
            }

            private WrapperForm _wrapperform;
            public WrapperForm GetWrapperForm()
            {
                this._wrapperform.Activate();
                FlashWindowEx(this._wrapperform);
                return this._wrapperform;
            }

            private SwapFileResult SwapFile(string filetobeswapped, string filetoswap, string backupname)
            {
                try
                {
                    if (File.Exists(backupname))
                        throw new InvalidOperationException("Backup already existed.");
                    File.Move(filetobeswapped, backupname);
                }
                catch (Exception ex)
                {
                    return new SwapFileResult(ex);
                }
                
                try
                {
                    if (File.Exists(filetobeswapped))
                        throw new InvalidOperationException("Current Swapped file already existed.");
                    File.Move(filetoswap, filetobeswapped);
                }
                catch (Exception ex)
                {
                    try
                    {
                        File.Move(backupname, filetobeswapped);
                    }
                    catch (Exception exx)
                    {
                        return new SwapFileResult(exx);
                    }
                    return new SwapFileResult(ex);
                }

                return new SwapFileResult(filetobeswapped, filetoswap, backupname);
            }
            
            private void ExitProgram(int code)
            {
                if (config != null)
                    config.Save();
                if (this.MainForm != null)
                    this.MainForm.Dispose();
                // System.Environment.ExitCode = code;
                // System.Windows.Forms.Application.Exit();
                System.Environment.Exit(code);
            }

            private class SwapFileResult
            {
                public Exception Error { get; }
                public string FilenameToBeSwapped { get; }
                public string FilenameToSwap { get; }
                public string BackupFilename { get; }

                private SwapFileResult(Exception ex, string filetobeswapped, string filetoswap, string backupname)
                {
                    this.Error = ex;
                    this.FilenameToBeSwapped = filetobeswapped;
                    this.FilenameToSwap = filetoswap;
                    this.BackupFilename = backupname;
                }
                public SwapFileResult(string filetobeswapped, string filetoswap, string backupname) : this(null, filetobeswapped, filetoswap, backupname) { }
                public SwapFileResult(Exception ex) : this(ex, null, null, null) { }
            }

            private class Win32Window : System.Windows.Forms.IWin32Window
            {
                public Win32Window(IntPtr handle)
                {
                    this._hwnd = handle;
                }
                public IntPtr Handle => this._hwnd;
                private IntPtr _hwnd;
            }

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

            //Flash both the window caption and taskbar button.
            //This is equivalent to setting the FLASHW_CAPTION | FLASHW_TRAY flags. 
            public const UInt32 FLASHW_ALL = 3;

            // Flash continuously until the window comes to the foreground. 
            public const UInt32 FLASHW_TIMERNOFG = 12;

            [StructLayout(LayoutKind.Sequential)]
            public struct FLASHWINFO
            {
                public UInt32 cbSize;
                public IntPtr hwnd;
                public UInt32 dwFlags;
                public UInt32 uCount;
                public UInt32 dwTimeout;
            }

            // Do the flashing - this does not involve a raincoat.
            public static bool FlashWindowEx(Form form)
            {
                IntPtr hWnd = form.Handle;
                FLASHWINFO fInfo = new FLASHWINFO();

                fInfo.cbSize = Convert.ToUInt32(Marshal.SizeOf(fInfo));
                fInfo.hwnd = hWnd;
                fInfo.dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG;
                fInfo.uCount = UInt32.MaxValue;
                fInfo.dwTimeout = 0;

                return FlashWindowEx(ref fInfo);
            }
        }
    }
}
