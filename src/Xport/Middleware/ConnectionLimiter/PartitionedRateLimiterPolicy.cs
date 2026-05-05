// Copyright © 2026 Xpl0itR
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Connections;

namespace Xport.Middleware.ConnectionLimiter;

internal sealed class PartitionedRateLimiterPolicy(PartitionedRateLimiter<BaseConnectionContext> limiter) : IConnectionLimiterPolicy
{
    public PartitionedRateLimiter<BaseConnectionContext> CreateLimiter() => limiter;
}