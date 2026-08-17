using System.Text;
using System.Text.RegularExpressions;

namespace GitDoc;

internal static partial class MarkdownReport
{
    public static string Create(CommandOptions options, string repository, string mergeBase, IReadOnlyList<CommitInfo> commits, IReadOnlyList<FileChange> files)
    {
        var title = options.Title ?? $"Resumen de cambios — {options.Branch}";
        var added = files.Sum(file => file.Added ?? 0);
        var deleted = files.Sum(file => file.Deleted ?? 0);
        var authors = commits.Select(commit => commit.Author).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var builder = new StringBuilder();

        builder.AppendLine($"# {Escape(title)}").AppendLine();
        builder.AppendLine("> Documento generado automáticamente a partir del historial de Git.").AppendLine();
        builder.AppendLine("## Información general").AppendLine();
        builder.AppendLine($"- **Repositorio:** `{EscapeCode(Path.GetFileName(repository))}`");
        builder.AppendLine($"- **Rama base:** `{EscapeCode(options.BaseBranch)}`");
        builder.AppendLine($"- **Rama analizada:** `{EscapeCode(options.Branch)}`");
        builder.AppendLine($"- **Punto de comparación:** `{mergeBase[..Math.Min(7, mergeBase.Length)]}`");
        builder.AppendLine($"- **Fecha de generación:** {DateTimeOffset.Now:yyyy-MM-dd HH:mm zzz}").AppendLine();
        builder.AppendLine("## Resumen ejecutivo").AppendLine();
        builder.AppendLine($"La rama contiene **{commits.Count} {Word(commits.Count, "commit", "commits")}** realizados por **{authors.Count} {Word(authors.Count, "colaborador", "colaboradores")}** y modifica **{files.Count} {Word(files.Count, "archivo", "archivos")}**.");
        builder.AppendLine($"El impacto acumulado es de **{added:N0} líneas agregadas** y **{deleted:N0} líneas eliminadas**.").AppendLine();
        builder.AppendLine("## Cambios realizados").AppendLine();

        if (commits.Count == 0) builder.AppendLine("No se encontraron commits exclusivos de la rama analizada.").AppendLine();
        else foreach (var group in commits.GroupBy(commit => Category(commit.Subject)))
        {
            builder.AppendLine($"### {group.Key}").AppendLine();
            foreach (var commit in group) builder.AppendLine($"- {Escape(CleanSubject(commit.Subject))} (`{commit.ShortHash}`) — {Escape(commit.Author)}, {commit.Date:yyyy-MM-dd}");
            builder.AppendLine();
        }

        builder.AppendLine("## Archivos modificados").AppendLine();
        builder.AppendLine("| Estado | Archivo | Agregadas | Eliminadas |");
        builder.AppendLine("|:--:|---|---:|---:|");
        foreach (var file in files) builder.AppendLine($"| {Status(file.Status)} | `{EscapeCode(file.Path)}` | {Format(file.Added)} | {Format(file.Deleted)} |");
        if (files.Count == 0) builder.AppendLine("| — | Sin cambios | 0 | 0 |");
        builder.AppendLine().AppendLine("## Participantes").AppendLine();
        foreach (var author in authors) builder.AppendLine($"- {Escape(author)}");
        if (authors.Count == 0) builder.AppendLine("- Sin participantes");
        builder.AppendLine().AppendLine("## Notas para la documentación final").AppendLine();
        builder.AppendLine("- Completar el contexto funcional y el objetivo del cambio.");
        builder.AppendLine("- Indicar configuraciones, migraciones o pasos de despliegue necesarios.");
        builder.AppendLine("- Registrar riesgos conocidos, pruebas realizadas y evidencias.");
        return builder.ToString();
    }

    private static string Category(string subject) => PrefixRegex().Match(subject).Groups[1].Value.ToLowerInvariant() switch
    {
        "feat" => "Nuevas funcionalidades", "fix" => "Correcciones", "refactor" or "perf" => "Mejoras técnicas",
        "test" => "Pruebas", "docs" => "Documentación", "build" or "ci" or "chore" => "Infraestructura y mantenimiento",
        _ => "Otros cambios"
    };
    private static string CleanSubject(string subject) => PrefixRegex().Replace(subject, "").Trim();
    private static string Status(string status) => status[0] switch { 'A' => "Agregado", 'D' => "Eliminado", 'M' => "Modificado", 'R' => "Renombrado", 'C' => "Copiado", _ => status };
    private static string Format(int? value) => value?.ToString("N0") ?? "Binario";
    private static string Word(int count, string singular, string plural) => count == 1 ? singular : plural;
    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
    private static string EscapeCode(string value) => value.Replace("`", "\\`");

    [GeneratedRegex(@"^(feat|fix|refactor|perf|test|docs|build|ci|chore)(?:\([^)]*\))?!?:\s*", RegexOptions.IgnoreCase)]
    private static partial Regex PrefixRegex();
}
