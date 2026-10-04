using System.Data;

namespace ISRORCert.Tests;

/// <summary>
/// Builds a small but complete certification topology: one division, farm, shard and machine,
/// a Certification body plus GlobalManager / MachineManager / FarmManager / SR_ShardManager / AgentServer bodies.
/// </summary>
internal static class CertificationData
{
    public const string PublicIp = "10.0.0.5";
    public const string PrivateIp = "192.168.0.5";

    private static DataTable Table(params (string Name, Type Type)[] columns)
    {
        var table = new DataTable();
        foreach (var (name, type) in columns)
            table.Columns.Add(name, type);
        return table;
    }

    public static FakeDbAdapter CreateAdapter(bool includeGlobalManagerModule = true, int agentMachineId = 1, string privateIp = PrivateIp)
    {
        var adapter = new FakeDbAdapter();

        var content = Table(("id", typeof(byte)), ("name", typeof(string)));
        content.Rows.Add((byte)1, "SR");
        adapter.Tables["_GetContentList"] = content;

        var modules = Table(("id", typeof(byte)), ("name", typeof(string)));
        modules.Rows.Add((byte)1, "Certification");
        if (includeGlobalManagerModule)
            modules.Rows.Add((byte)2, "GlobalManager");
        modules.Rows.Add((byte)3, "MachineManager");
        modules.Rows.Add((byte)4, "FarmManager");
        modules.Rows.Add((byte)5, "SR_ShardManager");
        modules.Rows.Add((byte)6, "AgentServer");
        adapter.Tables["_GetModuleList"] = modules;

        var divisions = Table(("id", typeof(byte)), ("name", typeof(string)), ("db", typeof(string)));
        divisions.Rows.Add((byte)1, "Division", DBNull.Value);
        adapter.Tables["_GetDivisionList"] = divisions;

        var farms = Table(("id", typeof(byte)), ("division", typeof(byte)), ("name", typeof(string)), ("db", typeof(string)));
        farms.Rows.Add((byte)1, (byte)1, "Farm", DBNull.Value);
        adapter.Tables["_GetFarmList"] = farms;

        var farmContent = Table(("id", typeof(int)), ("farm", typeof(byte)), ("content", typeof(byte)));
        farmContent.Rows.Add(1, (byte)1, (byte)1);
        adapter.Tables["_GetFarmContentList"] = farmContent;

        var shards = Table(("id", typeof(short)), ("farm", typeof(byte)), ("content", typeof(byte)), ("name", typeof(string)),
            ("db", typeof(string)), ("logdb", typeof(string)), ("maxuser", typeof(short)));
        shards.Rows.Add((short)64, (byte)1, (byte)1, "Shard", DBNull.Value, DBNull.Value, (short)1000);
        adapter.Tables["_GetShardList"] = shards;

        var machines = Table(("id", typeof(int)), ("division", typeof(byte)), ("name", typeof(string)), ("public", typeof(string)), ("private", typeof(string)));
        machines.Rows.Add(1, (byte)1, "Machine", PublicIp, privateIp);
        adapter.Tables["_GetServerMachineList"] = machines;

        var bodies = Table(("id", typeof(short)), ("division", typeof(byte)), ("farm", typeof(byte)), ("shard", typeof(short)),
            ("machine", typeof(int)), ("module", typeof(byte)), ("moduleType", typeof(byte)), ("certifier", typeof(short)), ("port", typeof(short)));
        bodies.Rows.Add((short)1, (byte)1, DBNull.Value, DBNull.Value, 1, (byte)1, (byte)0, DBNull.Value, (short)32000); // Certification
        bodies.Rows.Add((short)2, (byte)1, DBNull.Value, DBNull.Value, 1, (byte)2, (byte)0, (short)1, (short)32001);      // GlobalManager
        bodies.Rows.Add((short)3, DBNull.Value, DBNull.Value, DBNull.Value, 1, (byte)3, (byte)0, (short)1, (short)32002); // MachineManager
        bodies.Rows.Add((short)4, DBNull.Value, (byte)1, DBNull.Value, 1, (byte)4, (byte)0, (short)1, (short)32003);     // FarmManager
        bodies.Rows.Add((short)5, DBNull.Value, DBNull.Value, (short)64, 1, (byte)5, (byte)0, (short)1, (short)32004);   // ShardManager
        bodies.Rows.Add((short)6, DBNull.Value, DBNull.Value, (short)64, agentMachineId, (byte)6, (byte)0, (short)1, (short)15884); // AgentServer
        adapter.Tables["_GetServerBodyList"] = bodies;

        adapter.Tables["_GetServerCordList"] = Table(("id", typeof(int)), ("outlet", typeof(short)), ("inlet", typeof(short)), ("bind", typeof(byte)));

        return adapter;
    }
}
