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

namespace FoundationDB.Client
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Extensions methods and helpers to work with Key Subspaces</summary>
	public static class KeySubspaceExtensions
	{

		/// <summary>Return a version of this subspace, which uses a different type system to produces the keys and values</summary>
		/// <param name="subspace">Instance of a generic subspace</param>
		/// <param name="encoding">If non-null, uses this specific instance of the TypeSystem. If null, uses the default instance for this particular TypeSystem</param>
		/// <returns>Subspace equivalent to <paramref name="subspace"/>, but augmented with a specific TypeSystem</returns>
		[Pure, NotNull]
		public static IDynamicKeySubspace Using([NotNull] this IKeySubspace subspace, [NotNull] IKeyEncoding encoding)
		{
			Contract.NotNull(subspace, nameof(subspace));
			Contract.NotNull(encoding, nameof(encoding));
			return KeySubspace.CopyDynamic(subspace, encoding);
		}

		/// <summary>Return a version of this subspace, which uses a different type system to produces the keys and values</summary>
		/// <param name="subspace">Instance of a generic subspace</param>
		/// <param name="encoder">Custom key encoder</param>
		/// <returns>Subspace equivalent to <paramref name="subspace"/>, but augmented with a specific TypeSystem</returns>
		[Pure, NotNull]
		public static IDynamicKeySubspace UsingEncoder([NotNull] this IKeySubspace subspace, [NotNull] IDynamicKeyEncoder encoder)
		{
			Contract.NotNull(subspace, nameof(subspace));
			Contract.NotNull(encoder, nameof(encoder));
			return KeySubspace.CopyDynamic(subspace, encoder);
		}

		/// <summary>Return a version of this subspace, which uses a different type system to produces the keys and values</summary>
		/// <param name="subspace">Instance of a generic subspace</param>
		/// <param name="encoding">Custom key encoder</param>
		/// <returns>Subspace equivalent to <paramref name="subspace"/>, but augmented with a specific TypeSystem</returns>
		[Pure, NotNull]
		public static ITypedKeySubspace<T> UsingEncoder<T>([NotNull] this IKeySubspace subspace, [NotNull] IKeyEncoding encoding)
		{
			Contract.NotNull(subspace, nameof(subspace));
			Contract.NotNull(encoding, nameof(encoding));
			return KeySubspace.CopyEncoder<T>(subspace, encoding);
		}

		/// <summary>Return a version of this subspace, which uses a different type system to produces the keys and values</summary>
		/// <param name="subspace">Instance of a generic subspace</param>
		/// <param name="encoder">Custom key encoder</param>
		/// <returns>Subspace equivalent to <paramref name="subspace"/>, but augmented with a specific TypeSystem</returns>
		[Pure, NotNull]
		public static ITypedKeySubspace<T> UsingEncoder<T>([NotNull] this IKeySubspace subspace, [NotNull] IKeyEncoder<T> encoder)
		{
			Contract.NotNull(subspace, nameof(subspace));
			Contract.NotNull(encoder, nameof(encoder));
			return KeySubspace.CopyEncoder<T>(subspace, encoder);
		}

		/// <summary>Return a version of this subspace, which uses a different type system to produces the keys and values</summary>
		/// <param name="subspace">Instance of a generic subspace</param>
		/// <param name="encoding">Custom key encoder</param>
		/// <returns>Subspace equivalent to <paramref name="subspace"/>, but augmented with a specific TypeSystem</returns>
		[Pure, NotNull]
		public static ITypedKeySubspace<T1, T2> UsingEncoder<T1, T2>([NotNull] this IKeySubspace subspace, [NotNull] IKeyEncoding encoding)
		{
			Contract.NotNull(subspace, nameof(subspace));
			Contract.NotNull(encoding, nameof(encoding));
			return KeySubspace.CopyEncoder<T1, T2>(subspace, encoding);
		}

		/// <summary>Return a version of this subspace, which uses a different type system to produces the keys and values</summary>
		/// <param name="subspace">Instance of a generic subspace</param>
		/// <param name="encoder">Custom key encoder</param>
		/// <returns>Subspace equivalent to <paramref name="subspace"/>, but augmented with a specific TypeSystem</returns>
		[Pure, NotNull]
		public static ITypedKeySubspace<T1, T2> UsingEncoder<T1, T2>([NotNull] this IKeySubspace subspace, [NotNull] ICompositeKeyEncoder<T1, T2> encoder)
		{
			Contract.NotNull(subspace, nameof(subspace));
			Contract.NotNull(encoder, nameof(encoder));
			return KeySubspace.CopyEncoder<T1, T2>(subspace, encoder);
		}

		/// <summary>Return a version of this subspace, which uses a different type system to produces the keys and values</summary>
		/// <param name="subspace">Instance of a generic subspace</param>
		/// <param name="encoding">Custom key encoder</param>
		/// <returns>Subspace equivalent to <paramref name="subspace"/>, but augmented with a specific TypeSystem</returns>
		[Pure, NotNull]
		public static ITypedKeySubspace<T1, T2, T3> UsingEncoder<T1, T2, T3>([NotNull] this IKeySubspace subspace, [NotNull] IKeyEncoding encoding)
		{
			Contract.NotNull(subspace, nameof(subspace));
			Contract.NotNull(encoding, nameof(encoding));
			return KeySubspace.CopyEncoder<T1, T2, T3>(subspace, encoding);
		}

		/// <summary>Return a version of this subspace, which uses a different type system to produces the keys and values</summary>
		/// <param name="subspace">Instance of a generic subspace</param>
		/// <param name="encoder">Custom key encoder</param>
		/// <returns>Subspace equivalent to <paramref name="subspace"/>, but augmented with a specific TypeSystem</returns>
		[Pure, NotNull]
		public static ITypedKeySubspace<T1, T2, T3> UsingEncoder<T1, T2, T3>([NotNull] this IKeySubspace subspace, [NotNull] ICompositeKeyEncoder<T1, T2, T3> encoder)
		{
			Contract.NotNull(subspace, nameof(subspace));
			Contract.NotNull(encoder, nameof(encoder));
			return KeySubspace.CopyEncoder<T1, T2, T3>(subspace, encoder);
		}

		/// <summary>Return a version of this subspace, which uses a different type system to produces the keys and values</summary>
		/// <param name="subspace">Instance of a generic subspace</param>
		/// <param name="encoding">Custom key encoder</param>
		/// <returns>Subspace equivalent to <paramref name="subspace"/>, but augmented with a specific TypeSystem</returns>
		[Pure, NotNull]
		public static ITypedKeySubspace<T1, T2, T3, T4> UsingEncoder<T1, T2, T3, T4>([NotNull] this IKeySubspace subspace, [NotNull] IKeyEncoding encoding)
		{
			Contract.NotNull(subspace, nameof(subspace));
			Contract.NotNull(encoding, nameof(encoding));
			return KeySubspace.CopyEncoder<T1, T2, T3, T4>(subspace, encoding);
		}

		/// <summary>Return a version of this subspace, which uses a different type system to produces the keys and values</summary>
		/// <param name="subspace">Instance of a generic subspace</param>
		/// <param name="encoder">Custom key encoder</param>
		/// <returns>Subspace equivalent to <paramref name="subspace"/>, but augmented with a specific TypeSystem</returns>
		[Pure, NotNull]
		public static ITypedKeySubspace<T1, T2, T3, T4> UsingEncoder<T1, T2, T3, T4>([NotNull] this IKeySubspace subspace, [NotNull] ICompositeKeyEncoder<T1, T2, T3, T4> encoder)
		{
			Contract.NotNull(subspace, nameof(subspace));
			Contract.NotNull(encoder, nameof(encoder));
			return KeySubspace.CopyEncoder<T1, T2, T3, T4>(subspace, encoder);
		}

		/// <summary>Clear the entire content of a subspace</summary>
		public static void ClearRange(this IFdbTransaction trans, [NotNull] IKeySubspace subspace)
		{
			Contract.Requires(trans != null && subspace != null);

			//BUGBUG: should we call subspace.ToRange() ?
			trans.ClearRange(subspace.ToRange());
		}

		/// <summary>Clear the entire content of a subspace</summary>
		public static Task ClearRangeAsync(this IFdbRetryable db, [NotNull] IKeySubspace subspace, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(subspace, nameof(subspace));

			return db.WriteAsync((tr) => ClearRange(tr, subspace), ct);
		}

		/// <summary>Returns all the keys inside of a subspace</summary>
		[Pure, NotNull]
		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRangeStartsWith(this IFdbReadOnlyTransaction trans, [NotNull] IKeySubspace subspace, FdbRangeOptions options = null)
		{
			//REVIEW: should we remove this method?
			Contract.Requires(trans != null && subspace != null);

			return trans.GetRange(subspace.ToRange(), options);
		}

	}
}
