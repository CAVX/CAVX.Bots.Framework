using CAVX.Bots.Framework.Modules.Contexts;
using Discord.Rest;
using System;
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
        protected IContextMetadata _contextMetadata;
        protected RestUserMessage _message;

        public void Initialize(IContextMetadata contextMetadata, RestUserMessage message)
        {
            _contextMetadata = contextMetadata;
            _message = message;
        }
    }

    public interface IDeferredInvoke
    {
        Type InstanceType { get; }

        void SetInstanceAndProperties(object instance, IContextMetadata contextMetadata, RestUserMessage message);

        Task PreprocessAsync();

        Task<IMessageBuilder> GetDeferredMessageAsync();
    }

    public class DeferredInvoke<T>(Func<T, Task> getDeferredPreprocessInvoke,
            Func<T, Task<IMessageBuilder>> getDeferredMessageInvoke)
        : IDeferredInvoke
        where T : DeferredBuilder
    {
        public Func<T, Task> GetDeferredPreprocessInvoke { get; set; } = getDeferredPreprocessInvoke;
        public Func<T, Task<IMessageBuilder>> GetDeferredMessageInvoke { get; set; } = getDeferredMessageInvoke;
        public T Instance { get; set; }
        public Type InstanceType => typeof(T);

        public void SetInstanceAndProperties(object instance, IContextMetadata contextMetadata, RestUserMessage message)
        {
            Instance = instance as T;
            Instance?.Initialize(contextMetadata, message);
        }

        public Task PreprocessAsync()
        {
            return GetDeferredPreprocessInvoke?.Invoke(Instance) ?? Task.CompletedTask;
        }

        public Task<IMessageBuilder> GetDeferredMessageAsync()
        {
            return GetDeferredMessageInvoke?.Invoke(Instance) ?? Task.FromResult<IMessageBuilder>(null);
        }
    }

    public interface IMessageBuilder
    {
        MessageResultCode Result { get; set; }
        IDeferredInvoke DeferredBuilder { get; set; }

        MessageMetadata BuildOutput();
    }
}