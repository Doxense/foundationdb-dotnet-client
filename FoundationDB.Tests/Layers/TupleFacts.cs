#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
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

namespace FoundationDB.Layers.Tuples.Tests
{
	using FoundationDB.Client;
	using FoundationDB.Client.Converters;
	using FoundationDB.Client.Utils;
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;

	[TestFixture]
	public class TupleFacts
	{

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

			var tn = FdbTuple.Create(new object[] { "hello world", 123, false, 1234, -1234, "six" });
			Assert.That(tn.Count, Is.EqualTo(6));
			Assert.That(tn.Get<string>(0), Is.EqualTo("hello world"));
			Assert.That(tn.Get<int>(1), Is.EqualTo(123));
			Assert.That(tn.Get<bool>(2), Is.EqualTo(false));
			Assert.That(tn.Get<int>(3), Is.EqualTo(1234));
			Assert.That(tn.Get<long>(4), Is.EqualTo(-1234));
			Assert.That(tn.Get<string>(5), Is.EqualTo("six"));
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
		public void Test_FdbTuple_Last()
		{
			// tuple.Last<T>() should be equivalent to tuple.Get<T>(-1)

			var t1 = FdbTuple.Create(1);
			Assert.That(t1.Last<int>(), Is.EqualTo(1));
			Assert.That(t1.Last<string>(), Is.EqualTo("1"));

			var t2 = FdbTuple.Create(1, 2);
			Assert.That(t2.Last<int>(), Is.EqualTo(2));
			Assert.That(t2.Last<string>(), Is.EqualTo("2"));

			var t3 = FdbTuple.Create(1, 2, 3);
			Assert.That(t3.Last<int>(), Is.EqualTo(3));
			Assert.That(t3.Last<string>(), Is.EqualTo("3"));

			var tn = FdbTuple.Create(1, 2, 3, 4);
			Assert.That(tn.Last<int>(), Is.EqualTo(4));
			Assert.That(tn.Last<string>(), Is.EqualTo("4"));

			Assert.That(() => FdbTuple.Empty.Last<string>(), Throws.InstanceOf<InvalidOperationException>());

		}

		[Test]
		public void Test_FdbTuple_UnpackLast()
		{
			// should only work with tuples having at least one element

			Slice packed;

			packed = FdbTuple.Pack(1);
			Assert.That(FdbTuple.UnpackLast<int>(packed), Is.EqualTo(1));
			Assert.That(FdbTuple.UnpackLast<string>(packed), Is.EqualTo("1"));

			packed = FdbTuple.Pack(1, 2);
			Assert.That(FdbTuple.UnpackLast<int>(packed), Is.EqualTo(2));
			Assert.That(FdbTuple.UnpackLast<string>(packed), Is.EqualTo("2"));

			packed = FdbTuple.Pack(1, 2, 3);
			Assert.That(FdbTuple.UnpackLast<int>(packed), Is.EqualTo(3));
			Assert.That(FdbTuple.UnpackLast<string>(packed), Is.EqualTo("3"));

			Assert.That(() => FdbTuple.UnpackLast<string>(Slice.Nil), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => FdbTuple.UnpackLast<string>(Slice.Empty), Throws.InstanceOf<InvalidOperationException>());

		}

		[Test]
		public void Test_FdbTuple_UnpackSingle()
		{
			// should only work with tuples having exactly one element

			Slice packed;

			packed = FdbTuple.Pack(1);
			Assert.That(FdbTuple.UnpackSingle<int>(packed), Is.EqualTo(1));
			Assert.That(FdbTuple.UnpackSingle<string>(packed), Is.EqualTo("1"));

			packed = FdbTuple.Pack("Hello\0World");
			Assert.That(FdbTuple.UnpackSingle<string>(packed), Is.EqualTo("Hello\0World"));

			Assert.That(() => FdbTuple.UnpackSingle<string>(Slice.Nil), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => FdbTuple.UnpackSingle<string>(Slice.Empty), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => FdbTuple.UnpackSingle<int>(FdbTuple.Pack(1, 2)), Throws.InstanceOf<FormatException>());
			Assert.That(() => FdbTuple.UnpackSingle<int>(FdbTuple.Pack(1, 2, 3)), Throws.InstanceOf<FormatException>());

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
				Assert.Fail("{0}: Count mismatch between {1} and expected {2}", message, t, FdbTuple.ToString(expected));
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
			tmp = t.Memoize().Items;
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
			VerifyTuple("[:]", tuple[null, 5], items);
			VerifyTuple("[:]", tuple[0, null], items);
			VerifyTuple("[:]", tuple[0, 5], items);
			VerifyTuple("[:]", tuple[0, -1], items);
			VerifyTuple("[:]", tuple[-6, -1], items);
			VerifyTuple("[:]", tuple[-6, 5], items);

			// tail
			VerifyTuple("[n:]", tuple[4, null], new object[] { 456, "bar" });
			VerifyTuple("[n:+]", tuple[4, 5], new object[] { 456, "bar" });
			VerifyTuple("[n:-]", tuple[4, -1], new object[] { 456, "bar" });
			VerifyTuple("[-n:+]", tuple[-2, 5], new object[] { 456, "bar" });
			VerifyTuple("[-n:-]", tuple[-2, -1], new object[] { 456, "bar" });

			// head
			VerifyTuple("[:n]", tuple[null, 2], new object[] { "hello", "world", 123 });
			VerifyTuple("[0:n]", tuple[0, 2], new object[] { "hello", "world", 123 });
			VerifyTuple("[0:-n]", tuple[0, -4], new object[] { "hello", "world", 123 });
			VerifyTuple("[-:n]", tuple[-6, 2], new object[] { "hello", "world", 123 });
			VerifyTuple("[-:-n]", tuple[-6, -4], new object[] { "hello", "world", 123 });

			// chunk
			VerifyTuple("[2:3]", tuple[2, 3], new object[] { 123, "foo" });
			VerifyTuple("[2:-3]", tuple[2, -3], new object[] { 123, "foo" });
			VerifyTuple("[-4:-3]", tuple[-4, 3], new object[] { 123, "foo" });
			VerifyTuple("[-4:-3]", tuple[-4, -3], new object[] { 123, "foo" });

			// remove first
			VerifyTuple("[1:]", tuple[1, null], new object[] { "world", 123, "foo", 456, "bar" });
			VerifyTuple("[1:+]", tuple[1, 5], new object[] { "world", 123, "foo", 456, "bar" });
			VerifyTuple("[1:-]", tuple[1, -1], new object[] { "world", 123, "foo", 456, "bar" });
			VerifyTuple("[-5:-]", tuple[1, -1], new object[] { "world", 123, "foo", 456, "bar" });

			// remove last
			VerifyTuple("[:4]", tuple[null, 4], new object[] { "hello", "world", 123, "foo", 456 });
			VerifyTuple("[:-2]", tuple[null, -2], new object[] { "hello", "world", 123, "foo", 456 });
			VerifyTuple("[0:4]", tuple[0, 4], new object[] { "hello", "world", 123, "foo", 456 });
			VerifyTuple("[0:-2]", tuple[0, -2], new object[] { "hello", "world", 123, "foo", 456 });

			// index out of range
			Assert.That(() => tuple[2, 6], Throws.InstanceOf<IndexOutOfRangeException>());
			Assert.That(() => tuple[2, 123456], Throws.InstanceOf<IndexOutOfRangeException>());

		}

		private static object[] GetRange(int from, int to, int count)
		{
			if (count == 0) return new object[0];

			if (from < 0) from += count;
			if (to < 0) to += count;

			if (to >= count) to = count - 1;
			var tmp = new object[to - from + 1];
			for (int i = 0; i < tmp.Length; i++) tmp[i] = new string((char) (65 + from + i), 1);
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
		public void Test_Fdb_Tuple_Serialize_Bytes()
		{
			Slice packed;

			packed = FdbTuple.Pack(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 });
			Assert.That(packed.ToString(), Is.EqualTo("<01><12>4Vx<9A><BC><DE><F0><00>"));
			packed = FdbTuple.Pack(new byte[] { 0x00, 0x42 });
			Assert.That(packed.ToString(), Is.EqualTo("<01><00><FF>B<00>"));
			packed = FdbTuple.Pack(new byte[] { 0x42, 0x00 });
			Assert.That(packed.ToString(), Is.EqualTo("<01>B<00><FF><00>"));
			packed = FdbTuple.Pack(new byte[] { 0x42, 0x00, 0x42 });
			Assert.That(packed.ToString(), Is.EqualTo("<01>B<00><FF>B<00>"));
			packed = FdbTuple.Pack(new byte[] { 0x42, 0x00, 0x00, 0x42 });
			Assert.That(packed.ToString(), Is.EqualTo("<01>B<00><FF><00><FF>B<00>"));
		}

		[Test]
		public void Test_Fdb_Tuple_Deserialize_Bytes()
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
			// Unicode strings are stored with prefix '02' followed by the utf8 bytes, and terminated by '00'. All occurence of '00' in the UTF8 bytes are escaped with '00 FF'

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
			// Guids are stored with prefix '03' followed by 16 bytes formatted according to RFC 4122

			// System.Guid are stored in Little-Endian, but RFC 4122's UUIDs are stored in Big Endian, so per convention we will swap them

			Slice packed;

			// note: new Guid(bytes from 0 to 15) => "03020100-0504-0706-0809-0a0b0c0d0e0f";
			packed = FdbTuple.Create(Guid.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")).ToSlice();
			Assert.That(packed.ToString(), Is.EqualTo("<03><00><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F>"));

			packed = FdbTuple.Create(Guid.Empty).ToSlice();
			Assert.That(packed.ToString(), Is.EqualTo("<03><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00>"));

		}

		[Test]
		public void Test_FdbTuple_Deserialize_Guids()
		{
			// Guids are stored with prefix '03' followed by 16 bytes
			// we also accept byte arrays (prefix '01') if they are of length 16

			IFdbTuple packed;

			packed = FdbTuple.Unpack(Slice.Unescape("<03><00><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F>"));
			Assert.That(packed.Get<Guid>(0), Is.EqualTo(Guid.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")));
			Assert.That(packed[0], Is.EqualTo(Guid.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")));

			packed = FdbTuple.Unpack(Slice.Unescape("<03><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00>"));
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
		public void Test_FdbTuple_Serialize_Uuids()
		{
			// UUIDs are stored with prefix '03' followed by 16 bytes formatted according to RFC 4122

			Slice packed;

			// note: new Guid(bytes from 0 to 15) => "03020100-0504-0706-0809-0a0b0c0d0e0f";
			packed = FdbTuple.Create(Uuid.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")).ToSlice();
			Assert.That(packed.ToString(), Is.EqualTo("<03><00><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F>"));

			packed = FdbTuple.Create(Uuid.Empty).ToSlice();
			Assert.That(packed.ToString(), Is.EqualTo("<03><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00>"));
		}

		[Test]
		public void Test_FdbTuple_Deserialize_Uuids()
		{
			// UUIDs are stored with prefix '03' followed by 16 bytes (the result of uuid.ToByteArray())
			// we also accept byte arrays (prefix '01') if they are of length 16

			IFdbTuple packed;

			// note: new Uuid(bytes from 0 to 15) => "00010203-0405-0607-0809-0a0b0c0d0e0f";
			packed = FdbTuple.Unpack(Slice.Unescape("<03><00><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F>"));
			Assert.That(packed.Get<Uuid>(0), Is.EqualTo(Uuid.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")));
			Assert.That(packed[0], Is.EqualTo(Uuid.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")));

			packed = FdbTuple.Unpack(Slice.Unescape("<03><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00>"));
			Assert.That(packed.Get<Uuid>(0), Is.EqualTo(Uuid.Empty));
			Assert.That(packed[0], Is.EqualTo(Uuid.Empty));

			// unicode string
			packed = FdbTuple.Unpack(Slice.Unescape("<02>00010203-0405-0607-0809-0a0b0c0d0e0f<00>"));
			Assert.That(packed.Get<Uuid>(0), Is.EqualTo(Uuid.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")));
			//note: t[0] returns a string, not a UUID

			// null maps to Uuid.Empty
			packed = FdbTuple.Unpack(Slice.Unescape("<00>"));
			Assert.That(packed.Get<Uuid>(0), Is.EqualTo(Uuid.Empty));
			//note: t[0] returns null, not a UUID

		}

		[Test]
		public void Test_FdbTuple_Serialize_Integers()
		{
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
			Slice slice;

			slice = Slice.Unescape("<14>");
			Assert.That(FdbTuplePackers.DeserializeBoxed(slice), Is.EqualTo(0));

			slice = Slice.Unescape("<15>{");
			Assert.That(FdbTuplePackers.DeserializeBoxed(slice), Is.EqualTo(123));

			slice = Slice.Unescape("<16><04><D2>");
			Assert.That(FdbTuplePackers.DeserializeBoxed(slice), Is.EqualTo(1234));

			slice = Slice.Unescape("<13><FE>");
			Assert.That(FdbTuplePackers.DeserializeBoxed(slice), Is.EqualTo(-1));

			slice = Slice.Unescape("<13><00>");
			Assert.That(FdbTuplePackers.DeserializeBoxed(slice), Is.EqualTo(-255));

			slice = Slice.Unescape("<12><FE><FF>");
			Assert.That(FdbTuplePackers.DeserializeBoxed(slice), Is.EqualTo(-256));

			slice = Slice.Unescape("<12><00><00>");
			Assert.That(FdbTuplePackers.DeserializeBoxed(slice), Is.EqualTo(-65535));

			slice = Slice.Unescape("<11><FE><FF><FF>");
			Assert.That(FdbTuplePackers.DeserializeBoxed(slice), Is.EqualTo(-65536));

			slice = Slice.Unescape("<18><7F><FF><FF><FF>");
			Assert.That(FdbTuplePackers.DeserializeBoxed(slice), Is.EqualTo(int.MaxValue));

			slice = Slice.Unescape("<10><7F><FF><FF><FF>");
			Assert.That(FdbTuplePackers.DeserializeBoxed(slice), Is.EqualTo(int.MinValue));

			slice = Slice.Unescape("<1C><7F><FF><FF><FF><FF><FF><FF><FF>");
			Assert.That(FdbTuplePackers.DeserializeBoxed(slice), Is.EqualTo(long.MaxValue));

			slice = Slice.Unescape("<0C><7F><FF><FF><FF><FF><FF><FF><FF>");
			Assert.That(FdbTuplePackers.DeserializeBoxed(slice), Is.EqualTo(long.MinValue));
		}

		[Test]
		public void Test_FdbTuple_Serialize_Negative_Integers()
		{

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
		public void Test_FdbTuple_Serialize_Booleans()
		{
			// False is 0, True is 1

			Slice packed;

			// bool
			packed = FdbTuple.Pack(false);
			Assert.That(packed.ToString(), Is.EqualTo("<14>"));
			packed = FdbTuple.Pack(true);
			Assert.That(packed.ToString(), Is.EqualTo("<15><01>"));

			// bool?
			packed = FdbTuple.Pack(default(bool?));
			Assert.That(packed.ToString(), Is.EqualTo("<00>"));
			packed = FdbTuple.Pack((bool?)false);
			Assert.That(packed.ToString(), Is.EqualTo("<14>"));
			packed = FdbTuple.Pack((bool?)true);
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
			Assert.That(FdbTuple.UnpackSingle<bool>(Slice.Unescape("<00>")), Is.EqualTo(false), "Null => False");
			Assert.That(FdbTuple.UnpackSingle<bool>(Slice.Unescape("<14>")), Is.EqualTo(false), "0 => False");
			Assert.That(FdbTuple.UnpackSingle<bool>(Slice.Unescape("<01><00>")), Is.EqualTo(false), "byte[0] => False");
			Assert.That(FdbTuple.UnpackSingle<bool>(Slice.Unescape("<02><00>")), Is.EqualTo(false), "String.Empty => False");

			// Truthy
			Assert.That(FdbTuple.UnpackSingle<bool>(Slice.Unescape("<15><01>")), Is.EqualTo(true), "1 => True");
			Assert.That(FdbTuple.UnpackSingle<bool>(Slice.Unescape("<13><FE>")), Is.EqualTo(true), "-1 => True");
			Assert.That(FdbTuple.UnpackSingle<bool>(Slice.Unescape("<01>Hello<00>")), Is.EqualTo(true), "'Hello' => True");
			Assert.That(FdbTuple.UnpackSingle<bool>(Slice.Unescape("<02>Hello<00>")), Is.EqualTo(true), "\"Hello\" => True");
			Assert.That(FdbTuple.UnpackSingle<bool>(FdbTuple.Pack(123456789)), Is.EqualTo(true), "random int => True");

			Assert.That(FdbTuple.UnpackSingle<bool>(Slice.Unescape("<02>True<00>")), Is.EqualTo(true), "\"True\" => True");
			Assert.That(FdbTuple.UnpackSingle<bool>(Slice.Unescape("<02>False<00>")), Is.EqualTo(true), "\"False\" => True ***");
			// note: even though it would be tempting to convert the string "false" to False, it is not a standard behavior accross all bindings

			// When decoded to object, though, they should return 0 and 1
			Assert.That(FdbTuplePackers.DeserializeBoxed(FdbTuple.Pack(false)), Is.EqualTo(0));
			Assert.That(FdbTuplePackers.DeserializeBoxed(FdbTuple.Pack(true)), Is.EqualTo(1));
		}

		[Test]
		public void Test_FdbTuple_Serialize_Alias()
		{
			Assert.That(
				FdbTuple.Pack(FdbTupleAlias.System).ToString(),
				Is.EqualTo("<FF>")
			);

			Assert.That(
				FdbTuple.Pack(FdbTupleAlias.Directory).ToString(),
				Is.EqualTo("<FE>")
			);

			Assert.That(
				FdbTuple.Pack(FdbTupleAlias.Zero).ToString(),
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
				FdbTuple.CreateRange(new object[] { "hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 } }, 1, 2).ToSlice().ToString(),
				Is.EqualTo("<15>{<14>")
			);

			Assert.That(
				FdbTuple.CreateRange(new List<object> { "hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 } }).ToSlice().ToString(),
				Is.EqualTo("<02>hello world<00><15>{<14><01>{<01>B<00><FF>*<00>")
			);

		}

		[Test]
		public void Test_FdbTuple_Pack()
		{
			Assert.That(
				FdbTuple.Pack("hello world").ToString(),
				Is.EqualTo("<02>hello world<00>")
			);

			Assert.That(
				FdbTuple.Pack("hello", "world").ToString(),
				Is.EqualTo("<02>hello<00><02>world<00>")
			);

			Assert.That(
				FdbTuple.Pack("hello world", 123).ToString(),
				Is.EqualTo("<02>hello world<00><15>{")
			);

			Assert.That(
				FdbTuple.Pack("hello world", 1234, -1234).ToString(),
				Is.EqualTo("<02>hello world<00><16><04><D2><12><FB>-")
			);

			Assert.That(
				FdbTuple.Pack("hello world", 123, false).ToString(),
				Is.EqualTo("<02>hello world<00><15>{<14>")
			);

			Assert.That(
				FdbTuple.Pack("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }).ToString(),
				Is.EqualTo("<02>hello world<00><15>{<14><01>{<01>B<00><FF>*<00>")
			);
		}

		[Test]
		public void Test_FdbTuple_Unpack()
		{

			var packed = FdbTuple.Create("hello world").ToSlice();
			Console.WriteLine(packed);

			var tuple = FdbTuple.Unpack(packed);
			Assert.That(tuple, Is.Not.Null);
			Console.WriteLine(tuple);
			Assert.That(tuple.Count, Is.EqualTo(1));
			Assert.That(tuple.Get<string>(0), Is.EqualTo("hello world"));

			packed = FdbTuple.Create("hello world", 123).ToSlice();
			Console.WriteLine(packed);

			tuple = FdbTuple.Unpack(packed);
			Assert.That(tuple, Is.Not.Null);
			Console.WriteLine(tuple);
			Assert.That(tuple.Count, Is.EqualTo(2));
			Assert.That(tuple.Get<string>(0), Is.EqualTo("hello world"));
			Assert.That(tuple.Get<int>(1), Is.EqualTo(123));

			packed = FdbTuple.Create(1, 256, 257, 65536, int.MaxValue, long.MaxValue).ToSlice();
			Console.WriteLine(packed);

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
			Console.WriteLine(packed);

			tuple = FdbTuple.Unpack(packed);
			Assert.That(tuple, Is.Not.Null);
			Assert.That(tuple, Is.InstanceOf<FdbSlicedTuple>());
			Console.WriteLine(tuple);
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
		public void Test_FdbTuple_PackBoxed()
		{
			Slice slice;

			slice = FdbTuple.PackBoxed(default(object));
			Assert.That(slice.ToString(), Is.EqualTo("<00>"));

			slice = FdbTuple.PackBoxed((object)1);
			Assert.That(slice.ToString(), Is.EqualTo("<15><01>"));

			slice = FdbTuple.PackBoxed((object)1L);
			Assert.That(slice.ToString(), Is.EqualTo("<15><01>"));

			slice = FdbTuple.PackBoxed((object)1U);
			Assert.That(slice.ToString(), Is.EqualTo("<15><01>"));

			slice = FdbTuple.PackBoxed((object)1UL);
			Assert.That(slice.ToString(), Is.EqualTo("<15><01>"));

			slice = FdbTuple.PackBoxed((object)false);
			Assert.That(slice.ToString(), Is.EqualTo("<14>"));

			slice = FdbTuple.PackBoxed((object)new byte[] { 4, 5, 6 });
			Assert.That(slice.ToString(), Is.EqualTo("<01><04><05><06><00>"));

			slice = FdbTuple.PackBoxed((object)"hello");
			Assert.That(slice.ToString(), Is.EqualTo("<02>hello<00>"));
		}

		[Test]
		public void Test_FdbTuple_Pack_Boxed_Values()
		{
			Slice slice;

			slice = FdbTuple.Pack<object>(default(object));
			Assert.That(slice.ToString(), Is.EqualTo("<00>"));

			slice = FdbTuple.Pack<object>(1);
			Assert.That(slice.ToString(), Is.EqualTo("<15><01>"));

			slice = FdbTuple.Pack<object>(1L);
			Assert.That(slice.ToString(), Is.EqualTo("<15><01>"));

			slice = FdbTuple.Pack<object>(1U);
			Assert.That(slice.ToString(), Is.EqualTo("<15><01>"));

			slice = FdbTuple.Pack<object>(1UL);
			Assert.That(slice.ToString(), Is.EqualTo("<15><01>"));

			slice = FdbTuple.Pack<object>(false);
			Assert.That(slice.ToString(), Is.EqualTo("<14>"));

			slice = FdbTuple.Pack<object>(new byte[] { 4, 5, 6 });
			Assert.That(slice.ToString(), Is.EqualTo("<01><04><05><06><00>"));

			slice = FdbTuple.Pack<object>("hello");
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
			Console.WriteLine("Checked " + N.ToString("N0") + " tuples in " + sw.ElapsedMilliseconds + " ms");

		}

		[Test]
		public void Test_FdbTuple_Serialize_ITupleFormattable()
		{
			// types that implement ITupleFormattable should be packed by calling ToTuple() and then packing the returned tuple

			Slice packed;

			packed = FdbTuplePacker<Thing>.Serialize(new Thing { Foo = 123, Bar = "hello" });
			Assert.That(packed.ToString(), Is.EqualTo("<15>{<02>hello<00>"));

			packed = FdbTuplePacker<Thing>.Serialize(new Thing());
			Assert.That(packed.ToString(), Is.EqualTo("<14><00>"));
		}

		[Test]
		public void Test_FdbTuple_Deserialize_ITupleFormattable()
		{
			Slice slice;
			Thing thing;

			slice = Slice.Unescape("<16><01><C8><02>world<00>");
			thing = FdbTuplePackers.DeserializeFormattable<Thing>(slice);
			Assert.That(thing, Is.Not.Null);
			Assert.That(thing.Foo, Is.EqualTo(456));
			Assert.That(thing.Bar, Is.EqualTo("world"));

			slice = Slice.Unescape("<14><00>");
			thing = FdbTuplePackers.DeserializeFormattable<Thing>(slice);
			Assert.That(thing, Is.Not.Null);
			Assert.That(thing.Foo, Is.EqualTo(0));
			Assert.That(thing.Bar, Is.EqualTo(null));

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
			slices = FdbTuple.PackRange(tuples);
			Assert.That(slices, Is.Not.Null);
			Assert.That(slices.Length, Is.EqualTo(tuples.Length));
			Assert.That(slices, Is.EqualTo(tuples.Select(t => t.ToSlice()).ToArray()));

			// IEnumerable version that is passed an array
			slices = FdbTuple.PackRange((IEnumerable<IFdbTuple>)tuples);
			Assert.That(slices, Is.Not.Null);
			Assert.That(slices.Length, Is.EqualTo(tuples.Length));
			Assert.That(slices, Is.EqualTo(tuples.Select(t => t.ToSlice()).ToArray()));

			// IEnumerable version but with a "real" enumerable 
			slices = FdbTuple.PackRange(tuples.Select(t => t));
			Assert.That(slices, Is.Not.Null);
			Assert.That(slices.Length, Is.EqualTo(tuples.Length));
			Assert.That(slices, Is.EqualTo(tuples.Select(t => t.ToSlice()).ToArray()));
		}

		[Test]
		public void Test_FdbTuple_PackRange_Of_T()
		{
			Slice[] slices;

			#region PackRange(Tuple, ...)

			var tuple = FdbTuple.Create("hello");
			int[] items = new int[] { 1, 2, 3, 123, -1, int.MaxValue };

			// array version
			slices = FdbTuple.PackRange<int>(tuple, items);
			Assert.That(slices, Is.Not.Null);
			Assert.That(slices.Length, Is.EqualTo(items.Length));
			Assert.That(slices, Is.EqualTo(items.Select(x => tuple.Append(x).ToSlice()).ToArray()));

			// IEnumerable version that is passed an array
			slices = FdbTuple.PackRange<int>(tuple, (IEnumerable<int>)items);
			Assert.That(slices, Is.Not.Null);
			Assert.That(slices.Length, Is.EqualTo(items.Length));
			Assert.That(slices, Is.EqualTo(items.Select(x => tuple.Append(x).ToSlice()).ToArray()));

			// IEnumerable version but with a "real" enumerable 
			slices = FdbTuple.PackRange<int>(tuple, items.Select(t => t));
			Assert.That(slices, Is.Not.Null);
			Assert.That(slices.Length, Is.EqualTo(items.Length));
			Assert.That(slices, Is.EqualTo(items.Select(x => tuple.Append(x).ToSlice()).ToArray()));

			#endregion

			#region PackRange(Slice, ...)

			string[] words = new string[] { "hello", "world", "très bien", "断トツ", "abc\0def", null, String.Empty };

			var merged = FdbTuple.PackRange(Slice.FromByte(42), words);
			Assert.That(merged, Is.Not.Null);
			Assert.That(merged.Length, Is.EqualTo(words.Length));

			for (int i = 0; i < words.Length; i++)
			{
				var expected = Slice.FromByte(42) + FdbTuple.Pack(words[i]);
				Assert.That(merged[i], Is.EqualTo(expected));

				Assert.That(merged[i].Array, Is.SameAs(merged[0].Array), "All slices should be stored in the same buffer");
				if (i > 0) Assert.That(merged[i].Offset, Is.EqualTo(merged[i - 1].Offset + merged[i - 1].Count), "All slices should be contiguous");
			}

			// corner cases
			Assert.That(() => FdbTuple.PackRange<int>(Slice.Empty, default(int[])), Throws.InstanceOf<ArgumentNullException>().With.Property("ParamName").EqualTo("keys"));
			Assert.That(() => FdbTuple.PackRange<int>(Slice.Empty, default(IEnumerable<int>)), Throws.InstanceOf<ArgumentNullException>().With.Property("ParamName").EqualTo("keys"));

			#endregion
		}

		[Test]
		public void Test_FdbTuple_PackBoxedRange()
		{
			Slice[] slices;
			var tuple = FdbTuple.Create("hello");
			object[] items = new object[] { "world", 123, false, Guid.NewGuid(), long.MinValue };

			// array version
			slices = FdbTuple.PackBoxedRange(tuple, items);
			Assert.That(slices, Is.Not.Null);
			Assert.That(slices.Length, Is.EqualTo(items.Length));
			Assert.That(slices, Is.EqualTo(items.Select(x => tuple.Append(x).ToSlice()).ToArray()));

			// IEnumerable version that is passed an array
			slices = FdbTuple.PackBoxedRange(tuple, (IEnumerable<object>)items);
			Assert.That(slices, Is.Not.Null);
			Assert.That(slices.Length, Is.EqualTo(items.Length));
			Assert.That(slices, Is.EqualTo(items.Select(x => tuple.Append(x).ToSlice()).ToArray()));

			// IEnumerable version but with a "real" enumerable 
			slices = FdbTuple.PackBoxedRange(tuple, items.Select(t => t));
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

		private static void PerformWriterTest<T>(Action<FdbBufferWriter, T> action, T value, string expectedResult, string message = null)
		{
			var writer = new FdbBufferWriter();
			action(writer, value);

			Assert.That(writer.ToSlice().ToHexaString(' '), Is.EqualTo(expectedResult), message != null ? "Value {0} ({1}) was not properly packed: {2}" : "Value {0} ({1}) was not properly packed", value == null ? "<null>" : value is string ? Clean(value as string) : value.ToString(), (value == null ? "null" : value.GetType().Name), message);
		}

		[Test]
		public void Test_Tuple_WriteInt64()
		{
			Action<FdbBufferWriter, long> test = (writer, value) => FdbTupleParser.WriteInt64(writer, value);

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
		public void Test_Tuple_WriteInt64_Ordered()
		{
			var list = new List<KeyValuePair<long, Slice>>();

			Action<long> test = (x) =>
			{
				var writer = new FdbBufferWriter();
				FdbTupleParser.WriteInt64(writer, x);
				var res = new KeyValuePair<long, Slice>(x, writer.ToSlice());
				list.Add(res);
				Console.WriteLine("{0,20} : {0:x16} {1}", res.Key, res.Value.ToString());
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
		public void Test_Tuple_WriteUInt64()
		{
			Action<FdbBufferWriter, ulong> test = (writer, value) => FdbTupleParser.WriteUInt64(writer, value);

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
		public void Test_Tuple_WriteUInt64_Ordered()
		{
			var list = new List<KeyValuePair<ulong, Slice>>();

			Action<ulong> test = (x) =>
			{
				var writer = new FdbBufferWriter();
				FdbTupleParser.WriteUInt64(writer, x);
				var res = new KeyValuePair<ulong, Slice>(x, writer.ToSlice());
				list.Add(res);
#if DEBUG
				Console.WriteLine("{0,20} : {0:x16} {1}", res.Key, res.Value.ToString());
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
		public void Test_Tuple_WriteAsciiString()
		{
			Action<FdbBufferWriter, string> test = (writer, value) => FdbTupleParser.WriteAsciiString(writer, value);

			PerformWriterTest(test, null, "00");
			PerformWriterTest(test, String.Empty, "01 00");
			PerformWriterTest(test, "A", "01 41 00");
			PerformWriterTest(test, "ABC", "01 41 42 43 00");

			// Must escape '\0' contained in the string as '\x00\xFF'
			PerformWriterTest(test, "\0", "01 00 FF 00");
			PerformWriterTest(test, "A\0", "01 41 00 FF 00");
			PerformWriterTest(test, "\0A", "01 00 FF 41 00");
			PerformWriterTest(test, "A\0\0A", "01 41 00 FF 00 FF 41 00");
			PerformWriterTest(test, "A\0B\0\xFF", "01 41 00 FF 42 00 FF FF 00");
		}

		[Test]
		public void Test_Write_TupleUnicodeString()
		{
			string s;
			Action<FdbBufferWriter, string> test = (writer, value) => FdbTupleParser.WriteString(writer, value);
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

			Console.Write("Creating " + N.ToString("N0") + " random tuples...");
			var tuples = new List<IFdbTuple>(N);
			var rnd = new Random(777);
			var sw = Stopwatch.StartNew();
			for (int i = 0; i < N; i++)
			{
				IFdbTuple tuple = FdbTuple.Empty;
				int s = 1 + (int)Math.Sqrt(rnd.Next(128));
				for (int j = 0; j < s; j++)
				{
					switch (rnd.Next(16))
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
						case 10: tuple = tuple.Append(Guid.NewGuid()); break;
						case 11: tuple = tuple.Append(Uuid.NewUuid()); break;
						case 12: { var buf = new byte[1 + (int)Math.Sqrt(rnd.Next(1024))]; rnd.NextBytes(buf); tuple = tuple.Append(buf); break; }
						case 13: tuple = tuple.Append(default(string)); break;
						case 14: tuple = tuple.Append<object>("hello"); break;
						case 15: tuple = tuple.Append<bool>(rnd.Next(2) == 0); break;
					}
				}
				tuples.Add(tuple);
			}
			sw.Stop();
			Console.WriteLine(" done in " + sw.Elapsed.TotalSeconds + " sec");
			Console.WriteLine(" > " + tuples.Sum(x => x.Count).ToString("N0") + " items");
			Console.WriteLine(" > "  + tuples[42]);
			Console.WriteLine();

			Console.Write("Packing tuples...");
			sw.Restart();
			var slices = FdbTuple.PackRange(tuples);
			sw.Stop();
			Console.WriteLine(" done in " + sw.Elapsed.TotalSeconds + " sec");
			Console.WriteLine(" > " + (N / sw.Elapsed.TotalSeconds).ToString("N0") + " tps");
			Console.WriteLine(" > " + slices.Sum(x => x.Count).ToString("N0") + " bytes");
			Console.WriteLine(" > " + slices[42]);
			Console.WriteLine();

			Console.Write("Unpacking tuples...");
			sw.Restart();
			var unpacked = slices.Select(slice => FdbTuple.Unpack(slice)).ToList();
			sw.Stop();
			Console.WriteLine(" done in " + sw.Elapsed.TotalSeconds + " sec");
			Console.WriteLine(" > " + (N / sw.Elapsed.TotalSeconds).ToString("N0") + " tps");
			Console.WriteLine(" > " + unpacked[42]);
			Console.WriteLine();

			Console.Write("Comparing ...");
			sw.Restart();
			tuples.Zip(unpacked, (x, y) => x.Equals(y)).All(b => b);
			sw.Stop();
			Console.WriteLine(" done in " + sw.Elapsed.TotalSeconds + " sec");
			Console.WriteLine();

			Console.Write("Tuples.ToString ...");
			sw.Restart();
			var strings = tuples.Select(x => x.ToString()).ToList();
			sw.Stop();
			Console.WriteLine(" done in " + sw.Elapsed.TotalSeconds + " sec");
			Console.WriteLine(" > " + strings.Sum(x => x.Length).ToString("N0") + " chars");
			Console.WriteLine(" > " + strings[42]);
			Console.WriteLine();

			Console.Write("Unpacked.ToString ...");
			sw.Restart();
			strings = unpacked.Select(x => x.ToString()).ToList();
			sw.Stop();
			Console.WriteLine(" done in " + sw.Elapsed.TotalSeconds + " sec");
			Console.WriteLine(" > " + strings.Sum(x => x.Length).ToString("N0") + " chars");
			Console.WriteLine(" > " + strings[42]);
			Console.WriteLine();

			Console.Write("Memoizing ...");
			sw.Restart();
			var memoized = tuples.Select(x => x.Memoize()).ToList();
			sw.Stop();
			Console.WriteLine(" done in " + sw.Elapsed.TotalSeconds + " sec");
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
