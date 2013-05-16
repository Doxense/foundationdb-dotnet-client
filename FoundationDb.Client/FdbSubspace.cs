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
	* Neither the name of the <organization> nor the
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

using FoundationDb.Client.Utils;
using System;
using System.Collections.Generic;

namespace FoundationDb.Client.Tuples
{

	/// <summary>Adds a prefix on every keys, to group them inside a common subspace</summary>
	public class FdbSubspace : IFdbTuple
	{
		/// <summary>Store a memoized version of the tuple to speed up serialization</summary>
		private readonly FdbMemoizedTuple Tuple;

		public FdbSubspace(string prefix)
		{
			this.Tuple = new FdbTuple<string>(prefix).Memoize();
		}

#if DEPRECATED
		public FdbSubspace(byte[] prefix)
		{
			var tuple = new FdbTuple<byte[]>(prefix);
			this.RawPrefix = tuple.ToByteArray();
			this.Tuple = tuple;
		}
#endif

		public FdbSubspace(IFdbTuple prefix)
		{
			this.Tuple = prefix.Memoize();
		}

		public void PackTo(FdbBufferWriter writer)
		{
			writer.WriteBytes(this.Tuple.Packed);
		}

		public Slice ToSlice()
		{
			return this.Tuple.Packed;
		}

		private FdbBufferWriter OpenBuffer(int extraBytes = 0)
		{
			var writer = new FdbBufferWriter();
			if (extraBytes > 0) writer.EnsureBytes(extraBytes + this.Tuple.PackedSize);
			writer.WriteBytes(this.Tuple.Packed);
			return writer;
		}

		public Slice GetKeyBytes<T>(T key)
		{
			var writer = OpenBuffer();
			FdbTuplePacker<T>.SerializeTo(writer, key);
			return writer.ToSlice();
		}

		public Slice GetKeyBytes(Slice keyBlob)
		{
			var writer = OpenBuffer(keyBlob.Count);
			writer.WriteBytes(keyBlob);
			return writer.ToSlice();
		}

		public Slice GetKeyBytes(IFdbTuple tuple)
		{
			var writer = new FdbBufferWriter();
			writer.WriteBytes(this.Tuple.Packed);
			tuple.PackTo(writer);
			return writer.ToSlice();
		}

		int IFdbTuple.Count
		{
			get { return this.Tuple.Count; }
		}

		object IFdbTuple.this[int index]
		{
			get { return this.Tuple[index]; }
		}
		
		public IFdbTuple Append<T>(T value)
		{
			return this.Tuple.Append<T>(value);
		}

		public IFdbTuple Append<T1, T2>(T1 value1, T2 value2)
		{
			return this.Tuple.Append<T1>(value1).Append<T2>(value2);
		}

		public IFdbTuple AppendRange(IFdbTuple value)
		{
			if (value == null) throw new ArgumentNullException("value");

			return this.Tuple.Concat(value);
		}

		public void CopyTo(object[] array, int offset)
		{
			this.Tuple.CopyTo(array, offset);
		}

		IEnumerator<object> IEnumerable<object>.GetEnumerator()
		{
			return this.Tuple.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.Tuple.GetEnumerator();
		}

		public override string ToString()
		{
			return this.Tuple.ToString();
		}

	}

}
