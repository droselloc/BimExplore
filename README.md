# BIM Explore

Aplicacion de escritorio para Windows que indexa, busca y explora archivos BIM (Building Information Modeling).

- Busqueda full-text sobre nombre, proyecto, disciplina, autor y texto extraido
- Visor 3D de IFC con orbitar / pan / zoom
- Indexado de carpetas con extraccion de metadatos
- Generacion automatica de miniaturas via Xbim

## Descargar y ejecutar

1. Ve a la pagina de [Releases](../../releases).
2. Descarga el archivo `BimExplore-win-x64.zip` de la ultima release.
3. Descomprimelo en cualquier carpeta.
4. Doble click en `BimExplore.exe`.

Es un ejecutable autocontenido: **no necesitas instalar .NET ni nada mas**.

> Requisitos: Windows 10/11 de 64 bits.

## Compilar desde el codigo

Requiere [.NET 9 SDK](https://dotnet.microsoft.com/download).

```powershell
git clone https://github.com/<TU_USUARIO>/BimExplore.git
cd BimExplore
dotnet build
dotnet run --project src/BimExplorer.App
```

### Generar el ejecutable distribuible localmente

```powershell
./publish.ps1
```

Esto genera `publish/BimExplore.exe` (single-file, self-contained, win-x64) listo para distribuir.

## Estructura del proyecto

```
BimExplorer.sln
src/
  BimExplorer.Data/   # EF Core + SQLite + FTS5
  BimExplorer.App/    # WPF (MVVM) + visor 3D
```

## Stack

.NET 9 - WPF - EF Core (SQLite + FTS5) - Xbim.Essentials - CommunityToolkit.Mvvm

## Licencia

MIT - ver [LICENSE](LICENSE).
