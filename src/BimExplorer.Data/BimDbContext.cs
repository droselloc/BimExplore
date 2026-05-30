using BimExplorer.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace BimExplorer.Data;

public class BimDbContext : DbContext
{
    public DbSet<BimFile> BimFiles => Set<BimFile>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<BimFileTag> BimFileTags => Set<BimFileTag>();
    public DbSet<IndexedFolder> IndexedFolders => Set<IndexedFolder>();
    public DbSet<Script> Scripts => Set<Script>();

    public BimDbContext(DbContextOptions<BimDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // BimFile
        modelBuilder.Entity<BimFile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(500);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.FileHash).HasMaxLength(128);
            entity.HasIndex(e => e.FilePath).IsUnique();
            entity.HasIndex(e => e.FileHash);
        });

        // Tag
        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // BimFileTag (many-to-many join table)
        modelBuilder.Entity<BimFileTag>(entity =>
        {
            entity.HasKey(e => new { e.BimFileId, e.TagId });

            entity.HasOne(e => e.BimFile)
                .WithMany(f => f.BimFileTags)
                .HasForeignKey(e => e.BimFileId);

            entity.HasOne(e => e.Tag)
                .WithMany(t => t.BimFileTags)
                .HasForeignKey(e => e.TagId);
        });

        modelBuilder.Entity<IndexedFolder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Path).IsRequired().HasMaxLength(2000);
            entity.HasIndex(e => e.Path).IsUnique();
        });

        modelBuilder.Entity<Script>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Target).HasMaxLength(50);
        });
    }

    /// <summary>
    /// Creates the FTS5 virtual table for full-text search over BimFiles.
    /// Call after EnsureCreated / Migrate.
    /// </summary>
    public void EnsureFts5Table()
    {
        Database.ExecuteSqlRaw("""
            CREATE VIRTUAL TABLE IF NOT EXISTS BimFiles_fts USING fts5(
                FileName,
                ProjectName,
                Discipline,
                AuthorName,
                ExtractedText,
                content='BimFiles',
                content_rowid='Id'
            );
            """);

        // Triggers to keep FTS index in sync with the main table
        Database.ExecuteSqlRaw("""
            CREATE TRIGGER IF NOT EXISTS BimFiles_ai AFTER INSERT ON BimFiles BEGIN
                INSERT INTO BimFiles_fts(rowid, FileName, ProjectName, Discipline, AuthorName, ExtractedText)
                VALUES (new.Id, new.FileName, new.ProjectName, new.Discipline, new.AuthorName, new.ExtractedText);
            END;
            """);

        Database.ExecuteSqlRaw("""
            CREATE TRIGGER IF NOT EXISTS BimFiles_ad AFTER DELETE ON BimFiles BEGIN
                INSERT INTO BimFiles_fts(BimFiles_fts, rowid, FileName, ProjectName, Discipline, AuthorName, ExtractedText)
                VALUES ('delete', old.Id, old.FileName, old.ProjectName, old.Discipline, old.AuthorName, old.ExtractedText);
            END;
            """);

        Database.ExecuteSqlRaw("""
            CREATE TRIGGER IF NOT EXISTS BimFiles_au AFTER UPDATE ON BimFiles BEGIN
                INSERT INTO BimFiles_fts(BimFiles_fts, rowid, FileName, ProjectName, Discipline, AuthorName, ExtractedText)
                VALUES ('delete', old.Id, old.FileName, old.ProjectName, old.Discipline, old.AuthorName, old.ExtractedText);
                INSERT INTO BimFiles_fts(rowid, FileName, ProjectName, Discipline, AuthorName, ExtractedText)
                VALUES (new.Id, new.FileName, new.ProjectName, new.Discipline, new.AuthorName, new.ExtractedText);
            END;
            """);
    }

    /// <summary>
    /// Full-text search over indexed BIM files using FTS5.
    /// </summary>
    public IQueryable<BimFile> SearchBimFiles(string query)
    {
        var sanitized = query.Replace("'", "''");
        return BimFiles.FromSqlRaw("""
            SELECT b.* FROM BimFiles b
            INNER JOIN BimFiles_fts fts ON b.Id = fts.rowid
            WHERE BimFiles_fts MATCH {0}
            ORDER BY rank
            """, sanitized);
    }
}
