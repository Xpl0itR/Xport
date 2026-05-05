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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace Xport;

public sealed class TransportServer : IHostedService, IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly ObjectPoolProvider _objectPoolProvider;
    private readonly Dictionary<string, IConnectionListenerFactory> _singleplexedFactories;
    private readonly Dictionary<string, IMultiplexedConnectionListenerFactory> _multiplexedFactories;
    private readonly List<TransportSocket> _transportSockets;

    public TransportServer(
        IServiceProvider applicationServices,
        ObjectPoolProvider objectPoolProvider,
        ILogger<TransportServer> logger,
        IEnumerable<IConnectionListenerFactory> singleplexedFactories,
        IEnumerable<IMultiplexedConnectionListenerFactory> multiplexedFactories)
    {
        _logger = logger;
        _objectPoolProvider    = objectPoolProvider;
        _singleplexedFactories = singleplexedFactories.Reverse().ToDictionary(static factory => factory.GetType().FullName!);
        _multiplexedFactories  = multiplexedFactories .Reverse().ToDictionary(static factory => factory.GetType().FullName!);

        if (_singleplexedFactories.Count == 0 && _multiplexedFactories.Count == 0)
            ThrowHelper.ThrowInvalidOperationException("No transport factories were registered. Ensure that at least one transport is added to the service collection.");

        _transportSockets = [];
        Options = new TransportServerOptions(applicationServices);
    }

    public TransportServerOptions Options { get; }

    public async Task StartAsync(CancellationToken ct)
    {
        if (Options.TransportSocketOptions.Count == 0)
            ThrowHelper.ThrowInvalidOperationException("No transport socket options were registered. Ensure that at least one TransportSocketOptions instance is added to TransportServerOptions.");

        foreach (TransportSocketOptions options in Options.TransportSocketOptions)
        {
            IConnectionListenerFactory? singleplexedFactory;
            IMultiplexedConnectionListenerFactory? multiplexedFactory;

            if (options.TransportFactoryName is null)
            {
                if (TryGetSingleplexedTransportFactory(options.EndPoint, out singleplexedFactory))
                    goto singleplexed;

                if (TryGetMultiplexedTransportFactory(options.EndPoint, out multiplexedFactory))
                    goto multiplexed;

                ThrowHelper.ThrowInvalidOperationException(
                    $"No transport factory registered for endpoint type {options.EndPoint.GetType().Name}.");
            }

            if (_singleplexedFactories.TryGetValue(options.TransportFactoryName, out singleplexedFactory))
                goto singleplexed;

            if (_multiplexedFactories.TryGetValue(options.TransportFactoryName, out multiplexedFactory))
                goto multiplexed;

            ThrowHelper.ThrowInvalidOperationException(
                $"No transport factory registered with name {options.TransportFactoryName}.");

        singleplexed:
            IConnectionListener singleplexedListener = await singleplexedFactory.BindAsync(options.EndPoint, ct).ConfigureAwait(false);
            TransportSocket<ConnectionContext> singleplexedSocket = new(
                _objectPoolProvider,
                options,
                options.BuildHandler(),
                singleplexedListener.AcceptAsync,
                singleplexedListener.UnbindAsync,
                singleplexedListener);

            ThreadPool.UnsafeQueueUserWorkItem(singleplexedSocket, preferLocal: false);
            _transportSockets.Add(singleplexedSocket);
            _logger.LogListeningOnEndpoint(singleplexedListener.EndPoint, singleplexedListener.GetType().Name);
            continue;

        multiplexed:
            IMultiplexedConnectionListener multiplexedListener = await multiplexedFactory.BindAsync(options.EndPoint, options.Features, ct).ConfigureAwait(false);
            TransportSocket<MultiplexedConnectionContext> multiplexedSocket = new(
                _objectPoolProvider,
                options,
                options.BuildMultiplexedHandler(),
                token => multiplexedListener.AcceptAsync(null, token),
                multiplexedListener.UnbindAsync,
                multiplexedListener);

            ThreadPool.UnsafeQueueUserWorkItem(multiplexedSocket, preferLocal: false);
            _transportSockets.Add(multiplexedSocket);
            _logger.LogListeningOnEndpoint(multiplexedListener.EndPoint, multiplexedListener.GetType().Name);
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        Task[] tasks = new Task[_transportSockets.Count];

        for (int i = 0; i < tasks.Length; i++)
            tasks[i] = _transportSockets[i].ShutdownAsync(ct);

        await Task.WhenAll(tasks).ConfigureAwait(false);

        _transportSockets.Clear();
    }

    public ValueTask DisposeAsync() =>
        new(StopAsync(new CancellationToken(canceled: true)));

    private bool TryGetSingleplexedTransportFactory(EndPoint endpoint, [NotNullWhen(true)] out IConnectionListenerFactory? listenerFactory)
    {
        foreach (IConnectionListenerFactory factory in _singleplexedFactories.Values)
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

    private bool TryGetMultiplexedTransportFactory(EndPoint endpoint, [NotNullWhen(true)] out IMultiplexedConnectionListenerFactory? listenerFactory)
    {
        foreach (IMultiplexedConnectionListenerFactory factory in _multiplexedFactories.Values)
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

    private static bool CanBind(IConnectionListenerFactorySelector? selector, EndPoint endpoint) =>
        selector?.CanBind(endpoint) ?? true; // Backwards compatibility for pre IConnectionListenerFactorySelector (net8)
}