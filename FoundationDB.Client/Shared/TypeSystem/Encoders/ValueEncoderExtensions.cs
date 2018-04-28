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

using System;

namespace Doxense.Serialization.Encoders
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	public static class ValueEncoderExtensions
	{

		/// <summary>Convert an array of <typeparamref name="T"/>s into an array of slices, using a serializer (or the default serializer if none is provided)</summary>
		[NotNull]
		public static Slice[] EncodeValues<T>([NotNull] this IValueEncoder<T> encoder, [NotNull] params T[] values)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(values, nameof(values));

			var slices = new Slice[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				slices[i] = encoder.EncodeValue(values[i]);
			}

			return slices;
		}

		/// <summary>Transform a sequence of <typeparamref name="T"/>s into a sequence of slices, using a serializer (or the default serializer if none is provided)</summary>
		[NotNull]
		public static IEnumerable<Slice> EncodeValues<T>([NotNull] this IValueEncoder<T> encoder, [NotNull] IEnumerable<T> values)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(values, nameof(values));

			// note: T=>Slice usually is used for writing batches as fast as possible, which means that keys will be consumed immediately and don't need to be streamed

			var array = values as T[];
			if (array != null)
			{ // optimized path for arrays
				return EncodeValues<T>(encoder, array);
			}

			var coll = values as ICollection<T>;
			if (coll != null)
			{ // optimized path when we know the count
				var slices = new List<Slice>(coll.Count);
				foreach (var value in coll)
				{
					slices.Add(encoder.EncodeValue(value));
				}
				return slices;
			}

			return values.Select(value => encoder.EncodeValue(value));
		}

		/// <summary>Convert an array of slices back into an array of <typeparamref name="T"/>s, using a serializer (or the default serializer if none is provided)</summary>
		[NotNull]
		public static T[] DecodeValues<T>([NotNull] this IValueEncoder<T> encoder, [NotNull] params Slice[] slices)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(slices, nameof(slices));

			var values = new T[slices.Length];
			for (int i = 0; i < slices.Length; i++)
			{
				values[i] = encoder.DecodeValue(slices[i]);
			}

			return values;
		}

		/// <summary>Convert the values of an array of key value pairs of slices back into an array of <typeparamref name="T"/>s, using a serializer (or the default serializer if none is provided)</summary>
		[NotNull]
		public static T[] DecodeValues<T>([NotNull] this IValueEncoder<T> encoder, [NotNull] KeyValuePair<Slice, Slice>[] items)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(items, nameof(items));

			var values = new T[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				values[i] = encoder.DecodeValue(items[i].Value);
			}

			return values;
		}

		/// <summary>Transform a sequence of slices back into a sequence of <typeparamref name="T"/>s, using a serializer (or the default serializer if none is provided)</summary>
		[NotNull]
		public static IEnumerable<T> DecodeValues<T>([NotNull] this IValueEncoder<T> encoder, [NotNull] IEnumerable<Slice> slices)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(slices, nameof(slices));

			// Slice=>T may be filtered in LINQ queries, so we should probably stream the values (so no optimization needed)

			return slices.Select(slice => encoder.DecodeValue(slice));
		}

	}

}
