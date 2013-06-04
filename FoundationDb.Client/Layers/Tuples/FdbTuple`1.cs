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

using FoundationDb.Client.Converters;
using FoundationDb.Client.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace FoundationDb.Client.Tuples
{

	/// <summary>Tuple that holds only one item</summary>
	/// <typeparam name="T1">Type of the item</typeparam>
	[DebuggerDisplay("({Item1})")]
	public struct FdbTuple<T1> : IFdbTuple
	{

		public readonly T1 Item1;

		public FdbTuple(T1 item1)
		{
			this.Item1 = item1;
		}

		public int Count { get { return 1; } }

		public object this[int index]
		{
			get
			{
				switch(index)
				{
					case 0: case -1: return this.Item1;
					default: throw new IndexOutOfRangeException();
				}
			}
		}

		public R Get<R>(int index)
		{
			if (index == 0 || index == -1) return FdbConverters.Convert<T1, R>(this.Item1);
			throw new IndexOutOfRangeException();
		}

		public void PackTo(FdbBufferWriter writer)
		{
			FdbTuplePacker<T1>.SerializeTo(writer, this.Item1);
		}

		IFdbTuple IFdbTuple.Append<T2>(T2 value)
		{
			return this.Append<T2>(value);
		}

		public FdbTuple<T1, T2> Append<T2>(T2 value)
		{
			return new FdbTuple<T1, T2>(this.Item1, value);
		}

		public void CopyTo(object[] array, int offset)
		{
			array[offset] = this.Item1;
		}

		public IEnumerator<object> GetEnumerator()
		{
			yield return this.Item1;
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public Slice ToSlice()
		{
			var writer = new FdbBufferWriter();
			PackTo(writer);
			return writer.ToSlice();
		}

		public override string ToString()
		{
			return "(" + this.Item1 + ")";
		}

		public override int GetHashCode()
		{
			return this.Item1 != null ? this.Item1.GetHashCode() : -1;
		}

	}

}
