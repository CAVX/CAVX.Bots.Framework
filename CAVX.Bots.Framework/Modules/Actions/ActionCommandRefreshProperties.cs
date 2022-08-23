using CAVX.Bots.Framework.Processing;
using Discord;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Modules.Actions
{
    public class ActionCommandRefreshProperties
    {
        public Func<string[], object[], Task> FillParametersAsync { get; set; }
        public Func<ActionRefreshTargetMessage[], Task<(bool, string)>> CanRefreshAsync { get; set; }
        public Func<ActionRefreshTargetMessage[], Task<List<ActionRefreshMessagePartCollection>>> RefreshAsync { get; set; }
    }
}
