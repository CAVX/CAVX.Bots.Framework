using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Modules.Actions
{
    public interface IActionComponent : IBotAction
    {
        Task FillComponentParametersAsync(string[] selectOptions, object[] idOptions);
    }
}