#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace Doxense.Serialization
{
	using System;
	using System.Diagnostics;
	using System.Globalization;
	using System.Collections.Generic;
	using System.Diagnostics.CodeAnalysis;

	/// <summary>Structure qui wrap un object CLR de n'importe quel type</summary>
	/// <typeparam name="T">Type de l'objet wrappé</typeparam>
	/// <remarks>Utile pour passer un ref type à une classe générique qui veut des structs, ou contourner certaines limitations des generic type constraints</remarks>
	[DebuggerDisplay("[{Value}]")]
	public readonly struct Boxed<T> : IEquatable<Boxed<T>>, IComparable<Boxed<T>>, IEquatable<T>, IComparable<T>
	{
		public static Boxed<T> Default => default;

		public readonly T Value;

		public Boxed(T value)
		{
			this.Value = value;
		}

		public bool IsNull => this.Value == null;

		public T? OrDefault(T? defaultValue)
		{
			// R# pense qu'on peut écrire "this.Value ?? defaultValue" ce qui est faux (T est generic sans reftype constraint)
			// ReSharper disable once ConvertConditionalTernaryToNullCoalescing
			return this.Value != null ? this.Value : defaultValue;
		}

		/// <summary>Retourne true si le type T est compatible avec le type TBase</summary>
		public bool IsA<TBase>()
		{
			return typeof(TBase).IsAssignableFrom(typeof(T));
		}

		/// <summary>Convertit la valeur de type T en instance de type TTarget</summary>
		/// <remarks>Si T et TTarget sont parent/enfants on fait un cast. Sinon on appelle Convert.ChangeType(...)</remarks>
		public TTarget? Cast<TTarget>()
		{
			if (typeof(TTarget).IsAssignableFrom(typeof(T)))
			{
				return (TTarget?) (object?) this.Value;
			}

			return (TTarget?) Convert.ChangeType(this.Value, typeof(TTarget), CultureInfo.InvariantCulture);
		}

		public bool Equals(Boxed<T> other)
		{
			return EqualityComparer<T>.Default.Equals(this.Value, other.Value);
		}

		public bool Equals(T? other)
		{
			return EqualityComparer<T>.Default.Equals(this.Value, other);
		}

		public int CompareTo(Boxed<T> other)
		{
			return Comparer<T>.Default.Compare(this.Value, other.Value);
		}

		public int CompareTo(T? other)
		{
			return Comparer<T>.Default.Compare(this.Value, other);
		}

		public override bool Equals(object? obj)
		{
			if (obj is Boxed<T> boxed) return Equals(boxed);
			if (obj is T value) return Equals(value);
			return false;
		}

		public override int GetHashCode()
		{
			return EqualityComparer<T>.Default.GetHashCode(this.Value!);
		}

		public static bool operator==(Boxed<T> left, Boxed<T> right)
		{
			return left.Equals(right);
		}

		public static bool operator!=(Boxed<T> left, Boxed<T> right)
		{
			return !left.Equals(right);
		}

		public static bool operator ==(Boxed<T> left, T right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(Boxed<T> left, T right)
		{
			return !left.Equals(right);
		}

		public static bool operator ==(T left, Boxed<T> right)
		{
			return right.Equals(left);
		}

		public static bool operator !=(T left, Boxed<T> right)
		{
			return !right.Equals(left);
		}

		public static implicit operator T(Boxed<T> value)
		{
			return value.Value;
		}

		public static implicit operator Boxed<T>(T value)
		{
			return new Boxed<T>(value);
		}
	}

}
