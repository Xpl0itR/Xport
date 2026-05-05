// Copyright © 2026 Xpl0itR
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Xport.Abstractions;
using Xport.Middleware.ConnectionLimiter;

namespace Xport;

public sealed class TransportSocketOptions(EndPoint endPoint, IServiceProvider applicationServices) : IConnectionBuilder, IMultiplexedConnectionBuilder
{
    private readonly IList<Func<Func<ConnectionContext, Task>, Func<ConnectionContext, Task>>> _singleplexedMiddleware = [];
    private readonly IList<Func<Func<MultiplexedConnectionContext, Task>, Func<MultiplexedConnectionContext, Task>>> _multiplexedMiddleware = [];

    internal string? TransportFactoryName;

    public EndPoint EndPoint { get; internal set; } = endPoint;

    public IServiceProvider ApplicationServices =>
        applicationServices;

    public FeatureCollection? Features { get; set; }

    public bool InjectScopedServiceProvider { get; set; } = true;

    public int SlotMapInitialCapacity { get; set; } = 256;

    public TransportSocketOptions UseTransport<TFactory>() where TFactory : IConnectionListenerFactory
    {
        TransportFactoryName = typeof(TFactory).FullName;
        return this;
    }

    public TransportSocketOptions UseMultiplexedTransport<TFactory>() where TFactory : IMultiplexedConnectionListenerFactory
    {
        TransportFactoryName = typeof(TFactory).FullName;
        return this;
    }

    public TransportSocketOptions UseSocketTransport()
    {
        TransportFactoryName = "Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets.SocketTransportFactory";
        return this;
    }

    public TransportSocketOptions UseNamedPipeTransport()
    {
        TransportFactoryName = "Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes.Internal.NamedPipeTransportFactory";
        return this;
    }

    public TransportSocketOptions UseQuicTransport()
    {
        TransportFactoryName = "Microsoft.AspNetCore.Server.Kestrel.Transport.Quic.QuicTransportFactory";
        return this;
    }

    public TransportSocketOptions Use(Func<Func<ConnectionContext, Task>, Func<ConnectionContext, Task>> middleware)
    {
        _singleplexedMiddleware.Add(middleware);
        return this;
    }

    public TransportSocketOptions Use(Func<Func<MultiplexedConnectionContext, Task>, Func<MultiplexedConnectionContext, Task>> middleware)
    {
        _multiplexedMiddleware.Add(middleware);
        return this;
    }

    public TransportSocketOptions Use(Func<ConnectionContext, Func<ConnectionContext, Task>, Task> middleware) =>
        Use(next => context => middleware(context, next));

    public TransportSocketOptions Use(Func<MultiplexedConnectionContext, Func<MultiplexedConnectionContext, Task>, Task> middleware) =>
        Use(next => context => middleware(context, next));

    public TransportSocketOptions UseMiddleware<TTransportMiddleware>() where TTransportMiddleware : ITransportMiddleware<ConnectionContext> =>
        UseMiddleware(
            ApplicationServices.GetRequiredService<TTransportMiddleware>());

    public TransportSocketOptions UseMiddleware<TTransportMiddleware>(TTransportMiddleware middleware)
        where TTransportMiddleware : ITransportMiddleware<ConnectionContext>
    {
        if (middleware is ITransportMiddleware<MultiplexedConnectionContext> multiplexedMiddleware)
            Use(next => context => multiplexedMiddleware.InvokeAsync(context, next));

        return Use(next => context => middleware.InvokeAsync(context, next));
    }

    public TransportSocketOptions UseMultiplexedMiddleware<TTransportMiddleware>() where TTransportMiddleware : ITransportMiddleware<MultiplexedConnectionContext> =>
        UseMultiplexedMiddleware(
            ApplicationServices.GetRequiredService<TTransportMiddleware>());

    public TransportSocketOptions UseMultiplexedMiddleware<TTransportMiddleware>(TTransportMiddleware middleware) where TTransportMiddleware : ITransportMiddleware<MultiplexedConnectionContext> =>
        Use(next => context => middleware.InvokeAsync(context, next));

    public TransportSocketOptions Run(Func<ConnectionContext, Task> handler) =>
        Use(_ => handler);

    public TransportSocketOptions Run(Func<MultiplexedConnectionContext, Task> handler) =>
        Use(_ => handler);

    public TransportSocketOptions UseConnectionHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TConnectionHandler>()
        where TConnectionHandler : ConnectionHandler
    {
        TConnectionHandler handler = ActivatorUtilities.GetServiceOrCreateInstance<TConnectionHandler>(ApplicationServices);

        if (handler is IMultiplexedConnectionHandler multiplexedHandler)
            Use(_ => context => multiplexedHandler.OnConnectedAsync(context));

        Use(_ => context => handler.OnConnectedAsync(context));

        return this;
    }

    public TransportSocketOptions UseMultiplexedConnectionHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TConnectionHandler>()
        where TConnectionHandler : IMultiplexedConnectionHandler
    {
        TConnectionHandler handler = ActivatorUtilities.GetServiceOrCreateInstance<TConnectionHandler>(ApplicationServices);

        Use(_ => context => handler.OnConnectedAsync(context));

        return this;
    }

    public TransportSocketOptions UseConnectionLimiterMiddleware() =>
        UseMiddleware<ConnectionLimiterMiddleware>();

    public Func<ConnectionContext, Task> BuildHandler()
    {
        Func<ConnectionContext, Task> handler = static _ => Task.CompletedTask;

        for (int i = _singleplexedMiddleware.Count - 1; i >= 0; i--)
            handler = _singleplexedMiddleware[i](handler);

        return handler;
    }

    public Func<MultiplexedConnectionContext, Task> BuildMultiplexedHandler()
    {
        Func<MultiplexedConnectionContext, Task> handler = static _ => Task.CompletedTask;

        for (int i = _multiplexedMiddleware.Count - 1; i >= 0; i--)
            handler = _multiplexedMiddleware[i](handler);

        return handler;
    }

    #region Compatibility
    private static TDelegate UnsafeAs<TDelegate>(Delegate @delegate) where TDelegate : Delegate =>
        (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), @delegate.Target, @delegate.Method);

    ConnectionDelegate IConnectionBuilder.Build() =>
        UnsafeAs<ConnectionDelegate>(BuildHandler());

    MultiplexedConnectionDelegate IMultiplexedConnectionBuilder.Build() =>
        UnsafeAs<MultiplexedConnectionDelegate>(BuildMultiplexedHandler());

    IConnectionBuilder IConnectionBuilder.Use(Func<ConnectionDelegate, ConnectionDelegate> middleware)
    {
        _singleplexedMiddleware.Add(
            UnsafeAs<Func<Func<ConnectionContext, Task>, Func<ConnectionContext, Task>>>(middleware));

        return this;
    }

    IMultiplexedConnectionBuilder IMultiplexedConnectionBuilder.Use(Func<MultiplexedConnectionDelegate, MultiplexedConnectionDelegate> middleware)
    {
        _multiplexedMiddleware.Add(
            UnsafeAs<Func<Func<MultiplexedConnectionContext, Task>, Func<MultiplexedConnectionContext, Task>>>(middleware));

        return this;
    }
    #endregion
}