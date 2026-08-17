# GitDoc

Aplicación de consola que genera un resumen Markdown de los cambios exclusivos de una rama Git.

## Uso

```bash
dotnet run -- --base main --branch dev-2026 --output sprint.md
```

Para analizar otro repositorio:

```bash
dotnet run -- --repo /ruta/al/repositorio --base develop --branch feature/pagos --output pagos.md
```

GitDoc compara las ramas desde su ancestro común (`merge-base`), clasifica commits que usan
[Conventional Commits](https://www.conventionalcommits.org/) y genera estadísticas de los archivos modificados.

Consulte todas las opciones con `dotnet run -- --help`.
