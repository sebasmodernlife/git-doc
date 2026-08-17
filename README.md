# GitDoc

GitDoc es una aplicación de consola que genera un documento Markdown con el resumen de los cambios realizados durante un sprint. Compara dos revisiones de Git, recopila todos los commits incluidos y presenta los archivos y líneas modificadas.

Cuando los mensajes siguen [Conventional Commits](https://www.conventionalcommits.org/), GitDoc agrupa automáticamente funcionalidades, correcciones, pruebas, documentación y mejoras técnicas.

## Requisitos

- Git instalado y disponible desde la terminal.
- .NET SDK 10 para compilar el proyecto.
- .NET Runtime 10 para ejecutar la publicación portable. Los binarios autónomos no requieren instalar .NET.

## Ejecutar desde el código fuente

Desde el directorio de GitDoc:

```bash
dotnet run -- --base main --branch dev-2026 --output sprint.md
```

Para analizar otro repositorio:

```bash
dotnet run -- \
  --repo /ruta/al/repositorio \
  --base develop \
  --branch feature/pagos \
  --output pagos.md \
  --title "Sprint - Módulo de pagos"
```

En PowerShell puede escribirse el comando en una sola línea:

```powershell
dotnet run -- --repo C:\Proyectos\MiApp --base develop --branch feature/pagos --output pagos.md --title "Sprint - Módulo de pagos"
```

## Compilar

```bash
dotnet build --configuration Release
```

El resultado se guarda en `bin/Release/net10.0/`.

## Publicación portable

Esta publicación genera un mismo `GitDoc.dll` que puede copiarse y ejecutarse en Windows, Linux o macOS. La máquina de destino debe tener instalado el Runtime de .NET 10.

```bash
dotnet publish --configuration Release --self-contained false --output publish/portable
```

Ejecución en cualquiera de los tres sistemas:

```bash
dotnet publish/portable/GitDoc.dll --base main --branch develop --output sprint.md
```

## Binarios autónomos

Los ejecutables autónomos incluyen el runtime y no requieren instalar .NET. Debido a las diferencias entre sistemas operativos y arquitecturas, se debe generar uno para cada plataforma.

### Windows x64

```bash
dotnet publish --configuration Release --runtime win-x64 --self-contained true \
  -p:PublishSingleFile=true --output publish/win-x64
```

```powershell
.\publish\win-x64\GitDoc.exe --base main --branch develop --output sprint.md
```

### Linux x64

```bash
dotnet publish --configuration Release --runtime linux-x64 --self-contained true \
  -p:PublishSingleFile=true --output publish/linux-x64
```

```bash
./publish/linux-x64/GitDoc --base main --branch develop --output sprint.md
```

### macOS Intel

```bash
dotnet publish --configuration Release --runtime osx-x64 --self-contained true \
  -p:PublishSingleFile=true --output publish/osx-x64
```

### macOS Apple Silicon

```bash
dotnet publish --configuration Release --runtime osx-arm64 --self-contained true \
  -p:PublishSingleFile=true --output publish/osx-arm64
```

```bash
./publish/osx-arm64/GitDoc --base main --branch develop --output sprint.md
```

Si el archivo pierde el permiso de ejecución al copiarlo:

```bash
chmod +x ./publish/linux-x64/GitDoc
chmod +x ./publish/osx-arm64/GitDoc
```

## Instrucciones de uso

```text
gitdoc --base <revisión-base> --branch <revisión-final> [opciones]
```

| Opción | Alias | Obligatoria | Descripción |
|---|---|:---:|---|
| `--base` | `-b` | No | Rama, etiqueta o commit base. El valor predeterminado es `main`. |
| `--branch` | `-r` | Sí | Rama, etiqueta o commit final que se desea documentar. |
| `--repo` | — | No | Ruta del repositorio. De forma predeterminada utiliza el directorio actual. |
| `--output` | `-o` | No | Ruta del Markdown. El valor predeterminado es `CHANGELOG_SPRINT.md`. |
| `--title` | `-t` | No | Título que aparecerá en el documento. |
| `--help` | `-h` | No | Muestra la ayuda de la aplicación. |

```bash
dotnet run -- --help
```

## Documentar varios commits de un sprint

`--base` debe identificar el estado anterior al sprint y `--branch` el estado final. GitDoc incluirá todos los commits comprendidos entre ambos puntos.

```bash
git tag sprint-1-inicio

# Después de realizar los commits del sprint:
dotnet run -- \
  --repo /ruta/al/repositorio \
  --base sprint-1-inicio \
  --branch develop \
  --output sprint-1.md \
  --title "Sprint 1"
```

También puede utilizarse directamente el código de un commit:

```bash
dotnet run -- --base b2c3456 --branch develop --output sprint-1.md
```

Para consultar los códigos disponibles:

```bash
git log --oneline --graph --decorate --all
```

> Si la revisión base y la revisión final apuntan al mismo commit, no existen cambios exclusivos y el documento mostrará cero commits.

## Contenido generado

- Información del repositorio y revisiones comparadas.
- Resumen de commits, colaboradores y líneas modificadas.
- Commits agrupados por tipo de cambio.
- Tabla de archivos agregados, modificados, eliminados o renombrados.
- Secciones editables para pruebas, despliegue, riesgos y observaciones.
