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

namespace FoundationDB.Client.Serializers
{
	using FoundationDB.Client.Converters;
	using FoundationDB.Layers.Tuples;
	using System;
	using System.Diagnostics;

	/// <summary>Very simple serializer that uses FdbConverters to convert values of type <typeparamref name="T"/> from/to Slice</summary>
	/// <typeparam name="T">Type of the value to serialize/deserialize</typeparam>
	public class FdbSliceSerializer<T> : ISliceSerializer<T>
	{
		#region Static Helpers...

		private static volatile FdbSliceSerializer<T> s_defaultSerializer;

		/// <summary>Default slice serializer for values of type <typeparam name="T"/></summary>
		public static FdbSliceSerializer<T> Default
		{
			get
			{
				var serializer = s_defaultSerializer;
				if (serializer == null)
				{
					serializer = CreateSerializer();
					s_defaultSerializer = serializer;
				}
				return serializer;
			}
		}

		private static FdbSliceSerializer<T> CreateSerializer()
		{
			Type type = typeof(T);

			if (typeof(Slice) == type)
			{
				object serializer = FdbSliceSerializer.Create<Slice>(
					(value) => value,
					(data) => data
				);
				return (FdbSliceSerializer<T>)serializer;
			}

			if (typeof(IFdbTuple) == type)
			{
				object serializer = FdbSliceSerializer.Create<IFdbTuple>(
					(value) => value == null ? Slice.Nil : value.ToSlice(), 
					(data) => FdbTuple.Unpack(data)
				);
				return (FdbSliceSerializer<T>)serializer;
			}

			if (typeof(ISliceSerializable).IsAssignableFrom(type))
			{
				if (type == typeof(ISliceSerializable))
				{
					// FromSlice() is impossible since we can't do "new ISliceSerializable()", but we can still allow calling ToSlice()
					object serializer = FdbSliceSerializer.Create<ISliceSerializable>(
						(value) => value == null ? Slice.Nil : value.ToSlice(),
						(data) => { throw new NotSupportedException(); }
					);
					return (FdbSliceSerializer<T>)serializer;
				}

				var t = typeof(FdbSliceSerializer.SerializableSerializer<>).MakeGenericType(type);
				return (FdbSliceSerializer<T>)Activator.CreateInstance(t);
			}

			return FdbSliceSerializer.Create<T>(
				(value) => FdbConverters.Convert<T, Slice>(value),
				(slice) => FdbConverters.Convert<Slice, T>(slice)
			);
		}

		#endregion

		private readonly Func<T, Slice> m_pack;

		private readonly Func<Slice, T> m_unpack;
		
		public FdbSliceSerializer(Func<T, Slice> pack, Func<Slice, T> unpack)
		{
			if (pack == null) throw new ArgumentNullException("pack");
			if (unpack == null) throw new ArgumentNullException("unpack");

			m_pack = pack;
			m_unpack = unpack;
		}

		/// <summary>Deserialize a packed representation into a <typeparamref name="T"/> instance</summary>
		/// <param name="slice">Packed representation</param>
		/// <returns>Deserialized <typeparamref name="T"/> instance.</returns>
		public T FromSlice(Slice slice)
		{
			return m_unpack(slice);
		}

		/// <summary>Serialize a <typeparamref name="T"/> instance into a packed representation</summary>
		/// <param name="value">Value to serialize</param>
		/// <returns>Packed representation of <paramref name="value"/></returns>
		public Slice ToSlice(T value)
		{
			return m_pack(value);
		}

	}

}
