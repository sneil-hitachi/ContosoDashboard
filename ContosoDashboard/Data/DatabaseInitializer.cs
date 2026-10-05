using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ContosoDashboard.Data;

public static class DatabaseInitializer
{
    public const string InitialSchemaMigrationId = "20261005155146_InitialSchema";
    private const string MigrationHistoryTable = "__EFMigrationsHistory";

    private static readonly IReadOnlyDictionary<string, string[]> LegacyColumns = new Dictionary<string, string[]>
    {
        ["Announcements"] = ["AnnouncementId", "Title", "Content", "CreatedByUserId", "PublishDate", "ExpiryDate", "IsActive"],
        ["Notifications"] = ["NotificationId", "UserId", "Title", "Message", "Type", "Priority", "IsRead", "CreatedDate"],
        ["ProjectMembers"] = ["ProjectMemberId", "ProjectId", "UserId", "Role", "AssignedDate"],
        ["Projects"] = ["ProjectId", "Name", "Description", "ProjectManagerId", "StartDate", "TargetCompletionDate", "Status", "CreatedDate", "UpdatedDate"],
        ["TaskComments"] = ["CommentId", "TaskId", "UserId", "CommentText", "CreatedDate"],
        ["Tasks"] = ["TaskId", "Title", "Description", "Priority", "Status", "DueDate", "AssignedUserId", "CreatedByUserId", "ProjectId", "CreatedDate", "UpdatedDate"],
        ["Users"] = ["UserId", "Email", "DisplayName", "Department", "JobTitle", "Role", "ProfilePhotoUrl", "AvailabilityStatus", "CreatedDate", "LastLoginDate", "PhoneNumber", "EmailNotificationsEnabled", "InAppNotificationsEnabled"]
    };

    public static async Task InitializeAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        var connection = context.Database.GetDbConnection();
        var databaseExists = await context.Database.CanConnectAsync(cancellationToken);
        if (databaseExists)
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                if (!await HasMigrationHistoryAsync(connection, cancellationToken) &&
                    await HasUserTablesAsync(connection, cancellationToken))
                {
                    await VerifyLegacySchemaAsync(connection, cancellationToken);
                    await AdoptBaselineAsync(connection, cancellationToken);
                }
            }
            finally
            {
                await connection.CloseAsync();
            }
        }

        await context.Database.MigrateAsync(cancellationToken);
    }

    private static async Task<bool> HasMigrationHistoryAsync(System.Data.Common.DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL THEN 0 ELSE 1 END";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    private static async Task<bool> HasUserTablesAsync(System.Data.Common.DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo') THEN 1 ELSE 0 END";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    private static async Task VerifyLegacySchemaAsync(System.Data.Common.DbConnection connection, CancellationToken cancellationToken)
    {
        var actual = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' ORDER BY TABLE_NAME, ORDINAL_POSITION";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var tableName = reader.GetString(0);
                if (!actual.TryGetValue(tableName, out var columns))
                {
                    columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    actual.Add(tableName, columns);
                }

                columns.Add(reader.GetString(1));
            }
        }

        if (actual.Count != LegacyColumns.Count || LegacyColumns.Any(expected =>
                !actual.TryGetValue(expected.Key, out var columns) ||
                !columns.SetEquals(expected.Value)))
        {
            throw new InvalidOperationException(
                "The existing database does not match the verified EnsureCreated baseline. Back it up and resolve the schema mismatch before applying migrations.");
        }
    }

    private static async Task AdoptBaselineAsync(System.Data.Common.DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE [dbo].[__EFMigrationsHistory] (
                [MigrationId] nvarchar(150) NOT NULL,
                [ProductVersion] nvarchar(32) NOT NULL,
                CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
            );
            INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
            VALUES (@migrationId, @productVersion);
            """;

        var migrationId = command.CreateParameter();
        migrationId.ParameterName = "@migrationId";
        migrationId.Value = InitialSchemaMigrationId;
        command.Parameters.Add(migrationId);

        var productVersion = command.CreateParameter();
        productVersion.ParameterName = "@productVersion";
        productVersion.Value = "10.0.12";
        command.Parameters.Add(productVersion);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}