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
    "Version": "ISROR" // 👈 "ISROR" (ISROR 2015+, default) or "VSRO188"
  }
}
```

Any value can also be overridden with an environment variable (`CertificationConfig__DbConfig=...`) or a command line argument (`--CertificationConfig:DbConfig=...`), so you don't have to store passwords in the file.

> The connection is not encrypted unless your connection string sets `Encrypt` (the same behavior as older versions). Set `Encrypt=True` if your SQL Server has a trusted certificate.

## How it works
- The listening address and port come from the database: the `_ServerBody` whose module is `Certification`, on its machine's public IP and `ListenerPort`.
- Only connections from IPs registered in the `_ServerMachine` table (public or private IP) are accepted.
- If the database can't be loaded or the listener can't be started, the server logs the reason and exits with code `1` (instead of running without listening).

## Known limitations
- Relaying messages to a server body other than the Certification server itself (`0x6008` with another target) is not implemented yet. The connection that requests it gets dropped (and the reason is logged).
- Topology changes in the database require a restart.

## Development
```bash
dotnet build src/ISRORCert.sln
dotnet test src/ISRORCert.sln
```
