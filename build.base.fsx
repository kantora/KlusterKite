#r ".fake/build.fsx/packages/netcorebuild/Fake.Core.Targets/lib/netstandard1.6/Fake.Core.Targets.dll"
#r ".fake/build.fsx/packages/netcorebuild/Fake.Core.Environment/lib/netstandard1.6/Fake.Core.Environment.dll"
#r ".fake/build.fsx/packages/netcorebuild/Fake.Core.Process/lib/netstandard1.6/Fake.Core.Process.dll"
#r ".fake/build.fsx/packages/netcorebuild/Fake.Core.SemVer/lib/netstandard1.6/Fake.Core.SemVer.dll"
#r ".fake/build.fsx/packages/netcorebuild/System.Diagnostics.Process/runtimes/win/lib/netstandard1.4/System.Diagnostics.Process.dll"
#r ".fake/build.fsx/packages/netcorebuild/System.Text.Encoding/ref/netstandard1.3/System.Text.Encoding.dll"
#r ".fake/build.fsx/packages/netcorebuild/Fake.DotNet.NuGet/lib/netstandard1.6/Fake.DotNet.NuGet.dll"
#r ".fake/build.fsx/packages/netcorebuild/Fake.DotNet.MsBuild/lib/netstandard1.6/Fake.DotNet.MsBuild.dll"
#r ".fake/build.fsx/packages/netcorebuild/Fake.IO.FileSystem/lib/netstandard1.6/Fake.IO.FileSystem.dll"
#r ".fake/build.fsx/packages/netcorebuild/Microsoft.Build/lib/netstandard1.5/Microsoft.Build.dll"
#r ".fake/build.fsx/packages/netcorebuild/System.Xml.ReaderWriter/lib/netstandard1.3/System.Xml.ReaderWriter.dll"
#r ".fake/build.fsx/packages/netcorebuild/System.Xml.XmlDocument/lib/netstandard1.3/System.Xml.XmlDocument.dll"
#r ".fake/build.fsx/packages/netcorebuild/System.Xml.XDocument/lib/netstandard1.3/System.Xml.XDocument.dll"
#r ".fake/build.fsx/packages/netcorebuild/System.IO.Compression/runtimes/win/lib/netstandard1.3/System.IO.Compression.dll"
#r ".fake/build.fsx/packages/netcorebuild/System.IO.Compression.ZipFile/lib/netstandard1.3/System.IO.Compression.ZipFile.dll"
#r ".fake/build.fsx/packages/netcorebuild/System.Net.Http/runtimes/win/lib/netstandard1.3/System.Net.Http.dll"

#r @".fake/build.fsx/packages/netcorebuild/Newtonsoft.Json/lib/netstandard1.3/Newtonsoft.Json.dll"
#r @".fake/build.fsx/packages/netcorebuild/NuGet.Protocol.Core.Types/lib/netstandard1.3/NuGet.Protocol.Core.Types.dll"
#r @".fake/build.fsx/packages/netcorebuild/NuGet.Protocol.Core.v3/lib/netstandard1.3/NuGet.Protocol.Core.v3.dll"
#r @".fake/build.fsx/packages/netcorebuild/NuGet.Configuration/lib/netstandard1.3/NuGet.Configuration.dll"
#r @".fake/build.fsx/packages/netcorebuild/NuGet.Common/lib/netstandard1.3/NuGet.Common.dll"
#r @".fake/build.fsx/packages/netcorebuild/NuGet.Frameworks/lib/netstandard1.3/NuGet.Frameworks.dll"
#r @".fake/build.fsx/packages/netcorebuild/NuGet.Versioning/lib/netstandard1.0/NuGet.Versioning.dll"
#r @".fake/build.fsx/packages/netcorebuild/NuGet.Packaging/lib/netstandard1.3/NuGet.Packaging.dll"
#r @".fake/build.fsx/packages/netcorebuild/NuGet.Packaging.Core/lib/netstandard1.3/NuGet.Packaging.Core.dll"
#r @".fake/build.fsx/packages/netcorebuild/NuGet.Packaging.Core.Types/lib/netstandard1.3/NuGet.Packaging.Core.Types.dll"

#load "./build.config.fsx"

namespace KlusterKite.Build

open System
open System.IO
open System.Diagnostics
open System.Xml
open System.Text.RegularExpressions
open System.Collections.Generic;

open Microsoft.Build.Evaluation

open Fake.Core
open Fake.Core.TargetOperators
open Fake.Core.Process
open Fake.DotNet.NuGet.Version
open Fake.DotNet.MsBuild
open Fake.IO.FileSystem.Shell

open NuGet.Configuration
open NuGet.Common
open NuGet.Protocol
open NuGet.Versioning
open NuGet.Packaging

open Config

module  Base =

    let buildDocker (containerName:string) (path:string) =
        if not((execProcess (fun info ->
                    info.FileName <- "docker"
                    info.Arguments <- (sprintf "build -t %s:latest %s" containerName path)
                    )  (TimeSpan.FromDays 2.0))) then
            failwithf "Error while building %s" path

    let dotNetBuild setParams project = 
        let args = 
            MSBuildDefaults
            |> setParams
            |> serializeMSBuildParams

        if not((execProcess (fun info ->
                    info.FileName <- "dotnet"
                    info.Arguments <- (sprintf "build %s %s" args project)
                    )  (TimeSpan.FromDays 2.0))) then
            failwithf "Error while building %s" project;

    let pushPackage (package:string) =
        let localPath = Path.GetFullPath(".");
        let packageLocal = package.Replace(localPath, ".");
        execProcess (fun info ->
            info.FileName <- "nuget.exe";
            info.Arguments <- sprintf "push %s -Source %s -ApiKey %s" packageLocal "http://docker:81/" "KlusterKite")
            (TimeSpan.FromMinutes 30.0)
            |> ignore;

    let filesInDirMatchingRecursive (pattern:string) (dir:DirectoryInfo) = 
        dir.GetFiles(pattern, SearchOption.AllDirectories);
    let filesInDirMatching (pattern:string) (dir:DirectoryInfo) = 
        dir.GetFiles(pattern, SearchOption.TopDirectoryOnly);

    Target.Create "Clean" (fun _ ->
        printfn "PreClean..."
        CleanDir buildDir
    )

    // switches nuget and build version from init one, to latest posible on docker nuget server
    Target.Create "SetVersion" (fun _ ->
        let nugetVersion = getLastNuGetVersion "http://docker:81" testPackageName
        if nugetVersion.IsSome then printfn "Current version is %s " (nugetVersion.ToString()) else printfn "Repository is empty"
        version <- Regex.Replace((if nugetVersion.IsSome then ((IncPatch nugetVersion.Value).ToString()) else "0.0.0-local"), "((\\d+\\.?)+)(.*)", "$1-local")
        packageDir <- packagePushDir
        printfn "New version is %s \n" version
    )

    Target.Create "PrepareSources" (fun _ ->
        printfn "Creating a sources copy..."
        let sourcesDir = Path.Combine(buildDir, "src")
        ensureDirectory sourcesDir
        CleanDir sourcesDir
    
        Directory.GetDirectories "."
            |> Seq.filter (fun (dir:string) -> not (Seq.isEmpty (filesInDirMatchingRecursive "*.csproj" (new DirectoryInfo(dir))))) 
            |> Seq.iter (fun (dir:string) ->
        
                    filesInDirMatchingRecursive "*.csproj" (new DirectoryInfo(dir))
                    |> Seq.iter
                        (fun (file:FileInfo) -> 
                            let projectDir = Path.GetDirectoryName(file.FullName)
                            CleanDir (Path.Combine(projectDir, "bin")))

                    let fullDir = Path.GetFullPath(dir)
                    let destinationDir = Path.Combine(sourcesDir, Path.GetFileName(fullDir), ".")                
                    CopyDir destinationDir fullDir (fun _ -> true))

        filesInDirMatching "*.sln" (new DirectoryInfo("."))
            |> Seq.iter (fun (file:FileInfo) -> CopyFile sourcesDir file.FullName)
        filesInDirMatching "*.fsx" (new DirectoryInfo("."))
            |> Seq.iter (fun (file:FileInfo) -> CopyFile sourcesDir file.FullName)
        filesInDirMatching "*.props" (new DirectoryInfo("."))
            |> Seq.iter (fun (file:FileInfo) -> CopyFile sourcesDir file.FullName)
       

        let projects = filesInDirMatchingRecursive "*.csproj" (new DirectoryInfo(sourcesDir))
        projects 
        |> Seq.iter (fun (file:FileInfo) -> 
            let projectDir = Path.GetDirectoryName(file.FullName)
            CleanDir (Path.Combine(projectDir, "obj"))
            RegexReplaceInFileWithEncoding  "<Version>(.*)</Version>" (sprintf "<Version>%s</Version>" version) Text.Encoding.UTF8 file.FullName) 
    )

    "SetVersion" ?=> "PrepareSources"    

    Target.Create  "Build" (fun _ ->
        printfn "Build..."
        let sourcesDir = Path.Combine(buildDir, "src")  
        Seq.iter
            (fun (file:FileInfo) ->
                let setParams defaults = { 
                    defaults with
                        Verbosity = Some(Minimal)
                        Targets = ["Restore"; "Build"]
                        RestorePackagesFlag = true
                        Properties = 
                        [
                            "Optimize", "True"
                            "DebugSymbols", "True"
                            "Configuration", "Release"                        
                        ]
                }
                dotNetBuild setParams file.FullName)
            (filesInDirMatching "*.sln" (new DirectoryInfo(sourcesDir)))
    )

    "PrepareSources" ==> "Build"

    Target.Create "Nuget" (fun _ ->
        printfn "Packing nuget..."
        
        let sourcesDir = Path.Combine(buildDir, "src")  
        
        filesInDirMatching "*.sln" (new DirectoryInfo(sourcesDir))
        |> Seq.iter
            (fun (file:FileInfo) ->
                let setParams defaults = { 
                    defaults with
                        Verbosity = Some(Minimal)
                        Targets = ["Pack"]
                        RestorePackagesFlag = true
                        Properties = 
                        [
                            "Optimize", "True"
                            "DebugSymbols", "True"
                            "Configuration", "Release"                        
                        ]
                }
                dotNetBuild setParams file.FullName)
                
        CleanDir packageDir

        let testProjects = filesInDirMatchingRecursive "*.csproj" (new DirectoryInfo(sourcesDir))
                                |> Seq.filter (fun (file:FileInfo) -> Regex.IsMatch(File.ReadAllText(file.FullName), "<IsTest>true</IsTest>", (RegexOptions.CultureInvariant ||| RegexOptions.IgnoreCase)))
                                |> Seq.map (fun (file:FileInfo) -> Path.GetFileNameWithoutExtension(file.Name))
                                |> List<string>
        
        filesInDirMatchingRecursive "*.nupkg" (new DirectoryInfo(sourcesDir))
        |> Seq.filter (fun (file:FileInfo) -> not(testProjects.Contains(((new DirectoryInfo(Path.GetFullPath (Path.Combine((Path.GetDirectoryName file.FullName), "../../")))).Name))))
        |> Seq.iter
            (fun (file:FileInfo) ->
                    printfn "%s" (Path.GetFileName file.FullName)
                    CopyFile packageDir file.FullName)
    )

    "Build" ==> "Nuget"


    Target.Create "CleanDockerImages" (fun _ ->
        let outputProcess line =
            let parts = Regex.Split(line, "[\t ]+")
            if ("<none>".Equals(parts.[0]) && parts.Length >= 3) then
                let args = sprintf "rmi %s" parts.[2]
                ExecProcess (fun info -> info.FileName <- "docker"; info.Arguments <- args) (TimeSpan.FromMinutes 30.0)
                    |> ignore

        let lines = new ResizeArray<String>();
        ExecProcessWithLambdas
            (fun info -> info.FileName <- "docker"; info.Arguments <- "images")
            (TimeSpan.FromMinutes 30.0)
            true
            (fun e -> failwith e)
            (fun l -> lines.Add(l))
            |> ignore

        lines |> Seq.iter outputProcess
    )

    Target.Create "PushLocalPackages" (fun _ ->
        pushPackage (Path.Combine(packagePushDir, "*.nupkg"))
    )

    "Nuget" ?=> "PushLocalPackages"

    Target.Create "RePushLocalPackages" (fun _ ->
        Directory.GetFiles(packagePushDir)
            |> Seq.filter (fun f -> Path.GetExtension(f) = ".nupkg")
            |> Seq.iter pushPackage
    )

    Target.Create "FinalPushLocalPackages" (fun _ -> ())
    "SetVersion" ==> "FinalPushLocalPackages"
    "Nuget" ==> "FinalPushLocalPackages"
    "PushLocalPackages" ==> "FinalPushLocalPackages"

    Target.Create "RestoreThirdPartyPackages" (fun _ ->
        printfn "Restoring packages"
    
        let sourcesDir = Path.Combine(buildDir, "src") 
        ensureDirectory packageThirdPartyDir
        CleanDir packageThirdPartyDir
        let packageCache = SettingsUtility.GetGlobalPackagesFolder(NullSettings.Instance)
        let packages = 
            LocalFolderUtility.GetPackagesV3(packageCache, NullLogger.Instance)
        let packageGroups = packages
                                |> Seq.groupBy (fun (p:LocalPackageInfo) -> p.Identity.Id.ToLower())
                                |> dict
                                |> Dictionary<string, seq<LocalPackageInfo>>

        let directPackages = filesInDirMatchingRecursive "*.csproj" (new DirectoryInfo(sourcesDir))
                                |> Seq.map(fun (file:FileInfo) -> new Project(file.FullName, null, null, ProjectCollection.GlobalProjectCollection, ProjectLoadSettings.IgnoreMissingImports))
                                |> Seq.collect (fun proj -> proj.ItemsIgnoringCondition |> Seq.map (fun item -> (proj, item)))
                                |> Seq.filter (fun (_, item) -> item.ItemType = "PackageReference")
                                |> Seq.map (fun (proj, item) -> ((item.Xml.Include), (item.Metadata |> Seq.filter (fun d -> d.Name = "Version") |> Seq.tryHead |> (fun i -> if i.IsSome then i.Value.EvaluatedValue else null)), proj))
                                |> Seq.filter (fun (_, version, _) -> version <> null)
                                |> Seq.map (fun (id, version, _) -> 
                                    // printfn "%s: %s %s" (Path.GetFileName proj.FullPath) id version  
                                    (id, version)
                                )
                                |> Seq.distinct
                                |> Seq.sortBy (fun (id, _) -> id)
                                |> Seq.map (fun (id, version) -> 
                                    let (success, list) = packageGroups.TryGetValue(id.ToLower().Trim())
                                    if success then
                                        list
                                        |> Seq.filter (fun (p:LocalPackageInfo) -> VersionRange.Parse(version).Satisfies(p.Identity.Version))
                                        |> Seq.sortBy (fun (p:LocalPackageInfo) -> p.Identity.Version)
                                        |> Seq.tryHead
                                    else
                                        printfn "!!!!Package %s was not found" (id.ToLower().Trim())
                                        None)
                                |> Seq.filter (fun (p:LocalPackageInfo option) -> p.IsSome)
                                |> Seq.map (fun (p:LocalPackageInfo option) -> p.Value)
                                |> Seq.distinct
                                |> Seq.map (fun (p:LocalPackageInfo) -> p.Identity, p)
                                |> dict
   
        let getDirectDependecies (packages : IDictionary<Core.PackageIdentity, LocalPackageInfo>) = 
            packages.Values
                |> Seq.collect(fun (p:LocalPackageInfo) -> p.Nuspec.GetDependencyGroups())
                |> Seq.collect(fun (dg:PackageDependencyGroup) -> dg.Packages)
                |> Seq.distinct
                |> Seq.map (fun (d: Core.PackageDependency) ->
                    let (success, list) = packageGroups.TryGetValue(d.Id.ToLower())
                    if success then
                        list 
                            |> Seq.filter (fun (p:LocalPackageInfo) -> (d.VersionRange.Satisfies(p.Identity.Version)))
                            |> Seq.sortBy (fun (p:LocalPackageInfo) -> p.Identity.Version)
                            |> Seq.tryHead 
                            |> (fun (p:LocalPackageInfo option) -> if p.IsSome then p.Value :> Object else d :> Object)
                    else d :> Object
                    )
                |> Seq.map (fun arg -> 
                    match arg with
                    | :? LocalPackageInfo as p -> p
                    | :? Core.PackageDependency as d -> 
                        printfn "Package requirement %s %s was not found, installing" d.Id (d.VersionRange.ToString())
                        ExecProcess (fun info ->
                                info.FileName <- "nuget.exe";
                                info.Arguments <- sprintf "install %s -Version %s -Prerelease" d.Id (d.VersionRange.MinVersion.ToString()))
                                (TimeSpan.FromMinutes 30.0)
                                |> ignore
                        let newPackage = 
                            LocalFolderUtility.GetPackagesV3(packageCache, NullLogger.Instance)
                            |> Seq.filter(fun p -> p.Identity.Id.ToLower() = d.Id.ToLower() && p.Identity.Version = (NuGetVersion.Parse(d.VersionRange.MinVersion.ToString())))
                            |> Seq.tryHead
                    
                        if newPackage.IsNone then failwith  (sprintf "package install of %s %s failed" d.Id (d.VersionRange.ToString()))
                    
                        if not(packageGroups.ContainsKey(newPackage.Value.Identity.Id.ToLower()))
                        then packageGroups.Add(newPackage.Value.Identity.Id.ToLower(), [newPackage.Value]) 
                        else packageGroups.[newPackage.Value.Identity.Id.ToLower()] <- (packageGroups.[newPackage.Value.Identity.Id.ToLower()] |> Seq.append [newPackage.Value])                   
                        newPackage.Value
                    | _ -> failwith "strange")
                |> Seq.cast<LocalPackageInfo>
                |> Seq.distinct
                |> Seq.filter(fun (p:LocalPackageInfo) -> not (packages.ContainsKey(p.Identity)))  

        let rec getPackagesWithDependencies (_packages : IDictionary<Core.PackageIdentity, LocalPackageInfo>) = 
            let _directDependencies = getDirectDependecies _packages
            if _directDependencies |> Seq.isEmpty then
                _packages
            else
                _packages.Values 
                    |> Seq.append _directDependencies 
                    |> Seq.map (fun (p:LocalPackageInfo) -> p.Identity, p)
                    |> dict                
                    |> getPackagesWithDependencies

        printfn "%d start packages"  (directPackages |> Seq.length)
    
        let dependecies = 
            getPackagesWithDependencies directPackages

        let filteredDependencies =
            dependecies.Values
            |> Seq.groupBy(fun (p:LocalPackageInfo) -> p.Identity.Id)
            |> Seq.map (fun (_, list) -> list |> Seq.sortBy(fun p -> p.Identity.Version) |> Seq.last)
        
        filteredDependencies
        |> Seq.sortBy(fun (p:LocalPackageInfo) -> p.Identity.Id)
        |> Seq.iter (fun (p:LocalPackageInfo) ->
            CopyFile packageThirdPartyDir p.Path        
        )

        printfn "total %d third party packages"  (filteredDependencies |> Seq.length)      
    )

    "PrepareSources" ==> "RestoreThirdPartyPackages"
    "Build" ==> "RestoreThirdPartyPackages"

    Target.Create "PushThirdPartyPackages" (fun _ ->
        pushPackage (Path.Combine(packageThirdPartyDir, "*.nupkg"))
    )

    "PushThirdPartyPackages" ?=> "PushLocalPackages"
    "RestoreThirdPartyPackages" ?=> "PushThirdPartyPackages"

    Target.Create "RePushThirdPartyPackages" (fun _ ->
        filesInDirMatchingRecursive "*.nupkg" (new DirectoryInfo(packageThirdPartyDir))
            |> Seq.map (fun (file:FileInfo) -> file.FullName)
            |> Seq.iter pushPackage
    )

    Target.Create "FinalPushThirdPartyPackages" (fun _ -> ())
    "RestoreThirdPartyPackages" ==> "FinalPushThirdPartyPackages"
    "PushThirdPartyPackages" ==> "FinalPushThirdPartyPackages"

    Target.Create "FinalPushAllPackages" (fun _ -> ())
    "FinalPushThirdPartyPackages" ==> "FinalPushAllPackages"
    "FinalPushLocalPackages" ==> "FinalPushAllPackages"

    Target.Create "Tests" (fun _ -> 
        let sourcesDir = Path.Combine(buildDir, "src") 
        let outputTests = Path.Combine(buildDir, "tests") 
        ensureDirectory outputTests
        CleanDir outputTests
        let runSingleProject project =
            ExecProcess(fun info ->
                info.FileName <- "dotnet"
                info.WorkingDirectory <- (Directory.GetParent project).FullName
                info.Arguments <- "restore") (TimeSpan.FromMinutes 30.) 
                |> ignore
            ExecProcess(fun info ->
                info.FileName <- "dotnet"
                info.WorkingDirectory <- (Directory.GetParent project).FullName
                info.Arguments <- (sprintf "xunit -parallel none -xml %s/%s_xunit.xml" outputTests (Path.GetFileNameWithoutExtension project))) (TimeSpan.FromMinutes 30.) 
                |> ignore

        filesInDirMatchingRecursive "*.csproj" (new DirectoryInfo(sourcesDir))
            |> Seq.map(fun (file:FileInfo) -> new Project(file.FullName, null, null, ProjectCollection.GlobalProjectCollection, ProjectLoadSettings.IgnoreMissingImports))
            |> Seq.collect (fun proj -> proj.ItemsIgnoringCondition |> Seq.map (fun item -> (proj, item)))
            |> Seq.filter (fun (_, item) -> item.ItemType = "DotNetCliToolReference" && item.EvaluatedInclude = "dotnet-xunit")
            |> Seq.map (fun (proj, _) -> proj)
            |> Seq.distinct
            |> Seq.iter(fun f -> runSingleProject f.FullPath)
    )
    "PrepareSources" ==> "Tests"
