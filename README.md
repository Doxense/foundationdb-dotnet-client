FoundationDB .NET Client
=======================

C#/.NET binding for the [FoundationDB](https://www.foundationdb.org/) client library.

[![.NET Build](https://github.com/Doxense/foundationdb-dotnet-client/actions/workflows/dotnetcore.yml/badge.svg)](https://github.com/Doxense/foundationdb-dotnet-client/actions/workflows/dotnetcore.yml)

# How to use

You will need to install two things:
- A copy of the FoundationDB client library, available at https://github.com/apple/foundationdb/releases
- A reference to the `FoundationDB.Client` package, and supporting packages.

For local development, using [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/) will also make it easier to spin up a working environment in a few minutes using a locally hosted Docker container (requires [Docker Desktop](https://www.docker.com/products/docker-desktop/) on Windows)

## Using Dependency Injection

Even though it is possible to use the binding without dependency injection, it is very easy to register the `IFdbDatabaseProvider` service with the DI, and inject it into any controller, razor pages or any other service that will need to query the database.

You can either manually setup the services, in which case you will need to provide a valid set of settings (API level, root path, ...) as well as copy a valid `fdb.cluster` file so that the process can connect to an existing FoundationDB cluster.

You can also use .NET Aspire to automatically setup a local FoundationDB docker container, and automatically generate valid connection strings.

### Manual configuration

In your Program.cs, you should register FoundationDB with the DI container:

```CSharp
using FoundationDB.Client; // this is the main namespace of the library

var builder = WebApplication.CreateBuilder(args);

// ...

// hook-up the various FoundationDB types and interfaces with the DI container
// You MUST select the appropriate API level that matches the target cluster (here '710' requires at least v7.1)
builder.Services.AddFoundationDb(710, options =>
{
    // auto-start the connection to the cluster on the first request
    options.AutoStart = true; 

    //you can configure additional options here, like the path to the .cluster file, default timeouts, ...
});

var app = builder.Build();

// ...

// note: you don't need to configure anything for this step

app.Run();

```

This will register an instance of the `IFdbDatabaseProvider` singleton, that you can then inject into other types.

Let say, for example, that we have a `Books` Razor Page, that is reachable via the `/Books/{id}` route:

- We first inject an instance of the `IFdbDatabaseProvider` via the constructor.
- Inside the `OnGet(...)` action, we can call any of the `ReadAsync`, `ReadWriteAsync` or `WriteAsync` methods on this instance, to start a transaction retry-loop.
- Inside the retry-loop, we get passed either an `IFdbReadOnlyTransaction` (read-only) or an `IFdbTransaction` (read-write).
- We use this transaction to read the value of the `("Books", <id>)` key from the database.
  - Please DO NOT mutate any global state from within the transaction handler! The handler could be called MULTIPLE TIMES if there are any conflicts or retryable errors!
  - Try to perform any pre-processing or post-processing OUTSIDE of the retry-loop. Remember, the transaction instance is only valid for 5 seconds!
- After the retry-loop, we can inspect the result:
  - if the key does not exist, then `GetAsync(...)` will return `Slice.Nil`.
  - if the key does exist, then `GetAsync(...)` returns a Slice containing the bytes of the value (which are expected to be a JSON encoded document)
- We de-serialize the JSON document into a `Book` record, that we can then pass to the Razor Template to be rendered into an HTML page.

```c#
namespace MyWebApp.Pages
{

    using FoundationDB.Client;

    /// <summary>Represent a Book that will be stored (as JSON) into the database</summary>
    public sealed record Book
    {
        public required string Id { get; init; }

        public required string Title { get; init; }

        public required string ISBN { get; init; }

        public required string AuthorId { get; init; }

        // ...

    }

    /// <summary>This page is used to display the details of a specific book</summary>
    /// <remarks>Accessible via the route '/Books/{id}'</remarks>
    public class BooksModel : PageModel
    {

        public BooksModel(IFdbDatabaseProvider db)
        {
            this.Db = db;
        }

        private IFdbDatabaseProvider Db { get; }

        public Book Book { get; private set; }

        public async Task OnGet(string id, CancellationToken ct)
        {
            // perform parameter validation, ACL checks, and any pre-processing here

            // start a read-only retry-loop
            Slice jsonBytes = await this.Db.ReadAsync((IFdbReadOnlyTransaction tr) =>
            {
                // Read the value of the ("Books", <ID>) key
                Slice value = await tr.GetAsync(TuPack.Pack(("Books", id)));

                // the transaction can be used to read additional keys and ranges,
                // and has a lifetime of max. 5 secondes.

                return value;
            }, ct);

            // here you can perform any post-processing of the result, outside of the retry-loop

            // if the key does not exist in the database, GetAsync(...) will return Slice.Nil
            if (jsonBytes.IsNull)
            {
                // This book does not exist, return a 404 page to the browser!
                return NotFound();
            }

            // If the key exists, then GetAsync(...) will return its value as bytes, that can be deserialized
            Book book = JsonSerializer.Deserialize<Book>(jsonBytes.Span);

            // perform any checks and validation here, like converting the Model (from the database) into a ViewModel (for the razor template)

            this.Book = book;
        }
    }
}
```

### Using Aspire

It is possible to add a FoundationDB cluster resource to your Aspire application model, and pass a reference to this cluster to the projects that need it.

For local development, a local FoundationDB node will be started using the `foundationdb/foundationdb` Docker image, and all projects that use the cluster reference will have a temporary Cluster file pointing to the local instance.

Note: you will need to install Docker on your development machine, as explained in https://learn.microsoft.com/en-us/dotnet/aspire/get-started/add-aspire-existing-app#prerequisites

In the Program.cs of you AppHost project:
```c#
private static void Main(string[] args)
{
    var builder = DistributedApplication.CreateBuilder(args);

    // Define a locally hosted FoundationDB cluster
    var fdb = builder
        .AddFoundationDb("fdb", apiVersion: 720, root: "/Sandbox/MySuperApp", clusterVersion: "7.2.5", rollForward: FdbVersionPolicy.Exact);

    // Project that needs a reference to this cluster
    var backend = builder
        .AddProject<Projects.AwesomeWebApiBackend>("backend")
        //...
        .WithReference(fdb); // register the fdb cluster connection

    // ...
}
```

Note: A FoundationDB node will be available on port 4550, while the Aspire Host is running, and can be reached with the `docker:docker@127.0.0.1:4550` connection string.

On the very first start, the cluster will be unavailable until a `create single ssd` command is executed. The most simple solution is to use `Docker Desktop` to execute a command in the running docker image. Simply run `fdbcli` from within the docker container. Once this is done, you should stop and restart the Aspire host.

For testing/staging/production, or "non local" development, it is also possible to configure a FoundationDB connection resource that will pass the specified Cluster file to the projects that reference the cluster resource.

In the Program.cs of your AppHost project:
```c#
private static void Main(string[] args)
{
    var builder = DistributedApplication.CreateBuilder(args);

    // Define an external FoundationDB cluster connection
    var fdb = builder
        .AddFoundationDbCluster("fdb", apiVersion: 720, root: "/Sandbox/MySuperApp", clusterFile: "/SOME/PATH/TO/testing.cluster")		;

    // Project that needs a reference to this cluster
    var backend = builder
        .AddProject<Projects.AwesomeWebApiBackend>("backend")
        //...
        .WithReference(fdb); // register the fdb cluster connection

    // ...
}
```

Then, in the Program.cs, or where you are declaring your services with the DI, use the following extension method to add support for FoundationDB:

```c#
var builder = WebApplication.CreateBuilder(args);

// setup Aspire services...
builder.AddServiceDefaults();
//...

// hookup the FoundationDB component
builder.AddFoundationDb("fdb"); // "fdb" is the same name we used in AddFoundationDb(...) or AddFoundationDbCLuster(...) in the AppHost above.

// ...rest of the startup logic....
```

This will automatically register an instance of the `IFdbDatabaseProvider` service, automatically configured to connect the FDB local or external cluster defined in the AppHost.

## Using the Directory Layer

Please note that in real use case, it is highly encourage to use the Directory Layer to generate a prefix for the keys, instead of simply using the `("Books", ...)` prefix.

In your startup logic:
```c#

public sealed class BookOptions
{

    /// <summary>Path to the root directory subspace of the application where all data will be stored</summary>
    public FdbPath Location { get; set; } // ex: "/Tenants/ACME/MyApp/v1"

}

// ...

builder.Services.Configure<BookOptions>(options =>
{
    // note: this would be read from your configuration!
    options.Location = FdbPath.Relative("Tenants", "ACME", "MyApp", "v1");
});

```

In your Razor Page:

```c#
public class BooksModel : PageModel
{

    public BooksModel(IOptions<BookOptions> options, IFdbDatabaseProvider db)
    {
        this.Options = options;
        this.Db = db;
    }

    private IFdbDatabaseProvider Db { get; }

    private IOptions<BookOptions> Options { get; }

    public async Task OnGet(string id, CancellationToken ct)
    {
        Slice jsonBytes = await this.Db.ReadAsync((IFdbReadOnlyTransaction tr) =>
        {
            // get the location that corresponds to this path
            var location = this.Db.Root[this.Options.Value.Location];

            // "resolve" this location into a Directory Subspace that will add the matching prefix to our keys
            var subspace = await location.Resolve(tr);

            // use this subspace to generate our keys
            Slice value = await tr.GetAsync(subspace.Encode("Books", id));

            // ....

        }

        // ...

    }

}
```

## Access the underlying `IFdbDatabase` singleton

The `IFdbDatabaseProvider` also has a `GetDatabase(...)` method that can be used to obtain an instance of the `IFdbDatabase` singleton, that can then be used directly, or passed to any other Layer or library.

```c#
public class FooBarModel : PageModel
{

    public FooBarModel(IFdbDatabaseProvider db, IFooBarLayer layer)
    {
        this.Db = db;
        this.Layer = layer;
    }

    private IFdbDatabaseProvider Db { get; }

    private IFooBarLayer Layer { get; }

    public List<Foo> Results { get; }

    public async Task OnGet(...., CancellationToken ct)
    {
        // get an instance of the database singleton
        var db = await this.Db.GetDatabase(ct);
        // notes:
        // - if AutoStart is false, this will throw an exception if the provider has not been started manually during starting.
        // - if AutoStart is true, the very first call will automatically start the connection.
        // - Once the connection has been established, calls to GetDatabase will return an already-completed task with a cached singleton (or exception).

        // call some method on this layer, that will perform a query on the database and return a list of results
        this.Results = await this.Layer.Query(db, ...., ct);
    }

}
```

# Deployment

## Docker containers

The easiest way to deploy is to use one of the [ASP.NET Core Runtime docker images](https://hub.docker.com/r/microsoft/dotnet-aspnet/) provided by Microsoft, such as `mcr.microsoft.com/dotnet/aspnet:8.0` or newer.

In order to function, the FoundationDB Native client library (`fdb_c.dll` on Windows, `libfdb_c.so`) needs to be present in the container image. The easiest way is to simply copy them from the [FoundationDB Docker image](https://hub.docker.com/r/foundationdb/foundationdb) that contains these files.

Example of a `Dockerfile` that will grab v7.3.x binaries and inject them into you application container:

```Dockerfile
# Version of the FoundationDB Client Library
ARG FDB_VERSION=7.3.38

# We will need the official fdb docker image to obtain the client binaries
FROM foundationdb/foundationdb:${FDB_VERSION} as fdb

FROM mcr.microsoft.com/dotnet/aspnet:7.0

# copy the binary from the official fdb image into our target image.
COPY --from=fdb /usr/lib/libfdb_c.so /usr/lib

WORKDIR /App

COPY . /App

ENTRYPOINT ["dotnet", "MyWebApp.dll"]
```

## Manual deployment

The easiest solution is to install the `foundationdb-clients-X.Y.Z` packages from `https://apple.github.io/foundationdb/downloads.html`. Only the client packages should be installed, unless you also intend to run the cluster locally.

If you are manually copying your application files to the destination, either by unzip into a folder, or using a single-exe deployment, it is still necessary to also copy the `fdb_c.dll` or `libfdb_c.so` binaries to the destination

If, for any reason, you cannot copy the client binary to the default platform location (ex: `/usr/lib` on Linux), you can specify the full path to the library by settings the `NativeLibraryPath` option, or setting the `Aspire:FoundationDb:Client:NativeLibraryPath` key in the `appSettings.json` file (see the `FdbClientSettings` class other available settings).

If you need to troubleshoot the connection to the FoundationDB cluster, from the point of view of your application, it is also recommended to install `fdbcli` (comes with the `foundationdb-clients` package, needs to be manually deployed if not).

# How to build

## Visual Studio Solution

You will need Visual Studio 2022 version 17.12 or above to build the solution (C# 13 and .NET 9.0 support is required).

### From the Command Line

You can also build, test and compile the NuGet packages from the command line using the `dotnet` CLI:

- `dotnet build` to build (in DEBUG) all the projects in the solution
- `dotnet test` to run the unit tests (requires a working local FoundationDB cluster).

### As a sub-module

Most projects in this repository are targeting multiple frameworks, meaning that each project will be build several times, one for each target.

When consuming this repository as a sub-module inside another repository, all the included projects will still want to build for all these targets, even if your parent solution only targets one framework (or a different subset).

This can also cause issues if you application is targeting an older .NET runtime and SDK (for example `net9.0` using the .NET 9.0.x SDK), which do not support more recent targets from this repo (ex: `net10.0`).

By default, the `Directory.Build.props` will attempt to detect when it is inside a git sub-module, and import any `Directory.Build.props` in the parent directory.
_note: Some CI build environments may checkout sub-module in non-standard way. If this happens, you can set the environment variable `FDB_BUILD_PROPS_OVERRIDE` to `1` in order to bypass the check._

This parent props file can then override a series of msbuild variables that are injected in the `TargetFrameworks` property of all `.csproj` in this repo:
- `CoreSdkVersions`: overrides the value of all the other variables at once. Use this is you are single-targeting.

If you are multi-targeting and need more fine grained precision, you can use the following variables:
- `CoreSdkRuntimeVersions`: targets for all the core libraries (FoundationDB.Client.dll, ...) that are redistributed
- `CoreSdkToolsVersions`: targets for all the tools and executables (FdbShell, FdbTop, ...) that are redistributed
- `CoreSdkUtilityVersions`: targets for all the internal tools and executables that are only used for building, testing, and are not expected to be redistributed.
- `CloudSdkRuntimeVersions`: targets for all libraries that reference .NET Aspire (which is only supports .NET 8 or later).

If you parent repository is also multi-targeting, you can specify several targets, like for example `net9.0;net10.0`. Please note that is you target a more recent framework that is not supported by this repo, they may fail to build properly!

An example of a parent `Directory.Build.props` that overrides the build to only target `net9.0`:
```xml
<Project>
	<PropertyGroup>

		<!-- Force all projects in the FoundationDB sub-module to target net9.0 -->
		<CoreSdkVersions>net9.0</CoreSdkVersions>

	</PropertyGroup>
</Project>
```

An example of a parent `Directory.Build.props` that multi-targets `net9.0` and `net10.0`, but only want to build the tools for `net10.0`:
```xml
<Project>
	<PropertyGroup>

		<!-- If you are using FoundationDB.Client, SnowBank.Core, etc... -->
		<CoreSdkRuntimeVersions>net9.0;net10.0</CoreSdkRuntimeVersions>

		<!-- If you are using the FoundationDB .NET Aspire Integration -->
		<CloudSdkRuntimeVersions>net9.0;net10.0</CloudSdkRuntimeVersions>

		<!-- If you are using any of the tools (FdbShell, FdbTop) -->
		<CoreSdkToolsVersions>net10.0</CoreSdkToolsVersions>

	</PropertyGroup>
</Project>
```

# How to test

The test projects are using NUnit 4, and the test running must run as a 64-bit process (32-bit is not supported).

> In order to run the tests, you will also need to obtain the 'fdb_c.dll'/`libfdb_c.so` native library.

You can either run the tests from Visual Studio or Visual Studio Code, using any extension (like ReSharper), or from the command line via `dotnet test`.

> WARNING: All the tests try to run in a dedicated subspace, but there is a possibility of data corruption if they are running against a test or staging cluster! You should run the test against a local cluster where all the data is considered expandable!

# Implementation Notes

Please refer to https://apple.github.io/foundationdb/ to get an overview on the FoundationDB API, if you haven't already.

This .NET binding has been modeled to be as close as possible to the other bindings (Python especially), while still having a '.NET' style API. 

There were a few design goals, that you may agree with or not:
* Reducing the need to allocate `byte[]` as much as possible. To achieve that, I'm using a `Slice` struct that is the logical equivalent of `ReadOnlyMemory<byte>`, but more versatile.
* Mapping FoundationDB's Future into `Task<T>` to be able to use `async`/`await`. 
* Reducing the risks of memory leaks in long running server processes by wrapping all FDB_xxx handles with .NET `SafeHandle`. This adds a little overhead when P/Invoking into native code, but will guarantee that all handles get released at some time (during the next GC).
* The Tuple layer has also been optimized to reduce the number of allocations required, and cache the packed bytes of often used tuples (in subspaces, for example).

However, there are some key differences between Python and .NET that may cause problems:
* Python's dynamic types and auto casting of Tuples values, are difficult to model in .NET (without relying on the DLR). The Tuple implementation try to be as dynamic as possible, but if you want to be safe, please try to only use strings, longs, bools and byte[] to be 100% compatible with other bindings. You should refrain from using the untyped `tuple[index]` indexer (that returns an object), and instead use the generic `tuple.Get<T>(index)` that will try to adapt the underlying type into a T.
* The Tuple layer uses ASCII and Unicode strings, while .NET only have Unicode strings. That means that all strings in .NET will be packed with prefix type 0x02 and byte arrays with prefix type 0x01. An ASCII string packed in Python will be seen as a byte[] unless you use `ITuple.Get<string>()` that will automatically convert it to Unicode.
* There is no dedicated 'UUID' type prefix, so that means that `System.Guid` would be serialized as byte arrays, and all instances of byte 0 would need to be escaped. Since `System.Guid` are frequently used as primary keys, I added a new custom type prefix (0x30) for 128-bits UUIDs and (0x31) for 64-bits UUIDs. This simplifies packing/unpacking and speeds up writing/reading/comparing Guid keys.

The following files will be required by your application
* `FoundationDB.Client.dll` : Contains the core types (FdbDatabase, FdbTransaction, ...) and infrastructure to connect to a FoundationDB cluster and execute basic queries, as well as the Tuple and Subspace layers.
* `FoundationDB.Layers.Commmon.dll` : Contains common Layers that emulates Tables, Indexes, Document Collections, Blobs, ...
* `fdb_c.dll`/`libfdb_c.so` : The native C client that you will need to obtain from the official FoundationDB Windows setup or Linux client packages.

# Known Limitations

* Since the native FoundationDB client is 64-bit only, this .NET library is also for 64-bit only applications! Even though it targets AnyCPU, it would fail at runtime. _Don't forget to disable the `Prefer 32-bit` option in your project Build properties, that is enabled by default!_ 
* You cannot unload the fdb C native client from the process once the network thread has started. You can stop the network thread once, but it does not support being restarted. This can cause problems when running under ASP.NET.
* FoundationDB does not support long running batch or range queries if they take too much time. Such queries will fail with a 'past_version' error. The current maximum duration for read transactions is 5 seconds.
* FoundationDB has a maximum allowed size of 100,000 bytes for values, and 10,000 bytes for keys. Larger values must be split into multiple keys
* FoundationDB has a maximum allowed size of 10,000,000 bytes for writes per transactions (some of all key+values that are mutated). You need multiple transaction if you need to store more data. There is a Bulk API (`Fdb.Bulk.*`) to help for the most common cases (import, export, backup/restore, ...)
* See https://apple.github.io/foundationdb/known-limitations.html for other known limitations of the FoundationDB database.

# License

This code is licensed under the 3-clause BSD License.

# Contributing

* Yes, we use tabs! Get over it.
* Style rules are encoded in `.editorconfig` which is supported by most IDEs (or via extensions).
* You can visit the FoundationDB forums for generic questions (not .NET): https://forums.foundationdb.org/
