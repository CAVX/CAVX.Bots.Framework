using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Utilities
{
    public class RelativePath
    {
        public static string Combine(params string[] paths)
        {
            paths = paths.Prepend(AppDomain.CurrentDomain.BaseDirectory).ToArray();
            return Path.Combine(paths);
        }
    }
}
