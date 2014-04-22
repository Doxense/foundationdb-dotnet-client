// include Fake lib
#r "tools/FAKE/tools/FakeLib.dll"

open Fake

RestorePackages()

// Properties
let buildDir = "./build/output/"
let nugetOutDir = buildDir + "_packages/"
let version = "0.205.0-pre"

let BuildProperties =
    [
        "Configuration", "Release"
        "TargetPlatform", "x64"
        "AllowUnsafeBlocks", "true"
    ]

Target "Clean" (fun _ -> 
    CleanDir buildDir
)
// Default target
Target "Build" (fun _ -> traceHeader "STARTING BUILD")

Target "BuildApp" (fun _ ->
    let binDirs =
        !! "src/**/bin/**"
        |> Seq.map DirectoryName
        |> Seq.distinct
        |> Seq.filter (fun f -> (f.EndsWith("Debug") || f.EndsWith("Release")) && not (f.Contains "CodeGeneration"))

    CleanDirs binDirs

    //Compile each csproj and output it seperately in build/output/PROJECTNAME
    !! "src/**/*.csproj"
    |> Seq.map(fun f -> (f, buildDir + directoryInfo(f).Name.Replace(".csproj", "")))
    |> Seq.iter(fun (f,d) -> MSBuild d "Build" BuildProperties (seq { yield f }) |> ignore)
)


Target "Test" (fun _ -> 
    !!(buildDir + "FoundationDb.Tests/FoundationDb.Tests.dll")
    |> NUnit(
        fun p -> { p with DisableShadowCopy = true
                          OutputFile = buildDir + "TestResults.xml"
                          ExcludeCategory = "LongRunning,LocalCluster" }))

let replaceVersionInNuspec nuspecFileName version =
    let re = @"(?<start>\<version\>|""FoundationDB.Client""\s?version="")[^""<>]+(?<end>\<\/version\>|"")"
    let nuspecContents = ReadFileAsString nuspecFileName
    let replacedContents = regex_replace re (sprintf "${start}%s${end}" version) nuspecContents
    WriteStringToFile false nuspecFileName replacedContents
    

Target "Release" (fun _ ->
    let projects = [ "FoundationDb.Client"; "FoundationDb.Layers.Common" ]
    CreateDir nugetOutDir
    projects
    |> List.iter (
        fun name ->
            let nuspec = sprintf @"build\%s.nuspec" name
            replaceVersionInNuspec nuspec version
            let binariesDir = sprintf "%s/%s/" buildDir name
            NuGetPack (
                fun p ->
                    { p with WorkingDir = binariesDir
                             OutputPath = binariesDir
                             Version = version}) nuspec

            let targetLoc = (buildDir + (sprintf "%s/%s.%s.nupkg" name name version))
            trace targetLoc

        (*MoveFile nugetOutDir (buildDir + (sprintf "%s/%s/.%s.nupkg" name name version))*)
    )
)

Target "Default" (fun _ -> trace "Starting build")

// Dependencies
"Clean" ==> "BuildApp" ==> "Test" ==> "Build"

// start build
RunTargetOrDefault "Build"

