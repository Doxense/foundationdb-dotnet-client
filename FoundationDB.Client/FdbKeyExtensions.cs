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

	/// <summary>Extension methods for working with <see cref="FdbKey{TKey,TEncoder}"/></summary>
	public static class FdbKeyExtensions
	{

		#region Transaction Set...

		/// <summary>
		/// Modify the database snapshot represented by transaction to change the given key to have the given value. If the given key was not previously present in the database it is inserted.
		/// The modification affects the actual database only if transaction is later committed with CommitAsync().
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key to be inserted into the database.</param>
		/// <param name="value">Value to be inserted into the database.</param>
		public static void Set<TKey>(this IFdbTransaction trans, TKey key, Slice value)
			where TKey : struct, IFdbKey
		{
			using var keyBytes = key.ToSlice(ArrayPool<byte>.Shared);

			trans.Set(keyBytes.Span, value);
		}

		/// <summary>
		/// Modify the database snapshot represented by transaction to change the given key to have the given value. If the given key was not previously present in the database it is inserted.
		/// The modification affects the actual database only if transaction is later committed with CommitAsync().
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key to be inserted into the database.</param>
		/// <param name="value">Value to be inserted into the database.</param>
		public static void Set<TKey>(this IFdbTransaction trans, TKey key, ReadOnlySpan<byte> value)
			where TKey : struct, IFdbKey
		{
			using var keyBytes = key.ToSlice(ArrayPool<byte>.Shared);

			trans.Set(keyBytes.Span, value);
		}

		/// <summary>
		/// Modify the database snapshot represented by transaction to change the given key to have the given value. If the given key was not previously present in the database it is inserted.
		/// The modification affects the actual database only if transaction is later committed with CommitAsync().
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key to be inserted into the database.</param>
		/// <param name="value">Value to be inserted into the database.</param>
		public static void Set<TValue>(this IFdbTransaction trans, Slice key, TValue value)
			where TValue: struct, IFdbValue
		{
			using var valueBytes = value.ToSlice(ArrayPool<byte>.Shared);

			trans.Set(FdbTransactionExtensions.ToSpanKey(key), valueBytes.Span);
		}

		/// <summary>
		/// Modify the database snapshot represented by transaction to change the given key to have the given value. If the given key was not previously present in the database it is inserted.
		/// The modification affects the actual database only if transaction is later committed with CommitAsync().
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key to be inserted into the database.</param>
		/// <param name="value">Value to be inserted into the database.</param>
		public static void Set<TValue>(this IFdbTransaction trans, ReadOnlySpan<byte> key, TValue value)
			where TValue : struct, IFdbValue
		{
			using var valueBytes = value.ToSlice(ArrayPool<byte>.Shared);

			trans.Set(key, valueBytes.Span);
		}

		/// <summary>
		/// Modify the database snapshot represented by transaction to change the given key to have the given value. If the given key was not previously present in the database it is inserted.
		/// The modification affects the actual database only if transaction is later committed with CommitAsync().
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key to be inserted into the database.</param>
		/// <param name="value">Value to be inserted into the database.</param>
		public static void Set<TKey, TValue>(this IFdbTransaction trans, TKey key, TValue value)
			where TKey : struct, IFdbKey
			where TValue : struct, IFdbValue
		{
			var pool = ArrayPool<byte>.Shared;

			using var keyBytes = key.ToSlice(pool);
			using var valueBytes = value.ToSlice(pool);

			trans.Set(keyBytes.Span, valueBytes.Span);
		}

		#endregion

		//EXPERIMENTAL

		#region TSubspace Keys...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKey<(TSubspace Subspace, TTuple Items), FdbKey.DynamicSubspaceTupleEncoder<TSubspace, TTuple>> PackKey<TSubspace, TTuple>(this TSubspace subspace, TTuple key)
			where TSubspace : IDynamicKeySubspace
			where TTuple : IVarTuple
		{
			return new((subspace, key), subspace);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKey<(ITypedKeySubspace<T1> Subspace, T1 Key), FdbKey.TypedSubspaceEncoder<T1>> GetKey<T1>(this ITypedKeySubspace<T1> subspace, T1 key1)
		{
			return new((subspace, key1), subspace);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKey<(TSubspace Subspace, STuple<T1, T2> Items), FdbKey.DynamicSubspaceTupleEncoder<TSubspace, STuple<T1, T2>>> PackKey<TSubspace, T1, T2>(this TSubspace subspace, ValueTuple<T1, T2> key)
			where TSubspace : IDynamicKeySubspace
		{
			return new((subspace, key), subspace);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKey<(TSubspace Subspace, TTuple Items), FdbKey.DynamicSubspaceTupleEncoder<TSubspace, TTuple>>[] PackKeys<TSubspace, TTuple>(this TSubspace subspace, ReadOnlySpan<TTuple> keys)
			where TSubspace : IDynamicKeySubspace
			where TTuple : IVarTuple
		{
			if (keys.Length == 0)
			{
				return [ ];
			}

			var res = new FdbKey<(TSubspace Subspace, TTuple Items), FdbKey.DynamicSubspaceTupleEncoder<TSubspace, TTuple>>[keys.Length];
			for (int i = 0; i < keys.Length; i++)
			{
				res[i] = new((subspace, keys[i]), subspace);
			}
			return res;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<FdbKey<(TSubspace Subspace, TTuple Items), FdbKey.DynamicSubspaceTupleEncoder<TSubspace, TTuple>>> PackKeys<TSubspace, TTuple>(this TSubspace subspace, IEnumerable<TTuple> keys)
			where TSubspace : IDynamicKeySubspace
			where TTuple : IVarTuple
		{
			return keys.TryGetSpan(out var span)
				? PackKeys(subspace, span)
				: GetKeysSlow(subspace, keys);

			static IEnumerable<FdbKey<(TSubspace Subspace, TTuple Items), FdbKey.DynamicSubspaceTupleEncoder<TSubspace, TTuple>>> GetKeysSlow(TSubspace subspace, IEnumerable<TTuple> keys)
			{
				foreach (var key in keys)
				{
					yield return new((subspace, key), subspace);
				}
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKey<(TSubspace Subspace, TTuple Items), FdbKey.DynamicSubspaceTupleEncoder<TSubspace, TTuple>>[] PackKeys<TSubspace, TItem, TTuple>(this TSubspace subspace, ReadOnlySpan<TItem> items, Func<TItem, TTuple> selector)
			where TSubspace : IDynamicKeySubspace
			where TTuple : IVarTuple
		{
			if (items.Length == 0)
			{
				return [ ];
			}

			var res = new FdbKey<(TSubspace Subspace, TTuple Items), FdbKey.DynamicSubspaceTupleEncoder<TSubspace, TTuple>>[items.Length];
			for(int i = 0; i < items.Length; i++)
			{
				res[i] = new((subspace, selector(items[i])), subspace);
			}
			return res;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<FdbKey<(TSubspace Subspace, TTuple Items), FdbKey.DynamicSubspaceTupleEncoder<TSubspace, TTuple>>> PackKeys<TSubspace, TItem, TTuple>(this TSubspace subspace, IEnumerable<TItem> items, Func<TItem, TTuple> selector)
			where TSubspace : IDynamicKeySubspace
			where TTuple : IVarTuple
		{
			return items.TryGetSpan(out var span)
				? PackKeys(subspace, span, selector)
				: EncodeSlow(subspace, items, selector);

			static IEnumerable<FdbKey<(TSubspace Subspace, TTuple Items), FdbKey.DynamicSubspaceTupleEncoder<TSubspace, TTuple>>> EncodeSlow(TSubspace subspace, IEnumerable<TItem> items, Func<TItem, TTuple> selector)
			{
				foreach (var item in items)
				{
					yield return new((subspace, selector(item)), subspace);
				}
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static FdbKey<(TSubspace Subspace, TTuple Items), FdbKey.DynamicSubspaceTupleEncoder<TSubspace, TTuple>>[] PackKeys<TSubspace, TItem, TState, TTuple>(this TSubspace subspace, ReadOnlySpan<TItem> items, TState state, Func<TState, TItem, TTuple> selector)
			where TSubspace : IDynamicKeySubspace
			where TTuple : IVarTuple
		{
			if (items.Length == 0)
			{
				return [ ];
			}

			var res = new FdbKey<(TSubspace Subspace, TTuple Items), FdbKey.DynamicSubspaceTupleEncoder<TSubspace, TTuple>>[items.Length];
			for(int i = 0; i < items.Length; i++)
			{
				res[i] = new((subspace, selector(state, items[i])), subspace);
			}
			return res;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static IEnumerable<FdbKey<(TSubspace Subspace, TTuple Items), FdbKey.DynamicSubspaceTupleEncoder<TSubspace, TTuple>>> PackKeys<TSubspace, TItem, TState, TTuple>(this TSubspace subspace, IEnumerable<TItem> items, TState state, Func<TState, TItem, TTuple> selector)
			where TSubspace : IDynamicKeySubspace
			where TTuple : IVarTuple
		{
			return items.TryGetSpan(out var span)
				? PackKeys(subspace, span, state, selector)
				: EncodeSlow(subspace, items, state, selector);

			static IEnumerable<FdbKey<(TSubspace Subspace, TTuple Items), FdbKey.DynamicSubspaceTupleEncoder<TSubspace, TTuple>>> EncodeSlow(TSubspace subspace, IEnumerable<TItem> items, TState state, Func<TState, TItem, TTuple> selector)
			{
				foreach (var item in items)
				{
					yield return new((subspace, selector(state, item)), subspace);
				}
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<FdbKey<(TSubspace Subspace, STuple<T1> Items), FdbKey.DynamicSubspaceTupleEncoder<TSubspace, STuple<T1>>>> PackKeys<TSubspace, TItem, T1>(this TSubspace subspace, IEnumerable<TItem> items, Func<TItem, ValueTuple<T1>> selector)
			where TSubspace : IDynamicKeySubspace
		{
			return PackKeys(subspace, items, selector, (fn, item) => fn(item).ToSTuple());
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<FdbKey<(TSubspace Subspace, STuple<T1, T2> Items), FdbKey.DynamicSubspaceTupleEncoder<TSubspace, STuple<T1, T2>>>> PackKeys<TSubspace, TItem, T1, T2>(this TSubspace subspace, IEnumerable<TItem> items, Func<TItem, ValueTuple<T1, T2>> selector)
			where TSubspace : IDynamicKeySubspace
		{
			return PackKeys(subspace, items, selector, (fn, item) => fn(item).ToSTuple());
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<FdbKey<(TSubspace Subspace, STuple<T1, T2, T3> Items), FdbKey.DynamicSubspaceTupleEncoder<TSubspace, STuple<T1, T2, T3>>>> PackKeys<TSubspace, TItem, T1, T2, T3>(this TSubspace subspace, IEnumerable<TItem> items, Func<TItem, ValueTuple<T1, T2, T3>> selector)
			where TSubspace : IDynamicKeySubspace
		{
			return PackKeys(subspace, items, selector, (fn, item) => fn(item).ToSTuple());
		}

		#endregion

	}

}
