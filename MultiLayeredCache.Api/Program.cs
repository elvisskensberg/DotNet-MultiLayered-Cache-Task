namespace MultiLayeredCache.Api;

/// <summary>
/// Application entry point
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                var env = context.HostingEnvironment;

                // Clear default configuration sources
                config.Sources.Clear();

                // Add configuration from appsettings folder
                config
                    .SetBasePath(env.ContentRootPath)
                    .AddJsonFile("appsettings/appsettings.json", optional: false, reloadOnChange: true)
                    .AddJsonFile($"appsettings/appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .AddCommandLine(args);
            })
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });
}
