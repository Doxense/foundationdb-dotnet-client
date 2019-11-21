#!/bin/bash
dotnet build FoundationDB.Client/FoundationDB.Client.csproj -c Debug -f netstandard2.1 --version-suffix dev
dotnet build FoundationDB.DependencyInjection/FoundationDB.DependencyInjection.csproj -c Debug -f netstandard2.1 --version-suffix dev
dotnet build FoundationDB.Layers.Common/FoundationDB.Layers.Common.csproj -c Debug -f netstandard2.1 --version-suffix dev
dotnet build FoundationDB.Linq.Providers/FoundationDB.Linq.Providers.csproj -c Debug -f netstandard2.1 --version-suffix dev
dotnet build FdbShell/FdbShell.csproj -c Debug -f netcoreapp3.0 --version-suffix dev
dotnet build FdbTop/FdbTop.csproj -c Debug -f netcoreapp3.0 --version-suffix dev