// Copyright © 2026 Xpl0itR
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xport;
using Xport.AspNetCore.Adapters.Transport.Sockets;
using Xport.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Logging.SetMinimumLevel(LogLevel.Debug);
builder.Services.AddSocketTransport()
                .AddHostedTransportServer(builder.Configuration.GetSection("Xport"))
                .AddLoggingTransportMiddleware()
                .AddConnectionLimiterMiddleware()
                .AddConnectionLimiterPolicy(
                    PartitionedRateLimiter.Create<BaseConnectionContext, string>(_ =>
                        RateLimitPartition.GetConcurrencyLimiter("global", _ =>
                            new ConcurrencyLimiterOptions { PermitLimit = 10000 })));

using IHost host = builder.Build();

host.ConfigureTransportServer(static options =>
{
    Action<TransportSocketOptions> configure = static options =>
        options.UseSocketTransport()
               .UseConnectionLimiterMiddleware()
               .UseLoggingTransportMiddleware()
               .Run(static async conn =>
               {
                   await conn.Transport.Output.WriteAsync("Welcome to the echo test server!\n"u8.ToArray(), conn.ConnectionClosed);
                   await conn.Transport.Input.CopyToAsync(conn.Transport.Output, conn.ConnectionClosed);
               });

    options.ListenIp(IPAddress.Loopback, 1234, configure); // programmatically configure an endpoint
    options.ConfigureEndpoint("EchoServer", configure);    // configure an endpoint from configuration (see appsettings.json)
});

await host.RunAsync();