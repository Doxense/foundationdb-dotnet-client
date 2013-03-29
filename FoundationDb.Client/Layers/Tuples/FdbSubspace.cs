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
		private readonly IFdbTuple Tuple;
		private readonly byte[] RawPrefix;

		public FdbSubspace(string prefix)
		{
			var tuple = new FdbTuple<string>(prefix);
			this.RawPrefix = tuple.ToBytes();
			this.Tuple = tuple;
		}

		public FdbSubspace(byte[] prefix)
		{
			var tuple = new FdbTuple<byte[]>(prefix);
			this.RawPrefix = tuple.ToBytes();
			this.Tuple = tuple;
		}

		public FdbSubspace(IFdbTuple prefix)
		{
			this.RawPrefix = prefix.ToBytes();
			this.Tuple = prefix;
		}

		public void AppendTo(BinaryWriteBuffer writer)
		{
			writer.WriteBytes(this.RawPrefix);
		}

		public void PackTo(BinaryWriteBuffer writer)
		{
			FdbTuplePackers.SerializeTo(writer, this.RawPrefix);
		}

		private BinaryWriteBuffer OpenBuffer(int extraBytes = 0)
		{
			var writer = new BinaryWriteBuffer();
			if (extraBytes > 0) writer.EnsureBytes(extraBytes + this.RawPrefix.Length);
			writer.WriteBytes(this.RawPrefix);
			return writer;
		}

		public ArraySegment<byte> GetKeyBytes<T>(T key)
		{
			var writer = OpenBuffer();
			FdbTuplePacker<T>.SerializeTo(writer, key);
			return writer.ToArraySegment();
		}

		public ArraySegment<byte> GetKeyBytes(ArraySegment<byte> keyBlob)
		{
			var writer = OpenBuffer(keyBlob.Count);
			writer.WriteBytes(keyBlob);
			return writer.ToArraySegment();
		}

		public ArraySegment<byte> GetKeyBytes(IFdbKey tuple)
		{
			var writer = new BinaryWriteBuffer();
			writer.WriteBytes(this.RawPrefix);
			tuple.PackTo(writer);
			return writer.ToArraySegment();
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

		public IFdbTuple AppendRange(IFdbTuple value)
		{
			if (value == null) throw new ArgumentNullException("value");

			return this.Tuple.Concat(value);
		}

		IEnumerator<object> IEnumerable<object>.GetEnumerator()
		{
			return this.Tuple.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.Tuple.GetEnumerator();
		}


	}

}
