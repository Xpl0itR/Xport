// Copyright © 2026 Xpl0itR
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Sockets;
using CommunityToolkit.Diagnostics;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Xport;

public sealed class TransportServerOptions(IServiceProvider applicationServices)
{
    private readonly List<TransportSocketOptions> _transportSocketOptions = [];

    [NotNull] private TransportServerConfig? Config =>
        field ??= applicationServices.GetRequiredService<IOptions<TransportServerConfig>>().Value;

    public IReadOnlyList<TransportSocketOptions> TransportSocketOptions =>
        _transportSocketOptions;

    public TransportServerOptions ListenIp(ReadOnlySpan<char> host, Action<TransportSocketOptions> configure) =>
        Listen(IPEndPoint.Parse(host), configure);

    public TransportServerOptions ListenIp(ReadOnlySpan<char> ip, int port, Action<TransportSocketOptions> configure) =>
        ListenIp(IPAddress.Parse(ip), port, configure);

    public TransportServerOptions ListenIp(IPAddress ip, int port, Action<TransportSocketOptions> configure) =>
        Listen(new IPEndPoint(ip, port), configure);

    public TransportServerOptions ListenHandle(ulong handle, Action<TransportSocketOptions> configure) =>
        ListenHandle(handle, FileHandleType.Auto, configure);

    public TransportServerOptions ListenHandle(ulong handle, FileHandleType type, Action<TransportSocketOptions> configure) =>
        Listen(new FileHandleEndPoint(handle, type), configure);

    public TransportServerOptions ListenNamedPipe(string pipeName, Action<TransportSocketOptions> configure) =>
        Listen(new NamedPipeEndPoint(pipeName), configure);

    public TransportServerOptions ListenUnixSocket(string socketPath, Action<TransportSocketOptions> configure)
    {
        if (!Path.IsPathRooted(socketPath))
            ThrowHelper.ThrowArgumentException(nameof(socketPath), "Unix socket path must be absolute.");

        return Listen(new UnixDomainSocketEndPoint(socketPath), configure);
    }

    public TransportServerOptions Listen(EndPoint endPoint, Action<TransportSocketOptions> configure)
    {
        TransportSocketOptions options = new(endPoint, applicationServices);

        configure(options);
        _transportSocketOptions.Add(options);

        return this;
    }

    public TransportServerOptions ConfigureEndpoint(string name, Action<TransportSocketOptions> configure)
    {
        if (Config.Endpoints is null || !Config.Endpoints.TryGetValue(name, out string? connectionString))
            return ThrowHelper.ThrowArgumentException<TransportServerOptions>(
                nameof(name), $"""An endpoint named "{name}" was not found in the config.""");

        EndPoint endpoint = EndPointFromConnectionString(connectionString);
        Listen(endpoint, configure);

        return this;
    }

    private static EndPoint EndPointFromConnectionString(ReadOnlySpan<char> connectionString)
    {
        const string unixSocketPrefix = "unix:/";
        const string namedPipePrefix = "pipe:/";

        if (connectionString.StartsWith(unixSocketPrefix, StringComparison.OrdinalIgnoreCase))
        {
            int lenPrefix = unixSocketPrefix.Length;
            if (!OperatingSystem.IsWindows())
                lenPrefix--;

            ReadOnlySpan<char> path = connectionString[lenPrefix..];

            if (!Path.IsPathRooted(path))
                ThrowHelper.ThrowArgumentException(nameof(connectionString), "Unix socket path must be absolute.");

            return new UnixDomainSocketEndPoint(path.ToString());
        }

        if (connectionString.StartsWith(namedPipePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return new NamedPipeEndPoint(
                connectionString[namedPipePrefix.Length..].ToString());
        }

        return IPEndPoint.Parse(connectionString);
    }
}