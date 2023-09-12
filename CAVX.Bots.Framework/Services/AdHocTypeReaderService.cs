using CAVX.Bots.Framework.Modules.Contexts;
using CAVX.Bots.Framework.TypeReaders;
using System;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Services
{
    public class AdHocTypeReaderService(IServiceProvider services)
    {
        public void Initialize()
        {
            //Nothing to initialize.
        }

        public async Task<T2> BestMatchOrDefaultAsync<T, T2>(RequestContext context, string input) where T : InteractionTypeReader<T2>, new() where T2 : class
        {
            return await new T().BestMatchOrDefaultAsync(context, input, services);
        }
    }
}