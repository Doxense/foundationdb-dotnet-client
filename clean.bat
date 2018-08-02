rem Workaround for "Run as administrator" changing the current directory
cd %~dp0

for /d /r . %%d in (bin,obj) do @if exist "%%d" rd /s/q "%%d"

dotnet restore FoundationDB.Client.sln
