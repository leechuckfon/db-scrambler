using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NReco.Logging.File;

IHostBuilder builder = new HostBuilder();

builder.ConfigureServices(x =>
{
    x.AddLogging(loggingBuilder =>
    {
        loggingBuilder.AddFile("ScrambleLog.log", configure =>
        {
            configure.Append = false;
            configure.MinLevel = LogLevel.Information;
            configure.FileSizeLimitBytes = 200000000;
            configure.MaxRollingFiles = 10;
        });
        loggingBuilder.AddConsole();
    });

    x.AddPooledDbContextFactory<DbContext>(config =>
      {
          config.UseSqlServer("<DatabaseConnectionString>");
      });

    x.AddHostedService<ScrambleService>();
});
await builder.Build().RunAsync();