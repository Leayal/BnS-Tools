using System;
using System.IO;

namespace BnSDat
{
    public class WriteArchiveEventArgs : EventArgs
    {
        public Stream ArchiveStream { get; }
        public string Filename { get; }
        public FilepathInsideArchive PathInsideArchive { get; }
        internal WriteArchiveEventArgs(Stream _archive, string _filename, FilepathInsideArchive _filepathinside) : base()
        {
            this.ArchiveStream = _archive;
            this.Filename = _filename;
            this.PathInsideArchive = _filepathinside;
        }
    }
}
