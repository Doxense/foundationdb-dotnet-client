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

namespace FoundationDB.Client
{
	using System;

	/// <summary>Extension methods for working with <see cref="FdbKey{TKey,TEncoder}"/></summary>
	public static class FdbKeyExtensions
	{

		#region Encoding...

		/// <summary>Returns a pre-encoded version of a key</summary>
		/// <typeparam name="TKey">Type of the key to pre-encode</typeparam>
		/// <param name="key">Key to pre-encoded</param>
		/// <returns>Key with a cached version of the encoded original</returns>
		/// <remarks>This key can be used multiple times without re-encoding the original</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbRawKey Memoize<TKey>(this TKey key)
			where TKey : struct, IFdbKey
		{
			if (typeof(TKey) == typeof(FdbRawKey))
			{ // already cached!
				return (FdbRawKey) (object) key;
			}

			return new(key.ToSlice());
		}

		/// <summary>Encodes this key into <see cref="Slice"/></summary>
		/// <param name="key">Key to encode</param>
		/// <returns><see cref="Slice"/> that contains the binary representation of this key</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice ToSlice<TKey>(this TKey key)
			where TKey : struct, IFdbKey
		{
			if (typeof(TKey) == typeof(FdbRawKey))
			{
				return ((FdbRawKey) (object) key).Data;
			}

			if (key.TryGetSpan(out var span))
			{
				return Slice.FromBytes(span);
			}

			return ToSliceSlow(in key);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static Slice ToSliceSlow(in TKey key)
			{
				byte[]? tmp = null;
				if (key.TryGetSizeHint(out var capacity))
				{
					// we will hope for the best, and pre-allocate the slice

					tmp = new byte[capacity];
					if (key.TryEncode(tmp, out var bytesWritten))
					{
						return tmp.AsSlice(0, bytesWritten);
					}

					if (capacity >= FdbKey.MaxSize)
					{
						goto key_too_long;
					}

					capacity *= 2;
				}
				else
				{
					capacity = 128;
				}

				var pool = ArrayPool<byte>.Shared;
				try
				{
					while (true)
					{
						tmp = pool.Rent(capacity);
						if (key.TryEncode(tmp, out int bytesWritten))
						{
							return tmp.AsSlice(0, bytesWritten).Copy();
						}

						pool.Return(tmp);
						tmp = null;

						if (capacity >= FdbKey.MaxSize)
						{
							goto key_too_long;
						}

						capacity *= 2;
					}
				}
				catch (Exception)
				{
					if (tmp is not null)
					{
						pool.Return(tmp);
					}

					throw;
				}

			key_too_long:
				// it would be too large anyway!
				throw new ArgumentException("Cannot encode key because it would exceed the maximum allowed length.");
			}
		}

		/// <summary>Encodes this key into <see cref="Slice"/>, using backing buffer rented from a pool</summary>
		/// <param name="key">Key to encode</param>
		/// <param name="pool">Pool used to rent the buffer (<see cref="ArrayPool{T}.Shared"/> is <c>null</c>)</param>
		/// <returns><see cref="SliceOwner"/> that contains the binary representation of this key</returns>
		[MustDisposeResource, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SliceOwner ToSlice<TKey>(this TKey key, ArrayPool<byte>? pool)
			where TKey : struct, IFdbKey
		{
			if (typeof(TKey) == typeof(FdbRawKey))
			{
				return SliceOwner.Wrap(((FdbRawKey) (object) key).Data);
			}

			pool ??= ArrayPool<byte>.Shared;
			return key.TryGetSpan(out var span)
				? SliceOwner.Copy(span, pool)
				: Encode(in key, pool);
		}

		[MustDisposeResource, MethodImpl(MethodImplOptions.NoInlining)]
		internal static SliceOwner Encode<TKey>(in TKey key, ArrayPool<byte>? pool, int? sizeHint = null)
			where TKey : struct, IFdbKey
		{
			Contract.Debug.Requires(pool != null);

			int capacity;
			if (sizeHint is not null)
			{
				capacity = sizeHint.Value;
			}
			else if (!key.TryGetSizeHint(out capacity))
			{
				capacity = 0;
			}
			if (capacity <= 0)
			{
				capacity = 128;
			}

			byte[]? tmp = null;
			try
			{
				while (true)
				{
					tmp = pool.Rent(capacity);
					if (key.TryEncode(tmp, out int bytesWritten))
					{
						if (bytesWritten == 0)
						{
							pool.Return(tmp);
							tmp = null;
							return SliceOwner.Empty;
						}

						return SliceOwner.Create(tmp.AsSlice(0, bytesWritten), pool);
					}

					pool.Return(tmp);
					tmp = null;

					if (capacity >= FdbKey.MaxSize)
					{
						// it would be too large anyway!
						throw new ArgumentException("Cannot encode key because it would exceed the maximum allowed length.");
					}
					capacity *= 2;
				}
			}
			catch(Exception)
			{
				if (tmp is not null)
				{
					pool.Return(tmp);
				}
				throw;
			}
		}

		[MustUseReturnValue, MethodImpl(MethodImplOptions.NoInlining)]
		internal static ReadOnlySpan<byte> Encode<TKey>(scoped in TKey key, scoped ref byte[]? buffer, ArrayPool<byte>? pool)
			where TKey : struct, IFdbKey
		{
			Contract.Debug.Requires(pool != null);

			if (!key.TryGetSizeHint(out int capacity) || capacity <= 0)
			{
				capacity = 128;
			}

			while (true)
			{
				if (buffer is null)
				{
					buffer = pool.Rent(capacity);
				}
				else if (buffer.Length < capacity)
				{
					pool.Return(buffer);
					buffer = pool.Rent(capacity);
				}

				if (key.TryEncode(buffer, out int bytesWritten))
				{
					return bytesWritten > 0 ? buffer.AsSpan(0, bytesWritten) : default;
				}

				if (capacity >= FdbKey.MaxSize)
				{
					// it would be too large anyway!
					throw new ArgumentException("Cannot encode key because it would exceed the maximum allowed length.");
				}
				capacity *= 2;
			}
		}

		#endregion

		#region TSubspace Keys...

		#region IDynamicKeySubspace.GetKey(...)...

		/// <summary>Returns the key for this subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbBinaryKey GetKey(this IDynamicKeySubspace subspace) => new(subspace, Slice.Empty);

		/// <summary>Returns a key under this subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">value of the single element in the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1> GetKey<T1>(this IDynamicKeySubspace subspace, T1 item1) => new(subspace, item1);

		/// <summary>Returns a key with 2 elements under this subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">value of the 1st element in the key</param>
		/// <param name="item2">value of the 2nd element in the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2> GetKey<T1, T2>(this IDynamicKeySubspace subspace, T1 item1, T2 item2) => new(subspace, item1, item2);

		/// <summary>Returns a key with 3 elements under this subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">value of the 1st element in the key</param>
		/// <param name="item2">value of the 2nd element in the key</param>
		/// <param name="item3">value of the 3rd element in the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3> GetKey<T1, T2, T3>(this IDynamicKeySubspace subspace, T1 item1, T2 item2, T3 item3) => new(subspace, item1, item2, item3);

		/// <summary>Returns a key with 4 elements under this subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">value of the 1st element in the key</param>
		/// <param name="item2">value of the 2nd element in the key</param>
		/// <param name="item3">value of the 3rd element in the key</param>
		/// <param name="item4">value of the 4th element in the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4> GetKey<T1, T2, T3, T4>(this IDynamicKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4) => new(subspace, item1, item2, item3, item4);

		/// <summary>Returns a key with 5 elements under this subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">value of the 1st element in the key</param>
		/// <param name="item2">value of the 2nd element in the key</param>
		/// <param name="item3">value of the 3rd element in the key</param>
		/// <param name="item4">value of the 4th element in the key</param>
		/// <param name="item5">value of the 5th element in the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5> GetKey<T1, T2, T3, T4, T5>(this IDynamicKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5) => new(subspace, item1, item2, item3, item4, item5);

		/// <summary>Returns a key with 6 elements under this subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">value of the 1st element in the key</param>
		/// <param name="item2">value of the 2nd element in the key</param>
		/// <param name="item3">value of the 3rd element in the key</param>
		/// <param name="item4">value of the 4th element in the key</param>
		/// <param name="item5">value of the 5th element in the key</param>
		/// <param name="item6">value of the 6th element in the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6> GetKey<T1, T2, T3, T4, T5, T6>(this IDynamicKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6) => new(subspace, item1, item2, item3, item4, item5, item6);

		/// <summary>Returns a key with 7 elements under this subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">value of the 1st element in the key</param>
		/// <param name="item2">value of the 2nd element in the key</param>
		/// <param name="item3">value of the 3rd element in the key</param>
		/// <param name="item4">value of the 4th element in the key</param>
		/// <param name="item5">value of the 5th element in the key</param>
		/// <param name="item6">value of the 6th element in the key</param>
		/// <param name="item7">value of the 7th element in the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> GetKey<T1, T2, T3, T4, T5, T6, T7>(this IDynamicKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7) => new(subspace, item1, item2, item3, item4, item5, item6, item7);

		/// <summary>Returns a key with 8 elements under this subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">value of the 1st element in the key</param>
		/// <param name="item2">value of the 2nd element in the key</param>
		/// <param name="item3">value of the 3rd element in the key</param>
		/// <param name="item4">value of the 4th element in the key</param>
		/// <param name="item5">value of the 5th element in the key</param>
		/// <param name="item6">value of the 6th element in the key</param>
		/// <param name="item7">value of the 7th element in the key</param>
		/// <param name="item8">value of the 8th element in the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> GetKey<T1, T2, T3, T4, T5, T6, T7, T8>(this IDynamicKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8) => new(subspace, item1, item2, item3, item4, item5, item6, item7, item8);

		#endregion

		#region IKeySubspace.PackKey(ValueTuple<...>)...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1> PackKey<T1>(this IKeySubspace subspace, ValueTuple<T1> key) => new(subspace, key.Item1);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2> PackKey<T1, T2>(this IKeySubspace subspace, in ValueTuple<T1, T2> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3> PackKey<T1, T2, T3>(this IKeySubspace subspace, in ValueTuple<T1, T2, T3> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4> PackKey<T1, T2, T3, T4>(this IKeySubspace subspace, in ValueTuple<T1, T2, T3, T4> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5> PackKey<T1, T2, T3, T4, T5>(this IKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6> PackKey<T1, T2, T3, T4, T5, T6>(this IKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5, T6> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> PackKey<T1, T2, T3, T4, T5, T6, T7>(this IKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5, T6, T7> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> PackKey<T1, T2, T3, T4, T5, T6, T7, T8>(this IKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5, T6, T7, ValueTuple<T8>> key) => new(subspace, in key);

		#endregion

		#region IKeySubspace.PackKey(STuple<...>)...

		/// <summary>Returns a key that packs the given items under this subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="items">elements of the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbVarTupleKey PackKey(this IKeySubspace subspace, IVarTuple items) => new(subspace, items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1> PackKey<T1>(this IKeySubspace subspace, STuple<T1> key) => new(subspace, key.Item1);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2> PackKey<T1, T2>(this IKeySubspace subspace, in STuple<T1, T2> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3> PackKey<T1, T2, T3>(this IKeySubspace subspace, in STuple<T1, T2, T3> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4> PackKey<T1, T2, T3, T4>(this IKeySubspace subspace, in STuple<T1, T2, T3, T4> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5> PackKey<T1, T2, T3, T4, T5>(this IKeySubspace subspace, in STuple<T1, T2, T3, T4, T5> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6> PackKey<T1, T2, T3, T4, T5, T6>(this IKeySubspace subspace, in STuple<T1, T2, T3, T4, T5, T6> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> PackKey<T1, T2, T3, T4, T5, T6, T7>(this IKeySubspace subspace, in STuple<T1, T2, T3, T4, T5, T6, T7> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> PackKey<T1, T2, T3, T4, T5, T6, T7, T8>(this IKeySubspace subspace, in STuple<T1, T2, T3, T4, T5, T6, T7, T8> key) => new(subspace, in key);

		#endregion

		#region ITypedKeySubspace<...>.GetKey(...)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1> GetKey<T1>(this ITypedKeySubspace<T1> subspace, T1 item1) => new(subspace, item1);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2> GetKey<T1, T2>(this ITypedKeySubspace<T1, T2> subspace, T1 item1, T2 item2) => new(subspace, item1, item2);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3> GetKey<T1, T2, T3>(this ITypedKeySubspace<T1, T2, T3> subspace, T1 item1, T2 item2, T3 item3) => new(subspace, item1, item2, item3);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4> GetKey<T1, T2, T3, T4>(this ITypedKeySubspace<T1, T2, T3, T4> subspace, T1 item1, T2 item2, T3 item3, T4 item4) => new(subspace, item1, item2, item3, item4);

		#endregion

		#region ITypedKeySubspace<...>.PackKey(ValueTuple<...>)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1> PackKey<T1>(this ITypedKeySubspace<T1> subspace, ValueTuple<T1> items) => new(subspace, items.Item1);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2> PackKey<T1, T2>(this ITypedKeySubspace<T1, T2> subspace, in ValueTuple<T1, T2> items) => new(subspace, in items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3> PackKey<T1, T2, T3>(this ITypedKeySubspace<T1, T2, T3> subspace, in ValueTuple<T1, T2, T3> items) => new(subspace, in items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4> PackKey<T1, T2, T3, T4>(this ITypedKeySubspace<T1, T2, T3, T4> subspace, in ValueTuple<T1, T2, T3, T4> items) => new(subspace, in items);

		#endregion

		#region ITypedKeySubspace<...>.PackKey(STuple<...>)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1> PackKey<T1>(this ITypedKeySubspace<T1> subspace, STuple<T1> items) => new(subspace, items.Item1);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2> PackKey<T1, T2>(this ITypedKeySubspace<T1, T2> subspace, in STuple<T1, T2> items) => new(subspace, in items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3> PackKey<T1, T2, T3>(this ITypedKeySubspace<T1, T2, T3> subspace, in STuple<T1, T2, T3> items) => new(subspace, in items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4> PackKey<T1, T2, T3, T4>(this ITypedKeySubspace<T1, T2, T3, T4> subspace, in STuple<T1, T2, T3, T4> items) => new(subspace, in items);

		#endregion

		#region IBinaryKeySubspace.AppendKey(...)

		[Pure]
		public static FdbBinaryKey AppendKey(this IBinaryKeySubspace subspace, Slice relativeKey)
		{
			Contract.NotNull(subspace);
			return new(subspace, relativeKey);
		}

		[Pure]
		public static FdbBinaryKey AppendKey(this IBinaryKeySubspace subspace, byte[]? relativeKey)
		{
			Contract.NotNull(subspace);
			return new(subspace, relativeKey.AsSlice());
		}

		#endregion

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbVarTupleKey[] PackKeys(this IDynamicKeySubspace subspace, ReadOnlySpan<IVarTuple> keys)
		{
			if (keys.Length == 0)
			{
				return [ ];
			}

			var res = new FdbVarTupleKey[keys.Length];
			for (int i = 0; i < keys.Length; i++)
			{
				res[i] = new(subspace, keys[i]);
			}
			return res;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<FdbVarTupleKey> PackKeys(this IDynamicKeySubspace subspace, IEnumerable<IVarTuple> keys)
		{
			return keys.TryGetSpan(out var span)
				? PackKeys(subspace, span)
				: GetKeysSlow(subspace, keys);

			static IEnumerable<FdbVarTupleKey> GetKeysSlow(IDynamicKeySubspace subspace, IEnumerable<IVarTuple> keys)
			{
				foreach (var key in keys)
				{
					yield return new(subspace, key);
				}
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbVarTupleKey[] PackKeys<TItem>(this IDynamicKeySubspace subspace, ReadOnlySpan<TItem> items, Func<TItem, IVarTuple> selector)
		{
			if (items.Length == 0)
			{
				return [ ];
			}

			var res = new FdbVarTupleKey[items.Length];
			for(int i = 0; i < items.Length; i++)
			{
				res[i] = new(subspace, selector(items[i]));
			}
			return res;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<FdbVarTupleKey> PackKeys<TItem>(this IDynamicKeySubspace subspace, IEnumerable<TItem> items, Func<TItem, IVarTuple> selector)
		{
			return items.TryGetSpan(out var span)
				? PackKeys(subspace, span, selector)
				: EncodeSlow(subspace, items, selector);

			static IEnumerable<FdbVarTupleKey> EncodeSlow(IDynamicKeySubspace subspace, IEnumerable<TItem> items, Func<TItem, IVarTuple> selector)
			{
				foreach (var item in items)
				{
					yield return new(subspace, selector(item));
				}
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbVarTupleKey[] PackKeys<TItem, TState>(this IDynamicKeySubspace subspace, ReadOnlySpan<TItem> items, TState state, Func<TState, TItem, IVarTuple> selector)
		{
			if (items.Length == 0)
			{
				return [ ];
			}

			var res = new FdbVarTupleKey[items.Length];
			for(int i = 0; i < items.Length; i++)
			{
				res[i] = new(subspace, selector(state, items[i]));
			}
			return res;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<FdbVarTupleKey> PackKeys<TItem, TState>(this IDynamicKeySubspace subspace, IEnumerable<TItem> items, TState state, Func<TState, TItem, IVarTuple> selector)
		{
			return items.TryGetSpan(out var span)
				? PackKeys(subspace, span, state, selector)
				: EncodeSlow(subspace, items, state, selector);

			static IEnumerable<FdbVarTupleKey> EncodeSlow(IDynamicKeySubspace subspace, IEnumerable<TItem> items, TState state, Func<TState, TItem, IVarTuple> selector)
			{
				foreach (var item in items)
				{
					yield return new(subspace, selector(state, item));
				}
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<FdbTupleKey<T1>> PackKeys<TItem, T1>(this IDynamicKeySubspace subspace, IEnumerable<TItem> items, Func<TItem, ValueTuple<T1>> selector)
		{
			foreach (var item in items)
			{
				yield return new(subspace, selector(item));
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<FdbTupleKey<T1, T2>> PackKeys<TItem, T1, T2>(this IDynamicKeySubspace subspace, IEnumerable<TItem> items, Func<TItem, ValueTuple<T1, T2>> selector)
		{
			foreach (var item in items)
			{
				yield return new(subspace, selector(item));
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<FdbTupleKey<T1, T2, T3>> PackKeys<TItem, T1, T2, T3>(this IDynamicKeySubspace subspace, IEnumerable<TItem> items, Func<TItem, ValueTuple<T1, T2, T3>> selector)
		{
			foreach (var item in items)
			{
				yield return new(subspace, selector(item));
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<FdbTupleKey<T1, T2, T3, T4>> PackKeys<TItem, T1, T2, T3, T4>(this IDynamicKeySubspace subspace, IEnumerable<TItem> items, Func<TItem, ValueTuple<T1, T2, T3, T4>> selector)
		{
			foreach (var item in items)
			{
				yield return new(subspace, selector(item));
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<FdbTupleKey<T1, T2, T3, T4, T5>> PackKeys<TItem, T1, T2, T3, T4, T5>(this IDynamicKeySubspace subspace, IEnumerable<TItem> items, Func<TItem, ValueTuple<T1, T2, T3, T4, T5>> selector)
		{
			foreach (var item in items)
			{
				yield return new(subspace, selector(item));
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<FdbTupleKey<T1, T2, T3, T4, T5, T6>> PackKeys<TItem, T1, T2, T3, T4, T5, T6>(this IDynamicKeySubspace subspace, IEnumerable<TItem> items, Func<TItem, ValueTuple<T1, T2, T3, T4, T5, T6>> selector)
		{
			foreach (var item in items)
			{
				yield return new(subspace, selector(item));
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<FdbTupleKey<T1, T2, T3, T4, T5, T6, T7>> PackKeys<TItem, T1, T2, T3, T4, T5, T6, T7>(this IDynamicKeySubspace subspace, IEnumerable<TItem> items, Func<TItem, ValueTuple<T1, T2, T3, T4, T5, T6, T7>> selector)
		{
			foreach (var item in items)
			{
				yield return new(subspace, selector(item));
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8>> PackKeys<TItem, T1, T2, T3, T4, T5, T6, T7, T8>(this IDynamicKeySubspace subspace, IEnumerable<TItem> items, Func<TItem, ValueTuple<T1, T2, T3, T4, T5, T6, T7, ValueTuple<T8>>> selector)
		{
			foreach (var item in items)
			{
				yield return new(subspace, selector(item));
			}
		}

		#endregion

	}

}
