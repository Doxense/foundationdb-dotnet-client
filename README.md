FoundationDB.Net Client
=======================

This is a prototype .NET wrapper for FoundationDB Client C API

**THIS IS PROTOTYPE QUALITY, DO NOT USE IN PRODUCTION**

The .NET binding is licensed under the 3-clause BSD Licence. 

It requires the .NET 4.5 Framework, and uses the 64-bit C API binding that is licensed by FoundationDB LLC and must be obtained separately.

It currently targets the Beta 2 of FoundationDB (API level 22)

How to use
----------

```CSharp

// Connect to the db "DB" using the default local cluster file
using (var db = await Fdb.OpenLocalDatabaseAsync("DB"))
{
    // we will use a subspace for all our data
    var location = db.Partition("Test");
    
    // starts a transaction to write some keys to the db
    using (var trans = db.BeginTransaction())
    {
        // ("Test", "Hello", ) = "World"
        trans.Set(location.Pack("Hello"), Slice.FromString("World"));

        // ("Test", "Count", ) = 42
        trans.Set(location.Pack("Count", Slice.FromInt32(42));
        
        // commits the transaction
        await trans.CommitAsync();
    }

    // starts another transaction to read some keys
    using (var trans = db.BeginTransaction())
    {  
        Slice value = await trans.GetAsync(location.Pack("Hello"));
        Console.WriteLine(value.ToUnicode()); // -> Hello
    
        value = await trans.GetAsync(location.Pack("Count"));
        Console.WriteLine(value.ToInt32()); // -> 42
    
        value = await trans.GetAsync(location.Pack("NotFound"));
        Console.WriteLine(value.HasValue); // -> false
        Console.WriteLine(value == Slice.Nul); // -> true
        
        // no writes, so we need to commit
    }

    // We can also do async "LINQ" queries
    using(var trans = db.BeginTransaction())
    {
        // create a list prefix
        var list = location.Create("List");
    
        // add some data to the list
        trans.Set(list.Pack(0), Slice.FromString("AAA"));
        trans.Set(list.Pack(1), Slice.FromString("BBB"));
        trans.Set(list.Pack(2), Slice.FromString("CCC"));
    
        // do a range query on the list prefix, that returns the pairs (int index, string value).        
        var results = await (trans.
            .GetRangeStartsWith(list)
            .Select((kvp) => new KeyValuePair<int, string>(
                location.Unpack(kvp.Key).Get<int>(-1), // unpack the tuple and returns the last item as an int
                kvp.Value.ToUnicode() // convert the value into an unicode string
            ))
            .ToListAsync());

       // list[0] -> <int, string>(0, "AAA")
       // list[1] -> <int, string>(1, "BBB")
       // list[2] -> <int, string>(2, "CCC")
        
    }
    
}


```

How to build
------------

You will need Visual Studio .NET 2012 and .NET 4.5 to compile the solution.

You will also need to obtain the 'fdb_c.dll' C API binding from the foundationdb.com wesite, and copy it in the client project folder.

If you already have installed FoundationDB you can skip to step 3

* Go to http://foundationdb.com/get/ and download the Windows x64 MSI. You may need to register for the Beta program to have access to these files.
* Install the MSI, selecting the default options. Note the installation path.
* Go to `C:\Program Files\foundationdb\bin\` and copy the `fdb_c.dll` into the `FoundationDb.Client\` folder of the VS solution (same folder where the FoundationDb.Client.csproj is)
* Go up one folder, and open the FoundationDb.Client.sln file in Visual Studio 2012
* Choose the Release or Debug configuration, and rebuild the solution.

If you see the error `Could not copy the file "....\FoundationDb.Client\fdb_c.dll" because it was not found` please make sure you have followed the previous steps.

If you see errors on 'await' or 'async' keywords, please make sure that you are using Visual Studio 2012, and not an earlier version.

The nuget package restore has been enabled on the solution, so it should redownload the missing packages during the first build.

How to test
-----------

The test project is using NUnit 2.6.2, and requires support for async test methods.

If you are using a custom runner or VS plugin (like TestDriven.net), make sure that is has the correct nunit version, and that it is configured to run the test using 64-bit process. The code will NOT work on 32 bit.

WARNING: some tests will clear the local database and you may lose all your data. You can speicify an alternative cluster file to use in `TestHelper.cs` file.

Implementation Notes
--------------------

Please refer to http://foundationdb.com/documentation/beta2/ to get an overview on the FoundationDB API, if you haven't already.

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

What should work okay:
* reading/inserting/clearing keys in the database
* range queries
* key selectors
* simple LINQ queries, like Select() or Where() on the result of range queries (to convert Slice key/values into oter types).



