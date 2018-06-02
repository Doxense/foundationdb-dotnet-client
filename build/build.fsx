// include Fake lib
#r "tools/FAKE/tools/FakeLib.dll"

open Fake
open Fake.Testing

let projectRoot () =
    if FileUtils.pwd().EndsWith("build") then
        FileUtils.pwd() @@ ".."
    else
        FileUtils.pwd()

// Properties
let version = "5.2.0-preview1" //TODO: find a way to extract this from somewhere convenient
let buildDir = projectRoot() @@ "build" @@ "output"
let nugetPath = projectRoot() @@ ".nuget" @@ "NuGet.exe"
let nugetOutDir = buildDir @@ "_packages"

let BuildProperties =
    [
        "TargetPlatform", "AnyCPU"
        "AllowUnsafeBlocks", "true"
    ]

Target "Clean" (fun _ ->
    CleanDir buildDir
)

Target "PackageRestore" (fun _ ->
    !! "./**/packages.config"
    |> Seq.iter (RestorePackage (fun p -> {p with ToolPath = nugetPath; OutputPath = projectRoot() @@ "packages"}))
)

// Default target
Target "Build" (fun _ -> traceHeader "STARTING BUILD")

let buildProject mode =
    let binDirs =
        !! (projectRoot() @@ "**" @@ "bin" @@ "**")
        |> Seq.map DirectoryName
        |> Seq.distinct
        |> Seq.filter (fun f -> (f.EndsWith("Debug") || f.EndsWith("Release")) && not (f.Contains "CodeGeneration"))

    CleanDirs binDirs

    //Compile each csproj and output it separately in build/output/PROJECTNAME
    !! (projectRoot() @@ "**" @@ "*.csproj")
    |> Seq.map(fun f -> (f, buildDir @@ directoryInfo(f).Name.Replace(".csproj", "")))
    |> Seq.iter(fun (f,d) -> MSBuild d "Build" (BuildProperties @ [ "Configuration", mode ]) (seq { yield f }) |> ignore)

Target "BuildAppRelease" (fun _ ->
    traceHeader "BUILDING APP (Release)"
    buildProject "Release"
)

Target "BuildAppDebug" (fun _ ->
    traceHeader "BUILDING APP (Debug)"
    buildProject "Debug"
)

Target "Test" (fun _ ->
    traceHeader "RUNNING UNIT TESTS"
    let testDir = buildDir @@ "tests"
    CreateDir testDir
    ActivateFinalTarget "CloseTestRunner"
    !! (buildDir @@ "**" @@ "*Test*.dll")
    |> NUnit3(
        fun p -> { p with ShadowCopy = false
                          //ResultSpecs = "TestResults.xml"
                          StopOnError = false
                          ErrorLevel = DontFailBuild
                          WorkingDir = testDir
                          TimeOut = System.TimeSpan.FromMinutes 10.0
                          Where = "cat != LongRunning && cat != LocalCluster" }))

FinalTarget "CloseTestRunner" (fun _ ->
    ProcessHelper.killProcess "nunit-agent.exe"
)

Target "Release" (fun _ ->
    traceHeader "BUILDING RELEASE"
)

let replaceVersionInNuspec nuspecFileName version =
    let re = @"(?<start>\<version\>|""FoundationDB.Client""\s?version="")[^""<>]+(?<end>\<\/version\>|"")"
    let nuspecContents = ReadFileAsString nuspecFileName
    let replacedContents = regex_replace re (sprintf "${start}%s${end}" version) nuspecContents
    WriteStringToFile false nuspecFileName replacedContents

Target "BuildNuget" (fun _ ->
    trace "Building Nuget Packages"
    let projects = [ "FoundationDB.Client"; "FoundationDB.Layers.Common" ]
    CreateDir nugetOutDir
    projects
    |> List.iter (
        fun name ->
            let nuspec = projectRoot() @@ "build" @@ (sprintf "%s.nuspec" name)
            replaceVersionInNuspec nuspec version
            let binariesDir = buildDir @@ name

            // Copy XML doc to binaries dir, works by default on windows but not on Mono.
            let xmlDocFile = projectRoot() @@ name @@ "bin" @@ "Release" @@ "netstandard2.0" @@ (sprintf "%s.XML") name
            FileUtils.cp xmlDocFile binariesDir

            NuGetPack (
                fun p ->
                    { p with WorkingDir = binariesDir
                             OutputPath = nugetOutDir
                             ToolPath = nugetPath
                             Version = version}) nuspec

            let targetLoc = (buildDir @@ name @@ (sprintf "%s.%s.nupkg" name version))
            trace targetLoc

        (*MoveFile nugetOutDir (buildDir + (sprintf "%s/%s/.%s.nupkg" name name version))*)
    )
)

Target "Default" (fun _ -> trace "Starting build")

// Dependencies
"Clean" ==> "PackageRestore" ==> "BuildAppDebug" ==> "Test" ==> "Build"
"Clean" ==> "PackageRestore" ==> "BuildAppRelease" ==> "BuildNuget" ==> "Release"

// start build
RunTargetOrDefault "Build"
