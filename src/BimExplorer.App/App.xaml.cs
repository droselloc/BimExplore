using System.IO;
using System.Windows;
using BimExplorer.App.Services;
using BimExplorer.App.ViewModels;
using BimExplorer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BimExplorer.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();

        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BimExplorer",
            "bimexplorer.db");

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        services.AddDbContext<BimDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        services.AddSingleton<ThumbnailService>();
        services.AddTransient<FileIndexerService>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<ScriptsViewModel>();
        services.AddTransient<SettingsViewModel>();

        Services = services.BuildServiceProvider();

        // Initialize database
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BimDbContext>();
        db.Database.EnsureCreated();
        MigrateAddThumbnailPath(db);
        MigrateCreateIndexedFolders(db);
        MigrateCreateScripts(db);
        db.EnsureFts5Table();
    }

    private static void MigrateAddThumbnailPath(BimDbContext db)
    {
        try
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE BimFiles ADD COLUMN ThumbnailPath TEXT NULL");
        }
        catch
        {
            // Column already exists — ignore
        }
    }

    private static void MigrateCreateIndexedFolders(BimDbContext db)
    {
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS IndexedFolders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Path TEXT NOT NULL,
                AddedAtUtc TEXT NOT NULL,
                LastIndexedAtUtc TEXT NULL
            );
            """);
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_IndexedFolders_Path ON IndexedFolders(Path);");
    }

    private static void MigrateCreateScripts(BimDbContext db)
    {
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS Scripts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Target TEXT NOT NULL DEFAULT 'Otro',
                Description TEXT NULL,
                Code TEXT NOT NULL DEFAULT '',
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            """);
    }
}
