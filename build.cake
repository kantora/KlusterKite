
// Configuration
var testPackageName = "KlusterKite.Core";
var rootDir = MakeAbsolute(Directory("./")).FullPath;
var tempDir = System.IO.Path.Combine(rootDir, "temp"); 
var buildDir = System.IO.Path.Combine(tempDir, "build");
var packageDir = System.IO.Path.Combine(tempDir, "packageOut");
var packagePushDir = System.IO.Path.Combine(tempDir, "packagePush");
var packageThirdPartyDir = System.IO.Path.Combine(tempDir, "packageThirdPartyDir");
var version = EnvironmentVariable("version") ?? "0.0.0-local";
var nugetServerUrl = EnvironmentVariable("NUGET_SERVER_URL") ?? "http://docker:81";
// For NuGet.org, the push endpoint (V3) differs from the version-query endpoint (V2)
var isPublicNuGet = nugetServerUrl.Contains("nuget.org");
var nugetQueryUrl = isPublicNuGet ? "https://www.nuget.org/api/v2" : nugetServerUrl;

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
    var latestVersion = GetLatestNuGetVersion(nugetQueryUrl, testPackageName);
    var suffix = isPublicNuGet ? "" : "-local";

    if (string.IsNullOrEmpty(latestVersion))
    {
        Information("Repository is empty");
        version = isPublicNuGet ? "0.1.0" : "0.0.0-local";
    }
    else
    {
        Information($"Current version is {latestVersion}");
        version = IncrementPatchVersion(latestVersion) + suffix;
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
        using var client = new System.Net.Http.HttpClient();
        var url = $"{serverUrl.TrimEnd('/')}/FindPackagesById()?id={packageName}&$orderby=Version+desc&$top=1";
        var response = client.GetAsync(url).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            Information($"NuGet server returned {response.StatusCode}");
            return null;
        }

        var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        var doc = System.Xml.Linq.XDocument.Parse(content);
        var ns = System.Xml.Linq.XNamespace.Get("http://schemas.microsoft.com/ado/2007/08/dataservices");
        var latestVersion = doc.Descendants(ns + "Version").FirstOrDefault()?.Value;

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
    // Strip pre-release suffix (e.g. "0.0.5-local" → "0.0.5") before parsing
    var baseVersion = version.Split('-')[0];
    var versionParts = baseVersion.Split('.');
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
// Ports the FAKE implementation directly:
//   1. Index all .nupkg files in the global NuGet cache.
//   2. Seed the set from PackageReference items in every .csproj under temp/build/src/.
//   3. BFS-expand via nuspec dependency groups (ALL groups, not just current TFM/RID).
//   4. Download missing packages with nuget.exe install (same as FAKE).
//   5. Deduplicate keeping the highest version per package ID (same as FAKE).
//   6. Copy .nupkg files to packageThirdPartyDir.
Task("RestoreThirdPartyPackages")
    .IsDependentOn("PrepareSources")
    .IsDependentOn("Build")
    .Does(() =>
{
    Information("Restoring third-party packages...");

    var sourcesDir = System.IO.Path.Combine(buildDir, "src");
    EnsureDirectoryExists(packageThirdPartyDir);
    CleanDirectory(packageThirdPartyDir);

    // --- Version helpers (replaces NuGet.Versioning) ---
    // Parse "1.2.3", "1.2.3.4", "1.2.3-beta" into a comparable tuple.
    (int, int, int, int, string) ParseVer(string v)
    {
        var pre = "";
        var dash = v.IndexOf('-');
        if (dash >= 0) { pre = v.Substring(dash + 1); v = v.Substring(0, dash); }
        var p = v.Split('.');
        int n(int i) => p.Length > i && int.TryParse(p[i], out var x) ? x : 0;
        return (n(0), n(1), n(2), n(3), pre);
    }

    int CmpVer(string a, string b)
    {
        var va = ParseVer(a); var vb = ParseVer(b);
        for (int i = 0; i < 4; i++)
        {
            var ai = i == 0 ? va.Item1 : i == 1 ? va.Item2 : i == 2 ? va.Item3 : va.Item4;
            var bi = i == 0 ? vb.Item1 : i == 1 ? vb.Item2 : i == 2 ? vb.Item3 : vb.Item4;
            if (ai != bi) return ai.CompareTo(bi);
        }
        // pre-release: empty string (release) sorts AFTER any pre-release label
        var pa = va.Item5; var pb = vb.Item5;
        if (pa == pb) return 0;
        if (pa == "") return 1;
        if (pb == "") return -1;
        return string.Compare(pa, pb, StringComparison.OrdinalIgnoreCase);
    }

    // Returns true if `version` satisfies NuGet version range `spec`.
    // Handles: "1.2.3" (>=), "[1.2.3]" (==), "[1,2)", "(1,2]", "[,2)" etc.
    bool Satisfies(string version, string spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return true;
        spec = spec.Trim();
        if (!spec.StartsWith("[") && !spec.StartsWith("("))
            return CmpVer(version, spec) >= 0;  // bare version = minimum

        bool minInc = spec[0] == '[';
        bool maxInc = spec[spec.Length - 1] == ']';
        var inner = spec.Substring(1, spec.Length - 2);
        var parts = inner.Split(',');
        if (parts.Length == 1)
            return CmpVer(version, parts[0].Trim()) == 0;

        var lo = parts[0].Trim(); var hi = parts[1].Trim();
        bool loOk = lo == "" || (minInc ? CmpVer(version, lo) >= 0 : CmpVer(version, lo) > 0);
        bool hiOk = hi == "" || (maxInc ? CmpVer(version, hi) <= 0 : CmpVer(version, hi) < 0);
        return loOk && hiOk;
    }

    // Extracts the minimum version string from a range spec (used for nuget.exe install).
    string MinVersion(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return "0.0.0";
        spec = spec.Trim();
        if (!spec.StartsWith("[") && !spec.StartsWith("(")) return spec;
        var inner = spec.Substring(1, spec.Length - 2).Split(',')[0].Trim();
        return inner == "" ? "0.0.0" : inner;
    }

    // --- 1. Get global NuGet cache path ---
    IEnumerable<string> localsOut;
    StartProcess("dotnet", new ProcessSettings
        { Arguments = "nuget locals global-packages --list", RedirectStandardOutput = true }, out localsOut);
    var globalPackagesPath = localsOut
        .FirstOrDefault(l => l.StartsWith("global-packages:", StringComparison.OrdinalIgnoreCase))
        ?.Substring("global-packages:".Length).Trim()
        ?? throw new Exception("Could not determine global NuGet packages folder.");
    Information($"Global NuGet cache: {globalPackagesPath}");

    // --- 2. Index all .nupkg files in the cache ---
    // Cache layout: {cache}/{id-lower}/{version}/{id-lower}.{version}.nupkg
    // packageGroups: id-lower -> list of (versionString, filePath)
    var packageGroups = new Dictionary<string, List<(string Version, string Path)>>(
        StringComparer.OrdinalIgnoreCase);

    foreach (var idDir in System.IO.Directory.GetDirectories(globalPackagesPath))
    {
        var pkgId = System.IO.Path.GetFileName(idDir);
        foreach (var verDir in System.IO.Directory.GetDirectories(idDir))
        {
            var verStr = System.IO.Path.GetFileName(verDir);
            var nupkg = System.IO.Path.Combine(verDir, $"{pkgId}.{verStr}.nupkg");
            if (!System.IO.File.Exists(nupkg)) continue;
            if (!packageGroups.TryGetValue(pkgId, out var lst))
                { lst = new List<(string, string)>(); packageGroups[pkgId] = lst; }
            lst.Add((verStr, nupkg));
        }
    }
    Information($"Indexed {packageGroups.Sum(kv => kv.Value.Count)} nupkg files in global cache");

    // --- Helper: re-index a single package directory after nuget.exe install ---
    void ReindexPackage(string id)
    {
        var idDir = System.IO.Path.Combine(globalPackagesPath, id.ToLower());
        if (!System.IO.Directory.Exists(idDir)) return;
        if (!packageGroups.ContainsKey(id))
            packageGroups[id] = new List<(string, string)>();
        foreach (var verDir in System.IO.Directory.GetDirectories(idDir))
        {
            var verStr = System.IO.Path.GetFileName(verDir);
            var nupkg = System.IO.Path.Combine(verDir, $"{id.ToLower()}.{verStr}.nupkg");
            if (!System.IO.File.Exists(nupkg)) continue;
            if (!packageGroups[id].Any(p => p.Version == verStr))
                packageGroups[id].Add((verStr, nupkg));
        }
    }

    // --- Helper: find lowest satisfying version in cache; download if absent (same as FAKE) ---
    (string Version, string Path)? ResolvePackage(string id, string versionSpec)
    {
        if (packageGroups.TryGetValue(id, out var lst))
        {
            var match = lst
                .Where(p => Satisfies(p.Version, versionSpec))
                .OrderBy(p => p.Version, Comparer<string>.Create((a, b) => CmpVer(a, b)))
                .Cast<(string Version, string Path)?>()
                .FirstOrDefault();
            if (match != null) return match;
        }

        // Not in cache – install it (mirrors FAKE's nuget.exe install branch)
        var minVer = MinVersion(versionSpec);
        Information($"Package {id} {versionSpec} not in cache, installing...");
        StartProcess(System.IO.Path.Combine(rootDir, "nuget.exe"), new ProcessSettings
        {
            Arguments = $"install {id} -Version {minVer} -Prerelease -NonInteractive"
        });
        ReindexPackage(id);

        if (packageGroups.TryGetValue(id, out var lst2))
        {
            var match2 = lst2
                .Where(p => Satisfies(p.Version, versionSpec))
                .OrderBy(p => p.Version, Comparer<string>.Create((a, b) => CmpVer(a, b)))
                .Cast<(string Version, string Path)?>()
                .FirstOrDefault();
            if (match2 != null) return match2;
        }

        Warning($"Package {id} {versionSpec} could not be resolved, skipping");
        return null;
    }

    // --- Helper: read ALL nuspec dependency entries from a nupkg (all TFM/RID groups) ---
    IEnumerable<(string Id, string VersionSpec)> NuspecDeps(string nupkgPath)
    {
        try
        {
            using var zip = System.IO.Compression.ZipFile.OpenRead(nupkgPath);
            var entry = zip.Entries.FirstOrDefault(e =>
                e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) &&
                !e.FullName.Contains('/'));
            if (entry == null) return Enumerable.Empty<(string, string)>();
            using var stream = entry.Open();
            var doc = System.Xml.Linq.XDocument.Load(stream);
            var ns = doc.Root.GetDefaultNamespace();
            return doc.Descendants(ns + "dependency")
                .Select(d => (
                    Id:          d.Attribute("id")?.Value,
                    VersionSpec: d.Attribute("version")?.Value ?? ""))
                .Where(d => d.Id != null)
                .ToList();
        }
        catch { return Enumerable.Empty<(string, string)>(); }
    }

    // --- 3. Seed from PackageReference items in all .csproj files ---
    // Key = "id/version" (PackageIdentity equivalent); allows multiple versions per id.
    var allPackages = new Dictionary<string, (string Id, string Version, string Path)>(
        StringComparer.OrdinalIgnoreCase);

    var csprojFiles = System.IO.Directory.GetFiles(sourcesDir, "*.csproj",
        System.IO.SearchOption.AllDirectories);
    Information($"Found {csprojFiles.Length} .csproj files");

    foreach (var csproj in csprojFiles)
    {
        var doc = System.Xml.Linq.XDocument.Load(csproj);
        foreach (var pr in doc.Descendants("PackageReference"))
        {
            var id  = pr.Attribute("Include")?.Value;
            var ver = pr.Attribute("Version")?.Value ?? pr.Element("Version")?.Value;
            if (id == null || ver == null) continue;

            var resolved = ResolvePackage(id, ver);
            if (resolved == null) continue;

            var key = $"{id}/{resolved.Value.Version}";
            if (!allPackages.ContainsKey(key))
                allPackages[key] = (id, resolved.Value.Version, resolved.Value.Path);
        }
    }
    Information($"{allPackages.Count} seed packages from .csproj files");

    // --- 4. BFS transitive expansion via nuspec deps (all groups) ---
    var bfsQueue = new Queue<string>(allPackages.Keys.ToList());
    while (bfsQueue.Count > 0)
    {
        var key = bfsQueue.Dequeue();
        var pkg  = allPackages[key];

        foreach (var (depId, depVer) in NuspecDeps(pkg.Path))
        {
            var resolved = ResolvePackage(depId, depVer);
            if (resolved == null) continue;

            var depKey = $"{depId}/{resolved.Value.Version}";
            if (!allPackages.ContainsKey(depKey))
            {
                allPackages[depKey] = (depId, resolved.Value.Version, resolved.Value.Path);
                bfsQueue.Enqueue(depKey);
            }
        }
    }
    Information($"{allPackages.Count} packages after transitive expansion");

    // --- 5. Deduplicate: keep highest version per package ID (same as FAKE's filteredDependencies) ---
    var filtered = allPackages.Values
        .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
        .Select(g => g.OrderByDescending(p => p.Version, Comparer<string>.Create((a, b) => CmpVer(a, b))).First())
        .ToList();
    Information($"{filtered.Count} packages after deduplication");

    // --- 6. Copy .nupkg files ---
    var copied = 0;
    foreach (var pkg in filtered.OrderBy(p => p.Id))
    {
        var dest = System.IO.Path.Combine(packageThirdPartyDir,
            System.IO.Path.GetFileName(pkg.Path));
        System.IO.File.Copy(pkg.Path, dest, overwrite: true);
        copied++;
    }
    Information($"Copied {copied} third-party packages to {packageThirdPartyDir}");
});

// Task: PushThirdPartyPackages
Task("PushThirdPartyPackages")
    .IsDependentOn("RestoreThirdPartyPackages")
    .Does(() =>
{
    Information("Pushing third-party NuGet packages...");

    var apiKey = EnvironmentVariable("NUGET_API_KEY") ?? "";

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
            Arguments = $"nuget push {file.FullPath} --source {nugetServerUrl} --api-key {apiKey} --skip-duplicate",
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
    var projFiles = GetFiles(System.IO.Path.Combine(sourcesDir, "**/*.csproj"))
        .Where(file =>
        {
            var content = System.IO.File.ReadAllText(file.FullPath);
            return !System.Text.RegularExpressions.Regex.IsMatch(content, "<IsTest>true</IsTest>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        });
    foreach (var proj in projFiles)
    {
        DotNetPack(proj.FullPath, new DotNetPackSettings
        {
            Configuration = "Release",
            NoBuild = true,
            NoRestore = true,
            IncludeSymbols = true,
            Verbosity = DotNetVerbosity.Minimal
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

    var nupkgFiles = GetFiles(System.IO.Path.Combine(sourcesDir, "**/bin/Release/*.nupkg"))
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

    var failedLocalFiles = new System.Collections.Generic.List<string>();

    foreach (var file in nupkgFiles)
    {
        Information($"Pushing local package: {file.FullPath}");

        IEnumerable<string> stdout, stderr;
        var result = StartProcess("dotnet", new ProcessSettings
        {
            Arguments = $"nuget push {file.FullPath} --source {nugetServerUrl} --api-key {apiKey} --skip-duplicate",
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }, out stdout, out stderr);

        foreach (var line in stdout) Information(line);
        foreach (var line in stderr) Warning(line);

        if (result != 0)
        {
            Warning($"Failed to push local package: {file.FullPath} (exit {result}). Continuing...");
            failedLocalFiles.Add(file.FullPath);
        }
    }

    if (failedLocalFiles.Any())
        throw new Exception($"The following local packages failed to push:\n{string.Join("\n", failedLocalFiles)}");

    Information("All local packages pushed successfully.");
});

// Task: RePushLocalPackages
Task("RePushLocalPackages")
    .Does(() =>
{
    Information("Re-pushing local NuGet packages one by one...");

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
            Arguments = $"nuget push {file.FullPath} --source {nugetServerUrl} --api-key {apiKey} --skip-duplicate",
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
            Arguments = $"nuget push {file.FullPath} --source {nugetServerUrl} --api-key {apiKey} --skip-duplicate",
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
        new { Name = "klusterkite/elasticsearch", Path = "Docker/KlusterKiteELK" },
        new { Name = "klusterkite/redis", Path = "Docker/KlusterKite.Redis" }
    };

    foreach (var image in dockerImages)
    {
        Information($"Building Docker image: {image.Name} from path: {image.Path}");

        var result = StartProcess("docker", new ProcessSettings
        {
            Arguments = $"buildx build --load -t {image.Name}:latest {image.Path}"
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

    // Define projects and their output paths (absolute so MSBuild resolves correctly)
    var projects = new[]
    {
        new { OutputPath = System.IO.Path.Combine(rootDir, "build", "launcher"), ProjectPath = System.IO.Path.Combine(buildDir, "src", "KlusterKite.NodeManager", "KlusterKite.NodeManager.Launcher", "KlusterKite.NodeManager.Launcher.csproj") },
        new { OutputPath = System.IO.Path.Combine(rootDir, "build", "seed"),    ProjectPath = System.IO.Path.Combine(buildDir, "src", "KlusterKite.Core", "KlusterKite.Core.Service", "KlusterKite.Core.Service.csproj") },
        new { OutputPath = System.IO.Path.Combine(rootDir, "build", "seeder"),  ProjectPath = System.IO.Path.Combine(buildDir, "src", "KlusterKite.NodeManager", "KlusterKite.NodeManager.Seeder.Launcher", "KlusterKite.NodeManager.Seeder.Launcher.csproj") }
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
            settings.WithProperty("TargetFramework", "net9.0");
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
        CopyDirectory(System.IO.Path.Combine(rootDir, "build", "launcher"), buildDir);
        CopyFileToDirectory(System.IO.Path.Combine(rootDir, "Docker", "utils", "launcher", "start.sh"), buildDir);
        CopyFileToDirectory(System.IO.Path.Combine(rootDir, "nuget.exe"), buildDir);
    }

    // Copy seed and seeder build outputs to their Docker contexts
    var seedBuildOut   = System.IO.Path.Combine(rootDir, "build", "seed");
    var seederBuildOut = System.IO.Path.Combine(rootDir, "build", "seeder");

    CleanDirectory("./Docker/KlusterKiteSeed/build");
    CopyDirectory(seedBuildOut, "./Docker/KlusterKiteSeed/build");

    CleanDirectory("./Docker/KlusterKiteSeeder/build");
    CopyDirectory(seederBuildOut, "./Docker/KlusterKiteSeeder/build");

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
            Arguments = $"buildx build --load -t {image.Name}:latest {image.Path}"
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
    // 1. Save current .env as .env-build
    if (System.IO.File.Exists(envFile))
    {
        if (System.IO.File.Exists(envBuild)) System.IO.File.Delete(envBuild);
        System.IO.File.Move(envFile, envBuild);
    }
    // 2. Activate .env-local as .env
    if (System.IO.File.Exists(envLocal))
    {
        if (System.IO.File.Exists(envFile)) System.IO.File.Delete(envFile);
        System.IO.File.Move(envLocal, envFile);
    }

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
            Arguments = "buildx build --load -t klusterkite/monitoring-ui:latest Docker/KlusterKiteMonitoring"
        });
        if (monitoringResult != 0)
            throw new Exception("Failed to build Docker image: klusterkite/monitoring-ui");
    }
    finally
    {
        // Always restore env files
        // 1. Restore .env to .env-local
        if (System.IO.File.Exists(envFile))
        {
            if (System.IO.File.Exists(envLocal)) System.IO.File.Delete(envLocal);
            System.IO.File.Move(envFile, envLocal);
        }
        // 2. Restore .env-build to .env
        if (System.IO.File.Exists(envBuild))
        {
            if (System.IO.File.Exists(envFile)) System.IO.File.Delete(envFile);
            System.IO.File.Move(envBuild, envFile);
        }
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