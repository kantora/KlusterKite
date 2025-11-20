#addin nuget:?package=Cake.Core&version=5.0.0&loaddependencies=true
#addin nuget:?package=Cake.Common&version=5.0.0&loaddependencies=true

// Configuration
var testPackageName = "KlusterKite.Core";
var rootDir = MakeAbsolute(Directory("./")).FullPath;
var tempDir = System.IO.Path.Combine(rootDir, "temp"); 
var buildDir = System.IO.Path.Combine(tempDir, "build");
var packageDir = System.IO.Path.Combine(tempDir, "packageOut");
var packagePushDir = System.IO.Path.Combine(tempDir, "packagePush");
var packageThirdPartyDir = System.IO.Path.Combine(tempDir, "packageThirdPartyDir");
var version = EnvironmentVariable("version") ?? "0.0.0-local";

// Task: Clean
Task("Clean")
    .Does(() =>
{
    Information("Cleaning build directory...");
    CleanDirectory(buildDir);
});

// Task: SetVersion
Task("SetVersion")
    .IsDependentOn("Clean")
    .Does(() =>
{
    Information("Setting version...");

    // Retrieve the latest NuGet version of the package
    var nugetServerUrl = "http://docker:81";
    var latestVersion = GetLatestNuGetVersion(nugetServerUrl, testPackageName);

    if (string.IsNullOrEmpty(latestVersion))
    {
        Information("Repository is empty");
        version = "0.0.0-local";
    }
    else
    {
        Information($"Current version is {latestVersion}");
        version = IncrementPatchVersion(latestVersion) + "-local";
    }

    packageDir = packagePushDir;
    Information($"New version is {version}");
});

// Helper methods
string GetLatestNuGetVersion(string serverUrl, string packageName)
{
    Information($"Fetching latest NuGet version for package {packageName} from {serverUrl}...");

    // Placeholder implementation replaced with a valid NuGet query
    var processSettings = new ProcessSettings
    {
        Arguments = $"list {packageName} -Source {serverUrl}",
        RedirectStandardOutput = true
    };

    IEnumerable<string> output;
    StartProcess("nuget", processSettings, out output);

    var latestVersion = output.FirstOrDefault(line => line.StartsWith(packageName))?.Split(' ').LastOrDefault();

    if (string.IsNullOrEmpty(latestVersion))
    {
        Information("No version found on the NuGet server.");
        return null;
    }

    Information($"Latest version found: {latestVersion}");
    return latestVersion;
}

string IncrementPatchVersion(string version)
{
    var versionParts = version.Split('.');
    if (versionParts.Length < 3)
    {
        throw new Exception("Invalid version format");
    }

    var patch = int.Parse(versionParts[2]) + 1;
    return $"{versionParts[0]}.{versionParts[1]}.{patch}";
}

// Task: PrepareSources
Task("PrepareSources")
    .IsDependentOn("Clean")
    .Does(() =>
{
    Information("Creating a sources copy in temporary directory...");
    var sourcesDir = System.IO.Path.Combine(buildDir, "src");
    Information($"Sources directory: {sourcesDir}");
    CleanDirectory(sourcesDir);

    try
    {
        Information("Fetching all .csproj files...");
        var csprojFiles = GetFiles(System.IO.Path.Combine(rootDir, "**/*.csproj"));

        if (csprojFiles == null || !csprojFiles.Any())
        {
            Information("No .csproj files found.");
            return;
        }

        foreach (var file in csprojFiles)
        {
            var projectDir = System.IO.Path.GetDirectoryName(file.FullPath);
            if (string.IsNullOrEmpty(projectDir))
            {
                continue;
            }

            Information($"Processing project directory: {projectDir} due to {file.FullPath}");

            CleanDirectory(System.IO.Path.Combine(projectDir, "bin"));

            var relativePath = System.IO.Path.GetRelativePath(rootDir, projectDir);
            var destinationDir = System.IO.Path.Combine(sourcesDir, relativePath);
            if (string.IsNullOrEmpty(destinationDir))
            {
                continue;
            }

            try
            {
                CopyDirectory(projectDir, destinationDir);
            }
            catch (Exception ex)
            {
                Error($"Error copying directory {projectDir}: {ex.Message}");
            }
        }

        var slnFiles = GetFiles(System.IO.Path.Combine(rootDir, "*.sln"));
        if (slnFiles != null && slnFiles.Any())
        {
            foreach (var file in slnFiles)
            {
                CopyFile(file.FullPath, System.IO.Path.Combine(sourcesDir, System.IO.Path.GetFileName(file.FullPath)));
            }
        }

        var fsxFiles = GetFiles(System.IO.Path.Combine(rootDir, "*.fsx"));
        if (fsxFiles != null && fsxFiles.Any())
        {
            foreach (var file in fsxFiles)
            {
                CopyFile(file.FullPath, System.IO.Path.Combine(sourcesDir, System.IO.Path.GetFileName(file.FullPath)));
            }
        }

        var propsFiles = GetFiles(System.IO.Path.Combine(rootDir, "*.props"));
        if (propsFiles != null && propsFiles.Any())
        {
            foreach (var file in propsFiles)
            {
                CopyFile(file.FullPath, System.IO.Path.Combine(sourcesDir, System.IO.Path.GetFileName(file.FullPath)));
            }
        }

        var projects = GetFiles(System.IO.Path.Combine(sourcesDir, "**/*.csproj"));
        if (projects != null && projects.Any())
        {
            foreach (var file in projects)
            {
                var projectDir = System.IO.Path.GetDirectoryName(file.FullPath);
                if (string.IsNullOrEmpty(projectDir))
                {
                    continue;
                }

                CleanDirectory(System.IO.Path.Combine(projectDir, "obj"));

                try
                {
                    var content = System.IO.File.ReadAllText(file.FullPath);
                    content = System.Text.RegularExpressions.Regex.Replace(content, "<Version>(.*)</Version>", $"<Version>{version}</Version>");
                    System.IO.File.WriteAllText(file.FullPath, content);
                }
                catch (Exception ex)
                {
                    Error($"Error replacing <Version> tag in file {file.FullPath}: {ex.Message}");
                }
            }
        }
    }
    catch (Exception ex)
    {
        Error($"Error during PrepareSources: {ex.Message}");
    }
});

// Task: Build
Task("Build")
    .IsDependentOn("PrepareSources")
    .Does(() =>
{
    Information("Building projects...");
    var sourcesDir = System.IO.Path.Combine(buildDir, "src");
    var slnFiles = GetFiles(System.IO.Path.Combine(sourcesDir, "*.sln"));

    foreach (var sln in slnFiles)
    {
        Information($"Building solution: {sln.FullPath}");
        MSBuild(sln.FullPath, settings =>
        {
            settings.SetVerbosity(Verbosity.Minimal);
            settings.WithTarget("Restore");
            settings.WithTarget("Build");
            settings.WithProperty("Configuration", "Release");
            settings.WithProperty("Optimize", "True");
            settings.WithProperty("DebugSymbols", "True");
        });
    }
});

// Task: Build
Task("BuildDebug")
    .IsDependentOn("PrepareSources")
    .Does(() =>
{
    Information("Building projects...");
    var sourcesDir = System.IO.Path.Combine(buildDir, "src");
    var slnFiles = GetFiles(System.IO.Path.Combine(sourcesDir, "*.sln"));

    foreach (var sln in slnFiles)
    {
        Information($"Building solution: {sln.FullPath}");
        MSBuild(sln.FullPath, settings =>
        {
            settings.SetVerbosity(Verbosity.Minimal);
            settings.WithTarget("Restore");
            settings.WithTarget("Build");
            settings.WithProperty("Configuration", "Debug");
            settings.WithProperty("Optimize", "False");
            settings.WithProperty("DebugSymbols", "True");
        });
    }
});

// Task: Nuget
Task("Nuget")
    .IsDependentOn("Build")
    .Does(() =>
{
    Information("Packing NuGet packages...");
    var sourcesDir = System.IO.Path.Combine(buildDir, "src");

    // Pack NuGet packages
    var slnFiles = GetFiles(System.IO.Path.Combine(sourcesDir, "*.sln"));
    foreach (var sln in slnFiles)
    {
        Information($"Packing solution: {sln.FullPath}");
        MSBuild(sln.FullPath, settings =>
        {
            settings.SetVerbosity(Verbosity.Minimal);
            settings.WithTarget("Pack");
            settings.WithProperty("Configuration", "Release");
            settings.WithProperty("Optimize", "True");
            settings.WithProperty("DebugSymbols", "True");
        });
    }

    // Clean package directory
    Information("Cleaning package directory...");
    CleanDirectory(packageDir);

    // Filter and copy .nupkg files
    Information("Filtering and copying .nupkg files...");
    var testProjects = GetFiles(System.IO.Path.Combine(sourcesDir, "**/*.csproj"))
        .Where(file =>
        {
            var content = System.IO.File.ReadAllText(file.FullPath);
            return System.Text.RegularExpressions.Regex.IsMatch(content, "<IsTest>true</IsTest>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        })
        .Select(file => System.IO.Path.GetFileNameWithoutExtension(file.FullPath))
        .ToList();

    var nupkgFiles = GetFiles(System.IO.Path.Combine(sourcesDir, "**/*.nupkg"))
        .Where(file => !testProjects.Contains(System.IO.Path.GetFileNameWithoutExtension(file.FullPath)));

    foreach (var file in nupkgFiles)
    {
        Information($"Copying package: {file.FullPath}");
        CopyFile(file.FullPath, System.IO.Path.Combine(packageDir, System.IO.Path.GetFileName(file.FullPath)));
    }
});

// Task: CleanDockerImages
Task("CleanDockerImages")
    .Does(() =>
{
    Information("Cleaning unnamed Docker images...");

    var processSettings = new ProcessSettings
    {
        Arguments = "images --format \"{{.Repository}} {{.ID}}\"",
        RedirectStandardOutput = true
    };

    IEnumerable<string> output;
    StartProcess("docker", processSettings, out output);

    foreach (var line in output)
    {
        var parts = line.Split(' ');
        if (parts.Length >= 2 && parts[0] == "<none>")
        {
            var imageId = parts[1];
            Information($"Removing unnamed Docker image: {imageId}");
            StartProcess("docker", new ProcessSettings
            {
                Arguments = $"rmi {imageId}"
            });
        }
    }
});

// Task: Tests
Task("Tests")
    .IsDependentOn("BuildDebug")
    .Does(() =>
{
    Information("Running tests...");
    var sourcesDir = System.IO.Path.Combine(buildDir, "src");
    var outputTests = System.IO.Path.Combine(buildDir, "tests");
    EnsureDirectoryExists(outputTests);
    CleanDirectory(outputTests);

    var testProjects = GetFiles(System.IO.Path.Combine(sourcesDir, "**/*.csproj"))
        .Where(file =>
        {
            var content = System.IO.File.ReadAllText(file.FullPath);
            return System.Text.RegularExpressions.Regex.IsMatch(content, "<IsTest>true</IsTest>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        });

    var failedProjects = new List<string>();

    foreach (var project in testProjects)
    {
        Information($"Running tests for project: {project.FullPath}");

        var result = StartProcess("dotnet", new ProcessSettings
        {
            Arguments = $"test {project.FullPath} --no-build --logger:trx;LogFileName={System.IO.Path.Combine(outputTests, System.IO.Path.GetFileNameWithoutExtension(project.FullPath) + ".trx")}",
            WorkingDirectory = System.IO.Path.GetDirectoryName(project.FullPath)
        });

        if (result != 0)
        {
            failedProjects.Add(project.FullPath);
        }
    }

    if (failedProjects.Any())
    {
        throw new Exception($"Tests failed for the following projects: {string.Join(", ", failedProjects)}");
    }
});

// Task: PushLocalPackages
Task("PushLocalPackages")
    .IsDependentOn("Nuget")
    .Does(() =>
{
    Information("Pushing local NuGet packages...");
    // Logic to push local NuGet packages
});

// Task: RestoreThirdPartyPackages
Task("RestoreThirdPartyPackages")
    .IsDependentOn("PrepareSources")
    .Does(() =>
{
    Information("Restoring third-party packages...");
    // Logic to restore third-party packages
});

// Task: PushThirdPartyPackages
Task("PushThirdPartyPackages")
    .IsDependentOn("RestoreThirdPartyPackages")
    .Does(() =>
{
    Information("Pushing third-party NuGet packages...");
    // Logic to push third-party NuGet packages
});

// Task: FinalPushAllPackages
Task("FinalPushAllPackages")
    .IsDependentOn("PushLocalPackages")
    .IsDependentOn("PushThirdPartyPackages")
    .Does(() =>
{
    Information("Finalizing push of all packages...");
    // Logic to finalize pushing all packages
});

// Default task
Task("Default");
    //.IsDependentOn("Build")
    //.IsDependentOn("Tests");

var target = Argument("target", "Build");
RunTarget(target);