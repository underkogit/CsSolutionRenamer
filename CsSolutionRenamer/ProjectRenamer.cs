using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CsSolutionRenamer
{
    public class ProjectRenamer
    {
        private static readonly Regex ClassDeclarationRegex = new Regex(
            @"(?<access>public|private|protected|internal)?\s*(?<modifier>static|abstract|sealed|partial)?\s*(?<type>class|interface|enum|struct)\s+(?<name>\w+)",
            RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly Regex NamespaceRegex = new Regex(
            @"namespace\s+([A-Za-z_][A-Za-z0-9_.]*)",
            RegexOptions.Compiled | RegexOptions.Multiline);


        private static readonly HashSet<string> ExcludedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bin", "obj", ".vs", ".vscode", "packages", "TestResults", ".git", ".idea"
        };


        public RenameResult RenameProjectClasses(string oldProjectName, string newProjectName, string projectPath)
        {
            ValidateInputParameters(oldProjectName, newProjectName, projectPath);

            var csFiles = GetCSharpFiles(projectPath);
            var classesToRename = FindClassesToRename(csFiles, oldProjectName, newProjectName);

            var result = new RenameResult
            {
                TotalFilesProcessed = csFiles.Count,
                ClassesFound = classesToRename.Count
            };

            if (classesToRename.Any())
            {
                result.NamespacesModified = RenameNamespaces(csFiles, oldProjectName, newProjectName);
                result.ProjectFilesModified = UpdateProjectFile(projectPath, oldProjectName, newProjectName);
            }

            return result;
        }

        private void ValidateInputParameters(string oldProjectName, string newProjectName, string projectPath)
        {
            if (string.IsNullOrWhiteSpace(oldProjectName))
                throw new ArgumentException("Old project name cannot be null or empty", nameof(oldProjectName));

            if (string.IsNullOrWhiteSpace(newProjectName))
                throw new ArgumentException("New project name cannot be null or empty", nameof(newProjectName));

            if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
                throw new ArgumentException($"Project path does not exist: {projectPath}", nameof(projectPath));
        }

        private Dictionary<string, string> FindClassesToRename(List<string> csFiles, string oldProjectName, string newProjectName)
        {
            var projectBaseName = ExtractBaseName(oldProjectName);
            var newProjectBaseName = ExtractBaseName(newProjectName);
            
            return csFiles
                .SelectMany(file => GetClassesFromFile(file))
                .Where(className => className.Contains(projectBaseName, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    className => className,
                    className => className.Replace(projectBaseName, newProjectBaseName, StringComparison.OrdinalIgnoreCase)
                );
        }
        
        private IEnumerable<string> GetClassesFromFile(string file)
        {
            try
            {
                var content = File.ReadAllText(file, Encoding.UTF8);
                return ClassDeclarationRegex.Matches(content)
                    .Cast<Match>()
                    .Select(match => match.Groups["name"].Value)
                    .Distinct();
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        private static string ExtractBaseName(string projectName) =>
            projectName.Split('.').FirstOrDefault() ?? projectName;

        private List<string> GetCSharpFiles(string projectPath) =>
            Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories)
                .Where(file => !ShouldExcludeFile(Path.GetRelativePath(projectPath, file)))
                .ToList();

        private bool ShouldExcludeFile(string relativePath) =>
            relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => ExcludedDirectories.Contains(segment));


        private int RenameNamespaces(List<string> csFiles, string oldProjectName, string newProjectName) =>
            csFiles.Sum(file => RenameNamespacesInFile(file, oldProjectName, newProjectName));
        
        private int RenameNamespacesInFile(string file, string oldProjectName, string newProjectName)
        {
            try
            {
                var content = File.ReadAllText(file, Encoding.UTF8);
                var namespacesToReplace = NamespaceRegex.Matches(content)
                    .Cast<Match>()
                    .Select(match => match.Groups[1].Value)
                    .Where(ns => ns.StartsWith(oldProjectName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (!namespacesToReplace.Any())
                    return 0;

                var updatedContent = namespacesToReplace.Aggregate(content, (current, namespaceName) =>
                {
                    var newNamespaceName = newProjectName + namespaceName.Substring(oldProjectName.Length);
                    return current.Replace($"namespace {namespaceName}", $"namespace {newNamespaceName}");
                });

                File.WriteAllText(file, updatedContent, Encoding.UTF8);
                return namespacesToReplace.Count;
            }
            catch
            {
                return 0;
            }
        }

        private int UpdateProjectFile(string projectPath, string oldProjectName, string newProjectName) =>
            Directory.GetFiles(projectPath, "*.csproj", SearchOption.TopDirectoryOnly)
                .Sum(file => UpdateSingleProjectFile(file, oldProjectName, newProjectName));
        
        private static int UpdateSingleProjectFile(string csprojFile, string oldProjectName, string newProjectName)
        {
            try
            {
                var doc = XDocument.Load(csprojFile);
                var elementsToUpdate = new[] { "AssemblyName", "RootNamespace" };
                
                var updated = elementsToUpdate
                    .Select(elementName => doc.Descendants(elementName).FirstOrDefault())
                    .Where(element => element != null && 
                                    element.Value.Equals(oldProjectName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                
                if (!updated.Any())
                    return 0;
                
                updated.ForEach(element => element.Value = newProjectName);
                doc.Save(csprojFile);
                return 1;
            }
            catch
            {
                return 0;
            }
        }
    }

    public class RenameResult
    {
        public int TotalFilesProcessed { get; set; }
        public int ClassesFound { get; set; }
        public int FilesModified { get; set; }
        public int NamespacesModified { get; set; }
        public int ProjectFilesModified { get; set; }

        public bool HasChanges => FilesModified > 0 || NamespacesModified > 0 || ProjectFilesModified > 0;
    }
}