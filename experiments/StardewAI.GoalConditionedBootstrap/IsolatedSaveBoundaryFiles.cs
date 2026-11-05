using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace StardewAI.GoalConditionedBootstrap;

internal static class IsolatedSaveBoundaryFiles
{
    internal static void ValidateOutput(string outputPath, string isolationRoot)
    {
        Require(Path.IsPathFullyQualified(outputPath) && outputPath.IndexOfAny(new[] { '*', '?', '[', ']' }) < 0,
            "Terminal boundary report requires an exact absolute path.");
        var report = Path.GetFullPath(outputPath);
        var reportRoot = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(isolationRoot)));
        Require(string.Equals(Path.GetDirectoryName(report), reportRoot, PathComparison) &&
            Path.GetExtension(report).Equals(".json", StringComparison.OrdinalIgnoreCase) && !File.Exists(report) &&
            !Directory.Exists(report) && new FileInfo(report).LinkTarget is null,
            "Terminal boundary report must be a new JSON file beside the isolation root, never an input or save file.");
    }

    internal static XDocument ReadDocument(string target)
    {
        using var reader = XmlReader.Create(target, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        return XDocument.Load(reader);
    }

    internal static void Commit(string source, string target, string isolationRoot, string sourceHash, XDocument document)
    {
        // Replace only the clone's directory entry, including when it is hard-linked.
        var staged = target + ".boundary-" + Guid.NewGuid().ToString("N") + ".tmp";
        using (var stream = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false }))
            document.Save(writer);
        ValidateScope(source, target, isolationRoot);
        Require(sourceHash == CurrentTeacherFrontierSupport.HashFile(source) && sourceHash == CurrentTeacherFrontierSupport.HashFile(target),
            "Terminal boundary source or target changed during preparation.");
        File.Move(staged, target, overwrite: true);
        Require(sourceHash == CurrentTeacherFrontierSupport.HashFile(source), "Original save changed during terminal boundary preparation.");
    }

    internal static (string Source, string Target) ValidateScope(string sourcePath, string targetPath, string isolationRoot)
    {
        static string Absolute(string path)
        {
            Require(!string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path) && path.IndexOfAny(new[] { '*', '?', '[', ']' }) < 0,
                "Terminal boundary paths must be exact absolute paths without wildcards.");
            return Path.GetFullPath(path);
        }
        var source = Absolute(sourcePath);
        var target = Absolute(targetPath);
        var root = Absolute(isolationRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var prefix = root + Path.DirectorySeparatorChar;
        Require(Directory.Exists(root) && root != Path.GetPathRoot(root)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) &&
            File.Exists(source) && File.Exists(target) && !source.Equals(target, PathComparison) &&
            target.StartsWith(prefix, PathComparison) && !source.StartsWith(prefix, PathComparison) &&
            Path.GetFileName(source) == Path.GetFileName(target), "Terminal boundary must write only a strict isolated copy, never its source or a drive root.");
        foreach (var path in new[] { source, target, root })
        {
            Require((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0, "Terminal boundary rejects reparse points.");
            for (var parent = new DirectoryInfo(File.Exists(path) ? Path.GetDirectoryName(path)! : path); parent is not null; parent = parent.Parent)
                Require((parent.Attributes & FileAttributes.ReparsePoint) == 0, "Terminal boundary rejects reparse-point ancestors.");
        }
        return (source, target);
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }
}
