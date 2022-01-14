using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Extensions
{
    public static class StreamExtensions
    {
        public static Stream RefreshOrUse(this Stream stream)
        {
            if (stream != null)
            {
                if (!stream.CanRead && stream is MemoryStream memoryStream)
                {
                    stream = new MemoryStream(memoryStream.ToArray());
                    stream.Seek(0, SeekOrigin.Begin);
                }
            }

            return stream;
        }
    }
}
