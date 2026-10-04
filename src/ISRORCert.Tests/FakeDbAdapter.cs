using System.Data;
using System.Data.Common;
using ISRORCert.Database;
using Microsoft.Data.SqlClient;

namespace ISRORCert.Tests;

/// <summary>
/// In-memory IDbAdapter: each stored procedure name maps to a DataTable that is read through the real entity Read() methods.
/// </summary>
internal class FakeDbAdapter : IDbAdapter
{
    public Dictionary<string, DataTable> Tables { get; } = new();
    public HashSet<string> FailingProcedures { get; } = new();
    public List<string> ExecutedProcedures { get; } = new();

    public string ConnectionString { get; set; } = "";

    public bool TryExecute(string cmdText, params DbParameter[] parameters)
    {
        ExecutedProcedures.Add(cmdText);
        return !FailingProcedures.Contains(cmdText);
    }

    public Task<bool> TryExecuteAsync(string cmdText, CancellationToken cancellationToken = default, params DbParameter[] parameters)
    {
        lock (ExecutedProcedures)
            return Task.FromResult(TryExecute(cmdText, parameters));
    }

    public int Execute(string cmdText, params DbParameter[] parameters) => TryExecute(cmdText, parameters) ? 0 : -1;

    public async Task<bool> GetDataTableAsync<T>(ICollection<T> collection, string cmdText, CancellationToken cancellationToken = default, params DbParameter[] parameters)
        where T : class, IDbEntity, new()
    {
        if (FailingProcedures.Contains(cmdText) || !Tables.TryGetValue(cmdText, out var table))
            return false;

        using var reader = table.CreateDataReader();
        await DbHelper.ReadDataTableAsync(reader, collection, cancellationToken);
        return true;
    }

    public DbParameter GetInputParameter(string name, object? inputValue) => new SqlParameter(name, inputValue);

    public Task<int> ExecuteAsync(string cmdText, CancellationToken cancellationToken = default, params DbParameter[] parameters) => throw new NotSupportedException();
    public T? GetData<T>(string cmdText, params DbParameter[] parameters) where T : class, IDbEntity, new() => throw new NotSupportedException();
    public Task<T?> GetDataAsync<T>(string cmdText, CancellationToken cancellationToken = default, params DbParameter[] parameters) where T : class, IDbEntity, new() => throw new NotSupportedException();
    public bool GetDataTable<T>(ICollection<T> collection, string cmdText, params DbParameter[] parameters) where T : class, IDbEntity, new() => throw new NotSupportedException();
    public IEnumerable<T>? GetDataTable<T>(string cmdText, params DbParameter[] parameters) where T : class, IDbEntity, new() => throw new NotSupportedException();
    public Task<IList<T>?> GetDataTableAsync<T>(string cmdText, CancellationToken cancellationToken = default, params DbParameter[] parameters) where T : class, IDbEntity, new() => throw new NotSupportedException();
    public DbParameter GetInputOutputParameter(string paramName, object? inputValue, DbType inputOutputType) => throw new NotSupportedException();
    public DbParameter GetOutputParameter(string paramName, DbType outputType) => throw new NotSupportedException();
    public DbParameter GetReturnParameter(DbType type) => throw new NotSupportedException();
    public T? GetScalar<T>(string cmdText, params DbParameter[] parameters) where T : unmanaged => throw new NotSupportedException();
    public Task<T?> GetScalarAsync<T>(string cmdText, CancellationToken cancellationToken = default, params DbParameter[] parameters) where T : unmanaged => throw new NotSupportedException();
}
