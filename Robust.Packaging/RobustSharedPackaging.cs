﻿using Robust.Packaging.AssetProcessing;

namespace Robust.Packaging;

public sealed class RobustSharedPackaging
{
    public static IReadOnlySet<string> SharedIgnoredResources { get; } = new HashSet<string>
    {
        "ss13model.7z",
        "ResourcePack.zip",
        "buildResourcePack.py",
        "CONTENT_GOES_HERE",
        ".gitignore",
        ".directory",
        ".DS_Store"
    };

    public static async Task WriteContentAssemblies(
        AssetPass pass,
        string contentDir,
        string binDir,
        IEnumerable<string> contentAssemblies,
        CancellationToken cancel = default)
    {
        await WriteContentAssemblies("Assemblies", pass, contentDir, binDir, contentAssemblies, cancel);
    }

    public static Task WriteContentAssemblies(
        string target,
        AssetPass pass,
        string contentDir,
        string binDir,
        IEnumerable<string> contentAssemblies,
        CancellationToken cancel = default)
    {
        var files = new List<string>();

        var sourceDir = Path.Combine(contentDir, "bin", binDir);

        foreach (var asm in contentAssemblies)
        {
            files.Add($"{asm}.dll");

            var pdbPath = $"{asm}.pdb";
            if (File.Exists(Path.Combine(sourceDir, pdbPath)))
                files.Add(pdbPath);
        }

        foreach (var f in files)
        {
            cancel.ThrowIfCancellationRequested();
            pass.InjectFileFromDisk($"{target}/{f}", Path.Combine(sourceDir, f));
        }

        return Task.CompletedTask;
    }

    public static Task DoResourceCopy(
        string diskSource,
        AssetPass pass,
        IReadOnlySet<string> ignoreSet,
        CancellationToken cancel = default)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(diskSource))
        {
            cancel.ThrowIfCancellationRequested();

            var filename = Path.GetFileName(path);
            if (ignoreSet.Contains(filename))
                continue;

            var targetPath = filename;
            if (Directory.Exists(path))
                CopyDirIntoZip(path, targetPath, pass);
            else
                pass.InjectFileFromDisk(targetPath, path);
        }

        return Task.CompletedTask;
    }

    private static void CopyDirIntoZip(string directory, string basePath, AssetPass pass)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
        {
            var relPath = Path.GetRelativePath(directory, file);
            if (Path.DirectorySeparatorChar != '/')
                relPath = relPath.Replace(Path.DirectorySeparatorChar, '/');

            var zipPath = $"{basePath}/{relPath}";

            // Console.WriteLine($"{directory}/{zipPath} -> /{zipPath}");
            pass.InjectFileFromDisk(zipPath, file);
        }
    }
}
