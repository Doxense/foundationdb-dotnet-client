@echo off

dotnet test FoundationDB.Tests\FoundationDB.Tests.csproj -c Debug -- RunConfiguration.TargetPlatform=x64
REM pause
