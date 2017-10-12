using System;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using Leayal.IO;
using BnSDat.Zlib;

namespace BnSDat
{
    public class BnSDatArchive : IDisposable
    {
        private Stream originalStream;
        internal Stream OriginalStream => this.originalStream;
        private BinaryReader binaryReader;
        internal BinaryReader BinaryReader => this.binaryReader;
        public bool Is64bit { get; }
        private MemoryStream headerStream;
        internal MemoryStream HeaderStream => this.headerStream;
        private int _entryCount;
        public int EntryCount => this._entryCount;
        private bool _leaveOpen;

        internal int OffsetGlobal;
        
        private Entry[] _entries;
        public Entry[] Entries
        {
            get
            {
                if (this._entries == null)
                    this.ReadFileList();
                return this._entries;
            }
        }

        private bool _hasReadHeader;
        internal bool HasReadHeader => this._hasReadHeader;
        
        public static BnSDatArchive Read(string filename) => BnSDatArchive.Read(filename, false);
        public static BnSDatArchive Read(string filename, bool is64)
        {
            if (!File.Exists(filename))
                throw new FileNotFoundException("The dat file is not existed.", filename);
            FileStream fs = File.OpenRead(filename);
            return new BnSDatArchive(fs, false, is64);
        }
        public static BnSDatArchive Read(Stream stream) => BnSDatArchive.Read(stream, false);
        public static BnSDatArchive Read(Stream stream, bool leaveOpen) => BnSDatArchive.Read(stream, leaveOpen, false);
        public static BnSDatArchive Read(Stream stream, bool leaveOpen, bool is64)
        {
            if (!stream.CanRead)
                throw new InvalidOperationException("The stream should be readable");
            BnSDatArchive dat = new BnSDatArchive(stream, leaveOpen, is64);
            dat.ReadHeader();
            return dat;
        }

        private BnSDatArchive(Stream stream, bool leaveOpen, bool _is64bit)
        {
            this.Is64bit = _is64bit;
            this._leaveOpen = leaveOpen;
            this._hasReadHeader = false;
            this.originalStream = stream;
            this.binaryReader = new BinaryReader(stream);
            this.headerReaded = false;
        }

        private void ReadFileList()
        {
            using (IReader reader = this.GetEntriesReader())
            {
                this._entries = new Entry[this.EntryCount];
                for (int i = 0; i < this.EntryCount; i++)
                    if (reader.MoveToNextEntry())
                        this._entries[i] = reader.Entry;
            }
        }

        private bool headerReaded; // Yea, read"ed" is totally wrong, but .....
        internal void ReadHeader()
        {
            if (this.headerReaded) return;
            this.headerReaded = true;

            byte[] Signature = this.binaryReader.ReadBytes(8);
            uint Version = this.binaryReader.ReadUInt32();

            byte[] Unknown_001 = this.binaryReader.ReadBytes(5);
            int FileDataSizePacked = this.Is64bit ? (int)this.binaryReader.ReadInt64() : this.binaryReader.ReadInt32();

            this._entryCount = this.Is64bit ? (int)this.binaryReader.ReadInt64() : this.binaryReader.ReadInt32();

            bool IsCompressed = this.binaryReader.ReadBoolean();
            bool IsEncrypted = this.binaryReader.ReadBoolean();
            byte[] Unknown_002 = this.binaryReader.ReadBytes(62);
            int FileTableSizePacked = this.Is64bit ? (int)this.binaryReader.ReadInt64() : this.binaryReader.ReadInt32();
            int FileTableSizeUnpacked = this.Is64bit ? (int)this.binaryReader.ReadInt64() : this.binaryReader.ReadInt32();
            
            byte[] FileTableUnpacked = Unpack(this.binaryReader.ReadBytes(FileTableSizePacked), FileTableSizePacked, FileTableSizePacked, FileTableSizeUnpacked, IsEncrypted, IsCompressed);

            this.headerStream = new MemoryStream(FileTableUnpacked, false);

            this.OffsetGlobal = this.Is64bit ? (int)this.binaryReader.ReadInt64() : this.binaryReader.ReadInt32();
            this.OffsetGlobal = (int)this.originalStream.Position;

            this._hasReadHeader = true;
        }

        private IReader GetEntriesReader()
        {
            return new BnSDatReader(this);
        }

        public IReader ExtractAllEntries()
        {
            return this.GetEntriesReader();
        }

        private bool _disposed;
        public void Dispose()
        {
            if (this._disposed) return;
            this._disposed = true;
            this.headerStream.Dispose();
            if (!this._leaveOpen)
            {
                this.binaryReader.Dispose();
                this.originalStream.Dispose();
            }
        }

        internal static byte[] Decrypt(byte[] buffer, int size)
        {
            // AES requires buffer to consist of blocks with 16 bytes (each)
            // expand last block by padding zeros if required...
            // -> the encrypted data in BnS seems already to be aligned to blocks
            int sizePadded = size + RijndaelStream.AES_KEY.Length;
            byte[] output = new byte[sizePadded];
            byte[] tmp = new byte[sizePadded];
            buffer.CopyTo(tmp, 0);
            buffer = null;

            using (Rijndael aes = Rijndael.Create())
            {
                aes.Mode = CipherMode.ECB;
                using (ICryptoTransform decrypt = aes.CreateDecryptor(RijndaelStream.RAW_AES_KEY, new byte[16]))
                    decrypt.TransformBlock(tmp, 0, sizePadded, output, 0);
                tmp = output;
                output = new byte[size];
                Array.Copy(tmp, 0, output, 0, size);
                tmp = null;
            }

            return output;
        }

        internal static void UncompressBuffer(byte[] compressed, Stream outStream)
        {
            using (var input = new MemoryStream(compressed))
                UncompressBuffer(input, outStream);
        }

        internal static void UncompressBuffer(Stream compressedStream, Stream outStream)
        {
            using (var decompressor = new ZlibStream(compressedStream, CompressionMode.Decompress))
            using (Leayal.ByteBuffer buffer = new Leayal.ByteBuffer(1024))
            {
                int read = decompressor.Read(buffer, 0, buffer.Length);
                while (read > 0)
                {
                    outStream.Write(buffer, 0, read);
                    read = decompressor.Read(buffer, 0, buffer.Length);
                }
            }
        }

        internal static byte[] UncompressBuffer(byte[] compressed)
        {
            using (var input = new MemoryStream(compressed))
            using (var output = new RecyclableMemoryStream())
            using (var decompressor = new ZlibStream(input, CompressionMode.Decompress))
            using (Leayal.ByteBuffer buffer = new Leayal.ByteBuffer(1024))
            {
                int read = decompressor.Read(buffer, 0, buffer.Length);
                while (read > 0)
                {
                    output.Write(buffer, 0, read);
                    read = decompressor.Read(buffer, 0, buffer.Length);
                }
                return output.ToArray();
            }
        }

        internal static byte[] Deflate(byte[] buffer, int sizeCompressed, int sizeDecompressed)
        {
            using (RecyclableMemoryStream tmp = new RecyclableMemoryStream())
            {
                BnSDatArchive.UncompressBuffer(buffer, tmp);

                if (tmp.Length != sizeDecompressed)
                {
                    byte[] tmp2 = new byte[sizeDecompressed];

                    if (tmp.Length > sizeDecompressed)
                        Array.Copy(tmp.GetBuffer(), 0, tmp2, 0, sizeDecompressed);
                    else
                        Array.Copy(tmp.GetBuffer(), 0, tmp2, 0, tmp.Length);
                    return tmp2;
                }
                else
                    return tmp.ToArray();
            }
        }

        internal static byte[] Unpack(byte[] buffer, int sizeStored, int sizeSheared, int sizeUnpacked, bool isEncrypted, bool isCompressed)
        {
            byte[] output;

            if (isEncrypted && isCompressed)
            {
                output = Decrypt(buffer, sizeStored);
                output = Deflate(output, sizeSheared, sizeUnpacked);
            }
            else if (isEncrypted)
            {
                output = Decrypt(buffer, sizeStored);
            }
            else if (isCompressed)
            {
                output = Deflate(buffer, sizeSheared, sizeUnpacked);
            }
            else
            {
                output = new byte[sizeUnpacked];
                if (sizeSheared < sizeUnpacked)
                {
                    Array.Copy(buffer, 0, output, 0, sizeSheared);
                }
                else
                {
                    Array.Copy(buffer, 0, output, 0, sizeUnpacked);
                }
            }

            return output;
        }

        public void ExtractTo(string folder)
        {
            using (IReader reader = this.GetEntriesReader())
            {
                string filename;
                while (reader.MoveToNextEntry())
                    {
                        filename = Path.Combine(folder, reader.Entry.FilePath);
                        Microsoft.VisualBasic.FileIO.FileSystem.CreateDirectory(Microsoft.VisualBasic.FileIO.FileSystem.GetParentPath(filename));
                        using (FileStream outStream = File.Create(filename))
                        {
                            reader.ExtractTo(outStream);
                        }
                    }
            }
        }
    }
}
