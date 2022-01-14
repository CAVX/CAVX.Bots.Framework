using CAVX.Bots.Framework.Modules.Contexts;
using Discord.Rest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Models
{
    public enum MessageStyle
    {
        Embed,
        PlainText,
        None
    }

    public abstract class DeferredBuilder
    {
        protected RequestContext _context;
        protected RestUserMessage _message;

        public void Initialize(RequestContext context, RestUserMessage message)
        {
            _context = context;
            _message = message;
        }
    }

    public interface IDeferredInvoke
    {
        Type InstanceType { get; }
        void SetInstanceAndProperties(object instance, RequestContext context, RestUserMessage message);
        Task<IMessageBuilder> GetDeferredMessage();
    }

    public class DeferredInvoke<T> : IDeferredInvoke where T : DeferredBuilder
    {
        public Func<T, Task<IMessageBuilder>> GetDeferredInvoke { get; set; }
        public T Instance { get; set; }
        public Type InstanceType => typeof(T);

        public DeferredInvoke(Func<T, Task<IMessageBuilder>> getDeferredInvoke)
        {
            GetDeferredInvoke = getDeferredInvoke;
        }

        public void SetInstanceAndProperties(object instance, RequestContext context, RestUserMessage message)
        {
            Instance = instance as T;
            Instance.Initialize(context, message);
        }

        public Task<IMessageBuilder> GetDeferredMessage()
        {
            return GetDeferredInvoke?.Invoke(Instance);
        }
    }

    public interface IMessageBuilder
    {
        bool Success { get; set; }
        IDeferredInvoke DeferredBuilder { get; set; }
        MessageMetadata BuildOutput();
    }
}
