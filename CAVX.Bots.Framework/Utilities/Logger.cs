using System;
using System.Threading.Tasks;
using CAVX.Bots.Framework.Modules.Contexts;

namespace CAVX.Bots.Framework.Utilities
{
    public sealed class Logger
    {
        private static readonly Lazy<Logger> Lazy = new(() => new Logger());

        public static Logger Instance => Lazy.Value;

        private Logger()
        { }

        public Func<RequestContext, string, Exception, Task> LogErrorAsync { get; set; }
    }
}