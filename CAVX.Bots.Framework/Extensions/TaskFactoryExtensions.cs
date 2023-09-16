using System;
using System.Threading;
using System.Threading.Tasks;

namespace CAVX.Bots.Framework.Extensions;

public static class TaskFactoryExtensions
{
    /// <summary>
    /// Creates and starts a task for the specified action delegate and state.
    /// </summary>
    /// <typeparam name="T">The parameter being passed into the target action.</typeparam>
    /// <param name="taskFactory">The current task factory.</param>
    /// <param name="action">The action delegate to execute asynchronously.</param>
    /// <param name="state">An object containing data to be used by the action delegate.</param>
    /// <returns>The started task.</returns>
    public static Task Run<T>(this TaskFactory taskFactory, Action<T> action, T state)
    {
        return taskFactory.StartNew(BoxedAction, state);

        void BoxedAction(object o) => action((T)o);
    }

    /// <summary>
    /// Creates and starts a task for the specified action delegate, state and cancellation token.
    /// </summary>
    /// <typeparam name="T">The parameter being passed into the target action.</typeparam>
    /// <param name="taskFactory">The current task factory.</param>
    /// <param name="action">The action delegate to execute asynchronously.</param>
    /// <param name="state">An object containing data to be used by the action delegate.</param>
    /// <param name="cancellationToken">The cancellation token that will be assigned to the new task.</param>
    /// <returns>The started task.</returns>
    public static Task Run<T>(this TaskFactory taskFactory, Action<T> action, T state, CancellationToken cancellationToken)
    {
        return taskFactory.StartNew(BoxedAction, state, cancellationToken);

        void BoxedAction(object o) => action((T)o);
    }

    /// <summary>
    /// Creates and starts a task for the specified action delegate, state, cancellation token, creation options and task scheduler.
    /// </summary>
    /// <typeparam name="T">The parameter being passed into the target action.</typeparam>
    /// <param name="taskFactory">The current task factory.</param>
    /// <param name="action">The action delegate to execute asynchronously.</param>
    /// <param name="state">An object containing data to be used by the action delegate.</param>
    /// <param name="creationOptions">One of the enumeration values that controls the behavior of the created task.</param>
    /// <param name="scheduler">The task scheduler that is used to schedule the created task.</param>
    /// <param name="cancellationToken">The cancellation token that will be assigned to the new task.</param>
    /// <returns>The started task.</returns>
    public static Task Run<T>(this TaskFactory taskFactory, Action<T> action, T state, TaskCreationOptions creationOptions, TaskScheduler scheduler, CancellationToken cancellationToken)
    {
        return taskFactory.StartNew(BoxedAction, state, cancellationToken, creationOptions, scheduler);

        void BoxedAction(object o) => action((T)o);
    }
}