namespace BnSDat
{
    public class FilepathInsideArchive
    {
        public string PathInsideArchive { get; }
        private FilepathInsideArchive(string str)
        {
            this.PathInsideArchive = str;
        }

        public override string ToString()
        {
            return this.PathInsideArchive;
        }

        public override int GetHashCode()
        {
            return this.PathInsideArchive.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return this.PathInsideArchive.Equals(obj);
        }

        public static implicit operator string(FilepathInsideArchive filepathInsideArchive)
        {
            return filepathInsideArchive.PathInsideArchive;
        }

        public static implicit operator FilepathInsideArchive(string filepathInsideArchive)
        {
            return new FilepathInsideArchive(filepathInsideArchive);
        }

        public static bool operator ==(FilepathInsideArchive filepathInsideArchive, string str)
        {
            return (filepathInsideArchive.PathInsideArchive == str);
        }

        public static bool operator !=(FilepathInsideArchive filepathInsideArchive, string str)
        {
            return !(filepathInsideArchive.PathInsideArchive == str);
        }
    }
}
