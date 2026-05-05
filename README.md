Xport
=====
A lightweight, Kestrel-inspired transport server for .NET.


Features
--------
- **Multiple Transports** — TCP sockets, Unix domain sockets, QUIC, and Named Pipes (Windows only)
- **Middleware Pipeline** — Compose connection handlers with the same middleware pattern used in ASP.NET Core
- **Hosted Service Integration** — Runs as an `IHostedService` alongside your existing .NET applications
- **Connection Management** — Built-in connection limiting with pluggable rate-limiting policies
- **AOT Compatible** — Supports native ahead-of-time compilation
- **Modern .NET** — Targets .NET 6 through .NET 10


Usage
-----
See [the example program](./example/Program.cs)

### Supported Transports
|         Transport         |        NuGet Package        |
|---------------------------|-----------------------------|
| TCP / UDS (SOCK_STREAM)   | `Xport.AspNetCore.Adapters` |
| QUIC                      | `Xport.AspNetCore.Adapters` |
| Named Pipes (Windows)     | `Xport.AspNetCore.Adapters` |
| KCP                       | Soon™️ |

### Custom Middleware
```csharp
public sealed class MyMiddleware : ITransportMiddleware<ConnectionContext>
{
    public async Task InvokeAsync(ConnectionContext connection, Func<ConnectionContext, Task> next)
    {
        // Do something before
        await next(connection);
        // Do something after
    }
}
```


Building
--------
Essentially just replicate the [CI instructions](.github/workflows/release.yml).


License
-------
This project is subject to the terms of the [Mozilla Public License, v. 2.0](./LICENSE).