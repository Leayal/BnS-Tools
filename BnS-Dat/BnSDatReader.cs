using Leayal.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace BnSDat
{
    internal class BnSDatReader : IReader
    {
        private Leayal.ByteBuffer buffer;
        private BnSDatArchive _archive;
        private int entryIndex;
        private BinaryReader br;
        private bool _leaveOpen;
        internal BPKG_FTE fileTableEntry;
        private Entry _entry;
        public Entry Entry => this._entry;
        private ProgressEventArgs progresseventargs;

        public int BufferLength
        {
            get => this.buffer.Length;
            set
            {
                if (this.buffer != null)
                    this.buffer.Dispose();
                this.buffer = new Leayal.ByteBuffer(value);
            }
        }

        internal BnSDatReader(BnSDatArchive archive) : this(archive, true) { }
        internal BnSDatReader(BnSDatArchive archive, bool leaveOpen)
        {
            this._leaveOpen = leaveOpen;
            this._archive = archive;
            if (!this._archive.HasReadHeader)
                this._archive.ReadHeader();
            this._archive.HeaderStream.Position = 0;
            this.BufferLength = 1024;
            this.br = new BinaryReader(this._archive.HeaderStream);
            this.entryIndex = -1;
            this.progresseventargs = new ProgressEventArgs(0, this._archive.EntryCount);
        }

        private bool _disposed;
        public void Dispose()
        {
            if (this._disposed) return;
            this._disposed = true;
        }

        public bool MoveToNextEntry()
        {
            this.entryIndex++;
            if (this.entryIndex < this._archive.EntryCount)
            {
                fileTableEntry = new BPKG_FTE();
                fileTableEntry.FilePathLength = this._archive.Is64bit ? (int)this.br.ReadInt64() : this.br.ReadInt32();
                fileTableEntry.FilePath = Encoding.Unicode.GetString(this.br.ReadBytes(fileTableEntry.FilePathLength * 2));
                fileTableEntry.Unknown_001 = this.br.ReadByte();
                fileTableEntry.IsCompressed = this.br.ReadBoolean();
                fileTableEntry.IsEncrypted = this.br.ReadBoolean();
                fileTableEntry.Unknown_002 = this.br.ReadByte();
                fileTableEntry.FileDataSizeUnpacked = this._archive.Is64bit ? (int)this.br.ReadInt64() : this.br.ReadInt32();
                fileTableEntry.FileDataSizeSheared = this._archive.Is64bit ? (int)this.br.ReadInt64() : this.br.ReadInt32();
                fileTableEntry.FileDataSizeStored = this._archive.Is64bit ? (int)this.br.ReadInt64() : this.br.ReadInt32();
                fileTableEntry.FileDataOffset = (this._archive.Is64bit ? (int)this.br.ReadInt64() : this.br.ReadInt32()) + this._archive.OffsetGlobal;
                fileTableEntry.Padding = this.br.ReadBytes(60);

                this._entry = Entry.FromStruct(fileTableEntry);

                this.progresseventargs._current = this.entryIndex;
                this.OnTotalProgress(this.progresseventargs);

                return true;
            }
            else
                return false;
        }
        
        public void CopyEntryTo(BnSDatWriter writer)
        {
            using (EntryStream stream = new EntryStream(this._archive.OriginalStream, this.Entry.Raw.FileDataOffset, this.Entry.Raw.FileDataSizeStored, true))
                writer.AddNewEntry(this.Entry, stream);
        }

        public EntryStream GetEntryStream()
        {
            this._archive.OriginalStream.Position = fileTableEntry.FileDataOffset;
            Stream tmpStream = null;
            if (!fileTableEntry.IsCompressed && !fileTableEntry.IsEncrypted)
            {
                if (fileTableEntry.FileDataSizeSheared < fileTableEntry.FileDataSizeUnpacked)
                    tmpStream = new EntryStream(this._archive.OriginalStream, fileTableEntry.FileDataOffset, fileTableEntry.FileDataSizeSheared, true);
                else
                    tmpStream = new EntryStream(this._archive.OriginalStream, fileTableEntry.FileDataOffset, fileTableEntry.FileDataSizeUnpacked, true);
            }
            else
            {
                using (RecyclableMemoryStream packed = new RecyclableMemoryStream() { Capacity = fileTableEntry.FileDataSizeStored })
                {
                    tmpStream = new RecyclableMemoryStream() { Capacity = fileTableEntry.FileDataSizeUnpacked };
                    for (int i = 0; i < fileTableEntry.FileDataSizeStored; i++)
                        packed.WriteByte(this._archive.BinaryReader.ReadByte());
                    packed.Position = 0;
                    this.Unpack(packed, tmpStream, fileTableEntry.FileDataSizeStored, fileTableEntry.FileDataSizeSheared, fileTableEntry.FileDataSizeUnpacked, fileTableEntry.IsEncrypted, fileTableEntry.IsCompressed);
                    tmpStream.Position = 0;
                }
            }

            if (this.Entry.FilePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || this.Entry.FilePath.EndsWith("x16", StringComparison.OrdinalIgnoreCase))
            {
                // decode bxml
                Leayal.IO.RecyclableMemoryStream temp = new Leayal.IO.RecyclableMemoryStream();
                BXML bns_xml = new BXML(BXML.XOR_KEY_Data);
                BXML.Convert(tmpStream, bns_xml.DetectType(tmpStream), temp, BXML_TYPE.BXML_PLAIN);
                tmpStream.Dispose();
                temp.Position = 0;
                tmpStream = temp;
            }

            if (tmpStream is RecyclableMemoryStream)
                return new EntryStream(tmpStream, 0, tmpStream.Length, false);
            else
                return (EntryStream)tmpStream;
        }

        private void Deflate(Stream inStream, Stream outStream)
        {
            BnSDatArchive.UncompressBuffer(inStream, outStream);
        }

        private void Unpack(MemoryStream inStream, Stream outStream, int sizeStored, int sizeSheared, int sizeUnpacked, bool isEncrypted, bool isCompressed)
        {
            byte[] output;

            if (isEncrypted && isCompressed)
            {
                output = this.Decrypt(inStream.GetBuffer(), inStream.Length, sizeStored);
                using (MemoryStream memStream = new MemoryStream(output, false))
                    Deflate(memStream, outStream);
                outStream.Flush();
            }
            else if (isEncrypted)
            {
                output = this.Decrypt(inStream.GetBuffer(), inStream.Length, sizeStored);
                outStream.Write(output, 0, output.Length);
                outStream.Flush();
            }
            else if (isCompressed)
            {
                Deflate(inStream, outStream);
                outStream.Flush();
            }
            else
            {
                outStream.Write(buffer, 0, buffer.Length);
                outStream.Flush();
            }
        }

        private byte[] Decrypt(byte[] buffer, long bufferLength, int size)
        {
            // AES requires buffer to consist of blocks with 16 bytes (each)
            // expand last block by padding zeros if required...
            // -> the encrypted data in BnS seems already to be aligned to blocks
            int sizePadded = size + RijndaelStream.AES_KEY.Length;
            byte[] output = new byte[sizePadded];

            byte[] tmp = new byte[sizePadded];


            unsafe
            {
                fixed (byte* source = buffer, dest = tmp)
                {
                    for (int i = 0; i < bufferLength; i++)
                        dest[i] = source[i];
                    
                    using (Rijndael aes = Rijndael.Create())
                    {
                        aes.Mode = CipherMode.ECB;
                        using (ICryptoTransform decrypt = aes.CreateDecryptor(RijndaelStream.RAW_AES_KEY, new byte[16]))
                            decrypt.TransformBlock(tmp, 0, sizePadded, output, 0);
                    }
                }
            }

            tmp = output;
            output = new byte[size];

            unsafe
            {
                fixed (byte* source = tmp, dest = output)
                {
                    for (int i = 0; i < size; i++)
                        dest[i] = source[i];
                }
            }
            
            return output;
        }

        public virtual void ExtractTo(Stream stream)
        {
            using (Stream entryStream = this.GetEntryStream())
            {
                EntryExtractingEventArgs eventArgs = new EntryExtractingEventArgs(0, this.Entry.FileDataSizeUnpacked);
                int byteread = entryStream.Read(this.buffer, 0, this.buffer.Length);
                eventArgs._extractedBytes = 0;
                while (byteread > 0)
                {
                    stream.Write(this.buffer, 0, byteread);
                    eventArgs._extractedBytes += byteread;

                    // Report progress
                    this.OnEntryExtracting(eventArgs);

                    byteread = entryStream.Read(this.buffer, 0, this.buffer.Length);
                }
                stream.Flush();
            }
        }

        public event EventHandler<ProgressEventArgs> TotalProgress;
        protected virtual void OnTotalProgress(ProgressEventArgs e)
        {
            this.TotalProgress?.Invoke(this, e);
        }

        public event EventHandler<EntryExtractingEventArgs> EntryExtracting;
        protected virtual void OnEntryExtracting(EntryExtractingEventArgs e)
        {
            this.EntryExtracting?.Invoke(this, e);
        }
    }
}
