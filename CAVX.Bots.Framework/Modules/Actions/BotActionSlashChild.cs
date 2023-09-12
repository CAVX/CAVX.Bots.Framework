using CAVX.Bots.Framework.Models;
using System;

namespace CAVX.Bots.Framework.Modules.Actions
{
    public interface IActionSlashChild
    {
        IActionSlashParent Parent { get; }
    }

    public abstract class BotActionSlashChild<TParent> : BotAction, IActionSlashChild where TParent : class, IActionSlashParent
    {
        public TParent Parent { get; set; } = Activator.CreateInstance<TParent>();
        IActionSlashParent IActionSlashChild.Parent => Parent;

        public sealed override bool RestrictAccessToGuilds => Parent.RestrictAccessToGuilds;
        public sealed override bool ConditionalGuildsOnly => Parent.ConditionalGuildsOnly;
        public sealed override ActionAccessRule RequiredAccessRule => Parent.RequiredAccessRule;
    }
}