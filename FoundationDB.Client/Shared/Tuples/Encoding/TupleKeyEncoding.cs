#region BSD License
/* Copyright (c) 2013-2023 Doxense SAS
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
	using Doxense.Serialization.Encoders;

	/// <summary>Encoding that uses the Tuple Binary Encoding format</summary>
	public sealed class TupleKeyEncoding : IDynamicTypeSystem
	{

		public static readonly TupleKeyEncoding Instance = new TupleKeyEncoding();
		
		public string Name => "TuPack";

		#region Keys...

		public IDynamicKeyEncoder GetDynamicKeyEncoder()
		{
			return TupleKeyEncoder.Instance;
		}

		public IKeyEncoder<T1> GetKeyEncoder<T1>()
		{
			return TupleEncoder.Encoder<T1>.Default;
		}

		public ICompositeKeyEncoder<T1, T2> GetKeyEncoder<T1, T2>()
		{
			return TupleEncoder.CompositeEncoder<T1, T2>.Default;
		}

		public ICompositeKeyEncoder<T1, T2, T3> GetKeyEncoder<T1, T2, T3>()
		{
			return TupleEncoder.CompositeEncoder<T1, T2, T3>.Default;
		}

		public ICompositeKeyEncoder<T1, T2, T3, T4> GetKeyEncoder<T1, T2, T3, T4>()
		{
			return TupleEncoder.CompositeEncoder<T1, T2, T3, T4>.Default;
		}

		public ICompositeKeyEncoder<T1, T2, T3, T4, T5> GetEncoder<T1, T2, T3, T4, T5>()
		{
			return TupleEncoder.CompositeEncoder<T1, T2, T3, T4, T5>.Default;
		}

		public ICompositeKeyEncoder<T1, T2, T3, T4, T5, T6> GetEncoder<T1, T2, T3, T4, T5, T6>()
		{
			return TupleEncoder.CompositeEncoder<T1, T2, T3, T4, T5, T6>.Default;
		}

		#endregion

		#region Values...

		IValueEncoder<TValue, TStorage> IValueEncoding.GetValueEncoder<TValue, TStorage>()
		{
			if (typeof(TStorage) != typeof(Slice)) throw new NotSupportedException($"Tuple Encoding does not support {typeof(TStorage).Name} as a storage type.");
			return (IValueEncoder<TValue, TStorage>) GetValueEncoder<TValue>();
		}

		public IValueEncoder<TValue> GetValueEncoder<TValue>()
		{
			return TupleEncoder.Encoder<TValue>.Default;
		}

		#endregion

	}
}
