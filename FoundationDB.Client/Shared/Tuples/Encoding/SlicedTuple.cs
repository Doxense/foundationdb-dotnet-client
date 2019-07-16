#region BSD License
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
	using Doxense.Runtime.Converters;

	/// <summary>Lazily-evaluated tuple that was unpacked from a key</summary>
	public sealed class SlicedTuple : IVarTuple, ITupleSerializable
	{
		// STuple.Unpack() splits a key into an array of slices (one for each item). We hold onto these slices, and only deserialize them if needed.
		// This is helpful because in most cases, the app code will only want to get the last few items (e.g: tuple[-1]) or skip the first few items (some subspace).
		// We also support offset/count so that Splicing is efficient (used a lot to remove the suffixes from keys)

		/// <summary>Buffer containing the original slices.</summary>
		private readonly ReadOnlyMemory<Slice> m_slices;

		private int? m_hashCode;

		public SlicedTuple(ReadOnlyMemory<Slice> slices)
		{
			m_slices = slices;
		}

		void ITupleSerializable.PackTo(ref TupleWriter writer)
		{
			PackTo(ref writer);
		}

		internal void PackTo(ref TupleWriter writer)
		{
			foreach(var slice in m_slices.Span)
			{
				writer.Output.WriteBytes(slice);
			}
		}

		public int Count => m_slices.Length;

		public object this[int index] => TuplePackers.DeserializeBoxed(GetSlice(index));

		public IVarTuple this[int? fromIncluded, int? toExcluded]
		{
			get
			{
				int count = m_slices.Length;
				int begin = fromIncluded.HasValue ? TupleHelpers.MapIndexBounded(fromIncluded.Value, count) : 0;
				int end = toExcluded.HasValue ? TupleHelpers.MapIndexBounded(toExcluded.Value, count) : count;

				int len = end - begin;
				if (len <= 0) return STuple.Empty;
				if (begin == 0 && len == count) return this;
				return new SlicedTuple(m_slices.Slice(begin, len));
			}
		}

		public T Get<T>(int index)
		{
			return TuplePacker<T>.Deserialize(GetSlice(index));
		}

		public T Last<T>()
		{
			int count = m_slices.Length;
			if (count == 0) throw new InvalidOperationException("Tuple is empty");
			return TuplePacker<T>.Deserialize(m_slices.Span[count - 1]);
		}

		public Slice GetSlice(int index)
		{
			return m_slices.Span[TupleHelpers.MapIndex(index, m_slices.Length)];
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
			var slices = m_slices.Span;
			for (int i = 0; i < slices.Length;i++)
			{
				array[i + offset] = TuplePackers.DeserializeBoxed(slices[i]);
			}
		}

		public IEnumerator<object> GetEnumerator()
		{
			//note: I'm not sure if we're allowed to use a local variable of type Span<..> in here?
			for (int i = 0; i < m_slices.Length; i++)
			{
				yield return TuplePackers.DeserializeBoxed(m_slices.Span[i]);
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
				// compare slices!
				var left = m_slices.Span;
				var right = sliced.m_slices.Span;
				if (left.Length != right.Length) return false;
				for (int i = 0; i < left.Length; i++)
				{
					if (left[i] != right[i]) return false;
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
			var slices = m_slices.Span;
			for (int i = 0; i < slices.Length; i++)
			{
				h = HashCodes.Combine(h, comparer.GetHashCode(slices[i]));
			}
			if (canUseCache) m_hashCode = h;
			return h;
		}

	}

}
