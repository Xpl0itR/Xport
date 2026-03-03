// Copyright © 2026 Xpl0itR
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace Xport;

public sealed class TransportServer : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly List<IConnectionListenerFactory> _transportFactories;
    private readonly List<IMultiplexedConnectionListenerFactory> _multiplexedFactories;
    private readonly List<TransportSocket> _transportSockets;

    public TransportServer(
        IServiceProvider applicationServices,
        ILogger<TransportServer> logger,
        IEnumerable<IConnectionListenerFactory> transportFactories,
        IEnumerable<IMultiplexedConnectionListenerFactory> multiplexedFactories)
    {
        _logger = logger;
        _transportFactories = transportFactories.Reverse().ToList();
        _multiplexedFactories = multiplexedFactories.Reverse().ToList();
        if (_transportFactories.Count == 0 && _multiplexedFactories.Count == 0)
            ThrowHelper.ThrowInvalidOperationException("No transport factories were registered. Ensure that at least one transport is added to the service collection.");

        _transportSockets = [];
        Options = new TransportServerOptions(applicationServices);
    }

    public TransportServerOptions Options { get; }

    public async Task StartAsync(CancellationToken ct)
    {
        if (Options.TransportSocketOptions.Count == 0)
            ThrowHelper.ThrowInvalidOperationException("No transport socket options were registered. Ensure that at least one TransportSocketOptions instance is added to TransportServerOptions.");

        foreach (TransportSocketOptions options in Options.TransportSocketOptions) // TODO: rewrite this so each endpoint can specify which transport protocol it wants to use instead of trying to guess based on the endpoint type
        {
            TransportSocket transportSocket;
            if (TryGetMultiplexedTransportFactory(options.EndPoint, out IMultiplexedConnectionListenerFactory? listenerFactory))
            {
                IMultiplexedConnectionListener listener = await listenerFactory.BindAsync(options.EndPoint, options.Features, ct).ConfigureAwait(false);
                transportSocket = new TransportSocket(options, listener);

                _logger.LogListeningOnEndpoint(listener.EndPoint);
            }
            else
            {
                IConnectionListener listener = await GetTransportFactory(options.EndPoint).BindAsync(options.EndPoint, ct).ConfigureAwait(false);
                transportSocket = new TransportSocket(options, listener);

                _logger.LogListeningOnEndpoint(listener.EndPoint);
            }

            _transportSockets.Add(transportSocket);
            ThreadPool.UnsafeQueueUserWorkItem(transportSocket, preferLocal: false);
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        Task[] tasks = new Task[_transportSockets.Count];

        for (int i = 0; i < tasks.Length; i++)
            tasks[i] = _transportSockets[i].UnbindAsync(ct);

        await Task.WhenAll(tasks).ConfigureAwait(false);

        for (int i = 0; i < tasks.Length; i++)
            tasks[i] = _transportSockets[i].CloseAllConnectionsAsync(ct);

        await Task.WhenAll(tasks).ConfigureAwait(false);

        for (int i = 0; i < tasks.Length; i++)
            tasks[i] = _transportSockets[i].DisposeAsync().AsTask();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        _transportSockets.Clear();
    }

    private bool TryGetMultiplexedTransportFactory(EndPoint endpoint, [NotNullWhen(true)] out IMultiplexedConnectionListenerFactory? listenerFactory)
    {
        foreach (IMultiplexedConnectionListenerFactory factory in _multiplexedFactories)
        {
            if (CanBind(factory as IConnectionListenerFactorySelector, endpoint))
            {
                listenerFactory = factory;
                return true;
            }
        }

        listenerFactory = null;
        return false;
    }

    private IConnectionListenerFactory GetTransportFactory(EndPoint endpoint)
    {
        foreach (IConnectionListenerFactory factory in _transportFactories)
        {
            if (CanBind(factory as IConnectionListenerFactorySelector, endpoint))
            {
                return factory;
            }
        }

        return ThrowHelper.ThrowInvalidOperationException<IConnectionListenerFactory>(
            $"No transport factory registered for endpoint type {endpoint.GetType().Name}.");
    }

    private static bool CanBind(IConnectionListenerFactorySelector? selector, EndPoint endpoint) =>
        selector?.CanBind(endpoint) ?? true; // Backwards compatibility for pre-net8

    public ValueTask DisposeAsync() =>
        new(StopAsync(new CancellationToken(canceled: true)));
}