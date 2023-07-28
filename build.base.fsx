#if FAKE
#r "paket: groupref netcorebuild //"
#endif
#load "./.fake/build.fsx/intellisense.fsx"
#load "./build.config.fsx"

namespace KlusterKite.Build

open System
open System.IO
open System.Text.RegularExpressions
open System.Collections.Generic;

open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.IO

open NuGet.Configuration
open NuGet.Protocol
open NuGet.Common
open NuGet.Versioning



open Config

module  Base =

    let CleanDir (directoryName: string) = 
        Directory.delete directoryName
        Directory.ensure directoryName

    let CleanDirs (directories: string list) = directories |> Seq.iter (fun dir -> CleanDir dir)

    let CopyDir (destination: string) (source: string) = 
        (DirectoryInfo.copyRecursiveTo true, DirectoryInfo(destination), DirectoryInfo(source))
            |> ignore

    let CopyFile (destination: string) (source: string) = 
        FileInfo(source).CopyTo(destination) |> ignore
    
    let CopyTo (destination: string) (source: string) = 
        FileInfo(source).CopyTo(Path.combine destination (FileInfo(source).Name)) |> ignore


    let buildDocker (containerName:string) (path:string) =
        let result  = ["build"; "-t"; sprintf "%s:latest" containerName; path]
                        |> CreateProcess.fromRawCommand "docker" 
                        |> Proc.run

        if result.ExitCode <> 0 then failwith "Error while building %s" path


    let pushPackage (package:string) =
        let localPath = Path.GetFullPath(".");
        let packageLocal = package.Replace(localPath, ".");

        ["push";packageLocal;"-Source";"http://docker:81/";"-ApiKey";"KlusterKite"]
                        |> CreateProcess.fromRawCommand "nuget.exe" 
                        |> Proc.run
                        |> ignore

        

    let filesInDirMatchingRecursive (pattern:string) (dir:DirectoryInfo) = 
        dir.GetFiles(pattern, SearchOption.AllDirectories);
    let filesInDirMatching (pattern:string) (dir:DirectoryInfo) = 
        dir.GetFiles(pattern, SearchOption.TopDirectoryOnly);

    Target.create "Clean" (fun _ ->
        printfn "PreClean..."
        CleanDir buildDir
    )

    // switches nuget and build version from init one, to latest posible on docker nuget server
    Target.create "SetVersion" (fun _ ->
        let nugetVersion = NuGet.Version.getLastNuGetVersion "http://docker:81" testPackageName
        if nugetVersion.IsSome then printfn "Current version is %s " (nugetVersion.ToString()) else printfn "Repository is empty"
        version <- Regex.Replace((if nugetVersion.IsSome then ((NuGet.Version.IncPatch nugetVersion.Value).ToString()) else "0.0.0-local"), "((\\d+\\.?)+)(.*)", "$1-local")
        packageDir <- packagePushDir
        printfn "New version is %s \n" version
    )

    Target.create "PrepareSources" (fun _ ->
        printfn "Creating a sources copy..."
        let sourcesDir = Path.Combine(buildDir, "src")
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
                    CopyDir destinationDir fullDir )

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
            Shell.regexReplaceInFileWithEncoding  "<Version>(.*)</Version>" (sprintf "<Version>%s</Version>" version) Text.Encoding.UTF8 file.FullName) 
    )

    "SetVersion" ?=> "PrepareSources"    

    Target.create  "Build" (fun _ ->
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
                MSBuild.build setParams file.FullName)
            (filesInDirMatching "*.sln" (new DirectoryInfo(sourcesDir)))
    )

    "PrepareSources" ==> "Build"

    Target.create "Nuget" (fun _ ->
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
                MSBuild.build setParams file.FullName)
                
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


    Target.create "CleanDockerImages" (fun _ ->
        let outputProcess line =
            let parts = Regex.Split(line, "[\t ]+")
            if ("<none>".Equals(parts.[0]) && parts.Length >= 3) then
                let args = sprintf "rmi %s" parts.[2]
                ["rmi";parts.[2]]
                |> CreateProcess.fromRawCommand "docker" 
                |> Proc.run
                |> ignore

                

        let lines = new ResizeArray<String>();

        CreateProcess.fromRawCommand "docker" ["images"] 
        |> CreateProcess.redirectOutput
        |> CreateProcess.map (fun r -> r.Result.Output.Split '\n' |> lines.AddRange)
        |> Proc.run
        

        lines |> Seq.iter outputProcess
    )

    Target.create "PushLocalPackages" (fun _ ->
        pushPackage (Path.Combine(packagePushDir, "*.nupkg"))
    )

    "Nuget" ?=> "PushLocalPackages"

    Target.create "RePushLocalPackages" (fun _ ->
        Directory.GetFiles(packagePushDir)
            |> Seq.filter (fun f -> Path.GetExtension(f) = ".nupkg")
            |> Seq.iter pushPackage
    )

    Target.create "FinalPushLocalPackages" (fun _ -> ())
    "SetVersion" ==> "FinalPushLocalPackages"
    "Nuget" ==> "FinalPushLocalPackages"
    "PushLocalPackages" ==> "FinalPushLocalPackages"

    Target.create "RestoreThirdPartyPackages" (fun _ ->
        printfn "Restoring packages"
    
        let sourcesDir = Path.Combine(buildDir, "src") 
        Directory.ensure packageThirdPartyDir
        CleanDir packageThirdPartyDir
        
        let packageCache = SettingsUtility.GetGlobalPackagesFolder(NullSettings.Instance)
        let packages = 
            LocalFolderUtility.GetPackagesV3(packageCache, NullLogger.Instance)
        
        let packageGroups = packages
                                |> Seq.groupBy (fun (p:LocalPackageInfo) -> p.Identity.Id.ToLower())
                                |> dict
                                |> Dictionary<string, seq<LocalPackageInfo>>

        let directPackages = filesInDirMatchingRecursive "*.csproj" (new DirectoryInfo(sourcesDir))
                                |> Seq.map(fun (file:FileInfo) -> new Microsoft.Build.Evaluation.Project(file.FullName, null, null, Microsoft.Build.Evaluation.ProjectCollection.GlobalProjectCollection, Microsoft.Build.Evaluation.ProjectLoadSettings.IgnoreMissingImports))
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
  
        let getDirectDependecies (packages : IDictionary<NuGet.Packaging.Core.PackageIdentity, LocalPackageInfo>) = 
            packages.Values
                |> Seq.collect(fun (p:LocalPackageInfo) -> p.Nuspec.GetDependencyGroups())
                |> Seq.collect(fun (dg:NuGet.Packaging.PackageDependencyGroup) -> dg.Packages)
                |> Seq.distinct
                |> Seq.map (fun (d: NuGet.Packaging.Core.PackageDependency) ->
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
                    | :? NuGet.Packaging.Core.PackageDependency as d -> 
                        printfn "Package requirement %s %s was not found, installing" d.Id (d.VersionRange.ToString())

                        ["install";d.Id;"-Version";d.VersionRange.MinVersion.ToString();"-Prerelease"]
                        |> CreateProcess.fromRawCommand "nuget.exe" 
                        |> Proc.run
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
        
        let rec getPackagesWithDependencies (_packages : IDictionary<NuGet.Packaging.Core.PackageIdentity, LocalPackageInfo>) = 
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

    Target.create "PushThirdPartyPackages" (fun _ ->
        pushPackage (Path.Combine(packageThirdPartyDir, "*.nupkg"))
    )

    "PushThirdPartyPackages" ?=> "PushLocalPackages"
    "RestoreThirdPartyPackages" ?=> "PushThirdPartyPackages"

    Target.create "RePushThirdPartyPackages" (fun _ ->
        filesInDirMatchingRecursive "*.nupkg" (new DirectoryInfo(packageThirdPartyDir))
            |> Seq.map (fun (file:FileInfo) -> file.FullName)
            |> Seq.iter pushPackage
    )

    Target.create "FinalPushThirdPartyPackages" (fun _ -> ())
    "RestoreThirdPartyPackages" ==> "FinalPushThirdPartyPackages"
    "PushThirdPartyPackages" ==> "FinalPushThirdPartyPackages"

    Target.create "FinalPushAllPackages" (fun _ -> ())
    "FinalPushThirdPartyPackages" ==> "FinalPushAllPackages"
    "FinalPushLocalPackages" ==> "FinalPushAllPackages"

    Target.create "Tests" (fun _ -> 
        let sourcesDir = Path.Combine(buildDir, "src") 
        let outputTests = Path.Combine(buildDir, "tests") 
        Directory.ensure  outputTests
        CleanDir outputTests

        let runSingleProject project =
            ["restore"]
                |> CreateProcess.fromRawCommand "dotnet" 
                |> CreateProcess.withWorkingDirectory (Directory.GetParent project).FullName
                |> Proc.run
                |> ignore
            ["xunit"; "-parallel"; "none"; "-xml"; (sprintf "%s/%s_xunit.xml" outputTests (Path.GetFileNameWithoutExtension project))]
                |> CreateProcess.fromRawCommand "dotnet" 
                |> CreateProcess.withWorkingDirectory (Directory.GetParent project).FullName
                |> Proc.run
                |> ignore            

        
        filesInDirMatchingRecursive "*.csproj" (new DirectoryInfo(sourcesDir))
            |> Seq.map(fun (file:FileInfo) -> new Microsoft.Build.Evaluation.Project(file.FullName, null, null, Microsoft.Build.Evaluation.ProjectCollection.GlobalProjectCollection, Microsoft.Build.Evaluation.ProjectLoadSettings.IgnoreMissingImports))
            |> Seq.collect (fun proj -> proj.ItemsIgnoringCondition |> Seq.map (fun item -> (proj, item)))
            |> Seq.filter (fun (_, item) -> item.ItemType = "DotNetCliToolReference" && item.EvaluatedInclude = "dotnet-xunit")
            |> Seq.map (fun (proj, _) -> proj)
            |> Seq.distinct
            |> Seq.iter(fun f -> runSingleProject f.FullPath)
        
    )
    "PrepareSources" ==> "Tests"
