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

namespace FoundationDB.Layers
{
	using FoundationDB.Client;
	using System;
	using System.Collections.Generic;
	using System.Runtime.InteropServices;

	public static class Optional
	{
		public static Optional<T> Return<T>(T value)
		{
			return new Optional<T>(value);
		}

		public static Optional<T> Empty<T>()
		{
			return default(Optional<T>);
		}

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

		public static T[] Unwrap<T>(Optional<T>[] values, T defaultValue)
		{
			if (values == null) return null;
			var tmp = new T[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				tmp[i] = values[i].GetValueOrDefault(defaultValue);
			}
			return tmp;
		}

		public static Optional<T>[] DecodeRange<T>(IValueEncoder<T> encoder, Slice[] values)
		{
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (values == null) throw new ArgumentNullException("values");

			var tmp = new Optional<T>[values.Length];
			Slice item;
			for (int i = 0; i < values.Length; i++)
			{
				if ((item = values[i]).HasValue)
				{
					tmp[i] = new Optional<T>(encoder.DecodeValue(item));
				}
			}
			return tmp;
		}

	}

	/// <summary>Container that is either empty (no value) or null (for reference types), or contains a value of type <typeparamref name="T"/>. </summary>
	/// <typeparam name="T">Type of the value</typeparam>
	[Serializable, StructLayout(LayoutKind.Sequential)]
	public struct Optional<T> : IEquatable<Optional<T>>, IEquatable<T>
	{
		// This is the equivalent of Nullable<T> that would accept reference types.
		// The main difference is that, 'null' is a legal value for reference types, which is distinct from "no value"
		// i.e.: new Optional<string>(null).HasValue == true

		public readonly bool m_hasValue;

		public readonly T m_value;

		/// <summary>Initializes a new instance of the <see cref="Optional&lt;&gt;"/> structure to the specified value.</summary>
		public Optional(T value)
		{
			m_hasValue = true;
			m_value = value;
		}

		/// <summary>Gets the value of the current <see cref="Value&lt;&gt;"/> value.</summary>
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

		/// <summary>Gets a value indicating whether the current <see cref="Optional&lt;&gt;"/> object has a value.</summary>
		public bool HasValue { get { return m_hasValue; } }

		/// <summary>Retrieves the value of the current <see cref="Value&lt;&gt;"/> object, or the object's default value.</summary>
		public T GetValueOrDefault()
		{
			return m_value;
		}

		/// <summary>Retrieves the value of the current <see cref="Value&lt;&gt;"/> object, or the specified default value.</summary>
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
			return m_hasValue == value.m_hasValue && object.Equals(m_value, value.m_value);
		}

		public bool Equals(T value)
		{
			return m_hasValue && object.Equals(m_value, value);
		}

		public override int GetHashCode()
		{
			if (!m_hasValue || m_value == null) return 0;
			return m_value.GetHashCode();
		}

		/// <summary>Indicates whether the current <see cref="Value&lt;&gt;"/> object is equal to a specified object.</summary>
		public override bool Equals(object obj)
		{
			if (!m_hasValue) return obj == null;
			return object.Equals(m_value, obj);
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
