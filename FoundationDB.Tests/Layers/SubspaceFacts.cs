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
	using FoundationDB.Client.Tests;
	using FoundationDB.Layers.Tuples;
	using NUnit.Framework;
	using System;
	using System.Threading.Tasks;

	[TestFixture]
	public class SubspaceFacts
	{

		[Test]
		public void Test_Empty_Subspace_Is_Empty()
		{
			var subspace = FdbSubspace.Empty;
			Assert.That(subspace, Is.Not.Null, "FdbSubspace.Empty should not return null");
			Assert.That(FdbSubspace.Empty, Is.SameAs(subspace), "FdbSubspace.Empty is a singleton");

			Assert.That(subspace.Key.Count, Is.EqualTo(0), "FdbSubspace.Empty.Key should be equal to Slice.Empty");
			Assert.That(subspace.Key.HasValue, Is.True, "FdbSubspace.Empty.Key should be equal to Slice.Empty");

		}

		[Test]
		public void Test_Subspace_With_Binary_Prefix()
		{
			var subspace = new FdbSubspace(Slice.Create(new byte[] { 42, 255, 0, 127 }));

			Assert.That(subspace.Key.ToString(), Is.EqualTo("*<FF><00><7F>"));

			// concat(Slice) should append the slice to the binary prefix directly
			Assert.That(subspace.Concat(Slice.FromInt32(0x01020304)).ToString(), Is.EqualTo("*<FF><00><7F><04><03><02><01>"));
			Assert.That(subspace.Concat(Slice.FromAscii("hello")).ToString(), Is.EqualTo("*<FF><00><7F>hello"));

			// pack(...) should use tuple serialization
			Assert.That(subspace.Pack(123).ToString(), Is.EqualTo("*<FF><00><7F><15>{"));
			Assert.That(subspace.Pack("hello").ToString(), Is.EqualTo("*<FF><00><7F><02>hello<00>"));
			Assert.That(subspace.Pack(Slice.FromAscii("world")).ToString(), Is.EqualTo("*<FF><00><7F><01>world<00>"));
			Assert.That(subspace.Pack(FdbTuple.Create("hello", 123)).ToString(), Is.EqualTo("*<FF><00><7F><02>hello<00><15>{"));

			// if we derive a tuple from this subspace, it should keep the binary prefix when converted to a key
			var t = subspace.Append("world", 123, false);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Subspace, Is.SameAs(subspace));
			Assert.That(t.Count, Is.EqualTo(3));
			Assert.That(t.Get<string>(0), Is.EqualTo("world"));
			Assert.That(t.Get<int>(1), Is.EqualTo(123));
			Assert.That(t.Get<bool>(2), Is.False);
			var k = t.ToSlice();
			Assert.That(k.ToString(), Is.EqualTo("*<FF><00><7F><02>world<00><15>{<14>"));

			// if we unpack the key with the binary prefix, we should get a valid tuple
			var t2 = subspace.Unpack(k);
			Assert.That(t2, Is.Not.Null);
			Assert.That(t2.Count, Is.EqualTo(3));
			Assert.That(t2.Get<string>(0), Is.EqualTo("world"));
			Assert.That(t2.Get<int>(1), Is.EqualTo(123));
			Assert.That(t2.Get<bool>(2), Is.False);

		}
	
		[Test]
		public void Test_Subspace_With_Tuple_Prefix()
		{
			var subspace = new FdbSubspace(FdbTuple.Create("hello"));

			Assert.That(subspace.Key.ToString(), Is.EqualTo("<02>hello<00>"));

			// concat(Slice) should append the slice to the tuple prefix directly
			Assert.That(subspace.Concat(Slice.FromInt32(0x01020304)).ToString(), Is.EqualTo("<02>hello<00><04><03><02><01>"));
			Assert.That(subspace.Concat(Slice.FromAscii("world")).ToString(), Is.EqualTo("<02>hello<00>world"));

			// pack(...) should use tuple serialization
			Assert.That(subspace.Pack(123).ToString(), Is.EqualTo("<02>hello<00><15>{"));
			Assert.That(subspace.Pack("world").ToString(), Is.EqualTo("<02>hello<00><02>world<00>"));

			// even though the subspace prefix is a tuple, appending to it will only return the new items
			var t = subspace.Append("world", 123, false);
			Assert.That(t, Is.Not.Null);
			Assert.That(t.Count, Is.EqualTo(3));
			Assert.That(t.Get<string>(0), Is.EqualTo("world"));
			Assert.That(t.Get<int>(1), Is.EqualTo(123));
			Assert.That(t.Get<bool>(2), Is.False);
			Assert.That(t.Subspace, Is.SameAs(subspace));
			// but ToSlice() should include the prefix
			var k = t.ToSlice();
			Assert.That(k.ToString(), Is.EqualTo("<02>hello<00><02>world<00><15>{<14>"));

			// if we unpack the key with the binary prefix, we should get a valid tuple
			var t2 = subspace.Unpack(k);
			Assert.That(t2, Is.Not.Null);
			Assert.That(t2.Count, Is.EqualTo(3));
			Assert.That(t2.Get<string>(0), Is.EqualTo("world"));
			Assert.That(t2.Get<int>(1), Is.EqualTo(123));
			Assert.That(t2.Get<bool>(2), Is.False);

		}

		[Test]
		public void Test_Subspace_Partitioning()
		{
			// start from a parent subspace
			var parent = new FdbSubspace(Slice.Create(new byte[] { 254 }));
			Assert.That(parent.Key.ToString(), Is.EqualTo("<FE>"));

			// create a child subspace using a tuple
			var child = parent.Partition(FdbTuple.Create("hca"));
			Assert.That(child, Is.Not.Null);
			Assert.That(child.Key.ToString(), Is.EqualTo("<FE><02>hca<00>"));

			// create a tuple from this child subspace
			var tuple = child.Append(123);
			Assert.That(tuple, Is.Not.Null);
			Assert.That(tuple.ToSlice().ToString(), Is.EqualTo("<FE><02>hca<00><15>{"));

			// derive another tuple from this one
			var t1 = tuple.Append(false);
			Assert.That(t1.ToSlice().ToString(), Is.EqualTo("<FE><02>hca<00><15>{<14>"));

			// check that we could also create the same key starting from the parent subspace
			var t2 = parent.Append("hca", 123, false);
			Assert.That(t2.ToSlice(), Is.EqualTo(t1.ToSlice()));
		}

	}

}
