using System.Linq.Dynamic.Core;
using System.Reflection;
using LocalDbScramble.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class ScrambleService(IDbContextFactory<DbContext> contextFactory, ILogger<ScrambleService> logger, IHostApplicationLifetime applicationLifetime) : BackgroundService
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
        var typeSource = await contextFactory.CreateDbContextAsync();

        var members = typeSource.GetType().GetProperties();
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

                    switch (property.ColumnType.ToUpper())
                    {
                        case string _ when property.ColumnType.ToUpper().Contains("VARCHAR") || property.ColumnType.ToUpper().Contains("NCHAR"):
                            var stringCastedValue = valueInMemory as string;
                            if (string.IsNullOrWhiteSpace(stringCastedValue) || string.IsNullOrEmpty(stringCastedValue))
                            {
                                continue;
                            }
                            var stringScrambledValue = stringCastedValue?.Scramble();
                            logger.LogInformation($"Scrambled {property.ColumnName} value from [{valueInMemory}] to [{stringScrambledValue}]");
                            setAccessor?.Invoke(dbValue, [stringScrambledValue]);
                            break;
                        case string _ when property.ColumnType.ToUpper().Contains("INT"):
                            var intCastedValue = (int)valueInMemory;
                            var intScrambledValue = intCastedValue.Scramble();
                            logger.LogInformation($"Scrambled {property.ColumnName} value from [{valueInMemory}] to [{intScrambledValue}]");
                            setAccessor?.Invoke(dbValue, [intScrambledValue]);
                            break;
                        case string _ when property.ColumnType.ToUpper().Contains("MONEY"):
                            var decimalCastedValue = (decimal)valueInMemory;
                            var decimalScrambledValue = decimalCastedValue.Scramble();
                            logger.LogInformation($"Scrambled {property.ColumnName} value from [{valueInMemory}] to [{decimalScrambledValue}]");
                            setAccessor?.Invoke(dbValue, [decimalScrambledValue]);
                            break;
                        case string _ when property.ColumnType.ToUpper().Contains("FLOAT"):
                            var floatCastedValue = (double)valueInMemory;
                            var floatScrambledValue = floatCastedValue.Scramble();
                            logger.LogInformation($"Scrambled {property.ColumnName} value from [{valueInMemory}] to [{floatScrambledValue}]");
                            setAccessor?.Invoke(dbValue, [floatScrambledValue]);
                            break;
                        case string _ when property.ColumnType.ToUpper().Contains("DATETIME"):
                            var dateTimeCastedValue = (DateTime)valueInMemory;
                            var dateTimeScrambledValue = dateTimeCastedValue.Scramble();
                            logger.LogInformation($"Scrambled {property.ColumnName} value from [{dateTimeCastedValue.ToShortTimeString()}] to [{dateTimeScrambledValue.ToShortTimeString()}]");
                            setAccessor?.Invoke(dbValue, [dateTimeScrambledValue]);
                            break;
                        case string _ when property.ColumnType.ToUpper().Contains("VARBINARY"):
                            // Empty
                            setAccessor?.Invoke(dbValue, [Enumerable.Empty<byte>().ToArray()]);
                            break;
                        case string _ when property.ColumnType.ToUpper().Contains("BIT"):
                            var boolCastedValue = (bool)valueInMemory;
                            var boolScrambledValue = boolCastedValue.Scramble();
                            logger.LogInformation($"Scrambled {property.ColumnName} value from [{boolCastedValue}] to [{boolScrambledValue}]");
                            setAccessor?.Invoke(dbValue, [boolScrambledValue]);
                            break;
                        default:
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