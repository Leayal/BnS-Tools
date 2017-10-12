using System;
using SharpCompress.Compressors.Deflate;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using Leayal.IO;
using System.Xml;
using System.Collections.Generic;

namespace BnSDat
{
    public class BNSDat_raw
    {
        private byte[] Decrypt(Stream stream, int size)
        {
            // AES requires buffer to consist of blocks with 16 bytes (each)
            // expand last block by padding zeros if required...
            // -> the encrypted data in BnS seems already to be aligned to blocks
            int sizePadded = size + AES_KEY.Length;
            byte[] output = new byte[sizePadded];
            byte[] tmp = new byte[sizePadded];
            stream.Read(tmp, 0, size);

            using (Rijndael aes = Rijndael.Create())
            {
                aes.Mode = CipherMode.ECB;
                using (ICryptoTransform decrypt = aes.CreateDecryptor(Encoding.ASCII.GetBytes(AES_KEY), new byte[16]))
                    decrypt.TransformBlock(tmp, 0, sizePadded, output, 0);
                tmp = output;
                output = new byte[size];
                Array.Copy(tmp, 0, output, 0, size);
            }
            tmp = null;

            return output;
        }

        /*
        private byte[] No_Deflate(byte[] buffer, int sizeCompressed, int sizeDecompressed)
        {
            byte[] tmp = Ionic.Zlib.ZlibStream.UncompressBuffer(buffer);

            if (tmp.Length != sizeDecompressed)
            {
                byte[] tmp2 = new byte[sizeDecompressed];

                if (tmp.Length > sizeDecompressed)
                    Array.Copy(tmp, 0, tmp2, 0, sizeDecompressed);
                else
                    Array.Copy(tmp, 0, tmp2, 0, tmp.Length);
                tmp = tmp2;
                tmp2 = null;
            }
            return tmp;
        }//*/

        private byte[] Deflate(Stream rawstream, int sizeCompressed, int sizeDecompressed)
        {
            using (ZlibStream zlibStream = new ZlibStream(rawstream, SharpCompress.Compressors.CompressionMode.Decompress, true))
            {
                byte[] result = new byte[sizeDecompressed];
                zlibStream.Read(result, 0, sizeCompressed);
                return result;
            }
        }

        private byte[] Unpack(Stream stream, int sizeStored, int sizeSheared, int sizeUnpacked, bool isEncrypted, bool isCompressed)
        {
            byte[] output;

            if (isEncrypted)
                output = Decrypt(stream, sizeStored);
            if (isCompressed)
                output = Deflate(stream, sizeSheared, sizeUnpacked);
            
            {
                output = new byte[sizeUnpacked];
                if (sizeSheared < sizeUnpacked)
                    stream.Read(output, 0, sizeSheared);
                else
                    stream.Read(output, 0, sizeUnpacked);
            }

            return output;
        }

        private byte[] No_Inflate(byte[] buffer, int sizeDecompressed, out int sizeCompressed, int compressionLevel)
        {
            MemoryStream output = new MemoryStream();
            Ionic.Zlib.ZlibStream zs = new Ionic.Zlib.ZlibStream(output, Ionic.Zlib.CompressionMode.Compress, (Ionic.Zlib.CompressionLevel)compressionLevel, true);
            zs.Write(buffer, 0, sizeDecompressed);
            zs.Flush();
            zs.Close();
            sizeCompressed = (int)output.Length;
            return output.ToArray();
        }

        private byte[] Encrypt(byte[] buffer, int size, out int sizePadded)
        {
            int AES_BLOCK_SIZE = AES_KEY.Length;
            sizePadded = size + (AES_BLOCK_SIZE - (size % AES_BLOCK_SIZE));
            byte[] output = new byte[sizePadded];
            byte[] temp = new byte[sizePadded];
            Array.Copy(buffer, 0, temp, 0, buffer.Length);
            buffer = null;
            Rijndael aes = Rijndael.Create();
            aes.Mode = CipherMode.ECB;

            ICryptoTransform encrypt = aes.CreateEncryptor(Encoding.ASCII.GetBytes(AES_KEY), new byte[16]);
            encrypt.TransformBlock(temp, 0, sizePadded, output, 0);
            temp = null;
            return output;
        }

        private byte[] Pack(byte[] buffer, int sizeUnpacked, out int sizeSheared, out int sizeStored, bool encrypt, bool compress, int compressionLevel)
        {
            byte[] output = buffer;
            buffer = null;
            sizeSheared = sizeUnpacked;
            sizeStored = sizeSheared;

            if (compress)
            {
                byte[] tmp = Inflate(output, sizeUnpacked, out sizeSheared, compressionLevel);
                sizeStored = sizeSheared;
                output = tmp;
                tmp = null;
            }

            if (encrypt)
            {
                byte[] tmp = Encrypt(output, sizeSheared, out sizeStored);
                output = tmp;
                tmp = null;
            }
            return output;
        }

        private Stream originalStream;
        internal Stream OriginalStream => this.originalStream;
        private BinaryReader binaryReader;
        internal BinaryReader BinaryReader => this.binaryReader;
        public bool Is64bit { get; }
        private MemoryStream headerStream;
        internal MemoryStream HeaderStream => this.headerStream;
        private int _entryCount;
        public int EntryCount => this._entryCount;

        internal int OffsetGlobal;

        internal List<BPKG_FTE> entrylist;
        private Entry[] _entries;
        public Entry[] Entries => this._entries;

        private bool _hasReadHeader;
        internal bool HasReadHeader => this._hasReadHeader;

        public static BNSDat Read(Stream stream) => BNSDat.Read(stream, false);
        public static BNSDat Read(Stream stream, bool is64)
        {
            BNSDat dat = new BNSDat(stream, is64);
            dat.ReadHeader();
            return dat;
        }

        private BNSDat_raw(Stream stream, bool _is64bit)
        {
            this.Is64bit = _is64bit;
            this._hasReadHeader = false;
            this.originalStream = stream;
            this.binaryReader = new BinaryReader(stream);
        }

        internal void ReadHeader()
        {
            byte[] Signature = this.binaryReader.ReadBytes(8);
            uint Version = this.binaryReader.ReadUInt32();

            byte[] Unknown_001 = this.binaryReader.ReadBytes(5);
            int FileDataSizePacked = this.Is64bit ? (int)this.binaryReader.ReadInt64() : this.binaryReader.ReadInt32();

            this._entryCount = this.Is64bit ? (int)this.binaryReader.ReadInt64() : this.binaryReader.ReadInt32();
            this.entrylist = new List<BPKG_FTE>(this._entryCount);
            this._entries = new Entry[this._entryCount];

            bool IsCompressed = this.binaryReader.ReadByte() != 0;
            bool IsEncrypted = this.binaryReader.ReadByte() != 0;
            byte[] Unknown_002 = this.binaryReader.ReadBytes(62);
            int FileTableSizePacked = this.Is64bit ? (int)this.binaryReader.ReadInt64() : this.binaryReader.ReadInt32();
            int FileTableSizeUnpacked = this.Is64bit ? (int)this.binaryReader.ReadInt64() : this.binaryReader.ReadInt32();

            byte[] FileTableUnpacked = Unpack(this.originalStream, FileTableSizePacked, FileTableSizePacked, FileTableSizeUnpacked, IsEncrypted, IsCompressed);

            this.headerStream = new MemoryStream(FileTableUnpacked, false);

            this.OffsetGlobal = this.Is64bit ? (int)this.binaryReader.ReadInt64() : this.binaryReader.ReadInt32();
            this.OffsetGlobal = (int)this.originalStream.Position;
            
            this._hasReadHeader = true;
        }

        public void Extract(string FileName, string outputFolder, bool is64 = false)
        {
            using (FileStream fs = new FileStream(FileName, FileMode.Open))
            using (BinaryReader br = new BinaryReader(fs))
            {
                string file_path;
                byte[] buffer_packed;
                byte[] buffer_unpacked;

                byte[] Signature = br.ReadBytes(8);
                uint Version = br.ReadUInt32();

                byte[] Unknown_001 = br.ReadBytes(5);
                int FileDataSizePacked = is64 ? (int)br.ReadInt64() : br.ReadInt32();
                int FileCount = is64 ? (int)br.ReadInt64() : br.ReadInt32();
                bool IsCompressed = br.ReadByte() == 1;
                bool IsEncrypted = br.ReadByte() == 1;
                byte[] Unknown_002 = br.ReadBytes(62);
                int FileTableSizePacked = is64 ? (int)br.ReadInt64() : br.ReadInt32();
                int FileTableSizeUnpacked = is64 ? (int)br.ReadInt64() : br.ReadInt32();

                buffer_packed = br.ReadBytes(FileTableSizePacked);
                int OffsetGlobal = is64 ? (int)br.ReadInt64() : br.ReadInt32();
                OffsetGlobal = (int)br.BaseStream.Position; // don't trust value, read current stream location.

                byte[] FileTableUnpacked = Unpack(buffer_packed, FileTableSizePacked, FileTableSizePacked, FileTableSizeUnpacked, IsEncrypted, IsCompressed);
                buffer_packed = null;
                MemoryStream ms = new MemoryStream(FileTableUnpacked);
                BinaryReader br2 = new BinaryReader(ms);

                for (int i = 0; i < FileCount; i++)
                {
                    BPKG_FTE FileTableEntry = new BPKG_FTE();
                    FileTableEntry.FilePathLength = is64 ? (int)br2.ReadInt64() : br2.ReadInt32();
                    FileTableEntry.FilePath = Encoding.Unicode.GetString(br2.ReadBytes(FileTableEntry.FilePathLength * 2));
                    FileTableEntry.Unknown_001 = br2.ReadByte();
                    FileTableEntry.IsCompressed = br2.ReadByte() == 1;
                    FileTableEntry.IsEncrypted = br2.ReadByte() == 1;
                    FileTableEntry.Unknown_002 = br2.ReadByte();
                    FileTableEntry.FileDataSizeUnpacked = is64 ? (int)br2.ReadInt64() : br2.ReadInt32();
                    FileTableEntry.FileDataSizeSheared = is64 ? (int)br2.ReadInt64() : br2.ReadInt32();
                    FileTableEntry.FileDataSizeStored = is64 ? (int)br2.ReadInt64() : br2.ReadInt32();
                    FileTableEntry.FileDataOffset = (is64 ? (int)br2.ReadInt64() : br2.ReadInt32()) + OffsetGlobal;
                    FileTableEntry.Padding = br2.ReadBytes(60);

                    file_path = Path.Combine(outputFolder, FileTableEntry.FilePath);
                    Microsoft.VisualBasic.FileIO.FileSystem.CreateDirectory(Microsoft.VisualBasic.FileIO.FileSystem.GetParentPath(file_path));

                    br.BaseStream.Position = FileTableEntry.FileDataOffset;
                    buffer_packed = br.ReadBytes(FileTableEntry.FileDataSizeStored);
                    buffer_unpacked = Unpack(buffer_packed, FileTableEntry.FileDataSizeStored, FileTableEntry.FileDataSizeSheared, FileTableEntry.FileDataSizeUnpacked, FileTableEntry.IsEncrypted, FileTableEntry.IsCompressed);
                    buffer_packed = null;
                    FileTableEntry = null;

                    if (file_path.EndsWith("xml") || file_path.EndsWith("x16"))
                    {
                        // decode bxml
                        MemoryStream temp = new MemoryStream();
                        MemoryStream temp2 = new MemoryStream(buffer_unpacked);
                        Convert(temp2, BXML.DetectType(temp2), temp, BXML_TYPE.BXML_PLAIN);
                        temp2.Close();
                        File.WriteAllBytes(file_path, temp.ToArray());
                        temp.Close();
                        buffer_unpacked = null;
                    }
                    else
                    {
                        // extract raw
                        File.WriteAllBytes(file_path, buffer_unpacked);
                        buffer_unpacked = null;
                    }

                    // Report progress
                    string whattosend = "Extracting: " + i.ToString() + "/" + FileCount.ToString();
                    Revamped_BnS_Buddy.Form1.CurrentForm.SortOutputHandler(whattosend);
                    // End report progress
                }

                // Report job done
                Revamped_BnS_Buddy.Form1.CurrentForm.SortOutputHandler("Done!");
                // End report

                br2.Close();
                ms.Close();
                br2 = null;
                ms = null;
            }
        }

        public void Compress(string Folder, bool is64 = false, int compression = 9)
        {
            string file_path;
            byte[] buffer_packed;
            byte[] buffer_unpacked;

            string[] files = Directory.EnumerateFiles(Folder, "*", SearchOption.AllDirectories).ToArray();

            int FileCount = files.Count();

            BPKG_FTE FileTableEntry = new BPKG_FTE();
            MemoryStream mosTablems = new MemoryStream();
            BinaryWriter mosTable = new BinaryWriter(mosTablems);
            MemoryStream mosFilesms = new MemoryStream();
            BinaryWriter mosFiles = new BinaryWriter(mosFilesms);

            for (int i = 0; i < FileCount; i++)
            {
                file_path = files[i].Replace(Folder, "").TrimStart('\\');
                FileTableEntry.FilePathLength = file_path.Length;

                if (is64)
                    mosTable.Write((long)FileTableEntry.FilePathLength);
                else
                    mosTable.Write(FileTableEntry.FilePathLength);

                FileTableEntry.FilePath = file_path;
                mosTable.Write(Encoding.Unicode.GetBytes(FileTableEntry.FilePath));
                FileTableEntry.Unknown_001 = 2;
                mosTable.Write(FileTableEntry.Unknown_001);
                FileTableEntry.IsCompressed = true;
                mosTable.Write(FileTableEntry.IsCompressed);
                FileTableEntry.IsEncrypted = true;
                mosTable.Write(FileTableEntry.IsEncrypted);
                FileTableEntry.Unknown_002 = 0;
                mosTable.Write(FileTableEntry.Unknown_002);

                FileStream fis = new FileStream(files[i], FileMode.Open);
                MemoryStream tmp = new MemoryStream();

                if (file_path.EndsWith(".xml") || file_path.EndsWith(".x16"))
                {
                    // encode bxml
                    BXML bxml = new BXML(XOR_KEY);
                    Convert(fis, bxml.DetectType(fis), tmp, BXML_TYPE.BXML_BINARY);
                }
                else
                {
                    // compress raw
                    fis.CopyTo(tmp);
                }
                fis.Close();
                fis = null;

                FileTableEntry.FileDataOffset = (int)mosFiles.BaseStream.Position;
                FileTableEntry.FileDataSizeUnpacked = (int)tmp.Length;

                if (is64)
                    mosTable.Write((long)FileTableEntry.FileDataSizeUnpacked);
                else
                    mosTable.Write(FileTableEntry.FileDataSizeUnpacked);

                buffer_unpacked = tmp.ToArray();
                tmp.Close();
                tmp = null;
                buffer_packed = Pack(buffer_unpacked, FileTableEntry.FileDataSizeUnpacked, out FileTableEntry.FileDataSizeSheared, out FileTableEntry.FileDataSizeStored, FileTableEntry.IsEncrypted, FileTableEntry.IsCompressed, compression);
                buffer_unpacked = null;
                mosFiles.Write(buffer_packed);
                buffer_packed = null;

                if (is64)
                    mosTable.Write((long)FileTableEntry.FileDataSizeSheared);
                else
                    mosTable.Write(FileTableEntry.FileDataSizeSheared);

                if (is64)
                    mosTable.Write((long)FileTableEntry.FileDataSizeStored);
                else
                    mosTable.Write(FileTableEntry.FileDataSizeStored);

                if (is64)
                    mosTable.Write((long)FileTableEntry.FileDataOffset);
                else
                    mosTable.Write(FileTableEntry.FileDataOffset);

                FileTableEntry.Padding = new byte[60];
                mosTable.Write(FileTableEntry.Padding);

                // Report progress
                string whattosend = "Compiling: " + i.ToString() + "/" + FileCount.ToString();
                Revamped_BnS_Buddy.Form1.CurrentForm.SortOutputHandler(whattosend);
                // End report progress
            }

            // Report job done
            Revamped_BnS_Buddy.Form1.CurrentForm.SortOutputHandler("Packing!");
            // End report

            MemoryStream output = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(output);
            byte[] Signature = new byte[8] { (byte)'U', (byte)'O', (byte)'S', (byte)'E', (byte)'D', (byte)'A', (byte)'L', (byte)'B' };
            bw.Write(Signature);
            int Version = 2;
            bw.Write(Version);
            byte[] Unknown_001 = new byte[5] { 0, 0, 0, 0, 0 };
            bw.Write(Unknown_001);
            int FileDataSizePacked = (int)mosFiles.BaseStream.Length;

            if (is64)
            {
                bw.Write((long)FileDataSizePacked);
                bw.Write((long)FileCount);
            }
            else
            {
                bw.Write(FileDataSizePacked);
                bw.Write(FileCount);
            }

            bool IsCompressed = true;
            bw.Write(IsCompressed);
            bool IsEncrypted = true;
            bw.Write(IsEncrypted);
            byte[] Unknown_002 = new byte[62];
            bw.Write(Unknown_002);

            int FileTableSizeUnpacked = (int)mosTable.BaseStream.Length;
            int FileTableSizeSheared = FileTableSizeUnpacked;
            int FileTableSizePacked = FileTableSizeUnpacked;
            buffer_unpacked = mosTablems.ToArray();
            mosTable.Close();
            mosTablems.Close();
            mosTable = null;
            mosTablems = null;
            buffer_packed = Pack(buffer_unpacked, FileTableSizeUnpacked, out FileTableSizeSheared, out FileTableSizePacked, IsEncrypted, IsCompressed, compression);
            buffer_unpacked = null;

            if (is64)
                bw.Write((long)FileTableSizePacked);
            else
                bw.Write(FileTableSizePacked);

            if (is64)
                bw.Write((long)FileTableSizeUnpacked);
            else
                bw.Write(FileTableSizeUnpacked);

            bw.Write(buffer_packed);
            buffer_packed = null;

            int OffsetGlobal = (int)output.Position + (is64 ? 8 : 4);

            if (is64)
                bw.Write((long)OffsetGlobal);
            else
                bw.Write(OffsetGlobal);

            buffer_packed = mosFilesms.ToArray();
            mosFiles.Close();
            mosFilesms.Close();
            mosFiles = null;
            mosFilesms = null;
            bw.Write(buffer_packed);
            buffer_packed = null;
            File.WriteAllBytes(Folder.Replace(".files", ""), output.ToArray());
            bw.Close();
            output.Close();
            bw = null;
            output = null;

            // Report job done
            Revamped_BnS_Buddy.Form1.CurrentForm.SortOutputHandler("Done!");
            // End report
        }

        private void Convert(Stream iStream, BXML_TYPE iType, Stream oStream, BXML_TYPE oType)
        {
            if ((iType == BXML_TYPE.BXML_PLAIN && oType == BXML_TYPE.BXML_BINARY) || (iType == BXML_TYPE.BXML_BINARY && oType == BXML_TYPE.BXML_PLAIN))
            {
                BXML bns_xml = new BXML(XOR_KEY);
                bns_xml.Load(iStream, iType);
                bns_xml.Save(oStream, oType);
            }
            else
            {
                iStream.CopyTo(oStream);
            }
        }
    }
}
