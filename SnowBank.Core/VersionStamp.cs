#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace System
{
	using System.ComponentModel;
	using System.Runtime.InteropServices;
	using SnowBank.Data.Json;
	using Globalization;
	using SnowBank.Buffers;
	using SnowBank.Buffers.Binary;

	/// <summary>VersionStamp</summary>
	/// <remarks>A VersionStamp is unique, monotonically (but not sequentially) increasing value for each committed transaction.
	/// Its size can either be 10 bytes (80-bits) or 12-bytes (96-bits).
	/// The first 8 bytes are the committed version of the database. The next 2 bytes are monotonic in the serialization order for transactions.
	/// The optional last 2 bytes can contain a user-provider version number used to allow multiple stamps inside the same transaction.
	/// </remarks>
	[DebuggerDisplay("{ToString(),nq}")]
	[ImmutableObject(true), PublicAPI, Serializable]
	public readonly struct VersionStamp : IEquatable<VersionStamp>, IComparable<VersionStamp>, IJsonSerializable, IJsonPackable, IJsonDeserializable<VersionStamp>
#if NET8_0_OR_GREATER
		, ISpanParsable<VersionStamp>
#endif
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

		/// <summary>Converts an 80-bits UUID into an 80-bits VersionStamp</summary>
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

		/// <summary>Converts a 96-bits UUID into a 96-bits VersionStamp</summary>
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
		/// <para>This stamp contains a temporary marker that will be later filled by the database with the actual VersionStamp at transaction commit time.</para>
		/// <para>If you need to create multiple distinct stamps within the same transaction, please use <see cref="Incomplete(int)"/> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static VersionStamp Incomplete()
		{
			return new VersionStamp(PLACEHOLDER_VERSION, PLACEHOLDER_ORDER, NO_USER_VERSION, FLAGS_IS_INCOMPLETE);
		}

		/// <summary>Creates an incomplete 96-bit <see cref="VersionStamp"/> with the given user version.</summary>
		/// <param name="userVersion">Value between 0 and 65535 that will be appended at the end of the VersionStamp, making it unique <i>within</i> the transaction.</param>
		/// <returns>Placeholder that will be serialized as <code>FF FF FF FF FF FF FF FF FF FF vv vv</code> (12 bytes) where 'vv vv' is the user version encoded in little-endian.</returns>
		/// <remarks>
		/// <para>This stamp contains a temporary marker that will be later filled by the database with the actual VersionStamp at transaction commit time.</para>
		/// </remarks>
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

		/// <summary>Creates an 80-bit <see cref="VersionStamp"/>.</summary>
		/// <returns>Complete stamp, with a user version.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static VersionStamp Custom(ulong version, ushort order, bool incomplete)
		{
			return new VersionStamp(version, order, NO_USER_VERSION, incomplete ? FLAGS_IS_INCOMPLETE : FLAGS_NONE);
		}

		/// <summary>Creates an 80-bit <see cref="VersionStamp"/>.</summary>
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

		/// <summary>Creates a 96-bit <see cref="VersionStamp"/>.</summary>
		/// <returns>Complete stamp, with a user version.</returns>
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

		/// <summary>Creates an 80-bit <see cref="VersionStamp"/>, obtained from the database.</summary>
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

		/// <inheritdoc />
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

		/// <summary>Returns a newly allocated <see cref="Slice"/> that represents this VersionStamp</summary>
		/// <remarks>The slice with have a length of either 10 or 12 bytes.</remarks>
		public Slice ToSlice()
		{
			int len = GetLength(); // 10 or 12
			var tmp = new byte[len];
			WriteUnsafe(tmp.AsSpan(), in this);
			return new Slice(tmp);
		}

		/// <summary>Converts this 80-bits VersionStamp into an 80-bits UUID</summary>
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

		/// <summary>Converts this 96-bits VersionStamp into a 96-bits UUID</summary>
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

		/// <summary>Writes this VersionStamp to the specified buffer, if it is large enough.</summary>
		/// <param name="buffer">Destination buffer, that must have a length of at least 10 or 12 bytes</param>
		/// <exception cref="ArgumentException"> if the buffer is not large enough.</exception>
		public void WriteTo(Span<byte> buffer)
		{
			int len = GetLength(); // 10 or 12
			if (buffer.Length < len) throw new ArgumentException($"The target buffer must be at least {len} bytes long.");
			WriteUnsafe(buffer.Slice(0, len), in this);
		}

		/// <summary>Writes this VersionStamp to the specified buffer, if it is large enough.</summary>
		/// <param name="buffer">Destination buffer, that must have a length of at least 10 or 12 bytes</param>
		/// <returns><c>true</c> if the buffer was large enough; otherwise, <c>false</c></returns>
		public bool TryWriteTo(Span<byte> buffer)
		{
			int len = GetLength(); // 10 or 12
			if (buffer.Length < len) return false;
			WriteUnsafe(buffer.Slice(0, len), in this);
			return true;
		}

		/// <summary>Writes this VersionStamp to the specified destination</summary>
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

		/// <summary>Attempts to parse a <see cref="VersionStamp"/> from a sequence of bytes.</summary>
		/// <param name="data">The byte sequence to parse.</param>
		/// <param name="vs">When this method returns, contains the parsed <see cref="VersionStamp"/> if the parsing succeeded, or the default value if it failed.</param>
		/// <returns><see langword="true"/> if the parsing was successful; otherwise, <see langword="false"/>.</returns>
		/// <remarks>A valid <see cref="VersionStamp"/> is either 10 or 12 bytes long.</remarks>
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

		/// <summary>[DANGEROUS] Reads a VersionStamp from a source buffer, that must be large enough.</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
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

		/// <summary>Parses the specified <see cref="ReadOnlySpan{T}"/> of characters into a <see cref="VersionStamp"/>.</summary>
		/// <param name="s">The span of characters to parse.</param>
		/// <param name="provider">This parameter is ignored.</param>
		/// <returns><see cref="VersionStamp"/> value equivalent to the characters contained in <paramref name="s"/>.</returns>
		/// <exception cref="FormatException">If the format is not valid.</exception>
		/// <remarks>This method can parse the result of calling <see cref="ToString()"/></remarks>
		public static VersionStamp Parse(string s, IFormatProvider? provider)
		{
			Contract.NotNull(s);
			return Parse(s.AsSpan(), provider);
		}

		/// <summary>Attempts to parse the specified <see cref="ReadOnlySpan{T}"/> of characters into a <see cref="VersionStamp"/>.</summary>
		/// <param name="s">The span of characters to parse.</param>
		/// <param name="provider">This parameter is ignored.</param>
		/// <param name="result">When this method returns, contains the <see cref="VersionStamp"/> value equivalent to the characters contained in <paramref name="s"/>, if the conversion succeeded, or the default value if the conversion failed.</param>
		/// <returns><see langword="true"/> if the <paramref name="s"/> was successfully parsed; otherwise, <see langword="true"/>.</returns>
		/// <remarks>This method can parse the result of calling <see cref="ToString()"/></remarks>
		public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out VersionStamp result)
		{
			if (s == null || !TryParse(s.AsSpan(), provider, out result))
			{
				result = default;
				return false;
			}
			return true;
		}

		/// <summary>Parses the specified <see cref="ReadOnlySpan{T}"/> of characters into a <see cref="VersionStamp"/>.</summary>
		/// <param name="s">The span of characters to parse.</param>
		/// <param name="provider">This parameter is ignored.</param>
		/// <returns><see cref="VersionStamp"/> value equivalent to the characters contained in <paramref name="s"/>.</returns>
		/// <exception cref="FormatException">If the format is not valid.</exception>
		/// <remarks>This method can parse the result of calling <see cref="ToString()"/></remarks>
		public static VersionStamp Parse(ReadOnlySpan<char> s, IFormatProvider? provider = null)
		{
			if (!TryParse(s, provider, out var result))
			{
				throw new FormatException("Unrecognized VersionStamp format.");
			}

			return result;
		}

		/// <summary>Attempts to parse the specified <see cref="ReadOnlySpan{T}"/> of characters into a <see cref="VersionStamp"/>.</summary>
		/// <param name="s">The span of characters to parse.</param>
		/// <param name="provider">This parameter is ignored.</param>
		/// <param name="result">When this method returns, contains the <see cref="VersionStamp"/> value equivalent to the characters contained in <paramref name="s"/>, if the conversion succeeded, or the default value if the conversion failed.</param>
		/// <returns><see langword="true"/> if the <paramref name="s"/> was successfully parsed; otherwise, <see langword="true"/>.</returns>
		/// <remarks>This method can parse the result of calling <see cref="ToString()"/></remarks>
		public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out VersionStamp result)
		{
			if (s.Length < 2) goto invalid;
			if (s[0] != '@') goto invalid;

			s = s[1..];

			if (s[0] == '?')
			{ // incomplete: "@?" or "@?#USER"

				if (s.Length == 1)
				{
					result = Incomplete();
					return true;
				}
				
				if (s.Length < 3 || s[1] != '#') goto invalid;
				
#if NET8_0_OR_GREATER
				if (!ushort.TryParse(s[2..], CultureInfo.InvariantCulture, out var x))
#else
				if (!ushort.TryParse(s[2..], out var x))
#endif
				{
					goto invalid;
				}

				result = Incomplete(x);
				return true;
			}

			// complete: "@VERSION-ORDER" or "@VERSION-ORDER#USER"
			int p = s.IndexOf('-');
			if (p <= 0) goto invalid;

#if NET8_0_OR_GREATER
			if (!ulong.TryParse(s[..p], CultureInfo.InvariantCulture, out var version))
#else
			if (!ulong.TryParse(s[..p], out var version))
#endif
			{
				goto invalid;
			}

			s = s[(p + 1)..];

			p = s.IndexOf('#');
			if (p == 0) goto invalid;
			ushort user, flags;
			
			if (p > 0)
			{
#if NET8_0_OR_GREATER
				if (!ushort.TryParse(s[(p + 1)..], CultureInfo.InvariantCulture, out user))
#else
				if (!ushort.TryParse(s[(p + 1)..], out user))
#endif
				{
					goto invalid;
				}
				s = s[..p];
				flags = FLAGS_HAS_VERSION;
			}
			else
			{
				user = 0;
				flags = NO_USER_VERSION;
			}

#if NET8_0_OR_GREATER
			if (!ushort.TryParse(s, CultureInfo.InvariantCulture, out var order))
#else
			if (!ushort.TryParse(s, out var order))
#endif
			{
				goto invalid;
			}

			result = new(version, order, user, flags);
			return true;
			
		invalid:
			result = default;
			return false;
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

		/// <inheritdoc />
		public override bool Equals(object? obj)
		{
			return obj is VersionStamp vs && Equals(vs);
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return HashCode.Combine(this.TransactionVersion.GetHashCode(), (int) this.TransactionOrder, (int) this.UserVersion, (int) this.Flags);
		}

		/// <inheritdoc />
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

		/// <inheritdoc />
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

		/// <summary>Compares <see cref="VersionStamp"/> instances for equality and ordering</summary>
		public sealed class Comparer : IEqualityComparer<VersionStamp>, IComparer<VersionStamp>
		{

			/// <summary>Default comparer for <see cref="VersionStamp"/>s</summary>
			public static Comparer Default { get; } = new();

			private Comparer()
			{ }

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool Equals(VersionStamp x, VersionStamp y)
			{
				return x.Equals(y);
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public int GetHashCode(VersionStamp obj)
			{
				return obj.GetHashCode();
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public int Compare(VersionStamp x, VersionStamp y)
			{
				return x.CompareTo(y);
			}

		}

		#endregion

		#region JSON Serialization...

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => writer.WriteValue(this.ToString());

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => JsonString.Return(this.ToString());

		/// <inheritdoc />
		public static VersionStamp JsonDeserialize(JsonValue value, ICrystalJsonTypeResolver? resolver = null) => value switch
		{
			null or JsonNull => Incomplete(), //REVIEW: there is no real good candidate for this ... ?
			JsonString str => Parse(str.Value),
			_ => throw JsonBindingException.CannotBindJsonValueToThisType(value, typeof(VersionStamp)),
		};

		#endregion

	}

}
