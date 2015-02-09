# Tuples are made of stuff and things...*
*: _Working Title_

_"A tuple is an ordered list of elements."_ - [Wikipedia](http://en.wikipedia.org/wiki/Tuple)

<pre>
//TODO: insert a diagram of a tuple
         1       2                      3
    +---------+-----+--------------------------------------+
t = | "Hello" | 123 | 773166b7-de74-4fcc-845c-84080cc89533 |
    +---------+-----+--------------------------------------+
</pre>

This is a tuple of size 3, which contains 3 elements in a specific order: the first element, the second element and then - you guessed it - the third element.

The difference with a regular struct, is that the elements do not have names (only a position). The difference with an array, is that all the elements can have a different types.

There are various ways to represent a tuple in text, and one of them is as a vector:

<pre>("Hello", 123, {773166b7-de74-4fcc-845c-84080cc89533})</pre>

There is a special case, for the tuple of size 1, where we usually add an extra `,` at the end, to distinguish it from an expression:

<pre>("Hello", )</pre>

And of course you can have an empty tuple, of size 0:

<pre>()</pre>

### The Dark Ages

The absolute minimum implementation of a tuple is a `object[]` array, but this would not be very efficient nor user friendly, especially when you need to encode and decode keys composed of multiple elements with different types. You will probably use a lot of value types (int, Guid, bool, ...) that would need to be boxed, and you will also need to blindly casts items back into their expected type. Now was the 3rd element of this tuple an `int` or a `long`? If you guessed wrong, you will get an `InvalidCastException` at runtime. Uhoh :(

<pre>
// in application A that encoded a key...
var items = new object[] { "Hello", 123, Guid.NewGuid() };
// one allocation for the object[] array, and two allocations to box the int and the guid!
var key = SomeLibrary.Encode(items);

// in a different application B that decodes the same key
var items = SomeLibrary.Decode(key);
var a = (string)items[0];
var b = (long)items[1]; // FAIL: it's actually an int !
var c = (Guid)items[2];
var d = (int)items[3]; // FAIL: there is 4th item !
</pre>

The .NET Framework comes with a set of `Tuple<...>` classes, which gives you the ability to specify the types as well as the number of elements. This gives you type safety and a better intellisense experience.

<pre>
// in application A that encoded a key...
Tuple<string, int, Guid> items = Tuple.Create("Hello", 123, Guid.NewGuid());
// a single allocation for the Tuple instance
var key = SomeLibrary.Encode(items);

// in a different application B that decodes the same key
Tuple<string, int, Guid> items = SomeLibrary.Decode<string, int, Guid>(key);
string a = items.Item1;
int b = items.Item2;
Guid c = items.Item3;
</pre>

This is much better, but unfortunately, the BCL's Tuple<..> classes are relatively barebone and don't offer much in term of fire power. You can't really combine them or split them. They still require you to know that the 2nd element was an `int`, and not a `long` or `uint`.

And quite frankly, if you have used other languages where tuples are first-class citizens (Python, for example), they seem rather bleak.
    
That's way we need a better API, in order to help us be productive in the day to day handling of keys.

## IFdbTuple

The `IFdbTuple` interface, defined in `FoundationDB.Layers.Tuples` (TODO: update this if we rename it!), is the base of all the different tuples implementation, all targetting a specific use case.
    
This interface has the bare minimum API, thats must be implemented by each variant, and is in turn used by a set of extension methods that add more generic behavior that does NOT need to be replicated in all the variants.

There is also a static class, called `FdbTuple`, which holds a bunch of methods to create and handle all the different variants of tuples.

### Types of tuples

Tuples need to adapt to different use case: some tuples should have a fixed size and types (like the BCL Tuples), some should have a variable length (like a vector or list). Some tuples should probably be structs (to reduce the number of allocation in tight loops), while others need to be reference types. And finally, some tuples could be thin wrappers around encoded binary blobs, and defer the decoding of items until they are accessed.

That's why there is multiple variants of tuples, all implementing the `IFdbTuple` interface:

- FdbTuple<T1>, FdbTuple<T1, T2> (up to T5 right now) are the equivalent of the BCL's Tuple<T1, ...> except that they are implemented as a struct. They are efficient when used as a temporary step to create bigger tuples, or when you have control of the actual type (in LINQ queries, inside your own private methods, ...). They are also ideal if you want type safety and nice intellisense support, since the types are known at compile time.
- FdbListTuple wraps an array of object[] and exposes a subset of this array. Getting a substring of this cheap since it does not have to copy the items.
- FdbJoinedTuple is a wrapper that glues together two tuples (of any type).
- FdbLinkedTuple is a special case of an FdbJoinedTupel, where we are only adding one value to an existing tuple.
- FdbSlicedTuple is a wrapper around a half-parsed binary representation of a tuple, and which will only decode items if they are accessed. In cases where you are only interested in part of a key, you won't waste CPU cycles decoding the other items.
- FdbMemoizedTuple will cache its binary representation, which is usefull when you have a common tuple prefix which is used everytime to construct other tuples.
- FdbPrefixedTuple is some sort of hybrid tuples whose binary representation always have a constant binary prefix, which may or may not be a valid binary tuple representation itself (need to use tuples with prefixes generated from a different encoding).

### Creating a tuple

The most simple way to create a tuple, is from its elements:

<pre>
var t = FdbTuple.Create("Hello", 123, Guid.NewGuid());
</pre>

The actual type of the tuple will be `FdbTuple<string, int, Guid>` which is a struct. Since we are using the `var` keyword, then as long as `t` stays inside the method, it will not be boxed.

We can also create a tuple by adding something to an existing tuples, even starting with the Empty tuple:

<pre>
var t = FdbTuple.Empty.Append("Hello").Append(123).Append(Guid.NextGuid());
</pre>

The good news here is that _t_ is still a struct of type `FdbTuple<string, int, Guid>` and we did not produce any allocations: the Empty tuple is a singleton, and all the intermediate Append() returned structs of type `FdbTuple<string>` and `FdbTuple<string, int>`. There is of course a limit to the number of elements that can be added, before we have to switch to an array-based tuple variant.

If we have a variable-size list of items, we can also create a tuple from it:

<pre>
IEnumerable<MyFoo> xs = ....;
// xs is a sequence of MyFoo objects, with an Id property (of type Guid)
var t = FdbTuple.FromSequence(xs.Select(x => x.Id));
</pre>

When all the elements or a tuple are of the same type, you can use specialized versions:
<pre>
var xs = new [] { "Bonjour", "le", "Monde!" };
var t = FdbTuple.FromArray<string>(xs);
</pre>

And for the more adventurous, you can of course create a tuple by copying the elements of an object[] array.

<pre>
var xs = new object[] { "Hello", 123, Guid.NewGuid() };
var t1 = FdbTuple.FromObjects(xs); // => ("hello", 123, guid)
var t2 = FdbTuple.FromObjects(xs, 1, 2); // => (123, guid)
xs[1] = 456; // won't change the content of the tuples
// t[1] => 123
</pre>

If you really want to push it, you can skip copying the items by wrapping an existing array, but then you will break the immutability contract of the Tuples API. Don't try this at home!

<pre>
var xs = new object[] { "Hello", 123, Guid.NewGuid() };
var t1 = FdbTuple.Wrap(xs); // no copy!
var t2 = FdbTuple.Wrap(xs, 1, 2); // no copy!
xs[1] = 456; // will change the content of the tuples!!
// t[1] => 456
</pre>

### Using a tuple

Now that you have a tuple, the first thing you would wan't to know, is its size and if it is empty or not.

All tuples expose a `Count` property which returns the number of elements in the tuple (0 to N).
    
To help you verify that a tuple has the correct size before accessing its elements, there is a set of help extension methods just for that:

- `t.IsNullOrEmpty()` returns `true` if either `t == null` or `t.Count == 0`
- `t.OfSize(3)` checks that `t` is not null, and that `t.Count` is equal to 3, and then returns the tuple itself, so you can write: `t.OfSize(3).DoSomethingWichExceptsThreeElements()`
- `t.OfSizeAtLeast(3)` (and `t.OfSizeAtMost(3)`) work the same, except they check that `t.Count >= 3` (or `t.Count <= 3`)

Of course, if you have one of the FdbTuple<T1, ...> struct, you can skip this step, since the size if known at compile time.

To read the content of a tuple, you can simply call `t.Get<T>(index)`, where `index` is the offset _in the tuple_ of the element, and `T` is the type into which the value will be converted.

<pre>
var t = FdbTuple.Create("hello", 123, Guid.NewGuid());
var x = t.Get<string>(0); // => "hello"
var y = t.Get<int>(1); // => 123
var z = t.Get<Guid>(2); // => guid
</pre>

If `index` is negative, then it is relative to the end of the tuple, where -1 is the last element, -2 is the next-to-last element, and -N is the first element.

<pre>
var t = FdbTuple.Create("hello", 123, Guid.NewGuid());
var x = t.Get<string>(-3); // => "hello"
var y = t.Get<int>(-2); // => 123
var z = t.Get<Guid>(-1); // => guid
</pre>

### Pretty Printing

Code that manipulate tuples can get complex pretty fast, so you need a way to display the content of a tuple is a nice and understable way.

For that, every tuple overrides `ToString()` to return a nicely formatted string with a standardized format.

<pre>
var t1 = FdbTuple.Create("hello", 123, Guid.NewGuid());
Console.WriteLine("t1 = {0}", t1);
// => t1 = ("hello", 123, {773166b7-de74-4fcc-845c-84080cc89533})
var t2 = FdbTuple.Create("hello");
Console.WriteLine("t1 = {0}", t2);
// => t2 = ("hello",)
var t3 = FdbTuple.Empty;
Console.WriteLine("t3 = {0}", t3);
// => t3 = ()
</pre>

There is a special case for tuples of size 1, which have a trailing comma - `(123,)` instead of `(123)` - so that they can be distinguished from a normal expression in parenthesis.

### Combining tuples

Since tuples are immutable, there are no methods to modify the value of an element. You'd do that by creating a new tuple, with a combination of Substring, Append or Concat.

You can, though, modify tuples by returning a new tuple, with or without copying the items (depending on the tuple variant being used).

- Append, Concat
- Substring, [..,..]

### Tuples all the way down

Since a tuple is just a vector of elements, you can of course put a tuple inside another tuple.

This works:

<pre>
var t1 = FdbTuple.Create("hello", FdbTuple(123, 456), Guid.NewGuid());
// t1 = ("hello", (123, 456), {773166b7-de74-4fcc-845c-84080cc89533})
var t2 = FdbTuple.Create(FdbTuple.Create("a", "b"));
// t2 = ((a, b),)
var t3 = FdbTuple.Create("hello", FdbTuple.Empty, "world");
// t3 = ("hello", (), "world");
</pre>

_note: The easy mistake is to call t1.Append(t2) instead of t1.Concat(t2), which will add t2 as a single element at the end of t1, instead of adding t2's elements ad the end of t1._

### TODO

- `t.Append<T>(value)`: returns a new tuple which will have all elements of _t_, plus one extra item.
- `t.Concat(u)`: returns a new tuple which will have all the elements of _t_ followed by all the elements of _u_.
- `t[i, j]`: returns a new tuples with all the elements of t if their index _p_ is between _i_ (included) and _j_ (excluded), so `i <= p < j`.
- `t.Substring(i, n)`: returns a new tuples with all the element of t, starting at index _i_ and taking only _n_ elements.
- `t[i]`: returns the value of the _ith_ element, as an `object`. This method should not be used because it is dangerous (the runtime type of the value can change depending on the circumstances).

