using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

using System;
using System.Linq;

namespace ISRORCert.Database
{
    internal class SqlDbAdapter : DbAdapter
    {
        private string _connectionString = string.Empty;

        /// <summary>
        /// Microsoft.Data.SqlClient encrypts connections by default, which fails against the usual local SQL Server
        /// without a trusted certificate. Unless the connection string says otherwise, keep the old
        /// System.Data.SqlClient behavior (no encryption required).
        /// </summary>
        public override string ConnectionString
        {
            get => _connectionString;
            set => _connectionString = NormalizeConnectionString(value);
        }

        public static string NormalizeConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return connectionString;

            var builder = new SqlConnectionStringBuilder(connectionString);
            if (!connectionString.Contains("Encrypt", StringComparison.OrdinalIgnoreCase))
                builder.Encrypt = SqlConnectionEncryptOption.Optional;

            return builder.ConnectionString;
        }

        public SqlDbAdapter(ILogger<SqlDbAdapter> logger) : base(logger, SqlClientFactory.Instance)
        {
        }

        public SqlDbAdapter(ILogger<SqlDbAdapter> logger, string connectionString) : base(logger, SqlClientFactory.Instance)
        {
            ConnectionString = connectionString;
        }
    }
}
