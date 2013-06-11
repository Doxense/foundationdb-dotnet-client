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

using FoundationDb.Client;
using FoundationDb.Client.Converters;
using FoundationDb.Client.Utils;
using System;
using System.Collections.Generic;

namespace FoundationDb.Layers.Tuples
{

	public sealed class FdbSlicedTuple : IFdbTuple
	{
		private readonly Slice Buffer;
		private readonly List<Slice> Slices;

		public FdbSlicedTuple(Slice buffer, List<Slice> slices)
		{
			this.Buffer = buffer;
			this.Slices = slices;
		}

		public void PackTo(FdbBufferWriter writer)
		{
			writer.WriteBytes(this.Buffer);
		}

		public Slice ToSlice()
		{
			return this.Buffer;
		}

		public int Count
		{
			get { return this.Slices.Count; }
		}

		public object this[int index]
		{
			get { return FdbTuplePackers.DeserializeObject(GetSlice(index)); }
		}

		public R Get<R>(int index)
		{
			return FdbConverters.ConvertBoxed<R>(FdbTuplePackers.DeserializeObject(GetSlice(index)));
		}

		public Slice GetSlice(int index)
		{
			var slices = this.Slices;
			return slices[FdbTuple.MapIndex(index, slices.Count)];
		}

		public IFdbTuple Append<T>(T value)
		{
			throw new NotImplementedException();
		}

		public void CopyTo(object[] array, int offset)
		{
			var slices = this.Slices;
			foreach (var slice in slices)
			{
				array[offset++] = FdbTuplePackers.DeserializeObject(slice);
			}
		}

		public IEnumerator<object> GetEnumerator()
		{
			var slices = this.Slices;
			foreach (var slice in slices)
			{
				yield return FdbTuplePackers.DeserializeObject(slice);
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

	}

}
