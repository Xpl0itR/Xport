// Copyright © 2026 Xpl0itR
// 
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Net;
using Microsoft.Extensions.Logging;

namespace Xport;

internal static partial class LoggerMessages
{
    [LoggerMessage(LogLevel.Error, "An unhandled exception has occurred in an async void method.", EventName = "UnhandledExceptionAsyncVoid")]
    internal static partial void LogUnhandledExceptionAsyncVoid(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Warning, """Connection id "{connectionId}" rejected: rate limit exceeded.""", EventName = "ConnectionRejectedRateLimit")]
    public static partial void LogConnectionRejectedRateLimit(this ILogger logger, string connectionId);

    [LoggerMessage(LogLevel.Information, "Listening on: {endpoint} using {transportName}.", EventName = "ListeningOnEndpoint")]
    internal static partial void LogListeningOnEndpoint(this ILogger logger, EndPoint endpoint, string transportName);

    [LoggerMessage(LogLevel.Debug, """Connection id "{connectionId}" accepted.""", EventName = "ConnectionAccepted")]
    internal static partial void LogConnectionAccepted(this ILogger logger, string connectionId);

    [LoggerMessage(LogLevel.Debug, """Connection id "{connectionId}" started.""", EventName = "ConnectionStart")]
    internal static partial void LogConnectionStart(this ILogger logger, string connectionId);

    [LoggerMessage(LogLevel.Debug, """Connection id "{connectionId}" closing.""", EventName = "ConnectionClosing")]
    internal static partial void LogConnectionClosing(this ILogger logger, string connectionId);

    [LoggerMessage(LogLevel.Debug, """Connection id "{connectionId}" stopped.""", EventName = "ConnectionStop")]
    internal static partial void LogConnectionStop(this ILogger logger, string connectionId);

    [LoggerMessage(LogLevel.Debug, """Connection id "{connectionId}" failed to close gracefully. Aborting...""", EventName = "ConnectionFailedToClose")]
    internal static partial void LogConnectionFailedToClose(this ILogger logger, string connectionId);
}