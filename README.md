FoundationDB.Net Client
=======================

This code is licensed under the 3-clause BSD Licence.

[![Build status](https://ci.appveyor.com/api/projects/status/83u4pd2ckevdtb57?svg=true)](https://ci.appveyor.com/project/KrzysFR/foundationdb-dotnet-client)
[![foundationdb-dotnet-client MyGet Build Status](https://www.myget.org/BuildSource/Badge/foundationdb-dotnet-client?identifier=faedfb95-0e53-4c43-9bb3-dae95d59e2b8)](https://www.myget.org/)

How to use
----------

You will need to install two things:
- a copy of the FoundationDB client library, available on https://www.foundationdb.org/download/
- a reference to the `FoundationDB.Client` library.

The `FoundationDB.Client` library is available on NuGet:
- https://www.nuget.org/packages/FoundationDB.Client/

There is also a MyGet feed for nightly builds:
- NuGet v3: https://www.myget.org/F/foundationdb-dotnet-client/api/v3/index.json
- NuGet v2: https://www.myget.org/F/foundationdb-dotnet-client/api/v2

```CSharp
using FoundationDB.Client; // this is the main namespace of the library

// note: most operations require a valid CancellationToken, which you need to obtain from the context (HTTP request, component lifetime, timeout, ...)
CancellationToken cancel = ....;

// Connect to the db "DB" using the default cluster file
using (var db = await Fdb.OpenAsync())
{
    // we will use a "Test" directory to isolate our test data
    var location = await db.Directory.CreateOrOpenAsync("Test", cancel);
    // this location will remember the allocated prefix, and
    // automatically add it as a prefix to all our keys
    
    // we need a transaction to be able to make changes to the db
    // note: production code should use "db.WriteAsync(..., cancel)" instead
    using (var trans = db.BeginTransaction(cancel))
    {
        // For our convenience, we will use the Tuple Encoding format for our keys,
        // which is accessible via the "location.Keys" helper. We could have used
        // any other encoding for the keys. Tuples are simple to use and have some
        // interesting ordering properties that make it easy to work with.
        // => All our keys will be encoded as the packed tuple ({Test}, "foo"),
        //    making them very nice and compact. We could also use integers or GUIDs
        //    for the keys themselves.

        // Set "Hello" key to "World"
        trans.Set(
            location.Keys.Encode("Hello"),
            Slice.FromString("World") // UTF-8 encoded string
        );

        // Set "Count" key to 42
        trans.Set(
            location.Keys.Encode("Count"),
            Slice.FromInt32(42) // 1 byte
        );
        
        // Atomically add 123 to "Total"
        trans.AtomicAdd(
            location.Keys.Encode("Total"),
            Slice.FromFixed32(123) // 4 bytes, Little Endian
        );

        // Set bits 3, 9 and 30 in the bit map stored in the key "Bitmap"
        trans.AtomicOr(
            location.Keys.Encode("Bitmap"),
            Slice.FromFixed32((1 << 3) | (1 << 9) | (1 << 30)) // 4 bytes, Little Endian
        );
        
        // commit the changes to the db
        await trans.CommitAsync();
        // note: it is only after this completes, that the keys are actually
        // sent to the database and durably stored.
    }
    
    // we also need a transaction to read from the db
    // note: production code should use "db.ReadAsync(..., cancel)" instead.
    using (var trans = db.BeginReadOnlyTransaction(cancel))
    {
        // Read ({Test}, "Hello", ) as a string
        Slice value = await trans.GetAsync(location.Keys.Encode("Hello"));
        Console.WriteLine(value.ToUnicode()); // -> World
    
        // Read ({Test}, "Count", ) as an int
        value = await trans.GetAsync(location.Keys.Encode("Count"));
        Console.WriteLine(value.ToInt32()); // -> 42

        // Reading keys that do not exist will return 'Slice.Nil',
		// which is the equivalent of "key not found". 
        value = await trans.GetAsync(location.Keys.Encode("NotFound"));
        Console.WriteLine(value.HasValue); // -> false
        Console.WriteLine(value == Slice.Nil); // -> true
        // note: there is also 'Slice.Empty' that is returned for existing keys,
        // but with an empty value (frequently used by indexes)
        
        // this transaction doesn't write aynthing, which means that
        // we don't have to commit.
    }

    // Let's make something a little more useful, like a very simple
    // array of values, indexed by integers (0, 1, 2, ...)

    // First we will create a subdirectory for our little array,
    // just so that is does not interfere with other things in the cluster.
    var list = await location.CreateOrOpenAsync(db, "List", cancel);

    // here we will use db.WriteAsync(...) that implements a retry loop.
    // this helps protect you against intermitent failures by automatically
    // retrying the lambda method you provided.
    await db.WriteAsync((trans) =>
    {
        // add some data to the array with the format: (..., index) = value
        trans.Set(list.Keys.Encode(0), Slice.FromStringUtf8("AAA"));
        trans.Set(list.Keys.Encode(1), Slice.FromStringUtf8("BBB"));
        trans.Set(list.Keys.Encode(2), Slice.FromStringUtf8("CCC"));
        // The actual keys will be a concatenation of the prefix of {List},
        // and a packed tuple containing the index. Since we are using the
        // Directory Layer, this should still be fairly small (between 4
        // and 5 bytes). The values are raw slices, which means that your
        // application MUST KNOW that they are strings in order to decode
        // them. If you wan't any tool to be able to find out the type of
        // your values, you can also use TuPack.EncodeKey("AAA") to create
        // the values, at the cost of 2 extra bytes per entry.
        
        // This is always a good idea to maintain a counter of keys in our array.
        // The cheapest way to do that, is to reuse the subspace key itself, which
        // is 'in' the subspace, but not 'inside':
        trans.Set(list.Key, Slice.FromFixed32(3));
        // We could use TuPack.EncodeKey<int>(3) here, but having a fixed size counter
        // makes it easy to use AtomicAdd(...) to increment (or decrement) the value
        // when adding (or removing) entries in the array.
        
        // Finally, here we would normally call CommitAsync() to durably persist the
        // changes, but WriteAsync() will automatically call CommitAsync() for us and
        // return once this is done. This means that you don't even need to mark this
        // lambda as async, as long as you only call methods like Set(), Clear() or 
        // any of the AtomicXXX().

        // If something goes wrong with the database, this lambda will be called again,
        // until the problems goes away, or the retry loop decides that there is no point
        // in retrying anymore, and the exception will be re-thrown.

    }, cancel); // don't forget the CancellationToken, which can stop the retry loop !

    // We can read everything back in one shot, using an async "LINQ" query.
    var results = await db.QueryAsync((trans) =>
    {
        // do a range query on the list subspace, which should return all the pairs
        // in the subspace, one for each entry in the array.
        // We exploit the fact that subspace.Tuples.ToRange() usually does not include
        // the subspace prefix itself, because we don't want our counter to be returned
        // with the query itself.
        return trans
            // ask for all keys that are _inside_ our subspace
            .GetRange(list.Keys.ToRange())
            // transform the each KeyValuePair<Slice, Slice> into something
            // nicer to use, like a tuple (int Index, string Value)
            .Select((kvp) => 
            (
                    // unpack the tuple and returns the last item as an int
                    Index: list.Keys.DecodeLast<int>(kvp.Key),
                    // convert the value from UTF-8 bytes to a string
                    Value: kvp.Value.ToStringUtf8()
            ))
            // only get even values
            // note: this executes on the client, so the query will still need to
            // fetch ALL the values from the db!
            .Where((kvp) => kvp.Index % 2 == 0);

        // note that QueryAsync() is a shortcut for calling ReadAsync(...) and then
        // calling ToListAsync() on the async LINQ Query. If you want to call a
        // different operator than ToListAsync(), just use ReadAsync()
            
    }, cancel);

    // results.Count -> 2
    // results[0] -> (Index: 0, Value: "AAA")
    // results[1] -> (Index: 2, Value: "CCC")
    //
    // once you have the result, nothing stops you from using regular LINQ to finish
    // off your query in memory, outside of the retry loop.

    // now that our little sample program is done, we can dispose the database instance.
    // in production, you should actually open the connection once in your startup code,
    // and keep it in a place where the rest of your code can easily access it.
}
```

Please note that the above sample is ok for a simple HelloWorld.exe app, but for actual production you need to be careful of a few things:

- You should NOT open a new database connection (`Fdb.OpenAsync()`) everytime you need to read or write something. You should open a single database instance somewhere in your startup code, and use that instance everywhere. If you are using a Repository pattern, you can store the IFdbDatabase instance there. Another option is to use a Dependency Injection framework

- You should probably not create and transactions yourself (`db.CreateTransaction()`), and instead prefer using the standard retry loops implemented by `db.ReadAsync(...)`, `db.WriteAsync(...)` and `db.ReadWriteAsync(...)` which will handle all the gory details for you. They will ensure that your transactions are retried in case of conflicts or transient errors. See https://apple.github.io/foundationdb/developer-guide.html#conflict-ranges

- Use the `Tuple Layer` to encode and decode your keys, if possible. This will give you a better experience overall, since all the logging filters and key formatters will try to decode tuples by default, and display `(42, "hello", true)` instead of the cryptic `<15>*<02>hello<00><15><01>`. For simple values like strings (ex: JSON text) or 32-bit/64-bit numbers, you can also use `Slice.FromString(...)`, or `Slice.FromInt32(...)`. For composite values, you can also use the Tuple encoding, if the elements types are simple (string, numbers, dates, ...). You can also use custom encoders via the `IKeyEncoder<T>` and `IValueEncoder<T>`, which you can get from the helper class `KeyValueEncoders`, or roll your own by implementing these interfaces.

- You should use the `Directory Layer` instead of manual partitions (`db.Partition(...)`). This will help you organize your data into a folder-like structure with nice and description names, while keeping the binary prefixes small. You can access the default DirectoryLayer of your database via the `db.Directory` property. Using partitions makes it easier to browse the content of your database using tools like `FdbShell`.

- You should __NEVER__, _ever_ block on Tasks by using `.Wait()` from non-async code. This will either dead-lock your application, or greatly degrade the performances. If you cannot do otherwise (ex: top-level call in a `void Main()` then at least wrap your code inside a `static async Task MainAsync(string[] args)` method, and do a `MainAsync(args).GetAwaiter().GetResult()`.

- Don't give in, and resist the temptation of passing `CancellationToken.None` everywhere! Try to obtain a valid `CancellationToken` from your execution context (HTTP host, Test Framework, Actor/MicroService host, ...). This will allow the environment to safely shutdown and abort all pending transactions, without any risks of data corruption. If you don't have any easy source (like in a unit test framework), then at list provide you own using a global `CancellationTokenSource` that you can `Cancel()` in your shutdown code path. From inside your transactional code, you can get back the token anytime via the `tr.Cancellation` property which will trigger if the transaction completes or is aborted.

How to build
------------

### Visual Studio Solution

You will need Visual Studio 2017 version 15.5 or above to build the solution (C# 7.2 and .NET Standard 2.0 support is required).

You will also need to obtain the 'fdb_c.dll' C API binding from the foundationdb.org wesite, by installing the client SDK:

* Go to https://www.foundationdb.org/download/ and download the Windows x64 MSI. You can use the free Community edition that gives you unlimited server processes for development and testing.
* Install the MSI, selecting the default options.
* Go to `C:\Program Files\foundationdb\bin\` and make sure that `fdb_c.dll` is there.
* Open the FoundationDb.Client.sln file in Visual Studio 2012.
* Choose the Release or Debug configuration, and rebuild the solution.

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

How to test
-----------

The test project is using NUnit 3.10.

If you are using a custom runner or VS plugin (like TestDriven.net), make sure that it has the correct nunit version, and that it is configured to run the test using 64-bit process. The code will NOT work on 32 bit.

WARNING: All the tests should work under the ('T',) subspace, but any bug or mistake could end up wiping or corrupting the global keyspace and you may lose all your data. You can specify an alternative cluster file to use in `TestHelper.cs` file.

Hosting on IIS
--------------

* The .NET API is async-only, and should only be called inside async methods. You should NEVER write something like `tr.GetAsync(...).Wait()` or 'tr.GetAsync(...).Result' because it will GREATLY degrade performances and prevent you from scaling up past a few concurrent requests.
* The underlying client library will not run on a 32-bit Application Pool. You will need to move your web application to a 64-bit Application Pool.
* If you are using IIS Express with an ASP.NET or ASP.NET MVC application from Visual Studio, you need to configure your IIS Express instance to run in 64-bit. With Visual Studio 2013, this can be done by checking Tools | Options | Projects and Solutions | Web Projects | Use the 64 bit version of IIS Express for web sites and projects
* The fdb_c.dll library can only be started once per process. This makes impractical to run an web application running inside a dedicated Application Domain alongside other application, on a shared host process. The only current workaround is to have a dedicated host process for this application, by making it run inside its own Application Pool.
* If you don't use the host's CancellationToken for transactions and retry loops, deadlock can occur if the FoundationDB cluster is unavailable or under very heavy load. Please consider also using safe values for the DefaultTimeout and DefaultRetryLimit settings.

Hosting on OWIN
---------------

* There are no particular restrictions, apart from requiring a 64-bit OWIN host.
* You should explicitly call Fdb.Stop() when your OWIN host process shuts down, in order to ensure that any pending transaction gets cancelled properly.

Implementation Notes
--------------------

Please refer to https://apple.github.io/foundationdb/ to get an overview on the FoundationDB API, if you haven't already.

This .NET binding has been modeled to be as close as possible to the other bindings (Python especially), while still having a '.NET' style API. 

There were a few design goals, that you may agree with or not:
* Reducing the need to allocate byte[] as much as possible. To achieve that, I'm using a 'Slice' struct that is a glorified `ArraySegment<byte>`. All allocations made during a request try to use a single underlying byte[] array, and split it into several chunks.
* Mapping FoundationDB's Future into `Task<T>` to be able to use async/await. This means that .NET 4.5 is required to use this binding. It would be possible to port the binding to .NET 4.0 using the `Microsoft.Bcl.Async` nuget package.
* Reducing the risks of memory leaks in long running server processes by wrapping all FDB_xxx handles whith .NET `SafeHandle`. This adds a little overhead when P/Invoking into native code, but will guarantee that all handles get released at some time (during the next GC).
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
* You cannot unload the fdb C native client from the process once the netork thread has started. You can stop the network thread once, but it does not support being restarted. This can cause problems when running under ASP.NET.
* FoundationDB does not support long running batch or range queries if they take too much time. Such queries will fail with a 'past_version' error. The current maximum duration for read transactions is 5 seconds.
* FoundationDB as a maximum allowed size of 100,000 bytes for values, and 10,000 bytes for keys. Larger values must be split into multiple keys
* FoundationDB as a maximum allowed size of 10,000,000 bytes for writes per transactions (some of all key+values that are mutated). You need multiple transaction if you need to store more data. There is a Bulk API (`Fdb.Bulk.*`) to help for the most common cases (import, export, backup/restore, ...)
* See https://apple.github.io/foundationdb/known-limitations.html for other known limitations of the FoundationDB database.

Contributing
------------

* Yes, we use tabs! Get over it.
* Style rules are encoded in `.editorconfig` which is supported by most IDEs (or via extensions).
* You can visit the FoundationDB forums for generic questions (not .NET): https://forums.foundationdb.org/
