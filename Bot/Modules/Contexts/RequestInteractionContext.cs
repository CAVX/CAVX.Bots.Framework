using Discord;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bot.Services;
using Bot.Models;
using Bot.Utilities;

namespace Bot.Modules.Contexts
{
    public class RequestInteractionContext : RequestContext
    {
        readonly SemaphoreLocker _acknowledgedLock = new();
        bool _hasbeenAcknowledged = false;

        public RequestInteractionContext(SocketInteraction interaction, DiscordSocketClient client)
        {
            OriginalInteraction = interaction ?? throw new ArgumentNullException(nameof(interaction));
            Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public SocketInteraction OriginalInteraction { get; }

        public async Task<bool> HadBeenAcknowledgedAsync(bool setIfFalse = false, Func<Task> performIfFalse = null)
        {
            return await _acknowledgedLock.LockAsync(async () =>
            {
                bool wasAcknowledged = _hasbeenAcknowledged;
                if (!wasAcknowledged && setIfFalse)
                    _hasbeenAcknowledged = true;
                if (!wasAcknowledged && performIfFalse != null)
                {
                    try
                    {
                        await performIfFalse();
                    }
                    catch { }
                }

                return wasAcknowledged;
            });
        }

        public override DiscordSocketClient Client { get; }
        public override SocketGuild Guild => (OriginalInteraction.Channel as IGuildChannel)?.Guild as SocketGuild;
        public override ISocketMessageChannel Channel => OriginalInteraction.Channel;
        public override SocketUser User => OriginalInteraction.User;

        public InteractionType Type => OriginalInteraction.Type;

        public override SocketUserMessage Message
        {
            get
            {
                if (OriginalInteraction is SocketMessageComponent smc)
                    return smc.Message;
                return null;
            }
        }

        public override async Task<RestUserMessage> ReplyWithMessageAsync(EphemeralRule ephemeralRule, string message = null, bool isTTS = false, Embed[] embeds = null, Embed embed = null,
            RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, bool hasMentions = false)
        {
            return await _acknowledgedLock.LockAsync(async () =>
            {
                bool initial = await GetInitialAsync(true);
                bool wasAcknowledged = _hasbeenAcknowledged;

                try
                {
                    if (initial && !wasAcknowledged)
                    {
                        await OriginalInteraction.RespondAsync(message, embeds, isTTS, ephemeralRule.ToEphemeral(), allowedMentions, options, components, embed);
                        _hasbeenAcknowledged = true;
                        try
                        {
                            return await OriginalInteraction.GetOriginalResponseAsync();
                        }
                        catch (HttpException)
                        {
                            await Task.Delay(1000);
                            return await OriginalInteraction.GetOriginalResponseAsync();
                        }
                    }
                    else if (initial && OriginalInteraction is SocketSlashCommand)
                    {
                        if (hasMentions && ephemeralRule == EphemeralRule.Permanent)
                        {
                            await TryDeleteOriginalMessageAsync();
                            return await Channel.SendMessageAsync(message, isTTS, embed ?? embeds?.FirstOrDefault(), options, allowedMentions, messageReference, components);
                        }
                        else
                        {
                            try
                            {
                                return await OriginalInteraction.ModifyOriginalResponseAsync(mp =>
                                {
                                    if (!mp.Flags.IsSpecified)
                                        mp.Flags = MessageFlags.None;

                                    mp.Content = message;
                                    mp.Embeds = embeds ?? (embed == null ? null : new Embed[] { embed });
                                    mp.AllowedMentions = allowedMentions;
                                    mp.Components = components;
                                }, options);
                            }
                            catch (HttpException)
                            {
                                await Task.Delay(1000);
                                return await OriginalInteraction.ModifyOriginalResponseAsync(mp =>
                                {
                                    if (!mp.Flags.IsSpecified)
                                        mp.Flags = MessageFlags.None;

                                    mp.Content = message;
                                    mp.Embeds = embeds ?? (embed == null ? null : new Embed[] { embed });
                                    mp.AllowedMentions = allowedMentions;
                                    mp.Components = components;
                                }, options);
                            }
                        }
                    }

                    if (messageReference == null || ephemeralRule == EphemeralRule.EphemeralOrFail)
                        return await OriginalInteraction.FollowupAsync(message, embeds, isTTS, ephemeralRule.ToEphemeral(), allowedMentions, options, components, embed);
                    else
                        return await Channel.SendMessageAsync(message, isTTS, embed ?? embeds?.FirstOrDefault(), options, allowedMentions, messageReference, components);
                }
                catch (HttpException e)
                {
                    Console.WriteLine(e.ToString());

                    if (initial && OriginalInteraction is SocketSlashCommand)
                        await TryDeleteOriginalMessageAsync();

                    if (ephemeralRule == EphemeralRule.EphemeralOrFail)
                        return await Channel.SendMessageAsync("It took me too long to process that, and I don't want to show anyone else! Sorry! Try again!");
                    else
                        return await Channel.SendMessageAsync(message, isTTS, embed ?? embeds?.FirstOrDefault(), options, allowedMentions, messageReference, components);
                }
            });
        }

        public async override Task<RestUserMessage> ReplyWithFileAsync(EphemeralRule ephemeralRule, Stream stream, string filename, bool isSpoiler, string message = null, bool isTTS = false, Embed[] embeds = null, Embed embed = null,
            RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, bool hasMentions = false)
        {
            try
            {
                bool initial = await GetInitialAsync(true);

                if (embed == null && embeds != null && embeds.Any())
                    embed = embeds.FirstOrDefault();

                await _acknowledgedLock.LockAsync(async () =>
                {
                    if (initial && !_hasbeenAcknowledged)
                    {
                        await OriginalInteraction.DeferAsync(ephemeralRule.ToEphemeral());
                        _hasbeenAcknowledged = true;
                    }
                });

                if (initial && OriginalInteraction is SocketSlashCommand)
                {
                    var originalMessage = await ReplyWithMessageAsync(ephemeralRule, "‎");
                    if (!ephemeralRule.ToEphemeral())
                        await originalMessage.DeleteAsync();
                }
            }
            catch (HttpException e)
            {
                Console.WriteLine(e.ToString());
            }

            return await Channel.SendFileAsync(stream, filename, message, isTTS, embed, options, isSpoiler, allowedMentions, messageReference, components);
        }

        public override async Task UpdateReplyAsync(Action<MessageProperties> propBuilder, RequestOptions options = null)
        {
            try
            {
                bool initial = await GetInitialAsync(true);

                await _acknowledgedLock.LockAsync(async () =>
                {
                    bool wasAcknowledged = _hasbeenAcknowledged;
                    if (OriginalInteraction is SocketMessageComponent smc && !wasAcknowledged)
                    {
                        await smc.UpdateAsync(propBuilder);
                        _hasbeenAcknowledged = true;
                    }
                    else
                    {
                        if (initial && !wasAcknowledged)
                        {
                            await OriginalInteraction.DeferAsync(true);
                            _hasbeenAcknowledged = true;
                        }

                        var message = await OriginalInteraction.GetOriginalResponseAsync();
                        await message.ModifyAsync(propBuilder);
                    }
                });
            }
            catch (HttpException e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private async Task TryDeleteOriginalMessageAsync()
        {
            if (!_hasbeenAcknowledged)
            {
                await OriginalInteraction.DeferAsync();
                _hasbeenAcknowledged = true;
            }

            _ = Task.Run(async () =>
            {
                try
                {

                    await (await OriginalInteraction.GetOriginalResponseAsync())?.DeleteAsync();
                }
                catch { /*eh*/ }
            });
        }
    }
}
