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
	using System.Runtime.CompilerServices;
	using Doxense.Collections.Tuples;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using JetBrains.Annotations;

	/// <summary>Subspace that is created from a constant binary prefix</summary>
	/// <remarks>The prefix will never change during the lifetime of this object</remarks>
	public sealed class BinaryPrefixContext : IKeyContext
	{

		/// <summary>Prefix of this context</summary>
		private readonly Slice Key;

		/// <summary>Precomputed range that encompass all the keys in this subspace</summary>
		private readonly KeyRange Range;

		internal BinaryPrefixContext(Slice key, KeyRange range)
		{
			this.Key = key;
			this.Range = range;
		}

		/// <summary>Context without prefix</summary>
		[NotNull]
		public static readonly BinaryPrefixContext Empty = new BinaryPrefixContext(Slice.Empty, new KeyRange(Slice.Empty, FdbKey.MaxValue));

		/// <summary>Context for the root Directory Layer</summary>
		[NotNull]
		public static readonly BinaryPrefixContext Directory = new BinaryPrefixContext(FdbKey.Directory, new KeyRange(FdbKey.Directory, FdbKey.MaxValue));

		/// <summary>Create a context using a binary key prefix</summary>
		[Pure, NotNull]
		public static BinaryPrefixContext Create(Slice prefix)
		{
			if (prefix.Count == 0) return Empty;
			if (prefix.Count == 1 && prefix[0] == 254) return Directory;

			prefix = prefix.Memoize();
			return new BinaryPrefixContext(prefix, KeyRange.StartsWith(prefix));
		}

		/// <summary>Create a context using a binary key prefix</summary>
		[Pure, NotNull]
		public static BinaryPrefixContext Create([NotNull] byte[] prefix)
		{
			Contract.NotNull(prefix, nameof(prefix));
			return Create(prefix.AsSlice());
		}

		/// <summary>Create a context using an UTF-8 encoded string prefix</summary>
		[Pure, NotNull]
		public static BinaryPrefixContext Create([NotNull] string prefix)
		{
			Contract.NotNull(prefix, nameof(prefix));
			var key = Slice.FromStringUtf8(prefix);
			return new BinaryPrefixContext(key, KeyRange.StartsWith(key));
		}

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
			var prefix = this.Key.Concat(suffix);
			return new BinaryPrefixContext(prefix, KeyRange.StartsWith(prefix));
		}
	}
}
