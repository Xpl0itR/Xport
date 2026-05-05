// Copyright © 2026 Xpl0itR
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Xport;

internal sealed class TransportConnection<TConnection>(TransportSocket<TConnection> owningSocket, ILogger logger)
    : TransportConnection(logger), IThreadPoolWorkItem where TConnection : BaseConnectionContext
{
    private TConnection _connection = null!;
    private int _slot;

    internal void Initialize(TConnection connection, int slot)
    {
        _connection = connection;
        _slot = slot;

        BaseInit(connection);
    }

    async void IThreadPoolWorkItem.Execute()
    {
        bool hasScope = false;
        AsyncServiceScope scope = default;
        try
        {
            Logger.LogConnectionStart(_connection.ConnectionId);

            if (owningSocket.Options.InjectScopedServiceProvider)
            {
                scope = owningSocket.Options.ApplicationServices.CreateAsyncScope();
                hasScope = true;

                _connection.Features.Set(scope.ServiceProvider);
            }

            await owningSocket.ConnectionHandler(_connection).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (ConnectionResetException) { }
        catch (Exception ex)
        {
            Logger.LogUnhandledExceptionAsyncVoid(ex);
        }
        finally
        {
            try
            {
                if (hasScope) await scope.DisposeAsync().ConfigureAwait(false);
                await _connection.DisposeAsync().ConfigureAwait(false);
                Logger.LogConnectionStop(_connection.ConnectionId);
            }
            catch (Exception e)
            {
                Logger.LogUnhandledExceptionAsyncVoid(e);
            }
            finally
            {
                Complete();
                owningSocket.ConnectionSlots.RemoveAt(_slot);
                owningSocket.ConnObjPool.Return(this);
            }
        }
    }
}

internal abstract class TransportConnection(ILogger logger) : IValueTaskSource
{
    private static readonly ConnectionAbortedException ConnectionAborted =
        new("The connection was aborted by the application because it took too long to close cleanly");

    protected readonly ILogger Logger = logger;

    private ManualResetValueTaskSourceCore<bool> _completionSignal;
    private BaseConnectionContext _connection = null!;
    private CancellationTokenSource _cts = null!;

    protected void BaseInit(BaseConnectionContext connection)
    {
        _completionSignal.Reset();

        _connection = connection;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(connection.ConnectionClosed);

        connection.ConnectionClosed = _cts.Token;
    }

    protected void Complete()
    {
        _completionSignal.SetResult(true);
        _cts.Dispose();
    }

    internal Task CloseAsync(CancellationToken ct)
    {
        if (_completionSignal.GetStatus(_completionSignal.Version) == ValueTaskSourceStatus.Succeeded)
            return Task.CompletedTask;

        Logger.LogConnectionClosing(_connection.ConnectionId);

        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException) { }

        ValueTask vt = new(this, _completionSignal.Version);

        return vt.IsCompleted
            ? Task.CompletedTask
            : WaitForCloseAsync(vt, ct);
    }

    private async Task WaitForCloseAsync(ValueTask vt, CancellationToken ct)
    {
        Task task = vt.AsTask().WaitAsync(ct);
#if NET8_0_OR_GREATER
        await task.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
#else
        await new SuppressThrowingAwaiter(task);
#endif
        if (task.IsCanceled)
        {
            Logger.LogConnectionFailedToClose(_connection.ConnectionId);
            _connection.Abort(ConnectionAborted);
            _cts.Dispose();
        }
    }

    void IValueTaskSource.GetResult(short token) =>
        _completionSignal.GetResult(token);

    ValueTaskSourceStatus IValueTaskSource.GetStatus(short token) =>
        _completionSignal.GetStatus(token);

    void IValueTaskSource.OnCompleted(
        Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags) =>
            _completionSignal.OnCompleted(continuation, state, token, flags);

#if !NET8_0_OR_GREATER
    private readonly struct SuppressThrowingAwaiter(Task task) : System.Runtime.CompilerServices.ICriticalNotifyCompletion
    {
        public SuppressThrowingAwaiter GetAwaiter() => this;
        public bool IsCompleted => task.IsCompleted;
        public void GetResult() { }
        public void OnCompleted(Action continuation) => task.GetAwaiter().OnCompleted(continuation);
        public void UnsafeOnCompleted(Action continuation) => task.GetAwaiter().UnsafeOnCompleted(continuation);
    }
#endif
}