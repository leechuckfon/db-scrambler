using System.Linq.Dynamic.Core;
using System.Reflection;
using System.Runtime.CompilerServices;
using LocalDbScramble.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class ScrambleService(IDbContextFactory<TkmensTestContext> contextFactory, ILogger<ScrambleService> logger, IHostApplicationLifetime applicationLifetime) : BackgroundService
{
    private string[] ColumnsToScramble = [];
    private string[] ColumnsToEmpty = [];
    private string[] TablesToEmpty = [];
    private IEnumerable<string> DetectionColumns => ColumnsToScramble.Concat(ColumnsToEmpty).Where(x => !string.IsNullOrEmpty(x));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ColumnsToScramble = await File.ReadAllLinesAsync("colnamesscramble.txt", stoppingToken);
        ColumnsToEmpty = await File.ReadAllLinesAsync("colnamesempty.txt", stoppingToken);
        TablesToEmpty = await File.ReadAllLinesAsync("tablenamesempty.txt", stoppingToken);
        var triggersDisableScript = await File.ReadAllLinesAsync("DisableTriggers.txt", stoppingToken);

        var members = typeof(TkmensTestContext).GetProperties();
        var genericMethod = typeof(ScrambleService).GetMethod(nameof(ScrambleTable));

        List<Task> parallelTasks = new List<Task>();
        
        // Disable triggers for update performance
        using (var context = await contextFactory.CreateDbContextAsync())
        {
            foreach (var line in triggersDisableScript)
            {
                var split = line.Split("|");
                await context.Database.ExecuteSqlRawAsync($"DISABLE TRIGGER {split[0]} ON {split[1]}");
            }
        }

        // Prep scramble tasks
        foreach (var member in members.Where(m => m.PropertyType.Name == typeof(DbSet<>).Name))
        {
            var actualType = member.PropertyType.GetGenericArguments();
            logger.LogInformation($"Parsing Type: {actualType[0].Name}");
            var typedMethod = genericMethod.MakeGenericMethod(actualType[0]);
            parallelTasks.Add((Task<int>)typedMethod.Invoke(this, [member, stoppingToken]));
        }

        // Execute scramble per table in parallel
        await Task.WhenAll(parallelTasks);

        // Reenable triggers
        using (var context = await contextFactory.CreateDbContextAsync())
        {
            foreach (var line in triggersDisableScript)
            {
                var split = line.Split("|");
                await context.Database.ExecuteSqlRawAsync($"ENABLE TRIGGER {split[0]} ON {split[1]}");
            }
        }

        using (var context = await contextFactory.CreateDbContextAsync())
        {
            foreach (var table in TablesToEmpty)
            {
                await context.Database.ExecuteSqlRawAsync($"DELETE FROM {table}");
            }
        }

        logger.LogInformation($"🎉🥳🎉 SUCCESSFULLY SCRAMBLED DATABASE 🎉🥳🎉");
        applicationLifetime.StopApplication();
    }

    public async Task<int> ScrambleTable<T>(PropertyInfo? member, CancellationToken cancellationToken) where T : class
    {
        using var context = await contextFactory.CreateDbContextAsync();
        var dbSet = member!.GetValue(context) as DbSet<T>;
        try
        {
            await context.Database.BeginTransactionAsync(cancellationToken);
            logger.LogInformation("BEGIN TRANSACTION");
            var tableObjectType = dbSet!.GetType().GenericTypeArguments[0];
            var modelProperties = tableObjectType.GetProperties();
            var modelEntityType = context.Model
            .FindEntityType(tableObjectType);

            if (modelEntityType?.GetViewName() is not null)
            {
                // We can't/don't update views
                return 0;
            }

            var modelPropertiesWithDbInfo = modelEntityType?.GetProperties()
            .Where(x => !x.GetType().IsNested).Select(x => new
            {
                ColumnName = x.GetColumnName(),
                ColumnType = x.GetColumnType(),
                PropertyInfo = x.PropertyInfo
            }).Where(x => DetectionColumns.Any(y => string.Equals(y, x.ColumnName, StringComparison.InvariantCultureIgnoreCase)));

            if (modelPropertiesWithDbInfo?.Count() == 0 || modelPropertiesWithDbInfo is null) return 0;

            var dbValues = await dbSet.ToListAsync(cancellationToken);

            foreach (var property in modelPropertiesWithDbInfo)
            {
                logger.LogInformation($"Scrambling {property.ColumnName} with type {property.ColumnType} with mapping to {property.PropertyInfo?.Name}");

                var getAccessor = property.PropertyInfo?.GetGetMethod();
                var setAccessor = property.PropertyInfo?.GetSetMethod();

                foreach (var dbValue in dbValues)
                {
                    var valueInMemory = getAccessor?.Invoke(dbValue, null);
                    if (valueInMemory is null)
                    {
                        continue;
                    }

                    if (property.ColumnType.ToUpper().Contains("VARCHAR") || property.ColumnType.ToUpper().Contains("NCHAR"))
                    {
                        var castedValue = valueInMemory as string;
                        if (string.IsNullOrWhiteSpace(castedValue) || string.IsNullOrEmpty(castedValue))
                        {
                            continue;
                        }
                        var scrambledValue = castedValue?.Scramble();
                        logger.LogInformation($"Scrambled {property.ColumnName} value from [{valueInMemory}] to [{scrambledValue}]");
                        setAccessor?.Invoke(dbValue, [scrambledValue]);
                    }
                    else
                        if (property.ColumnType.ToUpper().Contains("INT"))
                        {
                            var castedValue = (int)valueInMemory;
                            var scrambledValue = castedValue.Scramble();
                            logger.LogInformation($"Scrambled {property.ColumnName} value from [{valueInMemory}] to [{scrambledValue}]");
                            setAccessor?.Invoke(dbValue, [scrambledValue]);
                        }
                        else
                            if (property.ColumnType.ToUpper().Contains("MONEY"))
                            {
                                var castedValue = (decimal)valueInMemory;
                                var scrambledValue = castedValue.Scramble();
                                logger.LogInformation($"Scrambled {property.ColumnName} value from [{valueInMemory}] to [{scrambledValue}]");
                                setAccessor?.Invoke(dbValue, [scrambledValue]);
                            }
                            else
                                if (property.ColumnType.ToUpper().Contains("DATETIME"))
                                {
                                    var castedValue = (DateTime)valueInMemory;
                                    var scrambledValue = castedValue.Scramble();
                                    logger.LogInformation($"Scrambled {property.ColumnName} value from [{castedValue.ToShortTimeString()}] to [{scrambledValue.ToShortTimeString()}]");
                                    setAccessor?.Invoke(dbValue, [scrambledValue]);
                                }
                                else
                                    if (property.ColumnType.ToUpper().Contains("VARBINARY"))
                                    {
                                        // Empty
                                        setAccessor?.Invoke(dbValue, [Enumerable.Empty<byte>().ToArray()]);
                                    }
                                else
                                    if (property.ColumnType.ToUpper().Contains("BIT"))
                                    {
                                        var castedValue = (bool)valueInMemory;
                                        var scrambledValue = castedValue.Scramble();
                                        logger.LogInformation($"Scrambled {property.ColumnName} value from [{castedValue}] to [{scrambledValue}]");
                                        setAccessor?.Invoke(dbValue, [scrambledValue]);
                                    }
                                    else
                                    {
                                        logger.LogError($"{property.ColumnType} is not supported");
                                        return 1;
                                    }

                }
            }
            await context.Database.CommitTransactionAsync(cancellationToken);
            logger.LogInformation("COMMIT TRANSACTION");
            await context.SaveChangesAsync(cancellationToken);
            return 0;
        }
        catch (Exception e)
        {
            logger.LogError(e.Message, e);
        }
        finally
        {
            if (context.Database.CurrentTransaction is not null)
            {
                await context.Database.RollbackTransactionAsync();
                logger.LogInformation("ROLLBACK TRANSACTION");
            }
        }
        return 1;
    }
}