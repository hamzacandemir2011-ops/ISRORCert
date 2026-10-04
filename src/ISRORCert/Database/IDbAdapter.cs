using System.Data;
using System.Data.Common;

namespace ISRORCert.Database
{
    internal interface IDbAdapter
    {
        string ConnectionString { get; set; }

        int Execute(string cmdText, params DbParameter[] parameters);
        /// <summary>
        /// Like <see cref="Execute"/>, but reports whether the command ran without error.
        /// (Execute's -1 is ambiguous: procedures using SET NOCOUNT ON also return -1 on success.)
        /// </summary>
        bool TryExecute(string cmdText, params DbParameter[] parameters);
        Task<int> ExecuteAsync(string cmdText, CancellationToken cancellationToken = default, params DbParameter[] parameters);
        T? GetData<T>(string cmdText, params DbParameter[] parameters) where T : class, IDbEntity, new();
        Task<T?> GetDataAsync<T>(string cmdText, CancellationToken cancellationToken = default, params DbParameter[] parameters) where T : class, IDbEntity, new();
        bool GetDataTable<T>(ICollection<T> collection, string cmdText, params DbParameter[] parameters) where T : class, IDbEntity, new();
        IEnumerable<T>? GetDataTable<T>(string cmdText, params DbParameter[] parameters) where T : class, IDbEntity, new();
        Task<bool> GetDataTableAsync<T>(ICollection<T> collection, string cmdText, CancellationToken cancellationToken = default, params DbParameter[] parameters) where T : class, IDbEntity, new();
        Task<IList<T>?> GetDataTableAsync<T>(string cmdText, CancellationToken cancellationToken = default, params DbParameter[] parameters) where T : class, IDbEntity, new();
        DbParameter GetInputOutputParameter(string paramName, object? inputValue, DbType inputOutputType);
        DbParameter GetInputParameter(string name, object? inputValue);
        DbParameter GetOutputParameter(string paramName, DbType outputType);
        DbParameter GetReturnParameter(DbType type);
        T? GetScalar<T>(string cmdText, params DbParameter[] parameters) where T : unmanaged;
        Task<T?> GetScalarAsync<T>(string cmdText, CancellationToken cancellationToken = default, params DbParameter[] parameters) where T : unmanaged;
    }
}