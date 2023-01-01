using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CAVX.Bots.Framework.Models;
using Discord.WebSocket;

namespace CAVX.Bots.Framework.Modules.Actions
{
    public interface IActionSlashChild
    {
        IActionSlashParent Parent { get; }
    }


    public abstract class BotActionSlashChild<TParent> : BotAction, IActionSlashChild where TParent : class, IActionSlashParent
    {
        public TParent Parent { get; set; }
        IActionSlashParent IActionSlashChild.Parent => Parent;

        public BotActionSlashChild() : base()
        {
            Parent = Activator.CreateInstance<TParent>();
        }

        public override sealed bool RestrictAccessToGuilds => Parent.RestrictAccessToGuilds;
        public override sealed bool ConditionalGuildsOnly => Parent.ConditionalGuildsOnly;
        public override sealed ActionAccessRule RequiredAccessRule => Parent.RequiredAccessRule;

    }
}
