// Copyright © 2026 Xpl0itR
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xport;
using Xport.AspNetCore.Adapters.Middleware;
using Xport.AspNetCore.Adapters.Transport.Sockets;
using Xport.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSocketsTransport()
                .AddTransportServer()
                .AddSingleton<LoggingTransportMiddleware>()
                .Configure<TransportServerConfig>(
                    builder.Configuration.GetSection("Xport"));

using IHost host = builder.Build();

host.ConfigureTransportServer(static options =>
{
    Action<TransportSocketOptions> configure = static options =>
        options.UseMiddleware<LoggingTransportMiddleware>()
               .Run(static async conn =>
               {
                   await conn.Transport.Output.WriteAsync("Welcome to the echo test server!\n"u8.ToArray(), conn.ConnectionClosed);
                   await conn.Transport.Input.CopyToAsync(conn.Transport.Output, conn.ConnectionClosed);
               });

    options.ListenIp(IPAddress.Loopback, 1234, configure);
    options.ConfigureEndpoint("EchoServer", configure);
});

await host.RunAsync();