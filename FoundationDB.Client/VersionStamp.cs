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

namespace FoundationDB.Client
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using Doxense;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using JetBrains.Annotations;

	/// <summary>VersionStamp</summary>
	/// <remarks>A versionstamp is unique, monotonically (but not sequentially) increasing value for each committed transaction.
	/// Its size can either be 10 bytes (80-bits) or 12-bytes (96-bits).
	/// The first 8 bytes are the committed version of the database. The next 2 bytes are monotonic in the serialization order for transactions.
	/// The optional last 2 bytes can contain a user-provider version number used to allow multiple stamps inside the same transaction.
	/// </remarks>
	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct VersionStamp : IEquatable<VersionStamp>, IComparable<VersionStamp>
	{
		//REVIEW: they are called "Versionstamp" in the doc, but "VersionStamp" seems more  .NETy (like 'TimeSpan').
		// => Should we keep the uppercase 'S' or not ?

		private const ulong PLACEHOLDER_VERSION = ulong.MaxValue;
		private const ushort PLACEHOLDER_ORDER = ushort.MaxValue;
		private const ushort NO_USER_VERSION = 0;

		private const ushort FLAGS_NONE = 0x0;
		private const ushort FLAGS_HAS_VERSION = 0x1; // unset: 80-bits, set: 96-bits
		private const ushort FLAGS_IS_INCOMPLETE = 0x2; // unset: complete, set: incomplete

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

		/// <summary>Creates an incomplete 80-bit <see cref="VersionStamp"/> with no user version.</summary>
		/// <returns>Placeholder that will be serialized as <code>FF FF FF FF FF FF FF FF FF FF</code> (10 bytes).</returns>
		/// <remarks>
		/// This stamp contains a temporary marker that will be later filled by the database with the actual VersioStamp by the database at transaction commit time.
		/// If you need to create multiple distinct stamps within the same transaction, please use <see cref="Incomplete(int)"/> instead.
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static VersionStamp Incomplete()
		{
			return new VersionStamp(PLACEHOLDER_VERSION, PLACEHOLDER_ORDER, NO_USER_VERSION, FLAGS_IS_INCOMPLETE);
		}

		/// <summary>Creates an incomplete 96-bit <see cref="VersionStamp"/> with the given user version.</summary>
		/// <param name="userVersion">Value between 0 and 65535 that will be appended at the end of the Versionstamp, making it unique <i>within</i> the transaction.</param>
		/// <returns>Placeholder that will be serialized as <code>FF FF FF FF FF FF FF FF FF FF vv vv</code> (12 bytes) where 'vv vv' is the user version encoded in little-endian.</returns>
		/// <exception cref="ArgumentException">If <paramref name="userVersion"/> is less than <c>0</c>, or greater than <c>65534</c> (0xFFFE).</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static VersionStamp Incomplete(int userVersion)
		{
			Contract.Between(userVersion, 0, 0xFFFF, nameof(userVersion), "Local version must fit in 16-bits.");
			return new VersionStamp(PLACEHOLDER_VERSION, PLACEHOLDER_ORDER, (ushort) userVersion, FLAGS_IS_INCOMPLETE | FLAGS_HAS_VERSION);
		}

		/// <summary>Creates an incomplete 96-bit <see cref="VersionStamp"/> with the given user version.</summary>
		/// <param name="userVersion">Value between 0 and 65535 that will be appended at the end of the Versionstamp, making it unique <i>within</i> the transaction.</param>
		/// <returns>Placeholder that will be serialized as <code>FF FF FF FF FF FF FF FF FF FF vv vv</code> (12 bytes) where 'vv vv' is the user version encoded in little-endian.</returns>
		/// <exception cref="ArgumentException">If <paramref name="userVersion"/> is less than <c>0</c>, or greater than <c>65534</c> (0xFFFE).</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static VersionStamp Incomplete(ushort userVersion)
		{
			return new VersionStamp(PLACEHOLDER_VERSION, PLACEHOLDER_ORDER, userVersion, FLAGS_IS_INCOMPLETE | FLAGS_HAS_VERSION);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static VersionStamp Custom(ulong version, ushort order, bool incomplete)
		{
			return new VersionStamp(version, order, NO_USER_VERSION, incomplete ? FLAGS_IS_INCOMPLETE : FLAGS_NONE);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static VersionStamp Custom(ulong version, ushort order, int userVersion, bool incomplete)
		{
			Contract.Between(userVersion, 0, 0xFFFF, nameof(userVersion), "Local version must fit in 16-bits.");
			return new VersionStamp(version, order, (ushort) userVersion, incomplete ? (ushort) (FLAGS_IS_INCOMPLETE | FLAGS_HAS_VERSION) : FLAGS_HAS_VERSION);
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
			Contract.Between(userVersion, 0, 0xFFFF, nameof(userVersion), "Local version must fit in 16-bits, and cannot be 0xFFFF.");
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

		/// <summary>Return the length (in bytes) of the versionstamp when serialized in binary format</summary>
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
			var tmp = Slice.Create(len);
			unsafe
			{
				fixed (byte* ptr = &tmp.DangerousGetPinnableReference())
				{
					WriteUnsafe(ptr, len, in this);
				}
			}
			return tmp;
		}

		public void WriteTo(in Slice buffer)
		{
			int len = GetLength(); // 10 or 12
			if (buffer.Count < len) throw new ArgumentException($"The target buffer must be at least {len} bytes long.");
			unsafe
			{
				fixed (byte* ptr = &buffer.DangerousGetPinnableReference())
				{
					WriteUnsafe(ptr, len, in this);
				}
			}
		}

		public void WriteTo(ref SliceWriter writer)
		{
			var tmp = writer.Allocate(GetLength());
			unsafe
			{
				fixed (byte* ptr = &tmp.DangerousGetPinnableReference())
				{
					WriteUnsafe(ptr, tmp.Count, in this);
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static unsafe void WriteUnsafe(byte* ptr, int len, in VersionStamp vs)
		{
			Contract.Debug.Assert(len == 10 || len == 12);
			UnsafeHelpers.StoreUInt64BE(ptr, vs.TransactionVersion);
			UnsafeHelpers.StoreUInt16BE(ptr + 8, vs.TransactionOrder);
			if (len == 12)
			{
				UnsafeHelpers.StoreUInt16BE(ptr + 10, vs.UserVersion);
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
			unsafe
			{
				fixed (byte* ptr = &data.DangerousGetPinnableReference())
				{
					ReadUnsafe(ptr, data.Count, FLAGS_NONE, out vs);
					return true;
				}
			}
		}

		/// <summary>Parse a VersionStamp from a sequence of 10 bytes</summary>
		/// <exception cref="FormatException">If the buffer length is not exactly 12 bytes</exception>
		[Pure]
		public static VersionStamp ParseIncomplete(Slice data)
		{
			return TryParseIncomplete(data, out var vs) ? vs : throw new FormatException("A VersionStamp is either 10 or 12 bytes.");
		}

		/// <summary>Try parsing a VersionStamp from a sequence of bytes</summary>
		public static bool TryParseIncomplete(Slice data, out VersionStamp vs)
		{
			if (data.Count != 10 && data.Count != 12)
			{
				vs = default;
				return false;
			}
			unsafe
			{
				fixed (byte* ptr = &data.DangerousGetPinnableReference())
				{
					ReadUnsafe(ptr, data.Count, FLAGS_IS_INCOMPLETE, out vs);
					return true;
				}
			}
		}

		internal static unsafe void ReadUnsafe(byte* ptr, int len, ushort flags, out VersionStamp vs)
		{
			Contract.Debug.Assert(len == 10 || len == 12);
			// reads a complete 12 bytes Versionstamp
			ulong ver = UnsafeHelpers.LoadUInt64BE(ptr);
			ushort order = UnsafeHelpers.LoadUInt16BE(ptr + 8);
			ushort idx = len == 10 ? NO_USER_VERSION : UnsafeHelpers.LoadUInt16BE(ptr + 10);
			flags |= len == 12 ? FLAGS_HAS_VERSION : FLAGS_NONE;
			vs = new VersionStamp(ver, order, idx, flags);
		}

		#region Equality, Comparision, ...

		public override bool Equals(object obj)
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
