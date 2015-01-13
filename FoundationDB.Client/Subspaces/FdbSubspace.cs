#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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
	using FoundationDB.Layers.Tuples;
	using JetBrains.Annotations;
	using System;

	/// <summary>Adds a prefix on every keys, to group them inside a common subspace</summary>
	public class FdbSubspace : IFdbSubspace, IEquatable<FdbSubspace>, IComparable<FdbSubspace>
	{
		/// <summary>Empty subspace, that does not add any prefix to the keys</summary>
		public static readonly FdbSubspace Empty = new FdbSubspace(Slice.Empty);

		/// <summary>Binary prefix of this subspace</summary>
		private Slice m_rawPrefix; //PERF: readonly struct

		/// <summary>Returns the key of this directory subspace</summary>
		/// <remarks>This should only be used by methods that can use the key internally, even if it is not supposed to be exposed (as is the case for directory partitions)</remarks>
		protected Slice InternalKey
		{
			get { return m_rawPrefix; }
		}

		#region Constructors...

		/// <summary>Wraps an existing subspace</summary>
		protected FdbSubspace([NotNull] FdbSubspace copy)
		{
			if (copy == null) throw new ArgumentNullException("copy");
			if (copy.m_rawPrefix.IsNull) throw new ArgumentException("The subspace key cannot be null. Use Slice.Empty if you want a subspace with no prefix.", "copy");
			m_rawPrefix = copy.m_rawPrefix;
		}

		/// <summary>Create a new subspace from a binary prefix</summary>
		/// <param name="rawPrefix">Prefix of the new subspace</param>
		/// <param name="copy">If true, take a copy of the prefix</param>
		protected FdbSubspace(Slice rawPrefix, bool copy)
		{
			if (rawPrefix.IsNull) throw new ArgumentException("The subspace key cannot be null. Use Slice.Empty if you want a subspace with no prefix.", "rawPrefix");
			if (copy) rawPrefix = rawPrefix.Memoize();
			m_rawPrefix = rawPrefix.Memoize();
		}

		/// <summary>Create a new subspace from a binary prefix</summary>
		/// <param name="rawPrefix">Prefix of the new subspace</param>
		public FdbSubspace(Slice rawPrefix)
			: this(rawPrefix, true)
		{ }

		/// <summary>Create a new subspace from a Tuple prefix</summary>
		/// <param name="tuple">Tuple packed to produce the prefix</param>
		public FdbSubspace([NotNull] IFdbTuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");
			m_rawPrefix = tuple.ToSlice().Memoize();
		}

		#endregion

		#region Static Prefix Helpers...

		/// <summary>Create a new Subspace using a binary key as the prefix</summary>
		/// <param name="slice">Prefix of the new subspace</param>
		/// <returns>New subspace that will use a copy of <paramref name="slice"/> as its prefix</returns>
		[NotNull]
		public static FdbSubspace Create(Slice slice)
		{
			return new FdbSubspace(slice);
		}

		/// <summary>Create a new Subspace using a tuples as the prefix</summary>
		/// <param name="tuple">Tuple that represents the prefix of the new subspace</param>
		/// <returns>New subspace instance that will use the packed representation of <paramref name="tuple"/> as its prefix</returns>
		[NotNull]
		public static FdbSubspace Create([NotNull] IFdbTuple tuple)
		{
			return new FdbSubspace(tuple);
		}

		/// <summary>Clone this subspace</summary>
		/// <returns>New Subspace that uses the same prefix key</returns>
		/// <remarks>Hint: Cloning a special Subspace like a <see cref="FoundationDB.Layers.Directories.FdbDirectoryLayer"/>  or <see cref="FoundationDB.Layers.Directories.FdbDirectoryPartition"/> will not keep all the "special abilities" of the parent.</remarks>
		[NotNull]
		public FdbSubspace Copy()
		{
			//SPOILER WARNING: You didn't hear it from me, but some say that you can use this to bypass the fact that FdbDirectoryPartition.get_Key and ToRange() throws in v2.x ... If you bypass this protection and bork your database, don't come crying!
			return new FdbSubspace(this.InternalKey.Memoize());
		}

		#endregion

		#region Partition...

		/// <summary>Returns the key to use when creating direct keys that are inside this subspace</summary>
		/// <returns>Prefix that must be added to all keys created by this subspace</returns>
		/// <remarks>Subspaces that disallow the creation of keys should override this method and throw an exception</remarks>
		protected virtual Slice GetKeyPrefix()
		{
			return m_rawPrefix;
		}

		/// <summary>Create a new subspace by adding a suffix to the key of the current subspace.</summary>
		/// <param name="suffix">Binary suffix that will be appended to the current prefix</param>
		/// <returns>New subspace whose prefix is the concatenation of the parent prefix, and <paramref name="suffix"/></returns>
		public FdbSubspace this[Slice suffix]
		{
			// note: there is a difference with the Pyton layer because here we don't use Tuple encoding, but just concat the slices together.
			// the .NET equivalent of the subspace.__getitem__(self, name) method would be subspace.Partition<Slice>(name) or subspace[FdbTuple.Create<Slice>(name)] !
			[NotNull]
			get
			{
				if (suffix.IsNull) throw new ArgumentException("The subspace key cannot be null. Use Slice.Empty if you want a subspace with no prefix.", "suffix");
				return FdbSubspace.Create(GetKeyPrefix() + suffix);
			}
		}

		IFdbSubspace IFdbSubspace.this[Slice suffix]
		{
			get { return this[suffix]; }
		}

		/// <summary>Create a new subspace by adding a <paramref name="key"/> to the current subspace's prefix</summary>
		/// <param name="key">Key that will be appended to the current prefix</param>
		/// <returns>New subspace whose prefix is the concatenation of the parent prefix, and the packed representation of <paramref name="key"/></returns>
		public FdbSubspace this[IFdbKey key]
		{
			[ContractAnnotation("null => halt; notnull => notnull")]
			get
			{
				if (key == null) throw new ArgumentNullException("key");
				var packed = key.ToFoundationDbKey();
				return packed.Count == 0 ? this : FdbSubspace.Create(GetKeyPrefix() + packed);
			}
		}

		IFdbSubspace IFdbSubspace.this[IFdbKey key]
		{
			get { return this[key]; }
		}

		#endregion

		#region IFdbKey...

		Slice IFdbKey.ToFoundationDbKey()
		{
			return GetKeyPrefix();
		}

		#endregion

		#region IFdbSubspace...

		/// <summary>Returns the raw prefix of this subspace</summary>
		/// <remarks>Will throw if the prefix is not publicly visible, as is the case for Directory Partitions</remarks>
		public Slice Key
		{
			get { return GetKeyPrefix(); }
		}

		/// <summary>Returns an helper object that knows how to create sub-partitions of this subspace</summary>
		/// <summary>Tests whether the specified <paramref name="key"/> starts with this Subspace's prefix, indicating that the Subspace logically contains <paramref name="key"/>.</summary>
		/// <param name="key">The key to be tested</param>
		/// <remarks>The key Slice.Nil is not contained by any Subspace, so subspace.Contains(Slice.Nil) will always return false</remarks>
		public virtual bool Contains(Slice key)
		{
			return key.HasValue && key.StartsWith(this.InternalKey);
		}

		/// <summary>Append a key to the subspace key</summary>
		/// <remarks>This is the equivalent of calling 'subspace.Key + key'</remarks>
		public Slice Concat(Slice key)
		{
			return Slice.Concat(GetKeyPrefix(), key);
		}

		/// <summary>Remove the subspace prefix from a binary key, and only return the tail, or Slice.Nil if the key does not fit inside the namespace</summary>
		/// <param name="key">Complete key that contains the current subspace prefix, and a binary suffix</param>
		/// <returns>Binary suffix of the key (or Slice.Empty is the key is exactly equal to the subspace prefix). If the key is outside of the subspace, returns Slice.Nil</returns>
		/// <remarks>This is the inverse operation of <see cref="P:FoundationDB.Client.IFdbSubspace.Item(Slice)"/></remarks>
		public Slice Extract(Slice key)
		{
			if (key.IsNull) return Slice.Nil;

			var prefix = GetKeyPrefix();
			if (!key.StartsWith(prefix))
			{
				// or should we throw ?
				return Slice.Nil;
			}

			return key.Substring(prefix.Count);
		}

		//REVIEW: add Extract<TKey>() where TKey : IFdbKey ?

		/// <summary>Remove the subspace prefix from a batch of binary keys, and only return the tail, or Slice.Nil if a key does not fit inside the namespace</summary>
		/// <param name="keys">Array of complete keys that contains the current subspace prefix, and a binary suffix</param>
		/// <returns>Array of only the binary suffix of the keys, Slice.Empty for a key that is exactly equal to the subspace prefix, or Slice.Nil for a key that is outside of the subspace</returns>
		[NotNull]
		public Slice[] Extract([NotNull] Slice[] keys)
		{ //REVIEW: rename to ExtractRange ?
			if (keys == null) throw new ArgumentNullException("keys");

			var prefix = GetKeyPrefix();
			var results = new Slice[keys.Length];
			for (int i = 0; i < keys.Length; i++)
			{
				if (keys[i].StartsWith(prefix))
				{
					results[i] = keys[i].Substring(prefix.Count);
				}
			}

			return results;
		}

		/// <summary>Remove the subspace prefix from a binary key, or throw if the key does not belong to this subspace</summary>
		/// <param name="key">Complete key that contains the current subspace prefix, and a binary suffix.</param>
		/// <returns>Binary suffix of the key (or Slice.Empty is the key is exactly equal to the subspace prefix). If the key is equal to Slice.Nil, then it will be returned unmodified. If the key is outside of the subspace, the method throws.</returns>
		/// <exception cref="System.ArgumentException">If key is outside the current subspace.</exception>
		public Slice ExtractAndCheck(Slice key)
		{
			if (key.IsNull) return Slice.Nil;

			var prefix = GetKeyPrefix();

			// ensure that the key starts with the prefix
			if (!key.StartsWith(prefix)) FailKeyOutOfBound(key);

			return key.Substring(prefix.Count);
		}

		[NotNull]
		public Slice[] ExtractAndCheck([NotNull] Slice[] keys)
		{
			if (keys == null) throw new ArgumentNullException("keys");

			var prefix = GetKeyPrefix();
			var results = new Slice[keys.Length];
			for (int i = 0; i < keys.Length; i++)
			{
				var key = keys[i];
				if (!key.IsNull)
				{
					if (!key.StartsWith(prefix)) FailKeyOutOfBound(key);
					results[i] = key.Substring(prefix.Count);
				}
			}
			return results;
		}

		/// <summary>Gets a key range respresenting all keys strictly within the Subspace.</summary>
		/// <rereturns>Key range that, when passed to ClearRange() or GetRange(), would clear or return all the keys contained by this subspace, excluding the subspace prefix itself.</rereturns>
		public FdbKeyRange ToRange()
		{
			return ToRange(Slice.Nil);
		}

		/// <summary>Gets a key range respresenting all keys strictly within a sub-section of this Subspace.</summary>
		/// <param name="suffix">Suffix added to the subspace prefix</param>
		/// <rereturns>Key range that, when passed to ClearRange() or GetRange(), would clear or return all the keys contained by this subspace, excluding the subspace prefix itself.</rereturns>
		public virtual FdbKeyRange ToRange(Slice suffix)
		{
			return FdbTuple.ToRange(GetKeyPrefix().Concat(suffix));
		}

		#endregion

		#region IEquatable / IComparable...

		/// <summary>Compare this subspace with another subspace</summary>
		public int CompareTo(FdbSubspace other)
		{
			if (other == null) return +1;
			if (object.ReferenceEquals(this, other)) return 0;
			return this.InternalKey.CompareTo(other.InternalKey);
		}

		/// <summary>Test if both subspaces have the same prefix</summary>
		public bool Equals(FdbSubspace other)
		{
			return other != null && (object.ReferenceEquals(this, other) || this.InternalKey.Equals(other.InternalKey));
		}

		/// <summary>Test if an object is a subspace with the same prefix</summary>
		public override bool Equals(object obj)
		{
			return Equals(obj as FdbSubspace);
		}

		/// <summary>Compute a hashcode based on the prefix of this subspace</summary>
		/// <returns></returns>
		public override int GetHashCode()
		{
			return this.InternalKey.GetHashCode();
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
			var prefix = this.InternalKey;

			// don't touch to nil and keys inside the globalspace
			if (key.IsNull || key.StartsWith(prefix)) return key;

			// let the system keys pass
			if (allowSystemKeys && key.Count > 0 && key[0] == 255) return key;

			// The key is outside the bounds, and must be corrected
			// > return empty if we are before
			// > return \xFF if we are after
			if (key < GetKeyPrefix())
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
			string msg = String.Format("The key {0} does not belong to subspace {1}", FdbKey.Dump(key), this.ToString());
#else
			string msg = "The specifed key does not belong to this subspace";
#endif
			throw new ArgumentException(msg, "key");
		}

		/// <summary>Return a user-friendly representation of a key from this subspace</summary>
		/// <param name="key">Key that is contained in this subspace</param>
		/// <returns>Printable version of this key, minus the subspace prefix</returns>
		[NotNull]
		public virtual string DumpKey(Slice key)
		{
			// note: we can't use ExtractAndCheck(...) because it may throw in derived classes
			var prefix = this.InternalKey;
			if (!key.StartsWith(prefix)) FailKeyOutOfBound(key);

			return FdbKey.Dump(key.Substring(prefix.Count));
		}

		/// <summary>Printable representation of this subspace</summary>
		public override string ToString()
		{
			return String.Format("Subspace({0})", this.InternalKey.ToString());
		}

		#endregion

	}

}
