using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Bot.Modules.Attributes;
using System.Linq;

namespace Bot.Modules
{
    [ModuleOrder(0)]
    public class MetaCommands : MessageCommandBase
    {
        // DI Services
        public CommandService CommandService { get; set; }
        public IServiceProvider ServiceProvider { get; set; }

        [Command("Help")]
        [RequireContext(ContextType.Guild)]
        public async Task Help()
        {
            List<CommandInfo> commands = CommandService.Commands.OrderBy(c => (c.Module?.Attributes?.FirstOrDefault(a => a.GetType() == typeof(ModuleOrderAttribute)) as ModuleOrderAttribute)?.Order ?? -1).ToList();
            EmbedBuilder embedBuilder = new()
            {
                Title = "Available Commands",
                Description = "Don't forget about my slash commands, though!"
            };

            foreach (CommandInfo command in commands)
            {
                if (command.Summary == null || !(await command.CheckPreconditionsAsync(Context, ServiceProvider)).IsSuccess)
                    continue;

                embedBuilder.AddField(command.Name, command.Summary);
            }

            await ReplyAsync(embed: embedBuilder.Build());
        }

        [Command("ping")]
        [Alias("hello", "hi", "hey")]
        [Summary("Yeah, baby! Use this to see if I'm around. If I am, I'll tell you to scram.")]
        [RequireContext(ContextType.Guild)]
        public async Task Ping()
        {
            await ReplyAsync("Go away.");
        }
    }
}