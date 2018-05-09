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
	using System.Runtime.CompilerServices;
	using Doxense.Collections.Tuples;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using Doxense.Serialization.Encoders;
	using JetBrains.Annotations;

	public interface IKeyContext
	{
		Slice GetPrefix();

		KeyRange GetRange();

		bool Contains(Slice absoluteKey);

		SliceWriter OpenWriter(int extra = 32);

		IKeyContext CreateChild(Slice suffix);

	}

	/// <summary>Subspace that is created from a constant binary prefix</summary>
	/// <remarks>The prefix will never change during the lifetime of this object</remarks>
	public sealed class BinaryPrefixContext : IKeyContext
	{

		public BinaryPrefixContext(Slice key)
		{
			this.Key = key;
			this.Range = KeyRange.StartsWith(key);
		}

		public BinaryPrefixContext(Slice key, KeyRange range)
		{
			this.Key = key;
			this.Range = range;
		}

		private readonly Slice Key;

		/// <summary>Precomputed range that encompass all the keys in this subspace</summary>
		private readonly KeyRange Range;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice GetPrefix()
		{
			return this.Key;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeyRange GetRange()
		{
			return this.Range;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(Slice absoluteKey)
		{
			return absoluteKey.StartsWith(this.Key);
		}

		public SliceWriter OpenWriter(int extra = 32)
		{
			var key = this.Key;
			var sw = new SliceWriter(key.Count + extra); //TODO: BufferPool ?
			sw.WriteBytes(key);
			return sw;
		}

		public IKeyContext CreateChild(Slice suffix)
		{
			if (suffix.Count == 0) return this;
			return new BinaryPrefixContext(this.Key.Concat(suffix));
		}
	}

	/// <summary>Adds a prefix on every keys, to group them inside a common subspace</summary>
	[PublicAPI]
	[DebuggerDisplay("{ToString(),nq}")]
	public abstract class KeySubspace : IKeySubspace, IEquatable<IKeySubspace>, IComparable<IKeySubspace>
	{
		/// <summary>Prefix common to all keys in this subspace</summary>
		private readonly IKeyContext Context;

		#region Constructors...
		
		internal KeySubspace(IKeyContext context)
		{
			this.Context = context;
		}

		#endregion

		#region Factory Methods...

		[NotNull]
		public static IKeySubspace Empty => new DynamicKeySubspace(new BinaryPrefixContext(Slice.Empty), TuPack.Encoding);

		#region FromKey...

		/// <summary>Initializes a new generic subspace with the given prefix.</summary>
		[Pure, NotNull]
		public static IKeySubspace FromKey(Slice prefix)
		{
			return new DynamicKeySubspace(new BinaryPrefixContext(prefix.Memoize()), TuPack.Encoding);
		}

		/// <summary>Initializes a new dynamic subspace with the given binary <paramref name="prefix"/> and key <paramref name="encoder"/>.</summary>
		/// <returns>A subspace that can handle keys of any types and size.</returns>
		[Pure, NotNull]
		public static IDynamicKeySubspace CreateDynamic(Slice prefix, [NotNull] IDynamicKeyEncoder encoder)
		{
			Contract.NotNull(encoder, nameof(encoder));
			return new DynamicKeySubspace(new BinaryPrefixContext(prefix), encoder);
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a dynamic key <paramref name="encoding"/>.</summary>
		/// <returns>A subspace that can handle keys of any types and size.</returns>
		[Pure, NotNull]
		public static IDynamicKeySubspace CreateDynamic(Slice prefix, [CanBeNull] IKeyEncoding encoding = null)
		{
			return new DynamicKeySubspace(new BinaryPrefixContext(prefix), (encoding ?? TuPack.Encoding).GetDynamicKeyEncoder());
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a typed key <paramref name="encoding"/>.</summary>
		/// <returns>A subspace that can handle keys of type <typeparamref name="T1"/>.</returns>
		public static ITypedKeySubspace<T1> CreateTyped<T1>(Slice prefix, [CanBeNull] IKeyEncoding encoding = null)
		{
			return new TypedKeySubspace<T1>(new BinaryPrefixContext(prefix), (encoding ?? TuPack.Encoding).GetKeyEncoder<T1>());
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a typed key <paramref name="encoder"/>.</summary>
		/// <returns>A subspace that can handle keys of type <typeparamref name="T1"/>.</returns>
		public static ITypedKeySubspace<T1> CreateTyped<T1>(Slice prefix, [NotNull] IKeyEncoder<T1> encoder)
		{
			Contract.NotNull(encoder, nameof(encoder));
			return new TypedKeySubspace<T1>(new BinaryPrefixContext(prefix), encoder);
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a typed key <paramref name="encoding"/>.</summary>
		/// <returns>A subspace that can handle composite keys of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
		public static ITypedKeySubspace<T1, T2> CreateTyped<T1, T2>(Slice prefix, [CanBeNull] IKeyEncoding encoding = null)
		{
			return new TypedKeySubspace<T1, T2>(new BinaryPrefixContext(prefix), (encoding ?? TuPack.Encoding).GetKeyEncoder<T1, T2>());
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a typed key <paramref name="encoder"/>.</summary>
		/// <returns>A subspace that can handle composite keys of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
		public static ITypedKeySubspace<T1, T2> CreateTyped<T1, T2>(Slice prefix, [NotNull] ICompositeKeyEncoder<T1, T2> encoder)
		{
			Contract.NotNull(encoder, nameof(encoder));
			return new TypedKeySubspace<T1, T2>(new BinaryPrefixContext(prefix), encoder);
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a typed key <paramref name="encoding"/>.</summary>
		/// <returns>A subspace that can handle composite keys of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>, <typeparamref name="T3"/>).</returns>
		public static ITypedKeySubspace<T1, T2, T3> CreateTyped<T1, T2, T3>(Slice prefix, [CanBeNull] IKeyEncoding encoding = null)
		{
			return new TypedKeySubspace<T1, T2, T3>(new BinaryPrefixContext(prefix), (encoding ?? TuPack.Encoding).GetKeyEncoder<T1, T2, T3>());
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a typed key <paramref name="encoder"/>.</summary>
		/// <returns>A subspace that can handle composite keys of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>, <typeparamref name="T3"/>).</returns>
		public static ITypedKeySubspace<T1, T2, T3> CreateTyped<T1, T2, T3>(Slice prefix, [NotNull] ICompositeKeyEncoder<T1, T2, T3> encoder)
		{
			Contract.NotNull(encoder, nameof(encoder));
			return new TypedKeySubspace<T1, T2, T3>(new BinaryPrefixContext(prefix), encoder);
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a typed key <paramref name="encoding"/>.</summary>
		/// <returns>A subspace that can handle composite keys of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>, <typeparamref name="T3"/>).</returns>
		public static ITypedKeySubspace<T1, T2, T3, T4> CreateTyped<T1, T2, T3, T4>(Slice prefix, [CanBeNull] IKeyEncoding encoding = null)
		{
			return new TypedKeySubspace<T1, T2, T3, T4>(new BinaryPrefixContext(prefix), (encoding ?? TuPack.Encoding).GetKeyEncoder<T1, T2, T3, T4>());
		}

		/// <summary>Initializes a new subspace with the given binary <paramref name="prefix"/>, that uses a typed key <paramref name="encoder"/>.</summary>
		/// <returns>A subspace that can handle composite keys of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>, <typeparamref name="T3"/>).</returns>
		public static ITypedKeySubspace<T1, T2, T3, T4> CreateTyped<T1, T2, T3, T4>(Slice prefix, [NotNull] ICompositeKeyEncoder<T1, T2, T3, T4> encoder)
		{
			Contract.NotNull(encoder, nameof(encoder));
			return new TypedKeySubspace<T1, T2, T3, T4>(new BinaryPrefixContext(prefix), encoder);
		}

		#endregion

		public IKeyContext GetContext() => this.Context;

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
			return this.Context.GetPrefix();
		}

		/// <summary>Returns the master instance of the prefix, without any safety checks</summary>
		/// <remarks>This instance should NEVER be exposed to anyone else, and should ONLY be used for logging/troubleshooting</remarks>
		internal Slice GetPrefixUnsafe()
		{
			return this.Context.GetPrefix();
		}

		public KeyRange ToRange()
		{
			return GetKeyRange();
		}

		protected virtual KeyRange GetKeyRange()
		{
			return this.Context.GetRange();
		}

		//public virtual KeyRange ToRange(Slice suffix)
		//{
		//	return KeyRange.StartsWith(this[suffix]);
		//}

		/// <summary>Tests whether the specified <paramref name="absoluteKey">key</paramref> starts with this Subspace's prefix, indicating that the Subspace logically contains <paramref name="absoluteKey">key</paramref>.</summary>
		/// <param name="absoluteKey">The key to be tested</param>
		/// <remarks>The key Slice.Nil is not contained by any Subspace, so subspace.Contains(Slice.Nil) will always return false</remarks>
		public virtual bool Contains(Slice absoluteKey)
		{
			return this.Context.Contains(absoluteKey);
		}

		///// <summary>Append a key to the subspace key</summary>
		///// <remarks>This is the equivalent of calling 'subspace.Key + suffix'</remarks>
		//public Slice this[Slice relativeKey]
		//{
		//	get
		//	{
		//		//note: we don't want to leak our key!
		//		var key = GetKeyPrefix();
		//		if (relativeKey.IsNullOrEmpty) return key.Memoize(); //TODO: better solution!
		//		return key.Concat(relativeKey);
		//	}
		//}

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
			return this.Context.GetPrefix().CompareTo(other.GetContext().GetPrefix());
		}

		/// <summary>Test if both subspaces have the same prefix</summary>
		public bool Equals(IKeySubspace other)
		{
			if (other == null) return false;
			if (object.ReferenceEquals(this, other)) return true;
			return this.Context.GetPrefix().Equals(other.GetContext().GetPrefix());
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
			return this.Context.GetHashCode().GetHashCode();
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
			var prefix = this.Context.GetPrefix();

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
			var prefix = this.Context.GetPrefix();
			if (!key.StartsWith(prefix)) FailKeyOutOfBound(key);

			return FdbKey.Dump(key.Substring(prefix.Count));
		}

		/// <summary>Printable representation of this subspace</summary>
		public override string ToString()
		{
			return "Subspace(" + this.Context.GetPrefix().ToString() + ")";
		}

		#endregion

	}

}
