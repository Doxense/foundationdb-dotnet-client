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

namespace Doxense.Collections.Tuples.Encoding
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using Doxense.Collections.Tuples;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Runtime.Converters;

	/// <summary>Lazily-evaluated tuple that was unpacked from a key</summary>
	public sealed class SlicedTuple : IVarTuple, ITupleSerializable
	{
		// STuple.Unpack() splits a key into an array of slices (one for each item). We hold onto these slices, and only deserialize them if needed.
		// This is helpful because in most cases, the app code will only want to get the last few items (e.g: tuple[-1]) or skip the first few items (some subspace).
		// We also support offset/count so that Splicing is efficient (used a lot to remove the suffixes from keys)

		/// <summary>Buffer containing the original slices. Note: can be bigger than the size of the tuple</summary>
		private readonly Slice[] m_slices;
		/// <summary>Start offset of the first slice of this tuple</summary>
		private readonly int m_offset;
		/// <summary>Number of slices in this tuple.</summary>
		private readonly int m_count;

		private int? m_hashCode;

		public SlicedTuple(Slice[] slices, int offset, int count)
		{
			Contract.Requires(slices != null && offset >= 0 && count >= 0);
			Contract.Requires(offset + count <= slices.Length);

			m_slices = slices;
			m_offset = offset;
			m_count = count;
		}

		void ITupleSerializable.PackTo(ref TupleWriter writer)
		{
			PackTo(ref writer);
		}
		internal void PackTo(ref TupleWriter writer)
		{
			var slices = m_slices;
			int offset = m_offset;
			int count = m_count;
			for (int i = 0; i < count; i++)
			{
				writer.Output.WriteBytes(slices[i + offset]);
			}
		}

		public int Count => m_count;

		public object this[int index] => TuplePackers.DeserializeBoxed(GetSlice(index));

		public IVarTuple this[int? fromIncluded, int? toExcluded]
		{
			get
			{
				int begin = fromIncluded.HasValue ? TupleHelpers.MapIndexBounded(fromIncluded.Value, m_count) : 0;
				int end = toExcluded.HasValue ? TupleHelpers.MapIndexBounded(toExcluded.Value, m_count) : m_count;

				int len = end - begin;
				if (len <= 0) return STuple.Empty;
				if (begin == 0 && len == m_count) return this;
				return new SlicedTuple(m_slices, m_offset + begin, len);
			}
		}

		public T Get<T>(int index)
		{
			return TuplePacker<T>.Deserialize(GetSlice(index));
		}

		public T Last<T>()
		{
			if (m_count == 0) throw new InvalidOperationException("Tuple is empty");
			return TuplePacker<T>.Deserialize(m_slices[m_offset + m_count - 1]);
		}

		public Slice GetSlice(int index)
		{
			return m_slices[m_offset + TupleHelpers.MapIndex(index, m_count)];
		}

		IVarTuple IVarTuple.Append<T>(T value)
		{
			throw new NotSupportedException();
		}

		IVarTuple IVarTuple.Concat(IVarTuple tuple)
		{
			throw new NotSupportedException();
		}

		public void CopyTo(object[] array, int offset)
		{
			for (int i = 0; i < m_count;i++)
			{
				array[i + offset] = TuplePackers.DeserializeBoxed(m_slices[i + m_offset]);
			}
		}

		public IEnumerator<object> GetEnumerator()
		{
			for (int i = 0; i < m_count; i++)
			{
				yield return TuplePackers.DeserializeBoxed(m_slices[i + m_offset]);
			}
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public override string ToString()
		{
			//OPTIMIZE: this could be optimized, because it may be called a lot when logging is enabled on keys parsed from range reads
			// => each slice has a type prefix that could be used to format it to a StringBuilder faster, maybe?
			return STuple.Formatter.ToString(this);
		}

		public override bool Equals(object obj)
		{
			return obj != null && ((IStructuralEquatable)this).Equals(obj, SimilarValueComparer.Default);
		}

		public bool Equals(IVarTuple other)
		{
			return !object.ReferenceEquals(other, null) && ((IStructuralEquatable)this).Equals(other, SimilarValueComparer.Default);
		}

		public override int GetHashCode()
		{
			return ((IStructuralEquatable)this).GetHashCode(SimilarValueComparer.Default);
		}

		bool IStructuralEquatable.Equals(object other, IEqualityComparer comparer)
		{
			if (object.ReferenceEquals(this, other)) return true;
			if (other == null) return false;

			var sliced = other as SlicedTuple;
			if (!object.ReferenceEquals(sliced, null))
			{
				if (sliced.m_count != m_count) return false;

				// compare slices!
				for (int i = 0; i < m_count; i++)
				{
					if (m_slices[i + m_offset] != sliced.m_slices[i + sliced.m_offset]) return false;
				}
				return false;
			}

			return TupleHelpers.Equals(this, other, comparer);
		}

		int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
		{
			bool canUseCache = object.ReferenceEquals(comparer, SimilarValueComparer.Default);

			if (m_hashCode.HasValue && canUseCache)
			{
				return m_hashCode.Value;
			}

			int h = 0;
			for (int i = 0; i < m_count; i++)
			{
				h = HashCodes.Combine(h, comparer.GetHashCode(m_slices[i + m_offset]));
			}
			if (canUseCache) m_hashCode = h;
			return h;
		}

	}

}
