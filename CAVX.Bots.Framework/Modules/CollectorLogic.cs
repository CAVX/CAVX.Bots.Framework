using CAVX.Bots.Framework.Models;
using CAVX.Bots.Framework.Services;
using CAVX.Bots.Framework.Utilities;
using Discord;
using System;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Modules
{
    internal interface ICollectorLogic
    {
        ulong MessageId { get; set; }
        ulong? OriginalUserId { get; set; }
        bool OnlyOriginalUserAllowed { get; set; }
        Func<IUser, ulong, object[], object[], Task<(MessageResultCode Result, string FailureMessage, IMessageBuilder MessageBuilder)>> Execute { get; set; }
    }

    public class CollectorLogic : ICollectorLogic
    {
        private readonly SemaphoreLocker _resultLock = new();
        private bool _enabled = true;
        private Func<IUser, ulong, object[], object[], Task<(MessageResultCode Result, string FailureMessage, IMessageBuilder MessageBuilder)>> _execute;

        public ulong MessageId { get; set; }
        public ulong? OriginalUserId { get; set; }
        public bool OnlyOriginalUserAllowed { get; set; }

        public Func<IUser, ulong, object[], object[], Task<(MessageResultCode Result, string FailureMessage, IMessageBuilder MessageBuilder)>> Execute
        {
            get => _execute;
            set
            {
                if (value == null)
                {
                    _execute = null;
                }
                else
                {
                    _execute = async (userData, messageId, idOptions, selectOptions) =>
                    {
                        return await _resultLock.LockAsync(async () =>
                        {
                            if (_enabled)
                                return await value(userData, messageId, idOptions, selectOptions);

                            return (MessageResultCode.ExecutionFailed, "Sorry! You were just too late!", null);
                        });
                    };
                }
            }
        }

        public async Task ExecuteAndWait(ActionService actionService, int secondsToWait)
        {
            try
            {
                actionService.RegisterCollector(this);
                await Task.Delay(TimeSpan.FromSeconds(secondsToWait));
            }
            finally
            {
                await _resultLock.LockAsync(() =>
                {
                    _enabled = false;
                    actionService.UnregisterCollector(this);
                    return Task.CompletedTask;
                });
            }
        }
    }
}