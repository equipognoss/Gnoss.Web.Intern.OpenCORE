using Es.Riam.Gnoss.Util.General;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;

namespace Gnoss.Web.Intern
{
    public class Program
    {
        public static void Main(string[] args)
        {
            LoggingService.ConfigurarBasicStartupSerilog().CreateBootstrapLogger();
            try
            {
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Error fatal durante el arranque");
            }
            finally
            {
                Log.CloseAndFlush(); // asegura que se escriben todos los logs pendientes
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService() //Windows
                .UseSystemd() //Linux
                .ConfigureAppConfiguration((hostContext, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    config.AddJsonFile($"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                })
                .UseSerilog((context, services, configuration) => LoggingService.ConfigurarSerilog(context.Configuration, services, configuration))
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1000000000); // Maximo tamaño de subida ~ 1Gb
                    AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
                });
    }
}
