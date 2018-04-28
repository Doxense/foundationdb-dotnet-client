@echo off

REM NuGet should already be present in the solution
if not exist .nuget\nuget.exe (
    ECHO Nuget not found.. Downloading..
    mkdir .nuget
    PowerShell -NoProfile -ExecutionPolicy Bypass -Command "& 'build\download-nuget.ps1'"
)

REM we need FAKE to process our build scripts
if not exist build\tools\FAKE\tools\Fake.exe (
    ECHO FAKE not found... Installing...
    ".nuget\nuget.exe" "install" "FAKE" "-Version" "4.64.12" "-OutputDirectory" "build\tools" "-ExcludeVersion"
)

REM we need nunit-console to run our tests
if not exist build\tools\NUnit.ConsoleRunner\tools\nunit3-console.exe (
    ECHO Nunit not found.. Installing
    ".nuget\nuget.exe" "install" "NUnit.Runners" "-OutputDirectory" "build\tools" "-ExcludeVersion" "-Prerelease"
)

SET TARGET="Build"
SET VERSION=""

IF NOT [%1]==[] (set TARGET="%1")

IF NOT [%2]==[] (set VERSION="%2")

shift
shift

"build\tools\FAKE\tools\Fake.exe" "build\build.fsx" "target=%TARGET%" "version=%VERSION%"
REM pause
