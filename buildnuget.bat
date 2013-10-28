@echo off

echo "Building Solution..."
msbuild "FoundationDb.Client.sln" /t:Clean,Build /property:Configuration="Release"
IF ERRORLEVEL 1 EXIT /B 1

echo "Building Packages..."
rmdir /s /q nuget
mkdir nuget
pushd nuget
..\.nuget\NuGet.exe pack ..\FoundationDB.Client\FoundationDB.Client.csproj -Symbols -Prop Configuration=Release
IF ERRORLEVEL 1 EXIT /B 1

..\.nuget\NuGet.exe pack ..\FoundationDB.Layers.Common\FoundationDb.Layers.Common.csproj -Symbols -Prop Configuration=Release
IF ERRORLEVEL 1 EXIT /B 1

popd
echo "Done"