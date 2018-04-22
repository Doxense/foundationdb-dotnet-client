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

namespace FoundationDB.Layers
{
	using FoundationDB.Client;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Runtime.InteropServices;
	using Doxense.Serialization.Encoders;

	/// <summary>Helper class for the <see cref="Optional{T}"/> value type</summary>
	public static class Optional
	{
		#region Wrapping...

		/// <summary>Returns an <see cref="Optional{T}"/> with the specified value</summary>
		public static Optional<T> Return<T>(T value)
		{
			return new Optional<T>(value);
		}

		/// <summary>Returns an empty <see cref="Optional{T}"/></summary>
		public static Optional<T> Empty<T>()
		{
			return default(Optional<T>);
		}

		/// <summary>Returns an array of <see cref="Optional{T}"/> from an array of values</summary>
		public static Optional<T>[] Wrap<T>(T[] values)
		{
			if (values == null) return null;
			var tmp = new Optional<T>[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				tmp[i] = new Optional<T>(values[i]);
			}
			return tmp;
		}

		/// <summary>Converts a <see cref="Nullable{T}"/> into an <see cref="Optional{T}"/></summary>
		/// <typeparam name="T">Nullable value type</typeparam>
		public static Optional<T> Wrap<T>(Nullable<T> value)
			where T : struct
		{
			if (!value.HasValue)
				return default(Optional<T>);
			return new Optional<T>(value.Value);
		}

		/// <summary>Converts an array of <see cref="Nullable{T}"/> into an array of <see cref="Optional{T}"/></summary>
		/// <typeparam name="T">Nullable value type</typeparam>
		public static Optional<T>[] Wrap<T>(Nullable<T>[] values)
			where T : struct
		{
			if (values == null) throw new ArgumentNullException("values");
			var tmp = new Optional<T>[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				if (values[i].HasValue) tmp[i] = new Optional<T>(values[i].Value);
			}
			return tmp;
		}

		/// <summary>Transforms a sequence of <see cref="Nullable{T}"/> into a sequence of <see cref="Optional{T}"/></summary>
		public static IEnumerable<Optional<T>> AsOptional<T>(IEnumerable<Nullable<T>> source)
			where T : struct
		{
			if (source == null) throw new ArgumentNullException("source");

			return source.Select(value => value.HasValue ? new Optional<T>(value.Value) : default(Optional<T>));
		}

		#endregion

		#region Single...

		/// <summary>Converts a <see cref="Optional{T}"/> into a <see cref="Nullable{T}"/></summary>
		/// <typeparam name="T">Nullable value type</typeparam>
		public static Nullable<T> ToNullable<T>(this Optional<T> value)
			where T : struct
		{
			return !value.HasValue ? default(Nullable<T>) : value.Value;
		}

		#endregion

		#region Array...

		/// <summary>Extract the values from an array of <see cref="Optional{T}"/></summary>
		/// <typeparam name="T">Nullable value type</typeparam>
		/// <param name="values">Array of optional values</param>
		/// <param name="defaultValue">Default value for empty values</param>
		/// <returns>Array of values</returns>
		public static T[] Unwrap<T>(Optional<T>[] values, T defaultValue)
		{
			if (values == null) throw new ArgumentNullException("values");
	
			var tmp = new T[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				tmp[i] = values[i].GetValueOrDefault(defaultValue);
			}
			return tmp;
		}

		/// <summary>Converts an array of <see cref="Optional{T}"/> into an array of <see cref="Nullable{T}"/></summary>
		/// <typeparam name="T">Nullable value type</typeparam>
		public static Nullable<T>[] ToNullable<T>(Optional<T>[] values)
			where T : struct
		{
			if (values == null) throw new ArgumentNullException("values");

			var tmp = new Nullable<T>[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				if (values[i].HasValue) tmp[i] = values[i].Value;
			}
			return tmp;
		}

		/// <summary>Converts an array of <see cref="Optional{T}"/> into an array of <see cref="Nullable{T}"/></summary>
		/// <typeparam name="T">Nullable value type</typeparam>
		public static T[] Unwrap<T>(Optional<T>[] values)
			where T : class
		{
			if (values == null) throw new ArgumentNullException("values");

			var tmp = new T[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				if (values[i].HasValue) tmp[i] = values[i].Value;
			}
			return tmp;
		}

		#endregion

		#region Enumerable...

		/// <summary>Transforms a sequence of <see cref="Optional{T}"/> into a sequence of values.</summary>
		/// <typeparam name="T">Type of the elements of <paramref name="source"/></typeparam>
		/// <param name="source">Sequence of optional values</param>
		/// <param name="defaultValue">Default value for empty entries</param>
		/// <returns>Sequence of values, using <paramref name="defaultValue"/> for empty entries</returns>
		public static IEnumerable<T> Unwrap<T>(this IEnumerable<Optional<T>> source, T defaultValue)
		{
			if (source == null) throw new ArgumentNullException("source");

			return source.Select(value => value.GetValueOrDefault(defaultValue));
		}

		/// <summary>Transforms a sequence of <see cref="Optional{T}"/> into a sequence of <see cref="Nullable{T}"/></summary>
		/// <typeparam name="T">Type of the elements of <paramref name="source"/></typeparam>
		/// <param name="source">Source of optional values</param>
		/// <returns>Sequence of nullable values</returns>
		public static IEnumerable<Nullable<T>> AsNullable<T>(this IEnumerable<Optional<T>> source)
			where T : struct
		{
			if (source == null) throw new ArgumentNullException("source");

			return source.Select(value => !value.HasValue ? default(Nullable<T>) : value.Value);
		}

		/// <summary>Transforms a squence of <see cref="Optional{T}"/> into a sequence of values</summary>
		/// <typeparam name="T">Type of the elements of <paramref name="source"/></typeparam>
		/// <param name="source">Source of optional values</param>
		/// <returns>Sequence of values, using the default of <typeparamref name="T"/> for empty entries</returns>
		public static IEnumerable<T> Unwrap<T>(this IEnumerable<Optional<T>> source)
			where T : class
		{
			if (source == null) throw new ArgumentNullException("source");

			return source.Select(value => value.GetValueOrDefault());
		}

		#endregion

		#region Decoding...

		/// <summary>Decode an array of slices into an array of <see cref="Optional{T}"/></summary>
		/// <typeparam name="T">Type of the decoded values</typeparam>
		/// <param name="decoder">Decoder used to produce the values</param>
		/// <param name="data">Array of slices to decode. Entries equal to <see cref="Slice.Nil"/> will not be decoded and returned as an empty optional.</param>
		/// <returns>Array of decoded <see cref="Optional{T}"/>.</returns>
		public static Optional<T>[] DecodeRange<T>(IValueEncoder<T> decoder, Slice[] data)
		{
			if (decoder == null) throw new ArgumentNullException("decoder");
			if (data == null) throw new ArgumentNullException("data");

			var values = new Optional<T>[data.Length];
			for (int i = 0; i < data.Length; i++)
			{
				Slice item;
				if ((item = data[i]).HasValue)
				{
					values[i] = new Optional<T>(decoder.DecodeValue(item));
				}
			}
			return values;
		}

		/// <summary>Decode a sequence of slices into a sequence of <see cref="Optional{T}"/></summary>
		/// <typeparam name="T">Type of the decoded values</typeparam>
		/// <param name="source">Sequence of slices to decode. Entries equal to <see cref="Slice.Nil"/> will not be decoded and returned as an empty optional.</param>
		/// <param name="decoder">Decoder used to produce the values</param>
		/// <returns>Sequence of decoded <see cref="Optional{T}"/>.</returns>
		public static IEnumerable<Optional<T>> Decode<T>(this IEnumerable<Slice> source, IValueEncoder<T> decoder)
		{
			if (decoder == null) throw new ArgumentNullException("decoder");
			if (source == null) throw new ArgumentNullException("source");

			return source.Select(value => value.HasValue ? decoder.DecodeValue(value) : default(Optional<T>));
		}

		#endregion

	}

	/// <summary>Container that is either empty (no value) or null (for reference types), or contains a value of type <typeparamref name="T"/>. </summary>
	/// <typeparam name="T">Type of the value</typeparam>
	[Serializable, StructLayout(LayoutKind.Sequential)]
	public struct Optional<T> : IEquatable<Optional<T>>, IEquatable<T>
	{
		// This is the equivalent of Nullable<T> that would accept reference types.
		// The main difference is that, 'null' is a legal value for reference types, which is distinct from "no value"
		// i.e.: new Optional<string>(null).HasValue == true

		//REVIEW: this looks very similar to Maybe<T>, except without the handling of errors. Maybe we could merge both?

		private readonly bool m_hasValue;

		private readonly T m_value;

		/// <summary>Initializes a new instance of the <see cref="Optional{T}"/> structure to the specified value.</summary>
		public Optional(T value)
		{
			m_hasValue = true;
			m_value = value;
		}

		/// <summary>Gets the value of the current <see cref="Optional{T}"/> value.</summary>
		/// <remarks>This can return null for reference types!</remarks>
		public T Value
		{
			get
			{
				if (!m_hasValue)
				{ // we construct and throw the exception in a static helper, to help with inlining
					NoValue();
				}
				return m_value;
			}
		}

		/// <summary>Gets a value indicating whether the current <see cref="Optional{T}"/> object has a value.</summary>
		public bool HasValue { get { return m_hasValue; } }

		/// <summary>Retrieves the value of the current <see cref="Optional{T}"/> object, or the object's default value.</summary>
		public T GetValueOrDefault()
		{
			return m_value;
		}

		/// <summary>Retrieves the value of the current <see cref="Optional{T}"/> object, or the specified default value.</summary>
		public T GetValueOrDefault(T defaultValue)
		{
			return m_hasValue ? m_value : defaultValue;
		}

		public override string ToString()
		{
			if (!m_hasValue || m_value == null) return String.Empty;
			return m_value.ToString();
		}

		public bool Equals(Optional<T> value)
		{
			return m_hasValue == value.m_hasValue && EqualityComparer<T>.Default.Equals(m_value, value.m_value);
		}

		public bool Equals(T value)
		{
			return m_hasValue && EqualityComparer<T>.Default.Equals(m_value, value);
		}

		public override int GetHashCode()
		{
			if (!m_hasValue || m_value == null) return 0;
			return m_value.GetHashCode();
		}

		/// <summary>Indicates whether the current <see cref="Optional{T}"/> object is equal to a specified object.</summary>
		public override bool Equals(object obj)
		{
			if (obj is T) return Equals((T)obj);
			if (obj is Optional<T>) return Equals((Optional<T>)obj);
			return m_hasValue ? object.Equals(m_value, obj) : object.ReferenceEquals(obj, null);
		}

		public static bool operator ==(Optional<T> a, Optional<T> b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(Optional<T> a, Optional<T> b)
		{
			return !a.Equals(b);
		}

		public static bool operator ==(Optional<T> a, T b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(Optional<T> a, T b)
		{
			return !a.Equals(b);
		}

		public static bool operator ==(T a, Optional<T> b)
		{
			return b.Equals(a);
		}

		public static bool operator !=(T a, Optional<T> b)
		{
			return !b.Equals(a);
		}

		public static bool operator ==(Optional<T>? a, Optional<T>? b)
		{
			// Needed to be able to write stuff like "if (optional == null)", the compiler will automatically lift "foo == null" to nullables if foo is a struct that implements the '==' operator
			return a.GetValueOrDefault().Equals(b.GetValueOrDefault());
		}

		public static bool operator !=(Optional<T>? a, Optional<T>? b)
		{
			// Needed to be able to write stuff like "if (optional != null)", the compiler will automatically lift "foo != null" to nullables if foo is a struct implements the '!=' operator
			return !a.GetValueOrDefault().Equals(b.GetValueOrDefault());
		}

		public static explicit operator T(Optional<T> value)
		{
			return value.Value;
		}

		public static implicit operator Optional<T>(T value)
		{
			return new Optional<T>(value);
		}

		private static void NoValue()
		{
			throw new InvalidOperationException("Nullable object must have a value.");
		}

	}

}
