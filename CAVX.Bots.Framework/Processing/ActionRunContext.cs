using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Processing
{
    public enum ActionRunContext
    {
        None,
        Slash,
        Message,
        User,
        Component,
        Text
    }
}
