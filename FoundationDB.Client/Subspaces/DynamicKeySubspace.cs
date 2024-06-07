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

namespace FoundationDB.Client
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using Doxense.Collections.Tuples;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using Doxense.Serialization.Encoders;
	using JetBrains.Annotations;

	/// <summary>Represents a <see cref="IKeySubspace">Key Subspace</see> which can encode and decode keys of arbitrary size and types.</summary>
	/// <remarks>This is useful when dealing with subspaces that store keys of different types and shapes.</remarks>
	/// <example>In pseudo code, we obtain a dynamic subspace that wraps a prefix, and uses the <see cref="Doxense.Collections.Tuples.TuPack">Tuple Encoder Format</see> to encode variable-size tuples into binary:
	/// <code>
	/// subspace = {...}.OpenOrCreate(..., "/some/path/to/data", TypeSystem.Tuples)
	/// subspace.GetPrefix() => {prefix}
	/// subspace.Keys.Pack(("Hello", "World")) => (PREFIX, 'Hello', 'World') => {prefix}.'\x02Hello\x00\x02World\x00'
	/// subspace.Keys.Encode("Hello", "World") => (PREFIX, 'Hello', 'World') => {prefix}.'\x02Hello\x00\x02World\x00'
	/// subspace.Keys.Decode({prefix}'\x02Hello\x00\x15\x42') => ('Hello', 0x42)
	/// </code>
	/// </example>
	[PublicAPI]
	public interface IDynamicKeySubspace : IKeySubspace
	{

		/// <summary>Returns an helper object that knows how to create sub-partitions of this subspace</summary>
		DynamicPartition Partition { get; }

		/// <summary>Encoder used to generate and parse the keys of this subspace</summary>
		IDynamicKeyEncoder KeyEncoder { get; }

		Slice this[IVarTuple tuple] { [Pure] get; }

		/// <summary>Convert a tuple into a key of this subspace</summary>
		/// <param name="tuple">Tuple that will be packed and appended to the subspace prefix</param>
		Slice Pack<TTuple>(TTuple tuple) where TTuple : IVarTuple;

		/// <summary>Unpack a key of this subspace, back into a tuple</summary>
		/// <param name="packedKey">Key that was produced by a previous call to <see cref="Pack{TTuple}"/></param>
		/// <returns>Original tuple</returns>
		IVarTuple Unpack(Slice packedKey);

	}

	/// <summary>Represents a <see cref="IDynamicKeySubspace">Dynamic Key Subspace</see> which can encode and decode keys of arbitrary size and types.</summary>
	/// <remarks>This is useful when dealing with subspaces that store keys of different types and shapes.</remarks>
	/// <example>In pseudo code, we obtain a dynamic subspace that wraps a prefix, and uses the <see cref="Doxense.Collections.Tuples.TuPack">Tuple Encoder Format</see> to encode variable-size tuples into binary:
	/// <code>
	/// subspace = {...}.OpenOrCreate(..., "/some/path/to/data", TypeSystem.Tuples)
	/// subspace.GetPrefix() => {prefix}
	/// subspace.Keys.Pack(("Hello", "World")) => (PREFIX, 'Hello', 'World') => {prefix}.'\x02Hello\x00\x02World\x00'
	/// subspace.Keys.Encode("Hello", "World") => (PREFIX, 'Hello', 'World') => {prefix}.'\x02Hello\x00\x02World\x00'
	/// subspace.Keys.Decode({prefix}'\x02Hello\x00\x15\x42') => ('Hello', 0x42)
	/// </code>
	/// </example>
	[PublicAPI]
	public class DynamicKeySubspace : KeySubspace, IDynamicKeySubspace, IBinaryKeySubspace
	{

		/// <summary>Encoder for the keys of this subspace</summary>
		public IDynamicKeyEncoder KeyEncoder { get; }

		/// <summary>Create a new subspace from a binary prefix</summary>
		/// <param name="prefix">Prefix of the new subspace</param>
		/// <param name="encoder">Encoder that will be used by this subspace</param>
		/// <param name="context">Context that controls the lifetime of this subspace</param>
		internal DynamicKeySubspace(Slice prefix, IDynamicKeyEncoder encoder, ISubspaceContext context)
			: base(prefix, context)
		{
			Contract.Debug.Requires(encoder != null);
			this.KeyEncoder = encoder;
			this.Partition = new DynamicPartition(this);
		}

		/// <summary>Return a view of all the possible binary keys of this subspace</summary>
		public DynamicPartition Partition { get; }

		public Slice this[IVarTuple item]
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Pack(item);
		}

		/// <summary>Convert a tuple into a key of this subspace</summary>
		/// <param name="tuple">Tuple that will be packed and appended to the subspace prefix</param>
		[Pure]
		public Slice Pack<TTuple>(TTuple tuple)
			where TTuple : IVarTuple
		{
			Contract.NotNullAllowStructs(tuple);

			var sw = this.OpenWriter();
			this.KeyEncoder.PackKey(ref sw, tuple);
			return sw.ToSlice();
		}

		/// <summary>Unpack a key of this subspace, back into a tuple</summary>
		/// <param name="packedKey">Key that was produced by a previous call to <see cref="Pack{TTuple}"/></param>
		/// <returns>Original tuple</returns>
		public IVarTuple Unpack(Slice packedKey)
		{
			return this.KeyEncoder.UnpackKey(ExtractKey(packedKey));
		}

		/// <summary>Return a user-friendly string representation of a key of this subspace</summary>
		public override string PrettyPrint(Slice packedKey)
		{
			if (packedKey.IsNull) return "<null>";
			var key = ExtractKey(packedKey, boundCheck: true);
			if (this.KeyEncoder.TryUnpackKey(key, out var tuple))
			{
				return tuple.ToString() ?? string.Empty;
			}
			return key.PrettyPrint();
		}

		#region IBinaryKeySubspace

		// we implement this because most subspaces will be dynamic, and converting them to binary would need an allocation of a BinaryKeySubspace otherwise!

		Slice IBinaryKeySubspace.this[Slice relativeKey] => Append(relativeKey.Span);

		Slice IBinaryKeySubspace.this[ReadOnlySpan<byte> relativeKey] => Append(relativeKey);

		Slice IBinaryKeySubspace.Decode(Slice absoluteKey) => ExtractKey(absoluteKey);

		IBinaryKeySubspace IBinaryKeySubspace.Partition(ReadOnlySpan<byte> relativeKey) => new BinaryKeySubspace(Append(relativeKey), this.Context);

		#endregion

	}

	/// <summary>Key helper for a dynamic TypeSystem</summary>
	[PublicAPI]
	public static class DynamicKeysExtensions
	{

		/// <summary>Encode a batch of tuples</summary>
		[Pure]
		public static Slice[] PackMany<TTuple>(this IDynamicKeySubspace self, IEnumerable<TTuple> items)
			where TTuple : IVarTuple
		{
			return Batched<TTuple, IDynamicKeyEncoder>.Convert(
				self.OpenWriter(),
				items,
				(ref SliceWriter writer, TTuple item, IDynamicKeyEncoder encoder) => encoder.PackKey(ref writer, item),
				self.KeyEncoder
			);
		}

		/// <summary>Encode a batch of tuples extracted from each elements</summary>
		[Pure]
		public static Slice[] PackMany<TSource, TTuple>(this IDynamicKeySubspace self, IEnumerable<TSource> items, Func<TSource, TTuple> selector)
			where TTuple : IVarTuple
		{
			return Batched<TSource, IDynamicKeyEncoder>.Convert(
				self.OpenWriter(),
				items,
				(ref SliceWriter writer, TSource item, IDynamicKeyEncoder encoder) => encoder.PackKey<TTuple>(ref writer, selector(item)),
				self.KeyEncoder
			);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1>(this IDynamicKeySubspace self, ValueTuple<T1> items)
		{
			return self.Encode<T1>(items.Item1);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2>(this IDynamicKeySubspace self, (T1, T2) items)
		{
			return self.Encode<T1, T2>(items.Item1, items.Item2);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3>(this IDynamicKeySubspace self, (T1, T2, T3) items)
		{
			return self.Encode<T1, T2, T3>(items.Item1, items.Item2, items.Item3);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4>(this IDynamicKeySubspace self, (T1, T2, T3, T4) items)
		{
			return self.Encode<T1, T2, T3, T4>(items.Item1, items.Item2, items.Item3, items.Item4);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5>(this IDynamicKeySubspace self, (T1, T2, T3, T4, T5) items)
		{
			return self.Encode<T1, T2, T3, T4, T5>(items.Item1, items.Item2, items.Item3, items.Item4, items.Item5);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5, T6>(this IDynamicKeySubspace self, (T1, T2, T3, T4, T5, T6) items)
		{
			return self.Encode<T1, T2, T3, T4, T5, T6>(items.Item1, items.Item2, items.Item3, items.Item4, items.Item5, items.Item6);
		}
		public static IVarTuple Unpack(this IDynamicKeySubspace self, Slice packedKey)
		{
			return self.KeyEncoder.UnpackKey(self.ExtractKey(packedKey));
		}

		#region ToRange()...

		/// <summary>Return a key range that encompass all the keys inside a partition of this subspace, according to the current key encoder</summary>
		public static KeyRange PackRange(this IDynamicKeySubspace self, IVarTuple tuple)
		{
			return self.KeyEncoder.ToRange(self.GetPrefix(), tuple);
		}

		public static KeyRange PackRange<T1>(this IDynamicKeySubspace self, STuple<T1> tuple)
		{
			return self.KeyEncoder.ToKeyRange(self.GetPrefix(), tuple.Item1);
		}

		public static KeyRange PackRange<T1, T2>(this IDynamicKeySubspace self, STuple<T1, T2> tuple)
		{
			return self.KeyEncoder.ToKeyRange(self.GetPrefix(), tuple.Item1, tuple.Item2);
		}

		public static KeyRange PackRange<T1, T2, T3>(this IDynamicKeySubspace self, STuple<T1, T2, T3> tuple)
		{
			return self.KeyEncoder.ToKeyRange(self.GetPrefix(), tuple.Item1, tuple.Item2, tuple.Item3);
		}

		public static KeyRange PackRange<T1, T2, T3, T4>(this IDynamicKeySubspace self, STuple<T1, T2, T3, T4> tuple)
		{
			return self.KeyEncoder.ToKeyRange(self.GetPrefix(), tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
		}

		public static KeyRange PackRange<T1, T2, T3, T4, T5>(this IDynamicKeySubspace self, STuple<T1, T2, T3, T4, T5> tuple)
		{
			return self.KeyEncoder.ToKeyRange(self.GetPrefix(), tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5);
		}

		public static KeyRange PackRange<T1, T2, T3, T4, T5, T6>(this IDynamicKeySubspace self, STuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			return self.KeyEncoder.ToKeyRange(self.GetPrefix(), tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5);
		}

		public static KeyRange PackRange<T1>(this IDynamicKeySubspace self, ValueTuple<T1> tuple)
		{
			return self.KeyEncoder.ToKeyRange(self.GetPrefix(), tuple.Item1);
		}

		public static KeyRange PackRange<T1, T2>(this IDynamicKeySubspace self, (T1, T2) tuple)
		{
			return self.KeyEncoder.ToKeyRange(self.GetPrefix(), tuple.Item1, tuple.Item2);
		}

		public static KeyRange PackRange<T1, T2, T3>(this IDynamicKeySubspace self, (T1, T2, T3) tuple)
		{
			return self.KeyEncoder.ToKeyRange(self.GetPrefix(), tuple.Item1, tuple.Item2, tuple.Item3);
		}

		public static KeyRange PackRange<T1, T2, T3, T4>(this IDynamicKeySubspace self, (T1, T2, T3, T4) tuple)
		{
			return self.KeyEncoder.ToKeyRange(self.GetPrefix(), tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
		}

		public static KeyRange PackRange<T1, T2, T3, T4, T5>(this IDynamicKeySubspace self, (T1, T2, T3, T4, T5) tuple)
		{
			return self.KeyEncoder.ToKeyRange(self.GetPrefix(), tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5);
		}

		public static KeyRange PackRange<T1, T2, T3, T4, T5, T6>(this IDynamicKeySubspace self, (T1, T2, T3, T4, T5, T6) tuple)
		{
			return self.KeyEncoder.ToKeyRange(self.GetPrefix(), tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5);
		}

		#endregion

		#region ToKeyRange()...

		public static KeyRange EncodeRange<T1>(this IDynamicKeySubspace self, T1 item1)
		{
			return self.KeyEncoder.ToKeyRange(self.GetPrefix(), item1);
		}

		public static KeyRange EncodeRange<T1, T2>(this IDynamicKeySubspace self, T1 item1, T2 item2)
		{
			return self.KeyEncoder.ToKeyRange(self.GetPrefix(), item1, item2);
		}

		public static KeyRange EncodeRange<T1, T2, T3>(this IDynamicKeySubspace self, T1 item1, T2 item2, T3 item3)
		{
			return self.KeyEncoder.ToKeyRange(self.GetPrefix(), item1, item2, item3);
		}

		public static KeyRange EncodeRange<T1, T2, T3, T4>(this IDynamicKeySubspace self, T1 item1, T2 item2, T3 item3, T4 item4)
		{
			return self.KeyEncoder.ToKeyRange(self.GetPrefix(), item1, item2, item3, item4);
		}

		public static KeyRange EncodeRange<T1, T2, T3, T4, T5>(this IDynamicKeySubspace self, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			return self.KeyEncoder.ToKeyRange(self.GetPrefix(), item1, item2, item3, item4, item5);
		}
		public static KeyRange EncodeRange<T1, T2, T3, T4, T5, T6>(this IDynamicKeySubspace self, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			return self.KeyEncoder.ToKeyRange(self.GetPrefix(), item1, item2, item3, item4, item5, item6);
		}
		public static KeyRange EncodeRange<T1, T2, T3, T4, T5, T6, T7>(this IDynamicKeySubspace self, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
		{
			return self.KeyEncoder.ToKeyRange(self.GetPrefix(), item1, item2, item3, item4, item5, item6, item7);
		}
		public static KeyRange EncodeRange<T1, T2, T3, T4, T5, T6, T7, T8>(this IDynamicKeySubspace self, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
		{
			return self.KeyEncoder.ToKeyRange(self.GetPrefix(), item1, item2, item3, item4, item5, item6, item7, item8);
		}

		#endregion

		#region Encode...

		/// <summary>Encode a key which is composed of a single element</summary>
		[Pure]
		public static Slice Encode<T1>(this IDynamicKeySubspace self, T1? item1)
		{
			var sw = self.OpenWriter();
			self.KeyEncoder.EncodeKey(ref sw, item1);
			return sw.ToSlice();
		}

		/// <summary>Encode a batch of keys, each one composed of a single element</summary>
		[Pure]
		public static Slice[] EncodeMany<T>(this IDynamicKeySubspace self, IEnumerable<T> items)
		{
			return Batched<T, IDynamicKeyEncoder>.Convert(
				self.OpenWriter(),
				items,
				(ref SliceWriter writer, T item, IDynamicKeyEncoder encoder) => encoder.EncodeKey<T>(ref writer, item),
				self.KeyEncoder
			);
		}

		/// <summary>Encode a batch of keys, each one composed of a single value extracted from each elements</summary>
		[Pure]
		public static Slice[] EncodeMany<TSource, T>(this IDynamicKeySubspace self, IEnumerable<TSource> items, Func<TSource, T> selector)
		{
			return Batched<TSource, IDynamicKeyEncoder>.Convert(
				self.OpenWriter(),
				items,
				(ref SliceWriter writer, TSource item, IDynamicKeyEncoder encoder) => encoder.EncodeKey<T>(ref writer, selector(item)),
				self.KeyEncoder
			);
		}

		/// <summary>Encode a key which is composed of a two elements</summary>
		[Pure]
		public static Slice Encode<T1, T2>(this IDynamicKeySubspace self, T1? item1, T2? item2)
		{
			var sw = self.OpenWriter();
			self.KeyEncoder.EncodeKey(ref sw, item1, item2);
			return sw.ToSlice();
		}

		/// <summary>Encode a key which is composed of three elements</summary>
		[Pure]
		public static Slice Encode<T1, T2, T3>(this IDynamicKeySubspace self, T1? item1, T2? item2, T3? item3)
		{
			var sw = self.OpenWriter();
			self.KeyEncoder.EncodeKey(ref sw, item1, item2, item3);
			return sw.ToSlice();
		}

		/// <summary>Encode a key which is composed of four elements</summary>
		[Pure]
		public static Slice Encode<T1, T2, T3, T4>(this IDynamicKeySubspace self, T1? item1, T2? item2, T3? item3, T4? item4)
		{
			var sw = self.OpenWriter();
			self.KeyEncoder.EncodeKey(ref sw, item1, item2, item3, item4);
			return sw.ToSlice();
		}

		/// <summary>Encode a key which is composed of five elements</summary>
		[Pure]
		public static Slice Encode<T1, T2, T3, T4, T5>(this IDynamicKeySubspace self, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5)
		{
			var sw = self.OpenWriter();
			self.KeyEncoder.EncodeKey(ref sw, item1, item2, item3, item4, item5);
			return sw.ToSlice();
		}

		/// <summary>Encode a key which is composed of six elements</summary>
		[Pure]
		public static Slice Encode<T1, T2, T3, T4, T5, T6>(this IDynamicKeySubspace self, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6)
		{
			var sw = self.OpenWriter();
			self.KeyEncoder.EncodeKey(ref sw, item1, item2, item3, item4, item5, item6);
			return sw.ToSlice();
		}

		/// <summary>Encode a key which is composed of seven elements</summary>
		[Pure]
		public static Slice Encode<T1, T2, T3, T4, T5, T6, T7>(this IDynamicKeySubspace self, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6, T7? item7)
		{
			var sw = self.OpenWriter();
			self.KeyEncoder.EncodeKey(ref sw, item1, item2, item3, item4, item5, item6, item7);
			return sw.ToSlice();
		}

		/// <summary>Encode a key which is composed of eight elements</summary>
		[Pure]
		public static Slice Encode<T1, T2, T3, T4, T5, T6, T7, T8>(this IDynamicKeySubspace self, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6, T7? item7, T8? item8)
		{
			var sw = self.OpenWriter();
			self.KeyEncoder.EncodeKey(ref sw, item1, item2, item3, item4, item5, item6, item7, item8);
			return sw.ToSlice();
		}

		/// <summary>Encodes part of a key that is intended to be used as a cursor</summary>
		/// <typeparam name="T">Type of the cursor</typeparam>
		/// <param name="self">Subspace</param>
		/// <param name="cursor">Value of the cursor</param>
		/// <returns>Slice that corresponds to only the cursor, and that can be appended as a suffix to a key generated from this subspace</returns>
		/// <remarks>
		/// <para>This method is the opposite of <see cref="EncodeRange{T}"/>, in the sense that it will generate the other part of the key.</para>
		/// <para><c>suspace.Encode("Hello", "World", 123) == subspace.Encode("Hello", "World") + subspace.EncodeCursor(123)</c></para>
		/// <para>The intended use case is in combination with methods like <see cref="KeySelectorPair.Tail"/> where we need to perform a range inside a given subspace, but resuming from a specific cursor.</para>
		/// </remarks>
		/// <example>This will read any new event received on a tenant since the last call:
		/// <code>
		/// IDynamicKeySubspace subspace = ...; // base subspace of an event log store
		/// VersionStamp cursor = ...;   // versionstamp of the last event that was received in a previous call
		/// 
		/// var (begin, end) = KeySelectorPair.Tail(
		///		prefix:  subspace.EncodeKey("Events", tenantId),
		///		cursor:  subspace.EncodeCursor(cursor),
		///		orEqual: false
		/// );
		/// await foreach(var (k, v) in tr.GetRange(begin, end))
		/// {
		///		// process the new event
		///     // var stamp = subspace.DecodeLast&lt;VersionStamp>();
		///     // ...
		///     // advance the cursor
		///		cursor = stamp;
		/// }
		/// </code></example>
		public static Slice EncodeCursor<T>(this IDynamicKeySubspace self, T? cursor)
		{
			var sw = new SliceWriter(); //TODO: BufferPool ?
			self.KeyEncoder.EncodeKey(ref sw, cursor);
			return sw.ToSlice();
		}

		#endregion

		#region Decode...

		/// <summary>Decode a key of this subspace, composed of a single element</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T1? Decode<T1>(this IDynamicKeySubspace self, Slice packedKey)
		{
			return self.KeyEncoder.DecodeKey<T1>(self.ExtractKey(packedKey));
		}

		/// <summary>Decode a key of this subspace, composed of exactly two elements</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?> Decode<T1, T2>(this IDynamicKeySubspace self, Slice packedKey)
		{
			return self.KeyEncoder.DecodeKey<T1, T2>(self.ExtractKey(packedKey));
		}

		/// <summary>Decode a key of this subspace, composed of exactly three elements</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?, T3?> Decode<T1, T2, T3>(this IDynamicKeySubspace self, Slice packedKey)
		{
			return self.KeyEncoder.DecodeKey<T1, T2, T3>(self.ExtractKey(packedKey));
		}

		/// <summary>Decode a key of this subspace, composed of exactly four elements</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?, T3?, T4?> Decode<T1, T2, T3, T4>(this IDynamicKeySubspace self, Slice packedKey)
		{
			return self.KeyEncoder.DecodeKey<T1, T2, T3, T4>(self.ExtractKey(packedKey));
		}

		/// <summary>Decode a key of this subspace, composed of exactly five elements</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?, T3?, T4?, T5?> Decode<T1, T2, T3, T4, T5>(this IDynamicKeySubspace self, Slice packedKey)
		{
			return self.KeyEncoder.DecodeKey<T1, T2, T3, T4, T5>(self.ExtractKey(packedKey));
		}

		/// <summary>Decode a key of this subspace, composed of exactly six elements</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?, T3?, T4?, T5?, T6?> Decode<T1, T2, T3, T4, T5, T6>(this IDynamicKeySubspace self, Slice packedKey)
		{
			return self.KeyEncoder.DecodeKey<T1, T2, T3, T4, T5, T6>(self.ExtractKey(packedKey));
		}

		/// <summary>Decode a key of this subspace, composed of exactly seven elements</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?> Decode<T1, T2, T3, T4, T5, T6, T7>(this IDynamicKeySubspace self, Slice packedKey)
		{
			return self.KeyEncoder.DecodeKey<T1, T2, T3, T4, T5, T6, T7>(self.ExtractKey(packedKey));
		}

		/// <summary>Decode a key of this subspace, and return only the first element without decoding the rest the key.</summary>
		/// <remarks>This method is faster than unpacking the complete key and reading only the first element.</remarks>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TFirst? DecodeFirst<TFirst>(this IDynamicKeySubspace self, Slice packedKey)
		{
			return self.KeyEncoder.DecodeKeyFirst<TFirst>(self.ExtractKey(packedKey));
		}

		/// <summary>Decode a key of this subspace, and return only the last element without decoding the rest.</summary>
		/// <remarks>This method is faster than unpacking the complete key and reading only the last element.</remarks>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TLast? DecodeLast<TLast>(this IDynamicKeySubspace self, Slice packedKey)
		{
			return self.KeyEncoder.DecodeKeyLast<TLast>(self.ExtractKey(packedKey));
		}

		/// <summary>Decode a key of this subspace, and return only the last two elements without decoding the rest.</summary>
		/// <remarks>This method is faster than unpacking the complete key and reading only the last elements.</remarks>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?) DecodeLast<T1, T2>(this IDynamicKeySubspace self, Slice packedKey)
		{
			return self.KeyEncoder.DecodeKeyLast<T1, T2>(self.ExtractKey(packedKey));
		}

		/// <summary>Decode a key of this subspace, and return only the last three elements without decoding the rest.</summary>
		/// <remarks>This method is faster than unpacking the complete key and reading only the last elements.</remarks>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?) DecodeLast<T1, T2, T3>(this IDynamicKeySubspace self, Slice packedKey)
		{
			return self.KeyEncoder.DecodeKeyLast<T1, T2, T3>(self.ExtractKey(packedKey));
		}

		#endregion

	}

	/// <summary>Partition helper for a dynamic TypeSystem</summary>
	[DebuggerDisplay("{Subspace.ToString(),nq}")]
	[PublicAPI]
	public sealed class DynamicPartition
	{

		public IDynamicKeySubspace Subspace { get; }

		internal DynamicPartition(DynamicKeySubspace subspace)
		{
			Contract.Debug.Requires(subspace != null);
			this.Subspace = subspace;
		}

		public IDynamicKeySubspace this[Slice binarySuffix]
		{
			[Pure]
			get => new DynamicKeySubspace(this.Subspace.Append(binarySuffix), this.Subspace.KeyEncoder, this.Subspace.Context);
		}

		public IDynamicKeySubspace this[IVarTuple suffix]
		{
			[Pure]
			get => new DynamicKeySubspace(this.Subspace.Pack(suffix), this.Subspace.KeyEncoder, this.Subspace.Context);
		}

		/// <summary>Partition this subspace into a child subspace</summary>
		/// <typeparam name="T">Type of the child subspace key</typeparam>
		/// <param name="value">Value of the child subspace</param>
		/// <returns>New subspace that is logically contained by the current subspace</returns>
		/// <remarks>Subspace([Foo, ]).Partition(Bar) is equivalent to Subspace([Foo, Bar, ])</remarks>
		/// <example>
		/// new FdbSubspace(["Users", ]).Partition("Contacts") == new FdbSubspace(["Users", "Contacts", ])
		/// </example>
		[Pure]
		public IDynamicKeySubspace ByKey<T>(T? value)
		{
			return new DynamicKeySubspace(this.Subspace.Encode<T>(value), this.Subspace.KeyEncoder, this.Subspace.Context);
		}

		/// <summary>Partition this subspace into a child subspace</summary>
		/// <typeparam name="T1">Type of the first subspace key</typeparam>
		/// <typeparam name="T2">Type of the second subspace key</typeparam>
		/// <param name="value1">Value of the first subspace key</param>
		/// <param name="value2">Value of the second subspace key</param>
		/// <returns>New subspace that is logically contained by the current subspace</returns>
		/// <remarks>Subspace([Foo, ]).Partition(Bar, Baz) is equivalent to Subspace([Foo, Bar, Baz])</remarks>
		/// <example>
		/// new FdbSubspace(["Users", ]).Partition("Contacts", "Friends") == new FdbSubspace(["Users", "Contacts", "Friends", ])
		/// </example>
		[Pure]
		public IDynamicKeySubspace ByKey<T1, T2>(T1? value1, T2? value2)
		{
			return new DynamicKeySubspace(this.Subspace.Encode<T1, T2>(value1, value2), this.Subspace.KeyEncoder, this.Subspace.Context);
		}

		/// <summary>Partition this subspace into a child subspace</summary>
		/// <typeparam name="T1">Type of the first subspace key</typeparam>
		/// <typeparam name="T2">Type of the second subspace key</typeparam>
		/// <typeparam name="T3">Type of the third subspace key</typeparam>
		/// <param name="value1">Value of the first subspace key</param>
		/// <param name="value2">Value of the second subspace key</param>
		/// <param name="value3">Value of the third subspace key</param>
		/// <returns>New subspace that is logically contained by the current subspace</returns>
		/// <example>
		/// new FdbSubspace(["Users", ]).Partition("John Smith", "Contacts", "Friends") == new FdbSubspace(["Users", "John Smith", "Contacts", "Friends", ])
		/// </example>
		[Pure]
		public IDynamicKeySubspace ByKey<T1, T2, T3>(T1? value1, T2? value2, T3? value3)
		{
			return new DynamicKeySubspace(this.Subspace.Encode<T1, T2, T3>(value1, value2, value3), this.Subspace.KeyEncoder, this.Subspace.Context);
		}

		/// <summary>Partition this subspace into a child subspace</summary>
		/// <typeparam name="T1">Type of the first subspace key</typeparam>
		/// <typeparam name="T2">Type of the second subspace key</typeparam>
		/// <typeparam name="T3">Type of the third subspace key</typeparam>
		/// <typeparam name="T4">Type of the fourth subspace key</typeparam>
		/// <param name="value1">Value of the first subspace key</param>
		/// <param name="value2">Value of the second subspace key</param>
		/// <param name="value3">Value of the third subspace key</param>
		/// <param name="value4">Value of the fourth subspace key</param>
		/// <returns>New subspace that is logically contained by the current subspace</returns>
		/// <example>
		/// new FdbSubspace(["Users", ]).Partition("John Smith", "Contacts", "Friends", "Messages") == new FdbSubspace(["Users", "John Smith", "Contacts", "Friends", "Messages", ])
		/// </example>
		[Pure]
		public IDynamicKeySubspace ByKey<T1, T2, T3, T4>(T1? value1, T2? value2, T3? value3, T4? value4)
		{
			return new DynamicKeySubspace(this.Subspace.Encode<T1, T2, T3, T4>(value1, value2, value3, value4), this.Subspace.KeyEncoder, this.Subspace.Context);
		}

	}

}
