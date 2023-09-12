using System;

namespace CAVX.Bots.Framework.Services.Settings
{
    public class SettingMetadata
    {
        public string Description { get; set; }
        public string DefaultValue { get; set; }

        protected SettingMetadata(string description, string defaultValue)
        { Description = description; DefaultValue = defaultValue; }
    }

    public class SettingMetadata<T> : SettingMetadata
    {
        public Func<string, (bool Success, T Value)> Conversion { get; set; }

        public SettingMetadata(string description, T defaultValue, Func<string, (bool Success, T Value)> conversion) : base(description, defaultValue.ToString())
        {
            Conversion = conversion;
        }

        public SettingMetadata(string description, string defaultValue, Func<string, (bool Success, T Value)> conversion) : base(description, defaultValue)
        {
            Conversion = conversion;
        }
    }
}