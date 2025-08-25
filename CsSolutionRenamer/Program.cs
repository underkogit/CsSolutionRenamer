/*
 * C# Solution Renamer - Главный модуль приложения
 * 
 * Функциональность:
 * • Интерактивное отображение проектов и их пространств имен
 * • Выбор проектов для переименования с пользовательским интерфейсом
 * • Два режима переименования: только в .sln или полное переименование
 * • Переименование файлов решения
 * • Детальная отчетность об изменениях
 * 
 * Структуры:
 * • ProjectWithNamespace - связывает проект с найденными пространствами имен
 * 
 * Основные функции:
 * • GetProjectNamespaces() - извлечение пространств имен из C# файлов
 * • DisplayProjectsAndNamespaces() - отображение проектов с их namespace'ами
 * • SelectProjectsForEditing() - интерактивный выбор проектов
 * • ProcessSelectedProjects() - обработка выбранных проектов
 */

// Основные системные библиотеки для работы с файловой системой и коллекциями
using CsSolutionRenamer;
// Библиотека для работы с регулярными выражениями при поиске namespace'ов
using System.Text.RegularExpressions;

// CODE --------------------------

Console.WriteLine("=== C# Solution Renamer ===");
Console.WriteLine();

var solutionPath = @"C:\Users\UnderKo\RiderProjects\Robokassa";

var solutionFiles = Directory.GetFiles(solutionPath)
    .Where(file => file.EndsWith(".sln") && !Path.GetFileName(file).StartsWith("rep_"))
    .ToList();

if (!solutionFiles.Any())
{
    Console.WriteLine("Файлы решения не найдены.");
    return;
}

var solutionParser = new SolutionParser();
solutionParser.LoadSolution(solutionFiles.First());

var projects = solutionParser.GetProjects();
var solutionDirectory = Path.GetDirectoryName(solutionFiles.First());

Console.WriteLine($"Найдено проектов: {projects.Count}");
Console.WriteLine();

var projectsWithNamespaces = new List<ProjectWithNamespace>();

foreach (var project in projects)
{
    var projectDirectory = GetProjectDirectory(solutionDirectory, project.Path);
    if (Directory.Exists(projectDirectory))
    {
        var namespaces = GetProjectNamespaces(projectDirectory);
        projectsWithNamespaces.Add(new ProjectWithNamespace(project, projectDirectory, namespaces));
    }
}

DisplayProjectsAndNamespaces(projectsWithNamespaces);

var selectedProjects = SelectProjectsForEditing(projectsWithNamespaces);

if (selectedProjects.Any())
{
    ProcessSelectedProjects(selectedProjects, solutionParser, solutionDirectory);
    
    Console.Write("\nХотите переименовать файл решения? (y/n): ");
    var renameSolution = Console.ReadLine()?.Trim().ToLower();
    
    if (renameSolution == "y" || renameSolution == "yes")
    {
        Console.Write("Введите новое имя для файла решения (без расширения): ");
        var newSolutionName = Console.ReadLine()?.Trim();
        
        if (!string.IsNullOrEmpty(newSolutionName))
        {
            try
            {
                solutionParser.RenameSolutionFile(newSolutionName);
                Console.WriteLine($"✓ Файл решения переименован в: {newSolutionName}.sln");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Ошибка при переименовании файла решения: {ex.Message}");
            }
        }
    }
    
    Console.WriteLine("\nИзменения сохранены.");
}
else
{
    Console.WriteLine("\nИзменения не внесены.");
}

/// <summary>
/// Получает полный путь к директории проекта на основе пути к solution и относительного пути проекта
/// </summary>
/// <param name="solutionDirectory">Путь к директории solution файла</param>
/// <param name="projectPath">Относительный путь к .csproj файлу</param>
/// <returns>Полный путь к директории проекта</returns>
/// <example>
/// GetProjectDirectory(@"C:\Solution", @"MyProject\MyProject.csproj") 
/// // возвращает: @"C:\Solution\MyProject"
/// </example>
static string GetProjectDirectory(string solutionDirectory, string projectPath)
{
    var projectDir = Path.GetDirectoryName(projectPath);
    return string.IsNullOrEmpty(projectDir) 
        ? solutionDirectory 
        : Path.Combine(solutionDirectory, projectDir);
}

/// <summary>
/// Извлекает все уникальные пространства имен из C# файлов в указанной директории проекта
/// </summary>
/// <param name="projectDirectory">Путь к директории проекта для сканирования</param>
/// <returns>HashSet с уникальными пространствами имен, найденными в проекте</returns>
/// <example>
/// GetProjectNamespaces(@"C:\MyProject") 
/// // возвращает: { "MyProject", "MyProject.Services", "MyProject.Models" }
/// </example>
static HashSet<string> GetProjectNamespaces(string projectDirectory)
{
    var namespaces = new HashSet<string>();
    var namespaceRegex = new Regex(@"namespace\s+([A-Za-z_][A-Za-z0-9_.]*)", RegexOptions.Compiled);
    
    var csFiles = Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
        .Where(file => !ShouldExcludeFile(Path.GetRelativePath(projectDirectory, file)))
        .ToList();

    foreach (var file in csFiles)
    {
        try
        {
            var content = File.ReadAllText(file);
            var matches = namespaceRegex.Matches(content);
            foreach (Match match in matches)
            {
                namespaces.Add(match.Groups[1].Value);
            }
        }
        catch
        {
            // Игнорируем ошибки чтения файла
        }
    }

    return namespaces;
}

/// <summary>
/// Проверяет, следует ли исключить файл из обработки на основе его относительного пути
/// </summary>
/// <param name="relativePath">Относительный путь к файлу для проверки</param>
/// <returns>true если файл нужно исключить, false если обрабатывать</returns>
/// <example>
/// ShouldExcludeFile(@"bin\Debug\MyApp.dll") // возвращает: true
/// ShouldExcludeFile(@"Services\MyService.cs") // возвращает: false
/// </example>
static bool ShouldExcludeFile(string relativePath)
{
    var excludedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".vs", ".vscode", "packages", "TestResults", ".git", ".idea"
    };

    return relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Any(segment => excludedDirectories.Contains(segment));
}

/// <summary>
/// Отображает пронумерованный список проектов с их путями и пространствами имен в консоли
/// </summary>
/// <param name="projects">Список проектов с извлеченными пространствами имен</param>
/// <example>
/// DisplayProjectsAndNamespaces(projectsList)
/// // Выводит:
/// // 1. Проект: MyProject
/// //    Путь: MyProject\MyProject.csproj
/// //    Пространства имен (2):
/// //      - MyProject
/// //      - MyProject.Services
/// </example>
static void DisplayProjectsAndNamespaces(List<ProjectWithNamespace> projects)
{
    for (int i = 0; i < projects.Count; i++)
    {
        var project = projects[i];
        Console.WriteLine($"{i + 1}. Проект: {project.ProjectInfo.Name}");
        Console.WriteLine($"   Путь: {project.ProjectInfo.Path}");
        
        if (project.Namespaces.Any())
        {
            Console.WriteLine($"   Пространства имен ({project.Namespaces.Count}):");
            foreach (var ns in project.Namespaces.OrderBy(x => x))
            {
                Console.WriteLine($"     - {ns}");
            }
        }
        else
        {
            Console.WriteLine("   Пространства имен не найдены");
        }
        Console.WriteLine();
    }
}

/// <summary>
/// Позволяет пользователю интерактивно выбрать проекты для переименования из списка
/// </summary>
/// <param name="projects">Список всех доступных проектов</param>
/// <returns>Список выбранных проектов для обработки</returns>
/// <example>
/// SelectProjectsForEditing(allProjects)
/// // Пользователь вводит: "1,3,5"
/// // возвращает: проекты с индексами 0, 2, 4 из исходного списка
/// // 
/// // Пользователь вводит: "all" 
/// // возвращает: все проекты из списка
/// </example>
static List<ProjectWithNamespace> SelectProjectsForEditing(List<ProjectWithNamespace> projects)
{
    Console.WriteLine("Выберите проекты для редактирования:");
    Console.WriteLine("Введите номера проектов через запятую (например: 1,3,5) или 'all' для всех проектов, или Enter для выхода:");
    
    var input = Console.ReadLine()?.Trim();
    
    if (string.IsNullOrEmpty(input))
        return new List<ProjectWithNamespace>();
    
    if (input.Equals("all", StringComparison.OrdinalIgnoreCase))
        return projects;
    
    var selectedIndices = new List<int>();
    var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries);
    
    foreach (var part in parts)
    {
        if (int.TryParse(part.Trim(), out var index) && index >= 1 && index <= projects.Count)
        {
            selectedIndices.Add(index - 1);
        }
    }
    
    return selectedIndices.Select(i => projects[i]).ToList();
}

/// <summary>
/// Обрабатывает выбранные проекты, предлагая два режима переименования для каждого проекта
/// </summary>
/// <param name="selectedProjects">Список проектов для обработки</param>
/// <param name="solutionParser">Экземпляр парсера решения для выполнения операций</param>
/// <param name="solutionDirectory">Путь к директории решения</param>
/// <example>
/// ProcessSelectedProjects(selectedProjects, parser, @"C:\MySolution")
/// // Для каждого проекта предлагает:
/// // 1. Переименовать только в solution файле
/// // 2. Переименовать проект полностью (папка, .csproj, пути в .sln)
/// </example>
static void ProcessSelectedProjects(List<ProjectWithNamespace> selectedProjects, SolutionParser solutionParser, string solutionDirectory)
{
    foreach (var projectWithNamespace in selectedProjects)
    {
        Console.WriteLine($"\nРедактирование проекта: {projectWithNamespace.ProjectInfo.Name}");
        Console.WriteLine($"Текущее имя: {projectWithNamespace.ProjectInfo.Name}");
        Console.WriteLine($"Текущий путь: {projectWithNamespace.ProjectInfo.Path}");
        Console.Write("Введите новое имя (или Enter для пропуска): ");
        
        var newName = Console.ReadLine()?.Trim();
        
        if (!string.IsNullOrEmpty(newName) && newName != projectWithNamespace.ProjectInfo.Name)
        {
            Console.WriteLine("\nВыберите действия:");
            Console.WriteLine("1. Переименовать только в solution файле");
            Console.WriteLine("2. Переименовать проект полностью (папка, .csproj, пути в .sln)");
            Console.Write("Введите номер (1 или 2): ");
            
            var choice = Console.ReadLine()?.Trim();
            
            if (choice == "2")
            {
                var renameResult = solutionParser.RenameProjectWithFiles(projectWithNamespace.ProjectInfo.Name, newName, solutionDirectory);
                
                if (renameResult.Success)
                {
                    Console.WriteLine("  ✓ Проект полностью переименован:");
                    if (renameResult.DirectoryRenamed)
                        Console.WriteLine($"    ✓ Папка: {Path.GetFileName(renameResult.OldDirectoryPath)} → {Path.GetFileName(renameResult.NewDirectoryPath)}");
                    if (renameResult.CsprojRenamed)
                        Console.WriteLine($"    ✓ .csproj файл переименован");
                    if (renameResult.SolutionUpdated)
                        Console.WriteLine($"    ✓ Пути в .sln обновлены");
                    
                    if (renameResult.NamespaceChanges?.HasChanges == true)
                    {
                        Console.WriteLine($"    ✓ Обработано файлов: {renameResult.NamespaceChanges.TotalFilesProcessed}");
                        Console.WriteLine($"    ✓ Изменено пространств имен: {renameResult.NamespaceChanges.NamespacesModified}");
                        Console.WriteLine($"    ✓ Обновлено файлов проекта: {renameResult.NamespaceChanges.ProjectFilesModified}");
                    }
                }
                else
                {
                    Console.WriteLine($"  ✗ Ошибка: {renameResult.ErrorMessage}");
                }
            }
            else if (choice == "1")
            {
                var changed = solutionParser.ChangeProjectName(projectWithNamespace.ProjectInfo.Name, newName);
                if (changed)
                {
                    Console.WriteLine("  ✓ Имя проекта изменено в файле решения");
                }
                else
                {
                    Console.WriteLine("  - Изменений в файле решения не требуется");
                }
                
                var renamer = new ProjectRenamer();
                var result = renamer.RenameProjectClasses(projectWithNamespace.ProjectInfo.Name, newName, projectWithNamespace.Directory);
                
                if (result.HasChanges)
                {
                    Console.WriteLine($"  ✓ Обработано файлов: {result.TotalFilesProcessed}");
                    Console.WriteLine($"  ✓ Изменено пространств имен: {result.NamespacesModified}");
                    Console.WriteLine($"  ✓ Обновлено файлов проекта: {result.ProjectFilesModified}");
                }
            }
            else
            {
                Console.WriteLine("  - Некорректный выбор, проект пропущен");
            }
        }
        else
        {
            Console.WriteLine("  - Пропущено");
        }
    }
    
    solutionParser.SaveSolution();
}

/// <summary>
/// Структура, связывающая информацию о проекте с найденными в нем пространствами имен и путем к директории
/// </summary>
/// <param name="ProjectInfo">Информация о проекте из .sln файла</param>
/// <param name="Directory">Полный путь к директории проекта на диске</param>
/// <param name="Namespaces">Коллекция уникальных пространств имен, найденных в C# файлах проекта</param>
record ProjectWithNamespace(ProjectInfo ProjectInfo, string Directory, HashSet<string> Namespaces);