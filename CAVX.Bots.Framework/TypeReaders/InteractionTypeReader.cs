using CAVX.Bots.Framework.Modules.Contexts;
using Discord.Commands;
using System;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.TypeReaders
{
    public abstract class InteractionTypeReader<TResult> : TypeReader where TResult : class
    {
        public async Task<TResult> BestMatchOrDefaultAsync(RequestContext context, string input, IServiceProvider services)
        {
            if (string.IsNullOrWhiteSpace(input))
                return default;

            var typeReaderResult = await ReadAsync(context, input, services);
            if (typeReaderResult.IsSuccess) return (TResult)typeReaderResult.BestMatch;

            if (typeReaderResult.Error == CommandError.Exception)
                throw new CommandParameterValidationException(typeReaderResult.ErrorReason);
            return default;
        }

        public sealed override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
            => ReadAsync(new RequestCommandContext(context as SocketCommandContext), input, services);

        public abstract Task<TypeReaderResult> ReadAsync(RequestContext context, string input, IServiceProvider services);
    }
}