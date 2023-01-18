using CAVX.Bots.Framework.Extensions;
using Discord;
using System;

namespace CAVX.Bots.Framework.Modules.Actions.Attributes
{

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class SlashOptionMapAttribute : Attribute
    {
        public ApplicationCommandOptionType Mapping { get; set; }

        public SlashOptionMapAttribute(ApplicationCommandOptionType mapping)
        {
            Mapping = mapping;
        }
    }

    public enum SlashOptionType : byte
    {
        [SlashOptionMap(ApplicationCommandOptionType.String)]
        String,
        [SlashOptionMap(ApplicationCommandOptionType.Integer)]
        Integer,
        [SlashOptionMap(ApplicationCommandOptionType.Boolean)]
        Boolean,
        [SlashOptionMap(ApplicationCommandOptionType.User)]
        User,
        [SlashOptionMap(ApplicationCommandOptionType.Channel)]
        Channel,
        [SlashOptionMap(ApplicationCommandOptionType.Role)]
        Role,
        [SlashOptionMap(ApplicationCommandOptionType.Mentionable)]
        Mentionable,
        [SlashOptionMap(ApplicationCommandOptionType.Number)]
        Number,
        [SlashOptionMap(ApplicationCommandOptionType.Attachment)]
        Attachment
    }

    public static class SlashOptionTypeExtensions
    {
        public static ApplicationCommandOptionType ToApplicationCommandOptionType(this SlashOptionType slashOptionType)
        {
            return slashOptionType.GetAttributeDetails<SlashOptionMapAttribute>().Mapping;
        }
    }


    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ActionParameterSlashAttribute : Attribute, IActionParameterAttribute
    {
        public int Order { get; set; }

        public string Name { get; set; }
        public SlashOptionType Type { get; set; }
        public string Description { get; set; }
        public bool Required { get; set; } = false;
    }
}
