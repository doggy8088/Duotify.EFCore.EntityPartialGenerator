using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace Duotify.EFCore.EntityPartialGenerator
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<App>();
                })
                .ConfigureAppConfiguration(delegate (HostBuilderContext hostingContext, IConfigurationBuilder config)
                {
                    config.Sources.Clear();
                    //IHostEnvironment hostingEnvironment = hostingContext.HostingEnvironment;
                    //bool value = hostingContext.Configuration.GetValue("hostBuilder:reloadConfigOnChange", defaultValue: true);
                    //config.AddJsonFile("appsettings.json", optional: true, value).AddJsonFile("appsettings." + hostingEnvironment.EnvironmentName + ".json", optional: true, value);
                    //if (hostingEnvironment.IsDevelopment() && !string.IsNullOrEmpty(hostingEnvironment.ApplicationName))
                    //{
                    //    Assembly assembly = Assembly.Load(new AssemblyName(hostingEnvironment.ApplicationName));
                    //    if (assembly != null)
                    //    {
                    //        config.AddUserSecrets(assembly, optional: true);
                    //    }
                    //}
                    //config.AddEnvironmentVariables();
                    if (args != null)
                    {
                        config.AddCommandLine(args);
                    }
                })
                .ConfigureLogging((hostContext, configLogging) =>
                {
                    //configLogging.ClearProviders();
                    //configLogging.AddConsole();
                    //configLogging.AddDebug();
                    configLogging.SetMinimumLevel(LogLevel.Warning);
                });
    }
}