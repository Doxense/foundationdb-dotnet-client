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

namespace FoundationDB.Client.Converters
{
	using FoundationDB.Layers.Tuples;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq.Expressions;
	using System.Threading;

	/// <summary>Helper class to convert object from one type to another</summary>
	public static class FdbConverters
	{
		#region Identity<T>

		/// <summary>Simple converter where the source and destination types are the same</summary>
		/// <typeparam name="T">Source and Destination type</typeparam>
		private class Identity<T> : IFdbConverter<T, T>
		{
			private static readonly bool IsReferenceType = typeof(T).IsClass; //TODO: nullables ?

			public static readonly IFdbConverter<T, T> Default = new Identity<T>();

			public static readonly Func<object, T> FromObject = (Func<object, T>)FdbConverters.CreateCaster(typeof(T));

			public Type Source { get { return typeof(T); } }

			public Type Destination { get { return typeof(T); } }

			public T Convert(T value)
			{
				return value;
			}

			public object ConvertBoxed(object value)
			{
				return FromObject(value);
			}

			public static T Cast(object value)
			{
				if (value == null && !IsReferenceType) return default(T);
				return (T)value;
			}
		}

		#endregion

		#region Anonymous<T>

		/// <summary>Simple converter that wraps a lambda function</summary>
		/// <typeparam name="T">Source type</typeparam>
		/// <typeparam name="R">Destination type</typeparam>
		private class Anonymous<T, R> : IFdbConverter<T, R>
		{
			private Func<T, R> Converter { get; set; }

			public Anonymous(Func<T, R> converter)
			{
				if (converter == null) throw new ArgumentNullException("converter");
				this.Converter = converter;
			}

			public Type Source { get { return typeof(T); } }

			public Type Destination { get { return typeof(R); } }

			public R Convert(T value)
			{
				return this.Converter(value);
			}

			public object ConvertBoxed(object value)
			{
				return (object) this.Converter(Identity<T>.FromObject(value));
			}
		}

		#endregion

		/// <summary>Static ctor that initialize the default converters</summary>
		static FdbConverters()
		{
			RegisterDefaultConverters();
		}

		/// <summary>Map of all known converters from T to R</summary>
		/// <remarks>No locking required, because all changes will replace this instance with a new Dictionary</remarks>
		private static Dictionary<KeyValuePair<Type, Type>, IFdbConverter> Converters = new Dictionary<KeyValuePair<Type, Type>, IFdbConverter>();

		/// <summary>Register all the default converters</summary>
		private static void RegisterDefaultConverters()
		{
			RegisterUnsafe<bool, Slice>((value) => Slice.FromInt32(value ? 1 : 0));
			RegisterUnsafe<bool, byte[]>((value) => Slice.FromInt32(value ? 1 : 0).GetBytes());
			RegisterUnsafe<bool, int>((value) => value ? 1 : default(int));
			RegisterUnsafe<bool, uint>((value) => value ? 1U : default(uint));
			RegisterUnsafe<bool, long>((value) => value ? 1L : default(long));
			RegisterUnsafe<bool, ulong>((value) => value ? 1UL : default(ulong));
			RegisterUnsafe<bool, string>((value) => value ? "true" : "false");

			RegisterUnsafe<int, Slice>((value) => Slice.FromInt32(value));
			RegisterUnsafe<int, byte[]>((value) => Slice.FromInt32(value).GetBytes());
			RegisterUnsafe<int, string>((value) => value.ToString(CultureInfo.InvariantCulture));
			RegisterUnsafe<int, uint>((value) => (uint)value);
			RegisterUnsafe<int, long>((value) => (long)value);
			RegisterUnsafe<int, ulong>((value) => (ulong)value);
			RegisterUnsafe<int, bool>((value) => value != 0);

			RegisterUnsafe<long, Slice>((value) => Slice.FromInt64(value));
			RegisterUnsafe<long, byte[]>((value) => Slice.FromInt64(value).GetBytes());
			RegisterUnsafe<long, string>((value) => value.ToString(CultureInfo.InvariantCulture));
			RegisterUnsafe<long, int>((value) => { checked { return (int)value; } });
			RegisterUnsafe<long, uint>((value) => { return (uint)value; });
			RegisterUnsafe<long, ulong>((value) => { return (ulong)value; });
			RegisterUnsafe<long, bool>((value) => value != 0);
			RegisterUnsafe<long, TimeSpan>((value) => TimeSpan.FromTicks(value));

			RegisterUnsafe<ulong, Slice>((value) => Slice.FromUInt64(value));
			RegisterUnsafe<ulong, byte[]>((value) => Slice.FromUInt64(value).GetBytes());
			RegisterUnsafe<ulong, string>((value) => value.ToString(CultureInfo.InvariantCulture));
			RegisterUnsafe<ulong, int>((value) => { checked { return (int)value; } });
			RegisterUnsafe<ulong, uint>((value) => { checked { return (uint)value; } });
			RegisterUnsafe<ulong, long>((value) => { checked { return (long)value; } });
			RegisterUnsafe<ulong, bool>((value) => value != 0);

			RegisterUnsafe<string, Slice>((value) => Slice.FromString(value));
			RegisterUnsafe<string, byte[]>((value) => Slice.FromString(value).GetBytes());
			RegisterUnsafe<string, int>((value) => string.IsNullOrEmpty(value) ? default(int) : Int32.Parse(value, CultureInfo.InvariantCulture));
			RegisterUnsafe<string, uint>((value) => string.IsNullOrEmpty(value) ? default(uint) : UInt32.Parse(value, CultureInfo.InvariantCulture));
			RegisterUnsafe<string, long>((value) => string.IsNullOrEmpty(value) ? default(long) : Int64.Parse(value, CultureInfo.InvariantCulture));
			RegisterUnsafe<string, ulong>((value) => string.IsNullOrEmpty(value) ? default(ulong) : UInt64.Parse(value, CultureInfo.InvariantCulture));
			RegisterUnsafe<string, Guid>((value) => string.IsNullOrEmpty(value) ? default(Guid) : Guid.Parse(value));
			RegisterUnsafe<string, bool>((value) => string.IsNullOrEmpty(value) ? default(bool) : Boolean.Parse(value));

			RegisterUnsafe<byte[], Slice>((value) => Slice.Create(value));
			RegisterUnsafe<byte[], string>((value) => value == null ? default(string) : value.Length == 0 ? String.Empty : System.Convert.ToBase64String(value));
			RegisterUnsafe<byte[], int>((value) => value == null ? 0 : Slice.Create(value).ToInt32());
			RegisterUnsafe<byte[], uint>((value) => value == null ? 0U : Slice.Create(value).ToUInt32());
			RegisterUnsafe<byte[], long>((value) => value == null ? 0L : Slice.Create(value).ToInt64());
			RegisterUnsafe<byte[], ulong>((value) => value == null ? 0UL : Slice.Create(value).ToUInt64());
			RegisterUnsafe<byte[], Guid>((value) => value == null || value.Length == 0 ? Guid.Empty : new Guid(value));
			RegisterUnsafe<byte[], TimeSpan>((value) => value == null ? TimeSpan.Zero : TimeSpan.FromTicks(Slice.Create(value).ToInt64()));

			RegisterUnsafe<Guid, Slice>((value) => Slice.FromGuid(value));
			RegisterUnsafe<Guid, byte[]>((value) => value.ToByteArray());
			RegisterUnsafe<Guid, string>((value) => value.ToString(null));

			RegisterUnsafe<TimeSpan, Slice>((value) => Slice.FromInt64(value.Ticks));
			RegisterUnsafe<TimeSpan, byte[]>((value) => Slice.FromInt64(value.Ticks).GetBytes());
			RegisterUnsafe<TimeSpan, long>((value) => value.Ticks);
			RegisterUnsafe<TimeSpan, double>((value) => value.TotalSeconds);

			//REVIEW: this should go in the Tuples layer !
			RegisterUnsafe<Slice, byte[]>((value) => value.GetBytes());
			RegisterUnsafe<Slice, string>((value) => value.ToAscii());
			RegisterUnsafe<Slice, int>((value) => value.ToInt32());
			RegisterUnsafe<Slice, uint>((value) => value.ToUInt32());
			RegisterUnsafe<Slice, long>((value) => value.ToInt64());
			RegisterUnsafe<Slice, ulong>((value) => value.ToUInt64());
			RegisterUnsafe<Slice, Guid>((value) => value.ToGuid());
			RegisterUnsafe<Slice, TimeSpan>((value) => TimeSpan.FromTicks(value.ToInt64()));
		}

		/// <summary>Helper method to throw an exception when we don't know how to convert from <paramref name="source"/> to <paramref name="destination"/></summary>
		/// <param name="source"></param>
		/// <param name="destination"></param>
		private static void FailCannotConvert(Type source, Type destination)
		{
			throw new InvalidOperationException(String.Format("Cannot convert values of type {0} into {1}", source.Name, destination.Name));
		}

		/// <summary>Create a new delegate that cast a boxed valued of type T (object) into a T</summary>
		/// <returns>Delegate that is of type Func&lt;object, <param name="type"/>&gt;</returns>
		private static Delegate CreateCaster(Type type)
		{
			var prm = Expression.Parameter(typeof(object), "value");
			//TODO: valuetype vs ref type ?
			var body = Expression.Convert(prm, type);
			var lambda = Expression.Lambda(body, true, prm);
			return lambda.Compile();
		}

		/// <summary>Helper method that wraps a lambda function into a converter</summary>
		/// <typeparam name="T">Source type</typeparam>
		/// <typeparam name="R">Destination type</typeparam>
		/// <param name="converter">Lambda that converts a value of type <typeparamref name="T"/> into a value of type <typeparamref name="R"/></param>
		/// <returns>Converters that wraps the lambda</returns>
		public static IFdbConverter<T, R> Create<T, R>(Func<T, R> converter)
		{
			if (converter == null) throw new ArgumentNullException("converter");
			return new Anonymous<T, R>(converter);
		}

		/// <summary>Add a new known converter (without locking)</summary>
		/// <typeparam name="T">Source type</typeparam>
		/// <typeparam name="R">Destination type</typeparam>
		/// <param name="converter">Lambda that converts a value of type <typeparamref name="T"/> into a value of type <typeparamref name="R"/></param>
		internal static void RegisterUnsafe<T, R>(Func<T, R> converter)
		{
			Converters[new KeyValuePair<Type, Type>(typeof(T), typeof(R))] = new Anonymous<T, R>(converter);
		}

		/// <summary>Registers a new converter</summary>
		/// <typeparam name="T">Source type</typeparam>
		/// <typeparam name="R">Destination type</typeparam>
		/// <param name="converter">Lambda that converts a value of type <typeparamref name="T"/> into a value of type <typeparamref name="R"/></param>
		public static void Register<T, R>(IFdbConverter<T, R> converter)
		{
			while (true)
			{
				var previous = Converters;
				var dic = new Dictionary<KeyValuePair<Type, Type>, IFdbConverter>(previous);
				dic[new KeyValuePair<Type, Type>(typeof(T), typeof(R))] = converter;
				if (Interlocked.CompareExchange(ref Converters, dic, previous) == previous)
				{
					break;
				}
			}
		}

		/// <summary>Returns a converter that converts <typeparamref name="T"/>s into <typeparamref name="R"/>s</summary>
		/// <typeparam name="T">Source type</typeparam>
		/// <typeparam name="R">Destination type</typeparam>
		/// <returns>Valid convertir for this types, or an exception if there are no known convertions</returns>
		/// <exception cref="System.InvalidOperationException">No valid converter for these types was found</exception>
		public static IFdbConverter<T, R> GetConverter<T, R>()
		{
			if (typeof(T) == typeof(R))
			{ // R == T : identity function
				return (IFdbConverter<T, R>)Identity<T>.Default;
			}

			// Try to get from the known converters
			IFdbConverter converter;
			if (!Converters.TryGetValue(new KeyValuePair<Type, Type>(typeof(T), typeof(R)), out converter))
			{
				//TODO: ..?
				FailCannotConvert(typeof(T), typeof(R));
			}

			return (IFdbConverter<T, R>)converter;
		}

		/// <summary>Convert a value of type <typeparamref name="T"/> into type <typeparamref name="R"/></summary>
		/// <typeparam name="T">Source type</typeparam>
		/// <typeparam name="R">Destination type</typeparam>
		/// <param name="value">Value to convert</param>
		/// <returns>Converted value</returns>
		public static R Convert<T, R>(T value)
		{
			//note: most of the types, T will be equal to R. We should get an optimized converter that will not box the values
			return GetConverter<T, R>().Convert(value);
		}

		/// <summary>Cast a boxed value (known to be of type <typeparamref name="T"/>) into an unboxed value</summary>
		/// <typeparam name="T">Runtime type of the value</typeparam>
		/// <param name="value">Value that is known to be of type <typeparamref name="T"/>, but is boxed into an object</param>
		/// <returns>Original value casted into its runtime type</returns>
		public static T Unbox<T>(object value)
		{
			return Identity<T>.FromObject(value);
		}

		/// <summary>Convert a boxed value into type <typeparamref name="R"/></summary>
		/// <typeparam name="R">Destination type</typeparam>
		/// <param name="value">Boxed value</param>
		/// <returns>Converted value, or an exception if there are no known convertions. The value null is converted into default(<typeparamref name="R"/>) by convention</returns>
		/// <exception cref="System.InvalidOperationException">No valid converter for these types was found</exception>
		public static R ConvertBoxed<R>(object value)
		{
			if (value == null) return default(R);
			var type = value.GetType();

			// cast !
			if (type == typeof(R)) return (R)value;

			IFdbConverter converter;
			if (!Converters.TryGetValue(new KeyValuePair<Type, Type>(type, typeof(R)), out converter))
			{
				FailCannotConvert(type, typeof(R));
			}

			return (R)converter.ConvertBoxed(value);
		}

		/// <summary>Converts all the elements of a sequence</summary>
		/// <returns>New sequence with all the converted elements</returns>
		public static IEnumerable<R> ConvertAll<T, R>(this IFdbConverter<T, R> converter, IEnumerable<T> items)
		{
			if (converter == null) throw new ArgumentNullException("converter");
			if (items == null) throw new ArgumentNullException("items");

			foreach (var item in items)
			{
				yield return converter.Convert(item);
			}
		}

		/// <summary>Converts all the elements of a list</summary>
		/// <returns>New list with all the converted elements</returns>
		public static List<R> ConvertAll<T, R>(this IFdbConverter<T, R> converter, List<T> items)
		{
			if (converter == null) throw new ArgumentNullException("converter");
			if (items == null) throw new ArgumentNullException("items");

			return items.ConvertAll<R>(new Converter<T, R>(converter.Convert));
		}

		/// <summary>Converts all the elements of an array</summary>
		/// <returns>New array with all the converted elements</returns>
		public static R[] ConvertAll<T, R>(this IFdbConverter<T, R> converter, T[] items)
		{
			if (converter == null) throw new ArgumentNullException("converter");
			if (items == null) throw new ArgumentNullException("items");

			var results = new R[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				results[i] = converter.Convert(items[i]);
			}
			return results;
		}

	}

}
