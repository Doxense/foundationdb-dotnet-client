#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
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

namespace Doxense.Memory.Tests
{
	using System;
	using System.Text;
	using FoundationDB.Client.Tests;
	using NUnit.Framework;

	[TestFixture]
	public class MutableSliceComparerFacts : FdbTest
	{

		[Test]
		public void Test_MutableSliceComparer_Equals()
		{
			var cmp = MutableSlice.Comparer.Default;
			Assert.That(cmp, Is.Not.Null);
			Assert.That(MutableSlice.Comparer.Default, Is.SameAs(cmp));

			Assert.That(cmp.Equals(MutableSlice.Nil, MutableSlice.Nil), Is.True);
			Assert.That(cmp.Equals(MutableSlice.Empty, MutableSlice.Empty), Is.True);
			Assert.That(cmp.Equals(MutableSlice.Nil, MutableSlice.Empty), Is.False);
			Assert.That(cmp.Equals(MutableSlice.Empty, MutableSlice.Nil), Is.False);

			Assert.That(cmp.Equals(MutableSlice.FromByte(42), MutableSlice.FromByte(42)), Is.True);
			Assert.That(cmp.Equals(MutableSlice.FromByte(42), new byte[] { 42 }.AsMutableSlice()), Is.True);
			Assert.That(cmp.Equals(MutableSlice.FromByte(42), MutableSlice.FromByte(77)), Is.False);

			Assert.That(cmp.Equals(new byte[] { 65, 66, 67 }.AsMutableSlice(), MutableSlice.FromString("ABC")), Is.True);
			Assert.That(cmp.Equals(new byte[] { 65, 66, 67, 68 }.AsMutableSlice(), MutableSlice.FromString("ABC")), Is.False);

			var buf1 = Encoding.ASCII.GetBytes("ABBAABA");
			var buf2 = Encoding.ASCII.GetBytes("ABBAABA");
			Assert.That(cmp.Equals(buf1.AsMutableSlice(0, 2), buf1.AsMutableSlice(0, 2)), Is.True);
			Assert.That(cmp.Equals(buf1.AsMutableSlice(0, 2), buf1.AsMutableSlice(0, 3)), Is.False);
			Assert.That(cmp.Equals(buf1.AsMutableSlice(0, 2), buf1.AsMutableSlice(4, 2)), Is.True);
			Assert.That(cmp.Equals(buf1.AsMutableSlice(0, 3), buf1.AsMutableSlice(4, 3)), Is.False);
			Assert.That(cmp.Equals(buf1.AsMutableSlice(0, 2), buf2.AsMutableSlice(4, 2)), Is.True);
			Assert.That(cmp.Equals(buf1.AsMutableSlice(0, 3), buf2.AsMutableSlice(4, 3)), Is.False);
		}

		[Test]
		public void Test_MutableSliceComparer_GetHashCode_Should_Return_Same_As_Slice()
		{
			var cmp = MutableSlice.Comparer.Default;
			Assert.That(cmp, Is.Not.Null);

			Assert.That(cmp.GetHashCode(MutableSlice.Nil), Is.EqualTo(MutableSlice.Nil.GetHashCode()));
			Assert.That(cmp.GetHashCode(MutableSlice.Empty), Is.EqualTo(MutableSlice.Empty.GetHashCode()));
			Assert.That(cmp.GetHashCode(MutableSlice.Nil), Is.Not.EqualTo(MutableSlice.Empty));

			var rnd = new Random(123456);
			for (int i = 0; i < 100; i++)
			{
				var s = MutableSlice.Random(rnd, rnd.Next(1, 16));
				Assert.That(cmp.GetHashCode(s), Is.EqualTo(s.GetHashCode()));
			}
		}

		[Test]
		public void Test_MutableSliceComparer_Compare()
		{
			var cmp = MutableSlice.Comparer.Default;
			Assert.That(cmp, Is.Not.Null);

			Assert.That(cmp.Compare(MutableSlice.Nil, MutableSlice.Nil), Is.Zero);
			Assert.That(cmp.Compare(MutableSlice.Empty, MutableSlice.Empty), Is.Zero);
			Assert.That(cmp.Compare(MutableSlice.FromByte(42), MutableSlice.FromByte(42)), Is.Zero);

			//REVIEW: Inconsistency: compare(nil, empty) == 0, but Equals(nil, empty) == false
			Assert.That(cmp.Compare(MutableSlice.Nil, MutableSlice.Empty), Is.Zero, "Nil and Empty are considered similar regarding ordering");
			Assert.That(cmp.Compare(MutableSlice.Empty, MutableSlice.Nil), Is.Zero, "Nil and Empty are considered similar regarding ordering");

			Assert.That(cmp.Compare(MutableSlice.FromByte(42), MutableSlice.FromByte(77)), Is.LessThan(0));
			Assert.That(cmp.Compare(MutableSlice.FromByte(42), MutableSlice.FromByte(21)), Is.GreaterThan(0));
			Assert.That(cmp.Compare(MutableSlice.FromByte(42), MutableSlice.Empty), Is.GreaterThan(0));
			Assert.That(cmp.Compare(MutableSlice.FromByte(42), MutableSlice.Nil), Is.GreaterThan(0));

			Assert.That(cmp.Compare(MutableSlice.FromString("hello"), MutableSlice.FromString("world")), Is.LessThan(0));
			Assert.That(cmp.Compare(MutableSlice.FromString("world"), MutableSlice.FromString("hello")), Is.GreaterThan(0));

			Assert.That(cmp.Compare(MutableSlice.FromString("hell"), MutableSlice.FromString("hello")), Is.LessThan(0));
			Assert.That(cmp.Compare(MutableSlice.FromString("help"), MutableSlice.FromString("hello")), Is.GreaterThan(0));
		}

	}
}
