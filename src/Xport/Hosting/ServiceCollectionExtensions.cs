// Copyright © 2026 Xpl0itR
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Xport.Hosting;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTransportServer(this IServiceCollection services) =>
        services.AddTransportServer<TransportServerHostService>();

    public static IServiceCollection AddTransportServer<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THostedTransportServer>(
        this IServiceCollection services)
            where THostedTransportServer : TransportServerHostService
    {
        services.AddSingleton<TransportServer>();
        services.AddHostedService<THostedTransportServer>();

        return services;
    }
}