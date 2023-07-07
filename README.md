FoundationDB .NET Client
=======================

C#/.NET binding for the [FoundationDB](https://www.foundationdb.org/) client library.

[![Build status](https://ci.appveyor.com/api/projects/status/83u4pd2ckevdtb57?svg=true)](https://ci.appveyor.com/project/KrzysFR/foundationdb-dotnet-client)
[![Actions Status](https://github.com/doxense/foundationdb-dotnet-client/workflows/.NET%20Core/badge.svg)](https://github.com/doxense/foundationdb-dotnet-client/actions)

How to use
----------

You will need to install two things:
- A copy of the FoundationDB client library, available at https://github.com/apple/foundationdb/releases
- A reference to the `FoundationDB.Client` package.

#### Using Dependency Injection

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

This will register an instance of the `IFdbDatabaseProvider` singleton, that you can then inject into your controllers, razor pages or any other services.

Let say, for example, that we have a `Books` Razor Page, that is reachable via the `/Books/{id}` route:

- We first inject an instance of the IFdbDatabaseProvider via the constructor.
- Inside the `OnGet(...)` action, we can call any of the `ReadAsync`, `ReadWriteAsync` or `WriteAsync` methods on this instance, to start a transaction retry-loop.
- Inside the retry-loop, we get passed either an `IFdbReadOnlyTransaction` (read-only) or an `IFdbTransaction` (read-write).
- We use this transaction to read the value of the `("Books", <id>)` key from the database.
  - Please DO NOT mutate any global state from within the transaction handler! The handler could be called MULTIPLE TIMES if there are any conflicts or retryable errors!
  - Try to perform any pre-processing or post-processing OUTSIDE of the retry-loop. Remember, the transaction instance is only valid for 5 seconds!
- After the retry-loop, we can inspect the result:
  - if the key does not exist, then `GetAsync(...)` will return `Slice.Nil`.
  - if the key does exist, then `GetAsync(...)` returns a Slice containing the bytes of the value (which are expected to be a JSON encoded document)
- We de-serialize the JSON document into a `Book` record, that we can then pass to the Razor Template to be rendered into an HTML page.

```CSharp

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

#### Using the Directory Layer

Please note that in real use case, it is highly encourage to use the Directory Layer to generate a prefix for the keys, instead of simply using the `("Books", ...)` prefix.

In your startup logic:
```CSharp

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

```CSharp
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

#### Access the underlying `IFdbDatabase` singleton

The `IFdbDatabaseProvider` also has a `GetDatabase(...)` method that can be used to obtain an instance of the `IFdbDatabase` singleton, that can then be used directly, or passed to any other Layer or library.

```
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

Hosting
-------

#### Hosting on ASP.NET Core / Kestrel

* The simplest solution is to inject an instance of `IFdbDatabaseProvider` in all your controllers, pages and services.
* The .NET Binding can be configured from your Startup or Program class, by reading configuration from your `appsettings.json` or by environment variable at runtime.
* If you are publishing your web application as Docker images, you have to inject the `fdb_c.dll` or `libfdb_c.so` binary in your docket image.

Here is an easy way to inject the client binary in a Dockerfile:

```
# Version of the FoundationDB Client Library
ARG FDB_VERSION=7.2.9

# We will need the official fdb docker image to obtain the client binaries
FROM foundationdb/foundationdb:${FDB_VERSION} as fdb

FROM mcr.microsoft.com/dotnet/aspnet:7.0

# copy the binary from the official fdb image into our target image.
COPY --from=fdb /usr/lib/libfdb_c.so /usr/lib

WORKDIR /App

COPY . /App

ENTRYPOINT ["dotnet", "MyWebApp.dll"]
```

#### Hosting on IIS


* The .NET API is async-only, and should only be called inside async methods. You should NEVER write something like `tr.GetAsync(...).Wait()` or `tr.GetAsync(...).Result` because it will GREATLY degrade performances and prevent you from scaling up past a few concurrent requests.
* The underlying client library will not run on a 32-bit Application Pool. You will need to move your web application to a 64-bit Application Pool.
* If you are using IIS Express with an ASP.NET or ASP.NET MVC application from Visual Studio, you need to configure your IIS Express instance to run in 64-bit. With Visual Studio 2013, this can be done by checking Tools | Options | Projects and Solutions | Web Projects | Use the 64 bit version of IIS Express for web sites and projects
* The fdb_c.dll library can only be started once per process. This makes impractical to run an web application running inside a dedicated Application Domain alongside other application, on a shared host process. The only current workaround is to have a dedicated host process for this application, by making it run inside its own Application Pool.
* If you don't use the host's CancellationToken for transactions and retry loops, deadlock can occur if the FoundationDB cluster is unavailable or under very heavy load. Please consider also using safe values for the DefaultTimeout and DefaultRetryLimit settings.

#### Hosting on OWIN

* There are no particular restrictions, apart from requiring a 64-bit OWIN host.
* You should explicitly call Fdb.Stop() when your OWIN host process shuts down, in order to ensure that any pending transaction gets cancelled properly.

How to build
------------

#### Visual Studio Solution

You will need Visual Studio 2022 version 17.5 or above to build the solution (C# 11 and .NET 6.0 support is required).

You will also need to obtain the 'fdb_c.dll' C API binding from the foundationdb.org website, by installing the client SDK:

* Go to https://github.com/apple/foundationdb/releases and download the Windows x64 MSI for the corresponding version.
* Install the MSI, selecting the default options.
* Go to `C:\Program Files\foundationdb\bin\` and make sure that `fdb_c.dll` is there.
* Open the FoundationDb.Client.sln file in Visual Studio 2022.
* Choose the Release or Debug configuration, and rebuild the solution.

#### From the Command Line

You can also build, test and compile the NuGet packages from the command line, using FAKE.

You will need to perform the same steps as above (download and install FoundationDB)

In a new Command Prompt, go the root folder of the solution and run one of the following commands:
- `build Test`: to build the solution (Debug) and run the unit tests.
- `build Release`: to build the solution (Release) and the NuGet packages (which will be copied to `.\build\output\_packages\`)
- `build BuildAppRelease`: only build the solution (Release)
- `build BuildAppDebug`: only build the solution (Debug)
- `build Clean`: clean the solution
 
If you get `System.UnauthorizedAccessException: Access to the path './build/output/FoundationDB.Tests\FoundationDB.Client.dll' is denied.` errors from time to time, you need to kill any `nunit-agent.exe` process that may have stuck around.

How to test
-----------

The test project is using NUnit 3.x.

If you are using a custom runner or VS plugin (like TestDriven.net), make sure that it has the correct nunit version, and that it is configured to run the test using 64-bit process. The code will NOT work on 32 bit.

WARNING: All the tests should work under the ('T',) subspace, but any bug or mistake could end up wiping or corrupting the global keyspace and you may lose all your data. You can specify an alternative cluster file to use in `TestHelper.cs` file.

Implementation Notes
--------------------

Please refer to https://apple.github.io/foundationdb/ to get an overview on the FoundationDB API, if you haven't already.

This .NET binding has been modeled to be as close as possible to the other bindings (Python especially), while still having a '.NET' style API. 

There were a few design goals, that you may agree with or not:
* Reducing the need to allocate byte[] as much as possible. To achieve that, I'm using a 'Slice' struct that is a glorified `ArraySegment<byte>`. All allocations made during a request try to use a single underlying byte[] array, and split it into several chunks.
* Mapping FoundationDB's Future into `Task<T>` to be able to use async/await. This means that .NET 4.5 is required to use this binding. It would be possible to port the binding to .NET 4.0 using the `Microsoft.Bcl.Async` nuget package.
* Reducing the risks of memory leaks in long running server processes by wrapping all FDB_xxx handles with .NET `SafeHandle`. This adds a little overhead when P/Invoking into native code, but will guarantee that all handles get released at some time (during the next GC).
* The Tuple layer has also been optimized to reduce the number of allocations required, and cache the packed bytes of oftenly used tuples (in subspaces, for example).

However, there are some key differences between Python and .NET that may cause problems:
* Python's dynamic types and auto casting of Tuples values, are difficult to model in .NET (without relying on the DLR). The Tuple implementation try to be as dynamic as possible, but if you want to be safe, please try to only use strings, longs, booleans and byte[] to be 100% compatible with other bindings. You should refrain from using the untyped `tuple[index]` indexer (that returns an object), and instead use the generic `tuple.Get<T>(index)` that will try to adapt the underlying type into a T.
* The Tuple layer uses ASCII and Unicode strings, while .NET only have Unicode strings. That means that all strings in .NET will be packed with prefix type 0x02 and byte arrays with prefix type 0x01. An ASCII string packed in Python will be seen as a byte[] unless you use `ITuple.Get<string>()` that will automatically convert it to Unicode.
* There is no dedicated 'UUID' type prefix, so that means that System.Guid would be serialized as byte arrays, and all instances of byte 0 would need to be escaped. Since `System.Guid` are frequently used as primary keys, I added a new custom type prefix (0x30) for 128-bits UUIDs and (0x31) for 64-bits UUIDs. This simplifies packing/unpacking and speeds up writing/reading/comparing Guid keys.

The following files will be required by your application
* `FoundationDB.Client.dll` : Contains the core types (FdbDatabase, FdbTransaction, ...) and infrastructure to connect to a FoundationDB cluster and execute basic queries, as well as the Tuple and Subspace layers.
* `FoundationDB.Layers.Commmon.dll` : Contains common Layers that emulates Tables, Indexes, Document Collections, Blobs, ...
* `fdb_c.dll` : The native C client that you will need to obtain from the official FoundationDB windows setup.

Known Limitations
-----------------

* Since the native FoundationDB client is 64-bit only, this .NET library is also for 64-bit only applications! Even though it targets AnyCPU, it would fail at runtime. _Don't forget to disable the `Prefer 32-bit` option in your project Build properties, that is enabled by default!_ 
* You cannot unload the fdb C native client from the process once the network thread has started. You can stop the network thread once, but it does not support being restarted. This can cause problems when running under ASP.NET.
* FoundationDB does not support long running batch or range queries if they take too much time. Such queries will fail with a 'past_version' error. The current maximum duration for read transactions is 5 seconds.
* FoundationDB has a maximum allowed size of 100,000 bytes for values, and 10,000 bytes for keys. Larger values must be split into multiple keys
* FoundationDB has a maximum allowed size of 10,000,000 bytes for writes per transactions (some of all key+values that are mutated). You need multiple transaction if you need to store more data. There is a Bulk API (`Fdb.Bulk.*`) to help for the most common cases (import, export, backup/restore, ...)
* See https://apple.github.io/foundationdb/known-limitations.html for other known limitations of the FoundationDB database.

License
-------

This code is licensed under the 3-clause BSD License.

Contributing
------------

* Yes, we use tabs! Get over it.
* Style rules are encoded in `.editorconfig` which is supported by most IDEs (or via extensions).
* You can visit the FoundationDB forums for generic questions (not .NET): https://forums.foundationdb.org/


