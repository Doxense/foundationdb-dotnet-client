﻿#region BSD License
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

namespace FoundationDB.Client.Tests
{
	using System;
	using Doxense.Collections.Tuples;
	using FoundationDB.Client;
	using NUnit.Framework;

	[TestFixture]
	public class KeySubspaceFacts : FdbTest
	{

		[Test]
		public void Test_Empty_Subspace_Is_Empty()
		{
			var subspace = KeySubspace.FromKey(Slice.Empty);
			Assert.That(subspace, Is.Not.Null, "FdbSubspace.Empty should not return null");
			Assert.That(subspace.GetPrefix(), Is.EqualTo(Slice.Empty), "FdbSubspace.Empty.Key should be equal to Slice.Empty");
			Assert.That(subspace.Copy(), Is.Not.SameAs(subspace));
		}

		[Test]
		[Category("LocalCluster")]
		public void Test_Subspace_With_Binary_Prefix()
		{
			var subspace = KeySubspace.CreateDynamic(new byte[] { 42, 255, 0, 127 }.AsSlice());

			Assert.That(subspace.GetPrefix().ToString(), Is.EqualTo("*<FF><00><7F>"));
			Assert.That(subspace.Copy(), Is.Not.SameAs(subspace));
			Assert.That(subspace.Copy().GetPrefix(), Is.EqualTo(subspace.GetPrefix()));

			// concat(Slice) should append the slice to the binary prefix directly
			Assert.That(subspace.Append(Slice.FromInt32(0x01020304)).ToString(), Is.EqualTo("*<FF><00><7F><04><03><02><01>"));
			Assert.That(subspace.Append(Slice.FromStringAscii("hello")).ToString(), Is.EqualTo("*<FF><00><7F>hello"));

			// pack(...) should use tuple serialization
			Assert.That(subspace.Encode(123).ToString(), Is.EqualTo("*<FF><00><7F><15>{"));
			Assert.That(subspace.Encode("hello").ToString(), Is.EqualTo("*<FF><00><7F><02>hello<00>"));
			Assert.That(subspace.Encode(Slice.FromStringAscii("world")).ToString(), Is.EqualTo("*<FF><00><7F><01>world<00>"));
			Assert.That(subspace.Pack(STuple.Create("hello", 123)).ToString(), Is.EqualTo("*<FF><00><7F><02>hello<00><15>{"));
			Assert.That(subspace.Pack(("hello", 123)).ToString(), Is.EqualTo("*<FF><00><7F><02>hello<00><15>{"));

			// if we encode a tuple from this subspace, it should keep the binary prefix when converted to a key
			var k = subspace.Pack(("world", 123, false));
			Assert.That(k.ToString(), Is.EqualTo("*<FF><00><7F><02>world<00><15>{&"));

			// if we unpack the key with the binary prefix, we should get a valid tuple
			var t2 = subspace.Unpack(k);
			Assert.That(t2, Is.Not.Null);
			Assert.That(t2.Count, Is.EqualTo(3));
			Assert.That(t2.Get<string>(0), Is.EqualTo("world"));
			Assert.That(t2.Get<int>(1), Is.EqualTo(123));
			Assert.That(t2.Get<bool>(2), Is.False);

			// ValueTuple
			Assert.That(subspace.Pack(ValueTuple.Create("hello")).ToString(), Is.EqualTo("*<FF><00><7F><02>hello<00>"));
			Assert.That(subspace.Pack(("hello", 123)).ToString(), Is.EqualTo("*<FF><00><7F><02>hello<00><15>{"));
			Assert.That(subspace.Pack(("hello", 123, "world")).ToString(), Is.EqualTo("*<FF><00><7F><02>hello<00><15>{<02>world<00>"));
			Assert.That(subspace.Pack(("hello", 123, "world", 456)).ToString(), Is.EqualTo("*<FF><00><7F><02>hello<00><15>{<02>world<00><16><01><C8>"));
		}

		[Test]
		public void Test_Subspace_Copy_Does_Not_Share_Key_Buffer()
		{
			var original = KeySubspace.FromKey(Slice.FromString("Hello"));
			var copy = original.Copy();
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
			Assert.That(() => new KeySubspace(Slice.Nil, SubspaceContext.Default), Throws.ArgumentException);
			Assert.That(() => new KeySubspace(Slice.Empty, default(SubspaceContext)), Throws.ArgumentNullException);
			Assert.That(() => KeySubspace.FromKey(Slice.Nil), Throws.ArgumentException);
			//FIXME: typed subspaces refactoring !
			//Assert.That(() => FdbSubspace.Empty.Partition[Slice.Nil], Throws.ArgumentException);
			//Assert.That(() => FdbSubspace.Create(FdbKey.Directory).Partition[Slice.Nil], Throws.ArgumentException);
		}

		[Test]
		[Category("LocalCluster")]
		public void Test_Subspace_With_Tuple_Prefix()
		{
			var subspace = KeySubspace.CreateDynamic(TuPack.EncodeKey("hello"));

			Assert.That(subspace.GetPrefix().ToString(), Is.EqualTo("<02>hello<00>"));
			Assert.That(subspace.Copy(), Is.Not.SameAs(subspace));
			Assert.That(subspace.Copy().GetPrefix(), Is.EqualTo(subspace.GetPrefix()));

			// concat(Slice) should append the slice to the tuple prefix directly
			Assert.That(subspace.Append(Slice.FromInt32(0x01020304)).ToString(), Is.EqualTo("<02>hello<00><04><03><02><01>"));
			Assert.That(subspace.Append(Slice.FromStringAscii("world")).ToString(), Is.EqualTo("<02>hello<00>world"));

			// pack(...) should use tuple serialization
			Assert.That(subspace.Encode(123).ToString(), Is.EqualTo("<02>hello<00><15>{"));
			Assert.That(subspace.Encode("world").ToString(), Is.EqualTo("<02>hello<00><02>world<00>"));

			// even though the subspace prefix is a tuple, appending to it will only return the new items
			var k = subspace.Pack(("world", 123, false));
			Assert.That(k.ToString(), Is.EqualTo("<02>hello<00><02>world<00><15>{&"));

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
			var parent = KeySubspace.CreateDynamic(Slice.Empty);
			Assert.That(parent.GetPrefix().ToString(), Is.EqualTo("<empty>"));

			// create a child subspace using a tuple
			var child = parent.Partition[FdbKey.Directory].AsBinary();
			Assert.That(child, Is.Not.Null);
			Assert.That(child.GetPrefix().ToString(), Is.EqualTo("<FE>"));

			// create a key from this child subspace
			var key = child[Slice.FromFixed32(0x01020304)];
			Assert.That(key.ToString(), Is.EqualTo("<FE><04><03><02><01>"));

			// create another child
			var grandChild = child.Partition(Slice.FromStringAscii("hello"));
			Assert.That(grandChild, Is.Not.Null);
			Assert.That(grandChild.GetPrefix().ToString(), Is.EqualTo("<FE>hello"));

			key = grandChild[Slice.FromFixed32(0x01020304)];
			Assert.That(key.ToString(), Is.EqualTo("<FE>hello<04><03><02><01>"));

			// corner case
			Assert.That(child.Partition(Slice.Empty).GetPrefix(), Is.EqualTo(child.GetPrefix()));
		}

		[Test]
		public void Test_DynamicKeySpace_API()
		{
			var location = KeySubspace.CreateDynamic(Slice.FromString("PREFIX"));

			Assert.That(location.Append(Slice.FromString("SUFFIX")).ToString(), Is.EqualTo("PREFIXSUFFIX"));

			// Encode<T...>(...)
			Assert.That(location.Encode("hello").ToString(), Is.EqualTo("PREFIX<02>hello<00>"));
			Assert.That(location.Encode("hello", 123).ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{"));
			Assert.That(location.Encode("hello", 123, "world").ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{<02>world<00>"));
			Assert.That(location.Encode("hello", 123, "world", 456).ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{<02>world<00><16><01><C8>"));
			Assert.That(location.Encode("hello", 123, "world", 456, "!").ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{<02>world<00><16><01><C8><02>!<00>"));
			Assert.That(location.Encode("hello", 123, "world", 456, "!", 789).ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{<02>world<00><16><01><C8><02>!<00><16><03><15>"));

			// Pack(ITuple)
			Assert.That(location.Pack((IVarTuple) STuple.Create("hello")).ToString(), Is.EqualTo("PREFIX<02>hello<00>"));
			Assert.That(location.Pack((IVarTuple) STuple.Create("hello", 123)).ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{"));
			Assert.That(location.Pack((IVarTuple) STuple.Create("hello", 123, "world")).ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{<02>world<00>"));
			Assert.That(location.Pack((IVarTuple) STuple.Create("hello", 123, "world", 456)).ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{<02>world<00><16><01><C8>"));
			Assert.That(location.Pack((IVarTuple) STuple.Create("hello", 123, "world", 456, "!")).ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{<02>world<00><16><01><C8><02>!<00>"));
			Assert.That(location.Pack((IVarTuple) STuple.Create("hello", 123, "world", 456, "!", 789)).ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{<02>world<00><16><01><C8><02>!<00><16><03><15>"));

			// Pack(ValueTuple)
			Assert.That(location.Pack(ValueTuple.Create("hello")).ToString(), Is.EqualTo("PREFIX<02>hello<00>"));
			Assert.That(location.Pack(("hello", 123)).ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{"));
			Assert.That(location.Pack(("hello", 123, "world")).ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{<02>world<00>"));
			Assert.That(location.Pack(("hello", 123, "world", 456)).ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{<02>world<00><16><01><C8>"));
			Assert.That(location.Pack(("hello", 123, "world", 456, "!")).ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{<02>world<00><16><01><C8><02>!<00>"));
			Assert.That(location.Pack(("hello", 123, "world", 456, "!", 789)).ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{<02>world<00><16><01><C8><02>!<00><16><03><15>"));

			// ITuple Unpack(Slice)
			Assert.That(location.Unpack(Slice.Unescape("PREFIX<02>hello<00>")), Is.EqualTo(STuple.Create("hello")));
			Assert.That(location.Unpack(Slice.Unescape("PREFIX<02>hello<00><15>{")), Is.EqualTo(STuple.Create("hello", 123)));
			Assert.That(location.Unpack(Slice.Unescape("PREFIX<02>hello<00><15>{<02>world<00>")), Is.EqualTo(STuple.Create("hello", 123, "world")));
			Assert.That(location.Unpack(Slice.Unescape("PREFIX<02>hello<00><15>{<02>world<00><16><01><C8>")), Is.EqualTo(STuple.Create("hello", 123, "world", 456)));
			Assert.That(location.Unpack(Slice.Unescape("PREFIX<02>hello<00><15>{<02>world<00><16><01><C8><02>!<00>")), Is.EqualTo(STuple.Create("hello", 123, "world", 456, "!")));
			Assert.That(location.Unpack(Slice.Unescape("PREFIX<02>hello<00><15>{<02>world<00><16><01><C8><02>!<00><16><03><15>")), Is.EqualTo(STuple.Create("hello", 123, "world", 456, "!", 789)));

			// STuple<T...> Decode(Slice)
			Assert.That(location.Decode<string>(Slice.Unescape("PREFIX<02>hello<00>")), Is.EqualTo("hello"));
			Assert.That(location.Decode<string, int>(Slice.Unescape("PREFIX<02>hello<00><15>{")), Is.EqualTo(("hello", 123)));
			Assert.That(location.Decode<string, int, string>(Slice.Unescape("PREFIX<02>hello<00><15>{<02>world<00>")), Is.EqualTo(("hello", 123, "world")));
			Assert.That(location.Decode<string, int, string, int>(Slice.Unescape("PREFIX<02>hello<00><15>{<02>world<00><16><01><C8>")), Is.EqualTo(("hello", 123, "world", 456)));
			Assert.That(location.Decode<string, int, string, int, string>(Slice.Unescape("PREFIX<02>hello<00><15>{<02>world<00><16><01><C8><02>!<00>")), Is.EqualTo(("hello", 123, "world", 456, "!")));
			Assert.That(location.Decode<string, int, string, int, string, int>(Slice.Unescape("PREFIX<02>hello<00><15>{<02>world<00><16><01><C8><02>!<00><16><03><15>")), Is.EqualTo(("hello", 123, "world", 456, "!", 789)));

			// DecodeFirst/DecodeLast
			Assert.That(location.DecodeFirst<string>(Slice.Unescape("PREFIX<02>hello<00><15>{<02>world<00><16><01><C8><02>!<00><16><03><15>")), Is.EqualTo("hello"));
			Assert.That(location.DecodeLast<int>(Slice.Unescape("PREFIX<02>hello<00><15>{<02>world<00><16><01><C8><02>!<00><16><03><15>")), Is.EqualTo(789));

		}

		[Test]
		public void Test_TypedKeySpace_T1()
		{
			var location = KeySubspace.CreateTyped<string>(Slice.FromString("PREFIX"));
			Assert.That(location.KeyEncoder, Is.Not.Null, "Should have a Key Encoder");
			Assert.That(location.KeyEncoder.Encoding, Is.SameAs(TuPack.Encoding), "Encoder should use Tuple type system");

			// shortcuts
			Assert.That(location.Append(Slice.FromString("SUFFIX")).ToString(), Is.EqualTo("PREFIXSUFFIX"));
			Assert.That(location["hello"].ToString(), Is.EqualTo("PREFIX<02>hello<00>"));
			Assert.That(location[ValueTuple.Create("hello")].ToString(), Is.EqualTo("PREFIX<02>hello<00>"));

			// Encode<T...>(...)
			Assert.That(location["hello"].ToString(), Is.EqualTo("PREFIX<02>hello<00>"));

			// Pack(ITuple)
			Assert.That(location.Pack(STuple.Create("hello")).ToString(), Is.EqualTo("PREFIX<02>hello<00>"));

			// Pack(ValueTuple)
			Assert.That(location.Pack(ValueTuple.Create("hello")).ToString(), Is.EqualTo("PREFIX<02>hello<00>"));

			// STuple<T...> Decode(...)
			Assert.That(location.Decode(Slice.Unescape("PREFIX<02>hello<00>")), Is.EqualTo("hello"));

			// Decode(..., out T)
			location.Decode(Slice.Unescape("PREFIX<02>hello<00>"), out string x);
			Assert.That(x, Is.EqualTo("hello"));
		}

		[Test]
		public void Test_TypedKeySpace_T2()
		{
			var location = KeySubspace.CreateTyped<string, int>(Slice.FromString("PREFIX"));

			// shortcuts
			Assert.That(location.Append(Slice.FromString("SUFFIX")).ToString(), Is.EqualTo("PREFIXSUFFIX"));
			Assert.That(location["hello", 123].ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{"));
			Assert.That(location[("hello", 123)].ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{"));

			// Encode<T...>(...)
			Assert.That(location.Encode("hello", 123).ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{"));

			// Pack(ITuple)
			Assert.That(location.Pack((IVarTuple) STuple.Create("hello", 123)).ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{"));

			// Pack(ValueTuple)
			Assert.That(location.Pack(("hello", 123)).ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{"));

			// STuple<T...> Decode(...)
			Assert.That(location.Decode(Slice.Unescape("PREFIX<02>hello<00><15>{")), Is.EqualTo(("hello", 123)));

			// Decode(..., out T)
			location.Decode(Slice.Unescape("PREFIX<02>hello<00><15>{"), out string x1, out int x2);
			Assert.That(x1, Is.EqualTo("hello"));
			Assert.That(x2, Is.EqualTo(123));
		}

		[Test]
		public void Test_TypedKeySpace_T3()
		{
			var location = KeySubspace.CreateTyped<string, int, string>(Slice.FromString("PREFIX"));

			// shortcuts
			Assert.That(location.Append(Slice.FromString("SUFFIX")).ToString(), Is.EqualTo("PREFIXSUFFIX"));
			Assert.That(location["hello", 123, "world"].ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{<02>world<00>"));
			Assert.That(location[("hello", 123, "world")].ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{<02>world<00>"));

			// Encode<T...>(...)
			Assert.That(location.Encode("hello", 123, "world").ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{<02>world<00>"));

			// Pack(ITuple)
			Assert.That(location.Pack((IVarTuple) STuple.Create("hello", 123, "world")).ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{<02>world<00>"));

			// Pack(ValueTuple)
			Assert.That(location.Pack(("hello", 123, "world")).ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{<02>world<00>"));

			// STuple<T...> Decode(...)
			Assert.That(location.Decode(Slice.Unescape("PREFIX<02>hello<00><15>{<02>world<00>")), Is.EqualTo(("hello", 123, "world")));

			// Decode(..., out T)
			location.Decode(Slice.Unescape("PREFIX<02>hello<00><15>{<02>world<00>"), out string x1, out int x2, out string x3);
			Assert.That(x1, Is.EqualTo("hello"));
			Assert.That(x2, Is.EqualTo(123));
			Assert.That(x3, Is.EqualTo("world"));
		}

		[Test]
		public void Test_TypedKeySpace_T4()
		{
			var location = KeySubspace.CreateTyped<string, int, string, int>(Slice.FromString("PREFIX"));

			// shortcuts
			Assert.That(location.Append(Slice.FromString("SUFFIX")).ToString(), Is.EqualTo("PREFIXSUFFIX"));
			Assert.That(location["hello", 123, "world", 456].ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{<02>world<00><16><01><C8>"));
			Assert.That(location[("hello", 123, "world", 456)].ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{<02>world<00><16><01><C8>"));

			// Encode<T...>(...)
			Assert.That(location.Encode("hello", 123, "world", 456).ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{<02>world<00><16><01><C8>"));

			// Pack(ITuple)
			Assert.That(location.Pack((IVarTuple) STuple.Create("hello", 123, "world", 456)).ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{<02>world<00><16><01><C8>"));

			// Pack(ValueTuple)
			Assert.That(location.Pack(("hello", 123, "world", 456)).ToString(), Is.EqualTo("PREFIX<02>hello<00><15>{<02>world<00><16><01><C8>"));

			// STuple<T...> Decode(...)
			Assert.That(location.Decode(Slice.Unescape("PREFIX<02>hello<00><15>{<02>world<00><16><01><C8>")), Is.EqualTo(("hello", 123, "world", 456)));

			// Decode(..., out T)
			location.Decode(Slice.Unescape("PREFIX<02>hello<00><15>{<02>world<00><16><01><C8>"), out string x1, out int x2, out string x3, out int x4);
			Assert.That(x1, Is.EqualTo("hello"));
			Assert.That(x2, Is.EqualTo(123));
			Assert.That(x3, Is.EqualTo("world"));
			Assert.That(x4, Is.EqualTo(456));
		}

	}

}
