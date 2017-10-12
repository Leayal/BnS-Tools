using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.IO;

namespace BnSDat
{
    class RijndaelStream : Stream
    {
        public const string AES_KEY = "bns_obt_kr_2014#";
        public readonly static byte[] RAW_AES_KEY = Encoding.ASCII.GetBytes(AES_KEY);

        public Stream BaseStream { get; }
        private Rijndael crypto;
        private CryptoStream cryptoStream;
        private ICryptoTransform icryptoTransform;
        private bool _leaveOpen;
        private CryptoStreamMode streamMode;

        public override bool CanRead => this.cryptoStream.CanRead;
        public override bool CanSeek => this.cryptoStream.CanSeek;
        public override bool CanWrite => this.cryptoStream.CanWrite;
        public override long Length => this.cryptoStream.Length;
        public override long Position { get => this.cryptoStream.Position; set => this.cryptoStream.Position = value; }
        public override bool CanTimeout => this.BaseStream.CanTimeout;
        public override int ReadTimeout => this.BaseStream.ReadTimeout;
        public override int WriteTimeout => this.BaseStream.WriteTimeout;

        public RijndaelStream(Stream source, CryptoStreamMode streamMode) : this(source, streamMode, true) { }
        public RijndaelStream(Stream source, CryptoStreamMode streamMode, bool leaveOpen)
        {
            this.BaseStream = source;
            this._leaveOpen = leaveOpen;
            this.streamMode = streamMode;
            this.crypto = Rijndael.Create();
            switch (streamMode)
            {
                case CryptoStreamMode.Read:
                    this.icryptoTransform = crypto.CreateDecryptor(RAW_AES_KEY, new byte[16]);
                    break;
                case CryptoStreamMode.Write:
                    this.icryptoTransform = crypto.CreateEncryptor(RAW_AES_KEY, new byte[16]);
                    break;
            }
            this.cryptoStream = new CryptoStream(this.BaseStream, this.icryptoTransform, streamMode);
        }

        /*
        private byte[] Decrypt(byte[] buffer, int size)
        {
            // AES requires buffer to consist of blocks with 16 bytes (each)
            // expand last block by padding zeros if required...
            // -> the encrypted data in BnS seems already to be aligned to blocks
            int sizePadded = size + AES_KEY.Length;
            byte[] output = new byte[sizePadded];



            using (Leayal.IO.RecyclableMemoryStream tmp = new Leayal.IO.RecyclableMemoryStream(string.Empty, sizePadded))
            using (Rijndael crypto = Rijndael.Create())
            {
                crypto.Mode = CipherMode.ECB;
                if (tmp.Length != sizePadded)
                    tmp.SetLength(sizePadded);
                tmp.Read(buffer, 0, buffer.Length);
                using (ICryptoTransform decrypt = crypto.CreateDecryptor(RAW_AES_KEY, new byte[16]))
                    decrypt.TransformBlock(tmp.GetBuffer(), 0, sizePadded, output, 0);
                tmp = output;
                output = new byte[size];
                Array.Copy(tmp, 0, output, 0, size);
            }
            tmp = null;

            return output;
        }

        private byte[] Encrypt(byte[] buffer, int size, out int sizePadded)
        {
            sizePadded = size + (AES_KEY.Length - (size % AES_KEY.Length));
            byte[] output = new byte[sizePadded];
            // byte[] temp = new byte[sizePadded];
            using (Leayal.IO.RecyclableMemoryStream tmp = new Leayal.IO.RecyclableMemoryStream(string.Empty, sizePadded))
            using (Rijndael crypto = Rijndael.Create())
            {
                crypto.Mode = CipherMode.ECB;
                if (tmp.Length != sizePadded)
                    tmp.SetLength(sizePadded);
                tmp.Write(buffer, 0, buffer.Length);
                using (ICryptoTransform encrypt = crypto.CreateEncryptor(RAW_AES_KEY, new byte[16]))
                    encrypt.TransformBlock(tmp.GetBuffer(), 0, sizePadded, output, 0);
            }

            return output;
        }//*/

        public override void Flush()
        {
            this.cryptoStream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return this.cryptoStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            this.cryptoStream.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.cryptoStream.Read(buffer, offset, count);
        }

        public override int ReadByte()
        {
            return this.cryptoStream.ReadByte();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.cryptoStream.Write(buffer, offset, count);
        }

        public override void WriteByte(byte value)
        {
            this.cryptoStream.WriteByte(value);
        }

        public override void Close()
        {
            this.Dispose();
        }

        public void FlushFinalBlock()
        {
            if (this.streamMode != CryptoStreamMode.Write)
                throw new InvalidOperationException();
            if (!this.cryptoStream.HasFlushedFinalBlock)
                this.cryptoStream.FlushFinalBlock();
        }

        private bool _disposed;
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!this._disposed)
                {
                    this._disposed = true;

                    if (streamMode == CryptoStreamMode.Write)
                    {
                        int paddingLength = Convert.ToInt32(this.BaseStream.Length + (AES_KEY.Length - (this.BaseStream.Length % AES_KEY.Length)));
                        using (Leayal.IO.RecyclableMemoryStream padding = new Leayal.IO.RecyclableMemoryStream(string.Empty, paddingLength))
                        {
                            if (padding.Length != paddingLength)
                                padding.SetLength(paddingLength);
                            this.cryptoStream.Write(padding.GetBuffer(), 0, paddingLength);
                            this.FlushFinalBlock();
                        }
                    }

                    this.cryptoStream.Dispose();
                    this.crypto.Dispose();
                    if (!this._leaveOpen)
                        this.BaseStream.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
