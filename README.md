FoundationDB.Net Client
=======================

__Important Notice__

> As you may or may not know, the FoundationDB K/V Store engine is no longer available for download, following the acquisition of FoundationDB LLC by Apple. This means that, even though the .NET binding itself is open source, it is not usable without access to a license for the underlying database engine. Given these circumstances, this means that this project is no longer under active development, and will now transition into a maintenance state.

> If you are actively using the .NET FoundationDB Binding in your application, and are in a situation where you have an existing infrastructure to support, please know that this repository will stay up as long as is needed, and there are currently no plans to close it down. If you want to be safe, please feel free to clone or fork it! We will try to help you fix any bug of issues, as is humanely possible. Since the license of the .NET Binding is open source (3-clause BSD), we encourage you to fork it and use it in anyway you think is necessary to help you transition to something else. In any case, we urge you to contact people at FoundationDB to help you on that (according to the message on the Community Forum, you should get in touch with info@foundationdb.com)

> If you were looking for a new database solution for a new project, you'll have to keep looking. Good luck, because FoundationDB was probably the best in its class.

> Some parts of this project could probably be lifted up and used or recycled in some shape or form into other projects (ex: Tuples, Slice, AsyncLINQ, ...). Please get in touch if you are interested in this!

> I'd like to thank everyone who participated in this project, and hope that you all will be able to land back on your feet! It was a fun ride and an invaluable experience. Merci à vous!

__Q&A / tl;dr__

What is the situation regarding FoundationDB (the database)?
> Apple acquired the company that makes FoundationDB. It does not seem to be available for download anymore. That's all we know. Please contact info@foundationdb.com if you want more details.

What is this project exactly?
> This project was a .NET wrapper for the underlying native Client API for the FoundationDB Key/Value Store, as well a some infrastructure to make it easier to work with key/value stores using C#. The wrapper itself is opensource, but it needs a native DLL (fdb_c.dll) to talk to the database, which was licensed by FoundationDB LLC and was closed source.

Where can I find the SQL layer or parsers?
> This project only dealt specifically with the Key/Value Store engine, and did not have any link with either the SQL Layer or the SQL parser. We don't have any knowledge on the fate of these two projects.

Is this project under active development?
> Not anymore. Bugs will be fixed but don't expect any major activity.

Will this repository go away like the other ones?
> No plans for that. If you want to make sure, please clone or fork it!

I'm using it in production, what gives?
> Please contact the folks at info@foundationdb.com ASAP!

Then, what can I use to replace FoundationDB in my application?
> If you find something as good as FDB, please let us know :)

What attributes should I look for, if I want something similar?
> You'll need an ordered key/value store, with multi-key transaction support, and ACID guarantees. Most of the Layers are designed to work with an underlying K/V store that offers strong ACID guarantees, and will probably not work with other K/V store that don't offer these. If we find something that works as a replacement, we'll make sure to update this notice. For know, you should maybe look at PostgreSQL.

I have a bug, can you help us?
> Please open an Issue and if this is at the .NET binding level, we will try to help you if we can!

Are there some intersting bits in this project worth looking at?
> Yes! Please take a look at the Tuple, Slice and FdbAsyncEnumerable classes! This was a fun project to make C# work well with an API mostly designed with dynamic languages (like Python or node.js) in mind. There is also the Memory Stoage project which was an attempt to have an in-memory version of the K/V store (for unit tests or embedded scenarios).

__End of Important Notice__

This code is licensed under the 3-clause BSD Licence.

[![Build status](https://ci.appveyor.com/api/projects/status/83u4pd2ckevdtb57?svg=true)](https://ci.appveyor.com/project/KrzysFR/foundationdb-dotnet-client)

> __There is an incoming API change that may break a few things, regarding the use of Subspaces and Tuples__. The new API is currently in the `refac_tuples` branch, and will be merge soon in `master` for the 0.9 release. If you are just starting, you should probably use the new branch. If you already have existing code, there will be a document explaining the changes and how to fix your source code. __See [#42](https://github.com/Doxense/foundationdb-dotnet-client/pull/42) for more details.__
    
> __There is another incoming change, regarding the way FDB's Futures are implemented under the hood, which should fix some random crash issues under heavy load__. Some of the problems have already been fixed in master, but the new branch `future_ng` is a complete rework, in collaboration with the folks at FoundationDB. __See [#54](https://github.com/Doxense/foundationdb-dotnet-client/pull/54) for more details__

> Both these branches are already used in production. Once everything is merged and tested, we will be ready for 1.0.

> The default API level selected on start is still 200 by default, but this will probably change to 300 by default for 1.0. If you are not doing it already, you should make sure to use `Fdb.UseApiVersion(200)` (or 300) if you want to lock your application to a specific level and be safe against future changes.

How to use
----------

```CSharp

// note: most operations require a valid CancellationToken, which you need to provide
CancellationToken token = ....; // host-provided cancellation token

// Connect to the db "DB" using the default cluster file
using (var db = await Fdb.OpenAsync())
{
    // we will use a "Test" directory to isolate our test data
    var location = await db.Directory.CreateOrOpenAsync("Test", token);
    
    // we need a transaction to be able to make changes to the db
    // note: production code should use "db.WriteAsync(..., token)" instead
    using (var trans = db.BeginTransaction(token))
    {
        // ("Test", "Hello", ) = "World"
        trans.Set(location.Pack("Hello"), Slice.FromString("World"));

        // ("Test", "Count", ) = 42
        trans.Set(location.Pack("Count"), Slice.FromInt32(42));
        
        // Atomically add 123 to ("Test", "Total")
        trans.AtomicAdd(location.Pack("Total"), Slice.FromFixed32(123));

        // Set bits 3, 9 and 30 in the bitmap stored at ("Test", "Bitmap")
        trans.AtomicOr(location.Pack("Bitmap"), Slice.FromFixed32((1 << 3) | (1 << 9) | (1 << 30)));
        
        // commit the changes to the db
        await trans.CommitAsync();
        // note: it is only after this completes, that the keys are actually
        // sent to the database and durably stored.
    }
    
    // we also need a transaction to read from the db
    // note: production code should use "db.ReadAsync(..., token)" instead.
    using (var trans = db.BeginReadOnlyTransaction(token))
    {  
        // Read ("Test", "Hello", ) as a string
        Slice value = await trans.GetAsync(location.Pack("Hello"));
        Console.WriteLine(value.ToUnicode()); // -> World
    
        // Read ("Test", "Count", ) as an int
        value = await trans.GetAsync(location.Pack("Count"));
        Console.WriteLine(value.ToInt32()); // -> 42
    
        // missing keys give a result of Slice.Nil, which is the equivalent
        // of "key not found". 
        value = await trans.GetAsync(location.Pack("NotFound"));
        Console.WriteLine(value.HasValue); // -> false
        Console.WriteLine(value == Slice.Nil); // -> true
        // note: there is also Slice.Empty that is returned for existing keys
        // with no value (used frequently for indexes)
        
        // this transaction does'nt write aynthing, which means that
        // we don't have to commit anything.
    }

    // Let's make something a little more useful, like a very simple
    // array of values, indexed by integers (0, 1, 2, ...)

    // First we will create a subdirectory for our little array,
    // just so that is does not interfere with other things in the cluster.
    var list = await location.CreateOrOpenAsync(db, "List", token);

	// here we will use db.WriteAsync(...) that implements a retry loop.
	// this helps protect you against intermitent failures by automatically
	// retrying the lambda method you provided.
	await db.WriteAsync((trans) =>
	{
        // add some data to the list with the format: (..., index) = value
        trans.Set(list.Pack(0), Slice.FromString("AAA"));
        trans.Set(list.Pack(1), Slice.FromString("BBB"));
        trans.Set(list.Pack(2), Slice.FromString("CCC"));
        // The actual keys will be a concatenation of the prefix of 'list',
        // and a packed tuple containing the index. Since we are using the
        // Directory Layer, this should still be fairly small (between 4
        // and 5 bytes). The values are raw slices, which means that your
        // application MUST KNOW that they are strings in order to decode
        // them. If you wan't any tool to be able to find out the type of
        // your values, you can also use FdbTuple.Pack("AAA") to create
        // the values, at the cost of 2 extra bytes per entry.
        
        // This is always a good idea to maintain a counter of keys in our array.
        // The cheapest way to do that, is to reuse the subspace key itself, which
        // is 'in' the subspace, but not 'inside':
        trans.Set(list.Key, Slice.FromFixed32(3));
        // We could use FdbTuple.Pack<int>(3) here, but have a fixed size counter
        // makes it easy to use AtomicAdd(...) to increment (or decrement) the value
        // when adding or removing entries in the array.
        
        // Finally, here we would normally call CommitAsync() to durably commit the
        // changes, but WriteAsync() will automatically do the commit for you and
        // return once this is done. This means that you don't even need to mark this
        // lambda as async, as long as you only call methods like Set(), Clear() or 
        // any of the AtomicXXX().

        // If something goes wrong with the database, this lambda will be called again,
        // until the problems goes away, or the retry loop decides that there is no point
        // in retrying anymore, and the exception will be re-thrown.
        
	}, token); // don't forget the cancellation token, which can stop the retry loop !

    // We can read everything back in one shot, using an async "LINQ" query.
    var results = await db.QueryAsync((trans) =>
    {
        // do a range query on the list subspace, which should return all the pairs
        // in the subspace, one for each entry in the array.
        // We exploit the fact that subspace.ToRange() usually does not include the
        // subspace prefix itself, because we don't want our counter to be returned
        // with the query itself.
        return trans
            // ask for all keys that are _inside_ our subspace
            .GetRange(list.ToRange())
            // transform the resultoing KeyValuePair<Slice, Slice> into something
            // nicer to use, like a typed KeyValuePair<int, string>
            .Select((kvp) => 
                new KeyValuePair<int, string>(
                    // unpack the tuple and returns the last item as an int
                    list.UnpackLast<int>(kvp.Key),
                    // convert the value into an unicode string
                    kvp.Value.ToUnicode() 
                ))
            // only get even values
            // note: this executes on the client, so the query will still need to
            // fetch ALL the values from the db!
            .Where((kvp) => kvp.Key % 2 == 0);

	    // note that QueryAsync() is a shortcut for calling ReadAsync(...) and then
	    // calling ToListAsync() on the async LINQ Query. If you want to call a
	    // different operator than ToListAsync(), just use ReadAsync()
            
    }, token);

    // results.Count -> 2
    // results[0] -> KeyValuePair<int, string>(0, "AAA")
    // results[1] -> KeyValuePair<int, string>(2, "CCC")
    //
    // once you have the result, nothing stops you from using regular LINQ to finish
    // off your query in memory, outside of the retry loop.

    // now that our little sample program is done, we can dispose the database instance.
    // in production, you should actually open the connection once in your startup code,
    // and keep it in a place where the rest of your code can easily access it.
}
```

Please note that the above sample is ok for a simple HelloWorld.exe app, but for actual production you need to be careful of a few things:

- You should NOT open a new connection (`Fdb.OpenAsync()`) everytime you need to read or write something. You should open a single database instance somewhere in your startup code, and use that instance everywhere. If you are using a Repository pattern, you can store the IFdbDatabase instance there. Another option is to use a Dependency Injection framework

- You should probably not create and transactions yourself (`db.CreateTransaction()`), and instead prefer using the standard retry loops implemented by `db.ReadAsync(...)`, `db.WriteAsync(...)` and `db.ReadWriteAsync(...)` which will handle all the gory details for you. They will ensure that your transactions are retried in case of conflicts or transient errors. See https://foundationdb.com/key-value-store/documentation/developer-guide.html#conflict-ranges

- Use the `Tuple Layer` to encode and decode your keys, if possible. This will give you a better experience overall, since all the logging filters and key formatters will try to decode tuples by default, and display `(42, "hello", true)` instead of the cryptic `<15>*<02>hello<00><15><01>`. For simple values like strings (ex: JSON text) or 32-bit/64-bit numbers, you can also use `Slice.FromString(...)`, or `Slice.FromInt32(...)`. For composite values, you can also use the Tuple encoding, if the elements types are simple (string, numbers, dates, ...). You can also use custom encoders via the `IKeyEncoder<T>` and `IValueEncoder<T>`, which you can get from the helper class `KeyValueEncoders`, or roll your own by implementing these interfaces.

- You should use the `Directory Layer` instead of manual partitions (`db.Partition(...)`). This will help you organize your data into a folder-like structure with nice and description names, while keeping the binary prefixes small. You can access the default DirectoryLayer of your database via the `db.Directory` property. Using partitions makes it easier to browse the content of your database using tools like `FdbShell`.

- You should NEVER block on Tasks by using .Wait() from non-async code. This will either dead-lock your application, or greatly degrade the performances. If you cannot do otherwise (ex: top-level call in a `void Main()` then at least wrap your code inside a `static async Task MainAsync(string[] args)` method, and do a `MainAsync(args).GetAwaiter().GetResult()`.

- Don't give in, and resist the tentation of passing `CancellationToken.None` everywhere! Try to obtain a valid `CancellationToken` from your execution context (HTTP host, Task Worker environment, ...). This will allow the environment to safely shutdown and abort all pending transactions, without any risks of data corruption. If you don't have any easy source (like in a unit test framework), then at list provide you own using a global `CancellationTokenSource` that you can `Cancel()` in your shutdown code path. From inside your transactional code, you can get back the token anytime via the `tr.Cancellation` property which will trigger if the transaction completes or is aborted.

How to build
------------

### Visual Studio Solution

You will need Visual Studio .NET 2012 or 2013 and .NET 4.5 minimum to compile the solution.

You will also need to obtain the 'fdb_c.dll' C API binding from the foundationdb.com wesite, by installing the client SDK:

* Go to http://foundationdb.com/get/ and download the Windows x64 MSI. You can use the free Community edition that gives you unlimited server processes for development and testing.
* Install the MSI, selecting the default options.
* Go to `C:\Program Files\foundationdb\bin\` and make sure that `fdb_c.dll` is there.
* Open the FoundationDb.Client.sln file in Visual Studio 2012.
* Choose the Release or Debug configuration, and rebuild the solution.

If you see errors on 'await' or 'async' keywords, please make sure that you are using Visual Studio 2012 or 2013 RC, and not an earlier version.

If you see the error `Unable to locate '...\foundationdb-dotnet-client\.nuget\nuget.exe'` then you need to run the `Enable Nuget Package Restore` entry in the `Project` menu (or right click on the solution) that will reinstall nuget.exe in the .nuget folder. Also, Nuget should redownload the missing packages during the first build.

### From the Command Line

You can also build, test and compile the NuGet packages from the command line, using FAKE.

You will need to perform the same steps as above (download and install FoundationDB)

In a new Command Prompt, go the root folder of the solution and run one of the following commands:
- `build Test`: to build the solution (Debug) and run the unit tests.
- `build Release`: to build the solution (Release) and the NuGet packages (which will be copied to `.\build\output\_packages\`)
- `build BuildAppRelease`: only build the solution (Release)
- `build BuildAppDebug`: only build the solution (Debug)
- `build Clean`: clean the solution
 
If you get `System.UnauthorizedAccessException: Access to the path './build/output/FoundationDB.Tests\FoundationDB.Client.dll' is denied.` errors from time to time, you need to kill any `nunit-agent.exe` process that may have stuck around.

### Mono

When building for Mono/Linux this version will look for `libfdb_c.so` instead of `fdb_c.dll`.

More details on running FoundationDB on Linux can be found here: https://foundationdb.com/key-value-store/documentation/getting-started-linux.html

How to build the NuGet packages
-------------------------------

They are easily build from the command line using FAKE, by running the `build Release` command from the root of the solution.

Once this is done, you can either push these package to your internal NuGet feed, or simplify create a local package folder on your disk. See http://docs.nuget.org/docs/creating-packages/hosting-your-own-nuget-feeds


How to test
-----------

The test project is using NUnit 2.6.3, and requires support for async test methods.

If you are using a custom runner or VS plugin (like TestDriven.net), make sure that it has the correct nunit version, and that it is configured to run the test using 64-bit process. The code will NOT work on 32 bit.

WARNING: All the tests should work under the ('T',) subspace, but any bug or mistake could end up wiping or corrupting the global keyspace and you may lose all your data. You can specify an alternative cluster file to use in `TestHelper.cs` file.

Hosting on IIS
--------------

* The .NET API is async-only, and should only be called inside async methods. You should NEVER write something like `tr.GetAsync(...).Wait()` or 'tr.GetAsync(...).Result' because it will GREATLY degrade performances and prevent you from scaling up past a few concurrent requests.
* The underlying client library will not run on a 32-bit Application Pool. You will need to move your web application to a 64-bit Application Pool.
* If you are using IIS Express with an ASP.NET or ASP.NET MVC application from Visual Studio, you need to configure your IIS Express instance to run in 64-bit. With Visual Studio 2013, this can be done by checking Tools | Options | Projects and Solutions | Web Projects | Use the 64 bit version of IIS Express for web sites and projects
* The fdb_c.dll library can only be started once per process. This makes impractical to run an web application running inside a dedicated Application Domain alongside other application, on a shared host process. See http://community.foundationdb.com/questions/1146/using-foundationdb-in-a-webapi-2-project for more details. The only current workaround is to have a dedicated host process for this application, by making it run inside its own Application Pool.
* If you don't use the host's cancellation token for transactions and retry loops, deadlock can occur if the FoundationDB cluster is unavailable or under very heavy load. Please consider also using safe values for the DefaultTimeout and DefaultRetryLimit settings.

Hosting on OWIN
---------------

* There are no particular restrictions, apart from requiring a 64-bit OWIN host.
* You should explicitly call Fdb.Stop() when your OWIN host process shuts down, in order to ensure that any pending transaction gets cancelled properly.

Implementation Notes
--------------------

Please refer to http://foundationdb.com/documentation/ to get an overview on the FoundationDB API, if you haven't already.

This .NET binding has been modeled to be as close as possible to the other bindings (Python especially), while still having a '.NET' style API. 

There were a few design goals, that you may agree with or not:
* Reducing the need to allocate byte[] as much as possible. To achieve that, I'm using a 'Slice' struct that is a glorified `ArraySegment<byte>`. All allocations made during a request try to use a single underlying byte[] array, and split it into several chunks.
* Mapping FoundationDB's Future into `Task<T>` to be able to use async/await. This means that .NET 4.5 is required to use this binding. It would be possible to port the binding to .NET 4.0 using the `Microsoft.Bcl.Async` nuget package.
* Reducing the risks of memory leaks in long running server processes by wrapping all FDB_xxx handles whith .NET `SafeHandle`. This adds a little overhead when P/Invoking into native code, but will guarantee that all handles get released at some time (during the next GC).
* The Tuple layer has also been optimized to reduce the number of allocations required, and cache the packed bytes of oftenly used tuples (in subspaces, for example).

However, there are some key differences between Python and .NET that may cause problems:
* Python's dynamic types and auto casting of Tuples values, are difficult to model in .NET (without relying on the DLR). The Tuple implementation try to be as dynamic as possible, but if you want to be safe, please try to only use strings, longs, booleans and byte[] to be 100% compatible with other bindings. You should refrain from using the untyped `tuple[index]` indexer (that returns an object), and instead use the generic `tuple.Get<T>(index)` that will try to adapt the underlying type into a T.
* The Tuple layer uses ASCII and Unicode strings, while .NET only have Unicode strings. That means that all strings in .NET will be packed with prefix type 0x02 and byte arrays with prefix type 0x01. An ASCII string packed in Python will be seen as a byte[] unless you use `IFdbTuple.Get<string>()` that will automatically convert it to Unicode.
* There is no dedicated 'UUID' type prefix, so that means that System.Guid would be serialized as byte arrays, and all instances of byte 0 would need to be escaped. Since `System.Guid` are frequently used as primary keys, I added a new custom type prefix (0x30) for 128-bits UUIDs and (0x31) for 64-bits UUIDs. This simplifies packing/unpacking and speeds up writing/reading/comparing Guid keys.

The following files will be required by your application
* `FoundationDB.Client.dll` : Contains the core types (FdbDatabase, FdbTransaction, ...) and infrastructure to connect to a FoundationDB cluster and execute basic queries, as well as the Tuple and Subspace layers.
* `FoundationDB.Layers.Commmon.dll` : Contains common Layers that emulates Tables, Indexes, Document Collections, Blobs, ...
* `fdb_c.dll` : The native C client that you will need to obtain from the official FoundationDB windows setup.

Known Limitations
-----------------

* While the .NET API supports UUIDs in the Tuple layer, none of the other bindings currently do. As a result, packed Tuples with UUIDs will not be able to be unpacked in other bindings.
* The LINQ API is still a work in progress, and may change a lot. Simple LINQ queries, like Select() or Where() on the result of range queries (to convert Slice key/values into oter types) should work.
* You cannot unload the fdb C native client from the process once the netork thread has started. You can stop the network thread once, but it does not support being restarted. This can cause problems when running under ASP.NET.
* FoundationDB does not support long running batch or range queries if they take too much time. Such queries will fail with a 'past_version' error.
* See https://foundationdb.com/documentation/known-limitations.html for other known limitations of the FoundationDB database.

Contributing
------------

* It is important to point out that this solution uses tabs instead of spaces for various reasons. In order to ease the transition for people who want to start contributing and avoid having to switch their Visual Studio configuration manually an .editorconfig file has been added to the root folder of the solution. The easiest way to use this is to install the [Extension for Visual Studio](http://visualstudiogallery.msdn.microsoft.com/c8bccfe2-650c-4b42-bc5c-845e21f96328). This will switch visual studio's settings for white space in csharp files to use tabs.

