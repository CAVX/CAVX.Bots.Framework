using CAVX.Bots.Framework.Modules.Contexts;
using CAVX.Bots.Framework.TypeReaders;
using System;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Services
{
    public class AdHocTypeReaderService : IDisposable
    {
        private readonly IServiceProvider _services;

        public AdHocTypeReaderService(IServiceProvider services)
        {
            _services = services;
        }

        public void Initialize() { }

        public async Task<T2> BestMatchOrDefaultAsync<T, T2>(RequestContext context, string input) where T : InteractionTypeReader<T2>, new() where T2 : class
        {
            return await new T().BestMatchOrDefaultAsync(context, input, _services);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
