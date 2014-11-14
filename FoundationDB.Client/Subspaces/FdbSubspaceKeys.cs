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
	using FoundationDB.Client.Utils;
	using JetBrains.Annotations;
	using System;
	using System.Linq;
	using System.Collections.Generic;

	public struct FdbSubspaceKeys
	{
		private readonly IFdbSubspace m_subspace;

		public FdbSubspaceKeys(IFdbSubspace subspace)
		{
			Contract.Requires(subspace != null);
			m_subspace = subspace;
		}

		public IFdbSubspace Subspace
		{
			[NotNull]  //note: except for corner cases like default(FdbTupleSubspace) or unallocated value
			get
			{ return m_subspace; }
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		/// <remarks>This is an alias to calling <code>subspace.Keys.Concat(key)</code></remarks>
		public Slice this[Slice key]
		{
			get { return m_subspace.ConcatKey(key); }
		}

		/// <summary>Append a serializable key to the subspace key</summary>
		/// <param name="key">Instance that can serialize itself into a binary key</param>
		/// <returns>Return Slice : 'subspace.Key + key.ToFoundationDbKey()'</returns>
		/// <remarks>This is an alias to calling <code>subspace.Keys.Concat&lt;IFdbKey&gt;(key)</code></remarks>
		public Slice this[[NotNull] IFdbKey key]
		{
			get
			{
				if (key == null) throw new ArgumentNullException("key");
				return m_subspace.ConcatKey(key.ToFoundationDbKey());
			}
		}

		public Slice Concat(Slice key)
		{
			return m_subspace.ConcatKey(key);
		}


		public Slice Concat<TKey>([NotNull] TKey key)
			where TKey : IFdbKey
		{
			if (key == null) throw new ArgumentNullException("key");
			return m_subspace.ConcatKey(key.ToFoundationDbKey());
		}

		[NotNull]
		public Slice[] Concat([NotNull] IEnumerable<Slice> keys)
		{
			return m_subspace.ConcatKeys(keys);
		}

		[NotNull]
		public Slice[] Concat([NotNull] params Slice[] keys)
		{
			return m_subspace.ConcatKeys(keys);
		}

		[NotNull]
		public Slice[] Concat<TKey>([NotNull] IEnumerable<TKey> keys)
			where TKey : IFdbKey
		{
			if (keys == null) throw new ArgumentNullException("keys");
			return m_subspace.ConcatKeys(keys.Select((key) => key.ToFoundationDbKey()));
		}

		[NotNull]
		public Slice[] Concat<TKey>([NotNull] params TKey[] keys)
			where TKey : IFdbKey
		{
			return Concat<TKey>((IEnumerable<TKey>)keys);
		}

		public Slice BoundCheck(Slice key)
		{
			return m_subspace.BoundCheck(key, allowSystemKeys: true);
		}

		public Slice Extract(Slice key)
		{
			return m_subspace.ExtractKey(key, boundCheck: true);
		}

		[NotNull]
		public Slice[] Extract([NotNull] params Slice[] keys)
		{
			return m_subspace.ExtractKeys(keys, boundCheck: true);
		}

		[NotNull]
		public Slice[] Extract([NotNull] IEnumerable<Slice> keys)
		{
			return m_subspace.ExtractKeys(keys, boundCheck: true);
		}

		public FdbKeyRange ToRange()
		{
			return m_subspace.ToRange();
		}

		public FdbKeyRange ToRange(Slice key)
		{
			return m_subspace.ToRange(key);
		}

		public FdbKeyRange ToRange<TKey>([NotNull] IFdbKey key)
			where TKey : IFdbKey
		{
			if (key == null) throw new ArgumentNullException("key");
			return m_subspace.ToRange(key.ToFoundationDbKey());
		}

	}

}
