FoundationDB-dotnet-Client
==========================

This is a prototype .NET wrapper for FoundationDB Client C API

*** THIS IS PROTOTYPE QUALITY, DO NOT USE IN PRODUCTION ***

The .NET binding is licensed under the 3-clause BSD Licence. 

It requires the .NET 4.5 Framework, and uses the 64-bit C API binding that is licensed by FoundationDB LLC and must be obtained separately.

How to build
============

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
===========

The test project is using NUnit 2.6.2, and requires support for async test methods.

If you are using a custom runner or VS plugin (like TestDriven.net), make sure that is has the correct nunit version, and that it is configured to run the test using 64-bit process. The code will NOT work on 32 bit.

Implementation Notes
====================

Please refer to http://foundationdb.com/documentation/beta2/ yo get an overview on the FoundationDB API, if you haven't already.

This .NET binding as been modeled to be as close as possible to the other bindings (Python especially), while still having a '.NET' style API. 

There were a few design goals, that you may agree with or not:
* Reducing the need to allocate byte[] as much as possible. To achieve that, I'm using a 'Slice' struct that is a glorified ArraySegment<byte>. All allocations tries to process a single request try to use a single underlying byte[] array, and split it into several chunks.
* Mapping FoudnationDB's Future into Task<T> to be able to use async/await. This means that .NET 4.5 is required to use this binding. It would be possible to port the binding to .NET 4.0 using the `Microsoft.Bcl.Async` nuget package.
* Reducing the risks of memory leaks int long running processes by wrapping all FDB_xxx handles into SafeHandles. This add a little overhead when P/Invoking into the native client, but will guarantee that all handle get released at some time.
* The Tuple layer has been optimized to also reduce the number of allocation required, and cache the packed version of oftenly used tuples (in subspaces, for example).

However, there are some key differences between Python and .NET that may cause problems:
* Python's dynamic types and auto casting of Tuples values, are difficult to model in .NET (without relying on the DLR). The tuple implementation try to be as dynamic as possible, but there if you want to be safe, please try to only use strings, longs, booleans and byte[] to be 100% compatible with other bindings. You should refrain from using the untyped `tuple[index]` (that returns an object) an instead use the generic `tuple.Get<T>(index)` that will try to adapt the underlying type into a T.
* The tuple layer uses ascii and unicode strings, while .NET only have unicode strings. That means that all strings in .NET will be packed with prefix type 0x02 ad byte[] arrays with prefix type 0x01. An ascii string packed in python will be seen as a byte[] unless you use  IFdbTuple.Get<string> that will automatically convert it.
* System.Guid are serialized using a dedicated type prefix (0x03) that is not supported by other bindings. This simplifies packing/unpacking a lot.

