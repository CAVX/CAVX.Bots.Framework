using CAVX.Bots.Framework.Modules.Contexts;
using System;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Services
{
    public sealed class Logger
    {
        private static readonly Lazy<Logger> _lazy = new(() => new Logger());

        public static Logger Instance => _lazy.Value;

        private Logger() { }

        public Func<RequestContext, string, Exception, Task> LogErrorAsync { get; set; }
    }
}
