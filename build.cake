#addin nuget:?package=Cake.Core&version=5.0.0&loaddependencies=true
#addin nuget:?package=Cake.Common&version=5.0.0&loaddependencies=true

// Configuration
var testPackageName = "KlusterKite.Core";
var buildDir = MakeAbsolute(Directory("./build"));
var packageDir = MakeAbsolute(Directory("./packageOut"));
var packagePushDir = MakeAbsolute(Directory("./packagePush"));
var packageThirdPartyDir = MakeAbsolute(Directory("./packageThirdPartyDir"));
var version = EnvironmentVariable("version") ?? "0.0.0-local";

// Task: Clean
Task("Clean")
    .Does(() =>
{
    Information("Cleaning directories...");
    CleanDirectory(buildDir);
    CleanDirectory(packageDir);
    CleanDirectory(packagePushDir);
    CleanDirectory(packageThirdPartyDir);
});

// Task: SetVersion
Task("SetVersion")
    .IsDependentOn("Clean")
    .Does(() =>
{
    Information("Setting version...");
    // Logic to set version based on the latest NuGet version
    version = "1.0.0"; // Example version
    Information($"Version set to: {version}");
});

// Task: PrepareSources
Task("PrepareSources")
    .IsDependentOn("Clean")
    .Does(() =>
{
    Information("Preparing sources...");
    var sourcesDir = System.IO.Path.Combine(buildDir.FullPath, "src");
    Information($"Sources directory: {sourcesDir}");

    try
    {
        var currentDirectory = MakeAbsolute(Directory("./")).FullPath;
        Information($"Current working directory: {currentDirectory}");

        if (string.IsNullOrEmpty(currentDirectory) || !DirectoryExists(currentDirectory))
        {
            throw new Exception("Current working directory does not exist or is inaccessible.");
        }

        var directories = GetDirectories(currentDirectory);
        Information($"Found directories: {directories.Count}");
        foreach (var dir in directories)
        {
            Information($"Directory: {dir.FullPath}");
        }
    }
    catch (Exception ex)
    {
        Error($"Error retrieving directories. Exception: {ex.Message}");
    }
});

// Task: Build
Task("Build")
    .IsDependentOn("PrepareSources")
    .Does(() =>
{
    Information("Building projects...");
    var slnFiles = GetFiles("./KlusterKite.sln");
    foreach (var sln in slnFiles)
    {
        MSBuild(sln, new MSBuildSettings
        {
            Configuration = "Release",
            Verbosity = Verbosity.Minimal
        });
    }
});

// Task: Nuget
Task("Nuget")
    .IsDependentOn("Build")
    .Does(() =>
{
    Information("Packing NuGet packages...");
    // Logic to pack NuGet packages
});

// Task: CleanDockerImages
Task("CleanDockerImages")
    .Does(() =>
{
    Information("Cleaning Docker images...");
    // Logic to clean unnamed Docker images
});

// Task: Tests
Task("Tests")
    .IsDependentOn("Build")
    .Does(() =>
{
    Information("Running tests...");
    var testProjects = GetFiles("**/*.Tests.csproj");
    foreach (var project in testProjects)
    {
        DotNetTest(project.FullPath, new DotNetTestSettings
        {
            Configuration = "Release"
        });
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