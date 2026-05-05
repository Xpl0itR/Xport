// Copyright © 2026 Xpl0itR
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Xport.Hosting;

public static class HostExtensions
{
    public static IHost ConfigureTransportServer(this IHost host, Action<TransportServerOptions> configure)
    {
        TransportServer server = host.Services.GetRequiredService<TransportServer>();
        configure(server.Options);

        return host;
    }

    public static IHost ConfigureTransportServer(this IHost host, Action<IServiceProvider, TransportServerOptions> configure)
    {
        TransportServer server = host.Services.GetRequiredService<TransportServer>();
        configure(host.Services, server.Options);

        return host;
    }
}