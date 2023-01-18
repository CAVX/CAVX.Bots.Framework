namespace CAVX.Bots.Framework.Modules.Actions.Attributes
{
    public interface IActionParameterAttribute
    {
        public string Name { get; set; }
        public string Description { get; set; }
        int Order { get; set; }
        bool Required { get; set; }
    }
}
