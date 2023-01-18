using CAVX.Bots.Framework.Models;

namespace CAVX.Bots.Framework.Modules.Actions
{
    public interface IActionSlashRoot
    {
        string CommandDescription { get; }
        string CommandName { get; }
        ActionAccessRule RequiredAccessRule { get; }
    }
}