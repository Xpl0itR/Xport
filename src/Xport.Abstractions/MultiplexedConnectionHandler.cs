// Copyright © 2026 Xpl0itR
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Xport.Abstractions;

public abstract class MultiplexedConnectionHandler : ConnectionHandler, IMultiplexedConnectionHandler
{
    public virtual async Task OnConnectedAsync(MultiplexedConnectionContext connection)
    {
        while (true)
        {
            ConnectionContext? stream = await connection.AcceptAsync().ConfigureAwait(false);
            if (stream is null)
                break;

            _ = HandleStreamAsync(stream);
        }
    }

    private async Task HandleStreamAsync(ConnectionContext stream)
    {
        try
        {
            await OnConnectedAsync(stream).ConfigureAwait(false);
        }
        finally
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }
}