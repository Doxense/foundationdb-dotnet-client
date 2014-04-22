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

			Assert.That(subspace.Copy(), Is.Not.SameAs(subspace));
		}

		[Test]
		[Category("LocalCluster")]
		public void Test_Subspace_With_Binary_Prefix()
		{
			var subspace = new FdbSubspace(Slice.Create(new byte[] { 42, 255, 0, 127 }));

			Assert.That(subspace.Key.ToString(), Is.EqualTo("*<FF><00><7F>"));
			Assert.That(subspace.Copy(), Is.Not.SameAs(subspace));
			Assert.That(subspace.Copy().Key, Is.EqualTo(subspace.Key));

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
		public void Test_Subspace_Copy_Does_Not_Share_Key_Buffer()
		{
			var original = FdbSubspace.Create(Slice.FromString("Hello"));
			var copy = original.Copy();
			Assert.That(copy, Is.Not.Null);
			Assert.That(copy, Is.Not.SameAs(original), "Copy should be a new instance");
			Assert.That(copy.Key, Is.EqualTo(original.Key), "Key should be equal");
			Assert.That(copy.Key.Array, Is.Not.SameAs(original.Key.Array), "Key should be a copy of the original");

			Assert.That(copy, Is.EqualTo(original), "Copy and original should be considered equal");
			Assert.That(copy.ToString(), Is.EqualTo(original.ToString()), "Copy and original should have the same string representation");
			Assert.That(copy.GetHashCode(), Is.EqualTo(original.GetHashCode()), "Copy and original should have the same hashcode");
		}

		[Test]
		public void Test_Cannot_Create_Or_Partition_Subspace_With_Slice_Nil()
		{
			Assert.That(() => new FdbSubspace(Slice.Nil), Throws.ArgumentException);
			Assert.That(() => FdbSubspace.Create(Slice.Nil), Throws.ArgumentException);
			Assert.That(() => FdbSubspace.Empty[Slice.Nil], Throws.ArgumentException);
			Assert.That(() => FdbSubspace.Create(FdbKey.Directory)[Slice.Nil], Throws.ArgumentException);
		}

		[Test]
		public void Test_Cannot_Create_Or_Partition_Subspace_With_Null_Tuple()
		{
			Assert.That(() => new FdbSubspace(default(IFdbTuple)), Throws.InstanceOf<ArgumentNullException>());
			Assert.That(() => FdbSubspace.Empty[default(IFdbTuple)], Throws.InstanceOf<ArgumentNullException>());
			Assert.That(() => FdbSubspace.Create(FdbKey.Directory)[default(IFdbTuple)], Throws.InstanceOf<ArgumentNullException>());
		}

		[Test]
		[Category("LocalCluster")]
		public void Test_Subspace_With_Tuple_Prefix()
		{
			var subspace = new FdbSubspace(FdbTuple.Create("hello"));

			Assert.That(subspace.Key.ToString(), Is.EqualTo("<02>hello<00>"));
			Assert.That(subspace.Copy(), Is.Not.SameAs(subspace));
			Assert.That(subspace.Copy().Key, Is.EqualTo(subspace.Key));

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
		public void Test_Subspace_Partitioning_With_Binary_Suffix()
		{
			// start from a parent subspace
			var parent = FdbSubspace.Empty;
			Assert.That(parent.Key.ToString(), Is.EqualTo("<empty>"));

			// create a child subspace using a tuple
			var child = parent[FdbKey.Directory];
			Assert.That(child, Is.Not.Null);
			Assert.That(child.Key.ToString(), Is.EqualTo("<FE>"));

			// create a key from this child subspace
			var key = child.Concat(Slice.FromFixed32(0x01020304));
			Assert.That(key.ToString(), Is.EqualTo("<FE><04><03><02><01>"));

			// create another child
			var grandChild = child[Slice.FromAscii("hello")];
			Assert.That(grandChild, Is.Not.Null);
			Assert.That(grandChild.Key.ToString(), Is.EqualTo("<FE>hello"));

			key = grandChild.Concat(Slice.FromFixed32(0x01020304));
			Assert.That(key.ToString(), Is.EqualTo("<FE>hello<04><03><02><01>"));

			// cornercase
			Assert.That(child[Slice.Empty].Key, Is.EqualTo(child.Key));
		}

		[Test]
		[Category("LocalCluster")]
		public void Test_Subspace_Partitioning_With_Tuple_Suffix()
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

			// check that we could also create the same tuple starting from the parent subspace
			var t2 = parent.Append("hca", 123, false);
			Assert.That(t2.ToSlice(), Is.EqualTo(t1.ToSlice()));

			// cornercase
			Assert.That(child[FdbTuple.Empty].Key, Is.EqualTo(child.Key));

		}

	}

}
