// Copyright © 2026 Xpl0itR
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.ObjectPool;
using Xport.Middleware.ConnectionLimiter;

namespace Xport.Hosting;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTransportServer(this IServiceCollection services, IConfiguration? configuration = null)
    {
        if (configuration is not null)
            services.Configure<TransportServerConfig>(configuration);

        services.TryAddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();

        return services.AddSingleton<TransportServer>();
    }

    public static IServiceCollection AddHostedTransportServer(this IServiceCollection services, IConfiguration? configuration = null) =>
        services.AddTransportServer(configuration)
                .AddHostedService(static services => services.GetRequiredService<TransportServer>());

    public static IServiceCollection AddConnectionLimiterMiddleware(this IServiceCollection services) =>
        services.AddSingleton<ConnectionLimiterMiddleware>();

    public static IServiceCollection AddConnectionLimiterMiddleware(this IServiceCollection services, Action<ConnectionLimiterOptions> configure) =>
        services.AddConnectionLimiterMiddleware()
                .Configure(configure);

    public static IServiceCollection AddConnectionLimiterPolicy<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPolicy>(this IServiceCollection services)
            where TPolicy : class, IConnectionLimiterPolicy =>
                services.AddSingleton<IConnectionLimiterPolicy, TPolicy>();

    public static IServiceCollection AddConnectionLimiterPolicy(
        this IServiceCollection services, PartitionedRateLimiter<BaseConnectionContext> limiter) =>
            services.AddSingleton<IConnectionLimiterPolicy>(new PartitionedRateLimiterPolicy(limiter));

    public static IServiceCollection AddConnectionLimiterPolicy(
        this IServiceCollection services, Func<IServiceProvider, PartitionedRateLimiter<BaseConnectionContext>> factory) =>
            services.AddSingleton<IConnectionLimiterPolicy>(sp => new PartitionedRateLimiterPolicy(factory(sp)));
}