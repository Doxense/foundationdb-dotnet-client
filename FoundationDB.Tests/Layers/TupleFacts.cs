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

			IFdbTuple t = FdbTuple.Empty;
			Assert.That(() => t.Last<string>(), Throws.InstanceOf<IndexOutOfRangeException>());

			t = null;
			Assert.That(() => t.Last<string>(), Throws.InstanceOf<ArgumentNullException>());
		}

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

			var tuples = new IFdbTuple[11];
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
			// Guids are stored with prefix '03' followed by 16 bytes (the result of guid.GetBytes())

			Slice packed;

			// note: new Guid(bytes from 0 to 15) => "03020100-0504-0706-0809-0a0b0c0d0e0f";
			packed = FdbTuple.Create(Guid.Parse("03020100-0504-0706-0809-0a0b0c0d0e0f")).ToSlice();
			Assert.That(packed.ToString(), Is.EqualTo("<03><00><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F>"));

			packed = FdbTuple.Create(Guid.Empty).ToSlice();
			Assert.That(packed.ToString(), Is.EqualTo("<03><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00>"));

		}

		[Test]
		public void Test_FdbTuple_Deserialize_Guids()
		{
			// Guids are stored with prefix '03' followed by 16 bytes (the result of guid.GetBytes())
			// we also accept byte arrays (prefix '01') if they are of length 16

			IFdbTuple packed;

			// note: new Guid(bytes from 0 to 15) => "03020100-0504-0706-0809-0a0b0c0d0e0f";
			packed = FdbTuple.Unpack(Slice.Unescape("<03><00><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F>"));
			Assert.That(packed.Get<Guid>(0), Is.EqualTo(Guid.Parse("03020100-0504-0706-0809-0a0b0c0d0e0f")));
			Assert.That(packed[0], Is.EqualTo(Guid.Parse("03020100-0504-0706-0809-0a0b0c0d0e0f")));

			packed = FdbTuple.Unpack(Slice.Unescape("<03><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00>"));
			Assert.That(packed.Get<Guid>(0), Is.EqualTo(Guid.Empty));
			Assert.That(packed[0], Is.EqualTo(Guid.Empty));

			// unicode string
			packed = FdbTuple.Unpack(Slice.Unescape("<02>03020100-0504-0706-0809-0a0b0c0d0e0f<00>"));
			Assert.That(packed.Get<Guid>(0), Is.EqualTo(Guid.Parse("03020100-0504-0706-0809-0a0b0c0d0e0f")));
			//note: t[0] returns a string, not a GUID

#if DOES_NOT_WORK
			// byte array (note: 00 are escaped !)
			packed = FdbTuple.Unpack(Slice.Unescape("<01><00><FF><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F><00>"));
			Assert.That(packed.Get<Guid>(0), Is.EqualTo(Guid.Parse("03020100-0504-0706-0809-0a0b0c0d0e0f")));
			//note: t[0] returns a string, not a GUID
#endif

			// null maps to Guid.Empty
			packed = FdbTuple.Unpack(Slice.Unescape("<00>"));
			Assert.That(packed.Get<Guid>(0), Is.EqualTo(Guid.Empty));
			//note: t[0] returns null, not a GUID

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
			Assert.That(FdbTuplePackers.DeserializeObject(slice), Is.EqualTo(0));

			slice = Slice.Unescape("<15>{");
			Assert.That(FdbTuplePackers.DeserializeObject(slice), Is.EqualTo(123));

			slice = Slice.Unescape("<16><04><D2>");
			Assert.That(FdbTuplePackers.DeserializeObject(slice), Is.EqualTo(1234));

			slice = Slice.Unescape("<13><FE>");
			Assert.That(FdbTuplePackers.DeserializeObject(slice), Is.EqualTo(-1));

			slice = Slice.Unescape("<13><00>");
			Assert.That(FdbTuplePackers.DeserializeObject(slice), Is.EqualTo(-255));

			slice = Slice.Unescape("<12><FE><FF>");
			Assert.That(FdbTuplePackers.DeserializeObject(slice), Is.EqualTo(-256));

			slice = Slice.Unescape("<12><00><00>");
			Assert.That(FdbTuplePackers.DeserializeObject(slice), Is.EqualTo(-65535));

			slice = Slice.Unescape("<11><FE><FF><FF>");
			Assert.That(FdbTuplePackers.DeserializeObject(slice), Is.EqualTo(-65536));

			slice = Slice.Unescape("<18><7F><FF><FF><FF>");
			Assert.That(FdbTuplePackers.DeserializeObject(slice), Is.EqualTo(int.MaxValue));

			slice = Slice.Unescape("<10><7F><FF><FF><FF>");
			Assert.That(FdbTuplePackers.DeserializeObject(slice), Is.EqualTo(int.MinValue));

			slice = Slice.Unescape("<1C><7F><FF><FF><FF><FF><FF><FF><FF>");
			Assert.That(FdbTuplePackers.DeserializeObject(slice), Is.EqualTo(long.MaxValue));

			slice = Slice.Unescape("<0C><7F><FF><FF><FF><FF><FF><FF><FF>");
			Assert.That(FdbTuplePackers.DeserializeObject(slice), Is.EqualTo(long.MinValue));
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
		public void Test_FdbTuple_Pack()
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

			Assert.That(writer.ToSlice().ToHexaString(' '), Is.EqualTo(expectedResult), "Value {0} ({1}) was not properly packed", value == null ? "<null>" : value is string ? Clean(value as string) : value.ToString(), (value == null ? "null" : value.GetType().Name));
		}

		[Test]
		public void Test_Write_TupleInt64()
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
		public void Test_Write_TupleInt64_Ordered()
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
		public void Test_Write_TupleUInt64()
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
		public void Test_Write_TupleUInt64_Ordered()
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
		public void Test_Write_TupleAsciiString()
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

		#endregion

		#region Bench....

		[Test]
		public void Bench_FdbTuple_Unpack_Random()
		{
			const int N = 100 * 1000;

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
					switch (rnd.Next(10))
					{
						case 0: tuple = tuple.Append<int>(rnd.Next(255)); break;
						case 1: tuple = tuple.Append<int>(-1 - rnd.Next(255)); break;
						case 2: tuple = tuple.Append<int>(256 + rnd.Next(65536 - 256)); break;
						case 3: tuple = tuple.Append<int>(rnd.Next(int.MaxValue)); break;
						case 4: tuple = tuple.Append<long>((rnd.Next(int.MaxValue) << 32) | rnd.Next(int.MaxValue)); break;
						case 5: tuple = tuple.Append(new string('A', 1 + rnd.Next(16))); break;
						case 6: tuple = tuple.Append(new string('B', 8 + (int)Math.Sqrt(rnd.Next(1024)))); break;
						case 7: tuple = tuple.Append(Guid.NewGuid()); break;
						case 8: { var buf = new byte[rnd.Next((int)Math.Sqrt(256))]; rnd.NextBytes(buf); tuple = tuple.Append(buf); break; }
						case 9: tuple = tuple.Append(default(string)); break;
					}
				}
				tuples.Add(tuple);
			}
			sw.Stop();
			Console.WriteLine(" done in " + sw.Elapsed.TotalSeconds);
			Console.WriteLine(" > " + tuples.Sum(x => x.Count).ToString("N0") + " items");
			Console.WriteLine(" > "  + tuples[42]);

			Console.Write("Packing tuples...");
			sw.Restart();
			var slices = FdbTuple.BatchPack(tuples);
			sw.Stop();
			Console.WriteLine(" done in " + sw.Elapsed.TotalSeconds);
			Console.WriteLine(" > " + (N / sw.Elapsed.TotalSeconds).ToString("N0") + " tps");
			Console.WriteLine(" > " + slices.Sum(x => x.Count).ToString("N0") + " bytes");
			Console.WriteLine(" > " + slices[42]);

			Console.Write("Unpacking tuples...");
			sw.Restart();
			var unpacked = slices.Select(slice => FdbTuple.Unpack(slice)).ToList();
			sw.Stop();
			Console.WriteLine(" done in " + sw.Elapsed.TotalSeconds);
			Console.WriteLine(" > " + (N / sw.Elapsed.TotalSeconds).ToString("N0") + " tps");
			Console.WriteLine(" > " + unpacked[42]);

			Console.Write("Comparing ...");
			sw.Restart();
			tuples.Zip(unpacked, (x, y) => x.Equals(y)).All(b => b);
			sw.Stop();
			Console.WriteLine(" done in " + sw.Elapsed.TotalSeconds);

			Console.Write("Tuples.ToString ...");
			sw.Restart();
			var strings = tuples.Select(x => x.ToString()).ToList();
			sw.Stop();
			Console.WriteLine(" done in " + sw.Elapsed.TotalSeconds);
			Console.WriteLine(" > " + strings.Sum(x => x.Length).ToString("N0") + " chars");
			Console.WriteLine(" > " + strings[42]);

			Console.Write("Unpacked.ToString ...");
			sw.Restart();
			strings = unpacked.Select(x => x.ToString()).ToList();
			sw.Stop();
			Console.WriteLine(" done in " + sw.Elapsed.TotalSeconds);
			Console.WriteLine(" > " + strings.Sum(x => x.Length).ToString("N0") + " chars");
			Console.WriteLine(" > " + strings[42]);

			Console.Write("Memoizing ...");
			sw.Restart();
			var memoized = tuples.Select(x => x.Memoize()).ToList();
			sw.Stop();
			Console.WriteLine(" done in " + sw.Elapsed.TotalSeconds);
		}

		#endregion
	}


}
