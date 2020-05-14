A transaction is the main type that allows to interact with the content of the database, either by reading the values of keys, and/or modifying them.

There is two types of transactions: read-only and read/write transactions.
- Read-only transactions can only read from the database, and don't need to commit. They will first attempt to obtain a valid _read version_ from the cluster, and all the reads will be performed at the point in time.
- A read/write transaction will also be able to mutate the content of the database, at commit time. Though, if any of the keys read by this transaction was mutated concurrently by another transaction, it will fail to commit because of a conflict.

To help the application designer know what is allowed or not, these transactions types are exposed via the main interfaces `IFdbReadOnlyTransaction` and `IFdbTransaction` (which implements `IFdbReadOnlyTransaction`)

Example:
````csharp
// read-only transaction
using (IFdbReadOnlyTransaction tr = await db.BeginReadOnlyTransaction(ct)) 
{
	Slice value1 = await tr.GetAsync(key1);
	Slice value2 = await tr.GetAsync(key2);
	List<KeyValuePair<Slice, Slice>> values = await tr.GetRange(keyBeginInclusive, keyEndExclusive).ToListAsync();
}

// read/write transaction
using (IFdbTransaction tr = await db.BeginTransaction(ct))
{
	Slice value1 = await tr.GetAsync(key1); // we can read
	tr.SetAsync(key2, value2); // we can write to a key
	tr.Clear(key3); // we can delete a key
	tr.ClearRange(keyBeginInclusive, keyEndExclusive); // or delete a range of keys

	await tr.CommitAsync(); // nothing will be actually changed in the database until we call this method!
}
```
**Important**: Applications can manually create and manage transcations, like in the above example, but this is not the expected way to write robust cost.

In the most common scenario, applications will use retry-loops (see below) to make their life easy:
- Transcation conflicting with one another is a normal and expected outcome, for some algorithms. If this happen, the transaction must be retried in the hope that it will succeed.
- There is also a lot of possible transient errors that can temporarily prevent a transaction from committing, but would not happen on the next retry.
- Controlling the lifetime of a transaction and deciding which errors can be retried (and for how long) and which errors are not retry-able is not easy.

To simplify this, all `IFdbDatabase` instances support several retry loop implementations, accessible via any of the `ReadAsync`, `ReadWriteAsync` or `WriteAsync` helper methods.

These methods implement retry-loops that are specialized for different use-cases:
- `ReadAsync` is for read-only transaction, that are only able to read keys. Any attempt to modify keys will throw exceptions.
- `WriteAsync` is for read/write transactions that do not produce any result (equivalent to a `void` method).
- `ReadWriteAsync`is for read/write trasanctions that produce a result.

The handler that is passed to a retry loop will be executed at least once. Only the result of the last iteration will be returned.
- In the best case scenario, the handle will only be called once, and there will be no retries.
- In case a retry-able error occurs, the transaction context is reset, and the handler is run again.
- In case a non-retryable error occurs, the retry loop aborts execution, and the error is rethrown to the caller.

All retry loops take a caller-provided CancellationToken that will be able to cancel the execution from the outside, like for example using the cancellation token of the HTTP request.
In some scenarios, when there is not timeout or retry limit specified, this cancellation token will be the _only_ way to abort a transaction that is being blocked on an external outage!

Example:

```csharp
CancellationToken ct = ....;

// Read the value of key 'Hello'
var result1 = await db.ReadAsync(tr => tr.GetAsync(key1, ct);

// Change it to something else
await db.WriteAsync(tr => tr.Set(key1, value1), ct);

// Read it back and change another key
var result2 = await db.ReadWriteAsync(tr =>
{
	tr.Set(key2, value2);
	return tr.GetAsync(key1);
}, ct);
```

**Note**: the reason for having both `ReadWriteAsync` and `WriteAsync` is due to some limitations of the C# compiler type resolution.
Most "write only" method return `Task`, and "read/write" methods return a `Task<T>` which can be down-casted to `Task`, causing type resolution ambiguities.
Having different names for "Task" vs "Task<T>" helps work around these limitations. The convention is that everything with "Read" will return some value to the caller.

