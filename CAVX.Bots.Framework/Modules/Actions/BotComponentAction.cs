using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Modules.Actions
{
    public abstract class BotComponentAction : BotAction
    {
        public virtual Task FillParametersAsync(string[] selectOptions, object[] idOptions) => Task.CompletedTask;
    }
}
