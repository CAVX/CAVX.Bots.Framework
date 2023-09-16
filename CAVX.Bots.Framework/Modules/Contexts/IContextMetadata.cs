namespace CAVX.Bots.Framework.Modules.Contexts;

public interface IContextMetadata
{
    RequestContext Context { get; set; }
    bool UseQueue { get; }
    bool SkipDefer { get; }
}