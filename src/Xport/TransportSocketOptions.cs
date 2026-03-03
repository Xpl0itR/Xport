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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Xport.Abstractions;

namespace Xport;

public sealed class TransportSocketOptions(EndPoint endPoint, IServiceProvider applicationServices) : IConnectionBuilder, IMultiplexedConnectionBuilder
{
    private readonly IList<Func<Func<ConnectionContext, Task>, Func<ConnectionContext, Task>>> _middleware = [];
    private readonly IList<Func<Func<MultiplexedConnectionContext, Task>, Func<MultiplexedConnectionContext, Task>>> _multiplexedMiddleware = [];

    public EndPoint EndPoint =>
        endPoint;

    public IServiceProvider ApplicationServices =>
        applicationServices;

    public FeatureCollection? Features { get; set; }

    public Func<ConnectionContext, Task> BuildHandler()
    {
        Func<ConnectionContext, Task> handler = static _ => Task.CompletedTask;

        foreach (Func<Func<ConnectionContext, Task>, Func<ConnectionContext, Task>> next in _middleware.Reverse())
            handler = next(handler);

        return handler;
    }

    public Func<MultiplexedConnectionContext, Task> BuildMultiplexedHandler()
    {
        Func<MultiplexedConnectionContext, Task> handler = static _ => Task.CompletedTask;

        foreach (Func<Func<MultiplexedConnectionContext, Task>, Func<MultiplexedConnectionContext, Task>> next in _multiplexedMiddleware.Reverse())
            handler = next(handler);

        return handler;
    }

    public TransportSocketOptions Use(Func<Func<ConnectionContext, Task>, Func<ConnectionContext, Task>> middleware)
    {
        _middleware.Add(middleware);
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

    public TransportSocketOptions UseMiddleware<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TTransportMiddleware>(bool optional = false)
        where TTransportMiddleware : ITransportMiddleware<ConnectionContext>
    {
        TTransportMiddleware? middleware = ApplicationServices.GetService<TTransportMiddleware>();
        if (middleware is null)
        {
            if (optional) return this;
            middleware = ActivatorUtilities.CreateInstance<TTransportMiddleware>(ApplicationServices);
        }

        if (middleware is ITransportMiddleware<MultiplexedConnectionContext> multiplexedMiddleware)
            Use(next => context => multiplexedMiddleware.InvokeAsync(context, next));

        Use(next => context => middleware.InvokeAsync(context, next));

        return this;
    }

    public TransportSocketOptions UseMultiplexedMiddleware<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TTransportMiddleware>(bool optional = false)
        where TTransportMiddleware : ITransportMiddleware<MultiplexedConnectionContext>
    {
        TTransportMiddleware? middleware = ApplicationServices.GetService<TTransportMiddleware>();
        if (middleware is null)
        {
            if (optional) return this;
            middleware = ActivatorUtilities.CreateInstance<TTransportMiddleware>(ApplicationServices);
        }

        Use(next => context => middleware.InvokeAsync(context, next));

        return this;
    }

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

    #region Compatibility
    private static TDelegate UnsafeAs<TDelegate>(Delegate @delegate) where TDelegate : Delegate =>
        (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), @delegate.Target, @delegate.Method);

    ConnectionDelegate IConnectionBuilder.Build() =>
        UnsafeAs<ConnectionDelegate>(BuildHandler());

    MultiplexedConnectionDelegate IMultiplexedConnectionBuilder.Build() =>
        UnsafeAs<MultiplexedConnectionDelegate>(BuildMultiplexedHandler());

    IConnectionBuilder IConnectionBuilder.Use(Func<ConnectionDelegate, ConnectionDelegate> middleware)
    {
        _middleware.Add(
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