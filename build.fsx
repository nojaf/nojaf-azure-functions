#r "paket:
nuget Fake.Core.Target
nuget Fake.IO.FileSystem
nuget Fake.DotNet.Cli //"
#load "./.fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators
open Fake.DotNet
open System.IO

let sln = Path.Combine(__SOURCE_DIRECTORY__, "nojaf-azure-functions.sln")
let tests =  Path.Combine(__SOURCE_DIRECTORY__, "tests", "AzureFunctions.Tests", "AzureFunctions.Tests.fsproj")

// Default target
Target.create "Fake" (fun _ ->
  Trace.trace "Fake magic"
)

Target.create "Clean" (fun _ ->
    !! "src/bin"
    ++ "src/obj"
    ++ "tests/**/bin"
    ++ "tests/**/obj"
    |> Seq.iter Shell.cleanDir
)

Target.create "Restore" (fun _ ->
    DotNet.restore id sln
)

Target.create "Build" (fun _ ->
    DotNet.build (fun opt -> { opt with
                                       Configuration = DotNet.BuildConfiguration.Release
                                       Common = { opt.Common with CustomParams = Some "--no-restore" } }) sln
    
)

Target.create "Tests" (fun _ ->
    DotNet.test (fun opt -> { opt with
                                  NoBuild = true
                                  NoRestore = true
                                  Configuration = DotNet.BuildConfiguration.Release
                                  }) tests
)

// Build order

"Clean"
    ==> "Restore"
    ==> "Build"
    ==> "Tests"

// start build
Target.runOrDefault "Tests"