#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace FoundationDB.Client.Tests
{
	using SnowBank.Testing;

	/// <summary>Base class for all FoundationDB tests that do not require a live FoundationDB cluster</summary>
	[Category("Fdb-Client")]
	public abstract class FdbSimpleTest : SimpleTest
	{

		/// <summary>Converts a string into a raw byte encoded key, where each character is treated as a single byte</summary>
		/// <example>Literal("Hello\xFFWorld\x00")</example>
		[DebuggerNonUserCode]
		protected static Slice Literal(string literal) => Slice.FromByteString(literal);

		/// <summary>Converts a 1-tuple into a binary key</summary>
		[DebuggerNonUserCode]
		protected static Slice Key<T1>(T1 item1) => TuPack.EncodeKey(item1);

		/// <summary>Converts a 2-tuple into a binary key</summary>
		[DebuggerNonUserCode]
		protected static Slice Key<T1, T2>(T1 item1, T2 item2) => TuPack.EncodeKey(item1, item2);

		/// <summary>Converts a 3-tuple into a binary key</summary>
		[DebuggerNonUserCode]
		protected static Slice Key<T1, T2, T3>(T1 item1, T2 item2, T3 item3) => TuPack.EncodeKey(item1, item2, item3);

		/// <summary>Converts a 4-tuple into a binary key</summary>
		[DebuggerNonUserCode]
		protected static Slice Key<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4) => TuPack.EncodeKey(item1, item2, item3, item4);

		/// <summary>Converts a 5-tuple into a binary key</summary>
		[DebuggerNonUserCode]
		protected static Slice Key<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5) => TuPack.EncodeKey(item1, item2, item3, item4, item5);

		/// <summary>Pack a tuple into a binary key</summary>
		[DebuggerNonUserCode]
		protected static Slice Pack(IVarTuple items) => TuPack.Pack(items);

		/// <summary>Pack a tuple into a binary key</summary>
		[DebuggerNonUserCode]
		protected static Slice Pack<T1>(STuple<T1> items) => TuPack.Pack(items);

		/// <summary>Pack a tuple into a binary key</summary>
		[DebuggerNonUserCode]
		protected static Slice Pack<T1, T2>(STuple<T1, T2> items) => TuPack.Pack(items);

		/// <summary>Pack a tuple into a binary key</summary>
		[DebuggerNonUserCode]
		protected static Slice Pack<T1, T2>(ValueTuple<T1, T2> items) => TuPack.Pack(items);

		/// <summary>Pack a tuple into a binary key</summary>
		[DebuggerNonUserCode]
		protected static Slice Pack<T1, T2, T3>(STuple<T1, T2, T3> items) => TuPack.Pack(items);
		
		/// <summary>Pack a tuple into a binary key</summary>
		[DebuggerNonUserCode]
		protected static Slice Pack<T1, T2, T3>(ValueTuple<T1, T2, T3> items) => TuPack.Pack(items);

		/// <summary>Pack a tuple into a binary key</summary>
		[DebuggerNonUserCode]
		protected static Slice Pack<T1, T2, T3, T4>(STuple<T1, T2, T3, T4> items) => TuPack.Pack(items);

		/// <summary>Pack a tuple into a binary key</summary>
		[DebuggerNonUserCode]
		protected static Slice Pack<T1, T2, T3, T4>(ValueTuple<T1, T2, T3, T4> items) => TuPack.Pack(items);

		/// <summary>Pack a tuple into a binary key</summary>
		[DebuggerNonUserCode]
		protected static Slice Pack<T1, T2, T3, T4, T5>(STuple<T1, T2, T3, T4, T5> items) => TuPack.Pack(items);

		/// <summary>Pack a tuple into a binary key</summary>
		[DebuggerNonUserCode]
		protected static Slice Pack<T1, T2, T3, T4, T5>(ValueTuple<T1, T2, T3, T4, T5> items) => TuPack.Pack(items);

		/// <summary>Converts a UTF-8 string into encoded value</summary>
		[DebuggerNonUserCode]
		protected static Slice Text(ReadOnlySpan<byte> text) => text.ToSlice();

		/// <summary>Converts a string into an utf-8 encoded value</summary>
		[DebuggerNonUserCode]
		protected static Slice Text(string text) => Slice.FromStringUtf8(text);

		/// <summary>Converts a number into a fixed-size 32-bit slice</summary>
		[DebuggerNonUserCode]
		protected static Slice Value(int value) => Slice.FromFixed32(value);

		/// <summary>Converts a number into a fixed-size 32-bit slice</summary>
		[DebuggerNonUserCode]
		protected static Slice Value(uint value) => Slice.FromFixedU32(value);

		/// <summary>Converts a number into a fixed-size 64-bit slice</summary>
		[DebuggerNonUserCode]
		protected static Slice Value(long value) => Slice.FromFixed64(value);

		/// <summary>Converts a number into a fixed-size 64-bit slice</summary>
		[DebuggerNonUserCode]
		protected static Slice Value(ulong value) => Slice.FromFixedU64(value);

		[DebuggerNonUserCode]
		protected static void Log(KeySelector selector) => Log($"(KeySelector) {selector}");

		[DebuggerNonUserCode]
		protected static void Log(KeySelectorPair selector) => Log($"(KeySelectorPair) {selector}");

		[DebuggerNonUserCode]
		protected static void Log(KeyRange range) => Log($"(KeyRange) {range}");

		[DebuggerNonUserCode]
		protected static void Log(IKeySubspace? subspace) => Log(subspace?.ToString() ?? "<null>");

		[DebuggerNonUserCode]
		protected static void Log(ISubspaceLocation? location) => Log(location?.ToString() ?? "<null>");

	}

}
