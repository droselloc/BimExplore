# BIM Explore

Desktop application for indexing, searching, and exploring BIM (Building Information Modeling) files.

## Project Structure

```
BimExplorer.sln
src/
  BimExplorer.Data/              # Data layer - EF Core models, DbContext, SQLite
    Models/
      BimFile.cs                 # Core entity: indexed BIM file with metadata
      Tag.cs                     # Classification tags
      BimFileTag.cs              # Many-to-many join table
    BimDbContext.cs              # EF Core context with FTS5 full-text search
  BimExplorer.App/               # WPF Application - UI + ViewModels
    App.xaml / App.xaml.cs       # Entry point, DI container, DB init
    ViewModels/
      MainViewModel.cs           # Search, list, index directory
      BimFileViewModel.cs        # Wrapper for BimFile binding
      RelayCommand.cs            # ICommand implementation
    Views/
      MainWindow.xaml            # 3-column layout: file list + 3D preview + detail
      Viewer3DControl.xaml       # IFC 3D viewer with orbit/pan/zoom
    Services/
      FileIndexerService.cs      # Scans directories, indexes BIM files
      ThumbnailService.cs        # Generates thumbnails from IFC via xBIM
    Converters/
      FileSizeConverter.cs       # Bytes → human-readable size
      NullToCollapsedConverter.cs # Null → Collapsed visibility
```

## Tech Stack

- .NET 9, C# 13
- WPF with MVVM pattern
- SQLite via EF Core (Microsoft.EntityFrameworkCore.Sqlite)
- FTS5 virtual table for full-text search
- CommunityToolkit.Mvvm
- Microsoft.Extensions.DependencyInjection
- Xbim.Essentials + Xbim.Geometry + Xbim.WindowsUI for IFC 3D viewing & thumbnails

## Build & Run

```bash
dotnet build
dotnet run --project src/BimExplorer.App
```

## Conventions

- File-scoped namespaces
- `required` keyword for mandatory string properties
- Collection navigation properties initialized with `[]`
- Nullable reference types enabled
- UTC timestamps for all date fields (suffix: `Utc`)

## Database

- SQLite with FTS5 content-sync table (`BimFiles_fts`)
- Call `BimDbContext.EnsureFts5Table()` after migration/creation to set up FTS5 + triggers
- FTS5 indexes: FileName, ProjectName, Discipline, AuthorName, ExtractedText
