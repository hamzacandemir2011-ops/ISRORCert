using ISRORCert.Database;
using Microsoft.Data.SqlClient;

namespace ISRORCert.Tests;

public class SqlDbAdapterTests
{
    [Fact]
    public void NormalizeConnectionString_DefaultsToOptionalEncryption()
    {
        var result = new SqlConnectionStringBuilder(
            SqlDbAdapter.NormalizeConnectionString("Data Source=.;Initial Catalog=SILKROAD_CERTIFICATION;User ID=sa;Password=1"));

        Assert.Equal(SqlConnectionEncryptOption.Optional, result.Encrypt);
        Assert.Equal("SILKROAD_CERTIFICATION", result.InitialCatalog);
    }

    [Fact]
    public void NormalizeConnectionString_KeepsExplicitEncryption()
    {
        var result = new SqlConnectionStringBuilder(
            SqlDbAdapter.NormalizeConnectionString("Data Source=.;Encrypt=True;User ID=sa;Password=1"));

        Assert.Equal(SqlConnectionEncryptOption.Mandatory, result.Encrypt);
    }
}
