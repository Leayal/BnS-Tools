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
        //public const string AES_KEY = "bns_obt_kr_2014#";
        public const string AES_KEY = "ja#n_2@020_compl";
        public readonly static byte[] RAW_AES_KEY = Encoding.ASCII.GetBytes(AES_KEY);

        public Stream BaseStream { get; }
        private Rijndael crypto;
        private ICryptoTransform icryptoTransform;
        private bool _leaveOpen;
        private CryptoStreamMode streamMode;
        private Leayal.ByteBuffer cryptoBuffer, dataBuffer;

        public override bool CanRead { get; }
        public override bool CanSeek => this.BaseStream.CanSeek;
        public override bool CanWrite { get; }
        public override long Length => this.BaseStream.Length;
        public override long Position { get => this.BaseStream.Position; set => this.BaseStream.Position = value; }
        public override bool CanTimeout => this.BaseStream.CanTimeout;
        public override int ReadTimeout => this.BaseStream.ReadTimeout;
        public override int WriteTimeout => this.BaseStream.WriteTimeout;

        /// <summary>
        /// Initialize an CryptoStream which dedicated for BnS encryption.
        /// </summary>
        /// <param name="source">The source stream of raw data</param>
        /// <param name="streamMode">Determine the stream is decrypt or encrypt</param>
        public RijndaelStream(Stream source, CryptoStreamMode streamMode) : this(source, streamMode, true) { }
        /// <summary>
        /// Initialize an CryptoStream which dedicated for BnS encryption.
        /// </summary>
        /// <param name="source">The source stream of raw data</param>
        /// <param name="streamMode">Determine the stream is decrypt or encrypt</param>
        /// <param name="cryptoBufferSize">Decrypt and encrypt buffer's size. Must be a factor of 16</param>
        public RijndaelStream(Stream source, CryptoStreamMode streamMode, int cryptoBufferSize) : this(source, streamMode, cryptoBufferSize, true) { }
        /// <summary>
        /// Initialize an CryptoStream which dedicated for BnS encryption.
        /// </summary>
        /// <param name="source">The source stream of raw data</param>
        /// <param name="streamMode">Determine the stream is decrypt or encrypt</param>
        /// <param name="leaveOpen">Determine if the source stream will not be disposed when this stream disposed.</param>
        public RijndaelStream(Stream source, CryptoStreamMode streamMode, bool leaveOpen) : this(source, streamMode, 512, leaveOpen) { }
        /// <summary>
        /// Initialize an CryptoStream which dedicated for BnS encryption.
        /// </summary>
        /// <param name="source">The source stream of raw data</param>
        /// <param name="streamMode">Determine the stream is decrypt or encrypt</param>
        /// <param name="cryptoBufferSize">Decrypt and encrypt buffer's size. Must be a factor of 16</param>
        /// <param name="leaveOpen">Determine if the source stream will not be disposed when this stream disposed.</param>
        public RijndaelStream(Stream source, CryptoStreamMode streamMode, int cryptoBufferSize, bool leaveOpen)
        {
            if ((cryptoBufferSize % 16) != 0)
                throw new ArgumentException("cryptoBufferSize must have a factor of 16.");
            this.cryptoBuffer = new Leayal.ByteBuffer(cryptoBufferSize);
            this.dataBuffer = new Leayal.ByteBuffer(cryptoBufferSize);
            this.BaseStream = source;
            this._leaveOpen = leaveOpen;
            this.streamMode = streamMode;
            this.crypto = Rijndael.Create();
            this.crypto.Mode = CipherMode.ECB;
            switch (streamMode)
            {
                case CryptoStreamMode.Read:
                    if (!source.CanRead)
                        throw new ArgumentException("The stream should be readable.");
                    this.CanWrite = false;
                    this.CanRead = true;
                    this.icryptoTransform = crypto.CreateDecryptor(RAW_AES_KEY, new byte[16]);
                    break;
                case CryptoStreamMode.Write:
                    if (!source.CanWrite)
                        throw new ArgumentException("The stream should be writable.");
                    this.CanWrite = true;
                    this.CanRead = false;
                    this.icryptoTransform = crypto.CreateEncryptor(RAW_AES_KEY, new byte[16]);
                    break;
            }
        }

        public override void Flush()
        {
            this.BaseStream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return this.BaseStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            if (!this.CanWrite)
                throw new InvalidOperationException();
            this.BaseStream.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!this.CanRead)
                throw new InvalidOperationException();

            if ((offset + count) > buffer.Length)
                throw new ArgumentException();
            
            // Yea, again, dataread"ed" is not even English.

            if (count < this.dataBuffer.Length)
            {
                int datareaded = this.BaseStream.Read(this.dataBuffer, offset, count);
                if (datareaded > 0)
                {
                    this.ProcessDataCrypto(this.dataBuffer, this.cryptoBuffer);
                    unsafe
                    {
                        fixed (byte* src = this.cryptoBuffer.GetBuffer(), dst = buffer)
                        {
                            for (int i = 0; i < datareaded; i++)
                                dst[offset + i] = src[i];
                        }
                    }
                }
                return datareaded;
            }
            else if (count > this.dataBuffer.Length)
            {
                int howmanytimetoread = count / this.dataBuffer.Length,
                    datareaded, totaldataread = 0;
                int virtualOffset = offset;
                for (int i = 0; i < howmanytimetoread; i++)
                {
                    datareaded = this.BaseStream.Read(this.dataBuffer, 0, this.dataBuffer.Length);
                    if (datareaded > 0)
                    {
                        virtualOffset += (this.dataBuffer.Length * i);
                        totaldataread += datareaded;
                        this.ProcessDataCrypto(this.dataBuffer, this.cryptoBuffer);
                        unsafe
                        {
                            fixed (byte* src = this.cryptoBuffer.GetBuffer(), dst = buffer)
                            {
                                for (int index = 0; index < this.dataBuffer.Length; index++)
                                    dst[virtualOffset + index] = src[index];
                            }
                        }
                    }
                    else
                        break;
                }
                return totaldataread;
            }
            else
            {
                int datareaded = this.BaseStream.Read(this.dataBuffer, 0, this.dataBuffer.Length);
                this.ProcessDataCrypto(this.dataBuffer, buffer);
                return datareaded;
            }
        }

        /// <summary>
        /// This thing is here for compatible reason. But you should NOT use it because of performance reason. Use <see cref="Read(byte[], int, int)"/> instead.
        /// </summary>
        /// <returns>Byte</returns>
        public override int ReadByte()
        {
            if (!this.CanRead)
                throw new InvalidOperationException();
            byte read = (byte)this.BaseStream.ReadByte();
            this.dataBuffer.GetBuffer()[0] = read;
            this.ProcessDataCrypto(this.dataBuffer, this.cryptoBuffer);
            return this.cryptoBuffer.GetBuffer()[0];
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!this.CanWrite)
                throw new InvalidOperationException();
            if (count < this.dataBuffer.Length)
            {
                unsafe
                {
                    fixed (byte* src = buffer, dst = this.dataBuffer.GetBuffer())
                    {
                        for (int i = 0; i < count; i++)
                            dst[i] = src[offset + i];
                    }
                }
                this.ProcessDataCrypto(this.dataBuffer, this.cryptoBuffer);
                this.BaseStream.Write(this.cryptoBuffer, 0, count);
            }
            else if (count > this.dataBuffer.Length)
            {
                int howmuchleftToWrite = count;
                int virtualoffset = offset;
                while (howmuchleftToWrite > this.dataBuffer.Length)
                {
                    this.Write(buffer, virtualoffset, this.dataBuffer.Length);
                    howmuchleftToWrite -= this.dataBuffer.Length;
                    virtualoffset += this.dataBuffer.Length;
                }
                if (howmuchleftToWrite > 0)
                    this.Write(buffer, virtualoffset, howmuchleftToWrite);
            }
            else
            {
                this.ProcessDataCrypto(buffer, offset, this.cryptoBuffer);
                this.BaseStream.Write(this.cryptoBuffer, 0, count);
            }
        }

        /// <summary>
        /// This thing is here for compatible reason. But you should NOT use it because of performance reason. Use <see cref="Write(byte[], int, int)"/> instead.
        /// </summary>
        /// <param name="value"></param>
        public override void WriteByte(byte value)
        {
            if (!this.CanWrite)
                throw new InvalidOperationException();
            
            this.dataBuffer.GetBuffer()[0] = value;
            this.ProcessDataCrypto(this.dataBuffer, this.cryptoBuffer);
            this.BaseStream.WriteByte(this.cryptoBuffer.GetBuffer()[0]);
        }

        private void ProcessDataCrypto(byte[] buffer, byte[] outbuffer)
        {
            this.ProcessDataCrypto(buffer, 0, outbuffer);
        }

        private void ProcessDataCrypto(byte[] buffer, int offset, byte[] outbuffer)
        {
            this.icryptoTransform.TransformBlock(buffer, offset, buffer.Length, outbuffer, 0);
        }

        public override void Close()
        {
            if (this._disposed) return;
            this._disposed = true;
            this.dataBuffer.Dispose();
            this.cryptoBuffer.Dispose();
            this.icryptoTransform.Dispose();
            this.crypto.Dispose();
            if (!this._leaveOpen)
                this.BaseStream.Dispose();
        }

        private bool _disposed;
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.Close();
            }
            base.Dispose(disposing);
        }
    }
}
