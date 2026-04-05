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

    try
    {
        var processSettings = new ProcessSettings
        {
            Arguments = $"list {packageName} -Source {serverUrl}",
            RedirectStandardOutput = true
        };

        IEnumerable<string> output;
        StartProcess("nuget", processSettings, out output);

        var latestVersion = output
            .Where(line => line.StartsWith(packageName))
            .Select(line => line.Split(' ').LastOrDefault())
            .FirstOrDefault();

        if (string.IsNullOrEmpty(latestVersion))
        {
            Information("No version found on the NuGet server.");
            return null;
        }

        Information($"Latest version found: {latestVersion}");
        return latestVersion;
    }
    catch (Exception ex)
    {
        Error($"Error fetching latest NuGet version: {ex.Message}");
        return null;
    }
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

// Task: RestoreThirdPartyPackages
Task("RestoreThirdPartyPackages")
    .IsDependentOn("PrepareSources")
    .Does(() =>
{
    Information("Restoring third-party packages...");

    var sourcesDir = System.IO.Path.Combine(buildDir, "src");
    var thirdPartyPackagesDir = packageThirdPartyDir;
    EnsureDirectoryExists(thirdPartyPackagesDir);
    CleanDirectory(thirdPartyPackagesDir);

    var thirdPartyPackages = GetFiles(System.IO.Path.Combine(sourcesDir, "**/*.csproj"))
        .SelectMany(file =>
        {
            var content = System.IO.File.ReadAllText(file.FullPath);
            return System.Text.RegularExpressions.Regex.Matches(content, "<PackageReference Include=\"(.*?)\" Version=\"(.*?)\" />")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(match => new { Id = match.Groups[1].Value, Version = match.Groups[2].Value });
        })
        .Distinct()
        .ToList();

    foreach (var package in thirdPartyPackages)
    {
        Information($"Restoring package: {package.Id}, Version: {package.Version}");

        var result = StartProcess("dotnet", new ProcessSettings
        {
            Arguments = $"nuget install {package.Id} -Version {package.Version} -OutputDirectory {thirdPartyPackagesDir}",
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        if (result != 0)
        {
            throw new Exception($"Failed to restore package: {package.Id}, Version: {package.Version}");
        }
    }

    Information("All third-party packages restored successfully.");
});

// Task: PushThirdPartyPackages
Task("PushThirdPartyPackages")
    .IsDependentOn("RestoreThirdPartyPackages")
    .Does(() =>
{
    Information("Pushing third-party NuGet packages...");

    var nugetServerUrl = "http://docker:81"; // Replace with your NuGet server URL
    var apiKey = EnvironmentVariable("NUGET_API_KEY") ?? ""; // Replace with your API key or set it as an environment variable

    if (string.IsNullOrEmpty(apiKey))
    {
        throw new Exception("NuGet API key is not set. Please set the NUGET_API_KEY environment variable.");
    }

    var nupkgFiles = GetFiles(System.IO.Path.Combine(packageThirdPartyDir, "*.nupkg"));

    if (nupkgFiles == null || !nupkgFiles.Any())
    {
        Information("No third-party NuGet packages found to push.");
        return;
    }

    foreach (var file in nupkgFiles)
    {
        Information($"Pushing third-party package: {file.FullPath}");

        var result = StartProcess("dotnet", new ProcessSettings
        {
            Arguments = $"nuget push {file.FullPath} --source {nugetServerUrl} --api-key {apiKey}",
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        if (result != 0)
        {
            throw new Exception($"Failed to push third-party package: {file.FullPath}");
        }
    }

    Information("All third-party packages pushed successfully.");
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

    var nugetServerUrl = "http://docker:81"; // Replace with your NuGet server URL
    var apiKey = EnvironmentVariable("NUGET_API_KEY");

    if (string.IsNullOrEmpty(apiKey))
    {
        throw new Exception("NuGet API key is not set. Please set the NUGET_API_KEY environment variable.");
    }

    var nupkgFiles = GetFiles(System.IO.Path.Combine(packageDir, "*.nupkg"));

    if (nupkgFiles == null || !nupkgFiles.Any())
    {
        Information("No local NuGet packages found to push.");
        return;
    }

    foreach (var file in nupkgFiles)
    {
        Information($"Pushing local package: {file.FullPath}");

        var result = StartProcess("dotnet", new ProcessSettings
        {
            Arguments = $"nuget push {file.FullPath} --source {nugetServerUrl} --api-key {apiKey}",
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        if (result != 0)
        {
            throw new Exception($"Failed to push local package: {file.FullPath}");
        }
    }

    Information("All local packages pushed successfully.");
});

// Task: RePushLocalPackages
Task("RePushLocalPackages")
    .Does(() =>
{
    Information("Re-pushing local NuGet packages one by one...");

    var nugetServerUrl = "http://docker:81";
    var apiKey = EnvironmentVariable("NUGET_API_KEY");

    if (string.IsNullOrEmpty(apiKey))
    {
        throw new Exception("NuGet API key is not set. Please set the NUGET_API_KEY environment variable.");
    }

    var nupkgFiles = GetFiles(System.IO.Path.Combine(packagePushDir, "*.nupkg"));

    if (nupkgFiles == null || !nupkgFiles.Any())
    {
        Information("No local NuGet packages found to re-push.");
        return;
    }

    var failedFiles = new System.Collections.Generic.List<string>();

    foreach (var file in nupkgFiles)
    {
        Information($"Pushing local package: {file.FullPath}");

        var result = StartProcess("dotnet", new ProcessSettings
        {
            Arguments = $"nuget push {file.FullPath} --source {nugetServerUrl} --api-key {apiKey}",
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        if (result != 0)
        {
            Warning($"Failed to push package: {file.FullPath}. Continuing...");
            failedFiles.Add(file.FullPath);
        }
    }

    if (failedFiles.Any())
    {
        Warning($"The following packages failed to push: {string.Join(", ", failedFiles)}");
    }
    else
    {
        Information("All local packages pushed successfully.");
    }
});

// Task: RePushThirdPartyPackages
Task("RePushThirdPartyPackages")
    .Does(() =>
{
    Information("Re-pushing third-party NuGet packages one by one...");

    var nugetServerUrl = "http://docker:81";
    var apiKey = EnvironmentVariable("NUGET_API_KEY");

    if (string.IsNullOrEmpty(apiKey))
    {
        throw new Exception("NuGet API key is not set. Please set the NUGET_API_KEY environment variable.");
    }

    var nupkgFiles = GetFiles(System.IO.Path.Combine(packageThirdPartyDir, "*.nupkg"));

    if (nupkgFiles == null || !nupkgFiles.Any())
    {
        Information("No third-party NuGet packages found to re-push.");
        return;
    }

    var failedFiles = new System.Collections.Generic.List<string>();

    foreach (var file in nupkgFiles)
    {
        Information($"Pushing third-party package: {file.FullPath}");

        var result = StartProcess("dotnet", new ProcessSettings
        {
            Arguments = $"nuget push {file.FullPath} --source {nugetServerUrl} --api-key {apiKey}",
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        if (result != 0)
        {
            Warning($"Failed to push package: {file.FullPath}. Continuing...");
            failedFiles.Add(file.FullPath);
        }
    }

    if (failedFiles.Any())
    {
        Warning($"The following packages failed to push: {string.Join(", ", failedFiles)}");
    }
    else
    {
        Information("All third-party packages pushed successfully.");
    }
});

// Task: FinalBuild
Task("FinalBuild")
    .IsDependentOn("Build")
    .Does(() =>
{
    Information("FinalBuild complete.");
});

// Task: FinalPushLocalPackages
// Full pipeline: Clean -> SetVersion -> PrepareSources -> Build -> Nuget -> PushLocalPackages
// SetVersion runs before PrepareSources because it is declared first and both depend on Clean.
Task("FinalPushLocalPackages")
    .IsDependentOn("SetVersion")
    .IsDependentOn("PushLocalPackages")
    .Does(() =>
{
    Information("FinalPushLocalPackages complete.");
});

// Task: FinalPushThirdPartyPackages
Task("FinalPushThirdPartyPackages")
    .IsDependentOn("RestoreThirdPartyPackages")
    .IsDependentOn("PushThirdPartyPackages")
    .Does(() =>
{
    Information("FinalPushThirdPartyPackages complete.");
});

// Task: FinalPushAllPackages
Task("FinalPushAllPackages")
    .IsDependentOn("FinalPushLocalPackages")
    .IsDependentOn("FinalPushThirdPartyPackages")
    .Does(() =>
{
    Information("All local and third-party packages have been pushed successfully.");
});

// Task: DockerBase
Task("DockerBase")
    .Does(() =>
{
    Information("Building base Docker images...");

    var dockerImages = new[]
    {
        new { Name = "klusterkite/baseworker", Path = "Docker/KlusterKiteBaseWorkerNode" },
        new { Name = "klusterkite/baseweb", Path = "Docker/KlusterKiteBaseWebNode" },
        new { Name = "klusterkite/nuget", Path = "Docker/KlusterKiteNuget" },
        new { Name = "klusterkite/postgres", Path = "Docker/KlusterKitePostgres" },
        new { Name = "klusterkite/entry", Path = "Docker/KlusterKiteEntry" },
        new { Name = "klusterkite/vpn", Path = "Docker/KlusterKiteVpn" },
        new { Name = "klusterkite/elk", Path = "Docker/KlusterKiteELK" },
        new { Name = "klusterkite/redis", Path = "Docker/KlusterKite.Redis" }
    };

    foreach (var image in dockerImages)
    {
        Information($"Building Docker image: {image.Name} from path: {image.Path}");

        var result = StartProcess("docker", new ProcessSettings
        {
            Arguments = $"build -t {image.Name}:latest {image.Path}",
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        if (result != 0)
        {
            throw new Exception($"Failed to build Docker image: {image.Name}");
        }
    }

    Information("All base Docker images built successfully.");
});

// Task: DockerContainers
Task("DockerContainers")
    .IsDependentOn("PrepareSources")
    .Does(() =>
{
    Information("Building standard Docker images...");

    // Define projects and their output paths
    var projects = new[]
    {
        new { OutputPath = "./build/launcher", ProjectPath = "./build/src/KlusterKite.NodeManager/KlusterKite.NodeManager.Launcher/KlusterKite.NodeManager.Launcher.csproj" },
        new { OutputPath = "./build/seed", ProjectPath = "./KlusterKite.Core/KlusterKite.Core.Service/KlusterKite.Core.Service.csproj" },
        new { OutputPath = "./build/seeder", ProjectPath = "./KlusterKite.NodeManager/KlusterKite.NodeManager.Seeder.Launcher/KlusterKite.NodeManager.Seeder.Launcher.csproj" }
    };

    // Clean and build projects
    foreach (var project in projects)
    {
        Information($"Building project: {project.ProjectPath}");

        CleanDirectory(project.OutputPath);

        MSBuild(project.ProjectPath, settings =>
        {
            settings.SetVerbosity(Verbosity.Minimal);
            settings.WithTarget("Restore");
            settings.WithTarget("Publish");
            settings.WithProperty("Optimize", "True");
            settings.WithProperty("DebugSymbols", "True");
            settings.WithProperty("Configuration", "Release");
            settings.WithProperty("OutputPath", project.OutputPath);
        });
    }

    // Copy data for Docker images
    void CopyLauncherData(string path)
    {
        var fullPath = MakeAbsolute(Directory(path)).FullPath;
        var buildDir = System.IO.Path.Combine(fullPath, "build");
        var packageCacheDir = System.IO.Path.Combine(fullPath, "packageCache");

        CleanDirectory(buildDir);
        CleanDirectory(packageCacheDir);
        CopyDirectory("./build/launcherpublish", buildDir);
        CopyFile("./Docker/utils/launcher/start.sh", buildDir);
        CopyFile("./nuget.exe", buildDir);
    }

    CopyLauncherData("./Docker/KlusterKiteWorker");
    CopyLauncherData("./Docker/KlusterKitePublisher");

    // Build Docker images
    var dockerImages = new[]
    {
        new { Name = "klusterkite/seed", Path = "Docker/KlusterKiteSeed" },
        new { Name = "klusterkite/seeder", Path = "Docker/KlusterKiteSeeder" },
        new { Name = "klusterkite/worker", Path = "Docker/KlusterKiteWorker" },
        new { Name = "klusterkite/manager", Path = "Docker/KlusterKiteManager" },
        new { Name = "klusterkite/publisher", Path = "Docker/KlusterKitePublisher" }
    };

    foreach (var image in dockerImages)
    {
        Information($"Building Docker image: {image.Name} from path: {image.Path}");

        var result = StartProcess("docker", new ProcessSettings
        {
            Arguments = $"build -t {image.Name}:latest {image.Path}",
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        if (result != 0)
        {
            throw new Exception($"Failed to build Docker image: {image.Name}");
        }
    }

    // Build monitoring UI (React app) and its Docker image
    var webDir = MakeAbsolute(Directory("./Docker/KlusterKiteMonitoring/klusterkite-web")).FullPath;
    var envLocal = System.IO.Path.Combine(webDir, ".env-local");
    var envFile  = System.IO.Path.Combine(webDir, ".env");
    var envBuild = System.IO.Path.Combine(webDir, ".env-build");

    // Detect npm executable
    string npmExe;
    if (IsRunningOnWindows())
    {
        var nodePath = System.Environment.GetEnvironmentVariable("PATH")
            .Split(';')
            .FirstOrDefault(p => p.IndexOf("nodejs", StringComparison.OrdinalIgnoreCase) >= 0);
        npmExe = nodePath != null && System.IO.File.Exists(System.IO.Path.Combine(nodePath, "npm.cmd"))
            ? System.IO.Path.Combine(nodePath, "npm.cmd")
            : "npm.cmd";
    }
    else
    {
        var whichResult = StartProcess("which", new ProcessSettings
        {
            Arguments = "npm",
            RedirectStandardOutput = true
        });
        npmExe = whichResult == 0 ? "npm" : "/usr/bin/npm";
    }

    // Clear babel loader cache
    var babelCache = System.IO.Path.Combine(webDir, "node_modules", ".cache", "babel-loader");
    if (System.IO.Directory.Exists(babelCache))
        CleanDirectory(babelCache);

    // Swap env files so the build sees .env (from .env-local)
    if (System.IO.File.Exists(envLocal))
        System.IO.File.Move(envLocal, envFile);
    if (System.IO.File.Exists(envFile))
        System.IO.File.Move(envFile, envBuild);

    try
    {
        Information($"Running: {npmExe} install");
        var npmInstall = StartProcess(npmExe, new ProcessSettings
        {
            Arguments = "install",
            WorkingDirectory = webDir
        });
        if (npmInstall != 0)
            throw new Exception("npm install failed for klusterkite-web");

        Information($"Running: {npmExe} run build");
        var npmBuild = StartProcess(npmExe, new ProcessSettings
        {
            Arguments = "run build",
            WorkingDirectory = webDir
        });
        if (npmBuild != 0)
            throw new Exception("npm run build failed for klusterkite-web");

        var monitoringResult = StartProcess("docker", new ProcessSettings
        {
            Arguments = "build -t klusterkite/monitoring-ui:latest Docker/KlusterKiteMonitoring",
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });
        if (monitoringResult != 0)
            throw new Exception("Failed to build Docker image: klusterkite/monitoring-ui");
    }
    finally
    {
        // Always restore env files
        if (System.IO.File.Exists(envBuild))
            System.IO.File.Move(envBuild, envFile);
        if (System.IO.File.Exists(envFile))
            System.IO.File.Move(envFile, envLocal);
    }

    Information("All standard Docker images built successfully.");
});

// Task: FinalBuildDocker
Task("FinalBuildDocker")
    .IsDependentOn("DockerBase")
    .IsDependentOn("DockerContainers")
    .IsDependentOn("CleanDockerImages")
    .Does(() =>
{
    Information("Finalizing the build of all Docker images...");

    Information("All Docker images have been built and cleaned successfully.");
});

// Entry point
Task("Default").IsDependentOn("FinalBuild");
var target = Argument("target", "Default");
RunTarget(target);