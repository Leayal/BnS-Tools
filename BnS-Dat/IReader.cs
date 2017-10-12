using System;
using System.IO;

namespace BnSDat
{
    public interface IReader : IDisposable
    {
        /// <summary>
        /// Advance to next entry. Return True if next entry is found, otherwise False.
        /// </summary>
        /// <returns></returns>
        bool MoveToNextEntry();
        /// <summary>
        /// Copy the entry to <see cref="BnSDatWriter"/> directly without processing its data (skip decrypt and uncompress step). This method is to reduce the overheat of unncessary processing.
        /// </summary>
        /// <param name="writer">Destination</param>
        void CopyEntryTo(BnSDatWriter writer);
        /// <summary>
        /// Extract entry to the destination stream.
        /// </summary>
        /// <param name="stream">Destination stream</param>
        void ExtractTo(Stream stream);
        EntryStream GetEntryStream();
        Entry Entry { get; }
    }
}
