#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
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
				return (TTarget) (object) this.Value;

			return (TTarget) Convert.ChangeType(this.Value, typeof(TTarget), CultureInfo.InvariantCulture);
		}

		public bool Equals(Boxed<T> other)
		{
			return EqualityComparer<T>.Default.Equals(this.Value, other.Value);
		}

		public bool Equals(T other)
		{
			return EqualityComparer<T>.Default.Equals(this.Value, other);
		}

		public int CompareTo(Boxed<T> other)
		{
			return Comparer<T>.Default.Compare(this.Value, other.Value);
		}

		public int CompareTo(T other)
		{
			return Comparer<T>.Default.Compare(this.Value, other);
		}

		public override bool Equals(object obj)
		{
			if (obj is Boxed<T> boxed) return Equals(boxed);
			if (obj is T value) return Equals(value);
			return false;
		}

		public override int GetHashCode()
		{
			return EqualityComparer<T>.Default.GetHashCode(this.Value);
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
