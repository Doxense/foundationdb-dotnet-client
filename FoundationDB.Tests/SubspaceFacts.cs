#region BSD Licence
/* Copyright (c) 2013-2018, Doxense SAS
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
	using System;
	using Doxense.Collections.Tuples;
	using Doxense.Serialization.Encoders;
	using FoundationDB.Client;
	using FoundationDB.Client.Tests;
	using NUnit.Framework;

	[TestFixture]
	public class SubspaceFacts : FdbTest
	{

		[Test]
		public void Test_Empty_Subspace_Is_Empty()
		{
			var subspace = KeySubspace.FromKey(Slice.Empty);
			Assert.That(subspace, Is.Not.Null, "FdbSubspace.Empty should not return null");
			Assert.That(subspace.GetPrefix(), Is.EqualTo(Slice.Empty), "FdbSubspace.Empty.Key should be equal to Slice.Empty");
			Assert.That(KeySubspace.Copy(subspace), Is.Not.SameAs(subspace));
		}

		[Test]
		[Category("LocalCluster")]
		public void Test_Subspace_With_Binary_Prefix()
		{
			var subspace = KeySubspace
				.FromKey(new byte[] { 42, 255, 0, 127 }.AsSlice())
				.Using(TypeSystem.Tuples);

			Assert.That(subspace.GetPrefix().ToString(), Is.EqualTo("*<FF><00><7F>"));
			Assert.That(KeySubspace.Copy(subspace), Is.Not.SameAs(subspace));
			Assert.That(KeySubspace.Copy(subspace).GetPrefix(), Is.EqualTo(subspace.GetPrefix()));

			// concat(Slice) should append the slice to the binary prefix directly
			Assert.That(subspace[Slice.FromInt32(0x01020304)].ToString(), Is.EqualTo("*<FF><00><7F><04><03><02><01>"));
			Assert.That(subspace[Slice.FromStringAscii("hello")].ToString(), Is.EqualTo("*<FF><00><7F>hello"));

			// pack(...) should use tuple serialization
			Assert.That(subspace.Keys.Encode(123).ToString(), Is.EqualTo("*<FF><00><7F><15>{"));
			Assert.That(subspace.Keys.Encode("hello").ToString(), Is.EqualTo("*<FF><00><7F><02>hello<00>"));
			Assert.That(subspace.Keys.Encode(Slice.FromStringAscii("world")).ToString(), Is.EqualTo("*<FF><00><7F><01>world<00>"));
			Assert.That(subspace.Keys.Pack(STuple.Create("hello", 123)).ToString(), Is.EqualTo("*<FF><00><7F><02>hello<00><15>{"));

			// if we encode a tuple from this subspace, it should keep the binary prefix when converted to a key
			var k = subspace.Keys.Pack(STuple.Create("world", 123, false));
			Assert.That(k.ToString(), Is.EqualTo("*<FF><00><7F><02>world<00><15>{<14>"));

			// if we unpack the key with the binary prefix, we should get a valid tuple
			var t2 = subspace.Keys.Unpack(k);
			Assert.That(t2, Is.Not.Null);
			Assert.That(t2.Count, Is.EqualTo(3));
			Assert.That(t2.Get<string>(0), Is.EqualTo("world"));
			Assert.That(t2.Get<int>(1), Is.EqualTo(123));
			Assert.That(t2.Get<bool>(2), Is.False);
		}

		[Test]
		public void Test_Subspace_Copy_Does_Not_Share_Key_Buffer()
		{
			var original = KeySubspace.FromKey(Slice.FromString("Hello"));
			var copy = KeySubspace.Copy(original);
			Assert.That(copy, Is.Not.Null);
			Assert.That(copy, Is.Not.SameAs(original), "Copy should be a new instance");
			Assert.That(copy.GetPrefix(), Is.EqualTo(original.GetPrefix()), "Key should be equal");
			Assert.That(copy.GetPrefix().Array, Is.Not.SameAs(original.GetPrefix().Array), "Key should be a copy of the original");

			Assert.That(copy, Is.EqualTo(original), "Copy and original should be considered equal");
			Assert.That(copy.ToString(), Is.EqualTo(original.ToString()), "Copy and original should have the same string representation");
			Assert.That(copy.GetHashCode(), Is.EqualTo(original.GetHashCode()), "Copy and original should have the same hashcode");
		}

		[Test]
		public void Test_Cannot_Create_Or_Partition_Subspace_With_Slice_Nil()
		{
			Assert.That(() => new KeySubspace(Slice.Nil), Throws.ArgumentException);
			Assert.That(() => KeySubspace.FromKey(Slice.Nil), Throws.ArgumentException);
			//FIXME: typed subspaces refactoring !
			//Assert.That(() => FdbSubspace.Empty.Partition[Slice.Nil], Throws.ArgumentException);
			//Assert.That(() => FdbSubspace.Create(FdbKey.Directory).Partition[Slice.Nil], Throws.ArgumentException);
		}

		[Test]
		[Category("LocalCluster")]
		public void Test_Subspace_With_Tuple_Prefix()
		{
			var subspace = KeySubspace
				.FromKey(STuple.Create("hello"))
				.Using(TypeSystem.Tuples);

			Assert.That(subspace.GetPrefix().ToString(), Is.EqualTo("<02>hello<00>"));
			Assert.That(KeySubspace.Copy(subspace), Is.Not.SameAs(subspace));
			Assert.That(KeySubspace.Copy(subspace).GetPrefix(), Is.EqualTo(subspace.GetPrefix()));

			// concat(Slice) should append the slice to the tuple prefix directly
			Assert.That(subspace[Slice.FromInt32(0x01020304)].ToString(), Is.EqualTo("<02>hello<00><04><03><02><01>"));
			Assert.That(subspace[Slice.FromStringAscii("world")].ToString(), Is.EqualTo("<02>hello<00>world"));

			// pack(...) should use tuple serialization
			Assert.That(subspace.Keys.Encode(123).ToString(), Is.EqualTo("<02>hello<00><15>{"));
			Assert.That(subspace.Keys.Encode("world").ToString(), Is.EqualTo("<02>hello<00><02>world<00>"));

			// even though the subspace prefix is a tuple, appending to it will only return the new items
			var k = subspace.Keys.Pack(STuple.Create("world", 123, false));
			Assert.That(k.ToString(), Is.EqualTo("<02>hello<00><02>world<00><15>{<14>"));

			// if we unpack the key with the binary prefix, we should get a valid tuple
			var t2 = subspace.Keys.Unpack(k);
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
			var parent = KeySubspace.FromKey(Slice.Empty).Using(TypeSystem.Tuples);
			Assert.That(parent.GetPrefix().ToString(), Is.EqualTo("<empty>"));

			// create a child subspace using a tuple
			var child = parent.Partition[FdbKey.Directory];
			Assert.That(child, Is.Not.Null);
			Assert.That(child.GetPrefix().ToString(), Is.EqualTo("<FE>"));

			// create a key from this child subspace
			var key = child[Slice.FromFixed32(0x01020304)];
			Assert.That(key.ToString(), Is.EqualTo("<FE><04><03><02><01>"));

			// create another child
			var grandChild = child.Partition[Slice.FromStringAscii("hello")];
			Assert.That(grandChild, Is.Not.Null);
			Assert.That(grandChild.GetPrefix().ToString(), Is.EqualTo("<FE>hello"));

			key = grandChild[Slice.FromFixed32(0x01020304)];
			Assert.That(key.ToString(), Is.EqualTo("<FE>hello<04><03><02><01>"));

			// cornercase
			Assert.That(child.Partition[Slice.Empty].GetPrefix(), Is.EqualTo(child.GetPrefix()));
		}

	}

}
