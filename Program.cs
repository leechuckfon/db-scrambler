using LocalDbScramble.Models;
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
          config.UseSqlServer("Data Source=localhost\\SQLEXPRESS;Initial Catalog=OpenLane;Integrated Security=True;Pooling=False;Connect Timeout=30;Encrypt=True;Trust Server Certificate=True;Application Name=vscode-mssql;Application Intent=ReadWrite;Command Timeout=30");
      });

    x.AddHostedService<ScrambleService>();
});
await builder.Build().RunAsync();