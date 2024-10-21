# FoundationDB.Client.Native

This package will redistribute the native client libraries required to connect to a FoundationDB cluster from a .NET application.

# Why is it needed

The .NET FoundationDB Binding (`FoundationDB.Client`) is a managed .NET assembly that contains the APIs and other facilities to query a FoundationDB cluster from your .NET application.

But like all other bindings, it requires a native library called `FDB Client Library` (usually `lifdb_c.so` or `fdb_c.dll`) to communicate with a compatible FoundationDB cluster.

Contrary to other database systems, a client library compiled for 7.3.x can only connect to 7.3.x servers
and will not be able to connect to older (<= 7.2.x) or newer (>= 7.4, 8.x, ...) live cluster.

This gives you two choices:
- Build your application to target a specific minor version of FoundationDB, such as `7.3`
  - You will need to also deploy a `7.3.x` cluster, and your binaries will not be compatible with any other versions.
  - You will have to rebuild your application if you decide to upgrade or download to a different minor or major version.
- Build your application to target a specific API level such as `730`.
  - You will be able to connect to a live cluster with version _at least_ 7.3.x
  - You will need to redistribute the native client libraries via a side channel, either by including them manually in a DockerFile, or installing them at the last minute during deployment

The `FoundationDB.Client.Native` package helps solve the first case, by allowing you to reference a specific version of the native libraries in your project,
and redistributes the native client library as part of the your binaries.

# What it does

The package includes the `fdb_c` client library and `fdbcli` utility for the following supported platforms
- `win-x64`
- `linux-x64`
- `linux-arm64` (aka `aarch64`)

This package can be used in two ways:

- Via the `FoundationDB.Client.Native` NuGet package, if you are also referencing the `FoundationDB.Client` NuGet package.
- Via the `.csproj` and additional MSBuild `.targets` files, if you are compiling the source of the binding as part of your application.

The assembly redistributed in the package contains a mini loader that will locate and use the correct library at runtime.

# Referencing in your project file

## Via NuGet

If you are not compiling the source of FoundationDB.Client, but instead are referencing it via the NuGet package, then you should instead add a reference to the FoundationDB.Client.Native package:

```msbuild_build_script
<ItemGroup>
	<PackageReference Include"FoundationDB.Client" Version="x.y.z" />
	<PackageReference Include"FoundationDB.Native.Client" Version="x.y.z" />
</ItemGroup>
```
			
The package will automatically copy the native libraries during build, pack or publish.

## Via Source compilation

Let's assume that you have created a git submodule called "FoundationDB" at the root of your solution folder.

To include this package in your project, simply add the following at the end of your `.csproj` file:

```msbuild_build_script
<ItemGroup>
	<ProjectReference Include="..\Path\To\FoundationDB.Client\\FoundationDB.Client.csproj" />
	<ProjectReference Include="..\Path\To\FoundationDB.Client.Native\\FoundationDB.Client.Native.csproj" />
</ItemGroup>

<Import Project="..\Path\To\FoundationDB.Native.Client\\UseNativeLibraries.targets" />
```

The imported target will automatically copy the native libraries to your project's output folder.

To check that this is working properly, navigate to your `bin/debug/net##.0/` folder,
and check that it contains the following structure:

For example, when building for .NET 9.0 in Debug configuration:
```
bin\debug\net9.0\
- YourApp.exe
  ...
- FoundationDB.Client.dll
- FoundationDB.Client.Native.dll
  ...
- runtimes\
  - win-x64\
    - native\
      - fdb_c.dll
      - fdbcli.exe
  - linux-x64\
    - native\
      - libfdb_c.so
      - fdbcli
```

# Loading the native libraries at runtime

To enable the loader to find the libraries at runtime, find the section of your application
startup logic that calls `AddFoundationDB(...)` and add a call to `UseNativeClient()` like so:

```csharp
builder.AddFoundationDb(730, options =>
{
	// other options...
		
	// instruct the loader to use the native libraries that were distributed with
	// the 'FoundationDB.Native.Client' package.
	options.UseNativeClient();

});
```

By default, this will probe the for the native libraries, and fail if they are not found,
which could happen if the application is running on a non-supported platform, or if the
files where not properly bundled with the application.
	
Specifying `UseNativeClient(allowSystemFallback: true)` will allow the loader to fall back
to the operating system mechanism for finding the native libraries, if you intend to deploy
them separately from your .NET application.

# Publishing / Packing

The package supports both Framework-Dependent/Self-Contained mode, and Portable/Platform specific.

When publishing to a specific runtime, for example `linux-x64` or `win-x64`,
the `libfdb_c.so` or `fdb_cdll_` library will copied to the same folder as your executable.

When publishing to for a portable that could run on any platform, all files for all supported
platforms will be copied into the `runtimes/{rid}/native` sub-folders.

If your are building a Docker image, you should target `linux-x64` or `linux-arm64`, in order to
only include the library for this specific platform.

# Alternatives

Another solution, is to redistribute the native libraries via a separate mechanism.

For local dev, install the fdb client libraries manually.

When publishing to Docker, you can use the existing `foundationdb/foundationdb` Docker images,
in order to copy the `libfdb_c.so` files into your own image:

Make sure to copy a version that is compatible with the live FoundationDB cluster.

Example of a `Dockerfile` that will grab v7.3.x binaries and inject them into you application container:

```Dockerfile
# Version of the FoundationDB Client Library
ARG FDB_VERSION=7.3.52

# We will need the official fdb docker image to obtain the client binaries
FROM foundationdb/foundationdb:${FDB_VERSION} as fdb

FROM mcr.microsoft.com/dotnet/aspnet:8.0

# copy the binary from the official fdb image into our target image.
COPY --from=fdb /usr/lib/libfdb_c.so /usr/lib

WORKDIR /App

COPY . /App

ENTRYPOINT ["dotnet", "MyWebApp.dll"]
```
