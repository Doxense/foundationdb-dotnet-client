#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
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

namespace FoundationDB.Client
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using Doxense.Collections.Tuples;

	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct FdbTenantName : IEquatable<FdbTenantName>, IComparable<FdbTenantName>
	{

		internal readonly Slice Value;

		public readonly string? Label;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTenantName(Slice value, string? label = null)
		{
			this.Value = value;
			this.Label = label;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTenantName Create(Slice name, string? label = null) => new(name, label);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTenantName Create(ReadOnlySpan<byte> name, string? label = null) => new(Slice.Copy(name), label);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTenantName Create(string name, string? label = null) => new(Slice.FromStringUtf8(name), label ?? name);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTenantName Create<TTuple>(TTuple items, string? label = null) where TTuple : IVarTuple => new(TuPack.Pack<TTuple>(items), label ?? items.ToString());

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTenantName Create<T1>(ValueTuple<T1> items, string? label = null) => new(TuPack.Pack<T1>(items), label ?? items.ToString());

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTenantName Create<T1, T2>(ValueTuple<T1, T2> items, string? label = null) => new(TuPack.Pack<T1, T2>(items), label ?? items.ToString());

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTenantName Create<T1, T2, T3>(ValueTuple<T1, T2, T3> items, string? label = null) => new(TuPack.Pack<T1, T2, T3>(items), label ?? items.ToString());

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTenantName Create<T1, T2, T3, T4>(ValueTuple<T1, T2, T3, T4> items, string? label = null) => new(TuPack.Pack<T1, T2, T3, T4>(items), label ?? items.ToString());

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal FdbTenantName Copy() => new(this.Value.Memoize(), this.Label);

		public override string ToString() => this.Label ?? this.Value.PrettyPrint();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTenantName other) => this.Value.Equals(other.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override bool Equals(object? obj) => obj is FdbTenantName other && Equals(other);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => this.Value.GetHashCode();

		public int CompareTo(FdbTenantName other) => Value.CompareTo(other.Value);

		public sealed class Comparer : IEqualityComparer<FdbTenantName>, IComparer<FdbTenantName>
		{

			public static readonly Comparer Default = new Comparer();

			private Comparer() { }

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool Equals(FdbTenantName x, FdbTenantName y) => x.Value.Equals(y.Value);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public int GetHashCode(FdbTenantName obj) => obj.Value.GetHashCode();

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public int Compare(FdbTenantName x, FdbTenantName y) => x.Value.CompareTo(y.Value);

		}

	}

}
