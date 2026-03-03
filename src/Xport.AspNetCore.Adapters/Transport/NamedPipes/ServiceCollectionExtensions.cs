// Copyright © 2026 Xpl0itR
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

#if NET10_0_OR_GREATER // UnsafeAccessorType is required
using System;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;

namespace Xport.AspNetCore.Adapters.Transport.NamedPipes;

public static class ServiceCollectionExtensions
{
    [SupportedOSPlatform("windows")]
    public static IServiceCollection AddNamedPipeTransport(this IServiceCollection services, Action<NamedPipeTransportOptions> options) =>
        services.AddNamedPipeTransport().Configure(options);

    [SupportedOSPlatform("windows")]
    public static IServiceCollection AddNamedPipeTransport(this IServiceCollection services)
    {
        services.TryAddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        services.AddOptions<NamedPipeTransportOptions>()
                .Configure(static (NamedPipeTransportOptions options, IServiceProvider services) =>
                {
                    IMemoryPoolFactory<byte>? factory = services.GetService<IMemoryPoolFactory<byte>>();
                    if (factory is not null)
                        options.SetMemoryPoolFactory(factory);
                });

        return services.AddSingleton<IConnectionListenerFactory>(static services =>
            (IConnectionListenerFactory)
            NamedPipeTransportFactoryCtor(
                services.GetRequiredService<ILoggerFactory>(),
                services.GetRequiredService<IOptions<NamedPipeTransportOptions>>(),
                services.GetRequiredService<ObjectPoolProvider>()));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes.Internal.NamedPipeTransportFactory, Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes")]
    private static extern object NamedPipeTransportFactoryCtor(ILoggerFactory loggerFactory, IOptions<NamedPipeTransportOptions> options, ObjectPoolProvider objectPoolProvider);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_MemoryPoolFactory")]
    private static extern void SetMemoryPoolFactory(this NamedPipeTransportOptions options, IMemoryPoolFactory<byte> factory);
}
#endif