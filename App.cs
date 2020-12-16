using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Duotify.EFCore.EntityPartialGenerator.Properties;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Duotify.EFCore.EntityPartialGenerator
{
    public class App : IHostedService
    {
        private int? _exitCode;

        private readonly ILogger<App> logger;
        private readonly IHostApplicationLifetime appLifetime;

        public App(ILogger<App> logger, IHostApplicationLifetime appLifetime)
        {
            this.logger = logger;
            this.appLifetime = appLifetime;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                logger.LogTrace(Resources.WorkerStarted(DateTimeOffset.Now));

                var runner = new CommandRunner("dotnet genmodelmetadatatype", "GenModelMetadataType Command Line Tools");

                runner.SubCommand("list", "show dbContext type list ", c =>
                {
                    c.Option("project", "project", "p", Resources.ProjectOptionDescription);
                    c.OnRun((namedArgs) =>
                    {
                        var assembly = GetAssembly(namedArgs.GetValueOrDefault("project"));

                        var dbContextNames = GetDbContextTypesFromAssembly(assembly).ToList().Select(type => GetFullName(type));

                        var sb = new StringBuilder();

                        foreach (var dbContextName in dbContextNames)
                        {
                            sb.AppendLine(dbContextName);
                        }

                        Reporter.WriteData(sb.ToString());

                        return 1;
                    });
                });

                runner.SubCommand("generate", "generate partial code ", c =>
                {
                    c.Option("output", "output-dir", "o", Resources.OutputOptionDescription);
                    c.Option("project", "project", "p", Resources.ProjectOptionDescription);
                    c.Option("context", "context", "c", Resources.ContextOptionDescription);
                    c.Option("force", "force", "f", Resources.ForceOptionDescription, true);
                    c.Option("verbose", "verbose", "v", Resources.ForceOptionDescription, true);
                    c.OnRun((namedArgs) =>
                    {
                        Reporter.IsVerbose = namedArgs.ContainsKey("verbose");

                        var assembly = GetAssembly(namedArgs.GetValueOrDefault("project"));

                        var types = GetEntityTypesFromAssembly(assembly, namedArgs.GetValueOrDefault("context"));

                        CreateFiles(types, namedArgs.GetValueOrDefault("output"), namedArgs.ContainsKey("force"));

                        return 1;
                    });
                });

                _exitCode = runner.Run(Environment.GetCommandLineArgs().Skip(1));
            }
            catch (CommandException ex)
            {
                Reporter.WriteError(ex.Message);
                _exitCode = 1;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, Resources.UnhandledException);
                _exitCode = 1;
            }
            finally
            {
                appLifetime.StopApplication();
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogTrace(Resources.WorkerStopped(DateTimeOffset.Now));

            Environment.ExitCode = _exitCode.GetValueOrDefault(-1);

            return Task.CompletedTask;
        }

        /// <summary>
        /// 取得 Assembly
        /// </summary>
        /// <param name="projectPath"></param>
        /// <returns></returns>
        private Assembly GetAssembly(string projectPath)
        {
            var project = GetAndBuildProject(projectPath);

            return GetAssemblyFromProject(project);
        }

        /// <summary>
        /// 取得專案資訊並建置專案
        /// </summary>
        /// <param name="projectPath"></param>
        /// <returns></returns>
        private Project GetAndBuildProject(string projectPath)
        {
            var projectFile = ResolveProject(projectPath);

            var project = Project.FromFile(projectFile, null);

            Reporter.WriteInformation(Resources.BuildStarted);
            project.Build();
            Reporter.WriteInformation(Resources.BuildSucceeded);

            return project;
        }

        /// <summary>
        /// 取得專案檔，若路徑中有零筆或多筆專案檔，則拋出例外
        /// </summary>
        /// <param name="projectPath"></param>
        /// <returns></returns>
        private static string ResolveProject(string projectPath)
        {
            var projects = GetProjectFiles(projectPath);

            return projects.Count switch
            {
                0 => throw new CommandException(projectPath != null
                    ? Resources.NoProjectInDirectory(projectPath)
                    : Resources.NoProject),
                > 1 => throw new CommandException(projectPath != null
                    ? Resources.MultipleProjectsInDirectory(projectPath)
                    : Resources.MultipleProjects),
                _ => projects[0],
            };
        }

        /// <summary>
        /// 取得 proj 檔案列表
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static List<string> GetProjectFiles(string path)
        {
            if (path == null)
            {
                path = Directory.GetCurrentDirectory();
            }
            else if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(Directory.GetCurrentDirectory(), path);
            }

            if (!Directory.Exists(path))
            {
                return new List<string> { path };
            }

            var projectFiles = Directory.EnumerateFiles(path, "*.*proj", SearchOption.TopDirectoryOnly)
                .Where(f => !string.Equals(Path.GetExtension(f), ".xproj", StringComparison.OrdinalIgnoreCase))
                .Take(2).ToList();

            return projectFiles;
        }

        /// <summary>
        /// 透過 project 取得 Assembly
        /// </summary>
        /// <param name="path"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        private static Assembly GetAssemblyFromProject(Project project)
        {
            var targetDir = Path.GetFullPath(Path.Combine(project.ProjectDir, project.OutputPath));

            string localPath = string.IsNullOrEmpty(targetDir)
                ? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                : targetDir;

            string assemblyFilePath = Path.Combine(localPath, project.TargetFileName);

            if (!File.Exists(assemblyFilePath))
            {
                throw new Exception(Resources.AssemblyFileNotFound(assemblyFilePath));
            }

            return Assembly.LoadFrom(assemblyFilePath);
        }

        /// <summary>
        /// 從 Assembly 取得所有 Entity 的 Type
        /// </summary>
        /// <param name="assembly"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        private IEnumerable<Type> GetEntityTypesFromAssembly(Assembly assembly, string context)
        {
            var dbContextTypes = GetDbContextTypesFromAssembly(assembly);

            if (!string.IsNullOrWhiteSpace(context))
            {
                dbContextTypes = dbContextTypes.Where(t => t.Name.Equals(context));
            }

            var dbContextType = dbContextTypes.FirstOrDefault();

            if (dbContextType == null)
            {
                throw new Exception(Resources.AssemblyNotContainDbContext);
            }

            return dbContextType.GetProperties()
                .Where(prop => CheckIfDbSetGenericType(prop.PropertyType))
                .Select(type => type.PropertyType.GetGenericArguments()[0]);
        }

        /// <summary>
        /// 新增檔案
        /// </summary>
        /// <param name="types"></param>
        /// <param name="output"></param>
        /// <param name="force"></param>
        private void CreateFiles(IEnumerable<Type> types, string output, bool force)
        {
            var outputDir = string.IsNullOrWhiteSpace(output)
                ? Directory.GetCurrentDirectory()
                : Path.Combine(Directory.GetCurrentDirectory(), output);

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            if (!force && CheckIfAnyFileExisted(types, outputDir, out string fileNames))
            {
                throw new CommandException(Resources.FileIsExisted(outputDir, fileNames));
            }

            var sb = new StringBuilder();

            foreach (var type in types)
            {
                var fileName = $"{type.Name}.Partial.cs";
                var fileContent = GeneratePartialCodeContent(type);
                var filePath = Path.Combine(outputDir, fileName);
                using StreamWriter sw = new StreamWriter(filePath);
                sw.Write(fileContent);
                sb.AppendLine($"{(force ? "Overwriting" : "Creating")} {filePath}");
            }
            Reporter.WriteVerbose(sb.ToString());
        }

        /// <summary>
        /// 檢查是否有已存在的檔案，若有則傳回 true，並組合相關字串
        /// </summary>
        /// <param name="types"></param>
        /// <param name="outputDir"></param>
        /// <param name="fileNames"></param>
        /// <returns></returns>
        private static bool CheckIfAnyFileExisted(IEnumerable<Type> types, string outputDir, out string fileNames)
        {
            var existFile = types.Select(type => $"{type.Name}.Partial.cs").Where(fileName => File.Exists(Path.Combine(outputDir, fileName)));
            fileNames = string.Join(", ", existFile) + ".";
            return existFile.Any();
        }

        /// <summary>
        /// 從 Assembly 取得 DbContext 的 Type
        /// </summary>
        /// <param name="assembly"></param>
        /// <returns></returns>
        private static IEnumerable<Type> GetDbContextTypesFromAssembly(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null && t.BaseType.FullName.Contains(Resources.DbContextFullName));
            }
        }

        /// <summary>
        /// 檢查是否為 DbSet 的泛型類別
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private bool CheckIfDbSetGenericType(Type type)
        {
            return type.IsGenericType && GetFullName(type).Contains("DbSet");
        }

        /// <summary>
        /// 取得 Type 的完整名稱
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private string GetFullName(Type type)
        {
            if (!type.IsGenericType) return type.Name;

            StringBuilder sb = new StringBuilder();

            sb.Append(type.Name.Substring(0, type.Name.LastIndexOf("`")));
            sb.Append(type.GetGenericArguments().Aggregate("<",
                delegate (string aggregate, Type type)
                {
                    return aggregate + (aggregate == "<" ? "" : ",") + GetFullName(type);
                }));
            sb.Append('>');

            return sb.ToString();
        }

        /// <summary>
        /// 產生 Partial Code 內容
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private string GeneratePartialCodeContent(Type type)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("using Microsoft.AspNetCore.Mvc;");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.ComponentModel.DataAnnotations;");
            sb.AppendLine();
            sb.AppendLine("#nullable disable");
            sb.AppendLine();
            sb.AppendLine($"namespace {type.Namespace}");
            sb.AppendLine("{");
            sb.AppendLine($"    [ModelMetadataType(typeof({type.Name}Metadata))]");
            sb.AppendLine($"    public partial class {type.Name}");
            sb.AppendLine("    {");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine($"    internal class {type.Name}Metadata");
            sb.AppendLine("    {");

            foreach (var prop in type.GetProperties().Where(t => !t.GetGetMethod().IsVirtual))
            {
                sb.AppendLine("        // [Required]");
                sb.AppendLine($"        public {GetFullName(prop.PropertyType)} {prop.Name} {{ get; set; }}");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}