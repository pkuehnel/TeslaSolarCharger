using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace TeslaSolarCharger.Model.EntityFramework;

public class SqliteBusyTimeoutInterceptor : DbConnectionInterceptor
{
    private readonly int? _busyTimeout;

    public SqliteBusyTimeoutInterceptor(int? busyTimeoutMs)
    {
        _busyTimeout = busyTimeoutMs;
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        SetBusyTimeout(connection);
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await SetBusyTimeoutAsync(connection);
    }

    private void SetBusyTimeout(DbConnection connection)
    {
        if (_busyTimeout == default)
        {
            return;
        }
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA busy_timeout={_busyTimeout};";
        command.ExecuteNonQuery();
    }

    private async Task SetBusyTimeoutAsync(DbConnection connection)
    {
        if (_busyTimeout == default)
        {
            return;
        }
        // ReSharper disable once UseAwaitUsing
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA busy_timeout={_busyTimeout};";
        await command.ExecuteNonQueryAsync();
    }
}
