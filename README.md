# BIM Explore

A Windows desktop application to index, search and explore BIM (Building Information Modeling) files — IFC, Revit, DWG/DXF and more.

If you work with hundreds of BIM files scattered across folders, BIM Explore turns them into a fast, searchable catalog with previews and a built-in 3D viewer.

---

## Features

- **Folder indexing** — point it at a directory and it walks the tree, extracting metadata (project name, discipline, author, etc.) from every supported file.
- **Full-text search** — instant results across file name, project, discipline, author and extracted text, powered by SQLite FTS5.
- **Built-in 3D viewer** — open IFC files directly in the app. Orbit, pan and zoom with the mouse.
- **Automatic thumbnails** — previews are generated from the geometry so you can scan results visually.
- **Tagging** — classify files with custom tags for project-specific workflows.
- **Self-contained** — single executable, no installer, no .NET runtime needed.

---

## Download & run

1. Go to the [Releases page](../../releases).
2. Download `BimExplore-win-x64.zip` from the latest release.
3. Extract it anywhere (Desktop, Documents, a USB stick — wherever).
4. Double-click `BimExplore.exe`.

That's it. The executable is fully self-contained, so **you don't need to install .NET or anything else**.

> **Requirements:** Windows 10 or 11, 64-bit.

---

## First-time setup

1. When the app opens, click **Index folder** and pick the root folder that contains your BIM files.
2. The first scan can take a few minutes depending on the number of files — extracted text and thumbnails are cached locally so subsequent launches are instant.
3. Start typing in the search box. Results filter live.
4. Click any file to preview metadata and (for IFC) load the 3D model.

The local database is stored next to the executable, so the app stays portable.

---

## Build from source

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download).

```powershell
git clone https://github.com/droselloc/BimExplore.git
cd BimExplore
dotnet build
dotnet run --project src/BimExplorer.App
```

### Produce a distributable executable

```powershell
./publish.ps1
```

This generates `publish/BimExplore.exe` — a single-file, self-contained, win-x64 executable ready to share.

---

## Project layout

```
BimExplorer.sln
src/
  BimExplorer.Data/   # EF Core + SQLite + FTS5 — entities, DbContext, migrations
  BimExplorer.App/    # WPF app (MVVM) + 3D viewer + indexer services
```

## Tech stack

.NET 9 · WPF · EF Core (SQLite + FTS5) · Xbim.Essentials (IFC parsing & geometry) · CommunityToolkit.Mvvm · Assimp (mesh I/O)

---

## Support the project

If BIM Explore saves you time and you'd like to support its development, you can sponsor it via the **Sponsor** button at the top of the repository, or through any of the platforms listed in [`.github/FUNDING.yml`](.github/FUNDING.yml).

Stars on GitHub help too — they make the project more discoverable.

---

## License

[MIT](LICENSE) — free to use, modify and redistribute, including commercially. No warranty.
