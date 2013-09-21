FoundationDB.Net Client
=======================

This is a C#/.NET binding for FoundationDB.

This code is licensed under the 3-clause BSD Licence. 

It requires the .NET 4.5 Framework, and uses the 64-bit C API binding that is licensed by FoundationDB LLC and must be obtained separately.

It currently targets version 1.0 FoundationDB (API level 100)

The core C#/.NET binding API (FoundationDB.Client namespace) is relatively stable but is subject to change. Modifications of any of the APIs will be accompanied by a change to the binding's assembly version. As a result, clients that are compiled against one version of the binding will not run when linked against another version of the binding.

How to use
----------

```CSharp

// Connect to the db "DB" using the default cluster file
using (var db = await Fdb.OpenAsync())
{
    // we will use a "Test" subspace to isolate our test data
    var location = db.Partition("Test");
    
    // we need a transaction to be able to make changes to the db
    using (var trans = db.BeginTransaction())
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
    }
    
    // we also need a transaction to read from the db
    using (var trans = db.BeginTransaction())
    {  
        // Read ("Test", "Hello", ) as a string
        Slice value = await trans.GetAsync(location.Pack("Hello"));
        Console.WriteLine(value.ToUnicode()); // -> World
    
        // Read ("Test", "Count", ) as an int
        value = await trans.GetAsync(location.Pack("Count"));
        Console.WriteLine(value.ToInt32()); // -> 42
    
        // missing keys give a result of Slice.Nil, which is the equivalent of "no value"
        value = await trans.GetAsync(location.Pack("NotFound"));
        Console.WriteLine(value.HasValue); // -> false
        Console.WriteLine(value == Slice.Nil); // -> true
        // note: there is also Slice.Empty that is returned for existing keys with no value (used frequently for indexes)
        
        // no writes, so we don't have to commit the transaction.
    }

    // We can also do async "LINQ" queries
    using (var trans = db.BeginTransaction())
    {
        // create a child partition for our list
        var list = location.Partition("List");
    
        // add some data to the list: ("Test", "List", index) = value
        trans.Set(list.Pack(0), Slice.FromString("AAA"));
        trans.Set(list.Pack(1), Slice.FromString("BBB"));
        trans.Set(list.Pack(2), Slice.FromString("CCC"));
    
        // do a range query on the list partition, that will return the pairs (int index, string value).
        var results = await (trans.
            // ask for all keys prefixed by the tuple '("Test", "List", )'
            .GetRangeStartsWith(list)
            // transform the results (KeyValuePair<Slice, Slice>) into something nicer
            .Select((kvp) => 
                new KeyValuePair<int, string>(
                    // unpack the tuple and returns the last item as an int
                    list.UnpackLast<int>(kvp.Key),
                    // convert the value into an unicode string
                    kvp.Value.ToUnicode() 
                ))
            // only get even values (note: this will execute on the client, the query will still need to fetch ALL the values)
            .Where((kvp) => kvp.Key % 2 == 0)
            // actually execute the query, and return a List<KeyValuePair<int, string>> with the results
            .ToListAsync()
        );

       // list.Count -> 2
       // list[0] -> <int, string>(0, "AAA")
       // list[1] -> <int, string>(2, "CCC")
    }
    
}
```

How to build
------------

You will need Visual Studio .NET 2012 or 2013 and .NET 4.5 minimum to compile the solution.

You will also need to obtain the 'fdb_c.dll' C API binding from the foundationdb.com wesite, by installing the client SDK:

* Go to http://foundationdb.com/get/ and download the Windows x64 MSI. You can use the free Community edition that gives you unlimited server processes for development and testing.
* Install the MSI, selecting the default options.
* Go to `C:\Program Files\foundationdb\bin\` and make sure that `fdb_c.dll` is there.
* Open the FoundationDb.Client.sln file in Visual Studio 2012.
* Choose the Release or Debug configuration, and rebuild the solution.

If you see errors on 'await' or 'async' keywords, please make sure that you are using Visual Studio 2012 or 2013 RC, and not an earlier version.

If you see the error `Unable to locate '...\foundationdb-dotnet-client\.nuget\nuget.exe'` then you need to run the `Enable Nuget Package Restore` entry in the `Project` menu (or right click on the solution) that will reinstall nuget.exe in the .nuget folder. Also, Nuget should should redownload the missing packages during the first build.

How to test
-----------

The test project is using NUnit 2.6.2, and requires support for async test methods.

If you are using a custom runner or VS plugin (like TestDriven.net), make sure that is has the correct nunit version, and that it is configured to run the test using 64-bit process. The code will NOT work on 32 bit.

WARNING: All the tests should work under the ('T',) subspace, but any bug or mistake could end up wiping or corrupting the global keyspace and you may lose all your data. You can specify an alternative cluster file to use in `TestHelper.cs` file.

Implementation Notes
--------------------

Please refer to http://foundationdb.com/documentation/ to get an overview on the FoundationDB API, if you haven't already.

This .NET binding as been modeled to be as close as possible to the other bindings (Python especially), while still having a '.NET' style API. 

There were a few design goals, that you may agree with or not:
* Reducing the need to allocate byte[] as much as possible. To achieve that, I'm using a 'Slice' struct that is a glorified `ArraySegment<byte>`. All allocations made during a request try to use a single underlying byte[] array, and split it into several chunks.
* Mapping FoundationDB's Future into `Task<T>` to be able to use async/await. This means that .NET 4.5 is required to use this binding. It would be possible to port the binding to .NET 4.0 using the `Microsoft.Bcl.Async` nuget package.
* Reducing the risks of memory leaks in long running server processes by wrapping all FDB_xxx handles whith .NET `SafeHandle`. This adds a little overhead when P/Invoking into native code, but will guarantee that all handles get released at some time (during the next GC).
* The Tuple layer has also been optimized to reduce the number of allocations required, and cache the packed bytes of oftenly used tuples (in subspaces, for example).

However, there are some key differences between Python and .NET that may cause problems:
* Python's dynamic types and auto casting of Tuples values, are difficult to model in .NET (without relying on the DLR). The Tuple implementation try to be as dynamic as possible, but if you want to be safe, please try to only use strings, longs, booleans and byte[] to be 100% compatible with other bindings. You should refrain from using the untyped `tuple[index]` indexer (that returns an object), and instead use the generic `tuple.Get<T>(index)` that will try to adapt the underlying type into a T.
* The Tuple layer uses ASCII and Unicode strings, while .NET only have Unicode strings. That means that all strings in .NET will be packed with prefix type 0x02 and byte arrays with prefix type 0x01. An ASCII string packed in Python will be seen as a byte[] unless you use `IFdbTuple.Get<string>()` that will automatically convert it to Unicode.
* There is no dedicated 'UUID' type prefix, so that means that System.Guid would be serialized as byte arrays, and all instances of byte 0 would need to be escaped. Since `System.Guid` are frequently used as primary keys, I added a new custom type prefix (0x03) for 128-bytes UUIDs. This simplifies packing/unpacking and speeds up writing/reading/comparing Guid keys.

The following files will be required by your application
* `FoundationDB.Client.dll` : Contains the core types (FdbDatabase, FdbTransaction, ...) and infrastructure to connect to a FoundationDB cluster and execute basic queries, as well as the Tuple and Subspace layers.
* `FoundationDB.Layers.Commmon.dll` : Contains common Layers that emulates Tables, Indexes, Document Collections, Blobs, ...
* `fdb_c.dll` : The native C client that you will need to obtain from the official FoundationDB windows setup.

Known Limitations
-----------------

As this is still a work in progress, this should not be used for any production or serious work. Also, the API may (will!) probably change a lot.

What is not working:
* Differences on the tuple layers between Python and .NET that need ironing.
* Timeouts are currently NOT implemented !
* You cannot unload the fdb C native client from the process once the netork thread has started. You can stop it once, but then your process should exit.
* The LINQ API is still a work in progress, and may change a lot.
* Long running batch or range queries may fail with a `past_version` error if they take too much time.

What is working but with some usability problems
* Active cancellation (outside of timeouts) requires you to create and manage a CancellationTokenSource to be able pass a CancellationToken to the async code.
* To emulate the @transactional python attribute (that allows you to pass a database or transaction instance) you have to use db.Attempt.Change((tr) => SomeLayer.SomeMethod(tr, ...)) instead of the simpler SomeLayer.SomeMethod(db, ...)
* Watches are currently wrapped into Task, and cannot be cancelled directly and require passing a valid CancellationToken.

What should work:
* reading/inserting/clearing keys in the database
* range queries
* key selectors
* atomic operations (add, and, or, xor)
* watches
* simple LINQ queries, like Select() or Where() on the result of range queries (to convert Slice key/values into oter types).
