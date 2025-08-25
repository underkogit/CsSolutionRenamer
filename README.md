# C# Solution Renamer

Интерактивный инструмент для переименования проектов в Visual Studio Solution с автоматическим обновлением namespace'ов, папок, файлов и путей.

## 🚀 Возможности

- **Интерактивное отображение** проектов и их namespace'ов
- **Два режима переименования:**
  - Только имя в .sln файле
  - Полное переименование (папка + .csproj + пути в .sln)
- **Автоматическое переименование:**
  - Папок проектов
  - .csproj файлов
  - Namespace'ов в C# файлах
  - AssemblyName и RootNamespace в .csproj
  - Путей в .sln файле
- **Переименование solution файла**
- **Детальная отчетность** о всех изменениях
- **Исключение системных папок** (bin, obj, .vs, etc.)

## 📋 Требования

- .NET 6.0 или выше
- Windows (тестировалось на Windows с WSL2)

## 🛠️ Установка и запуск

1. Клонируйте репозиторий:
```bash
git clone <repository-url>
cd CsSolutionRenamer
```

2. Соберите проект:
```bash
dotnet build
```

3. Запустите приложение:
```bash
dotnet run --project CsSolutionRenamer
```

## 📖 Использование

### Быстрый старт

1. **Запустите приложение** - увидите список всех проектов с их namespace'ами
2. **Выберите проекты** для переименования (по номерам через запятую или 'all')
3. **Выберите режим переименования** для каждого проекта
4. **Опционально переименуйте** сам .sln файл

### Пример использования

```
=== C# Solution Renamer ===

Найдено проектов: 2

1. Проект: Robokassa.Library
   Путь: Robokassa.Library\Robokassa.Library.csproj
   Пространства имен (3):
     - Robokassa.Library
     - Robokassa.Library.Models
     - Robokassa.Library.Services

2. Проект: Robokassa.Tests
   Путь: Tests\Robokassa.Tests.csproj
   Пространства имен (1):
     - Robokassa.Tests

Выберите проекты для редактирования:
Введите номера проектов через запятую (например: 1,3,5) или 'all' для всех проектов, или Enter для выхода:
> 1

Редактирование проекта: Robokassa.Library
Текущее имя: Robokassa.Library
Текущий путь: Robokassa.Library\Robokassa.Library.csproj
Введите новое имя (или Enter для пропуска): PaymentLibrary

Выберите действия:
1. Переименовать только в solution файле
2. Переименовать проект полностью (папка, .csproj, пути в .sln)
Введите номер (1 или 2): 2

  ✓ Проект полностью переименован:
    ✓ Папка: Robokassa.Library → PaymentLibrary
    ✓ .csproj файл переименован
    ✓ Пути в .sln обновлены
    ✓ Обработано файлов: 15
    ✓ Изменено пространств имен: 3
    ✓ Обновлено файлов проекта: 1

Хотите переименовать файл решения? (y/n): y
Введите новое имя для файла решения (без расширения): PaymentSolution
✓ Файл решения переименован в: PaymentSolution.sln

Изменения сохранены.
```

## 🏗️ Архитектура

### Основные модули

#### 📁 Program.cs - Главный модуль
- Пользовательский интерфейс
- Отображение проектов и namespace'ов
- Интерактивный выбор проектов
- Обработка пользовательского ввода

**Ключевые функции:**
```csharp
// Извлекает namespace'ы из C# файлов
static HashSet<string> GetProjectNamespaces(string projectDirectory)

// Отображает проекты с их namespace'ами
static void DisplayProjectsAndNamespaces(List<ProjectWithNamespace> projects)

// Интерактивный выбор проектов
static List<ProjectWithNamespace> SelectProjectsForEditing(List<ProjectWithNamespace> projects)
```

#### 📁 SolutionParser.cs - Парсер .sln файлов
- Загрузка и парсинг .sln файлов
- Извлечение информации о проектах
- Переименование проектов и путей
- Комплексное переименование с файлами

**Ключевые функции:**
```csharp
// Загружает .sln файл
public void LoadSolution(string filePath)

// Получает список проектов
public List<ProjectInfo> GetProjects()

// Полное переименование проекта
public RenameProjectResult RenameProjectWithFiles(string oldName, string newName, string solutionDirectory)

// Переименование .sln файла
public void RenameSolutionFile(string newSolutionName)
```

#### 📁 ProjectRenamer.cs - Переименование содержимого
- Переименование namespace'ов в C# файлах
- Поиск классов с именем проекта
- Обновление .csproj файлов
- Исключение системных папок

**Ключевые функции:**
```csharp
// Комплексное переименование содержимого проекта
public RenameResult RenameProjectClasses(string oldProjectName, string newProjectName, string projectPath)
```

### Структуры данных

#### ProjectInfo
```csharp
public class ProjectInfo
{
    public string ProjectTypeGuid { get; set; }  // GUID типа проекта
    public string Name { get; set; }             // Имя проекта
    public string Path { get; set; }             // Путь к .csproj
    public string ProjectGuid { get; set; }      // Уникальный GUID
}
```

#### RenameProjectResult
```csharp
public class RenameProjectResult
{
    public bool Success { get; set; }                    // Успех операции
    public bool DirectoryRenamed { get; set; }           // Папка переименована
    public bool CsprojRenamed { get; set; }              // .csproj переименован
    public bool SolutionUpdated { get; set; }            // .sln обновлен
    public RenameResult NamespaceChanges { get; set; }   // Изменения namespace'ов
    public string ErrorMessage { get; set; }             // Ошибки
}
```

#### RenameResult
```csharp
public class RenameResult
{
    public int TotalFilesProcessed { get; set; }     // Обработано файлов
    public int ClassesFound { get; set; }            // Найдено классов
    public int NamespacesModified { get; set; }      // Изменено namespace'ов
    public int ProjectFilesModified { get; set; }    // Обновлено .csproj
    public bool HasChanges { get; }                  // Есть изменения
}
```

## 🔧 Настройка

### Изменение пути к solution

Отредактируйте путь в `Program.cs`:
```csharp
var solutionPath = @"C:\Path\To\Your\Solution";
```

### Исключаемые директории

Список системных папок, которые исключаются из обработки:
```csharp
var excludedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "bin", "obj", ".vs", ".vscode", "packages", "TestResults", ".git", ".idea"
};
```

## ⚠️ Важные особенности

### Безопасность
- **Создает резервные копии** не требуется - изменения применяются напрямую
- **Атомарные операции** - при ошибке выполняется откат
- **Валидация входных данных** - проверка существования файлов и папок

### Ограничения
- Работает только с C# проектами (.csproj)
- Не поддерживает VB.NET и F# проекты
- Требует права записи в директорию solution'а

### Порядок операций при полном переименовании
1. **Переименование namespace'ов** в C# файлах (пока файлы в старой папке)
2. **Переименование папки** проекта
3. **Переименование .csproj** файла
4. **Обновление путей** в .sln файле

## 🐛 Troubleshooting

### Проблема: Namespace'ы не переименовываются
**Решение:** Убедитесь что проект содержит namespace'ы, начинающиеся с имени проекта

### Проблема: Ошибка доступа к файлу
**Решение:** 
- Закройте Visual Studio
- Проверьте права доступа к папке
- Убедитесь что файлы не заблокированы другими процессами

### Проблема: Папка уже существует
**Решение:** Переименуйте или удалите существующую папку с целевым именем

## 📝 Changelog

### v1.0
- ✅ Базовое переименование проектов в .sln
- ✅ Переименование namespace'ов
- ✅ Интерактивный интерфейс
- ✅ Два режима переименования
- ✅ Переименование .sln файлов
- ✅ Полная документация
- ✅ Исправлена проблема с rep_ файлами

## 🤝 Участие в разработке

1. Форкните репозиторий
2. Создайте feature ветку (`git checkout -b feature/amazing-feature`)
3. Зафиксируйте изменения (`git commit -m 'Add amazing feature'`)
4. Отправьте в ветку (`git push origin feature/amazing-feature`)
5. Откройте Pull Request

## 📄 Лицензия

Этот проект распространяется под лицензией MIT. См. файл `LICENSE` для деталей.

## 👨‍💻 Автор

UnderKo - разработка C# Solution Renamer

## 🙏 Благодарности

- Microsoft за .NET Framework
- Сообщество разработчиков C# за вдохновение
- Claude Code за помощь в документации