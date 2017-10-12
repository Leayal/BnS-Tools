using System;

namespace BnSDat
{
    public class ProgressEventArgs : EventArgs
    {
        internal int _current;
        public int Current => this._current;
        public int Total { get; }
        public ProgressEventArgs(int current, int total) : base()
        {
            this._current = current;
            this.Total = total;
        }
    }
}
