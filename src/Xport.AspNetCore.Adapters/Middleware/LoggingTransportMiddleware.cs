// Copyright © 2026 Xpl0itR
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

#if NET10_0_OR_GREATER // UnsafeAccessorType is required
using System;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using Xport.Abstractions;

namespace Xport.AspNetCore.Adapters.Middleware;

public sealed class LoggingTransportMiddleware(ILogger<LoggingTransportMiddleware> logger)
    : ITransportMiddleware<MultiplexedConnectionContext>, ITransportMiddleware<ConnectionContext>
{
    public Task InvokeAsync(MultiplexedConnectionContext connection, Func<MultiplexedConnectionContext, Task> next) =>
        next((MultiplexedConnectionContext)LoggingMultiplexedConnectionContextCtor(connection, logger));

    public async Task InvokeAsync(ConnectionContext connection, Func<ConnectionContext, Task> next)
    {
        IDuplexPipe transport = connection.Transport;
        try
        {
            object loggingTransport = LoggingDuplexPipeCtor(transport, logger);
            await using (((IAsyncDisposable)loggingTransport).ConfigureAwait(false))
            {
                connection.Transport = (IDuplexPipe)loggingTransport;

                await next(connection).ConfigureAwait(false);
            }
        }
        finally
        {
            connection.Transport = transport;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.AspNetCore.Server.Kestrel.Core.Internal.LoggingMultiplexedConnectionMiddleware.LoggingMultiplexedConnectionContext, Microsoft.AspNetCore.Server.Kestrel.Core")]
    private static extern object LoggingMultiplexedConnectionContextCtor(MultiplexedConnectionContext inner, ILogger logger);

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType("Microsoft.AspNetCore.Server.Kestrel.Core.Internal.LoggingDuplexPipe, Microsoft.AspNetCore.Server.Kestrel.Core")]
    private static extern object LoggingDuplexPipeCtor(IDuplexPipe transport, ILogger logger);
}
#endif