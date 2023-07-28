#if FAKE
#r "paket: groupref netcorebuild //"
#endif
#load "./.fake/build.fsx/intellisense.fsx"

namespace KlusterKite.Build

open System.IO
open Fake.Core

module Config =

    let testPackageName = "KlusterKite.Core"
    let buildDir = Path.GetFullPath("./build")
    let mutable packageDir = Path.GetFullPath("./packageOut")
    let packagePushDir = Path.GetFullPath("./packagePush")
    let packageThirdPartyDir = Path.GetFullPath("./packageThirdPartyDir")

    let envVersion = Environment.environVarOrDefault "version" null
    let mutable version = if envVersion <> null then envVersion else "0.0.0-local"