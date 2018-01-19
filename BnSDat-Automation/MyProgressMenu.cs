using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using BnSDat;
using System.Windows.Forms;
using Leayal.Ini;
using Leayal;
using Leayal.IO;
using System.Threading;

namespace BnSDat_Automation
{
    public enum Operation : byte
    {
        None,
        Extract,
        Compress,
        Patch
    }
    public partial class MyProgressMenu : Form
    {
        private BackgroundWorker bworker;
        private SynchronizationContext sync;
        private static readonly char[] directorySplit = { '\\', '/' };

        public MyProgressMenu()
        {
            this.InitializeComponent();
            this.Operation = Operation.None;
            this.Is64 = false;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            this.bworker = new BackgroundWorker();
            this.bworker.WorkerSupportsCancellation = true;
            this.bworker.WorkerReportsProgress = false;
            this.bworker.DoWork += this.Bworker_DoWork;
            this.bworker.RunWorkerCompleted += this.Bworker_RunWorkerCompleted;
        }

        private void Bworker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                MessageBox.Show(this, e.Error.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.ExitCode = 2;
                this.Close();
                // Application.Exit();
            }
            else if (e.Cancelled)
            {
                Environment.ExitCode = 1;
                this.Close();
            }
            else
            {
                this.Close();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!this.bworker.IsBusy)
                base.OnFormClosing(e);
            else
            {
                e.Cancel = true;
                this.label1.Text = "Cancelling";
                this.bworker.CancelAsync();
            }
        }

        private void Bworker_DoWork(object sender, DoWorkEventArgs e)
        {
            this.sync.Post(new SendOrPostCallback(delegate
            {
                this.label1.Text = "Preparing";
            }), null);
            Operation operation = this.Operation;
            string tmpFolder, path_original, path_output, path_info;
            int counting = 0;

            if (operation == Operation.Patch)
            {
                tmpFolder = this.TemporaryFolder;
                path_original = Path.GetFullPath(this.OriginalXML);
                path_output = Path.GetFullPath(this.OutputXML);
                path_info = Path.GetFullPath(this.DirectoryOfPatchcingInfo);

                if (!Directory.Exists(path_info))
                    throw new DirectoryNotFoundException();

                if (!File.Exists(path_original))
                    throw new FileNotFoundException("The original xml file is not existed.");

                if (path_original.IsEqual(path_output, true))
                    throw new InvalidDataException("You can't specify output as same as original xml file.");

                string tmpString;
                IniFile currentIni;
                Dictionary<string, string> nodes;

                Dictionary<string, Dictionary<string, string>> patchingList = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
                foreach (string iniPath in Directory.EnumerateFiles(path_info, "*.xmlpatch", SearchOption.AllDirectories))
                {
                    tmpString = PathHelper.PathTrim(iniPath.Substring(path_info.Length + 1));
                    currentIni = new IniFile(iniPath);
                    nodes = new Dictionary<string, string>();
                    foreach (string section in currentIni.Sections)
                        nodes.Add(section, currentIni.GetValue(section, "Replace", string.Empty));
                    if (nodes.Count > 0)
                        patchingList.Add(Path.ChangeExtension(tmpString, ".xml"), nodes);
                    currentIni.Close();
                }

                if (patchingList.Count > 0)
                {
                    using (BnSDatArchive archive = BnSDatArchive.Read(path_original))
                    using (BnSDatWriter outputArchive = string.IsNullOrWhiteSpace(tmpFolder) ? BnSDatWriter.Create(path_output) : BnSDatWriter.Create(path_output, tmpFolder))
                    using (IReader reader = archive.ExtractAllEntries())
                    {
                        this.sync.Post(new SendOrPostCallback(delegate
                        {
                            progressBar1.Maximum = archive.EntryCount;
                        }), null);
                        while (reader.MoveToNextEntry())
                        {
                            if (this.bworker.CancellationPending)
                            {
                                this.sync.Post(new SendOrPostCallback(delegate
                                {
                                    this.label1.Text = "Cancelling";
                                }), null);
                                e.Cancel = true;
                                break;
                            }
                            this.sync.Post(new SendOrPostCallback(delegate
                            {
                                this.label1.Text = $"Progressing: {reader.Entry.FilePath}";
                            }), null);
                            if (patchingList.ContainsKey(reader.Entry.FilePath))
                            {
                                using (EntryStream entryStream = reader.GetEntryStream())
                                using (StreamReader sr = new StreamReader(entryStream))
                                {
                                    tmpString = sr.ReadToEnd();
                                    foreach (var keypair in patchingList[reader.Entry.FilePath])
                                        tmpString = tmpString.Replace(keypair.Key, keypair.Value);
                                    outputArchive.CompressString(reader.Entry.FilePath, tmpString, sr.CurrentEncoding);
                                }
                                tmpString = null;
                            }
                            else
                            {
                                // Just copy the file
                                reader.CopyEntryTo(outputArchive);
                            }
                            counting++;
                            this.sync.Post(new SendOrPostCallback(delegate
                            {
                                progressBar1.Value = counting;
                            }), null);
                        }
                        if (!this.bworker.CancellationPending)
                            outputArchive.WriteArchive();
                    }
                }
                else
                    throw new Exception("There is nothing to patch.");
            }
            else if (operation == Operation.Extract)
            {
                path_original = Path.GetFullPath(this.OriginalXML);
                path_output = Path.GetFullPath(this.OutputXML);
                using (BnSDatArchive archive = BnSDatArchive.Read(path_original))
                using (IReader reader = archive.ExtractAllEntries())
                {
                    string filepath;
                    this.sync.Post(new SendOrPostCallback(delegate
                    {
                        progressBar1.Maximum = archive.EntryCount;
                    }), null);

                    while (reader.MoveToNextEntry())
                    {
                        this.sync.Post(new SendOrPostCallback(delegate
                        {
                            this.label1.Text = $"Extracting: {reader.Entry.FilePath}";
                        }), null);
                        if (this.bworker.CancellationPending)
                        {
                            this.sync.Post(new SendOrPostCallback(delegate
                            {
                                this.label1.Text = "Cancelling";
                            }), null);
                            e.Cancel = true;
                            break;
                        }

                        filepath = Path.Combine(path_output, reader.Entry.FilePath);
                        Microsoft.VisualBasic.FileIO.FileSystem.CreateDirectory(Microsoft.VisualBasic.FileIO.FileSystem.GetParentPath(filepath));
                        using (FileStream fs = File.Create(filepath))
                            reader.ExtractTo(fs);

                        counting++;
                        this.sync.Post(new SendOrPostCallback(delegate
                        {
                            progressBar1.Value = counting;
                        }), null);
                    }
                }
            }
            else if (operation == Operation.Compress)
            {
                tmpFolder = this.TemporaryFolder;
                path_original = Path.GetFullPath(this.OriginalXML);
                path_info = Path.GetFullPath(this.DirectoryOfPatchcingInfo);
                if (!Directory.Exists(path_info))
                    throw new DirectoryNotFoundException();

                string[] filelist = Directory.GetFiles(path_info, "*", SearchOption.AllDirectories);
                if (filelist.Length == 0)
                    throw new Exception("There is nothing to compress.");

                using (BnSDatWriter writer = string.IsNullOrWhiteSpace(tmpFolder) ? BnSDatWriter.Create(path_original) : BnSDatWriter.Create(path_original, tmpFolder))
                {
                    string filepath;
                    this.sync.Post(new SendOrPostCallback(delegate
                    {
                        progressBar1.Maximum = filelist.Length;
                    }), null);
                    for (int i = 0; i < filelist.Length; i++)
                    {
                        filepath = filelist[i].Remove(0, path_info.Length + 1);
                        this.sync.Post(new SendOrPostCallback(delegate
                        {
                            this.label1.Text = $"Compressing: {filepath}";
                            progressBar1.Value = i + 1;
                        }), null);

                        if (this.bworker.CancellationPending)
                        {
                            e.Cancel = true;
                            break;
                        }
                        writer.CompressFile(filepath, filelist[i]);
                    }
                    this.sync.Post(new SendOrPostCallback(delegate
                    {
                        this.label1.Text = "Finalizing archive...";
                    }), null);
                    writer.WriteArchive();
                }
            }
            else
                throw new InvalidEnumArgumentException();
        }

        public bool Is64 { get; set; }
        public string TemporaryFolder { get; set; }
        public string OriginalXML { get; set; }
        public string OutputXML { get; set; }
        public string DirectoryOfPatchcingInfo { get; set; }
        public Operation Operation { get; set; }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            this.sync = SynchronizationContext.Current;
            if (this.Operation != Operation.None)
                this.bworker.RunWorkerAsync();
            /*string reader = @"D:\Bns\bnsdat_0.7.6_windows-portable_i686\bnsdat\bin\xml.modded",
                    compressor = @"D:\Bns\bnsdat_0.7.6_windows-portable_i686\bnsdat\bin\xml.modded",
                    tmpFolder = @"D:\All Content\VB_Project\visual studio 2017\BnS-Mod-Apply-Helper\BnSDat-Automation\bin\Debug\tmp";

            //*
            using (BnSDat.BnSDatArchive asd = BnSDat.BnSDatArchive.Read(reader))
                asd.ExtractTo(@"D:\Bns\bnsdat_0.7.6_windows-portable_i686\bnsdat\bin\xml datasdasdasd");
            //*/

            /*
            Microsoft.VisualBasic.FileIO.FileSystem.CreateDirectory(tmpFolder);
            using (BnSDat.BnSDatWriter writer = BnSDat.BnSDatWriter.Create(compressor, tmpFolder))
            {
                writer.CompressionLevel = SharpCompress.Compressors.Deflate.CompressionLevel.BestSpeed;
                writer.CompressDirectory("xml.dat.files");
                writer.WriteArchive();
            }
            Directory.Delete(tmpFolder, true);
            //*/
        }


    }
}
