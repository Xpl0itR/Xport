// Copyright © 2026 Xpl0itR
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

#if NET10_0_OR_GREATER // UnsafeAccessorType is required
using System;
using System.Net.Quic;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Quic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Xport.AspNetCore.Adapters.Transport.Quic;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddQuicTransport(this IServiceCollection services, Action<QuicTransportOptions> options) =>
        services.AddQuicTransport().Configure(options);

    public static IServiceCollection AddQuicTransport(this IServiceCollection services)
    {
        if (QuicListener.IsSupported)
            services.AddSingleton<IMultiplexedConnectionListenerFactory>(static services =>
                (IMultiplexedConnectionListenerFactory)
                QuicTransportFactoryCtor(
                    services.GetRequiredService<ILoggerFactory>(),
                    services.GetRequiredService<IOptions<QuicTransportOptions>>()));

        return services;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.AspNetCore.Server.Kestrel.Transport.Quic.QuicTransportFactory, Microsoft.AspNetCore.Server.Kestrel.Transport.Quic")]
    private static extern object QuicTransportFactoryCtor(ILoggerFactory loggerFactory, IOptions<QuicTransportOptions> options);
}
#endif