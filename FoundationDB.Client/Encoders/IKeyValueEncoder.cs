﻿#region BSD Licence
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

namespace FoundationDB.Client
{
	using FoundationDB.Layers.Tuples;
	using System;

	public interface IValueEncoder<T>
	{
		/// <summary>Encode a single value</summary>
		Slice EncodeValue(T value);

		/// <summary>Decode a single value</summary>
		/// <param name="encoded"></param>
		/// <returns></returns>
		T DecodeValue(Slice encoded);
	}

	public interface IKeyEncoder<T1>
	{
		/// <summary>Encode a single value</summary>
		Slice EncodeKey(T1 value);

		/// <summary>Decode a single value</summary>
		/// <param name="encoded"></param>
		/// <returns></returns>
		T1 DecodeKey(Slice encoded);
	}

	public interface ICompositeKeyEncoder<TTuple> : IKeyEncoder<TTuple>
			where TTuple : IFdbTuple
	{
		Slice EncodeComposite(TTuple key, int items);

		TTuple DecodeComposite(Slice encoded, int items);
	}

	public interface ICompositeKeyEncoder<T1, T2> : ICompositeKeyEncoder<FdbTuple<T1, T2>>
	{
		Slice EncodeKey(T1 value1, T2 value2);
	}

	public interface ICompositeKeyEncoder<T1, T2, T3> : ICompositeKeyEncoder<FdbTuple<T1, T2, T3>>
	{
		Slice EncodeKey(T1 value1, T2 value2, T3 value3);
	}

	public interface ICompositeKeyEncoder<T1, T2, T3, T4> : ICompositeKeyEncoder<FdbTuple<T1, T2, T3, T4>>
	{
		Slice EncodeKey(T1 value1, T2 value2, T3 value3, T4 value4);
	}

}
