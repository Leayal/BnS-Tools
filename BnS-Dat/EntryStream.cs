using System;
using System.ComponentModel;
using System.IO;

namespace BnSDat
{
    public class EntryStream : Stream
    {
        private bool _leaveOpen;
        internal Stream Basestream { get; }
        public override bool CanRead => this.Basestream.CanRead;
        public override bool CanSeek => this.Basestream.CanSeek;
        public override bool CanWrite => this.Basestream.CanWrite;
        public override bool CanTimeout => this.Basestream.CanTimeout;
        public override int ReadTimeout { get => this.Basestream.ReadTimeout; set => this.Basestream.ReadTimeout = value; }
        public override int WriteTimeout { get => this.Basestream.WriteTimeout; set => this.Basestream.WriteTimeout = value; }

        private long _fixedLength;
        private long _offset;
        public override long Length => this._fixedLength;
        public override long Position
        {
            get => (this.Basestream.Position - this._offset);
            set
            {
                if (value < 0 || value > this.Length)
                    throw new InvalidOperationException();
                this.Basestream.Position = this._offset + value;
            }
        }

        public bool EndOfStream => !(this.Position < this.Length);

        internal EntryStream(Stream source, long offset, long entryPackedSize, bool leaveOpen)
        {
            this._leaveOpen = leaveOpen;
            this._offset = offset;
            this._fixedLength = entryPackedSize;
            this.Basestream = source;
        }

        public override void Flush()
        {
            this.Basestream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return this.Basestream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            this.Basestream.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            long howmuchleft = (this.Length - this.Position);
            if (howmuchleft < count)
                return this.Basestream.Read(buffer, offset, (int)howmuchleft);
            else
                return this.Basestream.Read(buffer, offset, count);
        }

        public override int ReadByte()
        {
            if (this.Position < this.Length)
                return this.Basestream.ReadByte();
            else
                return -1;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.Basestream.Write(buffer, offset, count);
        }

        public byte[] ReadToEnd()
        {
            byte[] result = new byte[this.Length];
            this.Basestream.Read(result, 0, result.Length);
            return result;
        }

        public override void WriteByte(byte value)
        {
            this.Basestream.WriteByte(value);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!this._leaveOpen)
                    this.Basestream.Dispose();
            }
            base.Dispose(disposing);
        }
#pragma warning disable 0809
        [Obsolete("Should not use this.", true), EditorBrowsable(EditorBrowsableState.Never), Browsable(false)]
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            throw new NotImplementedException();
        }
        [Obsolete("Should not use this.", true), EditorBrowsable(EditorBrowsableState.Never), Browsable(false)]
        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            throw new NotImplementedException();
        }
        [Obsolete("Should not use this.", true), EditorBrowsable(EditorBrowsableState.Never), Browsable(false)]
        public override int EndRead(IAsyncResult asyncResult)
        {
            throw new NotImplementedException();
        }
        [Obsolete("Should not use this.", true), EditorBrowsable(EditorBrowsableState.Never), Browsable(false)]
        public override void EndWrite(IAsyncResult asyncResult)
        {
            throw new NotImplementedException();
        }
#pragma warning restore 0809
    }
}
