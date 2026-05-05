// Copyright © 2026 Xpl0itR
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Xport.Middleware.ConnectionLimiter;

public sealed class ConnectionLimiterOptions
{
    private static readonly ConnectionAbortedException RateLimitExceededEx = new("Rate limit exceeded.");

    public Func<BaseConnectionContext, RateLimitLease, Task> OnRejected { get; set; } = AbortOnRejected;

    public static Task AbortOnRejected(BaseConnectionContext connection, RateLimitLease lease)
    {
        connection.Abort(RateLimitExceededEx);

        return Task.CompletedTask;
    }
}