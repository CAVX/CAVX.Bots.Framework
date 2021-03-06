using Discord;
using System;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Modules.Actions
{
    public class ActionCommandRefreshProperties
    {
        public Func<string[], object[], Task> FillParametersAsync { get; set; }
        public Func<bool, Task<(bool, string)>> CanRefreshAsync { get; set; }
        public Func<bool, MessageProperties, Task> RefreshAsync { get; set; }
    }
}
