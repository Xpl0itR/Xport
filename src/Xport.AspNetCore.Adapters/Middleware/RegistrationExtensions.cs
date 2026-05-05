// Copyright © 2026 Xpl0itR
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

#if NET10_0_OR_GREATER
using Microsoft.Extensions.DependencyInjection;
using Xport.AspNetCore.Adapters.Middleware;

namespace Xport
{
    public static class TransportSocketOptionsExtensions
    {
        public static TransportSocketOptions UseLoggingTransportMiddleware(this TransportSocketOptions options) =>
            options.UseMiddleware<LoggingTransportMiddleware>();
    }
}

namespace Xport.Hosting
{
    public static class HostingExtensions
    {
        public static IServiceCollection AddLoggingTransportMiddleware(this IServiceCollection services) =>
            services.AddSingleton<LoggingTransportMiddleware>();
    }
}
#endif