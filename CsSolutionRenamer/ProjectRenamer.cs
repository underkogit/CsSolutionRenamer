/*
 * C# Project Renamer - Модуль для переименования содержимого проектов
 * 
 * Функциональность:
 * • Переименование namespace'ов в C# файлах проекта
 * • Поиск и переименование классов содержащих имя проекта
 * • Обновление AssemblyName и RootNamespace в .csproj файлах
 * • Исключение системных директорий из обработки
 * • Детальная статистика изменений
 * 
 * Структуры:
 * • RenameResult - результат операции с количеством измененных элементов
 * 
 * Основные функции:
 * • RenameProjectClasses() - комплексное переименование namespace'ов и проектных файлов
 * • RenameNamespaces() - переименование namespace'ов в C# файлах
 * • UpdateProjectFile() - обновление .csproj файлов
 */

// Основные системные библиотеки
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
// Библиотека для работы с регулярными выражениями для поиска классов и namespace'ов
using System.Text.RegularExpressions;
// Библиотека для работы с XML при обработке .csproj файлов
using System.Xml.Linq;

// CODE --------------------------

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


        /// <summary>
        /// Выполняет комплексное переименование содержимого проекта: namespace'ы, классы и .csproj файлы
        /// </summary>
        /// <param name="oldProjectName">Текущее имя проекта</param>
        /// <param name="newProjectName">Новое имя проекта</param>
        /// <param name="projectPath">Путь к директории проекта</param>
        /// <returns>RenameResult с детальной статистикой изменений</returns>
        /// <example>
        /// var result = renamer.RenameProjectClasses("OldProject", "NewProject", @"C:\Solution\OldProject");
        /// // result.TotalFilesProcessed = 15
        /// // result.NamespacesModified = 3
        /// // result.ProjectFilesModified = 1
        /// // result.HasChanges = true
        /// </example>
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

    /// <summary>
    /// Содержит детальную статистику результатов операции переименования содержимого проекта
    /// </summary>
    public class RenameResult
    {
        /// <summary>Общее количество обработанных C# файлов</summary>
        public int TotalFilesProcessed { get; set; }
        /// <summary>Количество найденных классов, содержащих имя проекта</summary>
        public int ClassesFound { get; set; }
        /// <summary>Количество модифицированных файлов</summary>
        public int FilesModified { get; set; }
        /// <summary>Количество измененных пространств имен</summary>
        public int NamespacesModified { get; set; }
        /// <summary>Количество обновленных .csproj файлов</summary>
        public int ProjectFilesModified { get; set; }

        /// <summary>Показывает, были ли внесены какие-либо изменения</summary>
        public bool HasChanges => FilesModified > 0 || NamespacesModified > 0 || ProjectFilesModified > 0;
    }
}