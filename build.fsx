#if FAKE
#r "paket: groupref netcorebuild //"
#else
#r "./packages/netcorebuild/Fake.Core.Environment/lib/netstandard2.0/Fake.Core.Environment.dll"
#r "./packages/netcorebuild/Fake.Core.Target/lib/netstandard2.0/Fake.Core.Target.dll"
#r "./packages/netcorebuild/Fake.Core.Process/lib/netstandard2.0/Fake.Core.Process.dll"
#r "./packages/netcorebuild/Fake.Core.SemVer/lib/netstandard2.0/Fake.Core.SemVer.dll"
#r "./packages/netcorebuild/Fake.DotNet.NuGet/lib/netstandard2.0/Fake.DotNet.NuGet.dll"
#r "./packages/netcorebuild/Fake.DotNet.MsBuild/lib/netstandard2.0/Fake.DotNet.MsBuild.dll"
#r "./packages/netcorebuild/Fake.IO.FileSystem/lib/netstandard2.0/Fake.IO.FileSystem.dll"
#r "./packages/netcorebuild/Microsoft.Build/lib/net7.0/Microsoft.Build.dll"
#r "./packages/netcorebuild/Newtonsoft.Json/lib/netstandard2.0/Newtonsoft.Json.dll"
#r "./packages/netcorebuild/NuGet.Protocol/lib/netstandard2.0/NuGet.Protocol.dll"
#r "./packages/netcorebuild/NuGet.Configuration/lib/netstandard2.0/NuGet.Configuration.dll"
#r "./packages/netcorebuild/NuGet.Common/lib/netstandard2.0/NuGet.Common.dll"
#r "./packages/netcorebuild/NuGet.Frameworks/lib/netstandard2.0/NuGet.Frameworks.dll"
#r "./packages/netcorebuild/NuGet.Versioning/lib/netstandard2.0/NuGet.Versioning.dll"
#r "./packages/netcorebuild/NuGet.Packaging/lib/netstandard2.0/NuGet.Packaging.dll"
#endif

#load "./build.base.fsx"

open KlusterKite.Build.Base

open System.IO
open System.Diagnostics

open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.IO

// builds base (system) docker images
Target.create "DockerBase" (fun _ ->
    buildDocker "klusterkite/baseworker" "Docker/KlusterKiteBaseWorkerNode"
    buildDocker "klusterkite/baseweb" "Docker/KlusterKiteBaseWebNode"
    buildDocker "klusterkite/nuget" "Docker/KlusterKiteNuget"
    buildDocker "klusterkite/postgres" "Docker/KlusterKitePostgres"
    buildDocker "klusterkite/entry" "Docker/KlusterKiteEntry"
    buildDocker "klusterkite/vpn" "Docker/KlusterKiteVpn"
    buildDocker "klusterkite/elk" "Docker/KlusterKiteELK"
    buildDocker "klusterkite/redis" "Docker/KlusterKite.Redis"
)

// builds standard docker images
Target.create "DockerContainers" (fun _ ->
    
    let buildProject (outputPath:string) (projectPath:string) = 
        let setParams (defaults:Fake.DotNet.MSBuildParams) = { 
                defaults with
                    Verbosity = Some(Fake.DotNet.Minimal)
                    Targets = ["Restore"; "Publish"]
                    RestorePackagesFlag = true
                    Properties = 
                    [
                        "Optimize", "True"
                        "DebugSymbols", "True"
                        "Configuration", "Release"
                        "TargetFramework", "netcoreapp1.1"
                        "OutputPath", outputPath
                    ]
                }
        Fake.DotNet.MSBuild.build setParams projectPath

    CleanDirs ["./build/launcher"; "./build/launcherpublish"; "./build/seed"; "./build/seedpublish"; "./build/seeder"; "./build/seederpublish"]
    buildProject (Path.GetFullPath "./build/launcher") "./build/src/KlusterKite.NodeManager/KlusterKite.NodeManager.Launcher/KlusterKite.NodeManager.Launcher.csproj"
    
    buildProject (Path.GetFullPath "./build/seed") "./KlusterKite.Core/KlusterKite.Core.Service/KlusterKite.Core.Service.csproj"

    buildProject (Path.GetFullPath "./build/seeder") "./KlusterKite.NodeManager/KlusterKite.NodeManager.Seeder.Launcher/KlusterKite.NodeManager.Seeder.Launcher.csproj"
    

    let copyLauncherData (path : string) =
        let fullPath = Path.GetFullPath(path)
        let buildDir = Path.Combine ([|fullPath; "build"|])
        let packageCacheDir = Path.Combine ([|fullPath; "packageCache"|])

        CleanDirs [buildDir; packageCacheDir]
        CopyDir buildDir "./build/launcherpublish" 
         
        CopyTo buildDir "./Docker/utils/launcher/start.sh"
        CopyTo buildDir "./nuget.exe"
        

    CleanDirs ["./Docker/KlusterKiteSeed/build"]
    CopyDir "./Docker/KlusterKiteSeed/build" "./build/seedpublish"
    buildDocker "klusterkite/seed" "Docker/KlusterKiteSeed"

    CleanDirs ["./Docker/KlusterKiteSeeder/build"]
    CopyDir "./Docker/KlusterKiteSeeder/build" "./build/seederpublish" 
    buildDocker "klusterkite/seeder" "Docker/KlusterKiteSeeder"

    copyLauncherData "./Docker/KlusterKiteWorker" |> ignore
    copyLauncherData "./Docker/KlusterKitePublisher" |> ignore
    buildDocker "klusterkite/worker" "Docker/KlusterKiteWorker"
    buildDocker "klusterkite/manager" "Docker/KlusterKiteManager"

    buildDocker "klusterkite/publisher" "Docker/KlusterKitePublisher"
    
    // building node.js web sites
    CleanDir "./Docker/KlusterKiteMonitoring/klusterkite-web/node_modules/.cache/babel-loader"
    DirectoryInfo("./Docker/KlusterKiteMonitoring/klusterkite-web/.env-local").MoveTo "./Docker/KlusterKiteMonitoring/klusterkite-web/.env"
    DirectoryInfo("./Docker/KlusterKiteMonitoring/klusterkite-web/.env").MoveTo "./Docker/KlusterKiteMonitoring/klusterkite-web/.env-build"
    
    /// Default paths to Npm
    let npmFileName =
        match Environment.isWindows with
        | true -> 
            System.Environment.GetEnvironmentVariable("PATH")
            |> fun path -> path.Split ';'
            |> Seq.tryFind (fun p -> p.Contains "nodejs")
            |> fun res ->
                match res with
                | Some npm when File.Exists (sprintf @"%snpm.cmd" npm) -> (sprintf @"%snpm.cmd" npm)
                | _ -> "./fake/build.fsx/packages/netcorebuild/Npm/content/.bin/npm.cmd"
        | _ -> 
            let info = new ProcessStartInfo("which","npm")
            info.StandardOutputEncoding <- System.Text.Encoding.UTF8
            info.RedirectStandardOutput <- true
            info.UseShellExecute        <- false
            info.CreateNoWindow         <- true
            use proc = Process.Start info
            proc.WaitForExit()
            match proc.ExitCode with
                | 0 when not proc.StandardOutput.EndOfStream ->
                  proc.StandardOutput.ReadLine()
                | _ -> "/usr/bin/npm"

    printfn "Running: %s install" npmFileName
    let result  = ["install"]
                        |> CreateProcess.fromRawCommand npmFileName 
                        |> CreateProcess.withWorkingDirectory "./Docker/KlusterKiteMonitoring/klusterkite-web" 
                        |> Proc.run

    if result.ExitCode <> 0 then failwith "Could not install npm modules"
    printfn "Running: %s run build" npmFileName
    let result  = ["run"; "build"]     
                    |> CreateProcess.fromRawCommand npmFileName 
                    |> CreateProcess.withWorkingDirectory "./Docker/KlusterKiteMonitoring/klusterkite-web" 
                    |> Proc.run
    if result.ExitCode <> 0 then failwith "Could build klusterkite-web"

    buildDocker "klusterkite/monitoring-ui" "Docker/KlusterKiteMonitoring"
    
    DirectoryInfo("./Docker/KlusterKiteMonitoring/klusterkite-web/.env-build").MoveTo "./Docker/KlusterKiteMonitoring/klusterkite-web/.env"
    DirectoryInfo("./Docker/KlusterKiteMonitoring/klusterkite-web/.env").MoveTo "./Docker/KlusterKiteMonitoring/klusterkite-web/.env-local"
)

"PrepareSources" ==> "DockerContainers"
"DockerBase" ?=> "CleanDockerImages"
"DockerContainers" ?=> "CleanDockerImages"
"DockerBase" ?=> "DockerContainers"

// prepares docker images
Target.create  "FinalBuildDocker" (fun _ -> ())
"DockerBase" ==> "FinalBuildDocker"
"DockerContainers" ==> "FinalBuildDocker"
"CleanDockerImages" ==> "FinalBuildDocker"

Target.runOrDefault "Nuget"
