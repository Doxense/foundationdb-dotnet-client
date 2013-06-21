#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
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

using FoundationDb.Client;
using FoundationDb.Client.Converters;
using FoundationDb.Client.Utils;
using System;
using System.Collections.Generic;

namespace FoundationDb.Layers.Tuples
{

	/// <summary>Specialized tuple that maps a parsed key</summary>
	internal sealed class FdbSlicedTuple : IFdbTuple, IEquatable<FdbSlicedTuple>
	{
		// FdbTuple.Unpack() splits a key into an array of slices (one for each item). We hold onto these slices, and only "deserialize" them if needed.
		// This is helpful because in most keys, the app code will only want to get the last few items (e.g: tuple[-1]) or skip the first few items (some subspace).
		// We also support windowing so that Splicing is efficient (used a lot by FdbSubspace.Unpack(...) to remove the subspace itself)

		/// <summary>Buffer containing the original slices. Note: can be bigger than the size of the tuple</summary>
		private readonly Slice[] m_slices;
		/// <summary>Start offset of the first slice of this tuple</summary>
		private readonly int m_offset;
		/// <summary>Number of slices in this tuple.</summary>
		private readonly int m_count;

		private int? m_hashCode;

		public FdbSlicedTuple(Slice[] slices, int offset, int count)
		{
			Contract.Requires(slices != null && offset >= 0 && count >= 0);
			Contract.Requires(offset + count <= slices.Length);

			m_slices = slices;
			m_offset = offset;
			m_count = count;
		}

		public void PackTo(FdbBufferWriter writer)
		{
			var slices = m_slices;
			for (int n = m_count, p = m_offset; n > 0; n--)
			{
				writer.WriteBytes(slices[p++]);
			}
		}

		public Slice ToSlice()
		{
			// merge all the slices making up this segment
			//TODO: should we get the sum of all slices to pre-allocated the buffer ?
			var writer = new FdbBufferWriter();
			PackTo(writer);
			return writer.ToSlice();
		}

		public int Count
		{
			get { return m_count; }
		}

		public object this[int index]
		{
			get { return FdbTuplePackers.DeserializeObject(GetSlice(index)); }
		}

		public IFdbTuple this[int? from, int? to]
		{
			get
			{
				int start = FdbTuple.MapIndexBounded(from ?? 0, m_count);
				int end = FdbTuple.MapIndexBounded(to ?? -1, m_count);
				int len = end - start + 1;
				if (len <= 0) return FdbTuple.Empty;
				return new FdbSlicedTuple(m_slices, start, len);
			}
		}

		public R Get<R>(int index)
		{
			return FdbConverters.ConvertBoxed<R>(FdbTuplePackers.DeserializeObject(GetSlice(index)));
		}

		public Slice GetSlice(int index)
		{
			return m_slices[m_offset + FdbTuple.MapIndex(index, m_count)];
		}

		public IFdbTuple Append<T>(T value)
		{
			throw new NotImplementedException();
		}

		public void CopyTo(object[] array, int offset)
		{
			for (int i = 0; i < m_count;i++)
			{
				array[i + offset] = FdbTuplePackers.DeserializeObject(m_slices[i + m_offset]);
			}
		}

		public IEnumerator<object> GetEnumerator()
		{
			for (int i = 0; i < m_count; i++)
			{
				yield return FdbTuplePackers.DeserializeObject(m_slices[i + m_offset]);
			}
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public override string ToString()
		{
			return FdbTuple.ToString(this);
		}

		public bool Equals(FdbSlicedTuple other)
		{
			if (other == null || other.m_count != m_count) return false;

			// compare slices!
			for (int i = 0; i < m_count; i++)
			{
				if (m_slices[i + m_offset] != other.m_slices[i + other.m_offset]) return false;
			}
			return false;
		}

		public bool Equals(IFdbTuple other)
		{
			if (other == null || other.Count != m_count) return false;

			// compare deserialized values
			int p = 0;
			foreach (var item in other)
			{
				if (p >= m_count) return false; // note: this means the tuple's enumerator is buggy
				if (!ComparisonHelper.AreSimilar(this[p++], item)) return false;
			}
			return p >= m_count;
		}

		public override bool Equals(object obj)
		{
			var tuple = obj as FdbSlicedTuple;
			if (tuple != null) return Equals(tuple);
			return Equals(obj as IFdbTuple);
		}

		public override int GetHashCode()
		{
			if (!m_hashCode.HasValue)
			{
				int h = 0;
				for (int i = 0; i < m_count; i++)
				{
					var item = m_slices[i + m_offset];
					h ^= item != null ? item.GetHashCode() : -1;
				}
				m_hashCode = h;
			}
			return m_hashCode.GetValueOrDefault();
		}

	}

}
