using DbUp;
using DbUp.Engine;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Exemplar.API.Infrastructure;

public static class DbMigrationsExtensions
{
    /// <summary>
    /// Runs pending DbUp migrations against PostgreSQL on startup.
    /// Migrations are embedded SQL scripts in the Exemplar.API assembly
    /// stored under the "Migrations" folder.
    ///
    /// This method is idempotent: DbUp tracks applied scripts in a
    /// "schemaversions" journal table and will not re-run them.
    /// </summary>
    public static WebApplication RunDbMigrations(this WebApplication app)
    {
        var connectionString = app.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is required to run database migrations.");

        var logger = app.Services.GetRequiredService<ILogger<DatabaseMigrationMarker>>();

        EnsureDatabase.For.PostgresqlDatabase(connectionString);

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                typeof(DbMigrationsExtensions).Assembly,
                script => script.Contains(".Migrations."))
            .WithTransaction()
            .LogTo(new DbUpMicrosoftLogger(logger))
            .Build();

        if (!upgrader.IsUpgradeRequired())
        {
            logger.LogInformation("Database is up-to-date; no migrations to run.");
            return app;
        }

        DatabaseUpgradeResult result = upgrader.PerformUpgrade();

        if (!result.Successful)
        {
            logger.LogError(result.Error, "Database migration failed: {Error}", result.Error.Message);
            throw new InvalidOperationException(
                "Database migration failed. See logs for details.", result.Error);
        }

        logger.LogInformation(
            "Database migrations applied successfully. Scripts run: {Scripts}",
            string.Join(", ", result.Scripts.Select(s => s.Name)));

        return app;
    }

    // Marker class used only for ILogger<T> resolution.
    private sealed class DatabaseMigrationMarker;

    /// <summary>Bridges DbUp's logging to Microsoft.Extensions.Logging.</summary>
    private sealed class DbUpMicrosoftLogger : DbUp.Engine.Output.IUpgradeLog
    {
        private readonly ILogger _logger;

        public DbUpMicrosoftLogger(ILogger logger) => _logger = logger;

        public void LogTrace(string format, params object[] args)
            => _logger.LogTrace(format, args);

        public void LogDebug(string format, params object[] args)
            => _logger.LogDebug(format, args);

        public void LogInformation(string format, params object[] args)
            => _logger.LogInformation(format, args);

        public void LogWarning(string format, params object[] args)
            => _logger.LogWarning(format, args);

        public void LogError(string format, params object[] args)
            => _logger.LogError(format, args);

        public void LogError(Exception ex, string format, params object[] args)
            => _logger.LogError(ex, format, args);
    }
}
