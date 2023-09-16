namespace CAVX.Bots.Framework.Modules.Actions;

public interface IActionSlashParent : IActionSlashRoot
{
    bool RestrictAccessToGuilds { get; }
    bool ConditionalGuildsOnly { get; }
}