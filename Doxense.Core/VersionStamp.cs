#region Copyright (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of Doxense nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL DOXENSE BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

#if !USE_SHARED_FRAMEWORK

namespace System
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using JetBrains.Annotations;

	/// <summary>VersionStamp</summary>
	/// <remarks>A VersionStamp is unique, monotonically (but not sequentially) increasing value for each committed transaction.
	/// Its size can either be 10 bytes (80-bits) or 12-bytes (96-bits).
	/// The first 8 bytes are the committed version of the database. The next 2 bytes are monotonic in the serialization order for transactions.
	/// The optional last 2 bytes can contain a user-provider version number used to allow multiple stamps inside the same transaction.
	/// </remarks>
	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct VersionStamp : IEquatable<VersionStamp>, IComparable<VersionStamp>
	{
		//note: they are called "Versionstamp" in the doc, but "VersionStamp" seems more .NETy (like 'TimeSpan').

		private const ulong PLACEHOLDER_VERSION = ulong.MaxValue;
		private const ushort PLACEHOLDER_ORDER = ushort.MaxValue;
		private const ushort NO_USER_VERSION = 0;
		private const ulong HSB_VERSION = 0x8000000000000000UL;

		private const ushort FLAGS_NONE = 0x0;
		private const ushort FLAGS_HAS_VERSION = 0x1; // unset: 80-bits, set: 96-bits
		private const ushort FLAGS_IS_INCOMPLETE = 0x2; // unset: complete, set: incomplete

		/// <summary>Serialized bytes of the default incomplete stamp (composed of only 0xFF)</summary>
		public static readonly Slice IncompleteToken = Slice.Repeat(0xFF, 10);
		//BUGBUG: fdb client only needs 'internal' but with shared framework it must be 'public'... which can be dangerous if the buffer is exposed to anyone!

		/// <summary>Commit version of the transaction</summary>
		/// <remarks>This value is determined by the database at commit time.</remarks>
		
		public readonly ulong TransactionVersion; // Bytes 0..7

		/// <summary>Transaction Batch Order</summary>
		/// <remarks>This value is determined by the database at commit time.</remarks>
		public readonly ushort TransactionOrder; // Bytes 8..9

		/// <summary>User-provided version (between 0 and 65535)</summary>
		/// <remarks>For 80-bits VersionStamps, this value will be 0 and will not be part of the serialized key. You can use <see cref="HasUserVersion"/> to distinguish between both types of stamps.</remarks>
		public readonly ushort UserVersion; // Bytes 10..11 (if 'FLAGS_HAS_VERSION' is set)

		/// <summary>Internal flags (FLAGS_xxx constants)</summary>
		private readonly ushort Flags;
		//note: this flag is only present in memory, and is not serialized

		private VersionStamp(ulong version, ushort order, ushort user, ushort flags)
		{
			this.TransactionVersion = version;
			this.TransactionOrder = order;
			this.UserVersion = user;
			this.Flags = flags;
		}

		/// <summary>Convert a 80-bits UUID into a 80-bits VersionStamp</summary>
		public static VersionStamp FromUuid80(Uuid80 value)
		{
			unsafe
			{
				Span<byte> buf = stackalloc byte[Uuid80.SizeOf]; // 10 required
				value.WriteToUnsafe(buf);
				ReadUnsafe(buf, out var vs);
				return vs;
			}
		}

		/// <summary>Convert a 96-bits UUID into a 96-bits VersionStamp</summary>
		public static VersionStamp FromUuid96(Uuid96 value)
		{
			unsafe
			{
				Span<byte> buf = stackalloc byte[Uuid96.SizeOf]; // 12 required
				value.WriteToUnsafe(buf);
				ReadUnsafe(buf, out var vs);
				return vs;
			}
		}

		/// <summary>Creates an incomplete 80-bit <see cref="VersionStamp"/> with no user version.</summary>
		/// <returns>Placeholder that will be serialized as <code>FF FF FF FF FF FF FF FF FF FF</code> (10 bytes).</returns>
		/// <remarks>
		/// This stamp contains a temporary marker that will be later filled by the database with the actual VersionStamp by the database at transaction commit time.
		/// If you need to create multiple distinct stamps within the same transaction, please use <see cref="Incomplete(int)"/> instead.
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static VersionStamp Incomplete()
		{
			return new VersionStamp(PLACEHOLDER_VERSION, PLACEHOLDER_ORDER, NO_USER_VERSION, FLAGS_IS_INCOMPLETE);
		}

		/// <summary>Creates an incomplete 96-bit <see cref="VersionStamp"/> with the given user version.</summary>
		/// <param name="userVersion">Value between 0 and 65535 that will be appended at the end of the VersionStamp, making it unique <i>within</i> the transaction.</param>
		/// <returns>Placeholder that will be serialized as <code>FF FF FF FF FF FF FF FF FF FF vv vv</code> (12 bytes) where 'vv vv' is the user version encoded in little-endian.</returns>
		/// <exception cref="ArgumentException">If <paramref name="userVersion"/> is less than <c>0</c>, or greater than <c>65534</c> (0xFFFE).</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static VersionStamp Incomplete(int userVersion)
		{
			Contract.Between(userVersion, 0, 0xFFFF, message: "Local version must fit in 16-bits.");
			return new VersionStamp(PLACEHOLDER_VERSION, PLACEHOLDER_ORDER, (ushort) userVersion, FLAGS_IS_INCOMPLETE | FLAGS_HAS_VERSION);
		}

		/// <summary>Creates an incomplete 96-bit <see cref="VersionStamp"/> with the given user version.</summary>
		/// <param name="userVersion">Value between 0 and 65535 that will be appended at the end of the VersionStamp, making it unique <i>within</i> the transaction.</param>
		/// <returns>Placeholder that will be serialized as <code>FF FF FF FF FF FF FF FF FF FF vv vv</code> (12 bytes) where 'vv vv' is the user version encoded in little-endian.</returns>
		/// <exception cref="ArgumentException">If <paramref name="userVersion"/> is less than <c>0</c>, or greater than <c>65534</c> (0xFFFE).</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static VersionStamp Incomplete(ushort userVersion)
		{
			return new VersionStamp(PLACEHOLDER_VERSION, PLACEHOLDER_ORDER, userVersion, FLAGS_IS_INCOMPLETE | FLAGS_HAS_VERSION);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static VersionStamp Custom(ulong version, ushort order, bool incomplete)
		{
			return new VersionStamp(version, order, NO_USER_VERSION, incomplete ? FLAGS_IS_INCOMPLETE : FLAGS_NONE);
		}

		/// <summary>Creates a 96-bit <see cref="VersionStamp"/>.</summary>
		/// <returns>Complete stamp, with a user version.</returns>
		public static VersionStamp Custom(Uuid80 uuid, bool incomplete)
		{
			unsafe
			{
				byte* ptr = stackalloc byte[Uuid80.SizeOf];
				uuid.WriteToUnsafe(new Span<byte>(ptr, Uuid80.SizeOf));
				ulong version = UnsafeHelpers.LoadUInt64BE(ptr);
				ushort order = UnsafeHelpers.LoadUInt16BE(ptr + 8);
				return new VersionStamp(version, order, NO_USER_VERSION, incomplete ? FLAGS_IS_INCOMPLETE : FLAGS_NONE);
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static VersionStamp Custom(ulong version, ushort order, int userVersion, bool incomplete)
		{
			Contract.Between(userVersion, 0, 0xFFFF, message: "Local version must fit in 16-bits.");
			return new VersionStamp(version, order, (ushort) userVersion, incomplete ? (ushort) (FLAGS_IS_INCOMPLETE | FLAGS_HAS_VERSION) : FLAGS_HAS_VERSION);
		}

		/// <summary>Creates a 96-bit <see cref="VersionStamp"/>.</summary>
		/// <returns>Complete stamp, with a user version.</returns>
		public static VersionStamp Custom(Uuid80 uuid, ushort userVersion, bool incomplete)
		{
			unsafe
			{
				byte* ptr = stackalloc byte[Uuid80.SizeOf];
				uuid.WriteToUnsafe(new Span<byte>(ptr, Uuid80.SizeOf));
				ulong version = UnsafeHelpers.LoadUInt64BE(ptr);
				ushort order = UnsafeHelpers.LoadUInt16BE(ptr + 8);
				return new VersionStamp(version, order, userVersion, incomplete ? (ushort) (FLAGS_IS_INCOMPLETE | FLAGS_HAS_VERSION) : FLAGS_HAS_VERSION);
			}
		}

		/// <summary>Creates a 96-bit <see cref="VersionStamp"/>.</summary>
		/// <returns>Complete stamp, with a user version.</returns>
		public static VersionStamp Custom(Uuid96 uuid, bool incomplete)
		{
			unsafe
			{
				byte* ptr = stackalloc byte[Uuid96.SizeOf];
				uuid.WriteToUnsafe(new Span<byte>(ptr, Uuid96.SizeOf));
				ulong version = UnsafeHelpers.LoadUInt64BE(ptr);
				ushort order = UnsafeHelpers.LoadUInt16BE(ptr + 8);
				ushort userVersion = UnsafeHelpers.LoadUInt16BE(ptr + 10);
				return new VersionStamp(version, order, userVersion, incomplete ? (ushort) (FLAGS_IS_INCOMPLETE | FLAGS_HAS_VERSION) : FLAGS_HAS_VERSION);
			}
		}

		/// <summary>Creates a 80-bit <see cref="VersionStamp"/>, obtained from the database.</summary>
		/// <returns>Complete stamp, without user version.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static VersionStamp Complete(ulong version, ushort order)
		{
			return new VersionStamp(version, order, NO_USER_VERSION, FLAGS_NONE);
		}

		/// <summary>Creates a 96-bit <see cref="VersionStamp"/>, obtained from the database.</summary>
		/// <returns>Complete stamp, with a user version.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static VersionStamp Complete(ulong version, ushort order, int userVersion)
		{
			Contract.Between(userVersion, 0, 0xFFFF, message: "Local version must fit in 16-bits, and cannot be 0xFFFF.");
			return new VersionStamp(version, order, (ushort) userVersion, FLAGS_HAS_VERSION);
		}

		/// <summary>Creates a 96-bit <see cref="VersionStamp"/>, obtained from the database.</summary>
		/// <returns>Complete stamp, with a user version.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static VersionStamp Complete(ulong version, ushort order, ushort userVersion)
		{
			return new VersionStamp(version, order, userVersion, FLAGS_HAS_VERSION);
		}

		/// <summary>Test if the stamp has a user version (96-bits) or not (80-bits)</summary>
		public bool HasUserVersion
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (this.Flags & FLAGS_HAS_VERSION) != 0;
		}

		/// <summary>Test if the stamp is marked as <i>incomplete</i> (true), or has already been resolved by the database (false)</summary>
		public bool IsIncomplete
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (this.Flags & FLAGS_IS_INCOMPLETE) != 0;
		}

		/// <summary>Return the length (in bytes) of the VersionStamp when serialized in binary format</summary>
		/// <returns>Returns 12 bytes for stamps with a user version, and 10 bytes without.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int GetLength() => 10 + 2 * (this.Flags & FLAGS_HAS_VERSION);

		public override string ToString()
		{
			if (this.HasUserVersion)
			{
				return this.IsIncomplete
					? $"@?#{this.UserVersion}"
					: $"@{this.TransactionVersion}-{this.TransactionOrder}#{this.UserVersion}";
			}
			else
			{
				return this.IsIncomplete
					? "@?"
					: $"@{this.TransactionVersion}-{this.TransactionOrder}";
			}
		}

		public Slice ToSlice()
		{
			int len = GetLength(); // 10 or 12
			var tmp = new byte[len];
			WriteUnsafe(tmp.AsSpan(), in this);
			return new Slice(tmp);
		}

		/// <summary>Convert this 80-bits VersionStamp into a 80-bits UUID</summary>
		public Uuid80 ToUuid80()
		{
			if (this.HasUserVersion) throw new InvalidOperationException("Cannot convert 96-bit VersionStamp into a 80-bit UUID.");
			unsafe
			{
				Span<byte> ptr = stackalloc byte[Uuid80.SizeOf];
				WriteUnsafe(ptr, in this);
				Uuid80.ReadUnsafe(ptr, out var res);
				return res;
			}
		}

		/// <summary>Convert this 96-bits VersionStamp into a 96-bits UUID</summary>
		public Uuid96 ToUuid96()
		{
			if (!this.HasUserVersion) throw new InvalidOperationException("Cannot convert 80-bit VersionStamp into a 96-bit UUID.");
			unsafe
			{
				Span<byte> ptr = stackalloc byte[Uuid96.SizeOf];
				WriteUnsafe(ptr, in this);
				Uuid96.ReadUnsafe(ptr, out var res);
				return res;
			}
		}

		public void WriteTo(Span<byte> buffer)
		{
			int len = GetLength(); // 10 or 12
			if (buffer.Length < len) throw new ArgumentException($"The target buffer must be at least {len} bytes long.");
			WriteUnsafe(buffer.Slice(0, len), in this);
		}

		public bool TryWriteTo(Span<byte> buffer)
		{
			int len = GetLength(); // 10 or 12
			if (buffer.Length < len) return false;
			WriteUnsafe(buffer.Slice(0, len), in this);
			return true;
		}

		public void WriteTo(MutableSlice buffer)
		{
			int len = GetLength(); // 10 or 12
			if (buffer.Count < len) throw new ArgumentException($"The target buffer must be at least {len} bytes long.");
			WriteUnsafe(buffer.Substring(0, len).Span, in this);
		}

		public void WriteTo(ref SliceWriter writer)
		{
			WriteUnsafe(writer.AllocateSpan(GetLength()), in this);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void WriteToUnsafe(Span<byte> buffer)
		{
			WriteUnsafe(buffer, in this);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static unsafe void WriteUnsafe(Span<byte> buffer, in VersionStamp vs)
		{
			Contract.Debug.Assert(buffer.Length == 10 || buffer.Length == 12, "Buffer length must be 10 or 12");
			fixed (byte* ptr = &MemoryMarshal.GetReference(buffer))
			{
				UnsafeHelpers.StoreUInt64BE(ptr, vs.TransactionVersion);
				UnsafeHelpers.StoreUInt16BE(ptr + 8, vs.TransactionOrder);
				if (buffer.Length >= 12)
				{
					UnsafeHelpers.StoreUInt16BE(ptr + 10, vs.UserVersion);
				}
			}
		}

		/// <summary>Parse a VersionStamp from a sequence of 10 bytes</summary>
		/// <exception cref="FormatException">If the buffer length is not exactly 12 bytes</exception>
		[Pure]
		public static VersionStamp Parse(Slice data)
		{
			return TryParse(data, out var vs) ? vs : throw new FormatException("A VersionStamp is either 10 or 12 bytes.");
		}

		/// <summary>Try parsing a VersionStamp from a sequence of bytes</summary>
		public static bool TryParse(Slice data, out VersionStamp vs)
		{
			if (data.Count != 10 && data.Count != 12)
			{
				vs = default;
				return false;
			}
			ReadUnsafe(data.Span, out vs);
			return true;
		}

		/// <summary>Parse a VersionStamp from a sequence of 10 bytes</summary>
		/// <exception cref="FormatException">If the buffer length is not exactly 12 bytes</exception>
		[Pure]
		public static VersionStamp Parse(ReadOnlySpan<byte> data)
		{
			return TryParse(data, out var vs) ? vs : throw new FormatException("A VersionStamp is either 10 or 12 bytes.");
		}

		/// <summary>Try parsing a VersionStamp from a sequence of bytes</summary>
		public static bool TryParse(ReadOnlySpan<byte> data, out VersionStamp vs)
		{
			if (data.Length != 10 && data.Length != 12)
			{
				vs = default;
				return false;
			}
			ReadUnsafe(data, out vs);
			return true;
		}

		public static unsafe void ReadUnsafe(ReadOnlySpan<byte> buf, out VersionStamp vs)
		{
			// reads a complete 10 or 12 bytes VersionStamp
			Contract.Debug.Assert(buf.Length == 10 || buf.Length == 12);
			fixed (byte* ptr = &MemoryMarshal.GetReference(buf))
			{
				ulong ver = UnsafeHelpers.LoadUInt64BE(ptr);
				ushort order = UnsafeHelpers.LoadUInt16BE(ptr + 8);
				ushort idx = buf.Length == 10 ? NO_USER_VERSION : UnsafeHelpers.LoadUInt16BE(ptr + 10);
				ushort flags = FLAGS_NONE;
				if (buf.Length == 12) flags |= FLAGS_HAS_VERSION;
				if ((ver & HSB_VERSION) != 0) flags |= FLAGS_IS_INCOMPLETE;
				vs = new VersionStamp(ver, order, idx, flags);
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator VersionStamp(Uuid80 value) => FromUuid80(value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Uuid80(VersionStamp value) => value.ToUuid80();

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator VersionStamp(Uuid96 value) => FromUuid96(value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Uuid96(VersionStamp value) => value.ToUuid96();

		#region Equality, Comparision, ...

		public override bool Equals(object? obj)
		{
			return obj is VersionStamp vs && Equals(vs);
		}

		public override int GetHashCode()
		{
			return HashCodes.Combine(this.TransactionVersion.GetHashCode(), this.TransactionOrder, this.UserVersion, this.Flags);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(VersionStamp other)
		{
			//PERF: could we use Unsafe and compare the next sizeof(VersionStamp) bytes at once?
			return (this.TransactionVersion == other.TransactionVersion)
			   & (this.TransactionOrder == other.TransactionOrder)
			   & (this.UserVersion == other.UserVersion)
			   & (this.Flags == other.Flags);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(VersionStamp left, VersionStamp right)
		{
			return left.Equals(right);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(VersionStamp left, VersionStamp right)
		{
			return !left.Equals(right);
		}

		[Pure]
		public int CompareTo(VersionStamp other)
		{
			//ordering rules:
			// - incomplete stamps are stored AFTER resolved stamps (since if they commit they would have a value higher than any other stamp already in the database)
			// - ordered by transaction number then transaction batch order
			// - stamps with no user version are sorted before stamps with user version if they have the same first 10 bytes, so (XXXX) is before (XXXX, 0)

			if (this.IsIncomplete)
			{ // we ignore the transaction version/order!
				if (!other.IsIncomplete) return +1; // we are after
			}
			else
			{
				if (other.IsIncomplete) return -1; // we are before
				int cmp = this.TransactionVersion.CompareTo(other.TransactionVersion);
				if (cmp == 0) cmp = this.TransactionOrder.CompareTo(other.TransactionOrder);
				if (cmp != 0) return cmp;
			}

			// both have same version+order, or both are incomplete
			// => we need to decide on the (optional) user version
			return this.HasUserVersion 
				? (other.HasUserVersion ? this.UserVersion.CompareTo(other.UserVersion) : +1)
				: (other.HasUserVersion ? -1 : 0);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(VersionStamp left, VersionStamp right)
		{
			return left.CompareTo(right) < 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(VersionStamp left, VersionStamp right)
		{
			return left.CompareTo(right) <= 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(VersionStamp left, VersionStamp right)
		{
			return left.CompareTo(right) > 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(VersionStamp left, VersionStamp right)
		{
			return left.CompareTo(right) >= 0;
		}

		//REVIEW: does these make sense or not?
		// VersionStamp - VersionStamp == ???
		// VersionStamp + 123 == ???
		// VersionStamp * 2 == ???

		public sealed class Comparer : IEqualityComparer<VersionStamp>, IComparer<VersionStamp>
		{
			/// <summary>Default comparer for <see cref="VersionStamp"/>s</summary>
			public static Comparer Default { get; } = new Comparer();

			private Comparer()
			{ }

			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool Equals(VersionStamp x, VersionStamp y)
			{
				return x.Equals(y);
			}

			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public int GetHashCode(VersionStamp obj)
			{
				return obj.GetHashCode();
			}

			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public int Compare(VersionStamp x, VersionStamp y)
			{
				return x.CompareTo(y);
			}

		}

		#endregion

	}

}

#endif
