using CAVX.Bots.Framework.Extensions;
using Discord;
using System;

namespace CAVX.Bots.Framework.Modules.Actions.Attributes;

[AttributeUsage(AttributeTargets.Field)]
public class SlashOptionMapAttribute(ApplicationCommandOptionType mapping) : Attribute
{
    public ApplicationCommandOptionType Mapping { get; set; } = mapping;
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

[AttributeUsage(AttributeTargets.Property)]
public class ActionParameterSlashAttribute : Attribute, IActionParameterAttribute
{
    public int Order { get; set; }

    public string Name { get; set; }
    public SlashOptionType Type { get; set; }
    public string Description { get; set; }
    public bool Required { get; set; }
}