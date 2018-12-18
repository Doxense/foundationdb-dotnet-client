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

#if !USE_SHARED_FRAMEWORK

namespace Doxense.Serialization.Encoders
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	public static class ValueEncoderExtensions
	{
		#region Encoding...

		/// <summary>Encode a array of <typeparamref name="TValue"/> into an array of <typeparamref name="TStorage"/></summary>
		[NotNull]
		public static TStorage[] EncodeValues<TValue, TStorage>([NotNull] this IValueEncoder<TValue, TStorage> encoder, [NotNull] params TValue[] values)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(values, nameof(values));

			var slices = new TStorage[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				slices[i] = encoder.EncodeValue(values[i]);
			}

			return slices;
		}

		/// <summary>Encode the values of a sequence of Key/Value pairs into a list of <typeparamref name="TStorage"/>, discarding the keys in the process</summary>
		[NotNull, LinqTunnel]
		public static List<TStorage> EncodeValues<TValue, TStorage>([NotNull] this IValueEncoder<TValue, TStorage> encoder, [NotNull, InstantHandle] IEnumerable<TValue> values)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(values, nameof(values));

			if (values is ICollection<TValue> coll)
			{
				var res = new List<TStorage>(coll.Count);
				foreach (var value in values)
				{
					res.Add(encoder.EncodeValue(value));
				}
				return res;
			}

			return values.Select(encoder.EncodeValue).ToList();
		}

		/// <summary>Encode a sequence of <paramref name="items"/> into a list of <typeparamref name="TStorage"/> by extracting one field using the specified <paramref name="selector"/></summary>
		[NotNull]
		public static List<TStorage> EncodeValues<TValue, TStorage, TElement>([NotNull] this IValueEncoder<TValue, TStorage> encoder, [NotNull, InstantHandle] IEnumerable<TElement> items, [NotNull, InstantHandle] Func<TElement, TValue> selector)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(items, nameof(items));
			Contract.NotNull(selector, nameof(selector));

			if (items is ICollection<TElement> coll)
			{
				var res = new List<TStorage>(coll.Count);
				foreach (var item in items)
				{
					res.Add(encoder.EncodeValue(selector(item)));
				}
				return res;
			}

			return items.Select(item => encoder.EncodeValue(selector(item))).ToList();
		}

		/// <summary>Transform a sequence of <typeparamref name="TValue"/> into a sequence of <typeparamref name="TStorage"/></summary>
		[NotNull, LinqTunnel]
		public static IEnumerable<TStorage> SelectValues<TValue, TStorage>([NotNull] this IValueEncoder<TValue, TStorage> encoder, [NotNull] IEnumerable<TValue> values)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(values, nameof(values));

			return values.Select(encoder.EncodeValue);
		}

		/// <summary>Transform the values a sequence of Key/Value pairs into a sequence of <typeparamref name="TStorage"/>, discarding the keys in the process</summary>
		[NotNull, LinqTunnel]
		public static IEnumerable<TStorage> SelectValues<TValue, TStorage, TAny>([NotNull] this IValueEncoder<TValue, TStorage> encoder, [NotNull] IEnumerable<KeyValuePair<TAny, TValue>> items)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(items, nameof(items));

			return items.Select(item => encoder.EncodeValue(item.Value));
		}

		/// <summary>Transform a sequence of <paramref name="items"/> into a sequence of <typeparamref name="TStorage"/> by extracting one field using the specified <paramref name="selector"/></summary>
		[NotNull, LinqTunnel]
		public static IEnumerable<TStorage> SelectValues<TValue, TStorage, TElement>([NotNull] this IValueEncoder<TValue, TStorage> encoder, [NotNull] IEnumerable<TElement> items, [NotNull] Func<TElement, TValue> selector)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(items, nameof(items));
			Contract.NotNull(selector, nameof(selector));

			return items.Select(item => encoder.EncodeValue(selector(item)));
		}

		#endregion

		#region Decoding...

		/// <summary>Decode an array of <typeparamref name="TStorage"/> into an arror of <typeparamref name="TValue"/></summary>
		[NotNull]
		public static TValue[] DecodeValues<TValue, TStorage>([NotNull] this IValueEncoder<TValue, TStorage> encoder, [NotNull] params TStorage[] values)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(values, nameof(values));

			var res = new TValue[values.Length];
			for (int i = 0; i < res.Length; i++)
			{
				res[i] = encoder.DecodeValue(values[i]);
			}
			return res;
		}

		/// <summary>Decode the values from a sequence of Key/Value pairs into a list of <typeparamref name="TValue"/>, discarding the keys in the process.</summary>
		[NotNull]
		public static TValue[] DecodeValues<TValue, TStorage, TAny>([NotNull] this IValueEncoder<TValue, TStorage> encoder, [NotNull] IEnumerable<KeyValuePair<TAny, TStorage>> items)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(items, nameof(items));

			switch (items)
			{
				case KeyValuePair<TAny, TStorage>[] array:
				{
					var res = new TValue[array.Length];
					for(int i = 0; i < res.Length; i++)
					{
						res[i] = encoder.DecodeValue(array[i].Value);
					}
					return res;
				}
				case ICollection<KeyValuePair<TAny, TStorage>> coll:
				{
					var res = new TValue[coll.Count];
					int i = 0;
					foreach (var item in items)
					{
						res[i++] = encoder.DecodeValue(item.Value);
					}
					if (i != res.Length) throw new InvalidOperationException();
					return res;
				}
				default:
				{
					var res = new List<TValue>();
					foreach (var item in items)
					{
						res.Add(encoder.DecodeValue(item.Value));
					}
					return res.ToArray();
				}
			}
		}

		/// <summary>Decode a sequence of <typeparamref name="TStorage"/> into a list of <typeparamref name="TValue"/></summary>
		[NotNull]
		public static TValue[] DecodeValues<TValue, TStorage>([NotNull] this IValueEncoder<TValue, TStorage> encoder, [NotNull, InstantHandle] IEnumerable<TStorage> values)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(values, nameof(values));

			switch (values)
			{
				case TStorage[] arr:
				{
					var res = new TValue[arr.Length];
					for (int i = 0; i < res.Length; i++)
					{
						res[i] = encoder.DecodeValue(arr[i]);
					}
					return res;
				}
				case ICollection<TStorage> coll:
				{
					var res = new TValue[coll.Count];
					int i = 0;
					foreach (var value in values)
					{
						res[i++] = encoder.DecodeValue(value);
					}
					if (i != res.Length) throw new InvalidOperationException();
					return res;
				}
				default:
				{
					var res = new List<TValue>();
					foreach (var value in values)
					{
						res.Add(encoder.DecodeValue(value));
					}
					return res.ToArray();
				}
			}
		}

		/// <summary>Decode a sequence of <paramref name="items"/> into a list of <typeparamref name="TValue"/> by extracting one field using the specified <paramref name="selector"/></summary>
		[NotNull]
		public static TValue[] DecodeValues<TValue, TStorage, TElement>([NotNull] this IValueEncoder<TValue, TStorage> encoder, [NotNull, InstantHandle] IEnumerable<TElement> items, [NotNull, InstantHandle] Func<TElement, TStorage> selector)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(items, nameof(items));

			switch (items)
			{
				case TElement[] arr:
				{
					var res = new TValue[arr.Length];
					for (int i = 0; i < res.Length; i++)
					{
						res[i] = encoder.DecodeValue(selector(arr[i]));
					}
					return res;
				}
				case ICollection<TElement> coll:
				{
					var res = new TValue[coll.Count];
					int i = 0;
					foreach (var item in items)
					{
						res[i++] = encoder.DecodeValue(selector(item));
					}
					if (i != res.Length) throw new InvalidOperationException();
					return res;
				}
				default:
				{
					var res = new List<TValue>();
					foreach (var item in items)
					{
						res.Add(encoder.DecodeValue(selector(item)));
					}
					return res.ToArray();
				}
			}
		}

		/// <summary>Transform a sequence of slices back into a sequence of <typeparamref name="TValue"/>s, using a serializer (or the default serializer if none is provided)</summary>
		[NotNull, LinqTunnel]
		public static IEnumerable<TValue> SelectValues<TValue, TStorage>([NotNull] this IValueEncoder<TValue, TStorage> encoder, [NotNull] IEnumerable<TStorage> values)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(values, nameof(values));

			return values.Select(encoder.DecodeValue);
		}

		/// <summary>Transform the values from a sequence of Key/Value pairs, into another sequence of <typeparamref name="TValue"/>, discarding the keys in the process.</summary>
		[NotNull, LinqTunnel]
		public static IEnumerable<TValue> SelectValues<TValue, TStorage, TAny>([NotNull] this IValueEncoder<TValue, TStorage> encoder, [NotNull] IEnumerable<KeyValuePair<TAny, TStorage>> items)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(items, nameof(items));

			return items.Select(item => encoder.DecodeValue(item.Value));
		}

		/// <summary>Transform a sequence of <paramref name="items"/> into another sequence of <typeparamref name="TValue"/> by extracting one field using the specified <paramref name="selector"/></summary>
		[NotNull, LinqTunnel]
		public static IEnumerable<TValue> SelectValues<TValue, TStorage, TElement>([NotNull] this IValueEncoder<TValue, TStorage> encoder, [NotNull] IEnumerable<TElement> items, [NotNull] Func<TElement, TStorage> selector)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(items, nameof(items));

			return items.Select(x => encoder.DecodeValue(selector(x)));
		}

		#endregion

	}

}

#endif
