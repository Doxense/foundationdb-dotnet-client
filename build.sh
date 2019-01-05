#!/bin/bash
dotnet build FoundationDB.Client/FoundationDB.Client.csproj -c Debug -f netstandard2.0 --version-suffix dev
dotnet build FdbShell/FdbShell.csproj -c Debug -f netcoreapp2.2 --version-suffix dev
dotnet build FdbTop/FdbTop.csproj -c Debug -f netcoreapp2.2 --version-suffix dev