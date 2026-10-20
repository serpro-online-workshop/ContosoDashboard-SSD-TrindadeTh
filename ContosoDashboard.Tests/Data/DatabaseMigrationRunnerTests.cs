using ContosoDashboard.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ContosoDashboard.Tests.Data;

public sealed class DatabaseMigrationRunnerTests
{
    [Fact]
    public async Task Apply_creates_the_legacy_and_document_schemas_for_a_fresh_database()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);

        await DatabaseMigrationRunner.ApplyAsync(context);

        Assert.Equal(2, (await context.Database.GetAppliedMigrationsAsync()).Count());
        Assert.Equal(4, await context.Users.CountAsync());
        Assert.Equal(0, await context.Documents.CountAsync());
    }

    [Fact]
    public async Task Apply_adopts_an_existing_EnsureCreated_schema_without_overwriting_records()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.MigrateAsync();
        var existingUser = await context.Users.SingleAsync(user => user.UserId == 4);
        existingUser.DisplayName = "Existing user data";
        await context.SaveChangesAsync();

        await context.Database.ExecuteSqlRawAsync("DROP TABLE \"DocumentTags\";");
        await context.Database.ExecuteSqlRawAsync("DROP TABLE \"Documents\";");
        await context.Database.ExecuteSqlRawAsync("DROP TABLE \"__EFMigrationsHistory\";");

        await DatabaseMigrationRunner.ApplyAsync(context);

        Assert.Equal("Existing user data", (await context.Users.SingleAsync(user => user.UserId == 4)).DisplayName);
        Assert.Equal(2, (await context.Database.GetAppliedMigrationsAsync()).Count());
        Assert.Equal(0, await context.Documents.CountAsync());
    }

    private static ApplicationDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        return new ApplicationDbContext(options);
    }
}