# ISRORCert

A replacement Certification server for ISROR (and VSRO 1.188) server files.

When it starts, it loads the server topology (divisions, farms, shards, machines, server bodies and cords) from the `SILKROAD_CERTIFICATION` database. It then listens for the other server modules (GlobalManager, MachineManager, FarmManager, ShardManager, GatewayServer, AgentServer, GameServer...), certifies them and sends them their configuration.

## Requirements
- [.NET 10 runtime](https://dotnet.microsoft.com/download) (not needed if you use the self-contained release builds)
- SQL Server with the `SILKROAD_CERTIFICATION` database

## Setup
1. **Database:** run [`Database/SILKROAD_CERTIFICATION.sql`](/Database/SILKROAD_CERTIFICATION.sql) on your `SILKROAD_CERTIFICATION` database. It creates the tables (`_ServerBody`, `_ServerMachine`, `_Shard`, ...) and the stored procedures the server uses (`_GetServerBodyList`, `_UpdateShardName`, ...). Then fill the tables with your own topology. The script drops existing objects first. On an empty database, the `DROP` lines will show errors you can ignore.
2. **Patches:** the [`Patches`](/Patches) folder contains binary patches for the server modules, in x64dbg `.1337` format (`>module.exe` followed by `offset:old->new` lines). Apply each one to the matching executable, for example through x64dbg's *Patches* dialog (`Ctrl+P` → *Import*). **Back up your original executables first.**
3. **Configuration:** edit `appsettings.json` next to the executable:

```json
{
  "CertificationConfig":
  {
    "DbConfig": "Data Source=10.0.0.2;Initial Catalog=SILKROAD_CERTIFICATION;User ID=sa;Password=1", // 👈 Your certification DB
    "Version": "ISROR", // 👈 "ISROR" (ISROR 2015+, default) or "VSRO188"
    "StatusIntervalSeconds": 300, // 👈 How often a status summary is logged (0 = off)
    "ConsoleCommands": true       // 👈 Accept commands typed in the console, see below
  },
  "Logging": {
    "File": {
      "Path": "CertLog.txt",           // 👈 Logs are written here as well as to the console (remove "File" to disable)
      "FileSizeLimitBytes": 10485760,  // 👈 10 MB per file...
      "MaxRollingFiles": 5             // 👈 ...keeping the last 5 files
    }
  }
}
```

Any value can also be overridden with an environment variable (`CertificationConfig__DbConfig=...`) or a command line argument (`--CertificationConfig:DbConfig=...`), so you don't have to store passwords in the file.

> The connection is not encrypted unless your connection string sets `Encrypt` (the same behavior as older versions). Set `Encrypt=True` if your SQL Server has a trusted certificate.

## How it works
- The listening address and port come from the database: the `_ServerBody` whose module is `Certification`, on its machine's public IP and `ListenerPort`.
- Only connections from IPs registered in the `_ServerMachine` table (public or private IP) are accepted.
- If the database can't be loaded or the listener can't be started, the server logs the reason and exits with code `1` (instead of running without listening).
- Shard name / max user changes coming from the GlobalManager are saved to the database in the background, so a slow database doesn't block the connection. The change is only applied (and acknowledged) if the database update succeeded.

## Status summary
Every `StatusIntervalSeconds` the server logs which modules are connected and the state of every server body, e.g.:

```
Status: 2 connection(s), 6 server bodies, 3 cords
  connection 10.0.0.5:51234 (ServerBody#6 - AgentServer), connected 2h 5m ago
  connection 10.0.0.6:51240 (not certified yet), connected 0m 30s ago
  bodies by state: Blind=1, Loading=1, ServiceRunning=4
  not running: ServerBody#6 - AgentServer is 'Loading'
  cords by state: Stable=3
```

## Console commands
While the server is running you can type these commands in its console:

| Command | What it does |
|---|---|
| `help` | Lists the commands |
| `status` | Prints the status summary right away |
| `sessions` | Lists the connected modules: session id, address, certified server body and its state |
| `reload` | Reloads the topology from the database without restarting |
| `kick <target>` | Disconnects connections by IP (`10.0.0.5`), server body ID (`6`) or session id prefix (from `sessions`, at least 4 characters) |

If the server has no console input (e.g. running as a Windows service or in a container without stdin), the commands are simply not available. Set `ConsoleCommands` to `false` to turn them off.

### Reloading the topology
After changing the `SILKROAD_CERTIFICATION` tables (adding a shard, a machine, a server body...), type `reload` instead of restarting:
- The new data replaces the old one in one step, so a module that connects during the reload gets either the old or the new topology, never a mix.
- Connected modules stay connected. The state they reported (e.g. `ServiceRunning`) is kept for every server body and cord that still exists.
- If the new data can't be loaded or is invalid (database down, no Certification module/body, duplicate IDs), the current data is kept and the reason is logged.
- Modules that were already certified keep the configuration they received at that time. Restart a module to give it the new topology.

## Known limitations
- Relaying messages to a server body other than the Certification server itself (`0x6008` with another target) is not implemented yet. The connection that requests it gets dropped (and the reason is logged).
- Changing the Certification server's own address or port needs a restart (the listener is already bound). `reload` logs a warning when it detects that.

## Development
```bash
dotnet build src/ISRORCert.sln
dotnet test src/ISRORCert.sln
```
