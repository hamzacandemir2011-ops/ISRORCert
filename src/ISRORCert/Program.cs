using ISRORCert;
using ISRORCert.Database;
using ISRORCert.Logic;
using ISRORCert.Logic.Handler;
using ISRORCert.Model;
using ISRORCert.Model.Serialization;
using ISRORCert.Network;
using ISRORCert.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = new HostBuilder()
   .ConfigureAppConfiguration((hostingContext, config) =>
   {
       config.AddJsonFile("appsettings.json", optional: true);
       config.AddEnvironmentVariables();

       if (args != null)
       {
           config.AddCommandLine(args);
       }
   })
   .ConfigureServices((hostContext, services) =>
   {
       services.AddOptions();
       services.Configure<CertificationConfig>(hostContext.Configuration.GetSection("CertificationConfig"));

       services.AddSingleton<IDbAdapter, SqlDbAdapter>();

       // Registering both made DI always resolve the last one, so the serializer is picked from the config instead.
       var version = hostContext.Configuration.GetValue<string>("CertificationConfig:Version") ?? "ISROR";
       if (string.Equals(version, "VSRO188", StringComparison.OrdinalIgnoreCase))
           services.AddSingleton<ICertificationSerializer, CertificationSerializerOld>(); // VSRO188
       else
           services.AddSingleton<ICertificationSerializer, CertificationSerializerNew>(); // ISROR2015+

       services.AddSingleton<AsyncServer>();
       services.AddSingleton<IAsyncInterface, CertificationInterface>();
       services.AddSingleton<CertificationManager>();

       services.AddSingleton<PacketHandlerManager>();
       services.AddSingleton<IPacketHandler, PacketHandlerSetupCord>();
       services.AddSingleton<IPacketHandler, PacketHandlerCertificate>();
       services.AddSingleton<IPacketHandler, PacketHandlerNotify>();
       services.AddSingleton<IPacketHandler, PacketHandlerRelay>();
       services.AddSingleton<IPacketHandler, PacketHandlerChangeShardData>();

       services.AddHostedService<CertificationService>();
       services.AddHostedService<AsyncServerTickService>();
   })
   .ConfigureLogging((hostingContext, logging) =>
   {
       logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
       logging.AddConsole();
   });

await builder.RunConsoleAsync();


