// Copyright © 2026 Xpl0itR
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace Xport;

internal sealed class TransportSocket<TConnection> : TransportSocket, IThreadPoolWorkItem
    where TConnection : BaseConnectionContext
{
    internal readonly TransportSocketOptions Options;
    internal readonly Func<TConnection, Task> ConnectionHandler;
    internal readonly ObjectPool<TransportConnection<TConnection>> ConnObjPool;

    private readonly Func<CancellationToken, ValueTask<TConnection?>> _acceptAsync;
    private readonly ILogger _logger;

    internal TransportSocket(
        ObjectPoolProvider objectPoolProvider,
        TransportSocketOptions options,
        Func<TConnection, Task> connectionHandler,
        Func<CancellationToken, ValueTask<TConnection?>> acceptAsync,
        Func<CancellationToken, ValueTask> unbindAsync,
        IAsyncDisposable listener)
            : base(unbindAsync, listener, options.SlotMapInitialCapacity)
    {
        Options = options;
        ConnectionHandler = connectionHandler;
        ConnObjPool = objectPoolProvider.Create(new PoolingPolicy(this));
        _acceptAsync = acceptAsync;
        _logger = options.ApplicationServices.GetRequiredService<ILogger<TransportSocket>>();
    }

    async void IThreadPoolWorkItem.Execute()
    {
        try
        {
            while (true)
            {
                TConnection? connection = await _acceptAsync(CancellationToken.None).ConfigureAwait(false);
                if (connection is null)
                    break;

                _logger.LogConnectionAccepted(connection.ConnectionId);

                TransportConnection<TConnection> transportConnection = ConnObjPool.Get();
                int slot = ConnectionSlots.Insert(transportConnection);

                transportConnection.Initialize(connection, slot);
                ThreadPool.UnsafeQueueUserWorkItem(transportConnection, preferLocal: false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogUnhandledExceptionAsyncVoid(ex);
        }
        finally
        {
            Tcs.TrySetResult();
        }
    }

    private sealed class PoolingPolicy(TransportSocket<TConnection> socket)
        : IPooledObjectPolicy<TransportConnection<TConnection>>
    {
        private readonly ILogger _connectionLogger =
            socket.Options.ApplicationServices.GetRequiredService<ILogger<TransportConnection>>();

        public TransportConnection<TConnection> Create() =>
            new(socket, _connectionLogger);

        public bool Return(TransportConnection<TConnection> obj) =>
            true;
    }
}

internal abstract class TransportSocket(
    Func<CancellationToken, ValueTask> unbindAsync, IAsyncDisposable listener, int slotMapInitialCapacity)
{
    internal readonly SlotMap<TransportConnection> ConnectionSlots = new(slotMapInitialCapacity);

    protected readonly TaskCompletionSource Tcs = new();

    internal async Task ShutdownAsync(CancellationToken ct)
    {
        await unbindAsync(ct).ConfigureAwait(false);
        await Tcs.Task.ConfigureAwait(false);

        List<Task> closeTasks = [];
        foreach (TransportConnection connection in ConnectionSlots)
            closeTasks.Add(
                connection.CloseAsync(ct));

        if (closeTasks.Count > 0)
        {
            ConnectionSlots.Clear();
            await Task.WhenAll(closeTasks).ConfigureAwait(false);
        }

        await listener.DisposeAsync().ConfigureAwait(false);
    }
}