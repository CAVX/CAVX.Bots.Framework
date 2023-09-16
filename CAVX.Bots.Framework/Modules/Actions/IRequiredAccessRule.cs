using CAVX.Bots.Framework.Models;

namespace CAVX.Bots.Framework.Modules.Actions;

public interface IRequiredAccessRule
{
    ActionAccessRule RequiredAccessRule { get; }
}