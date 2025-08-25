using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CsSolutionRenamer
{
    public class SolutionParser
    {
        private List<string> _lines;
        private string _originalPath;
        private readonly object _lock = new object();

        private static readonly Regex ProjectLineRegex = new Regex(
            @"Project\(""([^""]*)""\)\s*=\s*""([^""]*)""\s*,\s*""([^""]*)""\s*,\s*""([^""]*)""",
            RegexOptions.Compiled);

        public SolutionParser()
        {
            _lines = new List<string>();
        }

        private void EnsureSolutionLoaded()
        {
            if (_lines == null || !_lines.Any())
                throw new InvalidOperationException("No solution loaded. Call LoadSolution first.");
        }

        private static void ValidateProjectParameters(string param1, string param2)
        {
            if (string.IsNullOrWhiteSpace(param1) || string.IsNullOrWhiteSpace(param2))
                throw new ArgumentException("Parameters cannot be null or empty");
        }

        private string GetFinalSavePath(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return _originalPath;
            }

            return outputPath;
        }

        public void LoadSolution(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Solution file not found: {filePath}");

            lock (_lock)
            {
                _originalPath = filePath;
                _lines = File.ReadAllLines(filePath, Encoding.UTF8).ToList();
            }
        }

        public bool ChangeProjectPath(string oldPath, string newPath)
        {
            ValidateProjectParameters(oldPath, newPath);

            lock (_lock)
            {
                EnsureSolutionLoaded();

                bool changed = false;
                for (int i = 0; i < _lines.Count; i++)
                {
                    var line = _lines[i];
                    if (line.StartsWith("Project(", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = ProjectLineRegex.Match(line);
                        if (match.Success && match.Groups[3].Value.Equals(oldPath, StringComparison.OrdinalIgnoreCase))
                        {
                            var projectTypeGuid = match.Groups[1].Value;
                            var projectName = match.Groups[2].Value;
                            var projectGuid = match.Groups[4].Value;

                            _lines[i] =
                                $"Project(\"{projectTypeGuid}\") = \"{projectName}\", \"{newPath}\", \"{projectGuid}\"";
                            changed = true;
                        }
                    }
                }

                return changed;
            }
        }

        public bool ChangeProjectName(string oldName, string newName)
        {
            ValidateProjectParameters(oldName, newName);

            lock (_lock)
            {
                EnsureSolutionLoaded();

                bool changed = false;
                for (int i = 0; i < _lines.Count; i++)
                {
                    var line = _lines[i];
                    if (line.StartsWith("Project(", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = ProjectLineRegex.Match(line);
                        if (match.Success && match.Groups[2].Value.Equals(oldName, StringComparison.OrdinalIgnoreCase))
                        {
                            var projectTypeGuid = match.Groups[1].Value;
                            var oldProjectPath = match.Groups[3].Value;
                            var projectGuid = match.Groups[4].Value;

                            var newProjectPath = UpdateProjectPath(oldProjectPath, oldName, newName);

                            _lines[i] =
                                $"Project(\"{projectTypeGuid}\") = \"{newName}\", \"{newProjectPath}\", \"{projectGuid}\"";
                            changed = true;
                        }
                    }
                }

                return changed;
            }
        }

        private string UpdateProjectPath(string oldPath, string oldProjectName, string newProjectName)
        {
            var directory = Path.GetDirectoryName(oldPath);
            var fileName = Path.GetFileName(oldPath);
            
            var newDirectory = directory?.Replace(oldProjectName, newProjectName) ?? newProjectName;
            var newFileName = fileName?.Replace(oldProjectName, newProjectName) ?? $"{newProjectName}.csproj";
            
            return string.IsNullOrEmpty(newDirectory) 
                ? newFileName 
                : Path.Combine(newDirectory, newFileName).Replace('\\', '/');
        }

        public RenameProjectResult RenameProjectWithFiles(string oldName, string newName, string solutionDirectory)
        {
            ValidateProjectParameters(oldName, newName);
            
            var result = new RenameProjectResult();
            var project = GetProjects().FirstOrDefault(p => p.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase));
            
            if (project == null)
            {
                result.ErrorMessage = $"Project '{oldName}' not found in solution";
                return result;
            }

            var oldProjectDirectory = Path.Combine(solutionDirectory, Path.GetDirectoryName(project.Path) ?? "");
            var newProjectDirectory = Path.Combine(solutionDirectory, Path.GetDirectoryName(project.Path)?.Replace(oldName, newName) ?? newName);
            
            try
            {
                var renamer = new ProjectRenamer();
                var codeResult = renamer.RenameProjectClasses(oldName, newName, oldProjectDirectory);
                result.NamespaceChanges = codeResult;

                if (Directory.Exists(oldProjectDirectory))
                {
                    if (Directory.Exists(newProjectDirectory))
                    {
                        result.ErrorMessage = $"Target directory already exists: {newProjectDirectory}";
                        return result;
                    }

                    Directory.Move(oldProjectDirectory, newProjectDirectory);
                    result.DirectoryRenamed = true;
                    result.OldDirectoryPath = oldProjectDirectory;
                    result.NewDirectoryPath = newProjectDirectory;
                }

                var oldCsprojPath = Path.Combine(newProjectDirectory, $"{oldName}.csproj");
                var newCsprojPath = Path.Combine(newProjectDirectory, $"{newName}.csproj");
                
                if (File.Exists(oldCsprojPath))
                {
                    File.Move(oldCsprojPath, newCsprojPath);
                    result.CsprojRenamed = true;
                    result.OldCsprojPath = oldCsprojPath;
                    result.NewCsprojPath = newCsprojPath;
                }

                ChangeProjectName(oldName, newName);
                result.SolutionUpdated = true;
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                
                try
                {
                    if (result.DirectoryRenamed && Directory.Exists(newProjectDirectory))
                    {
                        Directory.Move(newProjectDirectory, oldProjectDirectory);
                    }
                }
                catch
                {
                    // Ignore rollback errors
                }
            }

            return result;
        }

        public void RenameSolutionFile(string newSolutionName)
        {
            if (string.IsNullOrWhiteSpace(newSolutionName))
                throw new ArgumentException("New solution name cannot be null or empty", nameof(newSolutionName));

            if (_originalPath == null)
                throw new InvalidOperationException("No solution loaded");

            var directory = Path.GetDirectoryName(_originalPath);
            var newPath = Path.Combine(directory!, $"{newSolutionName}.sln");
            
            if (File.Exists(newPath))
                throw new InvalidOperationException($"Solution file already exists: {newPath}");

            File.Move(_originalPath, newPath);
            _originalPath = newPath;
        }

        public void SaveSolution(string outputPath = null)
        {
            lock (_lock)
            {
                EnsureSolutionLoaded();

                string finalPath = GetFinalSavePath(outputPath);

                try
                {
                    var directoryPath = Path.GetDirectoryName(finalPath);
                    if (!string.IsNullOrWhiteSpace(directoryPath) && !Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    File.WriteAllLines(finalPath, _lines, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to save solution to {finalPath}", ex);
                }
            }
        }

        public List<ProjectInfo> GetProjects() =>
            _lines?.Where(line => line.StartsWith("Project("))
                .Select(line => ProjectLineRegex.Match(line))
                .Where(match => match.Success)
                .Select(match => new ProjectInfo
                {
                    ProjectTypeGuid = match.Groups[1].Value,
                    Name = match.Groups[2].Value,
                    Path = match.Groups[3].Value,
                    ProjectGuid = match.Groups[4].Value
                })
                .ToList() ?? new List<ProjectInfo>();

        public bool HasChanges()
        {
            return _lines != null && _lines.Any();
        }

        public void AddProject(string projectTypeGuid, string projectName, string projectPath,
            string projectGuid = null)
        {
            if (_lines == null)
                throw new InvalidOperationException("No solution loaded. Call LoadSolution first.");

            if (string.IsNullOrWhiteSpace(projectName) || string.IsNullOrWhiteSpace(projectPath))
                throw new ArgumentException("Project name and path are required");

            if (string.IsNullOrWhiteSpace(projectGuid))
                projectGuid = Guid.NewGuid().ToString("B").ToUpper();

            var projectLine =
                $"Project(\"{projectTypeGuid}\") = \"{projectName}\", \"{projectPath}\", \"{projectGuid}\"";
            var endProjectLine = "EndProject";

            var insertIndex = _lines.FindLastIndex(line => line.StartsWith("EndProject")) + 1;
            if (insertIndex <= 0)
            {
                insertIndex = _lines.FindIndex(line => line.StartsWith("Global"));
                if (insertIndex < 0) insertIndex = _lines.Count;
            }

            _lines.Insert(insertIndex, projectLine);
            _lines.Insert(insertIndex + 1, endProjectLine);
        }

        private static readonly HashSet<string> ExcludedDirectories =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "bin", "obj", ".vs", ".vscode", "packages", "TestResults", ".git", ".svn", ".hg",
                "node_modules", "dist", "build", "output", "temp", "tmp", ".idea", "debug", "release"
            };

        private static readonly HashSet<string> ExcludedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".user", ".suo", ".cache", ".pdb", ".exe", ".dll", ".tmp", ".log", "thumbs.db", ".ds_store"
        };

        public void BackupSolution(string backupPath = null)
        {
            if (_lines == null)
                throw new InvalidOperationException("No solution loaded. Call LoadSolution first.");

            var solutionDirectory = Path.GetDirectoryName(_originalPath);
            if (string.IsNullOrWhiteSpace(solutionDirectory))
                throw new InvalidOperationException("Invalid solution path");

            if (string.IsNullOrWhiteSpace(backupPath))
            {
                var solutionName = Path.GetFileNameWithoutExtension(_originalPath);
                var parentDirectory = Directory.GetParent(solutionDirectory)?.FullName ?? solutionDirectory;
                backupPath = Path.Combine(parentDirectory, $"{solutionName}_backup_{DateTime.Now:yyyyMMdd_HHmmss}");
            }

            if (Directory.Exists(backupPath))
            {
                Directory.Delete(backupPath, true);
            }

            Directory.CreateDirectory(backupPath);

            CopyDirectorySelectively(solutionDirectory, backupPath);

        }

        private void CopyDirectorySelectively(string sourceDir, string destDir)
        {
            if (!Directory.Exists(sourceDir))
                return;

            var sourceDirName = Path.GetFileName(sourceDir);
            if (ExcludedDirectories.Contains(sourceDirName))
                return;

            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                var fileExtension = Path.GetExtension(file);

                if (ShouldExcludeFile(fileName, fileExtension))
                    continue;

                var destFile = Path.Combine(destDir, fileName);

                try
                {
                    File.Copy(file, destFile, true);
                }
                catch (Exception)
                {
                    // Silently ignore failed copy operations
                }
            }

            foreach (var directory in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(directory);
                if (!ExcludedDirectories.Contains(dirName))
                {
                    var destSubDir = Path.Combine(destDir, dirName);
                    CopyDirectorySelectively(directory, destSubDir);
                }
            }
        }

        private bool ShouldExcludeFile(string fileName, string fileExtension)
        {
            if (ExcludedFiles.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                return true;

            if (ExcludedFiles.Contains(fileExtension))
                return true;

            if (fileName.StartsWith(".", StringComparison.OrdinalIgnoreCase) &&
                !fileName.Equals(".gitignore", StringComparison.OrdinalIgnoreCase) &&
                !fileName.Equals(".editorconfig", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        public List<string> GetProjectFiles()
        {
            if (_lines == null)
                throw new InvalidOperationException("No solution loaded. Call LoadSolution first.");

            var solutionDirectory = Path.GetDirectoryName(_originalPath);
            if (string.IsNullOrWhiteSpace(solutionDirectory))
                return new List<string>();

            return GetProjectFilesRecursive(solutionDirectory)
                .OrderBy(f => f)
                .ToList();
        }

        private IEnumerable<string> GetProjectFilesRecursive(string directory)
        {
            if (!Directory.Exists(directory) || 
                ExcludedDirectories.Contains(Path.GetFileName(directory)))
                yield break;

            foreach (var file in Directory.GetFiles(directory)
                .Where(f => !ShouldExcludeFile(Path.GetFileName(f), Path.GetExtension(f))))
            {
                yield return file;
            }

            foreach (var subDir in Directory.GetDirectories(directory)
                .Where(d => !ExcludedDirectories.Contains(Path.GetFileName(d))))
            {
                foreach (var file in GetProjectFilesRecursive(subDir))
                {
                    yield return file;
                }
            }
        }
    }

    public class ProjectInfo
    {
        public string ProjectTypeGuid { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string ProjectGuid { get; set; }
    }

    public class RenameProjectResult
    {
        public bool Success { get; set; }
        public bool DirectoryRenamed { get; set; }
        public bool CsprojRenamed { get; set; }
        public bool SolutionUpdated { get; set; }
        public string OldDirectoryPath { get; set; }
        public string NewDirectoryPath { get; set; }
        public string OldCsprojPath { get; set; }
        public string NewCsprojPath { get; set; }
        public string ErrorMessage { get; set; }
        public RenameResult NamespaceChanges { get; set; }
    }
}