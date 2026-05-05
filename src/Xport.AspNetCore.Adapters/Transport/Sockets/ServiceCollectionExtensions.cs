// Copyright © 2026 Xpl0itR
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.DependencyInjection;

namespace Xport.AspNetCore.Adapters.Transport.Sockets;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSocketTransport(this IServiceCollection services, Action<SocketTransportOptions> options) =>
        services.AddSocketTransport().Configure(options);

    public static IServiceCollection AddSocketTransport(this IServiceCollection services)
    {
#if NET10_0_OR_GREATER
        services.AddOptions<SocketTransportOptions>()
                .Configure(static (SocketTransportOptions options, IServiceProvider services) =>
                {
                    IMemoryPoolFactory<byte>? factory = services.GetService<IMemoryPoolFactory<byte>>();
                    if (factory is not null)
                        options.SetMemoryPoolFactory(factory);
                });
#endif
        return services.AddSingleton<IConnectionListenerFactory, SocketTransportFactory>();
    }

#if NET10_0_OR_GREATER
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_MemoryPoolFactory")]
    private static extern void SetMemoryPoolFactory(this SocketTransportOptions options, IMemoryPoolFactory<byte> factory);
#endif
}