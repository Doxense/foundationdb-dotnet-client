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

	/// <summary>Extensions methods and helpers to work with Key Subspaces</summary>
	[PublicAPI]
	public static class KeySubspaceExtensions
	{

		#region Encodings...

		/// <summary>Return a version of this subspace, which uses a different type system to produces the keys and values</summary>
		/// <param name="subspace">Instance of a generic subspace</param>
		/// <param name="context">If non-null, overrides the current subspace context.</param>
		/// <returns>Subspace equivalent to <paramref name="subspace"/>, but augmented with a specific TypeSystem</returns>
		[Pure]
		public static IBinaryKeySubspace AsBinary(this IKeySubspace subspace, ISubspaceContext? context = null)
		{
			Contract.NotNull(subspace);
			if (subspace is IBinaryKeySubspace bin && (context == null || context == bin.Context))
			{ // already a binary subspace
				return bin;
			}
			return new BinaryKeySubspace(subspace.GetPrefix(), context ?? subspace.Context);
		}

		/// <summary>Return a version of this subspace, which uses a different type system to produces the keys and values</summary>
		/// <param name="subspace">Instance of a generic subspace</param>
		/// <param name="context">If non-null, overrides the current subspace context.</param>
		/// <returns>Subspace equivalent to <paramref name="subspace"/>, but augmented with a specific TypeSystem</returns>
		[Pure]
		public static IDynamicKeySubspace AsDynamic(this IKeySubspace subspace, ISubspaceContext? context = null)
		{
			Contract.NotNull(subspace);
			if (subspace is IDynamicKeySubspace dyn && (context == null || context == dyn.Context))
			{ // already a dynamic subspace
				return dyn;
			}
			return new DynamicKeySubspace(subspace.GetPrefix(), context ?? subspace.Context);
		}

		/// <summary>Return a version of this subspace, which uses a different type system to produces the keys and values</summary>
		/// <param name="subspace">Instance of a generic subspace</param>
		/// <param name="encoding">If non-null, uses this specific instance of the TypeSystem. If null, uses the default instance for this particular TypeSystem</param>
		/// <param name="context">If non-null, overrides the current subspace context.</param>
		/// <returns>Subspace equivalent to <paramref name="subspace"/>, but augmented with a specific TypeSystem</returns>
		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static IDynamicKeySubspace AsDynamic(this IKeySubspace subspace, IKeyEncoding encoding, ISubspaceContext? context = null)
		{
			Contract.NotNull(subspace);
			Contract.NotNull(encoding);

			if (subspace is IDynamicKeySubspace dyn && (context == null || context == dyn.Context) && (encoding == dyn.KeyEncoder.Encoding))
			{ // already a dynamic subspace
				return dyn;
			}
			return new DynamicKeySubspace(subspace.GetPrefix(), encoding.GetDynamicKeyEncoder(), context ?? subspace.Context);
		}

		/// <summary>Return a version of this subspace, which uses a different type system to produces the keys and values</summary>
		/// <param name="subspace">Instance of a generic subspace to extend</param>
		/// <param name="context">If non-null, overrides the current subspace context.</param>
		/// <returns>Subspace equivalent to <paramref name="subspace"/>, but augmented with a specific TypeSystem</returns>
		[Pure]
		public static ITypedKeySubspace<T> AsTyped<T>(this IKeySubspace subspace, ISubspaceContext? context = null)
		{
			Contract.NotNull(subspace);
			if (subspace is ITypedKeySubspace<T> typed && (context == null || context == typed.Context))
			{ // already a typed subspace
				return typed;
			}
			return new TypedKeySubspace<T>(subspace.GetPrefix(), context ?? subspace.Context);
		}

		/// <summary>Return a version of this subspace, which uses a different type system to produces the keys and values</summary>
		/// <param name="subspace">Instance of a generic subspace to extend</param>
		/// <param name="encoding">Encoding by the keys of this subspace. If not specified, the <see cref="TuPack">Tuple Encoding</see> will be used to generate an encoder.</param>
		/// <param name="context">If non-null, overrides the current subspace context.</param>
		/// <returns>Subspace equivalent to <paramref name="subspace"/>, but augmented with a specific TypeSystem</returns>
		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static ITypedKeySubspace<T> AsTyped<T>(this IKeySubspace subspace, IKeyEncoding encoding, ISubspaceContext? context = null)
		{
			Contract.NotNull(subspace);
			Contract.NotNull(encoding);

			if (subspace is ITypedKeySubspace<T> typed && (context == null || context == typed.Context))
			{ // already a typed subspace
				return typed;
			}
			return new TypedKeySubspace<T>(subspace.GetPrefix(), encoding.GetKeyEncoder<T>(), context ?? subspace.Context);
		}

		/// <summary>Return a version of this subspace, which uses a different type system to produces the keys and values</summary>
		/// <param name="subspace">Instance of a generic subspace to extend</param>
		/// <param name="context">If non-null, overrides the current subspace context.</param>
		/// <returns>Subspace equivalent to <paramref name="subspace"/>, but augmented with a specific TypeSystem</returns>
		[Pure]
		public static ITypedKeySubspace<T1, T2> AsTyped<T1, T2>(this IKeySubspace subspace, ISubspaceContext? context = null)
		{
			Contract.NotNull(subspace);
			if (subspace is ITypedKeySubspace<T1, T2> typed && (context == null || context == typed.Context))
			{ // already a typed subspace
				return typed;
			}
			return new TypedKeySubspace<T1, T2>(subspace.GetPrefix(), context ?? subspace.Context);
		}

		/// <summary>Return a version of this subspace, which uses a different type system to produces the keys and values</summary>
		/// <param name="subspace">Instance of a generic subspace to extend</param>
		/// <param name="encoding">Encoding used by the keys of this subspace. If not specified, the <see cref="TuPack">Tuple Encoding</see> will be used to generate an encoder.</param>
		/// <param name="context">If non-null, overrides the current subspace context.</param>
		/// <returns>Subspace equivalent to <paramref name="subspace"/>, but augmented with a specific TypeSystem</returns>
		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static ITypedKeySubspace<T1, T2> AsTyped<T1, T2>(this IKeySubspace subspace, IKeyEncoding encoding, ISubspaceContext? context = null)
		{
			Contract.NotNull(subspace);
			Contract.NotNull(encoding);
			if (subspace is ITypedKeySubspace<T1, T2> typed && (context == null || context == typed.Context))
			{ // already a typed subspace
				return typed;
			}
			return new TypedKeySubspace<T1, T2>(subspace.GetPrefix(), encoding.GetKeyEncoder<T1, T2>(), context ?? subspace.Context);
		}

		/// <summary>Return a version of this subspace, which uses a different type system to produces the keys and values</summary>
		/// <param name="subspace">Instance of a generic subspace to extend</param>
		/// <param name="context">If non-null, overrides the current subspace context.</param>
		/// <returns>Subspace equivalent to <paramref name="subspace"/>, but augmented with a specific TypeSystem</returns>
		[Pure]
		public static ITypedKeySubspace<T1, T2, T3> AsTyped<T1, T2, T3>(this IKeySubspace subspace, ISubspaceContext? context = null)
		{
			Contract.NotNull(subspace);
			if (subspace is ITypedKeySubspace<T1, T2, T3> typed && (context == null || context == typed.Context))
			{ // already a typed subspace
				return typed;
			}
			return new TypedKeySubspace<T1, T2, T3>(subspace.GetPrefix(), context ?? subspace.Context);
		}

		/// <summary>Return a version of this subspace, which uses a different type system to produces the keys and values</summary>
		/// <param name="subspace">Instance of a generic subspace to extend</param>
		/// <param name="encoding">Encoding used by the keys of this subspace. If not specified, the <see cref="TuPack">Tuple Encoding</see> will be used to generate an encoder.</param>
		/// <param name="context">If non-null, overrides the current subspace context.</param>
		/// <returns>Subspace equivalent to <paramref name="subspace"/>, but augmented with a specific TypeSystem</returns>
		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static ITypedKeySubspace<T1, T2, T3> AsTyped<T1, T2, T3>(this IKeySubspace subspace, IKeyEncoding encoding, ISubspaceContext? context = null)
		{
			Contract.NotNull(subspace);
			Contract.NotNull(encoding);
			if (subspace is ITypedKeySubspace<T1, T2, T3> typed && (context == null || context == typed.Context))
			{ // already a typed subspace
				return typed;
			}
			return new TypedKeySubspace<T1, T2, T3>(subspace.GetPrefix(), encoding.GetKeyEncoder<T1, T2, T3>(), context ?? subspace.Context);
		}

		/// <summary>Return a version of this subspace, which uses a different type system to produces the keys and values</summary>
		/// <param name="subspace">Instance of a generic subspace</param>
		/// <param name="context">If non-null, overrides the current subspace context.</param>
		/// <returns>Subspace equivalent to <paramref name="subspace"/>, but augmented with a specific TypeSystem</returns>
		[Pure]
		public static ITypedKeySubspace<T1, T2, T3, T4> AsTyped<T1, T2, T3, T4>(this IKeySubspace subspace, ISubspaceContext? context = null)
		{
			Contract.NotNull(subspace);
			if (subspace is ITypedKeySubspace<T1, T2, T3, T4> typed && (context == null || context == typed.Context))
			{ // already a typed subspace
				return typed;
			}
			return new TypedKeySubspace<T1, T2, T3, T4>(subspace.GetPrefix(), context ?? subspace.Context);
		}

		/// <summary>Return a version of this subspace, which uses a different type system to produces the keys and values</summary>
		/// <param name="subspace">Instance of a generic subspace</param>
		/// <param name="encoding">Encoding used by the keys of this namespace. If not specified, the <see cref="TuPack">Tuple Encoding</see> will be used to generate an encoder.</param>
		/// <param name="context">If non-null, overrides the current subspace context.</param>
		/// <returns>Subspace equivalent to <paramref name="subspace"/>, but augmented with a specific TypeSystem</returns>
		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static ITypedKeySubspace<T1, T2, T3, T4> AsTyped<T1, T2, T3, T4>(this IKeySubspace subspace, IKeyEncoding encoding, ISubspaceContext? context = null)
		{
			Contract.NotNull(subspace);
			Contract.NotNull(encoding);
			if (subspace is ITypedKeySubspace<T1, T2, T3, T4> typed && (context == null || context == typed.Context))
			{ // already a typed subspace
				return typed;
			}
			return new TypedKeySubspace<T1, T2, T3, T4>(subspace.GetPrefix(), encoding.GetKeyEncoder<T1, T2, T3, T4>(), context ?? subspace.Context);
		}

		#endregion

		#region Encoders...

		/// <summary>Return a version of this subspace, which uses a different type system to produces the keys and values</summary>
		/// <param name="subspace">Instance of a generic subspace to extend</param>
		/// <param name="encoder">Custom key encoder</param>
		/// <param name="context">Optional context used by the new subspace</param>
		/// <returns>Subspace equivalent to <paramref name="subspace"/>, but augmented with a specific TypeSystem</returns>
		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static IDynamicKeySubspace UsingEncoder(this IKeySubspace subspace, IDynamicKeyEncoder encoder, ISubspaceContext? context = null)
		{
			Contract.NotNull(subspace);
			Contract.NotNull(encoder);
			return new DynamicKeySubspace(subspace.GetPrefix(), encoder, context ?? subspace.Context);
		}

		/// <summary>Return a version of this subspace, which uses a different type system to produces the keys and values</summary>
		/// <param name="subspace">Instance of a generic subspace to extend</param>
		/// <param name="encoder">Custom key encoder</param>
		/// <param name="context">Optional context used by the new subspace</param>
		/// <returns>Subspace equivalent to <paramref name="subspace"/>, but augmented with a specific TypeSystem</returns>
		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static ITypedKeySubspace<T> UsingEncoder<T>(this IKeySubspace subspace, IKeyEncoder<T> encoder, ISubspaceContext? context = null)
		{
			Contract.NotNull(subspace);
			Contract.NotNull(encoder);
			return new TypedKeySubspace<T>(subspace.GetPrefix(), encoder, context ?? subspace.Context);
		}

		/// <summary>Return a version of this subspace, which uses a different type system to produces the keys and values</summary>
		/// <param name="subspace">Instance of a generic subspace to extend</param>
		/// <param name="encoder">Custom key encoder</param>
		/// <param name="context">Optional context used by the new subspace</param>
		/// <returns>Subspace equivalent to <paramref name="subspace"/>, but augmented with a specific TypeSystem</returns>
		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static ITypedKeySubspace<T1, T2> UsingEncoder<T1, T2>(this IKeySubspace subspace, ICompositeKeyEncoder<T1, T2> encoder, ISubspaceContext? context = null)
		{
			Contract.NotNull(subspace);
			Contract.NotNull(encoder);
			return new TypedKeySubspace<T1, T2>(subspace.GetPrefix(), encoder, context ?? subspace.Context);
		}

		/// <summary>Return a version of this subspace, which uses a different type system to produces the keys and values</summary>
		/// <param name="subspace">Instance of a generic subspace to extend</param>
		/// <param name="encoder">Custom key encoder</param>
		/// <param name="context">Optional context used by the new subspace</param>
		/// <returns>Subspace equivalent to <paramref name="subspace"/>, but augmented with a specific TypeSystem</returns>
		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static ITypedKeySubspace<T1, T2, T3> UsingEncoder<T1, T2, T3>(this IKeySubspace subspace, ICompositeKeyEncoder<T1, T2, T3> encoder, ISubspaceContext? context = null)
		{
			Contract.NotNull(subspace);
			Contract.NotNull(encoder);
			return new TypedKeySubspace<T1, T2, T3>(subspace.GetPrefix(), encoder, context ?? subspace.Context);
		}

		/// <summary>Return a version of this subspace, which uses a different type system to produces the keys and values</summary>
		/// <param name="subspace">Instance of a generic subspace</param>
		/// <param name="context">Optional context used by the new subspace</param>
		/// <param name="encoder">Encoder used to serialize the keys of this namespace.</param>
		/// <returns>Subspace equivalent to <paramref name="subspace"/>, but augmented with a specific TypeSystem</returns>
		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static ITypedKeySubspace<T1, T2, T3, T4> UsingEncoder<T1, T2, T3, T4>(this IKeySubspace subspace, ICompositeKeyEncoder<T1, T2, T3, T4> encoder, ISubspaceContext? context = null)
		{
			Contract.NotNull(subspace);
			Contract.NotNull(encoder);
			return new TypedKeySubspace<T1, T2, T3, T4>(subspace.GetPrefix(), encoder, context ?? subspace.Context);
		}

		#endregion

		#region Copy...

		/// <summary>Create a new copy of a subspace prefix</summary>
		[Pure]
		internal static Slice StealPrefix(IKeySubspace subspace)
		{
			//note: we can work around the 'security' in top directory partition by accessing their key prefix without triggering an exception!
			return subspace is KeySubspace ks
				? ks.GetPrefixUnsafe().Copy()
				: subspace.GetPrefix().Copy();
		}

		/// <summary>Create a copy of a generic subspace, sharing the same binary prefix</summary>
		[Pure]
		public static KeySubspace Copy(this IKeySubspace subspace)
		{
			Contract.NotNull(subspace);

			var prefix = StealPrefix(subspace);

			if (subspace is IDynamicKeySubspace dyn)
			{ // reuse the encoding of the original
#pragma warning disable CS0618 // Type or member is obsolete
				return new DynamicKeySubspace(prefix, dyn.KeyEncoder, subspace.Context);
#pragma warning restore CS0618 // Type or member is obsolete
			}

			// no encoding
			return new KeySubspace(prefix, subspace.Context);
		}

		/// <summary>Create a copy of a generic subspace, sharing the same binary prefix</summary>
		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static DynamicKeySubspace Copy(this IKeySubspace subspace, IDynamicKeyEncoding encoding, ISubspaceContext? context = null)
		{
			Contract.NotNull(subspace);
			Contract.NotNull(encoding);
			return new DynamicKeySubspace(StealPrefix(subspace), encoding.GetDynamicKeyEncoder(), context ?? subspace.Context);
		}

		/// <summary>Create a copy of a generic subspace, sharing the same binary prefix</summary>
		[Pure]
		[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
		public static DynamicKeySubspace Copy(this IKeySubspace subspace, IDynamicKeyEncoder encoder, ISubspaceContext? context = null)
		{
			Contract.NotNull(subspace);
			Contract.NotNull(encoder);
			return new DynamicKeySubspace(StealPrefix(subspace), encoder, context ?? subspace.Context);
		}

#pragma warning disable CS0618 // Type or member is obsolete

		/// <summary>Create a copy of a dynamic subspace, sharing the same binary prefix and encoder</summary>
		[Pure]
		public static DynamicKeySubspace Copy(this IDynamicKeySubspace subspace, ISubspaceContext? context = null)
		{
			Contract.NotNull(subspace);
			return new DynamicKeySubspace(StealPrefix(subspace), subspace.KeyEncoder, context ?? subspace.Context);
		}

		/// <summary>Create a copy of a typed subspace, sharing the same binary prefix and encoder</summary>
		[Pure]
		public static TypedKeySubspace<T1> Copy<T1>(this ITypedKeySubspace<T1> subspace, ISubspaceContext? context = null)
		{
			Contract.NotNull(subspace);
			return new TypedKeySubspace<T1>(StealPrefix(subspace), subspace.KeyEncoder, context ?? subspace.Context);
		}

		/// <summary>Create a copy of a typed subspace, sharing the same binary prefix and encoder</summary>
		[Pure]
		public static TypedKeySubspace<T1, T2> Copy<T1, T2>(this ITypedKeySubspace<T1, T2> subspace, ISubspaceContext? context = null)
		{
			Contract.NotNull(subspace);
			return new TypedKeySubspace<T1, T2>(StealPrefix(subspace), subspace.KeyEncoder, context ?? subspace.Context);
		}

		/// <summary>Create a copy of a typed subspace, sharing the same binary prefix and encoder</summary>
		[Pure]
		public static TypedKeySubspace<T1, T2, T3> Copy<T1, T2, T3>(this ITypedKeySubspace<T1, T2, T3> subspace, ISubspaceContext? context = null)
		{
			Contract.NotNull(subspace);
			return new TypedKeySubspace<T1, T2, T3>(StealPrefix(subspace), subspace.KeyEncoder, context ?? subspace.Context);
		}

		/// <summary>Create a copy of a typed subspace, sharing the same binary prefix and encoder</summary>
		[Pure]
		public static TypedKeySubspace<T1, T2, T3, T4> Copy<T1, T2, T3, T4>(this ITypedKeySubspace<T1, T2, T3, T4> subspace, ISubspaceContext? context = null)
		{
			Contract.NotNull(subspace);
			return new TypedKeySubspace<T1, T2, T3, T4>(StealPrefix(subspace), subspace.KeyEncoder, context ?? subspace.Context);
		}

#pragma warning restore CS0618 // Type or member is obsolete

		#endregion

		/// <summary>Return a key range that contains all the keys in a sub-partition of this subspace</summary>
		public static KeyRange ToRange(this KeySubspace subspace, Slice suffix)
		{
			if (suffix.IsNull) throw Fdb.Errors.KeyCannotBeNull(nameof(suffix));
			return subspace.ToRange(suffix.Span);
		}

		/// <summary>Return the key that is composed of the subspace prefix and a binary suffix</summary>
		/// <param name="subspace">Parent subspace</param>
		/// <param name="relativeKey">Binary suffix that will be appended to the current prefix</param>
		/// <returns>Full binary key</returns>
		public static Slice Append(this IKeySubspace subspace, Slice relativeKey)
		{
			//REVIEW: how do we handle Slice.Nil?
			return subspace.Append(relativeKey.Span);
		}

		/// <summary>Test if a key is inside the range of keys logically contained by this subspace</summary>
		/// <param name="subspace">Subspace used for the test</param>
		/// <param name="absoluteKey">Key to test</param>
		/// <returns>True if the key can exist inside the current subspace.</returns>
		/// <remarks>Please note that this method does not test if the key *actually* exists in the database, only if the key is not outside the range of keys defined by the subspace.</remarks>
		public static bool Contains(this IKeySubspace subspace, Slice absoluteKey)
		{
			return !absoluteKey.IsNull && subspace.Contains(absoluteKey.Span);
		}

		/// <summary>Clear the entire content of a subspace</summary>
		public static void ClearRange(this IFdbTransaction trans, IKeySubspace subspace)
		{
			Contract.Debug.Requires(trans != null && subspace != null);

			//BUGBUG: should we call subspace.ToRange() ?
			trans.ClearRange(subspace.ToRange());
		}

		/// <summary>Clear the entire content of a subspace</summary>
		public static Task ClearRangeAsync(this IFdbRetryable db, IKeySubspace subspace, CancellationToken ct)
		{
			Contract.NotNull(db);
			Contract.NotNull(subspace);

			return db.WriteAsync((tr) => ClearRange(tr, subspace), ct);
		}

	}
}
