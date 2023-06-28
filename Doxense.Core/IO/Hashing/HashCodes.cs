#region Copyright (c) 2005-2023 Doxense SAS
// See License.MD for license information
#endregion

namespace System
{
	using System;
	using System.Collections;
	using System.Runtime.CompilerServices;
	using JetBrains.Annotations;

	/// <summary>Helper methods to work with hashcodes</summary>
	[PublicAPI]
	public static class HashCodes
	{
		//REVIEW: déplacer dans le namespace "Doxense" tout court? => c'est utilisé dans des tonnes de classes Model POCO

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Compute(long value)
		{
			return unchecked((int) value) ^ (int) (value >> 32);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Compute(ulong value)
		{
			return unchecked((int)value) ^ (int)(value >> 32);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Compute(bool value)
		{
			return value ? 1 : 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Compute(Guid value)
		{
			return value.GetHashCode();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Compute(int? value)
		{
			return value ?? -1;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Compute(long? value)
		{
			return value.HasValue ? Compute(value.Value) : -1;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Compute(ulong? value)
		{
			return value.HasValue ? Compute(value.Value) : -1;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Compute(bool? value)
		{
			return value.HasValue ? Compute(value.Value) : -1;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Compute(Guid? value)
		{
			return value?.GetHashCode() ?? -1;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Compute(string value)
		{
			return value?.GetHashCode() ?? 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Compute<T>(T value)
			where T : class
		{
			return value?.GetHashCode() ?? 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Compute<T>(T? value)
			where T : struct
		{
			return value.GetValueOrDefault().GetHashCode();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Compute<T>(T value, System.Collections.IEqualityComparer comparer)
			where T : IStructuralEquatable
		{
			return value?.GetHashCode(comparer) ?? 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Combine(int h1, int h2)
		{
			return ((h1 << 5) + h1) ^ h2;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Combine(int h1, int h2, int h3)
		{
			int h = ((h1 << 5) + h1) ^ h2;
			return ((h << 5) + h) ^ h3;
		}

		[Pure]
		public static int Combine(int h1, int h2, int h3, int h4)
		{
			return Combine(Combine(h1, h2), Combine(h3, h4));
		}

		[Pure]
		public static int Combine(int h1, int h2, int h3, int h4, int h5)
		{
			return Combine(Combine(h1, h2, h3), Combine(h4, h5));
		}

		[Pure]
		public static int Combine(int h1, int h2, int h3, int h4, int h5, int h6)
		{
			return Combine(Combine(h1, h2, h3), Combine(h4, h5, h6));
		}

		/// <summary>Test that both hash codes, if present, have the same value</summary>
		/// <returns>False IIF h1 != nul && h2 != null && h1 != h2; otherisse, True</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool SameOrMissing(int? h1, int? h2)
		{
			return !h1.HasValue || !h2.HasValue || h1.Value == h2.Value;
		}

		#region Flags...

		// Combines one or more booleans into a single value (one bit per boolean)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Flags(bool a)
		{
			return (a ? 1 : 0);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Flags(bool a, bool b)
		{
			return (a ? 1 : 0) | (b ? 2 : 0);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Flags(bool a, bool b, bool c)
		{
			return (a ? 1 : 0) | (b ? 2 : 0) | (c ? 4 : 0);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Flags(bool a, bool b, bool c, bool d)
		{
			return (a ? 1 : 0) | (b ? 2 : 0) | (c ? 4 : 0) | (d ? 8 : 0);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Flags(bool a, bool b, bool c, bool d, bool e)
		{
			return (a ? 1 : 0) | (b ? 2 : 0) | (c ? 4 : 0) | (d ? 8 : 0) | (e ? 16 : 0);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Flags(bool a, bool b, bool c, bool d, bool e, bool  f)
		{
			return (a ? 1 : 0) | (b ? 2 : 0) | (c ? 4 : 0) | (d ? 8 : 0) | (e ? 16 : 0) | (f ? 32 : 0);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Flags(bool a, bool b, bool c, bool d, bool e, bool  f, bool g)
		{
			return (a ? 1 : 0) | (b ? 2 : 0) | (c ? 4 : 0) | (d ? 8 : 0) | (e ? 16 : 0) | (f ? 32 : 0) | (g ? 64 : 0);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Flags(bool a, bool b, bool c, bool d, bool e, bool  f, bool g, bool h)
		{
			return (a ? 1 : 0) | (b ? 2 : 0) | (c ? 4 : 0) | (d ? 8 : 0) | (e ? 16 : 0) | (f ? 32 : 0) | (g ? 64 : 0) | (h ? 128 : 0);
		}

		#endregion

	}

}
