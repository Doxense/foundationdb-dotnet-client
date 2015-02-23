#region BSD Licence
/* Copyright (c) 2013-2015, Doxense SAS
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

//#define MEASURE

namespace FoundationDB.Layers.Tuples.Tests
{
	using FoundationDB.Client;
	using FoundationDB.Client.Converters;
	using FoundationDB.Client.Tests;
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Net;
	using System.Text;

	[TestFixture]
	public class TupleFacts : FdbTest
	{

#if MEASURE
		[TestFixtureTearDown]
		public void DumpStats()
		{
			Log("# MemCopy:");
			for (int i = 0; i < SliceHelpers.CopyHistogram.Length; i++)
			{
				if (SliceHelpers.CopyHistogram[i] == 0) continue;
				Log("# {0} : {1:N0} ({2:N1} ns, {3:N3} ns/byte)", i, SliceHelpers.CopyHistogram[i], SliceHelpers.CopyDurations[i] / SliceHelpers.CopyHistogram[i], SliceHelpers.CopyDurations[i] / (SliceHelpers.CopyHistogram[i] * i));
			}
			Log("# MemCompare:");
			for (int i = 0; i < SliceHelpers.CompareHistogram.Length; i++)
			{
				if (SliceHelpers.CompareHistogram[i] == 0) continue;
				Log("# {0} : {1:N0} ({2:N1} ns, {3:N3} ns/byte)", i, SliceHelpers.CompareHistogram[i], SliceHelpers.CompareDurations[i] / SliceHelpers.CompareHistogram[i], SliceHelpers.CompareDurations[i] / (SliceHelpers.CompareHistogram[i] * i));
			}
		}
#endif

		#region General Use...

		[Test]
		public void Test_FdbTuple_Create()
		{
			var t1 = FdbTuple.Create("hello world");
			Assert.That(t1.Count, Is.EqualTo(1));
			Assert.That(t1.Item1, Is.EqualTo("hello world"));
			Assert.That(t1.Get<string>(0), Is.EqualTo("hello world"));
			Assert.That(t1[0], Is.EqualTo("hello world"));

			var t2 = FdbTuple.Create("hello world", 123);
			Assert.That(t2.Count, Is.EqualTo(2));
			Assert.That(t2.Item1, Is.EqualTo("hello world"));
			Assert.That(t2.Item2, Is.EqualTo(123));
			Assert.That(t2.Get<string>(0), Is.EqualTo("hello world"));
			Assert.That(t2.Get<int>(1), Is.EqualTo(123));
			Assert.That(t2[0], Is.EqualTo("hello world"));
			Assert.That(t2[1], Is.EqualTo(123));

			var t3 = FdbTuple.Create("hello world", 123, false);
			Assert.That(t3.Count, Is.EqualTo(3));
			Assert.That(t3.Item1, Is.EqualTo("hello world"));
			Assert.That(t3.Item2, Is.EqualTo(123));
			Assert.That(t3.Item3, Is.EqualTo(false));
			Assert.That(t3.Get<string>(0), Is.EqualTo("hello world"));
			Assert.That(t3.Get<int>(1), Is.EqualTo(123));
			Assert.That(t3.Get<bool>(2), Is.EqualTo(false));
			Assert.That(t3[0], Is.EqualTo("hello world"));
			Assert.That(t3[1], Is.EqualTo(123));
			Assert.That(t3[2], Is.EqualTo(false));

			var t4 = FdbTuple.Create("hello world", 123, false, 1234L);
			Assert.That(t4.Count, Is.EqualTo(4));
			Assert.That(t4.Item1, Is.EqualTo("hello world"));
			Assert.That(t4.Item2, Is.EqualTo(123));
			Assert.That(t4.Item3, Is.EqualTo(false));
			Assert.That(t4.Item4, Is.EqualTo(1234L));
			Assert.That(t4.Get<string>(0), Is.EqualTo("hello world"));
			Assert.That(t4.Get<int>(1), Is.EqualTo(123));
			Assert.That(t4.Get<bool>(2), Is.EqualTo(false));
			Assert.That(t4.Get<long>(3), Is.EqualTo(1234L));
			Assert.That(t4[0], Is.EqualTo("hello world"));
			Assert.That(t4[1], Is.EqualTo(123));
			Assert.That(t4[2], Is.EqualTo(false));
			Assert.That(t4[3], Is.EqualTo(1234L));

			var t5 = FdbTuple.Create("hello world", 123, false, 1234L, -1234);
			Assert.That(t5.Count, Is.EqualTo(5));
			Assert.That(t5.Item1, Is.EqualTo("hello world"));
			Assert.That(t5.Item2, Is.EqualTo(123));
			Assert.That(t5.Item3, Is.EqualTo(false));
			Assert.That(t5.Item4, Is.EqualTo(1234L));
			Assert.That(t5.Item5, Is.EqualTo(-1234));
			Assert.That(t5.Get<string>(0), Is.EqualTo("hello world"));
			Assert.That(t5.Get<int>(1), Is.EqualTo(123));
			Assert.That(t5.Get<bool>(2), Is.EqualTo(false));
			Assert.That(t5.Get<long>(3), Is.EqualTo(1234L));
			Assert.That(t5.Get<int>(4), Is.EqualTo(-1234));
			Assert.That(t5[0], Is.EqualTo("hello world"));
			Assert.That(t5[1], Is.EqualTo(123));
			Assert.That(t5[2], Is.EqualTo(false));
			Assert.That(t5[3], Is.EqualTo(1234L));
			Assert.That(t5[4], Is.EqualTo(-1234));

			var tn = FdbTuple.Create(new object[] { "hello world", 123, false, 1234L, -1234, "six" });
			Assert.That(tn.Count, Is.EqualTo(6));
			Assert.That(tn.Get<string>(0), Is.EqualTo("hello world"));
			Assert.That(tn.Get<int>(1), Is.EqualTo(123));
			Assert.That(tn.Get<bool>(2), Is.EqualTo(false));
			Assert.That(tn.Get<int>(3), Is.EqualTo(1234));
			Assert.That(tn.Get<long>(4), Is.EqualTo(-1234));
			Assert.That(tn.Get<string>(5), Is.EqualTo("six"));
		}

		[Test]
		public void Test_FdbTuple_Wrap()
		{
			// FdbTuple.Wrap(...) does not copy the items of the array

			var arr = new object[] { "Hello", 123, false, TimeSpan.FromSeconds(5) };

			var t = FdbTuple.Wrap(arr);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Count, Is.EqualTo(4));
			Assert.That(t[0], Is.EqualTo("Hello"));
			Assert.That(t[1], Is.EqualTo(123));
			Assert.That(t[2], Is.EqualTo(false));
			Assert.That(t[3], Is.EqualTo(TimeSpan.FromSeconds(5)));

			t = FdbTuple.Wrap(arr, 1, 2);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Count, Is.EqualTo(2));
			Assert.That(t[0], Is.EqualTo(123));
			Assert.That(t[1], Is.EqualTo(false));

			// changing the underyling array should change the tuple
			// DON'T DO THIS IN ACTUAL CODE!!!

			arr[1] = 456;
			arr[2] = true;
			Log("t = {0}", t);

			Assert.That(t[0], Is.EqualTo(456));
			Assert.That(t[1], Is.EqualTo(true));
		}

		[Test]
		public void Test_FdbTuple_FromObjects()
		{
			// FdbTuple.FromObjects(...) does a copy of the items of the array

			var arr = new object[] { "Hello", 123, false, TimeSpan.FromSeconds(5) };

			var t = FdbTuple.FromObjects(arr);
			Log("t = {0}", t);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Count, Is.EqualTo(4));
			Assert.That(t[0], Is.EqualTo("Hello"));
			Assert.That(t[1], Is.EqualTo(123));
			Assert.That(t[2], Is.EqualTo(false));
			Assert.That(t[3], Is.EqualTo(TimeSpan.FromSeconds(5)));

			t = FdbTuple.FromObjects(arr, 1, 2);
			Log("t = {0}", t);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Count, Is.EqualTo(2));
			Assert.That(t[0], Is.EqualTo(123));
			Assert.That(t[1], Is.EqualTo(false));

			// changing the underyling array should NOT change the tuple

			arr[1] = 456;
			arr[2] = true;
			Log("t = {0}", t);

			Assert.That(t[0], Is.EqualTo(123));
			Assert.That(t[1], Is.EqualTo(false));
		}

		[Test]
		public void Test_FdbTuple_FromArray()
		{
			var items = new string[] { "Bonjour", "le", "Monde" };

			var t = FdbTuple.FromArray(items);
			Log("t = {0}", t);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Count, Is.EqualTo(3));
			Assert.That(t[0], Is.EqualTo("Bonjour"));
			Assert.That(t[1], Is.EqualTo("le"));
			Assert.That(t[2], Is.EqualTo("Monde"));

			t = FdbTuple.FromArray(items, 1, 2);
			Log("t = {0}", t);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Count, Is.EqualTo(2));
			Assert.That(t[0], Is.EqualTo("le"));
			Assert.That(t[1], Is.EqualTo("Monde"));

			// changing the underlying array should NOT change the tuple
			items[1] = "ze";
			Log("t = {0}", t);

			Assert.That(t[0], Is.EqualTo("le"));
		}

		[Test]
		public void Test_FdbTuple_Negative_Indexing()
		{
			var t1 = FdbTuple.Create("hello world");
			Assert.That(t1.Get<string>(-1), Is.EqualTo("hello world"));
			Assert.That(t1[-1], Is.EqualTo("hello world"));

			var t2 = FdbTuple.Create("hello world", 123);
			Assert.That(t2.Get<int>(-1), Is.EqualTo(123));
			Assert.That(t2.Get<string>(-2), Is.EqualTo("hello world"));
			Assert.That(t2[-1], Is.EqualTo(123));
			Assert.That(t2[-2], Is.EqualTo("hello world"));

			var t3 = FdbTuple.Create("hello world", 123, false);
			Assert.That(t3.Get<bool>(-1), Is.EqualTo(false));
			Assert.That(t3.Get<int>(-2), Is.EqualTo(123));
			Assert.That(t3.Get<String>(-3), Is.EqualTo("hello world"));
			Assert.That(t3[-1], Is.EqualTo(false));
			Assert.That(t3[-2], Is.EqualTo(123));
			Assert.That(t3[-3], Is.EqualTo("hello world"));

			var t4 = FdbTuple.Create("hello world", 123, false, 1234L);
			Assert.That(t4.Get<long>(-1), Is.EqualTo(1234L));
			Assert.That(t4.Get<bool>(-2), Is.EqualTo(false));
			Assert.That(t4.Get<int>(-3), Is.EqualTo(123));
			Assert.That(t4.Get<String>(-4), Is.EqualTo("hello world"));
			Assert.That(t4[-1], Is.EqualTo(1234L));
			Assert.That(t4[-2], Is.EqualTo(false));
			Assert.That(t4[-3], Is.EqualTo(123));
			Assert.That(t4[-4], Is.EqualTo("hello world"));

			var t5 = FdbTuple.Create("hello world", 123, false, 1234L, -1234);
			Assert.That(t5.Get<long>(-1), Is.EqualTo(-1234));
			Assert.That(t5.Get<long>(-2), Is.EqualTo(1234L));
			Assert.That(t5.Get<bool>(-3), Is.EqualTo(false));
			Assert.That(t5.Get<int>(-4), Is.EqualTo(123));
			Assert.That(t5.Get<String>(-5), Is.EqualTo("hello world"));
			Assert.That(t5[-1], Is.EqualTo(-1234));
			Assert.That(t5[-2], Is.EqualTo(1234L));
			Assert.That(t5[-3], Is.EqualTo(false));
			Assert.That(t5[-4], Is.EqualTo(123));
			Assert.That(t5[-5], Is.EqualTo("hello world"));

			var tn = FdbTuple.Create(new object[] { "hello world", 123, false, 1234, -1234, "six" });
			Assert.That(tn.Get<string>(-1), Is.EqualTo("six"));
			Assert.That(tn.Get<int>(-2), Is.EqualTo(-1234));
			Assert.That(tn.Get<long>(-3), Is.EqualTo(1234));
			Assert.That(tn.Get<bool>(-4), Is.EqualTo(false));
			Assert.That(tn.Get<int>(-5), Is.EqualTo(123));
			Assert.That(tn.Get<string>(-6), Is.EqualTo("hello world"));
			Assert.That(tn[-1], Is.EqualTo("six"));
			Assert.That(tn[-2], Is.EqualTo(-1234));
			Assert.That(tn[-3], Is.EqualTo(1234));
			Assert.That(tn[-4], Is.EqualTo(false));
			Assert.That(tn[-5], Is.EqualTo(123));
			Assert.That(tn[-6], Is.EqualTo("hello world"));
		}

		[Test]
		public void Test_FdbTuple_First_And_Last()
		{
			// tuple.First<T>() should be equivalent to tuple.Get<T>(0)
			// tuple.Last<T>() should be equivalent to tuple.Get<T>(-1)

			var t1 = FdbTuple.Create(1);
			Assert.That(t1.First<int>(), Is.EqualTo(1));
			Assert.That(t1.First<string>(), Is.EqualTo("1"));
			Assert.That(((IFdbTuple)t1).Last<int>(), Is.EqualTo(1));
			Assert.That(((IFdbTuple)t1).Last<string>(), Is.EqualTo("1"));

			var t2 = FdbTuple.Create(1, 2);
			Assert.That(t2.First<int>(), Is.EqualTo(1));
			Assert.That(t2.First<string>(), Is.EqualTo("1"));
			Assert.That(t2.Last, Is.EqualTo(2));
			Assert.That(((IFdbTuple)t2).Last<int>(), Is.EqualTo(2));
			Assert.That(((IFdbTuple)t2).Last<string>(), Is.EqualTo("2"));

			var t3 = FdbTuple.Create(1, 2, 3);
			Assert.That(t3.First<int>(), Is.EqualTo(1));
			Assert.That(t3.First<string>(), Is.EqualTo("1"));
			Assert.That(t3.Last, Is.EqualTo(3));
			Assert.That(((IFdbTuple)t3).Last<int>(), Is.EqualTo(3));
			Assert.That(((IFdbTuple)t3).Last<string>(), Is.EqualTo("3"));

			var t4 = FdbTuple.Create(1, 2, 3, 4);
			Assert.That(t4.First<int>(), Is.EqualTo(1));
			Assert.That(t4.First<string>(), Is.EqualTo("1"));
			Assert.That(t4.Last, Is.EqualTo(4));
			Assert.That(((IFdbTuple)t4).Last<int>(), Is.EqualTo(4));
			Assert.That(((IFdbTuple)t4).Last<string>(), Is.EqualTo("4"));

			var t5 = FdbTuple.Create(1, 2, 3, 4, 5);
			Assert.That(t5.First<int>(), Is.EqualTo(1));
			Assert.That(t5.First<string>(), Is.EqualTo("1"));
			Assert.That(t5.Last, Is.EqualTo(5));
			Assert.That(((IFdbTuple)t5).Last<int>(), Is.EqualTo(5));
			Assert.That(((IFdbTuple)t5).Last<string>(), Is.EqualTo("5"));

			var tn = FdbTuple.Create(1, 2, 3, 4, 5, 6);
			Assert.That(tn.First<int>(), Is.EqualTo(1));
			Assert.That(tn.First<string>(), Is.EqualTo("1"));
			Assert.That(tn.Last<int>(), Is.EqualTo(6));
			Assert.That(tn.Last<string>(), Is.EqualTo("6"));

			Assert.That(() => FdbTuple.Empty.First<string>(), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => FdbTuple.Empty.Last<string>(), Throws.InstanceOf<InvalidOperationException>());
		}

		[Test]
		public void Test_FdbTuple_Unpack_First_And_Last()
		{
			// should only work with tuples having at least one element

			Slice packed;

			packed = FdbTuple.EncodeKey(1);
			Assert.That(FdbTuple.DecodeFirst<int>(packed), Is.EqualTo(1));
			Assert.That(FdbTuple.DecodeFirst<string>(packed), Is.EqualTo("1"));
			Assert.That(FdbTuple.DecodeLast<int>(packed), Is.EqualTo(1));
			Assert.That(FdbTuple.DecodeLast<string>(packed), Is.EqualTo("1"));

			packed = FdbTuple.EncodeKey(1, 2);
			Assert.That(FdbTuple.DecodeFirst<int>(packed), Is.EqualTo(1));
			Assert.That(FdbTuple.DecodeFirst<string>(packed), Is.EqualTo("1"));
			Assert.That(FdbTuple.DecodeLast<int>(packed), Is.EqualTo(2));
			Assert.That(FdbTuple.DecodeLast<string>(packed), Is.EqualTo("2"));

			packed = FdbTuple.EncodeKey(1, 2, 3);
			Assert.That(FdbTuple.DecodeFirst<int>(packed), Is.EqualTo(1));
			Assert.That(FdbTuple.DecodeFirst<string>(packed), Is.EqualTo("1"));
			Assert.That(FdbTuple.DecodeLast<int>(packed), Is.EqualTo(3));
			Assert.That(FdbTuple.DecodeLast<string>(packed), Is.EqualTo("3"));

			packed = FdbTuple.EncodeKey(1, 2, 3, 4);
			Assert.That(FdbTuple.DecodeFirst<int>(packed), Is.EqualTo(1));
			Assert.That(FdbTuple.DecodeFirst<string>(packed), Is.EqualTo("1"));
			Assert.That(FdbTuple.DecodeLast<int>(packed), Is.EqualTo(4));
			Assert.That(FdbTuple.DecodeLast<string>(packed), Is.EqualTo("4"));

			packed = FdbTuple.EncodeKey(1, 2, 3, 4, 5);
			Assert.That(FdbTuple.DecodeFirst<int>(packed), Is.EqualTo(1));
			Assert.That(FdbTuple.DecodeFirst<string>(packed), Is.EqualTo("1"));
			Assert.That(FdbTuple.DecodeLast<int>(packed), Is.EqualTo(5));
			Assert.That(FdbTuple.DecodeLast<string>(packed), Is.EqualTo("5"));

			packed = FdbTuple.EncodeKey(1, 2, 3, 4, 5, 6);
			Assert.That(FdbTuple.DecodeFirst<int>(packed), Is.EqualTo(1));
			Assert.That(FdbTuple.DecodeFirst<string>(packed), Is.EqualTo("1"));
			Assert.That(FdbTuple.DecodeLast<int>(packed), Is.EqualTo(6));
			Assert.That(FdbTuple.DecodeLast<string>(packed), Is.EqualTo("6"));

			packed = FdbTuple.EncodeKey(1, 2, 3, 4, 5, 6, 7);
			Assert.That(FdbTuple.DecodeFirst<int>(packed), Is.EqualTo(1));
			Assert.That(FdbTuple.DecodeFirst<string>(packed), Is.EqualTo("1"));
			Assert.That(FdbTuple.DecodeLast<int>(packed), Is.EqualTo(7));
			Assert.That(FdbTuple.DecodeLast<string>(packed), Is.EqualTo("7"));

			packed = FdbTuple.EncodeKey(1, 2, 3, 4, 5, 6, 7, 8);
			Assert.That(FdbTuple.DecodeFirst<int>(packed), Is.EqualTo(1));
			Assert.That(FdbTuple.DecodeFirst<string>(packed), Is.EqualTo("1"));
			Assert.That(FdbTuple.DecodeLast<int>(packed), Is.EqualTo(8));
			Assert.That(FdbTuple.DecodeLast<string>(packed), Is.EqualTo("8"));

			Assert.That(() => FdbTuple.DecodeFirst<string>(Slice.Nil), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => FdbTuple.DecodeFirst<string>(Slice.Empty), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => FdbTuple.DecodeLast<string>(Slice.Nil), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => FdbTuple.DecodeLast<string>(Slice.Empty), Throws.InstanceOf<InvalidOperationException>());

		}

		[Test]
		public void Test_FdbTuple_UnpackSingle()
		{
			// should only work with tuples having exactly one element

			Slice packed;

			packed = FdbTuple.EncodeKey(1);
			Assert.That(FdbTuple.DecodeKey<int>(packed), Is.EqualTo(1));
			Assert.That(FdbTuple.DecodeKey<string>(packed), Is.EqualTo("1"));

			packed = FdbTuple.EncodeKey("Hello\0World");
			Assert.That(FdbTuple.DecodeKey<string>(packed), Is.EqualTo("Hello\0World"));

			Assert.That(() => FdbTuple.DecodeKey<string>(Slice.Nil), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => FdbTuple.DecodeKey<string>(Slice.Empty), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => FdbTuple.DecodeKey<int>(FdbTuple.EncodeKey(1, 2)), Throws.InstanceOf<FormatException>());
			Assert.That(() => FdbTuple.DecodeKey<int>(FdbTuple.EncodeKey(1, 2, 3)), Throws.InstanceOf<FormatException>());
			Assert.That(() => FdbTuple.DecodeKey<int>(FdbTuple.EncodeKey(1, 2, 3, 4)), Throws.InstanceOf<FormatException>());
			Assert.That(() => FdbTuple.DecodeKey<int>(FdbTuple.EncodeKey(1, 2, 3, 4, 5)), Throws.InstanceOf<FormatException>());
			Assert.That(() => FdbTuple.DecodeKey<int>(FdbTuple.EncodeKey(1, 2, 3, 4, 5, 6)), Throws.InstanceOf<FormatException>());
			Assert.That(() => FdbTuple.DecodeKey<int>(FdbTuple.EncodeKey(1, 2, 3, 4, 5, 6, 7)), Throws.InstanceOf<FormatException>());
			Assert.That(() => FdbTuple.DecodeKey<int>(FdbTuple.EncodeKey(1, 2, 3, 4, 5, 6, 7, 8)), Throws.InstanceOf<FormatException>());

		}

		[Test]
		public void Test_FdbTuple_Embedded_Tuples()
		{
			// (A,B).Append((C,D)) should return (A,B,(C,D)) (length 3) and not (A,B,C,D) (length 4)

			FdbTuple<string, string> x = FdbTuple.Create("A", "B");
			FdbTuple<string, string> y = FdbTuple.Create("C", "D");

			// using the instance method that returns a FdbTuple<T1, T2, T3>
			IFdbTuple z = x.Append(y);
			Log(z);
			Assert.That(z, Is.Not.Null);
			Assert.That(z.Count, Is.EqualTo(3));
			Assert.That(z[0], Is.EqualTo("A"));
			Assert.That(z[1], Is.EqualTo("B"));
			Assert.That(z[2], Is.EqualTo(y));
			var t = z.Get<IFdbTuple>(2);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Count, Is.EqualTo(2));
			Assert.That(t[0], Is.EqualTo("C"));
			Assert.That(t[1], Is.EqualTo("D"));

			// casted down to the interface IFdbTuple
			z = ((IFdbTuple)x).Append((IFdbTuple)y);
			Log(z);
			Assert.That(z, Is.Not.Null);
			Assert.That(z.Count, Is.EqualTo(3));
			Assert.That(z[0], Is.EqualTo("A"));
			Assert.That(z[1], Is.EqualTo("B"));
			Assert.That(z[2], Is.EqualTo(y));
			t = z.Get<IFdbTuple>(2);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Count, Is.EqualTo(2));
			Assert.That(t[0], Is.EqualTo("C"));
			Assert.That(t[1], Is.EqualTo("D"));

			// composite index key "(prefix, value, id)"
			IFdbTuple subspace = FdbTuple.Create(123, 42);
			IFdbTuple value = FdbTuple.Create(2014, 11, 6); // Indexing a date value (Y, M, D)
			string id = "Doc123";
			z = subspace.Append(value, id);
			Log(z);
			Assert.That(z.Count, Is.EqualTo(4));
		}

		[Test]
		public void Test_FdbTuple_With()
		{
			//note: important to always cast to (IFdbTuple) to be sure that we don't call specialized instance methods (tested elsewhere)
			IFdbTuple t;

			// Size 1

			t = FdbTuple.Create(123);
			t.With((int a) =>
			{
				Assert.That(a, Is.EqualTo(123));
			});
			Assert.That(t.With((int a) =>
			{
				Assert.That(a, Is.EqualTo(123));
				return 42;
			}), Is.EqualTo(42));

			// Size 2

			t = t.Append("abc");
			t.With((int a, string b) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
			});
			Assert.That(t.With((int a, string b) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				return 42;
			}), Is.EqualTo(42));

			// Size 3

			t = t.Append(3.14f);
			t.With((int a, string b, float c) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
			});
			Assert.That(t.With((int a, string b, float c) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				return 42;
			}), Is.EqualTo(42));

			// Size 4

			t = t.Append(true);
			t.With((int a, string b, float c, bool d) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
			});
			Assert.That(t.With((int a, string b, float c, bool d) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				return 42;
			}), Is.EqualTo(42));

			// Size 5

			t = t.Append('z');
			t.With((int a, string b, float c, bool d, char e) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				Assert.That(e, Is.EqualTo('z'));
			});
			Assert.That(t.With((int a, string b, float c, bool d, char e) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				Assert.That(e, Is.EqualTo('z'));
				return 42;
			}), Is.EqualTo(42));

			// Size 6

			t = t.Append(Math.PI);
			t.With((int a, string b, float c, bool d, char e, double f) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				Assert.That(e, Is.EqualTo('z'));
				Assert.That(f, Is.EqualTo(Math.PI));
			});
			Assert.That(t.With((int a, string b, float c, bool d, char e, double f) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				Assert.That(e, Is.EqualTo('z'));
				Assert.That(f, Is.EqualTo(Math.PI));
				return 42;
			}), Is.EqualTo(42));

			// Size 7

			t = t.Append(IPAddress.Loopback);
			t.With((int a, string b, float c, bool d, char e, double f, IPAddress g) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				Assert.That(e, Is.EqualTo('z'));
				Assert.That(f, Is.EqualTo(Math.PI));
				Assert.That(g, Is.EqualTo(IPAddress.Loopback));
			});
			Assert.That(t.With((int a, string b, float c, bool d, char e, double f, IPAddress g) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				Assert.That(e, Is.EqualTo('z'));
				Assert.That(f, Is.EqualTo(Math.PI));
				Assert.That(g, Is.EqualTo(IPAddress.Loopback));
				return 42;
			}), Is.EqualTo(42));

			// Size 8

			t = t.Append(DateTime.MaxValue);
			t.With((int a, string b, float c, bool d, char e, double f, IPAddress g, DateTime h) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				Assert.That(e, Is.EqualTo('z'));
				Assert.That(f, Is.EqualTo(Math.PI));
				Assert.That(g, Is.EqualTo(IPAddress.Loopback));
				Assert.That(h, Is.EqualTo(DateTime.MaxValue));
			});
			Assert.That(t.With((int a, string b, float c, bool d, char e, double f, IPAddress g, DateTime h) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				Assert.That(e, Is.EqualTo('z'));
				Assert.That(f, Is.EqualTo(Math.PI));
				Assert.That(g, Is.EqualTo(IPAddress.Loopback));
				Assert.That(h, Is.EqualTo(DateTime.MaxValue));
				return 42;
			}), Is.EqualTo(42));

		}

		[Test]
		public void Test_FdbTuple_With_Struct()
		{
			// calling With() on the structs is faster

			FdbTuple<int> t1 = FdbTuple.Create(123);
			t1.With((a) =>
			{
				Assert.That(a, Is.EqualTo(123));
			});
			Assert.That(t1.With((a) =>
			{
				Assert.That(a, Is.EqualTo(123));
				return 42;
			}), Is.EqualTo(42));

			FdbTuple<int, string> t2 = FdbTuple.Create(123, "abc");
			t2.With((a, b) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
			});
			Assert.That(t2.With((a, b) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				return 42;
			}), Is.EqualTo(42));

			FdbTuple<int, string, float> t3 = FdbTuple.Create(123, "abc", 3.14f);
			t3.With((a, b, c) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
			});
			Assert.That(t3.With((a, b, c) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				return 42;
			}), Is.EqualTo(42));

			FdbTuple<int, string, float, bool> t4 = FdbTuple.Create(123, "abc", 3.14f, true);
			t4.With((a, b, c, d) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
			});
			Assert.That(t4.With((a, b, c, d) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				return 42;
			}), Is.EqualTo(42));

			FdbTuple<int, string, float, bool, char> t5 = FdbTuple.Create(123, "abc", 3.14f, true, 'z');
			t5.With((a, b, c, d, e) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				Assert.That(e, Is.EqualTo('z'));
			});
			Assert.That(t5.With((a, b, c, d, e) =>
			{
				Assert.That(a, Is.EqualTo(123));
				Assert.That(b, Is.EqualTo("abc"));
				Assert.That(c, Is.EqualTo(3.14f));
				Assert.That(d, Is.True);
				Assert.That(e, Is.EqualTo('z'));
				return 42;
			}), Is.EqualTo(42));

			//TODO: add more if we ever add struct tuples with 6 or more items
		}

		[Test]
		public void Test_FdbTuple_Of_Size()
		{
			// OfSize(n) check the size and return the tuple if it passed
			// VerifySize(n) only check the size
			// Both should throw if tuple is null, or not the expected size

			Action<IFdbTuple> verify = (t) =>
			{
				for (int i = 0; i <= 10; i++)
				{
					if (t.Count > i)
					{
						Assert.That(() => t.OfSize(i), Throws.InstanceOf<InvalidOperationException>());
						Assert.That(t.OfSizeAtLeast(i), Is.SameAs(t));
						Assert.That(() => t.OfSizeAtMost(i), Throws.InstanceOf<InvalidOperationException>());
					}
					else if (t.Count < i)
					{
						Assert.That(() => t.OfSize(i), Throws.InstanceOf<InvalidOperationException>());
						Assert.That(() => t.OfSizeAtLeast(i), Throws.InstanceOf<InvalidOperationException>());
						Assert.That(t.OfSizeAtMost(i), Is.SameAs(t));
					}
					else
					{
						Assert.That(t.OfSize(i), Is.SameAs(t));
						Assert.That(t.OfSizeAtLeast(i), Is.SameAs(t));
						Assert.That(t.OfSizeAtMost(i), Is.SameAs(t));
					}
				}
			};

			verify(FdbTuple.Empty);
			verify(FdbTuple.Create(123));
			verify(FdbTuple.Create(123, "abc"));
			verify(FdbTuple.Create(123, "abc", 3.14f));
			verify(FdbTuple.Create(123, "abc", 3.14f, true));
			verify(FdbTuple.Create(123, "abc", 3.14f, true, 'z'));
			verify(FdbTuple.FromArray(new[] { "hello", "world", "!" }));
			verify(FdbTuple.FromEnumerable(Enumerable.Range(0, 10)));

			verify(FdbTuple.Create(123, "abc", 3.14f, true, 'z')[0, 2]);
			verify(FdbTuple.Create(123, "abc", 3.14f, true, 'z')[1, 4]);
			verify(FdbTuple.FromEnumerable(Enumerable.Range(0, 50)).Substring(15, 6));

			IFdbTuple none = null;
			Assert.That(() => none.OfSize(0), Throws.InstanceOf<ArgumentNullException>());
			Assert.That(() => none.OfSizeAtLeast(0), Throws.InstanceOf<ArgumentNullException>());
			Assert.That(() => none.OfSizeAtMost(0), Throws.InstanceOf<ArgumentNullException>());
		}

		[Test]
		public void Test_FdbTuple_Truncate()
		{
			IFdbTuple t = FdbTuple.Create("Hello", 123, false, TimeSpan.FromSeconds(5), "World");

			var head = t.Truncate(1);
			Assert.That(head, Is.Not.Null);
			Assert.That(head.Count, Is.EqualTo(1));
			Assert.That(head[0], Is.EqualTo("Hello"));

			head = t.Truncate(2);
			Assert.That(head, Is.Not.Null);
			Assert.That(head.Count, Is.EqualTo(2));
			Assert.That(head[0], Is.EqualTo("Hello"));
			Assert.That(head[1], Is.EqualTo(123));

			head = t.Truncate(5);
			Assert.That(head, Is.EqualTo(t));

			var tail = t.Truncate(-1);
			Assert.That(tail, Is.Not.Null);
			Assert.That(tail.Count, Is.EqualTo(1));
			Assert.That(tail[0], Is.EqualTo("World"));

			tail = t.Truncate(-2);
			Assert.That(tail, Is.Not.Null);
			Assert.That(tail.Count, Is.EqualTo(2));
			Assert.That(tail[0], Is.EqualTo(TimeSpan.FromSeconds(5)));
			Assert.That(tail[1], Is.EqualTo("World"));

			tail = t.Truncate(-5);
			Assert.That(tail, Is.EqualTo(t));

			Assert.That(t.Truncate(0), Is.EqualTo(FdbTuple.Empty));
			Assert.That(() => t.Truncate(6), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => t.Truncate(-6), Throws.InstanceOf<InvalidOperationException>());

			Assert.That(() => FdbTuple.Empty.Truncate(1), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => FdbTuple.Create("Hello", "World").Truncate(3), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => FdbTuple.Create("Hello", "World").Truncate(-3), Throws.InstanceOf<InvalidOperationException>());
		}

		[Test]
		public void Test_FdbTuple_As()
		{
			// IFdbTuple.As<...>() adds types to an untyped IFdbTuple
			IFdbTuple t;

			t = FdbTuple.Create("Hello");
            var t1 = t.As<string>();
			Assert.That(t1.Item1, Is.EqualTo("Hello"));

			t = FdbTuple.Create("Hello", 123);
            var t2 = t.As<string, int>();
			Assert.That(t2.Item1, Is.EqualTo("Hello"));
			Assert.That(t2.Item2, Is.EqualTo(123));

			t = FdbTuple.Create("Hello", 123, false);
            var t3 = t.As<string, int, bool>();
			Assert.That(t3.Item1, Is.EqualTo("Hello"));
			Assert.That(t3.Item2, Is.EqualTo(123));
			Assert.That(t3.Item3, Is.EqualTo(false));

			var t4 = FdbTuple
				.Create("Hello", 123, false, TimeSpan.FromSeconds(5))
				.As<string, int, bool, TimeSpan>();
			Assert.That(t4.Item1, Is.EqualTo("Hello"));
			Assert.That(t4.Item2, Is.EqualTo(123));
			Assert.That(t4.Item3, Is.EqualTo(false));
			Assert.That(t4.Item4, Is.EqualTo(TimeSpan.FromSeconds(5)));

			t = FdbTuple.Create("Hello", 123, false, TimeSpan.FromSeconds(5), "World");
			var t5 = t.As<string, int, bool, TimeSpan, string>();
			Assert.That(t5.Item1, Is.EqualTo("Hello"));
			Assert.That(t5.Item2, Is.EqualTo(123));
			Assert.That(t5.Item3, Is.EqualTo(false));
			Assert.That(t5.Item4, Is.EqualTo(TimeSpan.FromSeconds(5)));
			Assert.That(t5.Item5, Is.EqualTo("World"));
		}

		[Test]
		public void Test_Cast_To_BCL_Tuples()
		{
			// implicit: Tuple => FdbTuple 
			// explicit: FdbTuple => Tuple

			var t1 = FdbTuple.Create("Hello");
			var b1 = (Tuple<string>) t1; // explicit
			Assert.That(b1, Is.Not.Null);
			Assert.That(b1.Item1, Is.EqualTo("Hello"));
			FdbTuple<string> r1 = t1; // implicit
			Assert.That(r1.Item1, Is.EqualTo("Hello"));

			var t2 = FdbTuple.Create("Hello", 123);
			var b2 = (Tuple<string, int>)t2;	// explicit
			Assert.That(b2, Is.Not.Null);
			Assert.That(b2.Item1, Is.EqualTo("Hello"));
			Assert.That(b2.Item2, Is.EqualTo(123));
			FdbTuple<string, int> r2 = t2; // implicit
			Assert.That(r2.Item1, Is.EqualTo("Hello"));
			Assert.That(r2.Item2, Is.EqualTo(123));

			var t3 = FdbTuple.Create("Hello", 123, false);
			var b3 = (Tuple<string, int, bool>)t3;	// explicit
			Assert.That(b3, Is.Not.Null);
			Assert.That(b3.Item1, Is.EqualTo("Hello"));
			Assert.That(b3.Item2, Is.EqualTo(123));
			Assert.That(b3.Item3, Is.EqualTo(false));
			FdbTuple<string, int, bool> r3 = t3; // implicit
			Assert.That(r3.Item1, Is.EqualTo("Hello"));
			Assert.That(r3.Item2, Is.EqualTo(123));
			Assert.That(r3.Item3, Is.EqualTo(false));

			var t4 = FdbTuple.Create("Hello", 123, false, TimeSpan.FromSeconds(5));
			var b4 = (Tuple<string, int, bool, TimeSpan>)t4;	// explicit
			Assert.That(b4, Is.Not.Null);
			Assert.That(b4.Item1, Is.EqualTo("Hello"));
			Assert.That(b4.Item2, Is.EqualTo(123));
			Assert.That(b4.Item3, Is.EqualTo(false));
			Assert.That(b4.Item4, Is.EqualTo(TimeSpan.FromSeconds(5)));
			FdbTuple<string, int, bool, TimeSpan> r4 = t4; // implicit
			Assert.That(r4.Item1, Is.EqualTo("Hello"));
			Assert.That(r4.Item2, Is.EqualTo(123));
			Assert.That(r4.Item3, Is.EqualTo(false));
			Assert.That(r4.Item4, Is.EqualTo(TimeSpan.FromSeconds(5)));

			var t5 = FdbTuple.Create("Hello", 123, false, TimeSpan.FromSeconds(5), "World");
			var b5 = (Tuple<string, int, bool, TimeSpan, string>)t5;	// explicit
			Assert.That(b5, Is.Not.Null);
			Assert.That(b5.Item1, Is.EqualTo("Hello"));
			Assert.That(b5.Item2, Is.EqualTo(123));
			Assert.That(b5.Item3, Is.EqualTo(false));
			Assert.That(b5.Item4, Is.EqualTo(TimeSpan.FromSeconds(5)));
			Assert.That(b5.Item5, Is.EqualTo("World"));
			FdbTuple<string, int, bool, TimeSpan, string> r5 = t5; // implicit
			Assert.That(r5.Item1, Is.EqualTo("Hello"));
			Assert.That(r5.Item2, Is.EqualTo(123));
			Assert.That(r5.Item3, Is.EqualTo(false));
			Assert.That(r5.Item4, Is.EqualTo(TimeSpan.FromSeconds(5)));
			Assert.That(r5.Item5, Is.EqualTo("World"));

		}

		#endregion

		#region Splicing...

		private static void VerifyTuple(string message, IFdbTuple t, object[] expected)
		{
			// count
			if (t.Count != expected.Length)
			{
#if DEBUG
				if (Debugger.IsAttached) Debugger.Break();
#endif
				Assert.Fail("{0}: Count mismatch between observed {1} and expected {2} for tuple of type {3}", message, t, FdbTuple.ToString(expected), t.GetType().Name);
			}

			// direct access
			for (int i = 0; i < expected.Length; i++)
			{
				Assert.That(ComparisonHelper.AreSimilar(t[i], expected[i]), Is.True, "{0}: t[{1}] != expected[{1}]", message, i);
			}

			// iterator
			int p = 0;
			foreach (var obj in t)
			{
				if (p >= expected.Length) Assert.Fail("Spliced iterator overshoot at t[{0}] = {1}", p, obj);
				Assert.That(ComparisonHelper.AreSimilar(obj, expected[p]), Is.True, "{0}: Iterator[{1}], {2} ~= {3}", message, p, obj, expected[p]);
				++p;
			}
			Assert.That(p, Is.EqualTo(expected.Length), "{0}: t.GetEnumerator() returned only {1} elements out of {2} exected", message, p, expected.Length);

			// CopyTo
			var tmp = new object[expected.Length];
			t.CopyTo(tmp, 0);
			for (int i = 0; i < tmp.Length; i++)
			{
				Assert.That(ComparisonHelper.AreSimilar(tmp[i], expected[i]), Is.True, "{0}: CopyTo[{1}], {2} ~= {3}", message, i, tmp[i], expected[i]);
			}

			// Memoize
			tmp = t.Memoize().ToArray();
			for (int i = 0; i < tmp.Length; i++)
			{
				Assert.That(ComparisonHelper.AreSimilar(tmp[i], expected[i]), Is.True, "{0}: Memoize.Items[{1}], {2} ~= {3}", message, i, tmp[i], expected[i]);
			}

			// Append
			if (!(t is FdbSlicedTuple))
			{
				var u = t.Append("last");
				Assert.That(u.Get<string>(-1), Is.EqualTo("last"));
				tmp = u.ToArray();
				for (int i = 0; i < tmp.Length - 1; i++)
				{
					Assert.That(ComparisonHelper.AreSimilar(tmp[i], expected[i]), Is.True, "{0}: Appended[{1}], {2} ~= {3}", message, i, tmp[i], expected[i]);
				}
			}
		}

		[Test]
		public void Test_Can_Splice_FdbListTuple()
		{
			var items = new object[] { "hello", "world", 123, "foo", 456, "bar" };
			//                            0        1      2     3     4     5
			//                           -6       -5     -4    -3    -2    -1

			var tuple = new FdbListTuple(items);
			Assert.That(tuple.Count, Is.EqualTo(6));

			// get all
			VerifyTuple("[:]", tuple[null, null], items);
			VerifyTuple("[:]", tuple[null, 6], items);
			VerifyTuple("[:]", tuple[0, null], items);
			VerifyTuple("[:]", tuple[0, 6], items);
			VerifyTuple("[:]", tuple[0, null], items);
			VerifyTuple("[:]", tuple[-6, null], items);
			VerifyTuple("[:]", tuple[-6, 6], items);

			// tail
			VerifyTuple("[n:]", tuple[4, null], new object[] { 456, "bar" });
			VerifyTuple("[n:+]", tuple[4, 6], new object[] { 456, "bar" });
			VerifyTuple("[-n:+]", tuple[-2, 6], new object[] { 456, "bar" });
			VerifyTuple("[-n:-]", tuple[-2, null], new object[] { 456, "bar" });

			// head
			VerifyTuple("[:n]", tuple[null, 3], new object[] { "hello", "world", 123 });
			VerifyTuple("[0:n]", tuple[0, 3], new object[] { "hello", "world", 123 });
			VerifyTuple("[0:-n]", tuple[0, -3], new object[] { "hello", "world", 123 });
			VerifyTuple("[-:n]", tuple[-6, 3], new object[] { "hello", "world", 123 });
			VerifyTuple("[-:-n]", tuple[-6, -3], new object[] { "hello", "world", 123 });

			// single
			VerifyTuple("[0:1]", tuple[0, 1], new object[] { "hello" });
			VerifyTuple("[-6:-5]", tuple[-6, -5], new object[] { "hello" });
			VerifyTuple("[1:2]", tuple[1, 2], new object[] { "world" });
			VerifyTuple("[-5:-4]", tuple[-5, -4], new object[] { "world" });
			VerifyTuple("[5:6]", tuple[5, 6], new object[] { "bar" });
			VerifyTuple("[-1:]", tuple[-1, null], new object[] { "bar" });

			// chunk
			VerifyTuple("[2:4]", tuple[2, 4], new object[] { 123, "foo" });
			VerifyTuple("[2:-2]", tuple[2, -2], new object[] { 123, "foo" });
			VerifyTuple("[-4:4]", tuple[-4, 4], new object[] { 123, "foo" });
			VerifyTuple("[-4:-2]", tuple[-4, -2], new object[] { 123, "foo" });

			// remove first
			VerifyTuple("[1:]", tuple[1, null], new object[] { "world", 123, "foo", 456, "bar" });
			VerifyTuple("[1:+]", tuple[1, 6], new object[] { "world", 123, "foo", 456, "bar" });
			VerifyTuple("[-5:]", tuple[-5, null], new object[] { "world", 123, "foo", 456, "bar" });
			VerifyTuple("[-5:+]", tuple[-5, 6], new object[] { "world", 123, "foo", 456, "bar" });

			// remove last
			VerifyTuple("[:5]", tuple[null, 5], new object[] { "hello", "world", 123, "foo", 456 });
			VerifyTuple("[:-1]", tuple[null, -1], new object[] { "hello", "world", 123, "foo", 456 });
			VerifyTuple("[0:5]", tuple[0, 5], new object[] { "hello", "world", 123, "foo", 456 });
			VerifyTuple("[0:-1]", tuple[0, -1], new object[] { "hello", "world", 123, "foo", 456 });

			// out of range
			VerifyTuple("[2:7]", tuple[2, 7], new object[] { 123, "foo", 456, "bar" });
			VerifyTuple("[2:42]", tuple[2, 42], new object[] { 123, "foo", 456, "bar" });
			VerifyTuple("[2:123456]", tuple[2, 123456], new object[] { 123, "foo", 456, "bar" });
			VerifyTuple("[-7:2]", tuple[-7, 2], new object[] { "hello", "world" });
			VerifyTuple("[-42:2]", tuple[-42, 2], new object[] { "hello", "world" });
		}

		private static object[] GetRange(int fromIncluded, int toExcluded, int count)
		{
			if (count == 0) return new object[0];

			if (fromIncluded < 0) fromIncluded += count;
			if (toExcluded < 0) toExcluded += count;

			if (toExcluded > count) toExcluded = count;
			var tmp = new object[toExcluded - fromIncluded];
			for (int i = 0; i < tmp.Length; i++) tmp[i] = new string((char) (65 + fromIncluded + i), 1);
			return tmp;
		}

		[Test]
		public void Test_Randomized_Splices()
		{
			// Test a random mix of sizes, and indexes...

			const int N = 100 * 1000;

			var tuples = new IFdbTuple[14];
			tuples[0] = FdbTuple.Empty;
			tuples[1] = FdbTuple.Create("A");
			tuples[2] = FdbTuple.Create("A", "B");
			tuples[3] = FdbTuple.Create("A", "B", "C");
			tuples[4] = FdbTuple.Create("A", "B", "C", "D");
			tuples[5] = FdbTuple.Create("A", "B", "C", "D", "E");
			tuples[6] = FdbTuple.Create("A", "B", "C", "D", "E", "F");
			tuples[7] = FdbTuple.Create("A", "B", "C", "D", "E", "F", "G");
			tuples[8] = FdbTuple.Create("A", "B", "C", "D", "E", "F", "G", "H");
			tuples[9] = FdbTuple.Create("A", "B", "C", "D", "E", "F", "G", "H", "I");
			tuples[10]= FdbTuple.Create("A", "B", "C", "D", "E", "F", "G", "H", "I", "J");
			tuples[11] = new FdbJoinedTuple(tuples[6], FdbTuple.Create("G", "H", "I", "J", "K"));
			tuples[12] = new FdbLinkedTuple<string>(tuples[11], "L");
			tuples[13] = new FdbLinkedTuple<string>(FdbTuple.Create("A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L"), "M");

#if false
			Console.Write("Checking tuples");

			foreach (var tuple in tuples)
			{
				var t = FdbTuple.Unpack(tuple.ToSlice());
				Assert.That(t.Equals(tuple), Is.True, t.ToString() + " != unpack(" + tuple.ToString() + ")");
			}
#endif

			var rnd = new Random(123456);

			for (int i = 0; i < N; i++)
			{
				if (i % 500 == 0) Console.Write(".");
				var len = rnd.Next(tuples.Length);
				var tuple = tuples[len];
				Assert.That(tuple.Count, Is.EqualTo(len));

				string prefix = tuple.ToString();

				if (rnd.Next(5) == 0)
				{ // randomly pack/unpack
					tuple = FdbTuple.Unpack(tuple.ToSlice());
					prefix = "unpacked:" + prefix;
				}
				else if (rnd.Next(5) == 0)
				{ // randomly memoize
					tuple = tuple.Memoize();
					prefix = "memoized:" + prefix;
				}

				switch (rnd.Next(6))
				{
					case 0:
					{ // [:+rnd]
						int x = rnd.Next(len);
						VerifyTuple(prefix + "[:" + x.ToString() + "]", tuple[null, x], GetRange(0, x, len));
						break;
					}
					case 1:
					{ // [+rnd:]
						int x = rnd.Next(len);
						VerifyTuple(prefix + "[" + x.ToString() + ":]", tuple[x, null], GetRange(x, int.MaxValue, len));
						break;
					}
					case 2:
					{ // [:-rnd]
						int x = -1 - rnd.Next(len);
						VerifyTuple(prefix + "[:" + x.ToString() + "]", tuple[null, x], GetRange(0, len + x, len));
						break;
					}
					case 3:
					{ // [-rnd:]
						int x = -1 - rnd.Next(len);
						VerifyTuple(prefix + "[" + x.ToString() + ":]", tuple[x, null], GetRange(len + x, int.MaxValue, len));
						break;
					}
					case 4:
					{ // [rnd:rnd]
						int x = rnd.Next(len);
						int y;
						do { y = rnd.Next(len); } while (y < x);
						VerifyTuple(prefix + " [" + x.ToString() + ":" + y.ToString() + "]", tuple[x, y], GetRange(x, y, len));
						break;
					}
					case 5:
					{ // [-rnd:-rnd]
						int x = -1 - rnd.Next(len);
						int y;
						do { y = -1 - rnd.Next(len); } while (y < x);
						VerifyTuple(prefix + " [" + x.ToString() + ":" + y.ToString() + "]", tuple[x, y], GetRange(len + x, len + y, len));
						break;
					}
				}

			}
			Console.WriteLine(" done");

		}

		#endregion

		#region Serialization...

		[Test]
		public void Test_FdbTuple_Serialize_Bytes()
		{
			// Byte arrays are stored with prefix '01' followed by the bytes, and terminated by '00'. All occurences of '00' in the byte array are escaped with '00 FF'
			// - Best case:  packed_size = 2 + array_len
			// - Worst case: packed_size = 2 + array_len * 2

			Slice packed;

			packed = FdbTuple.EncodeKey(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 });
			Assert.That(packed.ToString(), Is.EqualTo("<01><12>4Vx<9A><BC><DE><F0><00>"));
			packed = FdbTuple.EncodeKey(new byte[] { 0x00, 0x42 });
			Assert.That(packed.ToString(), Is.EqualTo("<01><00><FF>B<00>"));
			packed = FdbTuple.EncodeKey(new byte[] { 0x42, 0x00 });
			Assert.That(packed.ToString(), Is.EqualTo("<01>B<00><FF><00>"));
			packed = FdbTuple.EncodeKey(new byte[] { 0x42, 0x00, 0x42 });
			Assert.That(packed.ToString(), Is.EqualTo("<01>B<00><FF>B<00>"));
			packed = FdbTuple.EncodeKey(new byte[] { 0x42, 0x00, 0x00, 0x42 });
			Assert.That(packed.ToString(), Is.EqualTo("<01>B<00><FF><00><FF>B<00>"));
		}

		[Test]
		public void Test_FdbTuple_Deserialize_Bytes()
		{
			IFdbTuple t;

			t = FdbTuple.Unpack(Slice.Unescape("<01><01><23><45><67><89><AB><CD><EF><00>"));
			Assert.That(t.Get<byte[]>(0), Is.EqualTo(new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF }));
			Assert.That(t.Get<Slice>(0).ToHexaString(' '), Is.EqualTo("01 23 45 67 89 AB CD EF"));

			t = FdbTuple.Unpack(Slice.Unescape("<01><42><00><FF><00>"));
			Assert.That(t.Get<byte[]>(0), Is.EqualTo(new byte[] { 0x42, 0x00 }));
			Assert.That(t.Get<Slice>(0).ToHexaString(' '), Is.EqualTo("42 00"));

			t = FdbTuple.Unpack(Slice.Unescape("<01><00><FF><42><00>"));
			Assert.That(t.Get<byte[]>(0), Is.EqualTo(new byte[] { 0x00, 0x42 }));
			Assert.That(t.Get<Slice>(0).ToHexaString(' '), Is.EqualTo("00 42"));

			t = FdbTuple.Unpack(Slice.Unescape("<01><42><00><FF><42><00>"));
			Assert.That(t.Get<byte[]>(0), Is.EqualTo(new byte[] { 0x42, 0x00, 0x42 }));
			Assert.That(t.Get<Slice>(0).ToHexaString(' '), Is.EqualTo("42 00 42"));

			t = FdbTuple.Unpack(Slice.Unescape("<01><42><00><FF><00><FF><42><00>"));
			Assert.That(t.Get<byte[]>(0), Is.EqualTo(new byte[] { 0x42, 0x00, 0x00, 0x42 }));
			Assert.That(t.Get<Slice>(0).ToHexaString(' '), Is.EqualTo("42 00 00 42"));
		}

		[Test]
		public void Test_FdbTuple_Serialize_Unicode_Strings()
		{
			// Unicode strings are stored with prefix '02' followed by the utf8 bytes, and terminated by '00'. All occurences of '00' in the UTF8 bytes are escaped with '00 FF'

			Slice packed;

			// simple string
			packed = FdbTuple.Create("hello world").ToSlice();
			Assert.That(packed.ToString(), Is.EqualTo("<02>hello world<00>"));

			// empty
			packed = FdbTuple.Create(String.Empty).ToSlice();
			Assert.That(packed.ToString(), Is.EqualTo("<02><00>"));

			// null
			packed = FdbTuple.Create(default(string)).ToSlice();
			Assert.That(packed.ToString(), Is.EqualTo("<00>"));

			// unicode
			packed = FdbTuple.Create("こんにちは世界").ToSlice();
			// note: Encoding.UTF8.GetBytes("こんにちは世界") => { e3 81 93 e3 82 93 e3 81 ab e3 81 a1 e3 81 af e4 b8 96 e7 95 8c }
			Assert.That(packed.ToString(), Is.EqualTo("<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>"));
		}

		[Test]
		public void Test_FdbTuple_Deserialize_Unicode_Strings()
		{
			IFdbTuple t;

			// simple string
			t = FdbTuple.Unpack(Slice.Unescape("<02>hello world<00>"));
			Assert.That(t.Get<String>(0), Is.EqualTo("hello world"));
			Assert.That(t[0], Is.EqualTo("hello world"));

			// empty
			t = FdbTuple.Unpack(Slice.Unescape("<02><00>"));
			Assert.That(t.Get<String>(0), Is.EqualTo(String.Empty));
			Assert.That(t[0], Is.EqualTo(String.Empty));

			// null
			t = FdbTuple.Unpack(Slice.Unescape("<00>"));
			Assert.That(t.Get<String>(0), Is.EqualTo(default(string)));
			Assert.That(t[0], Is.Null);

			// unicode
			t = FdbTuple.Unpack(Slice.Unescape("<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>"));
			// note: Encoding.UTF8.GetString({ e3 81 93 e3 82 93 e3 81 ab e3 81 a1 e3 81 af e4 b8 96 e7 95 8c }) => "こんにちは世界"
			Assert.That(t.Get<String>(0), Is.EqualTo("こんにちは世界"));
			Assert.That(t[0], Is.EqualTo("こんにちは世界"));
		}

		[Test]
		public void Test_FdbTuple_Serialize_Guids()
		{
			// 128-bit Guids are stored with prefix '30' followed by 16 bytes formatted according to RFC 4122

			// System.Guid are stored in Little-Endian, but RFC 4122's UUIDs are stored in Big Endian, so per convention we will swap them

			Slice packed;

			// note: new Guid(bytes from 0 to 15) => "03020100-0504-0706-0809-0a0b0c0d0e0f";
			packed = FdbTuple.Create(Guid.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")).ToSlice();
			Assert.That(packed.ToString(), Is.EqualTo("0<00><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F>"));

			packed = FdbTuple.Create(Guid.Empty).ToSlice();
			Assert.That(packed.ToString(), Is.EqualTo("0<00><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00>"));

		}

		[Test]
		public void Test_FdbTuple_Deserialize_Guids()
		{
			// 128-bit Guids are stored with prefix '30' followed by 16 bytes
			// we also accept byte arrays (prefix '01') if they are of length 16

			IFdbTuple packed;

			packed = FdbTuple.Unpack(Slice.Unescape("<30><00><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F>"));
			Assert.That(packed.Get<Guid>(0), Is.EqualTo(Guid.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")));
			Assert.That(packed[0], Is.EqualTo(Guid.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")));

			packed = FdbTuple.Unpack(Slice.Unescape("<30><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00>"));
			Assert.That(packed.Get<Guid>(0), Is.EqualTo(Guid.Empty));
			Assert.That(packed[0], Is.EqualTo(Guid.Empty));

			// unicode string
			packed = FdbTuple.Unpack(Slice.Unescape("<02>03020100-0504-0706-0809-0a0b0c0d0e0f<00>"));
			Assert.That(packed.Get<Guid>(0), Is.EqualTo(Guid.Parse("03020100-0504-0706-0809-0a0b0c0d0e0f")));
			//note: t[0] returns a string, not a GUID

			// null maps to Guid.Empty
			packed = FdbTuple.Unpack(Slice.Unescape("<00>"));
			Assert.That(packed.Get<Guid>(0), Is.EqualTo(Guid.Empty));
			//note: t[0] returns null, not a GUID

		}

		[Test]
		public void Test_FdbTuple_Serialize_Uuid128s()
		{
			// UUID128s are stored with prefix '30' followed by 16 bytes formatted according to RFC 4122

			Slice packed;

			// note: new Uuid(bytes from 0 to 15) => "03020100-0504-0706-0809-0a0b0c0d0e0f";
			packed = FdbTuple.Create(Uuid128.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")).ToSlice();
			Assert.That(packed.ToString(), Is.EqualTo("0<00><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F>"));

			packed = FdbTuple.Create(Uuid128.Empty).ToSlice();
			Assert.That(packed.ToString(), Is.EqualTo("0<00><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00>"));
		}

		[Test]
		public void Test_FdbTuple_Deserialize_Uuid128s()
		{
			// UUID128s are stored with prefix '30' followed by 16 bytes (the result of uuid.ToByteArray())
			// we also accept byte arrays (prefix '01') if they are of length 16

			IFdbTuple packed;

			// note: new Uuid(bytes from 0 to 15) => "00010203-0405-0607-0809-0a0b0c0d0e0f";
			packed = FdbTuple.Unpack(Slice.Unescape("<30><00><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F>"));
			Assert.That(packed.Get<Uuid128>(0), Is.EqualTo(Uuid128.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")));
			Assert.That(packed[0], Is.EqualTo(Uuid128.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")));

			packed = FdbTuple.Unpack(Slice.Unescape("<30><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00>"));
			Assert.That(packed.Get<Uuid128>(0), Is.EqualTo(Uuid128.Empty));
			Assert.That(packed[0], Is.EqualTo(Uuid128.Empty));

			// unicode string
			packed = FdbTuple.Unpack(Slice.Unescape("<02>00010203-0405-0607-0809-0a0b0c0d0e0f<00>"));
			Assert.That(packed.Get<Uuid128>(0), Is.EqualTo(Uuid128.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")));
			//note: t[0] returns a string, not a UUID

			// null maps to Uuid.Empty
			packed = FdbTuple.Unpack(Slice.Unescape("<00>"));
			Assert.That(packed.Get<Uuid128>(0), Is.EqualTo(Uuid128.Empty));
			//note: t[0] returns null, not a UUID

		}

		[Test]
		public void Test_FdbTuple_Serialize_Uuid64s()
		{
			// UUID64s are stored with prefix '31' followed by 8 bytes formatted according to RFC 4122

			Slice packed;

			// note: new Uuid(bytes from 0 to 7) => "00010203-04050607";
			packed = FdbTuple.Create(Uuid64.Parse("00010203-04050607")).ToSlice();
			Assert.That(packed.ToString(), Is.EqualTo("1<00><01><02><03><04><05><06><07>"));

			packed = FdbTuple.Create(Uuid64.Parse("01234567-89ABCDEF")).ToSlice();
			Assert.That(packed.ToString(), Is.EqualTo("1<01>#Eg<89><AB><CD><EF>"));

			packed = FdbTuple.Create(Uuid64.Empty).ToSlice();
			Assert.That(packed.ToString(), Is.EqualTo("1<00><00><00><00><00><00><00><00>"));

			packed = FdbTuple.Create(new Uuid64(0xBADC0FFEE0DDF00DUL)).ToSlice();
			Assert.That(packed.ToString(), Is.EqualTo("1<BA><DC><0F><FE><E0><DD><F0><0D>"));

			packed = FdbTuple.Create(new Uuid64(0xDEADBEEFL)).ToSlice();
			Assert.That(packed.ToString(), Is.EqualTo("1<00><00><00><00><DE><AD><BE><EF>"));
		}

		[Test]
		public void Test_FdbTuple_Deserialize_Uuid64s()
		{
			// UUID64s are stored with prefix '31' followed by 8 bytes (the result of uuid.ToByteArray())
			// we also accept byte arrays (prefix '01') if they are of length 8, and unicode strings (prefix '02')

			IFdbTuple packed;

			// note: new Uuid(bytes from 0 to 15) => "00010203-0405-0607-0809-0a0b0c0d0e0f";
			packed = FdbTuple.Unpack(Slice.Unescape("<31><01><23><45><67><89><AB><CD><EF>"));
			Assert.That(packed.Get<Uuid64>(0), Is.EqualTo(Uuid64.Parse("01234567-89abcdef")));
			Assert.That(packed[0], Is.EqualTo(Uuid64.Parse("01234567-89abcdef")));

			packed = FdbTuple.Unpack(Slice.Unescape("<31><00><00><00><00><00><00><00><00>"));
			Assert.That(packed.Get<Uuid64>(0), Is.EqualTo(Uuid64.Empty));
			Assert.That(packed[0], Is.EqualTo(Uuid64.Empty));

			// 8 bytes
			packed = FdbTuple.Unpack(Slice.Unescape("<01><01><23><45><67><89><ab><cd><ef><00>"));
			Assert.That(packed.Get<Uuid64>(0), Is.EqualTo(Uuid64.Parse("01234567-89abcdef")));
			//note: t[0] returns a string, not a UUID

			// unicode string
			packed = FdbTuple.Unpack(Slice.Unescape("<02>01234567-89abcdef<00>"));
			Assert.That(packed.Get<Uuid64>(0), Is.EqualTo(Uuid64.Parse("01234567-89abcdef")));
			//note: t[0] returns a string, not a UUID

			// null maps to Uuid.Empty
			packed = FdbTuple.Unpack(Slice.Unescape("<00>"));
			Assert.That(packed.Get<Uuid64>(0), Is.EqualTo(Uuid64.Empty));
			//note: t[0] returns null, not a UUID

		}

		[Test]
		public void Test_FdbTuple_Serialize_Integers()
		{
			// Positive integers are stored with a variable-length encoding.
			// - The prefix is 0x14 + the minimum number of bytes to encode the integer, from 0 to 8, so valid prefixes range from 0x14 to 0x1C
			// - The bytes are stored in High-Endian (ie: the upper bits first)
			// Examples:
			// - 0 => <14>
			// - 1..255 => <15><##>
			// - 256..65535 .. => <16><HH><LL>
			// - ulong.MaxValue => <1C><FF><FF><FF><FF><FF><FF><FF><FF>

			Assert.That(
				FdbTuple.Create(0).ToSlice().ToString(),
				Is.EqualTo("<14>")
			);

			Assert.That(
				FdbTuple.Create(1).ToSlice().ToString(),
				Is.EqualTo("<15><01>")
			);

			Assert.That(
				FdbTuple.Create(255).ToSlice().ToString(),
				Is.EqualTo("<15><FF>")
			);

			Assert.That(
				FdbTuple.Create(256).ToSlice().ToString(),
				Is.EqualTo("<16><01><00>")
			);

			Assert.That(
				FdbTuple.Create(65535).ToSlice().ToString(),
				Is.EqualTo("<16><FF><FF>")
			);

			Assert.That(
				FdbTuple.Create(65536).ToSlice().ToString(),
				Is.EqualTo("<17><01><00><00>")
			);

			Assert.That(
				FdbTuple.Create(int.MaxValue).ToSlice().ToString(),
				Is.EqualTo("<18><7F><FF><FF><FF>")
			);

			// signed max
			Assert.That(
				FdbTuple.Create(long.MaxValue).ToSlice().ToString(),
				Is.EqualTo("<1C><7F><FF><FF><FF><FF><FF><FF><FF>")
			);

			// unsigned max
			Assert.That(
				FdbTuple.Create(ulong.MaxValue).ToSlice().ToString(),
				Is.EqualTo("<1C><FF><FF><FF><FF><FF><FF><FF><FF>")
			);
		}

		[Test]
		public void Test_FdbTuple_Deserialize_Integers()
		{

			Action<string, long> verify = (encoded, value) =>
			{
				var slice = Slice.Unescape(encoded);
				Assert.That(FdbTuplePackers.DeserializeBoxed(slice), Is.EqualTo(value), "DeserializeBoxed({0})", encoded);

				// int64
				Assert.That(FdbTuplePackers.DeserializeInt64(slice), Is.EqualTo(value), "DeserializeInt64({0})", encoded);
				Assert.That(FdbTuplePacker<long>.Deserialize(slice), Is.EqualTo(value), "Deserialize<long>({0})", encoded);

				// uint64
				if (value >= 0)
				{
					Assert.That(FdbTuplePackers.DeserializeUInt64(slice), Is.EqualTo((ulong)value), "DeserializeUInt64({0})", encoded);
					Assert.That(FdbTuplePacker<ulong>.Deserialize(slice), Is.EqualTo((ulong)value), "Deserialize<ulong>({0})", encoded);
				}
				else
				{
					Assert.That(() => FdbTuplePackers.DeserializeUInt64(slice), Throws.InstanceOf<OverflowException>(), "DeserializeUInt64({0})", encoded);
				}

				// int32
				if (value <= int.MaxValue && value >= int.MinValue)
				{
					Assert.That(FdbTuplePackers.DeserializeInt32(slice), Is.EqualTo((int)value), "DeserializeInt32({0})", encoded);
					Assert.That(FdbTuplePacker<long>.Deserialize(slice), Is.EqualTo((int)value), "Deserialize<int>({0})", encoded);
				}
				else
				{
					Assert.That(() => FdbTuplePackers.DeserializeInt32(slice), Throws.InstanceOf<OverflowException>(), "DeserializeInt32({0})", encoded);
				}

				// uint32
				if (value <= uint.MaxValue && value >= 0)
				{
					Assert.That(FdbTuplePackers.DeserializeUInt32(slice), Is.EqualTo((uint)value), "DeserializeUInt32({0})", encoded);
					Assert.That(FdbTuplePacker<uint>.Deserialize(slice), Is.EqualTo((uint)value), "Deserialize<uint>({0})", encoded);
				}
				else
				{
					Assert.That(() => FdbTuplePackers.DeserializeUInt32(slice), Throws.InstanceOf<OverflowException>(), "DeserializeUInt32({0})", encoded);
				}

				// int16
				if (value <= short.MaxValue && value >= short.MinValue)
				{
					Assert.That(FdbTuplePackers.DeserializeInt16(slice), Is.EqualTo((short)value), "DeserializeInt16({0})", encoded);
					Assert.That(FdbTuplePacker<short>.Deserialize(slice), Is.EqualTo((short)value), "Deserialize<short>({0})", encoded);
				}
				else
				{
					Assert.That(() => FdbTuplePackers.DeserializeInt16(slice), Throws.InstanceOf<OverflowException>(), "DeserializeInt16({0})", encoded);
				}

				// uint16
				if (value <= ushort.MaxValue && value >= 0)
				{
					Assert.That(FdbTuplePackers.DeserializeUInt16(slice), Is.EqualTo((ushort)value), "DeserializeUInt16({0})", encoded);
					Assert.That(FdbTuplePacker<ushort>.Deserialize(slice), Is.EqualTo((ushort)value), "Deserialize<ushort>({0})", encoded);
				}
				else
				{
					Assert.That(() => FdbTuplePackers.DeserializeUInt16(slice), Throws.InstanceOf<OverflowException>(), "DeserializeUInt16({0})", encoded);
				}

				// sbyte
				if (value <= sbyte.MaxValue && value >= sbyte.MinValue)
				{
					Assert.That(FdbTuplePackers.DeserializeSByte(slice), Is.EqualTo((sbyte)value), "DeserializeSByte({0})", encoded);
					Assert.That(FdbTuplePacker<sbyte>.Deserialize(slice), Is.EqualTo((sbyte)value), "Deserialize<sbyte>({0})", encoded);
				}
				else
				{
					Assert.That(() => FdbTuplePackers.DeserializeSByte(slice), Throws.InstanceOf<OverflowException>(), "DeserializeSByte({0})", encoded);
				}

				// byte
				if (value <= 255 && value >= 0)
				{
					Assert.That(FdbTuplePackers.DeserializeByte(slice), Is.EqualTo((byte)value), "DeserializeByte({0})", encoded);
					Assert.That(FdbTuplePacker<byte>.Deserialize(slice), Is.EqualTo((byte)value), "Deserialize<byte>({0})", encoded);
				}
				else
				{
					Assert.That(() => FdbTuplePackers.DeserializeByte(slice), Throws.InstanceOf<OverflowException>(), "DeserializeByte({0})", encoded);
				}

			};
			verify("<14>", 0);
			verify("<15>{", 123);
			verify("<15><80>", 128);
			verify("<15><FF>", 255);
			verify("<16><01><00>", 256);
			verify("<16><04><D2>", 1234);
			verify("<16><80><00>", 32768);
			verify("<16><FF><FF>", 65535);
			verify("<17><01><00><00>", 65536);
			verify("<13><FE>", -1);
			verify("<13><00>", -255);
			verify("<12><FE><FF>", -256);
			verify("<12><00><00>", -65535);
			verify("<11><FE><FF><FF>", -65536);
			verify("<18><7F><FF><FF><FF>", int.MaxValue);
			verify("<10><7F><FF><FF><FF>", int.MinValue);
			verify("<1C><7F><FF><FF><FF><FF><FF><FF><FF>", long.MaxValue);
			verify("<0C><7F><FF><FF><FF><FF><FF><FF><FF>", long.MinValue);
		}

		[Test]
		public void Test_FdbTuple_Serialize_Negative_Integers()
		{
			// Negative integers are stored with a variable-length encoding.
			// - The prefix is 0x14 - the minimum number of bytes to encode the integer, from 0 to 8, so valid prefixes range from 0x0C to 0x13
			// - The value is encoded as the one's complement, and stored in High-Endian (ie: the upper bits first)
			// - There is no way to encode '-0', it will be encoded as '0' (<14>)
			// Examples:
			// - -255..-1 => <13><00> .. <13><FE>
			// - -65535..-256 => <12><00>00> .. <12><FE><FF>
			// - long.MinValue => <0C><7F><FF><FF><FF><FF><FF><FF><FF>

			Assert.That(
				FdbTuple.Create(-1).ToSlice().ToString(),
				Is.EqualTo("<13><FE>")
			);

			Assert.That(
				FdbTuple.Create(-255).ToSlice().ToString(),
				Is.EqualTo("<13><00>")
			);

			Assert.That(
				FdbTuple.Create(-256).ToSlice().ToString(),
				Is.EqualTo("<12><FE><FF>")
			);
			Assert.That(
				FdbTuple.Create(-257).ToSlice().ToString(),
				Is.EqualTo("<12><FE><FE>")
			);

			Assert.That(
				FdbTuple.Create(-65535).ToSlice().ToString(),
				Is.EqualTo("<12><00><00>")
			);
			Assert.That(
				FdbTuple.Create(-65536).ToSlice().ToString(),
				Is.EqualTo("<11><FE><FF><FF>")
			);

			Assert.That(
				FdbTuple.Create(int.MinValue).ToSlice().ToString(),
				Is.EqualTo("<10><7F><FF><FF><FF>")
			);

			Assert.That(
				FdbTuple.Create(long.MinValue).ToSlice().ToString(),
				Is.EqualTo("<0C><7F><FF><FF><FF><FF><FF><FF><FF>")
			);
		}

		[Test]
		public void Test_FdbTuple_Serialize_Singles()
		{
			// 32-bit floats are stored in 5 bytes, using the prefix 0x20 followed by the High-Endian representation of their normalized form

			Assert.That(FdbTuple.Create(0f).ToSlice().ToHexaString(' '), Is.EqualTo("20 80 00 00 00"));
			Assert.That(FdbTuple.Create(42f).ToSlice().ToHexaString(' '), Is.EqualTo("20 C2 28 00 00"));
			Assert.That(FdbTuple.Create(-42f).ToSlice().ToHexaString(' '), Is.EqualTo("20 3D D7 FF FF"));

			Assert.That(FdbTuple.Create((float)Math.Sqrt(2)).ToSlice().ToHexaString(' '), Is.EqualTo("20 BF B5 04 F3"));

			Assert.That(FdbTuple.Create(float.MinValue).ToSlice().ToHexaString(' '), Is.EqualTo("20 00 80 00 00"), "float.MinValue");
			Assert.That(FdbTuple.Create(float.MaxValue).ToSlice().ToHexaString(' '), Is.EqualTo("20 FF 7F FF FF"), "float.MaxValue");
			Assert.That(FdbTuple.Create(-0f).ToSlice().ToHexaString(' '), Is.EqualTo("20 7F FF FF FF"), "-0f");
			Assert.That(FdbTuple.Create(float.NegativeInfinity).ToSlice().ToHexaString(' '), Is.EqualTo("20 00 7F FF FF"), "float.NegativeInfinity");
			Assert.That(FdbTuple.Create(float.PositiveInfinity).ToSlice().ToHexaString(' '), Is.EqualTo("20 FF 80 00 00"), "float.PositiveInfinity");
			Assert.That(FdbTuple.Create(float.Epsilon).ToSlice().ToHexaString(' '), Is.EqualTo("20 80 00 00 01"), "+float.Epsilon");
			Assert.That(FdbTuple.Create(-float.Epsilon).ToSlice().ToHexaString(' '), Is.EqualTo("20 7F FF FF FE"), "-float.Epsilon");

			// all possible variants of NaN should all be equal
			Assert.That(FdbTuple.Create(float.NaN).ToSlice().ToHexaString(' '), Is.EqualTo("20 00 3F FF FF"), "float.NaN");

			// cook up a non standard NaN (with some bits set in the fraction)
			float f = float.NaN; // defined as 1f / 0f
			uint nan;
			unsafe { nan = *((uint*)&f); }
			nan += 123;
			unsafe { f = *((float*)&nan); }
			Assert.That(float.IsNaN(f), Is.True);
			Assert.That(
				FdbTuple.Create(f).ToSlice().ToHexaString(' '),
				Is.EqualTo("20 00 3F FF FF"),
				"All variants of NaN must be normalized"
				//note: if we have 20 00 3F FF 84, that means that the NaN was not normalized
			);

		}

		[Test]
		public void Test_FdbTuple_Deserialize_Singles()
		{
			Assert.That(FdbTuple.DecodeKey<float>(Slice.FromHexa("20 80 00 00 00")), Is.EqualTo(0f), "0f");
			Assert.That(FdbTuple.DecodeKey<float>(Slice.FromHexa("20 C2 28 00 00")), Is.EqualTo(42f), "42f");
			Assert.That(FdbTuple.DecodeKey<float>(Slice.FromHexa("20 3D D7 FF FF")), Is.EqualTo(-42f), "-42f");

			Assert.That(FdbTuple.DecodeKey<float>(Slice.FromHexa("20 BF B5 04 F3")), Is.EqualTo((float)Math.Sqrt(2)), "Sqrt(2)");

			// well known values
			Assert.That(FdbTuple.DecodeKey<float>(Slice.FromHexa("20 00 80 00 00")), Is.EqualTo(float.MinValue), "float.MinValue");
			Assert.That(FdbTuple.DecodeKey<float>(Slice.FromHexa("20 FF 7F FF FF")), Is.EqualTo(float.MaxValue), "float.MaxValue");
			Assert.That(FdbTuple.DecodeKey<float>(Slice.FromHexa("20 7F FF FF FF")), Is.EqualTo(-0f), "-0f");
			Assert.That(FdbTuple.DecodeKey<float>(Slice.FromHexa("20 00 7F FF FF")), Is.EqualTo(float.NegativeInfinity), "float.NegativeInfinity");
			Assert.That(FdbTuple.DecodeKey<float>(Slice.FromHexa("20 FF 80 00 00")), Is.EqualTo(float.PositiveInfinity), "float.PositiveInfinity");
			Assert.That(FdbTuple.DecodeKey<float>(Slice.FromHexa("20 00 80 00 00")), Is.EqualTo(float.MinValue), "float.Epsilon");
			Assert.That(FdbTuple.DecodeKey<float>(Slice.FromHexa("20 80 00 00 01")), Is.EqualTo(float.Epsilon), "+float.Epsilon");
			Assert.That(FdbTuple.DecodeKey<float>(Slice.FromHexa("20 7F FF FF FE")), Is.EqualTo(-float.Epsilon), "-float.Epsilon");

			// all possible variants of NaN should end up equal and normalized to float.NaN
			Assert.That(FdbTuple.DecodeKey<float>(Slice.FromHexa("20 00 3F FF FF")), Is.EqualTo(float.NaN), "float.NaN");
			Assert.That(FdbTuple.DecodeKey<float>(Slice.FromHexa("20 00 3F FF FF")), Is.EqualTo(float.NaN), "float.NaN");
		}

		[Test]
		public void Test_FdbTuple_Serialize_Doubles()
		{
			// 64-bit floats are stored in 9 bytes, using the prefix 0x21 followed by the High-Endian representation of their normalized form

			Assert.That(FdbTuple.Create(0d).ToSlice().ToHexaString(' '), Is.EqualTo("21 80 00 00 00 00 00 00 00"));
			Assert.That(FdbTuple.Create(42d).ToSlice().ToHexaString(' '), Is.EqualTo("21 C0 45 00 00 00 00 00 00"));
			Assert.That(FdbTuple.Create(-42d).ToSlice().ToHexaString(' '), Is.EqualTo("21 3F BA FF FF FF FF FF FF"));

			Assert.That(FdbTuple.Create(Math.PI).ToSlice().ToHexaString(' '), Is.EqualTo("21 C0 09 21 FB 54 44 2D 18"));
			Assert.That(FdbTuple.Create(Math.E).ToSlice().ToHexaString(' '), Is.EqualTo("21 C0 05 BF 0A 8B 14 57 69"));

			Assert.That(FdbTuple.Create(double.MinValue).ToSlice().ToHexaString(' '), Is.EqualTo("21 00 10 00 00 00 00 00 00"), "double.MinValue");
			Assert.That(FdbTuple.Create(double.MaxValue).ToSlice().ToHexaString(' '), Is.EqualTo("21 FF EF FF FF FF FF FF FF"), "double.MaxValue");
			Assert.That(FdbTuple.Create(-0d).ToSlice().ToHexaString(' '), Is.EqualTo("21 7F FF FF FF FF FF FF FF"), "-0d");
			Assert.That(FdbTuple.Create(double.NegativeInfinity).ToSlice().ToHexaString(' '), Is.EqualTo("21 00 0F FF FF FF FF FF FF"), "double.NegativeInfinity");
			Assert.That(FdbTuple.Create(double.PositiveInfinity).ToSlice().ToHexaString(' '), Is.EqualTo("21 FF F0 00 00 00 00 00 00"), "double.PositiveInfinity");
			Assert.That(FdbTuple.Create(double.Epsilon).ToSlice().ToHexaString(' '), Is.EqualTo("21 80 00 00 00 00 00 00 01"), "+double.Epsilon");
			Assert.That(FdbTuple.Create(-double.Epsilon).ToSlice().ToHexaString(' '), Is.EqualTo("21 7F FF FF FF FF FF FF FE"), "-double.Epsilon");

			// all possible variants of NaN should all be equal

			Assert.That(FdbTuple.Create(double.NaN).ToSlice().ToHexaString(' '), Is.EqualTo("21 00 07 FF FF FF FF FF FF"), "double.NaN");

			// cook up a non standard NaN (with some bits set in the fraction)
			double d = double.NaN; // defined as 1d / 0d
			ulong nan;
			unsafe { nan = *((ulong*)&d); }
			nan += 123;
			unsafe { d = *((double*)&nan); }
			Assert.That(double.IsNaN(d), Is.True);
			Assert.That(
				FdbTuple.Create(d).ToSlice().ToHexaString(' '),
				Is.EqualTo("21 00 07 FF FF FF FF FF FF")
				//note: if we have 21 00 07 FF FF FF FF FF 84, that means that the NaN was not normalized
			);

			// roundtripping vectors of doubles
			var tuple = FdbTuple.Create(Math.PI, Math.E, Math.Log(1), Math.Log(2));
			Assert.That(FdbTuple.Unpack(FdbTuple.EncodeKey(Math.PI, Math.E, Math.Log(1), Math.Log(2))), Is.EqualTo(tuple));
			Assert.That(FdbTuple.Unpack(FdbTuple.Create(Math.PI, Math.E, Math.Log(1), Math.Log(2)).ToSlice()), Is.EqualTo(tuple));
			Assert.That(FdbTuple.Unpack(FdbTuple.Empty.Append(Math.PI).Append(Math.E).Append(Math.Log(1)).Append(Math.Log(2)).ToSlice()), Is.EqualTo(tuple));
		}

		[Test]
		public void Test_FdbTuple_Deserialize_Doubles()
		{
			Assert.That(FdbTuple.DecodeKey<double>(Slice.FromHexa("21 80 00 00 00 00 00 00 00")), Is.EqualTo(0d), "0d");
			Assert.That(FdbTuple.DecodeKey<double>(Slice.FromHexa("21 C0 45 00 00 00 00 00 00")), Is.EqualTo(42d), "42d");
			Assert.That(FdbTuple.DecodeKey<double>(Slice.FromHexa("21 3F BA FF FF FF FF FF FF")), Is.EqualTo(-42d), "-42d");

			Assert.That(FdbTuple.DecodeKey<double>(Slice.FromHexa("21 C0 09 21 FB 54 44 2D 18")), Is.EqualTo(Math.PI), "Math.PI");
			Assert.That(FdbTuple.DecodeKey<double>(Slice.FromHexa("21 C0 05 BF 0A 8B 14 57 69")), Is.EqualTo(Math.E), "Math.E");

			Assert.That(FdbTuple.DecodeKey<double>(Slice.FromHexa("21 00 10 00 00 00 00 00 00")), Is.EqualTo(double.MinValue), "double.MinValue");
			Assert.That(FdbTuple.DecodeKey<double>(Slice.FromHexa("21 FF EF FF FF FF FF FF FF")), Is.EqualTo(double.MaxValue), "double.MaxValue");
			Assert.That(FdbTuple.DecodeKey<double>(Slice.FromHexa("21 7F FF FF FF FF FF FF FF")), Is.EqualTo(-0d), "-0d");
			Assert.That(FdbTuple.DecodeKey<double>(Slice.FromHexa("21 00 0F FF FF FF FF FF FF")), Is.EqualTo(double.NegativeInfinity), "double.NegativeInfinity");
			Assert.That(FdbTuple.DecodeKey<double>(Slice.FromHexa("21 FF F0 00 00 00 00 00 00")), Is.EqualTo(double.PositiveInfinity), "double.PositiveInfinity");
			Assert.That(FdbTuple.DecodeKey<double>(Slice.FromHexa("21 80 00 00 00 00 00 00 01")), Is.EqualTo(double.Epsilon), "+double.Epsilon");
			Assert.That(FdbTuple.DecodeKey<double>(Slice.FromHexa("21 7F FF FF FF FF FF FF FE")), Is.EqualTo(-double.Epsilon), "-double.Epsilon");

			// all possible variants of NaN should end up equal and normalized to double.NaN
			Assert.That(FdbTuple.DecodeKey<double>(Slice.FromHexa("21 00 07 FF FF FF FF FF FF")), Is.EqualTo(double.NaN), "double.NaN");
			Assert.That(FdbTuple.DecodeKey<double>(Slice.FromHexa("21 00 07 FF FF FF FF FF 84")), Is.EqualTo(double.NaN), "double.NaN");
		}

		[Test]
		public void Test_FdbTuple_Serialize_Booleans()
		{
			// Booleans are stored as interger 0 (<14>) for false, and integer 1 (<15><01>) for true

			Slice packed;

			// bool
			packed = FdbTuple.EncodeKey(false);
			Assert.That(packed.ToString(), Is.EqualTo("<14>"));
			packed = FdbTuple.EncodeKey(true);
			Assert.That(packed.ToString(), Is.EqualTo("<15><01>"));

			// bool?
			packed = FdbTuple.EncodeKey(default(bool?));
			Assert.That(packed.ToString(), Is.EqualTo("<00>"));
			packed = FdbTuple.EncodeKey((bool?)false);
			Assert.That(packed.ToString(), Is.EqualTo("<14>"));
			packed = FdbTuple.EncodeKey((bool?)true);
			Assert.That(packed.ToString(), Is.EqualTo("<15><01>"));

			// tuple containing bools
			packed = FdbTuple.Create<bool>(true).ToSlice();
			Assert.That(packed.ToString(), Is.EqualTo("<15><01>"));
			packed = FdbTuple.Create<bool, bool?, bool?>(true, null, false).ToSlice();
			Assert.That(packed.ToString(), Is.EqualTo("<15><01><00><14>"));
		}

		[Test]
		public void Test_FdbTuple_Deserialize_Booleans()
		{
			// Null, 0, and empty byte[]/strings are equivalent to False. All others are equivalent to True

			// Falsy...
			Assert.That(FdbTuple.DecodeKey<bool>(Slice.Unescape("<00>")), Is.EqualTo(false), "Null => False");
			Assert.That(FdbTuple.DecodeKey<bool>(Slice.Unescape("<14>")), Is.EqualTo(false), "0 => False");
			Assert.That(FdbTuple.DecodeKey<bool>(Slice.Unescape("<01><00>")), Is.EqualTo(false), "byte[0] => False");
			Assert.That(FdbTuple.DecodeKey<bool>(Slice.Unescape("<02><00>")), Is.EqualTo(false), "String.Empty => False");

			// Truthy
			Assert.That(FdbTuple.DecodeKey<bool>(Slice.Unescape("<15><01>")), Is.EqualTo(true), "1 => True");
			Assert.That(FdbTuple.DecodeKey<bool>(Slice.Unescape("<13><FE>")), Is.EqualTo(true), "-1 => True");
			Assert.That(FdbTuple.DecodeKey<bool>(Slice.Unescape("<01>Hello<00>")), Is.EqualTo(true), "'Hello' => True");
			Assert.That(FdbTuple.DecodeKey<bool>(Slice.Unescape("<02>Hello<00>")), Is.EqualTo(true), "\"Hello\" => True");
			Assert.That(FdbTuple.DecodeKey<bool>(FdbTuple.EncodeKey(123456789)), Is.EqualTo(true), "random int => True");

			Assert.That(FdbTuple.DecodeKey<bool>(Slice.Unescape("<02>True<00>")), Is.EqualTo(true), "\"True\" => True");
			Assert.That(FdbTuple.DecodeKey<bool>(Slice.Unescape("<02>False<00>")), Is.EqualTo(true), "\"False\" => True ***");
			// note: even though it would be tempting to convert the string "false" to False, it is not a standard behavior accross all bindings

			// When decoded to object, though, they should return 0 and 1
			Assert.That(FdbTuplePackers.DeserializeBoxed(FdbTuple.EncodeKey(false)), Is.EqualTo(0));
			Assert.That(FdbTuplePackers.DeserializeBoxed(FdbTuple.EncodeKey(true)), Is.EqualTo(1));
		}

		[Test]
		public void Test_FdbTuple_Serialize_IPAddress()
		{
			// IP Addresses are stored as a byte array (<01>..<00>), in network order (big-endian)
			// They will take from 6 to 10 bytes, depending on the number of '.0' in them.

			Assert.That(
				FdbTuple.Create(IPAddress.Loopback).ToSlice().ToHexaString(' '),
				Is.EqualTo("01 7F 00 FF 00 FF 01 00")
			);

			Assert.That(
				FdbTuple.Create(IPAddress.Any).ToSlice().ToHexaString(' '),
				Is.EqualTo("01 00 FF 00 FF 00 FF 00 FF 00")
			);

			Assert.That(
				FdbTuple.Create(IPAddress.Parse("1.2.3.4")).ToSlice().ToHexaString(' '),
				Is.EqualTo("01 01 02 03 04 00")
			);

		}


		[Test]
		public void Test_FdbTuple_Deserialize_IPAddress()
		{
			Assert.That(FdbTuple.DecodeKey<IPAddress>(Slice.Unescape("<01><7F><00><FF><00><FF><01><00>")), Is.EqualTo(IPAddress.Parse("127.0.0.1")));
			Assert.That(FdbTuple.DecodeKey<IPAddress>(Slice.Unescape("<01><00><FF><00><FF><00><FF><00><FF><00>")), Is.EqualTo(IPAddress.Parse("0.0.0.0")));
			Assert.That(FdbTuple.DecodeKey<IPAddress>(Slice.Unescape("<01><01><02><03><04><00>")), Is.EqualTo(IPAddress.Parse("1.2.3.4")));

			Assert.That(FdbTuple.DecodeKey<IPAddress>(FdbTuple.EncodeKey("127.0.0.1")), Is.EqualTo(IPAddress.Loopback));

			var ip = IPAddress.Parse("192.168.0.1");
			Assert.That(FdbTuple.DecodeKey<IPAddress>(FdbTuple.EncodeKey(ip.ToString())), Is.EqualTo(ip));
			Assert.That(FdbTuple.DecodeKey<IPAddress>(FdbTuple.EncodeKey(ip.GetAddressBytes())), Is.EqualTo(ip));
			Assert.That(FdbTuple.DecodeKey<IPAddress>(FdbTuple.EncodeKey(ip.Address)), Is.EqualTo(ip));
		}

		[Test]
		public void Test_FdbTuple_NullableTypes()
		{
			// Nullable types will either be encoded as <14> for null, or their regular encoding if not null

			// serialize

			Assert.That(FdbTuple.EncodeKey<int?>(0), Is.EqualTo(Slice.Unescape("<14>")));
			Assert.That(FdbTuple.EncodeKey<int?>(123), Is.EqualTo(Slice.Unescape("<15>{")));
			Assert.That(FdbTuple.EncodeKey<int?>(null), Is.EqualTo(Slice.Unescape("<00>")));

			Assert.That(FdbTuple.EncodeKey<long?>(0L), Is.EqualTo(Slice.Unescape("<14>")));
			Assert.That(FdbTuple.EncodeKey<long?>(123L), Is.EqualTo(Slice.Unescape("<15>{")));
			Assert.That(FdbTuple.EncodeKey<long?>(null), Is.EqualTo(Slice.Unescape("<00>")));

			Assert.That(FdbTuple.EncodeKey<bool?>(true), Is.EqualTo(Slice.Unescape("<15><01>")));
			Assert.That(FdbTuple.EncodeKey<bool?>(false), Is.EqualTo(Slice.Unescape("<14>")));
			Assert.That(FdbTuple.EncodeKey<bool?>(null), Is.EqualTo(Slice.Unescape("<00>")), "Maybe it was File Not Found?");

			Assert.That(FdbTuple.EncodeKey<Guid?>(Guid.Empty), Is.EqualTo(Slice.Unescape("0<00><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00>")));
			Assert.That(FdbTuple.EncodeKey<Guid?>(null), Is.EqualTo(Slice.Unescape("<00>")));

			Assert.That(FdbTuple.EncodeKey<TimeSpan?>(TimeSpan.Zero), Is.EqualTo(Slice.Unescape("!<80><00><00><00><00><00><00><00>")));
			Assert.That(FdbTuple.EncodeKey<TimeSpan?>(null), Is.EqualTo(Slice.Unescape("<00>")));

			// deserialize

			Assert.That(FdbTuple.DecodeKey<int?>(Slice.Unescape("<14>")), Is.EqualTo(0));
			Assert.That(FdbTuple.DecodeKey<int?>(Slice.Unescape("<15>{")), Is.EqualTo(123));
			Assert.That(FdbTuple.DecodeKey<int?>(Slice.Unescape("<00>")), Is.Null);

			Assert.That(FdbTuple.DecodeKey<int?>(Slice.Unescape("<14>")), Is.EqualTo(0L));
			Assert.That(FdbTuple.DecodeKey<long?>(Slice.Unescape("<15>{")), Is.EqualTo(123L));
			Assert.That(FdbTuple.DecodeKey<long?>(Slice.Unescape("<00>")), Is.Null);

			Assert.That(FdbTuple.DecodeKey<bool?>(Slice.Unescape("<15><01>")), Is.True);
			Assert.That(FdbTuple.DecodeKey<bool?>(Slice.Unescape("<14>")), Is.False);
			Assert.That(FdbTuple.DecodeKey<bool?>(Slice.Unescape("<00>")), Is.Null);

			Assert.That(FdbTuple.DecodeKey<Guid?>(Slice.Unescape("0<00><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00>")), Is.EqualTo(Guid.Empty));
			Assert.That(FdbTuple.DecodeKey<Guid?>(Slice.Unescape("<00>")), Is.Null);

			Assert.That(FdbTuple.DecodeKey<TimeSpan?>(Slice.Unescape("<14>")), Is.EqualTo(TimeSpan.Zero));
			Assert.That(FdbTuple.DecodeKey<TimeSpan?>(Slice.Unescape("<00>")), Is.Null);

		}

		[Test]
		public void Test_FdbTuple_Serialize_Alias()
		{
			Assert.That(
				FdbTuple.EncodeKey(FdbTupleAlias.System).ToString(),
				Is.EqualTo("<FF>")
			);

			Assert.That(
				FdbTuple.EncodeKey(FdbTupleAlias.Directory).ToString(),
				Is.EqualTo("<FE>")
			);

			Assert.That(
				FdbTuple.EncodeKey(FdbTupleAlias.Zero).ToString(),
				Is.EqualTo("<00>")
			);

		}

		[Test]
		public void Test_FdbTuple_Deserialize_Alias()
		{
			Slice slice;

			slice = Slice.Unescape("<FF>");
			Assert.That(FdbTuplePackers.DeserializeBoxed(slice), Is.EqualTo(FdbTupleAlias.System));

			slice = Slice.Unescape("<FE>");
			Assert.That(FdbTuplePackers.DeserializeBoxed(slice), Is.EqualTo(FdbTupleAlias.Directory));

			//note: FdbTupleAlias.Start is <00> and will be deserialized as null
		}

		[Test]
		public void Test_FdbTuple_Serialize_Embedded_Tuples()
		{
			Action<IFdbTuple, string> verify = (t, expected) =>
			{
				var key = t.ToSlice();
				Assert.That(key.ToHexaString(' '), Is.EqualTo(expected));
				Assert.That(FdbTuple.Pack(t), Is.EqualTo(key));
				var t2 = FdbTuple.Unpack(key);
				Assert.That(t2, Is.Not.Null);
				Assert.That(t2.Count, Is.EqualTo(t.Count), "{0}", t2);
				Assert.That(t2, Is.EqualTo(t));
			};

			// Index composite key
			IFdbTuple value = FdbTuple.Create(2014, 11, 6); // Indexing a date value (Y, M, D)
			string docId = "Doc123";
			// key would be "(..., value, id)"

			verify(
				FdbTuple.Create(42, value, docId),
				"15 2A 03 16 07 DE 15 0B 15 06 00 02 44 6F 63 31 32 33 00"
			);
			verify(
				FdbTuple.Create(new object[] { 42, value, docId }),
				"15 2A 03 16 07 DE 15 0B 15 06 00 02 44 6F 63 31 32 33 00"
			);
			verify(
				FdbTuple.Create(42).Append(value).Append(docId),
				"15 2A 03 16 07 DE 15 0B 15 06 00 02 44 6F 63 31 32 33 00"
			);
			verify(
				FdbTuple.Create(42).Append(value, docId),
				"15 2A 03 16 07 DE 15 0B 15 06 00 02 44 6F 63 31 32 33 00"
			);

			// multiple depth
			verify(
				FdbTuple.Create(1, FdbTuple.Create(2, 3), FdbTuple.Create(FdbTuple.Create(4, 5, 6)), 7),
				"15 01 03 15 02 15 03 00 03 03 15 04 15 05 15 06 00 00 15 07"
			);

			// corner cases
			verify(
				FdbTuple.Create(FdbTuple.Empty),
				"03 00" // empty tumple should have header and footer
			);
			verify(
				FdbTuple.Create(FdbTuple.Empty, default(string)),
				"03 00 00" // outer null should not be escaped
			);
			verify(
				FdbTuple.Create(FdbTuple.Create(default(string)), default(string)),
				"03 00 FF 00 00" // inner null should be escaped, but not outer
			);
			verify(
				FdbTuple.Create(FdbTuple.Create(0x100, 0x10000, 0x1000000)),
				"03 16 01 00 17 01 00 00 18 01 00 00 00 00"
			);
			verify(
				FdbTuple.Create(default(string), FdbTuple.Empty, default(string), FdbTuple.Create(default(string)), default(string)),
				"00 03 00 00 03 00 FF 00 00"
			);

		}

		[Test]
		public void Test_FdbTuple_SameBytes()
		{
			IFdbTuple t1 = FdbTuple.Create("hello world");
			IFdbTuple t2 = FdbTuple.Create(new object[] { "hello world" });

			Assert.That(t1.ToSlice(), Is.EqualTo(t2.ToSlice()));

			t1 = FdbTuple.Create("hello world", 1234);
			t2 = FdbTuple.Create("hello world").Append(1234);

			Assert.That(t1.ToSlice(), Is.EqualTo(t2.ToSlice()));
			
		}

		[Test]
		public void Test_FdbTuple_Create_ToSlice()
		{
			Assert.That(
				FdbTuple.Create("hello world").ToSlice().ToString(),
				Is.EqualTo("<02>hello world<00>")
			);

			Assert.That(
				FdbTuple.Create("hello", "world").ToSlice().ToString(),
				Is.EqualTo("<02>hello<00><02>world<00>")
			);

			Assert.That(
				FdbTuple.Create("hello world", 123).ToSlice().ToString(),
				Is.EqualTo("<02>hello world<00><15>{")
			);

			Assert.That(
				FdbTuple.Create("hello world", 1234, -1234).ToSlice().ToString(),
				Is.EqualTo("<02>hello world<00><16><04><D2><12><FB>-")
			);

			Assert.That(
				FdbTuple.Create("hello world", 123, false).ToSlice().ToString(),
				Is.EqualTo("<02>hello world<00><15>{<14>")
			);

			Assert.That(
				FdbTuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }).ToSlice().ToString(),
				Is.EqualTo("<02>hello world<00><15>{<14><01>{<01>B<00><FF>*<00>")
			);

			Assert.That(
				FdbTuple.Create(new object[] { "hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 } }).ToSlice().ToString(),
				Is.EqualTo("<02>hello world<00><15>{<14><01>{<01>B<00><FF>*<00>")
			);

			Assert.That(
				FdbTuple.FromArray(new object[] { "hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 } }, 1, 2).ToSlice().ToString(),
				Is.EqualTo("<15>{<14>")
			);

			Assert.That(
				FdbTuple.FromEnumerable(new List<object> { "hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 } }).ToSlice().ToString(),
				Is.EqualTo("<02>hello world<00><15>{<14><01>{<01>B<00><FF>*<00>")
			);

		}

		[Test]
		public void Test_FdbTuple_EncodeKey()
		{
			Assert.That(
				FdbTuple.EncodeKey("hello world").ToString(),
				Is.EqualTo("<02>hello world<00>")
			);

			Assert.That(
				FdbTuple.EncodeKey("hello", "world").ToString(),
				Is.EqualTo("<02>hello<00><02>world<00>")
			);

			Assert.That(
				FdbTuple.EncodeKey("hello world", 123).ToString(),
				Is.EqualTo("<02>hello world<00><15>{")
			);

			Assert.That(
				FdbTuple.EncodeKey("hello world", 1234, -1234).ToString(),
				Is.EqualTo("<02>hello world<00><16><04><D2><12><FB>-")
			);

			Assert.That(
				FdbTuple.EncodeKey("hello world", 123, false).ToString(),
				Is.EqualTo("<02>hello world<00><15>{<14>")
			);

			Assert.That(
				FdbTuple.EncodeKey("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }).ToString(),
				Is.EqualTo("<02>hello world<00><15>{<14><01>{<01>B<00><FF>*<00>")
			);
		}

		[Test]
		public void Test_FdbTuple_Unpack()
		{

			var packed = FdbTuple.Create("hello world").ToSlice();
			Log(packed);

			var tuple = FdbTuple.Unpack(packed);
			Assert.That(tuple, Is.Not.Null);
			Log(tuple);
			Assert.That(tuple.Count, Is.EqualTo(1));
			Assert.That(tuple.Get<string>(0), Is.EqualTo("hello world"));

			packed = FdbTuple.Create("hello world", 123).ToSlice();
			Log(packed);

			tuple = FdbTuple.Unpack(packed);
			Assert.That(tuple, Is.Not.Null);
			Log(tuple);
			Assert.That(tuple.Count, Is.EqualTo(2));
			Assert.That(tuple.Get<string>(0), Is.EqualTo("hello world"));
			Assert.That(tuple.Get<int>(1), Is.EqualTo(123));

			packed = FdbTuple.Create(1, 256, 257, 65536, int.MaxValue, long.MaxValue).ToSlice();
			Log(packed);

			tuple = FdbTuple.Unpack(packed);
			Assert.That(tuple, Is.Not.Null);
			Assert.That(tuple.Count, Is.EqualTo(6));
			Assert.That(tuple.Get<int>(0), Is.EqualTo(1));
			Assert.That(tuple.Get<int>(1), Is.EqualTo(256));
			Assert.That(tuple.Get<int>(2), Is.EqualTo(257), ((FdbSlicedTuple)tuple).GetSlice(2).ToString());
			Assert.That(tuple.Get<int>(3), Is.EqualTo(65536));
			Assert.That(tuple.Get<int>(4), Is.EqualTo(int.MaxValue));
			Assert.That(tuple.Get<long>(5), Is.EqualTo(long.MaxValue));

			packed = FdbTuple.Create(-1, -256, -257, -65536, int.MinValue, long.MinValue).ToSlice();
			Log(packed);

			tuple = FdbTuple.Unpack(packed);
			Assert.That(tuple, Is.Not.Null);
			Assert.That(tuple, Is.InstanceOf<FdbSlicedTuple>());
			Log(tuple);
			Assert.That(tuple.Count, Is.EqualTo(6));
			Assert.That(tuple.Get<int>(0), Is.EqualTo(-1));
			Assert.That(tuple.Get<int>(1), Is.EqualTo(-256));
			Assert.That(tuple.Get<int>(2), Is.EqualTo(-257), "Slice is " + ((FdbSlicedTuple)tuple).GetSlice(2).ToString());
			Assert.That(tuple.Get<int>(3), Is.EqualTo(-65536));
			Assert.That(tuple.Get<int>(4), Is.EqualTo(int.MinValue));
			Assert.That(tuple.Get<long>(5), Is.EqualTo(long.MinValue));
		}

		[Test]
		public void Test_FdbTuple_CreateBoxed()
		{
			IFdbTuple tuple;

			tuple = FdbTuple.CreateBoxed(default(object));
			Assert.That(tuple.Count, Is.EqualTo(1));
			Assert.That(tuple[0], Is.Null);

			tuple = FdbTuple.CreateBoxed(1);
			Assert.That(tuple.Count, Is.EqualTo(1));
			Assert.That(tuple[0], Is.EqualTo(1));

			tuple = FdbTuple.CreateBoxed(1L);
			Assert.That(tuple.Count, Is.EqualTo(1));
			Assert.That(tuple[0], Is.EqualTo(1L));

			tuple = FdbTuple.CreateBoxed(false);
			Assert.That(tuple.Count, Is.EqualTo(1));
			Assert.That(tuple[0], Is.EqualTo(false));

			tuple = FdbTuple.CreateBoxed("hello");
			Assert.That(tuple.Count, Is.EqualTo(1));
			Assert.That(tuple[0], Is.EqualTo("hello"));

			tuple = FdbTuple.CreateBoxed(new byte[] { 1, 2, 3 });
			Assert.That(tuple.Count, Is.EqualTo(1));
			Assert.That(tuple[0], Is.EqualTo(Slice.Create(new byte[] { 1, 2, 3 })));
		}

		[Test]
		public void Test_FdbTuple_EncodeKey_Boxed()
		{
			Slice slice;

			slice = FdbTuple.EncodeKey<object>(default(object));
			Assert.That(slice.ToString(), Is.EqualTo("<00>"));

			slice = FdbTuple.EncodeKey<object>(1);
			Assert.That(slice.ToString(), Is.EqualTo("<15><01>"));

			slice = FdbTuple.EncodeKey<object>(1L);
			Assert.That(slice.ToString(), Is.EqualTo("<15><01>"));

			slice = FdbTuple.EncodeKey<object>(1U);
			Assert.That(slice.ToString(), Is.EqualTo("<15><01>"));

			slice = FdbTuple.EncodeKey<object>(1UL);
			Assert.That(slice.ToString(), Is.EqualTo("<15><01>"));

			slice = FdbTuple.EncodeKey<object>(false);
			Assert.That(slice.ToString(), Is.EqualTo("<14>"));

			slice = FdbTuple.EncodeKey<object>(new byte[] { 4, 5, 6 });
			Assert.That(slice.ToString(), Is.EqualTo("<01><04><05><06><00>"));

			slice = FdbTuple.EncodeKey<object>("hello");
			Assert.That(slice.ToString(), Is.EqualTo("<02>hello<00>"));
		}

		[Test]
		public void Test_FdbTuple_Numbers_Are_Sorted_Lexicographically()
		{
			// pick two numbers 'x' and 'y' at random, and check that the order of 'x' compared to 'y' is the same as 'pack(tuple(x))' compared to 'pack(tuple(y))'

			// ie: ensure that x.CompareTo(y) always has the same sign as Tuple(x).CompareTo(Tuple(y))

			const int N = 1 * 1000 * 1000;
			var rnd = new Random();
			var sw = Stopwatch.StartNew();

			for (int i = 0; i < N; i++)
			{
				int x = rnd.Next() - 1073741824;
				int y = x;
				while (y == x)
				{
					y = rnd.Next() - 1073741824;
				}

				var t1 = FdbTuple.Create(x).ToSlice();
				var t2 = FdbTuple.Create(y).ToSlice();

				int dint = x.CompareTo(y);
				int dtup = t1.CompareTo(t2);

				if (dtup == 0) Assert.Fail("Tuples for x={0} and y={1} should not have the same packed value", x, y);

				// compare signs
				if (Math.Sign(dint) != Math.Sign(dtup))
				{
					Assert.Fail("Tuples for x={0} and y={1} are not sorted properly ({2} / {3}): t(x)='{4}' and t(y)='{5}'", x, y, dint, dtup, t1.ToString(), t2.ToString());
				}
			}
			sw.Stop();
			Log("Checked {0:N0} tuples in {1:N1} ms", N, sw.ElapsedMilliseconds);

		}

		[Test]
		public void Test_FdbTuple_Serialize_ITupleFormattable()
		{
			// types that implement ITupleFormattable should be packed by calling ToTuple() and then packing the returned tuple

			Slice packed;

			packed = FdbTuplePacker<Thing>.Serialize(new Thing { Foo = 123, Bar = "hello" });
			Assert.That(packed.ToString(), Is.EqualTo("<03><15>{<02>hello<00><00>"));

			packed = FdbTuplePacker<Thing>.Serialize(new Thing());
			Assert.That(packed.ToString(), Is.EqualTo("<03><14><00><FF><00>"));

			packed = FdbTuplePacker<Thing>.Serialize(default(Thing));
			Assert.That(packed.ToString(), Is.EqualTo("<00>"));

		}

		[Test]
		public void Test_FdbTuple_Deserialize_ITupleFormattable()
		{
			Slice slice;
			Thing thing;

			slice = Slice.Unescape("<03><16><01><C8><02>world<00><00>");
			thing = FdbTuplePackers.DeserializeFormattable<Thing>(slice);
			Assert.That(thing, Is.Not.Null);
			Assert.That(thing.Foo, Is.EqualTo(456));
			Assert.That(thing.Bar, Is.EqualTo("world"));

			slice = Slice.Unescape("<03><14><00><FF><00>");
			thing = FdbTuplePackers.DeserializeFormattable<Thing>(slice);
			Assert.That(thing, Is.Not.Null);
			Assert.That(thing.Foo, Is.EqualTo(0));
			Assert.That(thing.Bar, Is.EqualTo(null));

			slice = Slice.Unescape("<00>");
			thing = FdbTuplePackers.DeserializeFormattable<Thing>(slice);
			Assert.That(thing, Is.Null);
		}

		[Test]
		public void Test_FdbTuple_BatchPack_Of_Tuples()
		{
			Slice[] slices;
			var tuples = new IFdbTuple[] {
				FdbTuple.Create("hello"),
				FdbTuple.Create(123),
				FdbTuple.Create(false),
				FdbTuple.Create("world", 456, true)
			};

			// array version
			slices = FdbTuple.Pack(tuples);
			Assert.That(slices, Is.Not.Null);
			Assert.That(slices.Length, Is.EqualTo(tuples.Length));
			Assert.That(slices, Is.EqualTo(tuples.Select(t => t.ToSlice()).ToArray()));

			// IEnumerable version that is passed an array
			slices = FdbTuple.Pack((IEnumerable<IFdbTuple>)tuples);
			Assert.That(slices, Is.Not.Null);
			Assert.That(slices.Length, Is.EqualTo(tuples.Length));
			Assert.That(slices, Is.EqualTo(tuples.Select(t => t.ToSlice()).ToArray()));

			// IEnumerable version but with a "real" enumerable 
			slices = FdbTuple.Pack(tuples.Select(t => t));
			Assert.That(slices, Is.Not.Null);
			Assert.That(slices.Length, Is.EqualTo(tuples.Length));
			Assert.That(slices, Is.EqualTo(tuples.Select(t => t.ToSlice()).ToArray()));
		}

		[Test]
		public void Test_FdbTuple_EncodeKeys_Of_T()
		{
			Slice[] slices;

			#region PackRange(Tuple, ...)

			var tuple = FdbTuple.Create("hello");
			int[] items = new int[] { 1, 2, 3, 123, -1, int.MaxValue };

			// array version
			slices = FdbTuple.EncodePrefixedKeys<int>(tuple, items);
			Assert.That(slices, Is.Not.Null);
			Assert.That(slices.Length, Is.EqualTo(items.Length));
			Assert.That(slices, Is.EqualTo(items.Select(x => tuple.Append(x).ToSlice()).ToArray()));

			// IEnumerable version that is passed an array
			slices = FdbTuple.EncodePrefixedKeys<int>(tuple, (IEnumerable<int>)items);
			Assert.That(slices, Is.Not.Null);
			Assert.That(slices.Length, Is.EqualTo(items.Length));
			Assert.That(slices, Is.EqualTo(items.Select(x => tuple.Append(x).ToSlice()).ToArray()));

			// IEnumerable version but with a "real" enumerable 
			slices = FdbTuple.EncodePrefixedKeys<int>(tuple, items.Select(t => t));
			Assert.That(slices, Is.Not.Null);
			Assert.That(slices.Length, Is.EqualTo(items.Length));
			Assert.That(slices, Is.EqualTo(items.Select(x => tuple.Append(x).ToSlice()).ToArray()));

			#endregion

			#region PackRange(Slice, ...)

			string[] words = new string[] { "hello", "world", "très bien", "断トツ", "abc\0def", null, String.Empty };

			var merged = FdbTuple.EncodePrefixedKeys(Slice.FromByte(42), words);
			Assert.That(merged, Is.Not.Null);
			Assert.That(merged.Length, Is.EqualTo(words.Length));

			for (int i = 0; i < words.Length; i++)
			{
				var expected = Slice.FromByte(42) + FdbTuple.EncodeKey(words[i]);
				Assert.That(merged[i], Is.EqualTo(expected));

				Assert.That(merged[i].Array, Is.SameAs(merged[0].Array), "All slices should be stored in the same buffer");
				if (i > 0) Assert.That(merged[i].Offset, Is.EqualTo(merged[i - 1].Offset + merged[i - 1].Count), "All slices should be contiguous");
			}

			// corner cases
			Assert.That(() => FdbTuple.EncodePrefixedKeys<int>(Slice.Empty, default(int[])), Throws.InstanceOf<ArgumentNullException>().With.Property("ParamName").EqualTo("keys"));
			Assert.That(() => FdbTuple.EncodePrefixedKeys<int>(Slice.Empty, default(IEnumerable<int>)), Throws.InstanceOf<ArgumentNullException>().With.Property("ParamName").EqualTo("keys"));

			#endregion
		}

		[Test]
		public void Test_FdbTuple_EncodeKeys_Boxed()
		{
			Slice[] slices;
			var tuple = FdbTuple.Create("hello");
			object[] items = new object[] { "world", 123, false, Guid.NewGuid(), long.MinValue };

			// array version
			slices = FdbTuple.EncodePrefixedKeys<object>(tuple, items);
			Assert.That(slices, Is.Not.Null);
			Assert.That(slices.Length, Is.EqualTo(items.Length));
			Assert.That(slices, Is.EqualTo(items.Select(x => tuple.Append(x).ToSlice()).ToArray()));

			// IEnumerable version that is passed an array
			slices = FdbTuple.EncodePrefixedKeys<object>(tuple, (IEnumerable<object>)items);
			Assert.That(slices, Is.Not.Null);
			Assert.That(slices.Length, Is.EqualTo(items.Length));
			Assert.That(slices, Is.EqualTo(items.Select(x => tuple.Append(x).ToSlice()).ToArray()));

			// IEnumerable version but with a "real" enumerable 
			slices = FdbTuple.EncodePrefixedKeys<object>(tuple, items.Select(t => t));
			Assert.That(slices, Is.Not.Null);
			Assert.That(slices.Length, Is.EqualTo(items.Length));
			Assert.That(slices, Is.EqualTo(items.Select(x => tuple.Append(x).ToSlice()).ToArray()));
		}

		#endregion

		#region FdbTupleParser

		private static string Clean(string value)
		{
			var sb = new StringBuilder(value.Length + 8);
			foreach (var c in value)
			{
				if (c < ' ') sb.Append("\\x").Append(((int)c).ToString("x2")); else sb.Append(c);
			}
			return sb.ToString();
		}

		private static void PerformWriterTest<T>(FdbTuplePackers.Encoder<T> action, T value, string expectedResult, string message = null)
		{
			var writer = new TupleWriter();
			action(ref writer, value);

			Assert.That(
				writer.Output.ToSlice().ToHexaString(' '),
				Is.EqualTo(expectedResult),
				message != null ? "Value {0} ({1}) was not properly packed: {2}" : "Value {0} ({1}) was not properly packed", value == null ? "<null>" : value is string ? Clean(value as string) : value.ToString(), (value == null ? "null" : value.GetType().Name), message);
		}

		[Test]
		public void Test_FdbTupleParser_WriteInt64()
		{
			var test = new FdbTuplePackers.Encoder<long>(FdbTupleParser.WriteInt64);

			PerformWriterTest(test, 0L, "14");

			PerformWriterTest(test, 1L, "15 01");
			PerformWriterTest(test, 2L, "15 02");
			PerformWriterTest(test, 123L, "15 7B");
			PerformWriterTest(test, 255L, "15 FF");
			PerformWriterTest(test, 256L, "16 01 00");
			PerformWriterTest(test, 257L, "16 01 01");
			PerformWriterTest(test, 65535L, "16 FF FF");
			PerformWriterTest(test, 65536L, "17 01 00 00");
			PerformWriterTest(test, 65537L, "17 01 00 01");

			PerformWriterTest(test, -1L, "13 FE");
			PerformWriterTest(test, -123L, "13 84");
			PerformWriterTest(test, -255L, "13 00");
			PerformWriterTest(test, -256L, "12 FE FF");
			PerformWriterTest(test, -65535L, "12 00 00");
			PerformWriterTest(test, -65536L, "11 FE FF FF");

			PerformWriterTest(test, (1L << 24) - 1, "17 FF FF FF");
			PerformWriterTest(test, 1L << 24, "18 01 00 00 00");

			PerformWriterTest(test, (1L << 32) - 1, "18 FF FF FF FF");
			PerformWriterTest(test, (1L << 32), "19 01 00 00 00 00");

			PerformWriterTest(test, long.MaxValue, "1C 7F FF FF FF FF FF FF FF");
			PerformWriterTest(test, long.MinValue, "0C 7F FF FF FF FF FF FF FF");
			PerformWriterTest(test, long.MaxValue - 1, "1C 7F FF FF FF FF FF FF FE");
			PerformWriterTest(test, long.MinValue + 1, "0C 80 00 00 00 00 00 00 00");

		}

		[Test]
		public void Test_FdbTupleParser_WriteInt64_Respects_Ordering()
		{
			var list = new List<KeyValuePair<long, Slice>>();

			Action<long> test = (x) =>
			{
				var writer = new TupleWriter();
				FdbTupleParser.WriteInt64(ref writer, x);
				var res = new KeyValuePair<long, Slice>(x, writer.Output.ToSlice());
				list.Add(res);
				Log("{0,20} : {0:x16} {1}", res.Key, res.Value.ToString());
			};

			// We can't test 2^64 values, be we are interested at what happens around powers of two (were size can change)

			// negatives
			for (int i = 63; i >= 3; i--)
			{
				long x = -(1L << i);

				if (i < 63)
				{
					test(x - 2);
					test(x - 1);
				}
				test(x + 0);
				test(x + 1);
				test(x + 2);
			}

			test(-2);
			test(0);
			test(+1);
			test(+2);

			// positives
			for (int i = 3; i <= 63; i++)
			{
				long x = (1L << i);

				test(x - 2);
				test(x - 1);
				if (i < 63)
				{
					test(x + 0);
					test(x + 1);
					test(x + 2);
				}
			}

			KeyValuePair<long, Slice> previous = list[0];
			for (int i = 1; i < list.Count; i++)
			{
				KeyValuePair<long, Slice> current = list[i];

				Assert.That(current.Key, Is.GreaterThan(previous.Key));
				Assert.That(current.Value, Is.GreaterThan(previous.Value), "Expect {0} > {1}", current.Key, previous.Key);

				previous = current;
			}
		}

		[Test]
		public void Test_FdbTupleParser_WriteUInt64()
		{
			var test = new FdbTuplePackers.Encoder<ulong>(FdbTupleParser.WriteUInt64);

			PerformWriterTest(test, 0UL, "14");

			PerformWriterTest(test, 1UL, "15 01");
			PerformWriterTest(test, 123UL, "15 7B");
			PerformWriterTest(test, 255UL, "15 FF");
			PerformWriterTest(test, 256UL, "16 01 00");
			PerformWriterTest(test, 257UL, "16 01 01");
			PerformWriterTest(test, 65535UL, "16 FF FF");
			PerformWriterTest(test, 65536UL, "17 01 00 00");
			PerformWriterTest(test, 65537UL, "17 01 00 01");

			PerformWriterTest(test, (1UL << 24) - 1, "17 FF FF FF");
			PerformWriterTest(test, 1UL << 24, "18 01 00 00 00");

			PerformWriterTest(test, (1UL << 32) - 1, "18 FF FF FF FF");
			PerformWriterTest(test, (1UL << 32), "19 01 00 00 00 00");

			PerformWriterTest(test, ulong.MaxValue, "1C FF FF FF FF FF FF FF FF");
			PerformWriterTest(test, ulong.MaxValue-1, "1C FF FF FF FF FF FF FF FE");

		}

		[Test]
		public void Test_FdbTupleParser_WriteUInt64_Respects_Ordering()
		{
			var list = new List<KeyValuePair<ulong, Slice>>();

			Action<ulong> test = (x) =>
			{
				var writer = new TupleWriter();
				FdbTupleParser.WriteUInt64(ref writer, x);
				var res = new KeyValuePair<ulong, Slice>(x, writer.Output.ToSlice());
				list.Add(res);
#if DEBUG
				Log("{0,20} : {0:x16} {1}", res.Key, res.Value);
#endif
			};

			// We can't test 2^64 values, be we are interested at what happens around powers of two (were size can change)

			test(0);
			test(1);

			// positives
			for (int i = 3; i <= 63; i++)
			{
				ulong x = (1UL << i);

				test(x - 2);
				test(x - 1);
				test(x + 0);
				test(x + 1);
				test(x + 2);
			}
			test(ulong.MaxValue - 2);
			test(ulong.MaxValue - 1);
			test(ulong.MaxValue);

			KeyValuePair<ulong, Slice> previous = list[0];
			for (int i = 1; i < list.Count; i++)
			{
				KeyValuePair<ulong, Slice> current = list[i];

				Assert.That(current.Key, Is.GreaterThan(previous.Key));
				Assert.That(current.Value, Is.GreaterThan(previous.Value), "Expect {0} > {1}", current.Key, previous.Key);

				previous = current;
			}
		}

		[Test]
		public void Test_FdbTupleParser_WriteString()
		{
			string s;
			var test = new FdbTuplePackers.Encoder<string>(FdbTupleParser.WriteString);
			Func<string, string> encodeSimple = (value) => "02 " + Slice.Create(Encoding.UTF8.GetBytes(value)).ToHexaString(' ') + " 00";
			Func<string, string> encodeWithZeroes = (value) => "02 " + Slice.Create(Encoding.UTF8.GetBytes(value)).ToHexaString(' ').Replace("00", "00 FF") + " 00";

			PerformWriterTest(test, null, "00");
			PerformWriterTest(test, String.Empty, "02 00");
			PerformWriterTest(test, "A", "02 41 00");
			PerformWriterTest(test, "\x80", "02 C2 80 00");
			PerformWriterTest(test, "\xFF", "02 C3 BF 00");
			PerformWriterTest(test, "\xFFFE", "02 EF BF BE 00"); // UTF-8 BOM

			PerformWriterTest(test, "ASCII", "02 41 53 43 49 49 00");
			PerformWriterTest(test, "héllø le 世界", "02 68 C3 A9 6C 6C C3 B8 20 6C 65 20 E4 B8 96 E7 95 8C 00");

			// Must escape '\0' contained in the string as '\x00\xFF'
			PerformWriterTest(test, "\0", "02 00 FF 00");
			PerformWriterTest(test, "A\0", "02 41 00 FF 00");
			PerformWriterTest(test, "\0A", "02 00 FF 41 00");
			PerformWriterTest(test, "A\0\0A", "02 41 00 FF 00 FF 41 00");
			PerformWriterTest(test, "A\0B\0\xFF", "02 41 00 FF 42 00 FF C3 BF 00");

			// random human text samples

			s = "This is a long string that has more than 1024 chars to force the encoder to use multiple chunks, and with some random UNICODE at the end so that it can not be optimized as ASCII-only." + new string('A', 1024) + "ಠ_ಠ";
			PerformWriterTest(test, s, encodeSimple(s));

			s = "String of exactly 1024 ASCII chars !"; s += new string('A', 1024 - s.Length);
			PerformWriterTest(test, s, encodeSimple(s));

			s = "Ceci est une chaîne de texte qui contient des caractères UNICODE supérieurs à 0x7F mais inférieurs à 0x800"; // n'est-il pas ?
			PerformWriterTest(test, s, encodeSimple(s));

			s = "色は匂へど　散りぬるを 我が世誰そ　常ならむ 有為の奥山　今日越えて 浅き夢見じ　酔ひもせず"; // iroha!
			PerformWriterTest(test, s, encodeSimple(s));

			s = "String that ends with funny UTF-32 chars like \xDFFF\xDBFF"; // supposed to be 0x10FFFF encoded in UTF-16
			PerformWriterTest(test, s, encodeSimple(s));

			// strings with random non-zero UNICODE chars
			var rnd = new Random();
			for (int k = 0; k < 100; k++)
			{
				int size = 1 + rnd.Next(10000);
				var chars = new char[size];
				for (int i = 0; i < chars.Length; i++)
				{
					// 1..0xFFFF
					switch (rnd.Next(3))
					{
						case 0: chars[i] = (char)rnd.Next(1, 0x80); break;
						case 1: chars[i] = (char)rnd.Next(0x80, 0x800); break;
						case 2: chars[i] = (char)rnd.Next(0x800, 0xFFFF); break;
					}
				}
				s = new string(chars);
				PerformWriterTest(test, s, encodeSimple(s), "Random string with non-zero unicode chars (from 1 to 0xFFFF)");
			}

			// random strings with zeroes
			for (int k = 0; k < 100; k++)
			{
				int size = 1 + rnd.Next(10000);
				var chars = new char[size];
				for (int i = 0; i < chars.Length; i++)
				{
					switch(rnd.Next(4))
					{
						case 0: chars[i] = '\0'; break;
						case 1: chars[i] = (char)rnd.Next(1, 0x80); break;
						case 2: chars[i] = (char)rnd.Next(0x80, 0x800); break;
						case 3: chars[i] = (char)rnd.Next(0x800, 0xFFFF); break;
					}
				}
				s = new string(chars);
				PerformWriterTest(test, s, encodeWithZeroes(s), "Random string with zeros ");
			}
	
		}

		[Test]
		public void Test_FdbTupleParser_WriteChar()
		{
			var test = new FdbTuplePackers.Encoder<char>(FdbTupleParser.WriteChar);

			// 1 bytes
			PerformWriterTest(test, 'A', "02 41 00", "Unicode chars in the ASCII table take only one byte in UTF-8");
			PerformWriterTest(test, '\0', "02 00 FF 00", "\\0 must be escaped as 00 FF");
			PerformWriterTest(test, '\x7F', "02 7F 00", "1..127 take ony 1 bytes");
			// 2 bytes
			PerformWriterTest(test, '\x80', "02 C2 80 00", "128 needs 2 bytes");
			PerformWriterTest(test, '\xFF', "02 C3 BF 00", "ASCII chars above 128 take at least 2 bytes in UTF-8");
			PerformWriterTest(test, 'é', "02 C3 A9 00", "0x00E9, LATIN SMALL LETTER E WITH ACUTE");
			PerformWriterTest(test, 'ø', "02 C3 B8 00", "0x00F8, LATIN SMALL LETTER O WITH STROKE");
			PerformWriterTest(test, '\x07FF', "02 DF BF 00");
			// 3 bytes
			PerformWriterTest(test, '\x0800', "02 E0 A0 80 00", "0x800 takes at least 3 bytes");
			PerformWriterTest(test, 'ಠ', "02 E0 B2 A0 00", "KANNADA LETTER TTHA");
			PerformWriterTest(test, '世', "02 E4 B8 96 00", "0x4E16, CJK Ideograph");
			PerformWriterTest(test, '界', "02 E7 95 8C 00", "0x754C, CJK Ideoghaph");
			PerformWriterTest(test, '\xFFFE', "02 EF BF BE 00", "Unicode BOM becomes EF BF BE in UTF-8");
			PerformWriterTest(test, '\xFFFF', "02 EF BF BF 00", "Maximum UTF-16 character");

			// check all the unicode chars
			for (int i = 1; i <= 65535; i++)
			{
				char c = (char)i;
				var writer = new TupleWriter();
				FdbTupleParser.WriteChar(ref writer, c);
				string s = new string(c, 1);
				Assert.That(writer.Output.ToSlice().ToString(), Is.EqualTo("<02>" + Slice.Create(Encoding.UTF8.GetBytes(s)).ToString() + "<00>"), "{0} '{1}'", i, c);
			}
		}

		#endregion

		#region Equality / Comparison

		private static void AssertEquality(IFdbTuple x, IFdbTuple y)
		{
			Assert.That(x.Equals(y), Is.True, "x.Equals(y)");
			Assert.That(x.Equals((object)y), Is.True, "x.Equals((object)y)");
			Assert.That(y.Equals(x), Is.True, "y.Equals(x)");
			Assert.That(y.Equals((object)x), Is.True, "y.Equals((object)y");
		}

		private static void AssertInequality(IFdbTuple x, IFdbTuple y)
		{
			Assert.That(x.Equals(y), Is.False, "!x.Equals(y)");
			Assert.That(x.Equals((object)y), Is.False, "!x.Equals((object)y)");
			Assert.That(y.Equals(x), Is.False, "!y.Equals(x)");
			Assert.That(y.Equals((object)x), Is.False, "!y.Equals((object)y");
		}

		[Test]
		public void Test_FdbTuple_Equals()
		{
			var t1 = FdbTuple.Create(1, 2);
			// self equality
			AssertEquality(t1, t1);

			var t2 = FdbTuple.Create(1, 2);
			// same type equality
			AssertEquality(t1, t2);

			var t3 = FdbTuple.Create(new object[] { 1, 2 });
			// other tuple type equality
			AssertEquality(t1, t3);

			var t4 = FdbTuple.Create(1).Append(2);
			// multi step
			AssertEquality(t1, t4);
		}

		[Test]
		public void Test_FdbTuple_Similar()
		{
			var t1 = FdbTuple.Create(1, 2);
			var t2 = FdbTuple.Create((long)1, (short)2);
			var t3 = FdbTuple.Create("1", "2");
			var t4 = FdbTuple.Create(new object[] { 1, 2L });
			var t5 = FdbTuple.Unpack(Slice.Unescape("<02>1<00><15><02>"));

			AssertEquality(t1, t1);
			AssertEquality(t1, t2);
			AssertEquality(t1, t3);
			AssertEquality(t1, t4);
			AssertEquality(t1, t5);
			AssertEquality(t2, t2);
			AssertEquality(t2, t3);
			AssertEquality(t2, t4);
			AssertEquality(t2, t5);
			AssertEquality(t3, t3);
			AssertEquality(t3, t4);
			AssertEquality(t3, t5);
			AssertEquality(t4, t4);
			AssertEquality(t4, t5);
			AssertEquality(t5, t5);
		}

		[Test]
		public void Test_FdbTuple_Not_Equal()
		{
			var t1 = FdbTuple.Create(1, 2);

			var x1 = FdbTuple.Create(2, 1);
			var x2 = FdbTuple.Create("11", "22");
			var x3 = FdbTuple.Create(1, 2, 3);
			var x4 = FdbTuple.Unpack(Slice.Unescape("<15><01>"));

			AssertInequality(t1, x1);
			AssertInequality(t1, x2);
			AssertInequality(t1, x3);
			AssertInequality(t1, x4);

			AssertInequality(x1, x2);
			AssertInequality(x1, x3);
			AssertInequality(x1, x4);
			AssertInequality(x2, x3);
			AssertInequality(x2, x4);
			AssertInequality(x3, x4);
		}

		[Test]
		public void Test_FdbTuple_Substring_Equality()
		{
			var x = FdbTuple.FromArray<string>(new [] { "A", "C" });
			var y = FdbTuple.FromArray<string>(new[] { "A", "B", "C" });

			Assert.That(x.Substring(0, 1), Is.EqualTo(y.Substring(0, 1)));
			Assert.That(x.Substring(1, 1), Is.EqualTo(y.Substring(2, 1)));

			var aa = FdbTuple.Create<string>("A");
			var bb = FdbTuple.Create<string>("A");
			Assert.That(aa == bb, Is.True);

			var a = x.Substring(0, 1);
			var b = y.Substring(0, 1);
			Assert.That(a.Equals((IFdbTuple)b), Is.True);
			Assert.That(a.Equals((object)b), Is.True);
			Assert.That(object.Equals(a, b), Is.True);
			Assert.That(FdbTuple.Equals(a, b), Is.True);
			Assert.That(FdbTuple.Equivalent(a, b), Is.True);

			// this is very unfortunate, but 'a == b' does NOT work because IFdbTuple is an interface, and there is no known way to make it work :(
			//Assert.That(a == b, Is.True);
		}

		[Test]
		public void Test_FdbTuple_String_AutoCast()
		{
			// 'a' ~= "A"
			AssertEquality(FdbTuple.Create("A"), FdbTuple.Create('A'));
			AssertInequality(FdbTuple.Create("A"), FdbTuple.Create('B'));
			AssertInequality(FdbTuple.Create("A"), FdbTuple.Create('a'));

			// ASCII ~= Unicode
			AssertEquality(FdbTuple.Create("ABC"), FdbTuple.Create(Slice.FromAscii("ABC")));
			AssertInequality(FdbTuple.Create("ABC"), FdbTuple.Create(Slice.FromAscii("DEF")));
			AssertInequality(FdbTuple.Create("ABC"), FdbTuple.Create(Slice.FromAscii("abc")));

			// 'a' ~= ASCII 'a'
			AssertEquality(FdbTuple.Create(Slice.FromAscii("A")), FdbTuple.Create('A'));
			AssertInequality(FdbTuple.Create(Slice.FromAscii("A")), FdbTuple.Create('B'));
			AssertInequality(FdbTuple.Create(Slice.FromAscii("A")), FdbTuple.Create('a'));
		}

		#endregion

		#region Formatters

		[Test]
		public void Test_Default_FdbTupleFormatter_For_Common_Types()
		{

			// common simple types
			Assert.That(FdbTupleFormatter<int>.Default, Is.InstanceOf<FdbGenericTupleFormatter<int>>());
			Assert.That(FdbTupleFormatter<bool>.Default, Is.InstanceOf<FdbGenericTupleFormatter<bool>>());
			Assert.That(FdbTupleFormatter<string>.Default, Is.InstanceOf<FdbGenericTupleFormatter<string>>());

			// corner cases
			Assert.That(FdbTupleFormatter<IFdbTuple>.Default, Is.InstanceOf<FdbAnonymousTupleFormatter<IFdbTuple>>());
			Assert.That(FdbTupleFormatter<FdbMemoizedTuple>.Default, Is.InstanceOf<FdbAnonymousTupleFormatter<FdbMemoizedTuple>>());

			// ITupleFormattable types
			Assert.That(FdbTupleFormatter<Thing>.Default, Is.InstanceOf<FdbFormattableTupleFormatter<Thing>>());
		}

		[Test]
		public void Test_Format_Common_Types()
		{
			Assert.That(FdbTupleFormatter<int>.Default.ToTuple(123), Is.EqualTo(FdbTuple.Create(123)));
			Assert.That(FdbTupleFormatter<int>.Default.FromTuple(FdbTuple.Create(123)), Is.EqualTo(123));

			Assert.That(FdbTupleFormatter<bool>.Default.ToTuple(true), Is.EqualTo(FdbTuple.Create(true)));
			Assert.That(FdbTupleFormatter<bool>.Default.FromTuple(FdbTuple.Create(true)), Is.True);

			Assert.That(FdbTupleFormatter<string>.Default.ToTuple("hello"), Is.EqualTo(FdbTuple.Create<string>("hello")));
			Assert.That(FdbTupleFormatter<string>.Default.FromTuple(FdbTuple.Create("hello")), Is.EqualTo("hello"));

			var t = FdbTuple.Create(new object[] { "hello", 123, false });
			Assert.That(FdbTupleFormatter<IFdbTuple>.Default.ToTuple(t), Is.SameAs(t));
			Assert.That(FdbTupleFormatter<IFdbTuple>.Default.FromTuple(t), Is.SameAs(t));

			var thing = new Thing { Foo = 123, Bar = "hello" };
			Assert.That(FdbTupleFormatter<Thing>.Default.ToTuple(thing), Is.EqualTo(FdbTuple.Create(123, "hello")));

			var thing2 = FdbTupleFormatter<Thing>.Default.FromTuple(FdbTuple.Create(456, "world"));
			Assert.That(thing2, Is.Not.Null);
			Assert.That(thing2.Foo, Is.EqualTo(456));
			Assert.That(thing2.Bar, Is.EqualTo("world"));

		}

		[Test]
		public void Test_Create_Appender_Formatter()
		{
			// create an appender formatter that will always add the values after the same prefix

			var fmtr = FdbTupleFormatter<int>.CreateAppender(FdbTuple.Create("hello", "world"));
			Assert.That(fmtr, Is.InstanceOf<FdbAnonymousTupleFormatter<int>>());

			Assert.That(fmtr.ToTuple(123), Is.EqualTo(FdbTuple.Create("hello", "world", 123)));
			Assert.That(fmtr.ToTuple(456), Is.EqualTo(FdbTuple.Create("hello", "world", 456)));
			Assert.That(fmtr.ToTuple(-1), Is.EqualTo(FdbTuple.Create("hello", "world", -1)));

			Assert.That(fmtr.FromTuple(FdbTuple.Create("hello", "world", 42)), Is.EqualTo(42));
			Assert.That(fmtr.FromTuple(FdbTuple.Create("hello", "world", -1)), Is.EqualTo(-1));

			Assert.That(() => fmtr.FromTuple(null), Throws.InstanceOf<ArgumentNullException>());
			Assert.That(() => fmtr.FromTuple(FdbTuple.Empty), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => fmtr.FromTuple(FdbTuple.Create("hello", "world", 42, 77)), Throws.InstanceOf<ArgumentException>(), "Too many values");
			Assert.That(() => fmtr.FromTuple(FdbTuple.Create("hello_world", 42)), Throws.InstanceOf<ArgumentException>(), "not enough values");
			Assert.That(() => fmtr.FromTuple(FdbTuple.Create("world", "hello", "42")), Throws.InstanceOf<ArgumentException>(), "incorrect type");
			Assert.That(() => fmtr.FromTuple(FdbTuple.Create(42)), Throws.InstanceOf<ArgumentException>(), "missing prefix");
			Assert.That(() => fmtr.FromTuple(FdbTuple.Create("extra", "hello", "world", 42)), Throws.InstanceOf<ArgumentException>(), "prefix must match exactly");
			Assert.That(() => fmtr.FromTuple(FdbTuple.Create("Hello", "World", 42)), Throws.InstanceOf<ArgumentException>(), "case sensitive");
		}

		#endregion

		#region Bench....

		[Test]
		public void Bench_FdbTuple_Unpack_Random()
		{
			const int N = 100 * 1000;

			Slice FUNKY_ASCII = Slice.FromAscii("bonjour\x00le\x00\xFFmonde");
			string FUNKY_STRING = "hello\x00world";
			string UNICODE_STRING = "héllø 世界";

			Console.Write("Creating {0:N0} random tuples", N);
			var tuples = new List<IFdbTuple>(N);
			var rnd = new Random(777);
			var guids = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray();
			var uuid128s = Enumerable.Range(0, 10).Select(_ => Uuid128.NewUuid()).ToArray();
			var uuid64s = Enumerable.Range(0, 10).Select(_ => Uuid64.NewUuid()).ToArray();
			var fuzz = new byte[1024 + 1000]; rnd.NextBytes(fuzz);
			var sw = Stopwatch.StartNew();
			for (int i = 0; i < N; i++)
			{
				IFdbTuple tuple = FdbTuple.Empty;
				int s = 1 + (int)Math.Sqrt(rnd.Next(128));
				if (i % (N / 100) == 0) Console.Write(".");
				for (int j = 0; j < s; j++)
				{
					switch (rnd.Next(17))
					{
						case 0: tuple = tuple.Append<int>(rnd.Next(255)); break;
						case 1: tuple = tuple.Append<int>(-1 - rnd.Next(255)); break;
						case 2: tuple = tuple.Append<int>(256 + rnd.Next(65536 - 256)); break;
						case 3: tuple = tuple.Append<int>(rnd.Next(int.MaxValue)); break;
						case 4: tuple = tuple.Append<long>((rnd.Next(int.MaxValue) << 32) | rnd.Next(int.MaxValue)); break;
						case 5: tuple = tuple.Append(new string('A', 1 + rnd.Next(16))); break;
						case 6: tuple = tuple.Append(new string('B', 8 + (int)Math.Sqrt(rnd.Next(1024)))); break;
						case 7: tuple = tuple.Append<string>(UNICODE_STRING); break;
						case 8: tuple = tuple.Append<string>(FUNKY_STRING); break;
						case 9: tuple = tuple.Append<Slice>(FUNKY_ASCII); break;
						case 10: tuple = tuple.Append<Guid>(guids[rnd.Next(10)]); break;
						case 11: tuple = tuple.Append<Uuid128>(uuid128s[rnd.Next(10)]); break;
						case 12: tuple = tuple.Append<Uuid64>(uuid64s[rnd.Next(10)]); break;
						case 13: tuple = tuple.Append<Slice>(Slice.Create(fuzz, rnd.Next(1000), 1 + (int)Math.Sqrt(rnd.Next(1024)))); break;
						case 14: tuple = tuple.Append(default(string)); break;
						case 15: tuple = tuple.Append<object>("hello"); break;
						case 16: tuple = tuple.Append<bool>(rnd.Next(2) == 0); break;
					}
				}
				tuples.Add(tuple);
			}
			sw.Stop();
			Log(" done in {0:N3} sec", sw.Elapsed.TotalSeconds);
			Log(" > {0:N0} items", tuples.Sum(x => x.Count));
			Log(" > {0}", tuples[42]);
			Log();

			Console.Write("Packing tuples...");
			sw.Restart();
			var slices = FdbTuple.Pack(tuples);
			sw.Stop();
			Log(" done in {0:N3} sec", sw.Elapsed.TotalSeconds);
			Log(" > {0:N0} tps", N / sw.Elapsed.TotalSeconds);
			Log(" > {0:N0} bytes", slices.Sum(x => x.Count));
			Log(" > {0}", slices[42]);
			Log();

			Console.Write("Unpacking tuples...");
			sw.Restart();
			var unpacked = slices.Select(slice => FdbTuple.Unpack(slice)).ToList();
			sw.Stop();
			Log(" done in {0:N3} sec", sw.Elapsed.TotalSeconds);
			Log(" > {0:N0} tps", N / sw.Elapsed.TotalSeconds);
			Log(" > {0}", unpacked[42]);
			Log();

			Console.Write("Comparing ...");
			sw.Restart();
			tuples.Zip(unpacked, (x, y) => x.Equals(y)).All(b => b);
			sw.Stop();
			Log(" done in {0:N3} sec", sw.Elapsed.TotalSeconds);
			Log();

			Console.Write("Tuples.ToString ...");
			sw.Restart();
			var strings = tuples.Select(x => x.ToString()).ToList();
			sw.Stop();
			Log(" done in {0:N3} sec", sw.Elapsed.TotalSeconds);
			Log(" > {0:N0} chars", strings.Sum(x => x.Length));
			Log(" > {0}", strings[42]);
			Log();

			Console.Write("Unpacked.ToString ...");
			sw.Restart();
			strings = unpacked.Select(x => x.ToString()).ToList();
			sw.Stop();
			Log(" done in {0:N3} sec", sw.Elapsed.TotalSeconds);
			Log(" > {0:N0} chars", strings.Sum(x => x.Length));
			Log(" > {0}", strings[42]);
			Log();

			Console.Write("Memoizing ...");
			sw.Restart();
			var memoized = tuples.Select(x => x.Memoize()).ToList();
			sw.Stop();
			Log(" done in {0:N3} sec", sw.Elapsed.TotalSeconds);
		}

		#endregion

		private class Thing : ITupleFormattable
		{
			public Thing()
			{ }

			public int Foo { get; set; }
			public string Bar { get; set; }

			IFdbTuple ITupleFormattable.ToTuple()
			{
				return FdbTuple.Create(this.Foo, this.Bar);
			}

			void ITupleFormattable.FromTuple(IFdbTuple tuple)
			{
				this.Foo = tuple.Get<int>(0);
				this.Bar = tuple.Get<string>(1);
			}
		}
	
	}


}
