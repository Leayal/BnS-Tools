using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BnSDat
{
    class BPKG_FTE
    {
        public int FilePathLength;
        public string FilePath;
        public byte Unknown_001;
        public bool IsCompressed;
        public bool IsEncrypted;
        public byte Unknown_002;
        public int FileDataSizeUnpacked;
        public int FileDataSizeSheared; // without padding for AES
        public int FileDataSizeStored;
        public int FileDataOffset; // (relative) offset
        public byte[] Padding;
    }

    public class Entry
    {
        internal BPKG_FTE Raw { get; }
        public string FilePath => this.Raw.FilePath;
        internal byte Unknown_001 => this.Raw.Unknown_001;
        public bool IsCompressed => this.Raw.IsCompressed;
        public bool IsEncrypted => this.Raw.IsEncrypted;
        internal byte Unknown_002 => this.Raw.Unknown_002;
        public int FileDataSizeUnpacked => this.Raw.FileDataSizeUnpacked;
        public int FileDataSizeSheared => this.Raw.FileDataSizeSheared;
        public int FileDataSizeStored => this.Raw.FileDataSizeStored;

        internal Entry(BPKG_FTE source)
        {
            this.Raw = source;
        }

        internal static Entry FromStruct(BPKG_FTE source)
        {
            return new Entry(source);
        }
    }
}
