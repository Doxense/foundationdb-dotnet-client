// include Fake lib
#r "packages/FAKE/tools/FakeLib.dll"

open Fake

RestorePackages()

// Properties
let buildDir = "./build/"
let testDir = "./test/"

// Targets
Target "Clean" (fun _ -> CleanDirs [ buildDir; testDir ])

let BuildProperties =
    [
        "TargetPlatform", "x64"
        "AllowUnsafeBlocks", "true"
    ]

Target "BuildApp" (fun _ -> 
    !!"src/**/*.csproj" -- "*Test*.csproj"
    |> MSBuild buildDir "Build" (("Configuration", "Release") :: BuildProperties)
    |> Log "AppBuild-Output: ")

Target "BuildTest" (fun _ -> 
    !!"src/**/*Test*.csproj"
    |> MSBuild testDir "Build" (("Configuration", "Debug") :: BuildProperties)
    |> Log "TestBuild-Output: ")

Target "Test" (fun _ -> 
    !!(testDir + "/FoundationDb.Tests.dll") |> NUnit(fun p -> 
                                             { p with DisableShadowCopy = true
                                                      OutputFile = testDir + "TestResults.xml"
                                                      ExcludeCategory = "LongRunning" }))

Target "Default" (fun _ -> trace "Hello World from FAKE")

// Dependencies
"Clean" ==> "BuildApp" ==> "BuildTest" ==> "Test" ==> "Default"

// start build
RunTargetOrDefault "Default"
