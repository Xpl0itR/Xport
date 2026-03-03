// Copyright © 2026 Xpl0itR
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Xport;

internal sealed class TransportConnection<TConnection>(
    IServiceProvider serviceProvider,
    TConnection connection,
    Func<TConnection, Task> connectionHandler,
    Action<string> unregisterConnection)
        : TransportConnection(connection, serviceProvider.GetRequiredService<ILogger<TransportConnection>>()), IThreadPoolWorkItem
            where TConnection : BaseConnectionContext
{
    async void IThreadPoolWorkItem.Execute()
    {
        try
        {
            Logger.LogConnectionStart(connection.ConnectionId);

            await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
            connection.Features.Set(scope.ServiceProvider);

            await connectionHandler(connection).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (ConnectionResetException) { }
        catch (Exception ex)
        {
            Logger.LogUnhandledExceptionAsyncVoid(ex);
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            Logger.LogConnectionStop(connection.ConnectionId);
            unregisterConnection(connection.ConnectionId);
            Complete();
        }
    }
}

internal abstract class TransportConnection
{
    private readonly BaseConnectionContext _connection;
    private readonly CancellationTokenSource _cts;
    private readonly TaskCompletionSource _tcs = new();

    protected readonly ILogger Logger;

    protected TransportConnection(BaseConnectionContext connection, ILogger<TransportConnection> logger)
    {
        _connection = connection;
        Logger = logger;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(connection.ConnectionClosed);

        connection.ConnectionClosed = _cts.Token;
    }

    protected void Complete()
    {
        _tcs.TrySetResult();
        _cts.Dispose();
    }

    internal async Task CloseAsync(CancellationToken ct)
    {
        if (_tcs.Task.IsCompleted)
            return;

        Logger.LogConnectionClosing(_connection.ConnectionId);
        _cts.Cancel();

        Task task = _tcs.Task.WaitAsync(ct);
#if NET8_0_OR_GREATER
        await task.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
#else
        await new SuppressThrowingAwaiter(task);
#endif
        if (task.IsCanceled)
        {
            Logger.LogConnectionFailedToClose(_connection.ConnectionId);
            _connection.Abort(
                new ConnectionAbortedException(
                    "The connection was aborted by the application because it took too long to close cleanly"));
            _cts.Dispose();
        }
    }
#if !NET8_0_OR_GREATER
    private readonly struct SuppressThrowingAwaiter(Task task) : System.Runtime.CompilerServices.ICriticalNotifyCompletion
    {
        public SuppressThrowingAwaiter GetAwaiter() => this;
        public bool IsCompleted => task.IsCompleted;
        public void GetResult() { }
        public void OnCompleted(Action continuation) => task.GetAwaiter().OnCompleted(continuation);
        public void UnsafeOnCompleted(Action continuation) => task.GetAwaiter().OnCompleted(continuation);
    }
#endif
}