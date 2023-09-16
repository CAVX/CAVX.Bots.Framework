using System.IO;

namespace CAVX.Bots.Framework.Extensions;

public static class StreamExtensions
{
    public static Stream RefreshOrUse(this Stream stream)
    {
        if (stream?.CanRead == false && stream is MemoryStream memoryStream)
        {
            stream = new MemoryStream(memoryStream.ToArray());
            stream.Seek(0, SeekOrigin.Begin);
        }

        return stream;
    }
}