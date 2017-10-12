using System;

namespace BnSDat
{
    public class EntryExtractingEventArgs : EventArgs
    {
        internal long _totalBytesToExtract, _extractedBytes;
        public long TotalBytesToExtract => this._totalBytesToExtract;
        public long ExtractedBytes => this._extractedBytes;
        internal EntryExtractingEventArgs(long _totalBytesToExtract, long _extractedBytes) : base()
        {
            this._totalBytesToExtract = _totalBytesToExtract;
            this._extractedBytes = _extractedBytes;
        }
    }
}
