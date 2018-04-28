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

namespace Doxense.Serialization.Encoders
{
	using System;
	using Doxense.Collections.Tuples.Encoding;
	using JetBrains.Annotations;

	/// <summary>Helper class for all key/value encoders</summary>
	public static partial class KeyValueEncoders
	{

		/// <summary>Encoders that use the Tuple Encoding, suitable for keys</summary>
		[PublicAPI]
		public static class Tuples
		{

			#region Keys

			[NotNull]
			public static IKeyEncoder<T1> Key<T1>()
			{
				return TupleEncoder.Encoder<T1>.Default;
			}

			[NotNull]
			public static ICompositeKeyEncoder<T1, T2> CompositeKey<T1, T2>()
			{
				return TupleEncoder.CompositeEncoder<T1, T2>.Default;
			}

			[NotNull]
			public static ICompositeKeyEncoder<T1, T2, T3> CompositeKey<T1, T2, T3>()
			{
				return TupleEncoder.CompositeEncoder<T1, T2, T3>.Default;
			}

			[NotNull]
			public static ICompositeKeyEncoder<T1, T2, T3, T4> CompositeKey<T1, T2, T3, T4>()
			{
				return TupleEncoder.CompositeEncoder<T1, T2, T3, T4>.Default;
			}

			#endregion

			#region Values...

			[NotNull]
			public static IValueEncoder<T> Value<T>()
			{
				return TupleEncoder.Encoder<T>.Default;
			}

			#endregion

		}

	}

}
