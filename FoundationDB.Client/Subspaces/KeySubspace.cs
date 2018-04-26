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
	using System.Diagnostics;
	using Doxense.Collections.Tuples;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
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

		#region Constructors...

		[NotNull]
		public static KeySubspace Empty => new KeySubspace(Slice.Empty);

		#region FromKey...

		/// <summary>Initializes a new generic subspace with the given prefix.</summary>
		[Pure, NotNull]
		public static KeySubspace FromKey(Slice prefix)
		{
			return new KeySubspace(prefix.Memoize());
		}

		/// <summary>Initializes a new dynamic subspace with the given binary <paramref name="prefix"/> and key <paramref name="encoder"/>.</summary>
		/// <returns>A subspace that can handle keys of any types and size.</returns>
		[Pure, NotNull]
		public static DynamicKeySubspace FromKey(Slice prefix, [NotNull] IDynamicKeyEncoder encoder)
		{
			Contract.NotNull(encoder, nameof(encoder));
			return new DynamicKeySubspace(prefix, encoder);
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a dynamic key <paramref name="encoding"/>.</summary>
		/// <returns>A subspace that can handle keys of any types and size.</returns>
		[Pure, NotNull]
		public static DynamicKeySubspace FromKey(Slice prefix, [NotNull] IKeyEncoding encoding)
		{
			Contract.NotNull(encoding, nameof(encoding));
			return new DynamicKeySubspace(prefix, encoding.GetDynamicEncoder());
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a typed key <paramref name="encoding"/>.</summary>
		/// <returns>A subspace that can handle keys of type <typeparamref name="T1"/>.</returns>
		public static TypedKeySubspace<T1> FromKey<T1>(Slice prefix, [CanBeNull] IKeyEncoding encoding = null)
		{
			return new TypedKeySubspace<T1>(prefix, (encoding ?? TypeSystem.Tuples).GetEncoder<T1>());
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a typed key <paramref name="encoder"/>.</summary>
		/// <returns>A subspace that can handle keys of type <typeparamref name="T1"/>.</returns>
		public static TypedKeySubspace<T1> FromKey<T1>(Slice prefix, [NotNull] IKeyEncoder<T1> encoder)
		{
			Contract.NotNull(encoder, nameof(encoder));
			return new TypedKeySubspace<T1>(prefix, encoder);
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a typed key <paramref name="encoding"/>.</summary>
		/// <returns>A subspace that can handle composite keys of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
		public static TypedKeySubspace<T1, T2> FromKey<T1, T2>(Slice prefix, [CanBeNull] IKeyEncoding encoding = null)
		{
			return new TypedKeySubspace<T1, T2>(prefix, (encoding ?? TypeSystem.Tuples).GetEncoder<T1, T2>());
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a typed key <paramref name="encoder"/>.</summary>
		/// <returns>A subspace that can handle composite keys of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
		public static TypedKeySubspace<T1, T2> FromKey<T1, T2>(Slice prefix, [NotNull] ICompositeKeyEncoder<T1, T2> encoder)
		{
			Contract.NotNull(encoder, nameof(encoder));
			return new TypedKeySubspace<T1, T2>(prefix, encoder);
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a typed key <paramref name="encoding"/>.</summary>
		/// <returns>A subspace that can handle composite keys of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>, <typeparamref name="T3"/>).</returns>
		public static TypedKeySubspace<T1, T2, T3> FromKey<T1, T2, T3>(Slice prefix, [CanBeNull] IKeyEncoding encoding = null)
		{
			return new TypedKeySubspace<T1, T2, T3>(prefix, (encoding ?? TypeSystem.Tuples).GetEncoder<T1, T2, T3>());
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a typed key <paramref name="encoder"/>.</summary>
		/// <returns>A subspace that can handle composite keys of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>, <typeparamref name="T3"/>).</returns>
		public static TypedKeySubspace<T1, T2, T3> FromKey<T1, T2, T3>(Slice prefix, [NotNull] ICompositeKeyEncoder<T1, T2, T3> encoder)
		{
			Contract.NotNull(encoder, nameof(encoder));
			return new TypedKeySubspace<T1, T2, T3>(prefix, encoder);
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a typed key <paramref name="encoding"/>.</summary>
		/// <returns>A subspace that can handle composite keys of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>, <typeparamref name="T3"/>).</returns>
		public static TypedKeySubspace<T1, T2, T3, T4> FromKey<T1, T2, T3, T4>(Slice prefix, [CanBeNull] IKeyEncoding encoding = null)
		{
			return new TypedKeySubspace<T1, T2, T3, T4>(prefix, (encoding ?? TypeSystem.Tuples).GetEncoder<T1, T2, T3, T4>());
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a typed key <paramref name="encoder"/>.</summary>
		/// <returns>A subspace that can handle composite keys of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>, <typeparamref name="T3"/>).</returns>
		public static TypedKeySubspace<T1, T2, T3, T4> FromKey<T1, T2, T3, T4>(Slice prefix, [NotNull] ICompositeKeyEncoder<T1, T2, T3, T4> encoder)
		{
			Contract.NotNull(encoder, nameof(encoder));
			return new TypedKeySubspace<T1, T2, T3, T4>(prefix, encoder);
		}

		/// <summary>Initializes a new generic subspace with the given <paramref name="prefix"/>.</summary>
		/// <returns>A subspace that can handle keys of any types and size.</returns>
		[Pure, NotNull]
		public static KeySubspace FromKey(ITuple prefix)
		{
			//REVIEW: this is tied to the Tuple Layer. Maybe this should be an extension method that lives in that namespace?
			return new KeySubspace(TuPack.Pack(prefix).Memoize());
		}

		/// <summary>Initializes a new dynamic subspace with the given <paramref name="prefix"/> and key <paramref name="encoder"/>.</summary>
		/// <returns>A subspace that can handle keys of any types and size.</returns>
		[Pure, NotNull]
		public static DynamicKeySubspace FromKey(ITuple prefix, [NotNull] IDynamicKeyEncoder encoder)
		{
			Contract.NotNull(encoder, nameof(encoder));
			var writer = new SliceWriter();
			encoder.PackKey(ref writer, prefix);
			return new DynamicKeySubspace(writer.ToSlice(), encoder);
		}

		/// <summary>Initializes a new subspace with the given <paramref name="prefix"/>, that uses a dynamic key <paramref name="encoding"/>.</summary>
		/// <returns>A subspace that can handle keys of any types and size.</returns>
		[Pure, NotNull]
		public static DynamicKeySubspace FromKey(ITuple prefix, [NotNull] IKeyEncoding encoding)
		{
			Contract.NotNull(encoding, nameof(encoding));
			return FromKey(prefix, encoding.GetDynamicEncoder());
		}

		#endregion

		#region Copy...

		/// <summary>Create a new copy of a subspace's prefix</summary>
		[Pure]
		internal static Slice StealPrefix([NotNull] IKeySubspace subspace)
		{
			//note: we can workaround the 'security' in top directory partition by accessing their key prefix without triggering an exception!
			return subspace is KeySubspace ks
				? ks.Key.Memoize()
				: subspace.GetPrefix().Memoize();
		}

		/// <summary>Create a copy of a generic subspace, sharing the same binary prefix</summary>
		[Pure, NotNull]
		public static KeySubspace Copy([NotNull] IKeySubspace subspace)
		{
			Contract.NotNull(subspace, nameof(subspace));

			var prefix = StealPrefix(subspace);

			if (subspace is IDynamicKeySubspace dyn)
			{ // reuse the encoding of the original
				return new DynamicKeySubspace(prefix, dyn.Encoding);
			}

			// no encoding
			return new KeySubspace(prefix);
		}

		/// <summary>Create a copy of a generic subspace, sharing the same binary prefix</summary>
		[Pure, NotNull]
		public static DynamicKeySubspace Copy([NotNull] IKeySubspace subspace, IKeyEncoding encoding)
		{
			Contract.NotNull(subspace, nameof(subspace));
			Contract.NotNull(encoding, nameof(encoding));
			return new DynamicKeySubspace(StealPrefix(subspace), encoding);
		}

		/// <summary>Create a copy of a generic subspace, sharing the same binary prefix</summary>
		[Pure, NotNull]
		public static DynamicKeySubspace Copy([NotNull] IKeySubspace subspace, IDynamicKeyEncoder encoder)
		{
			Contract.NotNull(subspace, nameof(subspace));
			Contract.NotNull(encoder, nameof(encoder));
			return new DynamicKeySubspace(StealPrefix(subspace), encoder);
		}

		/// <summary>Create a copy of a dynamic subspace, sharing the same binary prefix and encoder</summary>
		[Pure, NotNull]
		public static DynamicKeySubspace Copy([NotNull] IDynamicKeySubspace subspace)
		{
			Contract.NotNull(subspace, nameof(subspace));
			return new DynamicKeySubspace(StealPrefix(subspace), subspace.Encoding);
		}

		/// <summary>Create a copy of a typed subspace, sharing the same binary prefix and encoder</summary>
		[Pure, NotNull]
		public static TypedKeySubspace<T1> Copy<T1>([NotNull] ITypedKeySubspace<T1> subspace)
		{
			Contract.NotNull(subspace, nameof(subspace));
			return new TypedKeySubspace<T1>(StealPrefix(subspace), subspace.KeyEncoder);
		}

		/// <summary>Create a copy of a typed subspace, sharing the same binary prefix and encoder</summary>
		[Pure, NotNull]
		public static TypedKeySubspace<T1, T2> Copy<T1, T2>([NotNull] ITypedKeySubspace<T1, T2> subspace)
		{
			Contract.NotNull(subspace, nameof(subspace));
			return new TypedKeySubspace<T1, T2>(StealPrefix(subspace), subspace.KeyEncoder);
		}

		/// <summary>Create a copy of a typed subspace, sharing the same binary prefix and encoder</summary>
		[Pure, NotNull]
		public static TypedKeySubspace<T1, T2, T3> Copy<T1, T2, T3>([NotNull] ITypedKeySubspace<T1, T2, T3> subspace)
		{
			Contract.NotNull(subspace, nameof(subspace));
			return new TypedKeySubspace<T1, T2, T3>(StealPrefix(subspace), subspace.KeyEncoder);
		}

		/// <summary>Create a copy of a typed subspace, sharing the same binary prefix and encoder</summary>
		[Pure, NotNull]
		public static TypedKeySubspace<T1, T2, T3, T4> Copy<T1, T2, T3, T4>([NotNull] ITypedKeySubspace<T1, T2, T3, T4> subspace)
		{
			Contract.NotNull(subspace, nameof(subspace));
			return new TypedKeySubspace<T1, T2, T3, T4>(StealPrefix(subspace), subspace.KeyEncoder);
		}

		#endregion

		internal KeySubspace(Slice prefix)
		{
			this.Key = prefix;
			this.Range = KeyRange.StartsWith(prefix);
		}

		internal KeySubspace(Slice prefix, KeyRange range)
		{
			this.Key = prefix;
			this.Range = range;
		}

		/// <summary>Returns the raw prefix of this subspace</summary>
		/// <remarks>Will throw if the prefix is not publicly visible, as is the case for Directory Partitions</remarks>
		public Slice GetPrefix()
		{
			return GetKeyPrefix();
		}

		/// <summary>Returns the key to use when creating direct keys that are inside this subspace</summary>
		/// <returns>Prefix that must be added to all keys created by this subspace</returns>
		/// <remarks>Subspaces that disallow the creation of keys should override this method and throw an exception</remarks>
		[DebuggerStepThrough]
		protected virtual Slice GetKeyPrefix()
		{
			return this.Key;
		}

		/// <summary>Returns the master instance of the prefix, without any safety checks</summary>
		/// <remarks>This instance should NEVER be exposed to anyone else, and should ONLY be used for logging/troubleshooting</remarks>
		protected Slice GetPrefixUnsafe()
		{
			return this.Key;
		}

		public KeyRange ToRange()
		{
			return GetKeyRange();
		}

		protected virtual KeyRange GetKeyRange()
		{
			return this.Range;
		}

		public virtual KeyRange ToRange(Slice suffix)
		{
			return KeyRange.StartsWith(this[suffix]);
		}

		/// <summary>Tests whether the specified <paramref name="absoluteKey">key</paramref> starts with this Subspace's prefix, indicating that the Subspace logically contains <paramref name="absoluteKey">key</paramref>.</summary>
		/// <param name="absoluteKey">The key to be tested</param>
		/// <remarks>The key Slice.Nil is not contained by any Subspace, so subspace.Contains(Slice.Nil) will always return false</remarks>
		public virtual bool Contains(Slice absoluteKey)
		{
			return absoluteKey.StartsWith(this.Key);
		}

		/// <summary>Append a key to the subspace key</summary>
		/// <remarks>This is the equivalent of calling 'subspace.Key + suffix'</remarks>
		public Slice this[Slice relativeKey]
		{
			get
			{
				//note: we don't want to leak our key!
				var key = GetKeyPrefix();
				if (relativeKey.IsNullOrEmpty) return key.Memoize(); //TODO: better solution!
				return key.Concat(relativeKey);
			}
		}

		/// <summary>Remove the subspace prefix from a binary key, and only return the tail, or Slice.Nil if the key does not fit inside the namespace</summary>
		/// <param name="absoluteKey">Complete key that contains the current subspace prefix, and a binary suffix</param>
		/// <param name="boundCheck">If true, verify that <paramref name="absoluteKey"/> is inside the bounds of the subspace</param>
		/// <returns>Binary suffix of the key (or Slice.Empty is the key is exactly equal to the subspace prefix). If the key is outside of the subspace, returns Slice.Nil</returns>
		/// <remarks>This is the inverse operation of <see cref="P:FoundationDB.Client.IFdbSubspace.Item(Slice)"/></remarks>
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

		public SliceWriter OpenWriter(int extra = 32)
		{
			var key = GetKeyPrefix();
			var sw = new SliceWriter(key.Count + extra); //TODO: BufferPool ?
			sw.WriteBytes(key);
			return sw;
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
		/// <returns>The <paramref name="key"/> unchanged if it is contained in the namespace, Slice.Empty if it was before the subspace, or FdbKey.MaxValue if it was after.</returns>
		public Slice BoundCheck(Slice key, bool allowSystemKeys)
		{
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

		/// <summary>Throw an exception for a key that is out of the bounds of this subspace</summary>
		/// <param name="key"></param>
		[ContractAnnotation("=> halt")]
		protected void FailKeyOutOfBound(Slice key)
		{
#if DEBUG
			// only in debug mode, because have the key and subspace in the exception message could leak sensitive information
			string msg = $"The key {FdbKey.Dump(key)} does not belong to subspace {this}";
#else
			string msg = "The specifed key does not belong to this subspace";
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
			return "Subspace(" + this.Key.ToString() + ")";
		}

		#endregion

	}

}
