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
	using System.Linq;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Diagnostics;

	/// <summary>Adds a prefix on every keys, to group them inside a common subspace</summary>
	public class FdbSubspace : IFdbSubspace, IFdbKey, IEquatable<IFdbSubspace>, IComparable<IFdbSubspace>
	{
		/// <summary>Empty subspace, that does not add any prefix to the keys</summary>
		public static readonly FdbSubspace Empty = new FdbSubspace(Slice.Empty);

		/// <summary>Binary prefix of this subspace</summary>
		private Slice m_rawPrefix; //PERF: readonly struct

		/// <summary>Helper used to deal with keys in this subspace</summary>
		private FdbSubspaceKeys m_keys; // cached for perf reasons

		/// <summary>Helper used to deal with keys in this subspace</summary>
		private FdbSubspaceTuples m_tuples; // cached for perf reasons

		/// <summary>Returns the key of this directory subspace</summary>
		/// <remarks>This should only be used by methods that can use the key internally, even if it is not supposed to be exposed (as is the case for directory partitions)</remarks>
		protected Slice InternalKey
		{
			get { return m_rawPrefix; }
		}

		#region Constructors...

		/// <summary>Wraps an existing subspace, without copying the prefix (if possible)</summary>
		protected FdbSubspace([NotNull] IFdbSubspace copy)
		{
			if (copy == null) throw new ArgumentNullException("copy");
			var sub = copy as FdbSubspace;
			Slice key = sub != null ? sub.m_rawPrefix : copy.ToFoundationDbKey();
			if (key.IsNull) throw new ArgumentException("The subspace key cannot be null. Use Slice.Empty if you want a subspace with no prefix.", "copy");
			m_rawPrefix = key;
			m_keys = new FdbSubspaceKeys(this);
			m_tuples = new FdbSubspaceTuples(this);
		}

		/// <summary>Create a new subspace from a binary prefix</summary>
		/// <param name="rawPrefix">Prefix of the new subspace</param>
		/// <param name="copy">If true, take a copy of the prefix</param>
		protected FdbSubspace(Slice rawPrefix, bool copy)
		{
			if (rawPrefix.IsNull) throw new ArgumentException("The subspace key cannot be null. Use Slice.Empty if you want a subspace with no prefix.", "rawPrefix");
			if (copy) rawPrefix = rawPrefix.Memoize();
			m_rawPrefix = rawPrefix.Memoize();
			m_keys = new FdbSubspaceKeys(this);
			m_tuples = new FdbSubspaceTuples(this);
		}

		/// <summary>Create a new subspace from a binary prefix</summary>
		/// <param name="rawPrefix">Prefix of the new subspace</param>
		public FdbSubspace(Slice rawPrefix)
			: this(rawPrefix, true)
		{ }

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
			if (tuple == null) throw new ArgumentNullException("tuple");
			return new FdbSubspace(tuple.ToSlice(), true);
		}

		/// <summary>Clone this subspace</summary>
		/// <returns>New Subspace that uses the same prefix key</returns>
		/// <remarks>Hint: Cloning a special Subspace like a <see cref="FoundationDB.Layers.Directories.FdbDirectoryLayer"/>  or <see cref="FoundationDB.Layers.Directories.FdbDirectoryPartition"/> will not keep all the "special abilities" of the parent.</remarks>
		[NotNull]
		public static FdbSubspace Copy([NotNull] IFdbSubspace subspace)
		{
			var sub = subspace as FdbSubspace;
			if (sub != null)
			{
				//SPOILER WARNING: You didn't hear it from me, but some say that you can use this to bypass the fact that FdbDirectoryPartition.get_Key and ToRange() throws in v2.x ... If you bypass this protection and bork your database, don't come crying!
				return new FdbSubspace(sub.InternalKey, true);
			}
			else
			{
				return new FdbSubspace(subspace.Key, true);
			}
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

		/// <summary>Returns the key to use when creating direct keys that are inside this subspace</summary>
		/// <returns>Prefix that must be added to all keys created by this subspace</returns>
		/// <remarks>Subspaces that disallow the creation of keys should override this method and throw an exception</remarks>
		[DebuggerStepThrough]
		protected virtual Slice GetKeyPrefix()
		{
			return m_rawPrefix;
		}

		/// <summary>Return a view of all the possible binary keys of this subspace</summary>
		public FdbSubspaceKeys Keys
		{
			[DebuggerStepThrough]
			get { return m_keys; }
		}

		/// <summary>Returns an helper object that knows how to create sub-partitions of this subspace</summary>
		public FdbSubspacePartition Partition
		{
			//note: not cached, because this is probably not be called frequently (except in the init path)
			[DebuggerStepThrough]
			get { return new FdbSubspacePartition(this); }
		}

		/// <summary>Return a view of all the possible tuple-based keys of this subspace</summary>
		public FdbSubspaceTuples Tuples
		{
			[DebuggerStepThrough]
			get { return m_tuples; }
		}

		/// <summary>Tests whether the specified <paramref name="key"/> starts with this Subspace's prefix, indicating that the Subspace logically contains <paramref name="key"/>.</summary>
		/// <param name="key">The key to be tested</param>
		/// <remarks>The key Slice.Nil is not contained by any Subspace, so subspace.Contains(Slice.Nil) will always return false</remarks>
		public virtual bool Contains(Slice key)
		{
			return key.HasValue && key.StartsWith(this.InternalKey);
		}

		/// <summary>Append a key to the subspace key</summary>
		/// <remarks>This is the equivalent of calling 'subspace.Key + key'</remarks>
		public Slice ConcatKey(Slice key)
		{
			//REVIEW: what to do with Slice.Nil?
			return GetKeyPrefix().Concat(key);
		}

		/// <summary>Merge an array of keys with the subspace's prefix, all sharing the same buffer</summary>
		/// <param name="keys">Array of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[NotNull]
		public Slice[] ConcatKeys([NotNull] IEnumerable<Slice> keys)
		{
			if (keys == null) throw new ArgumentNullException("keys");
			//REVIEW: what to do with keys that are Slice.Nil ?
			return Slice.ConcatRange(GetKeyPrefix(), keys);
		}

		/// <summary>Remove the subspace prefix from a binary key, and only return the tail, or Slice.Nil if the key does not fit inside the namespace</summary>
		/// <param name="key">Complete key that contains the current subspace prefix, and a binary suffix</param>
		/// <param name="boundCheck">If true, verify that <paramref name="key"/> is inside the bounds of the subspace</param>
		/// <returns>Binary suffix of the key (or Slice.Empty is the key is exactly equal to the subspace prefix). If the key is outside of the subspace, returns Slice.Nil</returns>
		/// <remarks>This is the inverse operation of <see cref="IFdbSubspace.ConcatKey(Slice)"/></remarks>
		/// <exception cref="System.ArgumentException">If <paramref name="boundCheck"/> is true and <paramref name="key"/> is outside the current subspace.</exception>
		public Slice ExtractKey(Slice key, bool boundCheck = false)
		{
			if (key.IsNull) return Slice.Nil;

			var prefix = GetKeyPrefix();
			if (!key.StartsWith(prefix))
			{
				if (boundCheck) FailKeyOutOfBound(key);
				return Slice.Nil;
			}

			return key.Substring(prefix.Count);
		}

		/// <summary>Remove the subspace prefix from a batch of binary keys, and only return the tail, or Slice.Nil if a key does not fit inside the namespace</summary>
		/// <param name="keys">Sequence of complete keys that contains the current subspace prefix, and a binary suffix</param>
		/// <param name="boundCheck">If true, verify that each key in <paramref name="keys"/> is inside the bounds of the subspace</param>
		/// <returns>Array of only the binary suffix of the keys, Slice.Empty for a key that is exactly equal to the subspace prefix, or Slice.Nil for a key that is outside of the subspace</returns>
		/// <exception cref="System.ArgumentException">If <paramref name="boundCheck"/> is true and at least one key in <paramref name="keys"/> is outside the current subspace.</exception>
		[NotNull]
		public Slice[] ExtractKeys([NotNull] IEnumerable<Slice> keys, bool boundCheck = false)
		{
			if (keys == null) throw new ArgumentNullException("keys");

			var prefix = GetKeyPrefix();

			var arr = keys as Slice[];
			if (arr != null)
			{ // fast-path for Sice[] (frequent for range reads)

				var res = new Slice[arr.Length];
				for (int i = 0; i < arr.Length; i++)
				{
					if (arr[i].StartsWith(prefix))
					{
						res[i] = arr[i].Substring(prefix.Count);
					}
					else if (boundCheck)
					{
						FailKeyOutOfBound(arr[i]);
					}
				}
				return res;
			}
			else
			{  // slow path for the rest
				var coll = keys as ICollection<Slice>;
				var res = coll != null ? new List<Slice>(coll.Count) : new List<Slice>();
				foreach(var key in keys)
				{
					if (key.StartsWith(prefix))
					{
						res.Add(key.Substring(prefix.Count));
					}
					else if (boundCheck)
					{
						FailKeyOutOfBound(key);
					}
				}
				return res.ToArray();
			}
		}

		#endregion

		#region IEquatable / IComparable...

		/// <summary>Compare this subspace with another subspace</summary>
		public int CompareTo(IFdbSubspace other)
		{
			if (other == null) return +1;
			if (object.ReferenceEquals(this, other)) return 0;
			var sub = other as FdbSubspace;
			if (sub != null)
				return this.InternalKey.CompareTo(sub.InternalKey);
			else
				return this.InternalKey.CompareTo(other.ToFoundationDbKey());
		}

		/// <summary>Test if both subspaces have the same prefix</summary>
		public bool Equals(IFdbSubspace other)
		{
			if (other == null) return false;
			if (object.ReferenceEquals(this, other)) return true;
			var sub = other as FdbSubspace;
			if (sub != null)
				return this.InternalKey.Equals(sub.InternalKey);
			else
				return this.InternalKey.Equals(other.ToFoundationDbKey());
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
			return String.Format(CultureInfo.InvariantCulture, "Subspace({0})", this.InternalKey.ToString());
		}

		#endregion

	}

}
