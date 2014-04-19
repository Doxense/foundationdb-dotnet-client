@echo off
cls
"tools\nuget\nuget.exe" "install" "FAKE" "-OutputDirectory" "packages" "-ExcludeVersion"
"packages\FAKE\tools\Fake.exe" build.fsx
pause
