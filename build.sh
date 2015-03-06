#!/bin/bash
NGD=./.nuget
NGP=$NGD/NuGet.exe
echo "Updating SSL certificates..."
mozroots --import --machine --sync
echo -n "\nChecking for nuget... "
if [ ! -f $NGP ]; then
if [ ! -d $NGD ]; then
mkdir $NGD
fi
echo -n "\nDownloading Nuget... "
wget -O $NGP http://nuget.org/nuget.exe
echo "done"
else
echo "found"
fi
echo "\nUpdating packages..."
mono ./.nuget/NuGet.exe restore
echo "done"
if [ ! -x $NGP ]; then
chmod +x $NGP
fi
FT=./build/tools/FAKE/tools/FAKE.exe
if [ ! -f $FT ]; then
mono $NGP install FAKE -OutputDirectory build/tools -ExcludeVersion
fi
if [ ! -x $FT ]; then
chmod +x $FT
fi
export TARGET=BUILD
xbuild FoundationDB.Client.sln
