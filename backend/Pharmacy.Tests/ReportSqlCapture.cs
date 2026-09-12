using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Pharmacy.Tests;

internal sealed class ReportSqlCapture : DbCommandInterceptor
{
    public string Sql { get; private set; } = "";
    public List<NpgsqlParameter> Parameters { get; private set; } = [];
    public List<(string Sql, List<NpgsqlParameter> Parameters)> Commands { get; } = [];
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Sql = command.CommandText;
        Parameters = command.Parameters.Cast<NpgsqlParameter>().Select(x => new NpgsqlParameter(x.ParameterName, x.NpgsqlDbType) { Value = x.Value }).ToList();
        Commands.Add((Sql, Parameters));
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
