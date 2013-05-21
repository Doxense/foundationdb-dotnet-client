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
	* Neither the name of the <organization> nor the
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

using FoundationDb.Client;
using FoundationDb.Client.Tuples;
using NUnit.Framework;
using System;

namespace FoundationDb.Tests
{

	[TestFixture]
	public class TupleFacts
	{

		[Test]
		public void Test_FdbTuple_Create()
		{
			var t1 = FdbTuple.Create("hello world");
			Assert.That(t1.Count, Is.EqualTo(1));
			Assert.That(t1.Item1, Is.EqualTo("hello world"));
			Assert.That(t1[0], Is.EqualTo("hello world"));

			var t2 = FdbTuple.Create("hello world", 123);
			Assert.That(t2.Count, Is.EqualTo(2));
			Assert.That(t2.Item1, Is.EqualTo("hello world"));
			Assert.That(t2.Item2, Is.EqualTo(123));
			Assert.That(t2[0], Is.EqualTo("hello world"));
			Assert.That(t2[1], Is.EqualTo(123));

			var t3 = FdbTuple.Create("hello world", 123, false);
			Assert.That(t3.Count, Is.EqualTo(3));
			Assert.That(t3.Item1, Is.EqualTo("hello world"));
			Assert.That(t3.Item2, Is.EqualTo(123));
			Assert.That(t3.Item3, Is.EqualTo(false));
			Assert.That(t3[0], Is.EqualTo("hello world"));
			Assert.That(t3[1], Is.EqualTo(123));
			Assert.That(t3[2], Is.EqualTo(false));

			var tn = FdbTuple.Create(new object[] { "hello world", 123, false, 1234, -1234, "six" });
			Assert.That(tn.Count, Is.EqualTo(6));
			Assert.That(tn[0], Is.EqualTo("hello world"));
			Assert.That(tn[1], Is.EqualTo(123));
			Assert.That(tn[2], Is.EqualTo(false));
			Assert.That(tn[3], Is.EqualTo(1234));
			Assert.That(tn[4], Is.EqualTo(-1234));
			Assert.That(tn[5], Is.EqualTo("six"));
		}

		[Test]
		public void Test_FdbTuple_Negative_Indexing()
		{
			var t1 = FdbTuple.Create("hello world");
			Assert.That(t1[-1], Is.EqualTo("hello world"));

			var t2 = FdbTuple.Create("hello world", 123);
			Assert.That(t2[-1], Is.EqualTo(123));
			Assert.That(t2[-2], Is.EqualTo("hello world"));

			var t3 = FdbTuple.Create("hello world", 123, false);
			Assert.That(t3[-1], Is.EqualTo(false));
			Assert.That(t3[-2], Is.EqualTo(123));
			Assert.That(t3[-3], Is.EqualTo("hello world"));

			var tn = FdbTuple.Create(new object[] { "hello world", 123, false, 1234, -1234, "six" });
			Assert.That(tn[-1], Is.EqualTo("six"));
			Assert.That(tn[-2], Is.EqualTo(-1234));
			Assert.That(tn[-3], Is.EqualTo(1234));
			Assert.That(tn[-4], Is.EqualTo(false));
			Assert.That(tn[-5], Is.EqualTo(123));
			Assert.That(tn[-6], Is.EqualTo("hello world"));
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
				FdbTuple.Create(int.MaxValue).ToSlice().ToString(),
				Is.EqualTo("<18><7F><FF><FF><FF>")
			);

			Assert.That(
				FdbTuple.Create(int.MinValue).ToSlice().ToString(),
				Is.EqualTo("<10><7F><FF><FF><FF>")
			);

			Assert.That(
				FdbTuple.Create(long.MaxValue).ToSlice().ToString(),
				Is.EqualTo("<1C><7F><FF><FF><FF><FF><FF><FF><FF>")
			);

			Assert.That(
				FdbTuple.Create(long.MinValue).ToSlice().ToString(),
				Is.EqualTo("<0C><7F><FF><FF><FF><FF><FF><FF><FF>")
			);

		}

		[Test]
		public void Test_FdbTuple_Deserialize()
		{
			var slice = Slice.Unescape("<02>hello world<00>");
			Assert.That(FdbTuplePackers.DeserializeObject(slice), Is.EqualTo("hello world"));

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
		public void Test_FdbTuple_Unpack()
		{

			var packed = FdbTuple.Create("hello world").ToSlice();
			Console.WriteLine(packed);

			var tuple = FdbTuple.Unpack(packed);
			Assert.That(tuple, Is.Not.Null);
			Console.WriteLine(tuple);
			Assert.That(tuple.Count, Is.EqualTo(1));
			Assert.That(tuple[0], Is.EqualTo("hello world"));

			packed = FdbTuple.Create("hello world", 123).ToSlice();
			Console.WriteLine(packed);

			tuple = FdbTuple.Unpack(packed);
			Assert.That(tuple, Is.Not.Null);
			Console.WriteLine(tuple);
			Assert.That(tuple.Count, Is.EqualTo(2));
			Assert.That(tuple[0], Is.EqualTo("hello world"));
			Assert.That(tuple[1], Is.EqualTo(123));

			packed = FdbTuple.Create(1, 256, 257, 65536, int.MaxValue, long.MaxValue).ToSlice();
			Console.WriteLine(packed);

			tuple = FdbTuple.Unpack(packed);
			Assert.That(tuple, Is.Not.Null);
			Assert.That(tuple.Count, Is.EqualTo(6));
			Assert.That(tuple[0], Is.EqualTo(1));
			Assert.That(tuple[1], Is.EqualTo(256));
			Assert.That(tuple[2], Is.EqualTo(257), ((FdbSlicedTuple)tuple).GetSlice(2).ToString());
			Assert.That(tuple[3], Is.EqualTo(65536));
			Assert.That(tuple[4], Is.EqualTo(int.MaxValue));
			Assert.That(tuple[5], Is.EqualTo(long.MaxValue));

			packed = FdbTuple.Create(-1, -256, -257, -65536, int.MinValue, long.MinValue).ToSlice();
			Console.WriteLine(packed);

			tuple = FdbTuple.Unpack(packed);
			Assert.That(tuple, Is.Not.Null);
			Assert.That(tuple, Is.InstanceOf<FdbSlicedTuple>());
			Console.WriteLine(tuple);
			Assert.That(tuple.Count, Is.EqualTo(6));
			Assert.That(tuple[0], Is.EqualTo(-1));
			Assert.That(tuple[1], Is.EqualTo(-256));
			Assert.That(tuple[2], Is.EqualTo(-257), "Slice is " + ((FdbSlicedTuple)tuple).GetSlice(2).ToString());
			Assert.That(tuple[3], Is.EqualTo(-65536));
			Assert.That(tuple[4], Is.EqualTo(int.MinValue));
			Assert.That(tuple[5], Is.EqualTo(long.MinValue));
		}

		[Test]
		public void Test_FdbTuple_Numbers_Are_Sorted_Lexicographically()
		{
			// pick two numbers 'x' and 'y' at random, and check that the order of 'x' compared to 'y' is the same as 'pack(tuple(x))' compared to 'pack(tuple(y))'

			// ie: ensure that x.CompareTo(y) always has the same sign as Tuple(x).CompareTo(Tuple(y))

			const int N = 1 * 1000 * 1000;
			var rnd = new Random();

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

		}
	}


}
