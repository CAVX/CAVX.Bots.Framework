using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Modules.Contexts
{
    public interface IContextMetadata
    {
        RequestContext Context { get; set; }
        bool UseQueue { get; }
        bool SkipDefer { get; }
    }
}
