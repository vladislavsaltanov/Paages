using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Paages.Infrastructure.Data;

public class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    private const string BusyTimeout = "PRAGMA busy_timeout = 5000;";
    private const string JournalMode = "PRAGMA journal_mode=WAL;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        Execute(connection, BusyTimeout);
        Execute(connection, JournalMode);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(connection, BusyTimeout, cancellationToken);
        await ExecuteAsync(connection, JournalMode, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private static void Execute(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static async Task ExecuteAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}