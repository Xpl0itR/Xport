// Copyright © 2026 Xpl0itR
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Xport;

internal sealed class TransportSocket : IThreadPoolWorkItem, IAsyncDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    private readonly IConnectionListener? _listener;
    private readonly Func<ConnectionContext, Task>? _connectionHandler;

    private readonly IMultiplexedConnectionListener? _multiplexedListener;
    private readonly Func<MultiplexedConnectionContext, Task>? _multiplexedConnectionHandler;

    private ConcurrentDictionary<string, WeakReference<TransportConnection>>? _connections;
    private TaskCompletionSource? _tcs;

    internal TransportSocket(TransportSocketOptions options, IConnectionListener listener)
    {
        _serviceProvider = options.ApplicationServices;
        _logger = _serviceProvider.GetRequiredService<ILogger<TransportSocket>>();
        _listener = listener;
        _connectionHandler = options.BuildHandler();
    }

    internal TransportSocket(TransportSocketOptions options, IMultiplexedConnectionListener listener)
    {
        _serviceProvider = options.ApplicationServices;
        _logger = _serviceProvider.GetRequiredService<ILogger<TransportSocket>>();
        _multiplexedListener = listener;
        _multiplexedConnectionHandler = options.BuildMultiplexedHandler();
    }

    internal async Task UnbindAsync(CancellationToken ct)
    {
        await (_listener?.UnbindAsync(ct) 
            ?? _multiplexedListener!.UnbindAsync(ct));

        if (_tcs is not null)
            await _tcs.Task.ConfigureAwait(false);
    }

    internal Task CloseAllConnectionsAsync(CancellationToken ct)
    {
        if (_connections is null)
            return Task.CompletedTask;

        List<Task> closeTasks = new(_connections.Count);
        foreach (WeakReference<TransportConnection> reference in _connections.Values)
            if (reference.TryGetTarget(out TransportConnection? connection))
                closeTasks.Add(
                    connection.CloseAsync(ct));

        _connections.Clear();

        return Task.WhenAll(closeTasks);
    }

    public ValueTask DisposeAsync() =>
        _listener?.DisposeAsync()
     ?? _multiplexedListener!.DisposeAsync();

    async void IThreadPoolWorkItem.Execute()
    {
        try
        {
            _connections = [];
            _tcs = new TaskCompletionSource();

            if (_multiplexedListener is not null)
            {
                while (true)
                {
                    MultiplexedConnectionContext? connection = await _multiplexedListener.AcceptAsync().ConfigureAwait(false);
                    if (connection is null)
                        break;

                    HandleConnection(connection, _multiplexedConnectionHandler!);
                }
            }
            else
            {
                while (true)
                {
                    ConnectionContext? connection = await _listener!.AcceptAsync().ConfigureAwait(false);
                    if (connection is null)
                        break;

                    HandleConnection(connection, _connectionHandler!);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogUnhandledExceptionAsyncVoid(ex);
        }
        finally
        {
            _tcs?.TrySetResult();
        }
    }

    private void HandleConnection<TConnection>(
        TConnection connection, Func<TConnection, Task> connectionHandler)
            where TConnection : BaseConnectionContext
    {
        _logger.LogConnectionAccepted(connection.ConnectionId);

        TransportConnection<TConnection> transportConnection = new(
            _serviceProvider, connection, connectionHandler, UnregisterConnection);

        _connections!.TryAdd(
            connection.ConnectionId,
            new WeakReference<TransportConnection>(transportConnection));

        ThreadPool.UnsafeQueueUserWorkItem(transportConnection, preferLocal: false);
    }

    private void UnregisterConnection(string id) =>
        _connections!.TryRemove(id, out _);
}