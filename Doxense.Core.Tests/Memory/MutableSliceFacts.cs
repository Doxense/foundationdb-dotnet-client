#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
namespace Doxense.Slices.Tests //IMPORTANT: don't rename or else we loose all perf history on TeamCity!
{
	//README:IMPORTANT! This source file is expected to be stored as UTF-8! If the encoding is changed, some tests below may fail because they rely on specific code points!

	using System;
	using System.Diagnostics.CodeAnalysis;
	using System.Text;
	using Doxense.Testing;
	using NUnit.Framework;

	[TestFixture]
	[Category("Core-SDK")]
	public class MutableSliceFacts : DoxenseTest
	{

		[Test]
		public void Test_MutableSlice_Nil()
		{
			// MutableSlice.Nil is the equivalent of 'default(byte[])'

			Assert.That(MutableSlice.Nil.Count, Is.EqualTo(0));
			Assert.That(MutableSlice.Nil.Offset, Is.EqualTo(0));
			Assert.That(MutableSlice.Nil.Array, Is.Null);

			Assert.That(MutableSlice.Nil.IsNull, Is.True);
			Assert.That(MutableSlice.Nil.HasValue, Is.False);
			Assert.That(MutableSlice.Nil.IsEmpty, Is.False);
			Assert.That(MutableSlice.Nil.IsNullOrEmpty, Is.True);
			Assert.That(MutableSlice.Nil.IsPresent, Is.False);

			Assert.That(MutableSlice.Nil.GetBytes(), Is.Null);
			Assert.That(MutableSlice.Nil.GetBytesOrEmpty(), Has.Length.Zero);

			Assert.That(MutableSlice.Nil.Slice.Array, Is.Null);
			Assert.That(MutableSlice.Nil.Slice.Offset, Is.Zero);
			Assert.That(MutableSlice.Nil.Slice.Count, Is.Zero);
			Assert.That(MutableSlice.Nil.Slice, Is.EqualTo(Slice.Nil));
		}

		[Test]
		public void Test_MutableSlice_Empty()
		{
			// MutableSlice.Empty is the equivalent of 'new byte[0]'

			Assert.That(MutableSlice.Empty.Count, Is.EqualTo(0));
			Assert.That(MutableSlice.Empty.Offset, Is.EqualTo(0));
			Assert.That(MutableSlice.Empty.Array, Is.Not.Null);
			Assert.That(MutableSlice.Empty.Array?.Length, Is.GreaterThan(0), "The backing array for MutableSlice.Empty should not be empty, in order to work properly with the fixed() operator!");

			Assert.That(MutableSlice.Empty.IsNull, Is.False);
			Assert.That(MutableSlice.Empty.HasValue, Is.True);
			Assert.That(MutableSlice.Empty.IsEmpty, Is.True);
			Assert.That(MutableSlice.Empty.IsNullOrEmpty, Is.True);
			Assert.That(MutableSlice.Empty.IsPresent, Is.False);

			Assert.That(MutableSlice.Empty.GetBytes(), Has.Length.Zero);
			Assert.That(MutableSlice.Empty.GetBytesOrEmpty(), Has.Length.Zero);

			Assert.That(MutableSlice.Empty.Slice.Array, Is.Not.Null);
			Assert.That(MutableSlice.Empty.Slice.Offset, Is.Zero);
			Assert.That(MutableSlice.Empty.Slice.Count, Is.Zero);
			Assert.That(MutableSlice.Empty.Slice, Is.EqualTo(Slice.Empty));
		}

		[Test]
		public void Test_MutableSlice_With_Content()
		{
			var slice = MutableSlice.Create("ABC"u8.ToArray());

			Assert.That(slice.Count, Is.EqualTo(3));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Array, Is.Not.Null);
			Assert.That(slice.Array?.Length, Is.GreaterThanOrEqualTo(3));

			Assert.That(slice.IsNull, Is.False);
			Assert.That(slice.HasValue, Is.True);
			Assert.That(slice.IsEmpty, Is.False);
			Assert.That(slice.IsNullOrEmpty, Is.False);
			Assert.That(slice.IsPresent, Is.True);

			Assert.That(slice.GetBytes(), Is.EqualTo("ABC"u8.ToArray()));
			Assert.That(slice.GetBytesOrEmpty(), Is.EqualTo("ABC"u8.ToArray()));
			Assert.That(slice.Slice.ToByteString(), Is.EqualTo("ABC"));
			Assert.That(slice.Slice.ToUnicode(), Is.EqualTo("ABC"));
			Assert.That(slice.Slice.PrettyPrint(), Is.EqualTo("'ABC'"));
		}

		[Test]
		public void Test_MutableSlice_Create_With_Capacity()
		{
			Assert.That(MutableSlice.Zero(0).GetBytes(), Has.Length.Zero);
			Assert.That(MutableSlice.Zero(16).GetBytes(), Is.EqualTo(new byte[16]));

			Assert.That(() => MutableSlice.Zero(-1), Throws.InstanceOf<ArgumentException>());
		}

		[Test]
		public void Test_MutableSlice_Create_With_Byte_Array()
		{
			Assert.That(default(byte[]).AsMutableSlice().GetBytes(), Is.EqualTo(null));
			Assert.That(Array.Empty<byte>().AsMutableSlice().GetBytes(), Is.EqualTo(Array.Empty<byte>()));
			Assert.That(new byte[] { 1, 2, 3 }.AsMutableSlice().GetBytes(), Is.EqualTo(new byte[] { 1, 2, 3 }));

			// the array return by GetBytes() should not be the same array that was passed to Create !
			byte[] tmp = Guid.NewGuid().ToByteArray(); // create a 16-byte array
			var slice = tmp.AsMutableSlice();
			Assert.That(slice.Array, Is.SameAs(tmp));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(tmp.Length));
			// they should be equal, but not the same !
			Assert.That(slice.GetBytes(), Is.EqualTo(tmp));
			Assert.That(slice.GetBytes(), Is.Not.SameAs(tmp));

			// create from a slice of the array
			slice = tmp.AsMutableSlice(4, 7);
			Assert.That(slice.Array, Is.SameAs(tmp));
			Assert.That(slice.Offset, Is.EqualTo(4));
			Assert.That(slice.Count, Is.EqualTo(7));
			var buf = new byte[7];
			Array.Copy(tmp, 4, buf, 0, 7);
			Assert.That(slice.GetBytes(), Is.EqualTo(buf));

			Assert.That(default(byte[]).AsMutableSlice(), Is.EqualTo(MutableSlice.Nil));
			Assert.That(Array.Empty<byte>().AsMutableSlice(), Is.EqualTo(MutableSlice.Empty));
		}

		[Test]
		public void Test_MutableSlice_Create_Validates_Arguments()
		{
			// null array only allowed with offset=0 and count=0
			// ReSharper disable AssignNullToNotNullAttribute
			Assert.That(() => default(byte[]).AsMutableSlice(0, 1), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => default(byte[]).AsMutableSlice(1, 0), Throws.Nothing, "Count 0 ignores offset");
			Assert.That(() => default(byte[]).AsMutableSlice(1, 1), Throws.InstanceOf<ArgumentException>());
			// ReSharper restore AssignNullToNotNullAttribute

			// empty array only allowed with offset=0 and count=0
			Assert.That(() => Array.Empty<byte>().AsMutableSlice(0, 1), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => Array.Empty<byte>().AsMutableSlice(1, 0), Throws.Nothing, "Count 0 ignores offset");
			Assert.That(() => Array.Empty<byte>().AsMutableSlice(1, 1), Throws.InstanceOf<ArgumentException>());

			// last item must fit in the buffer
			Assert.That(() => new byte[3].AsMutableSlice(0, 4), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => new byte[3].AsMutableSlice(1, 3), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => new byte[3].AsMutableSlice(3, 1), Throws.InstanceOf<ArgumentException>());

			// negative arguments
			Assert.That(() => new byte[3].AsMutableSlice(-1, 1), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => new byte[3].AsMutableSlice(0, -1), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => new byte[3].AsMutableSlice(-1, -1), Throws.InstanceOf<ArgumentException>());
		}

		[Test]
		public void Test_MutableSlice_Create_With_ArraySegment()
		{
			byte[] tmp = Guid.NewGuid().ToByteArray();

			var slice = new ArraySegment<byte>(tmp).AsMutableSlice();
			Assert.That(slice.Array, Is.SameAs(tmp));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(tmp.Length));
			// they should be equal, but not the same !
			Assert.That(slice.GetBytes(), Is.EqualTo(tmp));
			Assert.That(slice.GetBytes(), Is.Not.SameAs(tmp));

			slice = new ArraySegment<byte>(tmp, 4, 7).AsMutableSlice();
			Assert.That(slice.Array, Is.SameAs(tmp));
			Assert.That(slice.Offset, Is.EqualTo(4));
			Assert.That(slice.Count, Is.EqualTo(7));
			var buf = new byte[7];
			Array.Copy(tmp, 4, buf, 0, 7);
			Assert.That(slice.GetBytes(), Is.EqualTo(buf));

			Assert.That(default(ArraySegment<byte>).AsMutableSlice(), Is.EqualTo(MutableSlice.Nil));
			Assert.That(new ArraySegment<byte>(Array.Empty<byte>()).AsMutableSlice(), Is.EqualTo(MutableSlice.Empty));
		}

		[Test]
		public void Test_MutableSlice_Pseudo_Random()
		{
			var rng = new Random();

			MutableSlice slice = MutableSlice.Random(rng, 16);
			Assert.That(slice.Array, Is.Not.Null);
			Assert.That(slice.Array.Length, Is.GreaterThanOrEqualTo(16));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(16));
			// can't really test random data, appart from checking that it's not filled with zeroes
			Assert.That(slice.GetBytes(), Is.Not.All.EqualTo(0));

			Assert.That(MutableSlice.Random(rng, 0), Is.EqualTo(MutableSlice.Empty));

			// ReSharper disable once AssignNullToNotNullAttribute
			Assert.That(() => MutableSlice.Random(default(Random), 16), Throws.ArgumentNullException);
			Assert.That(() => MutableSlice.Random(rng, -1), Throws.InstanceOf<ArgumentOutOfRangeException>());
		}

		[Test]
		public void Test_MutableSlice_Cryptographic_Random()
		{
			var rng = System.Security.Cryptography.RandomNumberGenerator.Create();

			// normal
			MutableSlice slice = MutableSlice.Random(rng, 16);
			Assert.That(slice.Array, Is.Not.Null);
			Assert.That(slice.Array.Length, Is.GreaterThanOrEqualTo(16));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(16));
			// can't really test random data, appart from checking that it's not filled with zeroes
			Assert.That(slice.GetBytes(), Is.Not.All.EqualTo(0));

			// non-zero bytes
			// we can't 100% test that, unless with a lot of iterations...
			for (int i = 0; i < 256; i++)
			{
				Assert.That(
					MutableSlice.Random(rng, 256, nonZeroBytes: true).GetBytes(),
					Is.All.Not.EqualTo(0)
				);
			}

			Assert.That(MutableSlice.Random(rng, 0), Is.EqualTo(MutableSlice.Empty));
			// ReSharper disable once AssignNullToNotNullAttribute
			Assert.That(() => MutableSlice.Random(default(System.Security.Cryptography.RandomNumberGenerator), 16), Throws.ArgumentNullException);
			Assert.That(() => MutableSlice.Random(rng, -1), Throws.InstanceOf<ArgumentException>());
		}

		#region Equality / Comparison / HashCodes...

		[Test]
		[SuppressMessage("ReSharper", "EqualExpressionComparison")]
		public void Test_MutableSlice_Equality()
		{
#pragma warning disable 1718
			// a == b == c && x != y && a != x
			var a = new byte[] { 1, 2, 3 }.AsMutableSlice();
			var b = new byte[] { 1, 2, 3 }.AsMutableSlice();
			var c = new byte[] { 0, 1, 2, 3, 4 }.AsMutableSlice(1, 3);
			var x = new byte[] { 4, 5, 6 }.AsMutableSlice();
			var y = new byte[] { 1, 2, 3 }.AsMutableSlice(0, 2);
			var z = new byte[] { 1, 2, 3, 4 }.AsMutableSlice();

			// IEquatable<MutableSlice>
			// equals
			Assert.That(a, Is.EqualTo(a));
			Assert.That(a, Is.EqualTo(b));
			Assert.That(a, Is.EqualTo(c));
			Assert.That(b, Is.EqualTo(a));
			Assert.That(b, Is.EqualTo(b));
			Assert.That(b, Is.EqualTo(c));
			Assert.That(c, Is.EqualTo(a));
			Assert.That(c, Is.EqualTo(b));
			Assert.That(c, Is.EqualTo(c));
			// not equals
			Assert.That(a, Is.Not.EqualTo(x));
			Assert.That(a, Is.Not.EqualTo(y));
			Assert.That(a, Is.Not.EqualTo(z));
#pragma warning restore 1718
		}

		[Test]
		public void Test_MutableSlice_Equals_MutableSlice()
		{

			var a = new byte[] { 1, 2, 3 }.AsMutableSlice();
			var b = new byte[] { 1, 2, 3 }.AsMutableSlice();
			var c = new byte[] { 0, 1, 2, 3, 4 }.AsMutableSlice(1, 3);
			var x = new byte[] { 4, 5, 6 }.AsMutableSlice();
			var y = new byte[] { 1, 2, 3 }.AsMutableSlice(0, 2);
			var z = new byte[] { 1, 2, 3, 4 }.AsMutableSlice();

			// equals
			Assert.That(a.Equals(a), Is.True);
			Assert.That(a.Equals(b), Is.True);
			Assert.That(a.Equals(c), Is.True);
			Assert.That(b.Equals(a), Is.True);
			Assert.That(b.Equals(b), Is.True);
			Assert.That(b.Equals(c), Is.True);
			Assert.That(c.Equals(a), Is.True);
			Assert.That(c.Equals(b), Is.True);
			Assert.That(c.Equals(c), Is.True);
			Assert.That(MutableSlice.Nil.Equals(MutableSlice.Nil), Is.True);
			Assert.That(MutableSlice.Empty.Equals(MutableSlice.Empty), Is.True);

			// not equals
			Assert.That(a.Equals(x), Is.False);
			Assert.That(a.Equals(y), Is.False);
			Assert.That(a.Equals(z), Is.False);
			Assert.That(a.Equals(MutableSlice.Nil), Is.False);
			Assert.That(a.Equals(MutableSlice.Empty), Is.False);
			Assert.That(MutableSlice.Empty.Equals(MutableSlice.Nil), Is.False);
			Assert.That(MutableSlice.Nil.Equals(MutableSlice.Empty), Is.False);
		}

		[Test]
		public void Test_MutableSlice_Equality_TwoByteArrayWithSameContentShouldReturnTrue()
		{
			var s1 = Literal("abcd");
			var s2 = Literal("abcd");
			Assert.That(s1.Equals(s2), Is.True, "'abcd' should equals 'abcd'");
		}

		[Test]
		public void Test_MutableSlice_Equality_TwoByteArrayWithSameContentFromSameOriginalBufferShouldReturnTrue()
		{
			var origin = "abcdabcd"u8.ToArray();
			var a1 = new ArraySegment<byte>(origin, 0, 4); //"abcd", refer first part of origin buffer
			var s1 = a1.AsMutableSlice(); //
			var a2 = new ArraySegment<byte>(origin, 4, 4);//"abcd", refer second part of origin buffer
			var s2 = a2.AsMutableSlice();
			Assert.That(s1.Equals(s2), Is.True, "'abcd' should equals 'abcd'");
		}

		[Test]
		public void Test_MutableSlice_Equality_Malformed()
		{
			var good = Literal("good");
			var evil = Literal("evil");

			// argument should be validated
			Assert.That(() => good.Equals(MutateOffset(evil, -1)), Throws.InstanceOf<FormatException>());
			Assert.That(() => good.Equals(MutateCount(evil, 666)), Throws.InstanceOf<FormatException>());
			Assert.That(() => good.Equals(MutateArray(evil, null)), Throws.InstanceOf<FormatException>());
			Assert.That(() => good.Equals(MutateOffset(MutateCount(evil, 5), -1)), Throws.InstanceOf<FormatException>());

			// instance should also be validated
			Assert.That(() => MutateOffset(evil, -1).Equals(good), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateCount(evil, 666).Equals(good), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateArray(evil, null).Equals(good), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateOffset(MutateCount(evil, 5), -1).Equals(good), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_MutableSlice_Hash_Code()
		{
			// note: the test values MAY change if the hashcode algorithm is modified.
			// That means that if all the asserts in this test fail, you should probably ensure that the expected results are still valid.

			Assert.That(MutableSlice.Nil.GetHashCode(), Is.EqualTo(0), "Nil hashcode should always be 0");
			Assert.That(MutableSlice.Empty.GetHashCode(), Is.Not.EqualTo(0), "Empty hashcode should not be equal to 0");

			Assert.That(Literal("abc").GetHashCode(), Is.EqualTo(Literal("abc").GetHashCode()), "Hashcode should not depend on the backing array");
			Assert.That(Literal("zabcz").Substring(1, 3).GetHashCode(), Is.EqualTo(Literal("abc").GetHashCode()), "Hashcode should not depend on the offset in the array");
			Assert.That(Literal("abc").GetHashCode(), Is.Not.EqualTo(Literal("abcd").GetHashCode()), "Hashcode should include all the bytes");

			// should validate the arguments
			var x = Literal("evil");
			Assert.That(() => MutateOffset(x, -1).GetHashCode(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateCount(x, 17).GetHashCode(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateArray(x, null).GetHashCode(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		[SuppressMessage("ReSharper", "EqualExpressionComparison")]
		public void Test_MutableSlice_Comparison()
		{
#pragma warning disable 1718
			var a = Literal((ReadOnlySpan<char>)"a");
			var ab = Literal((ReadOnlySpan<char>)"ab");
			var abc = Literal((ReadOnlySpan<char>)"abc");
			var abc2 = Literal((ReadOnlySpan<char>)"abc"); // same bytes but different buffer
			var b = Literal((ReadOnlySpan<char>)"b");

			// CompateTo
			// a = b
			Assert.That(a.CompareTo(a), Is.EqualTo(0));
			Assert.That(ab.CompareTo(ab), Is.EqualTo(0));
			Assert.That(abc.CompareTo(abc), Is.EqualTo(0));
			Assert.That(abc.CompareTo(abc2), Is.EqualTo(0));
			// a < b
			Assert.That(a.CompareTo(b), Is.LessThan(0));
			Assert.That(a.CompareTo(ab), Is.LessThan(0));
			Assert.That(a.CompareTo(abc), Is.LessThan(0));
			// a > b
			Assert.That(b.CompareTo(a), Is.GreaterThan(0));
			Assert.That(b.CompareTo(ab), Is.GreaterThan(0));
			Assert.That(b.CompareTo(abc), Is.GreaterThan(0));

#pragma warning restore 1718
		}

		[Test]
		public void Test_MutableSlice_Comparison_Corner_Cases()
		{
			// Nil == Empty
			Assert.That(MutableSlice.Nil.CompareTo(MutableSlice.Nil), Is.EqualTo(0));
			Assert.That(MutableSlice.Empty.CompareTo(MutableSlice.Empty), Is.EqualTo(0));
			Assert.That(MutableSlice.Nil.CompareTo(MutableSlice.Empty), Is.EqualTo(0));
			Assert.That(MutableSlice.Empty.CompareTo(MutableSlice.Nil), Is.EqualTo(0));

			// X > NULL, NULL < X
			var abc = Literal((ReadOnlySpan<char>)"abc");
			Assert.That(abc.CompareTo(MutableSlice.Nil), Is.GreaterThan(0));
			Assert.That(abc.CompareTo(MutableSlice.Empty), Is.GreaterThan(0));
			Assert.That(MutableSlice.Nil.CompareTo(abc), Is.LessThan(0));
			Assert.That(MutableSlice.Empty.CompareTo(abc), Is.LessThan(0));
		}

		[Test]
		public void Test_MutableSlice_Comparison_Malformed()
		{
			var good = Literal((ReadOnlySpan<char>)"good");
			var evil = Literal((ReadOnlySpan<char>)"evil");

			// argument should be validated
			Assert.That(() => good.CompareTo(MutateOffset(evil, -1)), Throws.InstanceOf<FormatException>());
			Assert.That(() => good.CompareTo(MutateCount(evil, 666)), Throws.InstanceOf<FormatException>());
			Assert.That(() => good.CompareTo(MutateArray(evil, null)), Throws.InstanceOf<FormatException>());
			Assert.That(() => good.CompareTo(MutateOffset(MutateCount(evil, 5), -1)), Throws.InstanceOf<FormatException>());

			// instance should also be validated
			Assert.That(() => MutateOffset(evil, -1).CompareTo(good), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateCount(evil, 666).CompareTo(good), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateArray(evil, null).CompareTo(good), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateOffset(MutateCount(evil, 5), -1).CompareTo(good), Throws.InstanceOf<FormatException>());
		}

		#endregion

		[Test]
		public void Test_MutableSlice_Substring()
		{
			Assert.That(MutableSlice.Empty.Substring(0), Is.EqualTo(MutableSlice.Empty));
			Assert.That(MutableSlice.Empty.Substring(0, 0), Is.EqualTo(MutableSlice.Empty));
			Assert.That(() => MutableSlice.Empty.Substring(0, 1), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => MutableSlice.Empty.Substring(1), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => MutableSlice.Empty.Substring(1, 0), Throws.Nothing, "We allow out of bound substring if count == 0");

			// Substring(offset)
			Assert.That(Literal("Hello, World!").Substring(0), Is.EqualTo(Literal("Hello, World!")));
			Assert.That(Literal("Hello, World!").Substring(7), Is.EqualTo(Literal("World!")));
			Assert.That(Literal("Hello, World!").Substring(12), Is.EqualTo(Literal("!")));
			Assert.That(Literal("Hello, World!").Substring(13), Is.EqualTo(MutableSlice.Empty));
			Assert.That(() => Literal("Hello, World!").Substring(14), Throws.InstanceOf<ArgumentOutOfRangeException>());

			// Substring(offset, count)
			Assert.That(Literal("Hello, World!").Substring(0, 5), Is.EqualTo(Literal("Hello")));
			Assert.That(Literal("Hello, World!").Substring(7, 5), Is.EqualTo(Literal("World")));
			Assert.That(Literal("Hello, World!").Substring(7, 6), Is.EqualTo(Literal("World!")));
			Assert.That(Literal("Hello, World!").Substring(12, 1), Is.EqualTo(Literal("!")));
			Assert.That(Literal("Hello, World!").Substring(13, 0), Is.EqualTo(MutableSlice.Empty));
			Assert.That(() => Literal("Hello, World!").Substring(7, 7), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => Literal("Hello, World!").Substring(13, 1), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => Literal("Hello, World!").Substring(7, -1), Throws.InstanceOf<ArgumentOutOfRangeException>());

			// Substring(offset) negative indexing
			Assert.That(Literal("Hello, World!").Substring(-1), Is.EqualTo(Literal("!")));
			Assert.That(Literal("Hello, World!").Substring(-2), Is.EqualTo(Literal("d!")));
			Assert.That(Literal("Hello, World!").Substring(-6), Is.EqualTo(Literal("World!")));
			Assert.That(Literal("Hello, World!").Substring(-13), Is.EqualTo(Literal("Hello, World!")));
			Assert.That(() => Literal("Hello, World!").Substring(-14), Throws.InstanceOf<ArgumentOutOfRangeException>());

		}

		#region Black Magic Incantations...

		// The MutableSlice struct is not blittable, so we can't take its address and modify it via pointers trickery.
		// Since its ctor is checking the arguments in Debug mode and all its fields are readonly, the only way to inject bad values is to use reflection.

		private static MutableSlice MutateOffset(MutableSlice value, int offset)
		{
			// Don't try this at home !
			object tmp = value;
			typeof(MutableSlice).GetField("Offset").SetValue(tmp, offset);
			return (MutableSlice) tmp;
		}

		private static MutableSlice MutateCount(MutableSlice value, int offset)
		{
			// Don't try this at home !
			object tmp = value;
			typeof(MutableSlice).GetField("Offset").SetValue(tmp, offset);
			return (MutableSlice) tmp;
		}

		private static MutableSlice MutateArray(MutableSlice value, byte[] array)
		{
			// Don't try this at home !
			object tmp = value;
			typeof(MutableSlice).GetField("Array").SetValue(tmp, array);
			return (MutableSlice) tmp;
		}

		#endregion

		private static MutableSlice Literal(ReadOnlySpan<byte> literal) => literal.ToArray().AsMutableSlice();

		private static MutableSlice Literal(ReadOnlySpan<char> literal) => Encoding.UTF8.GetBytes(literal.ToArray()).AsMutableSlice();

		private static MutableSlice Literal(string text) => Encoding.UTF8.GetBytes(text).AsMutableSlice();
	}

}
