using CsSolutionRenamer;

var solutionPath = @"C:\Users\UnderKo\RiderProjects\Robokassa";

var solutionFiles = Directory.GetFiles(solutionPath)
    .Where(file => file.EndsWith(".sln") && !Path.GetFileName(file).StartsWith("rep_"))
    .ToList();

if (!solutionFiles.Any())
    return;

var solutionParser = new SolutionParser();
solutionParser.LoadSolution(solutionFiles.First());

var projects = solutionParser.GetProjects();
var solutionDirectory = Path.GetDirectoryName(solutionFiles.First());

var projectData = projects
    .Select(project => new ProjectData(
        project,
        GetProjectDirectory(solutionDirectory, project.Path),
        project.Name + "22"))
    .Where(p => Directory.Exists(p.Directory))
    .ToList();

var processedProjects = projectData
    .Select(p => new ProcessedProject(
        p.Project,
        p.NewName,
        ProcessProject(p.Project, p.NewName, p.Directory)))
    .Where(p => p.Result.HasChanges)
    .ToList();

if (processedProjects.Any())
{
    processedProjects.ForEach(p => solutionParser.ChangeProjectName(p.OriginalProject.Name, p.NewName));
    solutionParser.SaveSolution();
}

static string GetProjectDirectory(string solutionDirectory, string projectPath)
{
    var projectDir = Path.GetDirectoryName(projectPath);
    return string.IsNullOrEmpty(projectDir) 
        ? solutionDirectory 
        : Path.Combine(solutionDirectory, projectDir);
}

static RenameResult ProcessProject(ProjectInfo project, string newProjectName, string projectDirectory)
{
    var renamer = new ProjectRenamer();
    var result = renamer.RenameProjectClasses(project.Name, newProjectName, projectDirectory);
    return result;
}

record ProjectData(ProjectInfo Project, string Directory, string NewName);

record ProcessedProject(ProjectInfo OriginalProject, string NewName, RenameResult Result);