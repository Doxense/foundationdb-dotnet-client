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
	using FoundationDB.Client.Utils;
	using FoundationDB.Layers.Tuples;
	using JetBrains.Annotations;
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

			public Anonymous([NotNull] Func<T, R> converter)
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
		private static Dictionary<ComparisonHelper.TypePair, IFdbConverter> Converters = new Dictionary<ComparisonHelper.TypePair, IFdbConverter>(ComparisonHelper.TypePairComparer.Default);

		/// <summary>Register all the default converters</summary>
		private static void RegisterDefaultConverters()
		{
			RegisterUnsafe<bool, Slice>((value) => Slice.FromByte(value ? (byte)1 : default(byte)));
			RegisterUnsafe<bool, byte[]>((value) => Slice.FromByte(value ? (byte)1 : default(byte)).GetBytes());
			RegisterUnsafe<bool, int>((value) => value ? 1 : default(int));
			RegisterUnsafe<bool, uint>((value) => value ? 1U : default(uint));
			RegisterUnsafe<bool, long>((value) => value ? 1L : default(long));
			RegisterUnsafe<bool, ulong>((value) => value ? 1UL : default(ulong));
			RegisterUnsafe<bool, string>((value) => value ? "true" : "false");
			RegisterUnsafe<bool, double>((value) => value ? 0.0d : 1.0d);
			RegisterUnsafe<bool, float>((value) => value ? 0.0f : 1.0f);

			RegisterUnsafe<int, Slice>((value) => Slice.FromInt32(value));
			RegisterUnsafe<int, byte[]>((value) => Slice.FromInt32(value).GetBytes());
			RegisterUnsafe<int, string>((value) => value.ToString(CultureInfo.InvariantCulture));
			RegisterUnsafe<int, uint>((value) => (uint)value);
			RegisterUnsafe<int, long>((value) => (long)value);
			RegisterUnsafe<int, ulong>((value) => (ulong)value);
			RegisterUnsafe<int, bool>((value) => value != 0);
			RegisterUnsafe<int, FdbTupleAlias>((value) => (FdbTupleAlias)value);
			RegisterUnsafe<int, double>((value) => (double)value);
			RegisterUnsafe<int, float>((value) => { checked { return (float)value; } });

			RegisterUnsafe<uint, Slice>((value) => Slice.FromUInt64(value));
			RegisterUnsafe<uint, byte[]>((value) => Slice.FromUInt64(value).GetBytes());
			RegisterUnsafe<uint, string>((value) => value.ToString(CultureInfo.InvariantCulture));
			RegisterUnsafe<uint, int>((value) => (int)value);
			RegisterUnsafe<uint, long>((value) => (long)value);
			RegisterUnsafe<uint, ulong>((value) => (ulong)value);
			RegisterUnsafe<uint, bool>((value) => value != 0);
			RegisterUnsafe<uint, double>((value) => (double)value);
			RegisterUnsafe<uint, float>((value) => { checked { return (float)value; } });

			RegisterUnsafe<long, Slice>((value) => Slice.FromInt64(value));
			RegisterUnsafe<long, byte[]>((value) => Slice.FromInt64(value).GetBytes());
			RegisterUnsafe<long, string>((value) => value.ToString(CultureInfo.InvariantCulture));
			RegisterUnsafe<long, short>((value) => { checked { return (short)value; } });
			RegisterUnsafe<long, int>((value) => { checked { return (int)value; } });
			RegisterUnsafe<long, uint>((value) => (uint)value);
			RegisterUnsafe<long, ulong>((value) => (ulong)value);
			RegisterUnsafe<long, bool>((value) => value != 0);
			RegisterUnsafe<long, TimeSpan>((value) => TimeSpan.FromTicks(value));
			RegisterUnsafe<long, double>((value) => { checked { return (double)value; } });
			RegisterUnsafe<long, float>((value) => { checked { return (float)value; } });
			RegisterUnsafe<long, Uuid64>((value) => new Uuid64(value));
			RegisterUnsafe<long, System.Net.IPAddress>((value) => new System.Net.IPAddress(value));

			RegisterUnsafe<ulong, Slice>((value) => Slice.FromUInt64(value));
			RegisterUnsafe<ulong, byte[]>((value) => Slice.FromUInt64(value).GetBytes());
			RegisterUnsafe<ulong, string>((value) => value.ToString(CultureInfo.InvariantCulture));
			RegisterUnsafe<ulong, int>((value) => { checked { return (int)value; } });
			RegisterUnsafe<ulong, uint>((value) => { checked { return (uint)value; } });
			RegisterUnsafe<ulong, long>((value) => { checked { return (long)value; } });
			RegisterUnsafe<ulong, bool>((value) => value != 0);
			RegisterUnsafe<ulong, double>((value) => { checked { return (double)value; } });
			RegisterUnsafe<ulong, float>((value) => { checked { return (float)value; } });
			RegisterUnsafe<ulong, Uuid64>((value) => new Uuid64(value));

			RegisterUnsafe<string, Slice>((value) => Slice.FromString(value));
			RegisterUnsafe<string, byte[]>((value) => Slice.FromString(value).GetBytes());
			RegisterUnsafe<string, int>((value) => string.IsNullOrEmpty(value) ? default(int) : Int32.Parse(value, CultureInfo.InvariantCulture));
			RegisterUnsafe<string, uint>((value) => string.IsNullOrEmpty(value) ? default(uint) : UInt32.Parse(value, CultureInfo.InvariantCulture));
			RegisterUnsafe<string, long>((value) => string.IsNullOrEmpty(value) ? default(long) : Int64.Parse(value, CultureInfo.InvariantCulture));
			RegisterUnsafe<string, ulong>((value) => string.IsNullOrEmpty(value) ? default(ulong) : UInt64.Parse(value, CultureInfo.InvariantCulture));
			RegisterUnsafe<string, Guid>((value) => string.IsNullOrEmpty(value) ? default(Guid) : Guid.Parse(value));
			RegisterUnsafe<string, Uuid128>((value) => string.IsNullOrEmpty(value) ? default(Uuid128) : Uuid128.Parse(value));
			RegisterUnsafe<string, Uuid64>((value) => string.IsNullOrEmpty(value) ? default(Uuid64) : Uuid64.Parse(value));
			RegisterUnsafe<string, bool>((value) => !string.IsNullOrEmpty(value));
			RegisterUnsafe<string, float>((value) => string.IsNullOrEmpty(value) ? default(float) : Single.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture));
			RegisterUnsafe<string, double>((value) => string.IsNullOrEmpty(value) ? default(double) : Double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture));
			RegisterUnsafe<string, System.Net.IPAddress>((value) => string.IsNullOrEmpty(value) ? default(System.Net.IPAddress) : System.Net.IPAddress.Parse(value));

			RegisterUnsafe<byte[], Slice>((value) => Slice.Create(value));
			RegisterUnsafe<byte[], string>((value) => value == null ? default(string) : value.Length == 0 ? String.Empty : System.Convert.ToBase64String(value));
			RegisterUnsafe<byte[], bool>((value) => value != null && value.Length > 0);
			RegisterUnsafe<byte[], int>((value) => value == null ? 0 : Slice.Create(value).ToInt32());
			RegisterUnsafe<byte[], uint>((value) => value == null ? 0U : Slice.Create(value).ToUInt32());
			RegisterUnsafe<byte[], long>((value) => value == null ? 0L : Slice.Create(value).ToInt64());
			RegisterUnsafe<byte[], ulong>((value) => value == null ? 0UL : Slice.Create(value).ToUInt64());
			RegisterUnsafe<byte[], Guid>((value) => value == null || value.Length == 0 ? default(Guid) : new Uuid128(value).ToGuid());
			RegisterUnsafe<byte[], Uuid128>((value) => value == null || value.Length == 0 ? default(Uuid128) : new Uuid128(value));
			RegisterUnsafe<byte[], Uuid64>((value) => value == null || value.Length == 0 ? default(Uuid64) : new Uuid64(value));
			RegisterUnsafe<byte[], TimeSpan>((value) => value == null ? TimeSpan.Zero : TimeSpan.FromTicks(Slice.Create(value).ToInt64()));
			RegisterUnsafe<byte[], System.Net.IPAddress>((value) => value == null || value.Length == 0 ? default(System.Net.IPAddress) : new System.Net.IPAddress(value));

			RegisterUnsafe<Guid, Slice>((value) => Slice.FromGuid(value));
			RegisterUnsafe<Guid, byte[]>((value) => Slice.FromGuid(value).GetBytes());
			RegisterUnsafe<Guid, string>((value) => value.ToString("D", null));
			RegisterUnsafe<Guid, Uuid128>((value) => new Uuid128(value));
			RegisterUnsafe<Guid, bool>((value) => value != Guid.Empty);
			RegisterUnsafe<Guid, System.Net.IPAddress>((value) => new System.Net.IPAddress(new Uuid128(value).ToByteArray()));

			RegisterUnsafe<Uuid128, Slice>((value) => value.ToSlice());
			RegisterUnsafe<Uuid128, byte[]>((value) => value.ToByteArray());
			RegisterUnsafe<Uuid128, string>((value) => value.ToString("D", null));
			RegisterUnsafe<Uuid128, Guid>((value) => value.ToGuid());
			RegisterUnsafe<Uuid128, bool>((value) => value != Uuid128.Empty);
			RegisterUnsafe<Guid, System.Net.IPAddress>((value) => new System.Net.IPAddress(value.ToByteArray()));

			RegisterUnsafe<Uuid64, Slice>((value) => value.ToSlice());
			RegisterUnsafe<Uuid64, byte[]>((value) => value.ToByteArray());
			RegisterUnsafe<Uuid64, string>((value) => value.ToString("D", null));
			RegisterUnsafe<Uuid64, long>((value) => value.ToInt64());
			RegisterUnsafe<Uuid64, ulong>((value) => value.ToUInt64());
			RegisterUnsafe<Uuid64, bool>((value) => value.ToInt64() != 0L);

			RegisterUnsafe<TimeSpan, Slice>((value) => Slice.FromInt64(value.Ticks));
			RegisterUnsafe<TimeSpan, byte[]>((value) => Slice.FromInt64(value.Ticks).GetBytes());
			RegisterUnsafe<TimeSpan, long>((value) => value.Ticks);
			RegisterUnsafe<TimeSpan, double>((value) => value.TotalSeconds);
			RegisterUnsafe<TimeSpan, bool>((value) => value == TimeSpan.Zero);

			RegisterUnsafe<System.Net.IPAddress, Slice>((value) => value != null ? Slice.Create(value.GetAddressBytes()) : Slice.Nil);
			RegisterUnsafe<System.Net.IPAddress, byte[]>((value) => value != null ? value.GetAddressBytes() : null);
			RegisterUnsafe<System.Net.IPAddress, string>((value) => value != null ? value.ToString() : null);

			RegisterUnsafe<FdbTupleAlias, int>((value) => (int)value);
			RegisterUnsafe<FdbTupleAlias, Slice>((value) => Slice.FromByte((byte)value));

			//REVIEW: this should go in the Tuples layer !
			RegisterUnsafe<Slice, byte[]>((value) => value.GetBytes());
			RegisterUnsafe<Slice, string>((value) => value.ToUnicode());
			RegisterUnsafe<Slice, bool>((value) => value.ToBool());
			RegisterUnsafe<Slice, int>((value) => value.ToInt32());
			RegisterUnsafe<Slice, uint>((value) => value.ToUInt32());
			RegisterUnsafe<Slice, long>((value) => value.ToInt64());
			RegisterUnsafe<Slice, ulong>((value) => value.ToUInt64());
			RegisterUnsafe<Slice, Guid>((value) => value.ToGuid());
			RegisterUnsafe<Slice, Uuid128>((value) => value.ToUuid128());
			RegisterUnsafe<Slice, Uuid64>((value) => value.ToUuid64());
			RegisterUnsafe<Slice, TimeSpan>((value) => TimeSpan.FromTicks(value.ToInt64()));
			RegisterUnsafe<Slice, FdbTupleAlias>((value) => (FdbTupleAlias)value.ToByte());
			RegisterUnsafe<Slice, System.Net.IPAddress>((value) => !value.IsNullOrEmpty ? new System.Net.IPAddress(value.GetBytes()) : null);
		}

		/// <summary>Helper method to throw an exception when we don't know how to convert from <paramref name="source"/> to <paramref name="destination"/></summary>
		/// <param name="source"></param>
		/// <param name="destination"></param>
		[ContractAnnotation("=> halt")]
		private static void FailCannotConvert(Type source, Type destination)
		{
			// prettyprint nullable type names to have something more usefull than "Nullable`1"
			//TODO: extend this to all generic types ?
			var nt = Nullable.GetUnderlyingType(source);
			string sourceName = nt == null ? source.Name : String.Format("Nullable<{0}>", nt.Name);
			nt = Nullable.GetUnderlyingType(destination);
			string destinationName = nt == null ? destination.Name : String.Format("Nullable<{0}>", nt.Name);

			throw new InvalidOperationException(String.Format("Cannot convert values of type {0} into {1}", sourceName, destinationName));
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
		public static IFdbConverter<T, R> Create<T, R>([NotNull] Func<T, R> converter)
		{
			if (converter == null) throw new ArgumentNullException("converter");
			return new Anonymous<T, R>(converter);
		}

		/// <summary>Add a new known converter (without locking)</summary>
		/// <typeparam name="T">Source type</typeparam>
		/// <typeparam name="R">Destination type</typeparam>
		/// <param name="converter">Lambda that converts a value of type <typeparamref name="T"/> into a value of type <typeparamref name="R"/></param>
		internal static void RegisterUnsafe<T, R>([NotNull] Func<T, R> converter)
		{
			Contract.Requires(converter != null);
			Converters[new ComparisonHelper.TypePair(typeof(T), typeof(R))] = new Anonymous<T, R>(converter);
		}

		/// <summary>Registers a new type converter</summary>
		/// <typeparam name="T">Source type</typeparam>
		/// <typeparam name="R">Destination type</typeparam>
		/// <param name="converter">Lambda that converts a value of type <typeparamref name="T"/> into a value of type <typeparamref name="R"/></param>
		public static void Register<T, R>([NotNull] Func<T, R> converter)
		{
			Contract.Requires(converter != null);
			Register<T, R>(new Anonymous<T, R>(converter));
		}

		/// <summary>Registers a new type converter</summary>
		/// <typeparam name="T">Source type</typeparam>
		/// <typeparam name="R">Destination type</typeparam>
		/// <param name="converter">Instance that can convert values of type <typeparamref name="T"/> into a values of type <typeparamref name="R"/></param>
		public static void Register<T, R>([NotNull] IFdbConverter<T, R> converter)
		{
			if (converter == null) throw new ArgumentNullException("converter");
			while (true)
			{
				var previous = Converters;
				var dic = new Dictionary<ComparisonHelper.TypePair, IFdbConverter>(previous, previous.Comparer);
				dic[new ComparisonHelper.TypePair(typeof(T), typeof(R))] = converter;
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
		[NotNull]
		public static IFdbConverter<T, R> GetConverter<T, R>()
		{
			if (typeof(T) == typeof(R))
			{ // R == T : identity function
				return (IFdbConverter<T, R>)Identity<T>.Default;
			}

			// Try to get from the known converters
			IFdbConverter converter;
			if (!Converters.TryGetValue(new ComparisonHelper.TypePair(typeof(T), typeof(R)), out converter))
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
			if (!Converters.TryGetValue(new ComparisonHelper.TypePair(type, typeof(R)), out converter))
			{
				// maybe it is a nullable type ?
				var nullableType = Nullable.GetUnderlyingType(typeof(R));
				if (nullableType != null)
				{ // we already nullchecked value above, so we just have to convert it to the underlying type...

					// shortcut for converting a R into a Nullable<R> ...
					if (type == nullableType) return (R)value;

					// maybe we have a converter for the underlying type ?
					if (Converters.TryGetValue(new ComparisonHelper.TypePair(type, nullableType), out converter))
					{ 
						return (R)converter.ConvertBoxed(value);
					}
				}

				FailCannotConvert(type, typeof(R));
			}

			return (R)converter.ConvertBoxed(value);
		}

		/// <summary>Converts all the elements of a sequence</summary>
		/// <returns>New sequence with all the converted elements</returns>
		public static IEnumerable<R> ConvertAll<T, R>(this IFdbConverter<T, R> converter, [NotNull] IEnumerable<T> items)
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
		[NotNull]
		public static List<R> ConvertAll<T, R>(this IFdbConverter<T, R> converter, [NotNull] List<T> items)
		{
			if (converter == null) throw new ArgumentNullException("converter");
			if (items == null) throw new ArgumentNullException("items");

			return items.ConvertAll<R>(converter.Convert);
		}

		/// <summary>Converts all the elements of an array</summary>
		/// <returns>New array with all the converted elements</returns>
		[NotNull]
		public static R[] ConvertAll<T, R>(this IFdbConverter<T, R> converter, [NotNull] T[] items)
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
