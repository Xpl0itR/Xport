// Copyright © 2026 Xpl0itR
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xport.Abstractions;

namespace Xport.Middleware.ConnectionLimiter;

public sealed class ConnectionLimiterMiddleware : ITransportMiddleware<ConnectionContext>, ITransportMiddleware<MultiplexedConnectionContext>, IAsyncDisposable
{
    private readonly ILogger<ConnectionLimiterMiddleware> _logger;
    private readonly PartitionedRateLimiter<BaseConnectionContext> _limiter;
    private readonly Func<BaseConnectionContext, RateLimitLease, Task> _onRejected;

    public ConnectionLimiterMiddleware(
        ILogger<ConnectionLimiterMiddleware> logger,
        IOptions<ConnectionLimiterOptions> options,
        IEnumerable<IConnectionLimiterPolicy> policies)
    {
        PartitionedRateLimiter<BaseConnectionContext>[] limiters =
            policies.Select(static policy => policy.CreateLimiter())
                    .ToArray();

        _logger = logger;
        _limiter = limiters.Length switch
        {
            0 => ThrowHelper.ThrowInvalidOperationException<PartitionedRateLimiter<BaseConnectionContext>>(
                "No connection limiter policies were registered. Ensure that at least one policy is added to the service collection."),
            1 => limiters[0],
            _ => PartitionedRateLimiter.CreateChained(limiters)
        };
        _onRejected = options.Value.OnRejected;
    }

    public Task InvokeAsync(ConnectionContext connection, Func<ConnectionContext, Task> next) =>
        InvokeAsync<ConnectionContext>(connection, next);

    public Task InvokeAsync(MultiplexedConnectionContext connection, Func<MultiplexedConnectionContext, Task> next) =>
        InvokeAsync<MultiplexedConnectionContext>(connection, next);

    private Task InvokeAsync<TConnection>(TConnection connection, Func<TConnection, Task> next)
        where TConnection : BaseConnectionContext
    {
        RateLimitLease lease = _limiter.AttemptAcquire(connection, permitCount: 1);
        if (!lease.IsAcquired)
        {
            lease.Dispose();
            _logger.LogConnectionRejectedRateLimit(connection.ConnectionId);

            return _onRejected(connection, lease);
        }

        return HandleAsync(lease, connection, next);

        static async Task HandleAsync(RateLimitLease lease, TConnection connection, Func<TConnection, Task> next)
        {
            using (lease) await next(connection).ConfigureAwait(false);
        }
    }

    public ValueTask DisposeAsync() =>
        _limiter.DisposeAsync();
}
