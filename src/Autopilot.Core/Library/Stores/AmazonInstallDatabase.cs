using Autopilot.Core.Platform;
using Microsoft.Data.Sqlite;

namespace Autopilot.Core.Library.Stores;

/// <summary>The Amazon Games app's own list of installed games, <c>GameInstallInfo.sqlite</c>, opened read-only.</summary>
internal static class AmazonInstallDatabase
{
    /// <summary>
    /// The installed rows as (title, folder); null when the database is missing, locked, corrupt or of
    /// another shape, so the caller can fall back to searching folders.
    /// </summary>
    public static async Task<List<(string? Title, string Folder)>?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        if (!DrivePaths.IsFile(path))
        {
            return null;
        }

        SqliteConnectionStringBuilder connection = new() { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        try
        {
            await using SqliteConnection database = new(connection.ToString());
            await database.OpenAsync(cancellationToken);
            await using SqliteCommand query = database.CreateCommand();
            query.CommandText = "SELECT ProductTitle, InstallDirectory FROM DbSet WHERE Installed = 1";
            await using SqliteDataReader rows = await query.ExecuteReaderAsync(cancellationToken);
            List<(string?, string)> games = [];
            while (await rows.ReadAsync(cancellationToken))
            {
                if (rows.GetValue(1) is string folder)
                {
                    games.Add((rows.GetValue(0) as string, folder));
                }
            }

            return games;
        }
        catch (SqliteException)
        {
            return null;
        }
    }
}
