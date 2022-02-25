using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Modules.Actions
{
    public interface IActionSlashCommandProperties
    {
        string Name { get; set; }
        string Description { get; set; }
        Func<IEnumerable<SocketSlashCommandDataOption>, Task> FillParametersAsync { get; set; }
        public Func<IServiceProvider, Task<Dictionary<string, List<(string Name, string Value)>>>> GenerateParameterOptionsAsync { get; set; }
        public Dictionary<string, Func<SocketAutocompleteInteraction, Task>> AutocompleteAsync { get; set; }
    }

    public class ActionGlobalSlashCommandProperties : IActionSlashCommandProperties
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Func<IEnumerable<SocketSlashCommandDataOption>, Task> FillParametersAsync { get; set; }
        public Func<IServiceProvider, Task<Dictionary<string, List<(string Name, string Value)>>>> GenerateParameterOptionsAsync { get; set; }
        public Dictionary<string, Func<SocketAutocompleteInteraction, Task>> AutocompleteAsync { get; set; }
    }

    public class ActionGuildSlashCommandProperties : IActionSlashCommandProperties
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Func<IEnumerable<SocketSlashCommandDataOption>, Task> FillParametersAsync { get; set; }
        public Func<IServiceProvider, Task<Dictionary<string, List<(string Name, string Value)>>>> GenerateParameterOptionsAsync { get; set; }
        public Dictionary<string, Func<SocketAutocompleteInteraction, Task>> AutocompleteAsync { get; set; }
    }
}
