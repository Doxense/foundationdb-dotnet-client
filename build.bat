@echo off
cls
"tools\nuget\nuget.exe" "install" "FAKE" "-OutputDirectory" "packages" "-ExcludeVersion"
"tools\nuget\nuget.exe" "install" "Nunit.Runners.lite" "-OutputDirectory" "tools" "-ExcludeVersion"
"packages\FAKE\tools\Fake.exe" build.fsx
pause
