using System.IO;

namespace BnSDat
{
    internal static class CommonMethods
    {
        internal static void StreamCopyChunk(Stream source, Stream destination, int buffersize)
        {
            using (Leayal.ByteBuffer buffer = new Leayal.ByteBuffer(buffersize))
            {
                int read = source.Read(buffer, 0, buffer.Length);
                while (read > 0)
                {
                    destination.Write(buffer, 0, read);
                    read = source.Read(buffer, 0, buffer.Length);
                }
            }
        }
    }
}
