using System.Globalization;
using System.Text;

namespace GitDoc;

public static class GitDocCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            var options = CommandOptions.Parse(args);
            if (options.ShowHelp) { PrintHelp(); return 0; }

            var repository = Path.GetFullPath(options.Repository);
            var output = Path.GetFullPath(options.Output);
            var git = new GitClient(repository);
            await git.EnsureRepositoryAsync();
            await git.EnsureRevisionAsync(options.BaseBranch);
            await git.EnsureRevisionAsync(options.Branch);

            var mergeBase = (await git.RunAsync("merge-base", options.BaseBranch, options.Branch)).Trim();
            var commits = await git.GetCommitsAsync(mergeBase, options.Branch);
            var files = await git.GetFilesAsync(mergeBase, options.Branch);
            var document = MarkdownReport.Create(options, repository, mergeBase, commits, files);

            var directory = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(output, document, new UTF8Encoding(false));
            Console.WriteLine($"Documento generado: {output}");
            Console.WriteLine($"{commits.Count} commits, {files.Count} archivos modificados.");
            return 0;
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine($"Error: {exception.Message}");
            Console.Error.WriteLine("Use --help para ver las opciones disponibles.");
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Error: {exception.Message}");
            return 1;
        }
    }

    private static void PrintHelp() => Console.WriteLine("""
        GitDoc - genera un resumen Markdown de los cambios de una rama.

        Uso:
          gitdoc --base <rama-base> --branch <rama> [opciones]
          dotnet run -- --base main --branch dev-2026 --output sprint.md

        Opciones:
          --base, -b       Rama base (predeterminado: main)
          --branch, -r     Rama que se desea documentar (obligatoria)
          --repo           Ruta del repositorio (predeterminado: directorio actual)
          --output, -o     Archivo Markdown (predeterminado: CHANGELOG_SPRINT.md)
          --title, -t      Título del documento
          --help, -h       Mostrar esta ayuda
        """);
}

internal sealed record CommandOptions(string BaseBranch, string Branch, string Repository, string Output, string? Title, bool ShowHelp)
{
    public static CommandOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        { ["-b"] = "--base", ["-r"] = "--branch", ["-o"] = "--output", ["-t"] = "--title" };
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "--base", "--branch", "--repo", "--output", "--title" };

        for (var index = 0; index < args.Length; index++)
        {
            var argument = aliases.GetValueOrDefault(args[index], args[index]);
            if (argument is "--help" or "-h") return new("main", "", ".", "CHANGELOG_SPRINT.md", null, true);
            if (!allowed.Contains(argument)) throw new ArgumentException($"Opción no reconocida: {args[index]}");
            if (++index >= args.Length) throw new ArgumentException($"Falta el valor de {argument}.");
            values[argument] = args[index];
        }

        var branch = values.GetValueOrDefault("--branch");
        if (string.IsNullOrWhiteSpace(branch)) throw new ArgumentException("Debe indicar la rama con --branch.");
        return new(values.GetValueOrDefault("--base", "main"), branch,
            values.GetValueOrDefault("--repo", "."), values.GetValueOrDefault("--output", "CHANGELOG_SPRINT.md"),
            values.GetValueOrDefault("--title"), false);
    }
}

internal sealed record CommitInfo(string Hash, string ShortHash, string Author, DateTimeOffset Date, string Subject);
internal sealed record FileChange(string Status, string Path, int? Added, int? Deleted);

internal sealed class GitClient(string repository)
{
    public async Task EnsureRepositoryAsync()
    {
        if (!Directory.Exists(repository)) throw new ArgumentException($"El repositorio no existe: {repository}");
        var result = await RunProcessAsync(["rev-parse", "--is-inside-work-tree"]);
        if (result.ExitCode != 0 || result.Output.Trim() != "true") throw new ArgumentException($"La ruta no es un repositorio Git: {repository}");
    }

    public async Task EnsureRevisionAsync(string revision)
    {
        var result = await RunProcessAsync(["rev-parse", "--verify", $"{revision}^{{commit}}"]);
        if (result.ExitCode != 0) throw new ArgumentException($"No se encontró la rama o revisión '{revision}'.");
    }

    public async Task<IReadOnlyList<CommitInfo>> GetCommitsAsync(string mergeBase, string branch)
    {
        const string separator = "\u001f";
        var output = await RunAsync("log", "--reverse", "--format=%H%x1f%h%x1f%an%x1f%aI%x1f%s", $"{mergeBase}..{branch}");
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Split(separator))
            .Where(parts => parts.Length == 5)
            .Select(parts => new CommitInfo(parts[0], parts[1], parts[2], DateTimeOffset.Parse(parts[3], CultureInfo.InvariantCulture), parts[4])).ToList();
    }

    public async Task<IReadOnlyList<FileChange>> GetFilesAsync(string mergeBase, string branch)
    {
        var stats = new Dictionary<string, (int? Added, int? Deleted)>(StringComparer.Ordinal);
        foreach (var line in (await RunAsync("diff", "--numstat", $"{mergeBase}..{branch}")).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t', 3);
            if (parts.Length == 3) stats[parts[2]] = (ParseStat(parts[0]), ParseStat(parts[1]));
        }

        var changes = new List<FileChange>();
        foreach (var line in (await RunAsync("diff", "--name-status", $"{mergeBase}..{branch}")).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t');
            if (parts.Length < 2) continue;
            var path = parts[^1];
            var stat = stats.GetValueOrDefault(path);
            changes.Add(new(parts[0], path, stat.Added, stat.Deleted));
        }
        return changes;
    }

    public async Task<string> RunAsync(params string[] arguments)
    {
        var result = await RunProcessAsync(arguments);
        if (result.ExitCode != 0) throw new InvalidOperationException(result.Error.Trim().Length > 0 ? result.Error.Trim() : "Git finalizó con error.");
        return result.Output;
    }

    private async Task<ProcessResult> RunProcessAsync(IEnumerable<string> arguments)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo("git")
        { WorkingDirectory = repository, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(startInfo) ?? throw new InvalidOperationException("No fue posible ejecutar Git.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new(process.ExitCode, await outputTask, await errorTask);
    }

    private static int? ParseStat(string value) => int.TryParse(value, out var number) ? number : null;
    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
