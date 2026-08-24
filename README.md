### Manual

## Setup

### Models

Run command `dotnet ef dbcontext scaffold "<DatabaseConnectionString>" Microsoft.EntityFrameworkCore.SqlServer --output-dir Models`

This will create `Models` folder with all database models and a DbContext.

In created DbContext (e.g. TkMensTestDbContext) remove connectionstring (to support DbContextPooling)

### Program.cs

Replace `<DatabaseConnectionString>` with database connection string with permissions to read/write to database.

### Text Files

Create the following files for correct working:

- `colnamesscramble.txt`
    - `\n` separated column name list
- `colnamesempty.txt`
    - `\n` separated column name list
- `DisableTriggers.txt`
    - `\n` separated trigger name and table list in following format: `TRIGGERNAME|TABLENAME`
- `tablenamesempty.txt`
    - `\n` separated table name list

### How it works

Via reflection program will go across all DbSets and replace every supported database type with randomized letters/numbers.

- The columns that will be replaced are read from `colnamesscramble.txt`
- The columns that will be emptied are read from `colnamesempty.txt`
- The tables that will be empties are read from `tablenamesempty.txt`
- Triggers that will be disabled and reenabled after scramble are read from `tablenamesempty.txt`

Program will only replace a letter with another randomized letter and a number with a randomized number, special characters are kept to keep database consistency

### Performance

Performance considerations:

- Disable triggers that trigger on update by adding them in a `DisableTriggers.txt` file to prevent overhead on updates.

- Do not run in Debug mode as it might crash application due to high data volume, to do a full run use `command prompt` or `terminal` with `dotnet run`

### Improvements

- Remove `colnamesempty.txt` cause it doesn't actually do anything
- There are still commands that need to be ran manually because of time constraints like resetting/avoiding of scrambling the username of the user for testing
- At the moment every `DbSet` is given their own thread to scramble and save.
    - Biggest bottleneck is saving to database.
    - This results in every dataset being limited by the performance of 1 thread and performance overhead loss when all other datasets are done and 1 or 2 are still executing (threads are left unused to increase performance for these saves).
    - This is better than running them sequentially but a better solution would be to spread the thread over the batches that can be executed on the database.
    - If correctly done this would improve the scrambling by at all times utilizing all threads whenever possible and reducing update time on db level drastically.