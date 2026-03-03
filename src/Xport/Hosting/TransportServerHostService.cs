// Copyright © 2026 Xpl0itR
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Xport.Hosting;

public class TransportServerHostService(TransportServer server) : IHostedService
{
    public virtual Task StartAsync(CancellationToken ct) =>
        server.StartAsync(ct);

    public virtual Task StopAsync(CancellationToken ct) =>
        server.StopAsync(ct);
}