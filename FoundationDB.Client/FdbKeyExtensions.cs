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

		/// <summary>
		/// Modify the database snapshot represented by transaction to change the given key to have the given value. If the given key was not previously present in the database it is inserted.
		/// The modification affects the actual database only if transaction is later committed with CommitAsync().
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key to be inserted into the database.</param>
		/// <param name="value">Value to be inserted into the database.</param>
		public static void Set<TKey, TKeyEncoder>(this IFdbTransaction trans, FdbKey<TKey, TKeyEncoder> key, Slice value)
			where TKeyEncoder: struct, ISpanEncoder<TKey>
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
		public static void Set<TKey, TKeyEncoder>(this IFdbTransaction trans, FdbKey<TKey, TKeyEncoder> key, ReadOnlySpan<byte> value)
			where TKeyEncoder: struct, ISpanEncoder<TKey>
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
		public static void Set<TValue, TValueEncoder>(this IFdbTransaction trans, Slice key, FdbValue<TValue, TValueEncoder> value)
			where TValueEncoder: struct, ISpanEncoder<TValue>
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
		public static void Set<TValue, TValueEncoder>(this IFdbTransaction trans, ReadOnlySpan<byte> key, FdbValue<TValue, TValueEncoder> value)
			where TValueEncoder: struct, ISpanEncoder<TValue>
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
		public static void Set<TKey, TKeyEncoder, TValue, TValueEncoder>(this IFdbTransaction trans, FdbKey<TKey, TKeyEncoder> key, FdbValue<TValue, TValueEncoder> value)
			where TKeyEncoder: struct, ISpanEncoder<TKey>
			where TValueEncoder: struct, ISpanEncoder<TValue>
		{
			var pool = ArrayPool<byte>.Shared;

			using var keyBytes = key.ToSlice(pool);
			using var valueBytes = value.ToSlice(pool);

			trans.Set(keyBytes.Span, valueBytes.Span);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKey<(TSubspace Subspace, TTuple Items), FdbKey.SubspaceTupleEncoder<TSubspace, TTuple>> MakeKey<TSubspace, TTuple>(this TSubspace subspace, TTuple items)
			where TSubspace : IDynamicKeySubspace
			where TTuple : IVarTuple
		{
			return new((subspace, items), subspace);
		}

	}

}
