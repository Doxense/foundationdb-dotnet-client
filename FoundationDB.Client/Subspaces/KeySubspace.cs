#region BSD License
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

namespace FoundationDB.Client
{
	using System;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using Doxense.Collections.Tuples;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Serialization.Encoders;
	using JetBrains.Annotations;

	/// <summary>Adds a prefix on every keys, to group them inside a common subspace</summary>
	[PublicAPI]
	[DebuggerDisplay("{ToString(),nq}")]
	public class KeySubspace : IKeySubspace, IEquatable<IKeySubspace>, IComparable<IKeySubspace>
	{

		/// <summary>Prefix common to all keys in this subspace</summary>
		private readonly Slice Key;

		/// <summary>Precomputed range that encompass all the keys in this subspace</summary>
		private readonly KeyRange Range;

		[NotNull]
		public ISubspaceContext Context { get; }

		#region Constructors...

		[NotNull]
		public static IKeySubspace Empty => new KeySubspace(Slice.Empty, SubspaceContext.Default);

		#region FromKey...

		/// <summary>Initializes a new generic subspace with the given prefix.</summary>
		[Pure, NotNull]
		public static IKeySubspace FromKey(Slice prefix, [CanBeNull] ISubspaceContext context = null)
		{
			return new KeySubspace(prefix.Memoize(), context ?? SubspaceContext.Default);
		}

		/// <summary>Create an unsafe copy of a directory by discarding its context</summary>
		/// <param name="subspace">Subspace that lives in a temporary context (ex: transaction context)</param>
		/// <returns>Subspace with the same key prefix, and disconnected from the original context.</returns>
		/// <remarks>THIS IS VERY DANGEROUS! Any change to the original subspace may cause the prefix stored in the copy to become obsolete, and MAY introduce data corruption!</remarks>
		public static IKeySubspace CopyUnsafe(IKeySubspace subspace)
		{
			return new KeySubspace(subspace.GetPrefix(), SubspaceContext.Default);
		}

		/// <summary>Initializes a new dynamic subspace with the given binary <paramref name="prefix"/> and key <paramref name="encoder"/>.</summary>
		/// <returns>A subspace that can handle keys of any types and size.</returns>
		[Pure, NotNull]
		public static IDynamicKeySubspace CreateDynamic(Slice prefix, [NotNull] IDynamicKeyEncoder encoder, [CanBeNull] ISubspaceContext context = null)
		{
			Contract.NotNull(encoder, nameof(encoder));
			return new DynamicKeySubspace(prefix, encoder, context ?? SubspaceContext.Default);
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a dynamic key <paramref name="encoding"/>.</summary>
		/// <returns>A subspace that can handle keys of any types and size.</returns>
		[Pure, NotNull]
		public static IDynamicKeySubspace CreateDynamic(Slice prefix, [CanBeNull] IKeyEncoding encoding = null, [CanBeNull] ISubspaceContext context = null)
		{
			return new DynamicKeySubspace(prefix, (encoding ?? TuPack.Encoding).GetDynamicKeyEncoder(), context ?? SubspaceContext.Default);
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a typed key <paramref name="encoding"/>.</summary>
		/// <returns>A subspace that can handle keys of type <typeparamref name="T1"/>.</returns>
		public static ITypedKeySubspace<T1> CreateTyped<T1>(Slice prefix, [CanBeNull] IKeyEncoding encoding = null, [CanBeNull] ISubspaceContext context = null)
		{
			return new TypedKeySubspace<T1>(prefix, (encoding ?? TuPack.Encoding).GetKeyEncoder<T1>(), context ?? SubspaceContext.Default);
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a typed key <paramref name="encoder"/>.</summary>
		/// <returns>A subspace that can handle keys of type <typeparamref name="T1"/>.</returns>
		public static ITypedKeySubspace<T1> CreateTyped<T1>(Slice prefix, [NotNull] IKeyEncoder<T1> encoder, [CanBeNull] ISubspaceContext context = null)
		{
			Contract.NotNull(encoder, nameof(encoder));
			return new TypedKeySubspace<T1>(prefix, encoder, context ?? SubspaceContext.Default);
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a typed key <paramref name="encoding"/>.</summary>
		/// <returns>A subspace that can handle composite keys of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
		public static ITypedKeySubspace<T1, T2> CreateTyped<T1, T2>(Slice prefix, [CanBeNull] IKeyEncoding encoding = null, [CanBeNull] ISubspaceContext context = null)
		{
			return new TypedKeySubspace<T1, T2>(prefix, (encoding ?? TuPack.Encoding).GetKeyEncoder<T1, T2>(), context ?? SubspaceContext.Default);
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a typed key <paramref name="encoder"/>.</summary>
		/// <returns>A subspace that can handle composite keys of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
		public static ITypedKeySubspace<T1, T2> CreateTyped<T1, T2>(Slice prefix, [NotNull] ICompositeKeyEncoder<T1, T2> encoder, [CanBeNull] ISubspaceContext context = null)
		{
			Contract.NotNull(encoder, nameof(encoder));
			return new TypedKeySubspace<T1, T2>(prefix, encoder, context ?? SubspaceContext.Default);
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a typed key <paramref name="encoding"/>.</summary>
		/// <returns>A subspace that can handle composite keys of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>, <typeparamref name="T3"/>).</returns>
		public static ITypedKeySubspace<T1, T2, T3> CreateTyped<T1, T2, T3>(Slice prefix, [CanBeNull] IKeyEncoding encoding = null, [CanBeNull] ISubspaceContext context = null)
		{
			return new TypedKeySubspace<T1, T2, T3>(prefix, (encoding ?? TuPack.Encoding).GetKeyEncoder<T1, T2, T3>(), context ?? SubspaceContext.Default);
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a typed key <paramref name="encoder"/>.</summary>
		/// <returns>A subspace that can handle composite keys of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>, <typeparamref name="T3"/>).</returns>
		public static ITypedKeySubspace<T1, T2, T3> CreateTyped<T1, T2, T3>(Slice prefix, [NotNull] ICompositeKeyEncoder<T1, T2, T3> encoder, [CanBeNull] ISubspaceContext context = null)
		{
			Contract.NotNull(encoder, nameof(encoder));
			return new TypedKeySubspace<T1, T2, T3>(prefix, encoder, context ?? SubspaceContext.Default);
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a typed key <paramref name="encoding"/>.</summary>
		/// <returns>A subspace that can handle composite keys of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>, <typeparamref name="T3"/>).</returns>
		public static ITypedKeySubspace<T1, T2, T3, T4> CreateTyped<T1, T2, T3, T4>(Slice prefix, [CanBeNull] IKeyEncoding encoding = null, [CanBeNull] ISubspaceContext context = null)
		{
			return new TypedKeySubspace<T1, T2, T3, T4>(prefix, (encoding ?? TuPack.Encoding).GetKeyEncoder<T1, T2, T3, T4>(), context ?? SubspaceContext.Default);
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a typed key <paramref name="encoder"/>.</summary>
		/// <returns>A subspace that can handle composite keys of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>, <typeparamref name="T3"/>).</returns>
		public static ITypedKeySubspace<T1, T2, T3, T4> CreateTyped<T1, T2, T3, T4>(Slice prefix, [NotNull] ICompositeKeyEncoder<T1, T2, T3, T4> encoder, [CanBeNull] ISubspaceContext context = null)
		{
			Contract.NotNull(encoder, nameof(encoder));
			return new TypedKeySubspace<T1, T2, T3, T4>(prefix, encoder, context ?? SubspaceContext.Default);
		}

		#endregion

		internal KeySubspace(Slice prefix, [NotNull] ISubspaceContext context)
		{
			this.Key = prefix;
			this.Range = KeyRange.StartsWith(prefix);
			this.Context = context ?? throw new ArgumentNullException(nameof(context));
		}

		internal KeySubspace(Slice prefix, KeyRange range, [NotNull] ISubspaceContext context)
		{
			this.Key = prefix;
			this.Range = range;
			this.Context = context ?? throw new ArgumentNullException(nameof(context));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected void EnsureIsValid()
		{
			// we must ensure that the context is still alive
			this.Context.EnsureIsValid();
		}

		/// <summary>Returns the raw prefix of this subspace</summary>
		/// <remarks>Will throw if the prefix is not publicly visible, as is the case for Directory Partitions</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice GetPrefix()
		{
			return GetKeyPrefix();
		}

		/// <summary>Returns the key to use when creating direct keys that are inside this subspace</summary>
		/// <returns>Prefix that must be added to all keys created by this subspace</returns>
		/// <remarks>Subspaces that disallow the creation of keys should override this method and throw an exception</remarks>
		[Pure, DebuggerStepThrough]
		protected virtual Slice GetKeyPrefix()
		{
			EnsureIsValid();
			return this.Key;
		}

		/// <summary>Returns the master instance of the prefix, without any safety checks</summary>
		/// <remarks>This instance should NEVER be exposed to anyone else, and should ONLY be used for logging/troubleshooting</remarks>
		internal Slice GetPrefixUnsafe()
		{
			return this.Key;
		}

		public KeyRange ToRange()
		{
			return GetKeyRange();
		}

		protected virtual KeyRange GetKeyRange()
		{
			EnsureIsValid();
			return this.Range;
		}

		public virtual KeyRange ToRange(Slice suffix)
		{
			return KeyRange.StartsWith(Append(suffix));
		}

		/// <summary>Tests whether the specified <paramref name="absoluteKey">key</paramref> starts with this Subspace's prefix, indicating that the Subspace logically contains <paramref name="absoluteKey">key</paramref>.</summary>
		/// <param name="absoluteKey">The key to be tested</param>
		/// <remarks>The key <see cref="Slice.Nil"/> is not contained by any Subspace, so <c>subspace.Contains(Slice.Nil)</c> will always return false</remarks>
		public virtual bool Contains(ReadOnlySpan<byte> absoluteKey)
		{
			EnsureIsValid();
			return absoluteKey.StartsWith(this.Key);
		}

		/// <summary>Manually append a binary suffix to the subspace's prefix</summary>
		/// <remarks>Appending a binary literal that does not comply with the subspace's key encoding may produce a result that cannot be decoded!</remarks>
		public Slice Append(ReadOnlySpan<byte> relativeKey)
		{
			//note: we don't want to leak our key!
			var key = GetKeyPrefix();
			if (relativeKey.Length == 0) return key.Memoize(); //TODO: better solution!
			return key.Concat(relativeKey);
		}

		/// <summary>Remove the subspace prefix from a binary key, and only return the tail, or <see cref="Slice.Nil"/> if the key does not fit inside the namespace</summary>
		/// <param name="absoluteKey">Complete key that contains the current subspace prefix, and a binary suffix</param>
		/// <param name="boundCheck">If true, verify that <paramref name="absoluteKey"/> is inside the bounds of the subspace</param>
		/// <returns>Binary suffix of the key (or <see cref="Slice.Empty"/> is the key is exactly equal to the subspace prefix). If the key is outside of the subspace, returns <see cref="Slice.Nil"/></returns>
		/// <remarks>This is the inverse operation of <see cref="P:FoundationDB.Client.IKeySubspace.Item(Slice)"/></remarks>
		/// <exception cref="System.ArgumentException">If <paramref name="boundCheck"/> is true and <paramref name="absoluteKey"/> is outside the current subspace.</exception>
		public virtual Slice ExtractKey(Slice absoluteKey, bool boundCheck = false)
		{
			if (absoluteKey.IsNull) return Slice.Nil;

			var key = GetKeyPrefix();
			if (!absoluteKey.StartsWith(key))
			{
				if (boundCheck) FailKeyOutOfBound(absoluteKey);
				return Slice.Nil;
			}
			return absoluteKey.Substring(key.Count);
		}

		#endregion

		#region IEquatable / IComparable...

		/// <summary>Compare this subspace with another subspace</summary>
		public int CompareTo(IKeySubspace other)
		{
			if (other == null) return +1;
			if (object.ReferenceEquals(this, other)) return 0;
			if (other is KeySubspace sub)
				return this.Key.CompareTo(sub.Key);
			else
				return this.Key.CompareTo(other.GetPrefix());
		}

		/// <summary>Test if both subspaces have the same prefix</summary>
		public bool Equals(IKeySubspace other)
		{
			if (other == null) return false;
			if (object.ReferenceEquals(this, other)) return true;
			if (other is KeySubspace sub)
				return this.Key.Equals(sub.Key);
			else
				return this.Key.Equals(other.GetPrefix());
		}

		/// <summary>Test if an object is a subspace with the same prefix</summary>
		public override bool Equals(object obj)
		{
			return Equals(obj as KeySubspace);
		}

		/// <summary>Compute a hashcode based on the prefix of this subspace</summary>
		/// <returns></returns>
		public override int GetHashCode()
		{
			return this.Key.GetHashCode();
		}

		#endregion

		#region Helpers...

		/// <summary>Check that a key fits inside this subspace, and return '' or '\xFF' if it is outside the bounds</summary>
		/// <param name="key">Key that needs to be checked</param>
		/// <param name="allowSystemKeys">If true, allow keys that starts with \xFF even if this subspace is not the Empty subspace or System subspace itself.</param>
		/// <returns>The <paramref name="key"/> unchanged if it is contained in the namespace, <see cref="Slice.Empty"/> if it was before the subspace, or FdbKey.MaxValue if it was after.</returns>
		public Slice BoundCheck(Slice key, bool allowSystemKeys)
		{
			EnsureIsValid();

			//note: Since this is needed to make GetRange/GetKey work properly, this should work for all subspace, include directory partitions
			var prefix = this.Key;

			// don't touch to nil and keys inside the globalspace
			if (key.IsNull || key.StartsWith(prefix)) return key;

			// let the system keys pass
			if (allowSystemKeys && key.Count > 0 && key[0] == 255) return key;

			// The key is outside the bounds, and must be corrected
			// > return empty if we are before
			// > return \xFF if we are after
			if (key < prefix)
				return Slice.Empty;
			else
				return FdbKey.System;
		}

		/// <summary>Check that a key fits inside this subspace, and return '' or '\xFF' if it is outside the bounds</summary>
		/// <param name="key">Key that needs to be checked</param>
		/// <param name="allowSystemKeys">If true, allow keys that starts with \xFF even if this subspace is not the Empty subspace or System subspace itself.</param>
		/// <returns>The <paramref name="key"/> unchanged if it is contained in the namespace, <see cref="Slice.Empty"/> if it was before the subspace, or FdbKey.MaxValue if it was after.</returns>
		public ReadOnlySpan<byte> BoundCheck(ReadOnlySpan<byte> key, bool allowSystemKeys)
		{
			EnsureIsValid();

			//note: Since this is needed to make GetRange/GetKey work properly, this should work for all subspace, include directory partitions
			var prefix = this.Key;

			// don't touch to nil and keys inside the globalspace
			if (key.StartsWith(prefix)) return key;

			// let the system keys pass
			if (allowSystemKeys && key.Length > 0 && key[0] == 255) return key;

			// The key is outside the bounds, and must be corrected
			// > return empty if we are before
			// > return \xFF if we are after
			if (key.SequenceCompareTo(prefix) < 0)
				return default;
			else
				return FdbKey.System;
		}

		/// <summary>Throw an exception for a key that is out of the bounds of this subspace</summary>
		/// <param name="key"></param>
		[ContractAnnotation("=> halt")]
		protected void FailKeyOutOfBound(Slice key)
		{
#if DEBUG
			// only in debug mode, because have the key and subspace in the exception message could leak sensitive information
			string msg = $"The key {FdbKey.Dump(key)} does not belong to subspace {this}";
#else
			string msg = "The specified key does not belong to this subspace";
#endif
			throw new ArgumentException(msg, nameof(key));
		}

		/// <summary>Return a user-friendly representation of a key from this subspace</summary>
		/// <param name="key">Key that is contained in this subspace</param>
		/// <returns>Printable version of this key, minus the subspace prefix</returns>
		[NotNull]
		public virtual string DumpKey(Slice key)
		{
			// note: we can't use ExtractAndCheck(...) because it may throw in derived classes
			var prefix = this.Key;
			if (!key.StartsWith(prefix)) FailKeyOutOfBound(key);

			return FdbKey.Dump(key.Substring(prefix.Count));
		}

		/// <summary>Printable representation of this subspace</summary>
		public override string ToString()
		{
			return $"{this.GetType().Name}({FdbKey.Dump(this.Key)})";
		}

		#endregion

	}

}
