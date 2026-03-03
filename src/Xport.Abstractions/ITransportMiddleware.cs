// Copyright © 2026 Xpl0itR
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Xport.Abstractions;

public interface ITransportMiddleware<TConnection> where TConnection : BaseConnectionContext
{
    Task InvokeAsync(TConnection connection, Func<TConnection, Task> next);
}