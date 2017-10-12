using Leayal.IO;
using BnSDat.Zlib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace BnSDat
{
    public enum BnSDatWriterTemporaryLocation : byte
    {
        InMemory,
        OnDisk
    }
    public class BnSDatWriter : IDisposable
    {
        private static readonly byte[] headerPadding = new byte[60];
        public static BnSDatWriter Create(string filename) => BnSDatWriter.Create(filename, false);
        public static BnSDatWriter Create(string filename, string temporaryFolder) => BnSDatWriter.Create(filename, temporaryFolder, false);
        public static BnSDatWriter Create(string filename, bool is64) => BnSDatWriter.Create(filename, null, is64);
        public static BnSDatWriter Create(string filename, string temporaryFolder, bool is64)
        {
            FileStream fs = File.Create(filename);
            return BnSDatWriter.Create(fs, temporaryFolder, false, is64);
        }
        public static BnSDatWriter Create(Stream stream) => BnSDatWriter.Create(stream, false);
        public static BnSDatWriter Create(Stream stream, string temporaryFolder) => BnSDatWriter.Create(stream, temporaryFolder, false);
        public static BnSDatWriter Create(Stream stream, bool leaveOpen) => BnSDatWriter.Create(stream, leaveOpen, false);
        public static BnSDatWriter Create(Stream stream, string temporaryFolder, bool leaveOpen) => BnSDatWriter.Create(stream, temporaryFolder, leaveOpen, false);
        public static BnSDatWriter Create(Stream stream, bool leaveOpen, bool is64) => BnSDatWriter.Create(stream, null, leaveOpen, is64);
        public static BnSDatWriter Create(Stream stream, string temporaryFolder, bool leaveOpen, bool is64)
        {
            return new BnSDatWriter(stream, temporaryFolder, leaveOpen, is64);
        }

        public static BnSDatWriter Modify(string filename) => BnSDatWriter.Modify(filename, false);
        public static BnSDatWriter Modify(string filename, bool is64)
        {
            FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            BnSDatWriter result = BnSDatWriter.Create(fs, false);
            using (BnSDatArchive archive = BnSDatArchive.Read(fs, true))
            using (IReader reader = archive.ExtractAllEntries())
                while (reader.MoveToNextEntry())
                    reader.CopyEntryTo(result);
            return result;
        }

        private Dictionary<string, Entry> currentItems;
        private Dictionary<Entry, Stream> currentContents;

        public ICollection<Entry> Entries => this.currentContents.Keys;
        public Entry this[FilepathInsideArchive path] => this.currentItems[path.PathInsideArchive];

        internal Stream BaseStream { get; }
        public string TemporaryFolder { get; }
        internal bool Is64bit { get; }
        public int ItemCount => this.currentItems.Count;
        private bool _leaveOpen;
        private string temporaryFilename;

        internal BnSDatWriter(Stream stream, string temporaryFolder, bool leaveOpen, bool is64)
        {
            if (!stream.CanWrite)
                throw new InvalidOperationException("The stream should be able to read/write.");

            if (!string.IsNullOrWhiteSpace(temporaryFolder) && !Directory.Exists(temporaryFolder))
                throw new DirectoryNotFoundException();

            this.temporaryFilename = Path.ChangeExtension(Path.GetRandomFileName(), ".{0}") + "." + DateTime.Now.ToBinary().ToString();
            this.Is64bit = is64;
            this.currentItems = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            this.currentContents = new Dictionary<Entry, Stream>();
            this._leaveOpen = leaveOpen;
            this.CompressionLevel = CompressionLevel.Default;
            this.BaseStream = stream;
            this.TemporaryFolder = temporaryFolder;
        }

        public void UpdateEntryPath(Entry entry, FilepathInsideArchive path)
        {
            entry.Raw.FilePath = path;
            entry.Raw.FilePathLength = path.PathInsideArchive.Length;
        }

        public void RemoveEntry(Entry entry)
        {
            this.currentItems.Remove(entry.FilePath);
            this.currentContents[entry].Dispose();
            this.currentContents.Remove(entry);
        }

        public void RemoveEntry(FilepathInsideArchive path)
        {
            this.RemoveEntry(this[path]);
        }

        private Stream CreateTempStream(string temporaryLocation)
        {
            if (string.IsNullOrWhiteSpace(temporaryLocation))
                return new RecyclableMemoryStream();
            else
                return new FileStream(Path.Combine(temporaryLocation, string.Format(this.temporaryFilename, (this.currentContents.Count + DateTime.Now.ToBinary()).ToString() + Path.GetRandomFileName())), FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.DeleteOnClose);
        }

        public CompressionLevel CompressionLevel { get; set; }
        
        public void WriteArchive()
        {
            this.BaseStream.Position = 0;
            if (this.BaseStream.Length != 0)
                this.BaseStream.SetLength(0);

            Entry[] entryList = this.currentContents.Keys.ToArray();
            Entry currentprocessingEntry;

            using (RecyclableMemoryStream rms_header = new RecyclableMemoryStream())
            using (BinaryWriter bw_header = new BinaryWriter(rms_header))
            {
                long virtualcontentoffset = 0;
                for (int i = 0; i < entryList.Length; i++)
                {
                    currentprocessingEntry = entryList[i];

                    currentprocessingEntry.Raw.FileDataOffset = (int)virtualcontentoffset;
                    virtualcontentoffset += this.currentContents[currentprocessingEntry].Length;

                    if (this.Is64bit)
                        bw_header.Write((long)currentprocessingEntry.Raw.FilePathLength);
                    else
                        bw_header.Write(currentprocessingEntry.Raw.FilePathLength);
                    bw_header.Write(Encoding.Unicode.GetBytes(currentprocessingEntry.Raw.FilePath));
                    bw_header.Write(currentprocessingEntry.Raw.Unknown_001);
                    bw_header.Write(currentprocessingEntry.Raw.IsCompressed);
                    bw_header.Write(currentprocessingEntry.Raw.IsEncrypted);
                    bw_header.Write(currentprocessingEntry.Raw.Unknown_002);

                    if (this.Is64bit)
                        bw_header.Write((long)currentprocessingEntry.Raw.FileDataSizeUnpacked);
                    else
                        bw_header.Write(currentprocessingEntry.Raw.FileDataSizeUnpacked);

                    if (this.Is64bit)
                        bw_header.Write((long)currentprocessingEntry.Raw.FileDataSizeSheared);
                    else
                        bw_header.Write(currentprocessingEntry.Raw.FileDataSizeSheared);

                    if (this.Is64bit)
                        bw_header.Write((long)currentprocessingEntry.Raw.FileDataSizeStored);
                    else
                        bw_header.Write(currentprocessingEntry.Raw.FileDataSizeStored);

                    if (this.Is64bit)
                        bw_header.Write((long)currentprocessingEntry.Raw.FileDataOffset);
                    else
                        bw_header.Write(currentprocessingEntry.Raw.FileDataOffset);
                    
                    bw_header.Write(headerPadding);
                }

                bw_header.Flush();

                BinaryWriter bw = new BinaryWriter(this.BaseStream);
                byte[] Signature = new byte[8] { (byte)'U', (byte)'O', (byte)'S', (byte)'E', (byte)'D', (byte)'A', (byte)'L', (byte)'B' };
                bw.Write(Signature);
                // Writing Version
                bw.Write((int)2);
                // Write Unknown_001
                bw.Write(new byte[5] { 0, 0, 0, 0, 0 });
                
                // Write FileDataSizePacked
                if (this.Is64bit)
                {
                    bw.Write(virtualcontentoffset);
                    bw.Write((long)this.ItemCount);
                }
                else
                {
                    bw.Write((int)virtualcontentoffset);
                    bw.Write(this.ItemCount);
                }

                // Write IsCompressed
                bool isCompressed = (this.CompressionLevel != CompressionLevel.None);
                bw.Write(isCompressed);
                // Write IsEncrypted
                bw.Write(true);
                // Write Unknown_002
                bw.Write(new byte[62]);

                int FileTableSizeUnpacked = (int)rms_header.Length;
                int FileTableSizeSheared = FileTableSizeUnpacked;
                int FileTableSizePacked = FileTableSizeUnpacked;

                using (var packedHeader = this.Pack(rms_header.GetBuffer(), rms_header.Length, FileTableSizeUnpacked, out FileTableSizeSheared, out FileTableSizePacked, true, isCompressed, this.CompressionLevel))
                {
                    if (this.Is64bit)
                        bw.Write((long)FileTableSizePacked);
                    else
                        bw.Write(FileTableSizePacked);

                    if (this.Is64bit)
                        bw.Write((long)FileTableSizeUnpacked);
                    else
                        bw.Write(FileTableSizeUnpacked);

                    bw.Write(packedHeader.GetBuffer(), 0, (int)packedHeader.Length);
                }

                int OffsetGlobal = (int)this.BaseStream.Position + (this.Is64bit ? 8 : 4);

                if (this.Is64bit)
                    bw.Write((long)OffsetGlobal);
                else
                    bw.Write(OffsetGlobal);

                bw.Flush();

                Stream currentProcessingStream;
                for (int i = 0; i < entryList.Length; i++)
                {
                    currentprocessingEntry = entryList[i];
                    currentProcessingStream = this.currentContents[currentprocessingEntry];
                    currentProcessingStream.Seek(0, SeekOrigin.Begin);
                    this.StreamCopyChunk(currentProcessingStream, bw.BaseStream, 4096);
                }
                bw.BaseStream.Flush();
            }
        }
        
        public void CompressString(FilepathInsideArchive filepathInsideArchive, string content)
        {
            this.CompressString(filepathInsideArchive, content, Encoding.Unicode);
        }

        public void CompressString(FilepathInsideArchive filepathInsideArchive, string content, Encoding encoding)
        {
            this.CompressEntry(filepathInsideArchive, encoding.GetBytes(content));
        }

        public void CompressFile(FilepathInsideArchive filepathInsideArchive, string filepath)
        {
            using (FileStream fs = File.OpenRead(filepath))
                this.CompressEntry(filepathInsideArchive, fs, true);
        }

        public void CompressEntry(FilepathInsideArchive filepathInsideArchive, Stream content)
        {
            this.CompressEntry(filepathInsideArchive, content, false);
        }

        public void CompressEntry(FilepathInsideArchive filepathInsideArchive, Stream content, bool leaveOpen)
        {
            if (this.currentItems.ContainsKey(filepathInsideArchive.PathInsideArchive))
                throw new ArgumentException($"Item '{filepathInsideArchive.PathInsideArchive}' is not already existed in the archive.");

            BPKG_FTE FileTableEntry = new BPKG_FTE();

            FileTableEntry.FilePathLength = filepathInsideArchive.PathInsideArchive.Length;

            FileTableEntry.FilePath = filepathInsideArchive.PathInsideArchive;
            FileTableEntry.Unknown_001 = 2;
            FileTableEntry.IsCompressed = (this.CompressionLevel != CompressionLevel.None);
            FileTableEntry.IsEncrypted = true;
            FileTableEntry.Unknown_002 = 0;

            Stream contentStream = this.CreateTempStream(this.TemporaryFolder);
            MemoryStream memoryStream;

            using (RecyclableMemoryStream tmp = new RecyclableMemoryStream())
                if (filepathInsideArchive.PathInsideArchive.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || filepathInsideArchive.PathInsideArchive.EndsWith(".x16", StringComparison.OrdinalIgnoreCase))
                {
                    // encode bxml
                    BXML bxml = new BXML(BXML.XOR_KEY_Data);
                    BXML.Convert(content, bxml.DetectType(content), tmp, BXML_TYPE.BXML_BINARY);

                    // FileTableEntry.FileDataOffset = (int)mosFiles.BaseStream.Position;
                    FileTableEntry.FileDataSizeUnpacked = (int)tmp.Length;

                    memoryStream = this.Pack(tmp.GetBuffer(), tmp.Length, FileTableEntry.FileDataSizeUnpacked, out FileTableEntry.FileDataSizeSheared, out FileTableEntry.FileDataSizeStored, FileTableEntry.IsEncrypted, FileTableEntry.IsCompressed, this.CompressionLevel);
                }
                else
                {
                    // compress raw
                    // FileTableEntry.FileDataOffset = (int)mosFiles.BaseStream.Position;
                    this.StreamCopyChunk(content, tmp, 1024);

                    FileTableEntry.FileDataSizeUnpacked = (int)content.Length;

                    memoryStream = this.Pack(tmp.GetBuffer(), tmp.Length, FileTableEntry.FileDataSizeUnpacked, out FileTableEntry.FileDataSizeSheared, out FileTableEntry.FileDataSizeStored, FileTableEntry.IsEncrypted, FileTableEntry.IsCompressed, this.CompressionLevel);
                }
            contentStream.Write(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
            contentStream.Flush();
            memoryStream.Dispose();
            memoryStream = null;

            if (!leaveOpen)
                content.Dispose();

            // FileTableEntry.Padding = new byte[60];
            Entry entry = Entry.FromStruct(FileTableEntry);
            this.currentItems.Add(FileTableEntry.FilePath, entry);
            this.currentContents.Add(entry, contentStream);
        }

        public void UpdateEntry(FilepathInsideArchive filepathInsideArchive, Stream content, bool leaveOpen)
        {
            if (!this.currentItems.ContainsKey(filepathInsideArchive))
                throw new ArgumentException($"Item '{filepathInsideArchive.PathInsideArchive}' was already existed in the archive.");

            Entry entry = this.currentItems[filepathInsideArchive];
            BPKG_FTE FileTableEntry = entry.Raw;
            
            FileTableEntry.FilePathLength = filepathInsideArchive.PathInsideArchive.Length;

            FileTableEntry.FilePath = filepathInsideArchive.PathInsideArchive;
            FileTableEntry.Unknown_001 = 2;
            FileTableEntry.IsCompressed = (this.CompressionLevel != CompressionLevel.None);
            FileTableEntry.IsEncrypted = true;
            FileTableEntry.Unknown_002 = 0;

            Stream contentStream = this.currentContents[entry];
            contentStream.Seek(0, SeekOrigin.Begin);
            contentStream.SetLength(0);
            MemoryStream memoryStream;

            using (RecyclableMemoryStream tmp = new RecyclableMemoryStream())
                if (filepathInsideArchive.PathInsideArchive.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || filepathInsideArchive.PathInsideArchive.EndsWith(".x16", StringComparison.OrdinalIgnoreCase))
                {
                    // encode bxml
                    BXML bxml = new BXML(BXML.XOR_KEY_Data);
                    BXML.Convert(content, bxml.DetectType(content), tmp, BXML_TYPE.BXML_BINARY);

                    // FileTableEntry.FileDataOffset = (int)mosFiles.BaseStream.Position;
                    FileTableEntry.FileDataSizeUnpacked = (int)tmp.Length;

                    memoryStream = this.Pack(tmp.GetBuffer(), tmp.Length, FileTableEntry.FileDataSizeUnpacked, out FileTableEntry.FileDataSizeSheared, out FileTableEntry.FileDataSizeStored, FileTableEntry.IsEncrypted, FileTableEntry.IsCompressed, this.CompressionLevel);
                }
                else
                {
                    // compress raw
                    // FileTableEntry.FileDataOffset = (int)mosFiles.BaseStream.Position;
                    this.StreamCopyChunk(content, tmp, 1024);

                    FileTableEntry.FileDataSizeUnpacked = (int)content.Length;

                    memoryStream = this.Pack(tmp.GetBuffer(), tmp.Length, FileTableEntry.FileDataSizeUnpacked, out FileTableEntry.FileDataSizeSheared, out FileTableEntry.FileDataSizeStored, FileTableEntry.IsEncrypted, FileTableEntry.IsCompressed, this.CompressionLevel);
                }
            contentStream.Write(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
            contentStream.Flush();
            memoryStream.Dispose();
            memoryStream = null;

            if (!leaveOpen)
                content.Dispose();
        }

        internal void AddNewEntry(Entry entry, Stream content)
        {
            Stream contentStream = this.CreateTempStream(this.TemporaryFolder);
            this.StreamCopyChunk(content, contentStream, 4096);
            this.currentItems.Add(entry.FilePath, entry);
            this.currentContents.Add(entry, contentStream);
        }
        
        public void CompressEntry(FilepathInsideArchive filepathInsideArchive, byte[] content)
        {
            using (MemoryStream memStream = new MemoryStream(content, false))
                this.CompressEntry(filepathInsideArchive, memStream, false);
        }

        public void CompressDirectory(string folder)
        {
            this.CompressDirectory(folder, "*");
        }

        public void CompressDirectory(string folder, string searchPattern)
        {
            this.CompressDirectory(folder, searchPattern, SearchOption.AllDirectories);
        }

        public void CompressDirectory(string folder, string searchPattern, SearchOption searchOption)
        {
            if (!Directory.Exists(folder))
                throw new DirectoryNotFoundException("The folder is not existed.");

            Dictionary<FilepathInsideArchive, string> dict = new Dictionary<FilepathInsideArchive, string>();
            foreach (string filename in Directory.EnumerateFiles(folder, searchPattern, searchOption))
                dict.Add(filename.Remove(0, folder.Length).TrimStart('\\', '/'), filename);
            this.CompressDirectory(dict);
        }

        public void CompressDirectory(IDictionary<FilepathInsideArchive, string> filelist)
        {
            foreach (var keypair in filelist)
                this.CompressFile(keypair.Key, keypair.Value);
        }

        private void StreamCopyChunk(Stream source, Stream destination, int buffersize)
        {
            using (Leayal.ByteBuffer buffer = new Leayal.ByteBuffer(buffersize))
            {
                int read = source.Read(buffer, 0, buffer.Length);
                while (read > 0)
                {
                    destination.Write(buffer, 0, read);
                    read = source.Read(buffer, 0, buffer.Length);
                }
            }
        }
        
        private RecyclableMemoryStream Inflate(byte[] buffer, long bufferlength, int sizeDecompressed, out int sizeCompressed, CompressionLevel compressionLevel)
        {
            RecyclableMemoryStream output = new RecyclableMemoryStream();
            using (ZlibStream zs = new ZlibStream(output, CompressionMode.Compress, compressionLevel, true))
            {
                zs.Write(buffer, 0, (int)bufferlength);
                zs.Flush();
            }
            sizeCompressed = (int)output.Length;
            output.Position = 0;
            return output;
        }

        private RecyclableMemoryStream Encrypt(byte[] buffer, long bufferlength, int size, out int sizePadded)
        {
            sizePadded = size + (RijndaelStream.AES_KEY.Length - (size % RijndaelStream.AES_KEY.Length));
            RecyclableMemoryStream result = new RecyclableMemoryStream();
            result.SetLength(sizePadded);
            byte[] temp = new byte[sizePadded];

            unsafe
            {
                fixed (byte* source = buffer, dest = temp)
                {
                    for (int i = 0; i < bufferlength; i++)
                        dest[i] = source[i];
                    
                    using (Rijndael aes = Rijndael.Create())
                    {
                        aes.Mode = CipherMode.ECB;
                        using (ICryptoTransform encrypt = aes.CreateEncryptor(RijndaelStream.RAW_AES_KEY, new byte[16]))
                            encrypt.TransformBlock(temp, 0, sizePadded, result.GetBuffer(), 0);
                    }
                }
            }
            result.Position = 0;
            return result;
        }

        private RecyclableMemoryStream Pack(byte[] buffer, long bufferlength, int sizeUnpacked, out int sizeSheared, out int sizeStored, bool encrypt, bool compress, CompressionLevel compressionLevel)
        {
            RecyclableMemoryStream result = null;
            sizeSheared = sizeUnpacked;
            sizeStored = sizeSheared;

            if (compress && (((int)compressionLevel) != 0))
            {
                result = Inflate(buffer, bufferlength, sizeUnpacked, out sizeSheared, compressionLevel);
                sizeStored = sizeSheared;
            }

            if (encrypt)
            {
                if (result == null)
                    result = Encrypt(buffer, bufferlength, sizeSheared, out sizeStored);
                else
                    result = Encrypt(result.GetBuffer(), (int)result.Length, sizeSheared, out sizeStored);
            }
            return result;
        }

        private bool _disposed;
        public void Dispose()
        {
            if (this._disposed) return;
            this._disposed = true;

            foreach (Stream bw in this.currentContents.Values)
                bw.Dispose();
            this.currentContents.Clear();
            this.currentItems.Clear();

            if (!this._leaveOpen)
                this.BaseStream.Dispose();
        }
    }
}
