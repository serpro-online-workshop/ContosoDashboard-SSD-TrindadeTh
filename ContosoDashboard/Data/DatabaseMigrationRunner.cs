using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Data;

public static class DatabaseMigrationRunner
{
    private static readonly string[] LegacyTables =
    [
        "Users",
        "Projects",
        "Tasks",
        "TaskComments",
        "Notifications",
        "ProjectMembers",
        "Announcements"
    ];

    private static readonly string[] DocumentTables = ["Documents", "DocumentTags"];

    public static async Task ApplyAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        var database = context.Database;
        var migrations = database.GetMigrations().ToArray();
        if (migrations.Length == 0)
        {
            return;
        }

        var appliedMigrations = (await database.GetAppliedMigrationsAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        if (appliedMigrations.Count == 0)
        {
            await database.OpenConnectionAsync(cancellationToken);
            var existingTables = await ReadTableNamesAsync(context, cancellationToken);
            var legacyTableCount = LegacyTables.Count(existingTables.Contains);
            var documentTableCount = DocumentTables.Count(existingTables.Contains);

            if (legacyTableCount > 0 && legacyTableCount != LegacyTables.Length)
            {
                throw new InvalidOperationException("The existing SQLite schema is incomplete; migrations were not applied.");
            }

            if (legacyTableCount == LegacyTables.Length)
            {
                if (documentTableCount != 0 && documentTableCount != DocumentTables.Length)
                {
                    throw new InvalidOperationException("The existing document schema is incomplete; migrations were not applied.");
                }

                await database.ExecuteSqlRawAsync(
                    "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL);",
                    cancellationToken);

                var migrationsToBaseline = documentTableCount == DocumentTables.Length ? migrations : migrations.Take(1);
                foreach (var migration in migrationsToBaseline)
                {
                    await database.ExecuteSqlInterpolatedAsync(
                        $"INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({migration}, {"8.0.0"});",
                        cancellationToken);
                }
            }
        }

        await database.MigrateAsync(cancellationToken);
    }

    private static async Task<HashSet<string>> ReadTableNamesAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";

        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }
}