namespace CAVX.Bots.Framework.Modules.Actions
{
    public interface IActionSlashRoot : IRequiredAccessRule
    {
        string CommandDescription { get; }
        string CommandName { get; }
    }
}