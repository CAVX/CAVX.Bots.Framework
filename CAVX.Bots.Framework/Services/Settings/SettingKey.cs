namespace CAVX.Bots.Framework.Services.Settings
{
    public class SettingKey
    {
        public string Name { get; set; }

        protected SettingKey(string name)
        {
            Name = name;
        }

        public static implicit operator SettingKey(string s) => new(s);
    }

    //This is typed so the explicit type can come back from the lists. There's a base class so that the key can be held in the list.
    public class SettingKey<T> : SettingKey
    {
        protected SettingKey(string name) : base(name)
        {
        }

        public static implicit operator SettingKey<T>(string s) => new(s);
    }
}