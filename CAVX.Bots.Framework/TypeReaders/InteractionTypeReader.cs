using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CAVX.Bots.Framework.Modules.Contexts;
using CAVX.Bots.Framework.Services;

namespace CAVX.Bots.Framework.TypeReaders
{
    public abstract class InteractionTypeReader<TResult> : TypeReader where TResult : class
    {
        public async Task<TResult> BestMatchOrDefaultAsync(RequestContext context, string input, IServiceProvider services)
        {
            if (string.IsNullOrWhiteSpace(input))
                return default;

            var typeReaderResult = await ReadAsync(context, input, services);
            if (!typeReaderResult.IsSuccess)
            {
                if (typeReaderResult.Error.HasValue && typeReaderResult.Error.Value == CommandError.Exception)
                    throw new CommandParameterValidationException(typeReaderResult.ErrorReason);
                return default;
            }

            return (TResult)typeReaderResult.BestMatch;
        }

        public sealed override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
            => ReadAsync(new RequestCommandContext(context as SocketCommandContext), input, services);

        public abstract Task<TypeReaderResult> ReadAsync(RequestContext context, string input, IServiceProvider services);
    }
}
