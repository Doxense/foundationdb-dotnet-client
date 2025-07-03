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

namespace SnowBank.Data.Tuples.Binary
{

	/// <summary>Represent a custom user type for the TuPack encoding</summary>
	[DebuggerDisplay("{ToString()},nq")]
	[PublicAPI]
	public sealed class TuPackUserType : IEquatable<TuPackUserType>, IFormattable
	{

		public const byte TypeDirectory = 0xFE;

		public const byte TypeSystem = 0xFF;

		/// <summary>Directory Layer (<c>0xFE</c>)</summary>
		public static readonly TuPackUserType Directory = new(TypeDirectory);

		/// <summary>System Subspace (<c>0xFF</c>)</summary>
		public static readonly TuPackUserType System = new(TypeSystem);

		public TuPackUserType(int type)
		{
			this.Type = type;
		}

		public TuPackUserType(int type, Slice value)
		{
			this.Type = type;
			this.Value = value;
		}

		public readonly int Type;

		public readonly Slice Value;

		public override string ToString()
		{
			switch (this.Type)
			{
				case TypeDirectory: return this.Value.IsNullOrEmpty ? "|Directory|" : $"|Directory|{this.Value:K}";
				case TypeSystem: return this.Value.IsNullOrEmpty ? "|System|" : $"|System|{this.Value:K}";
			}

			if (this.Value.IsNull)
			{
				return $"|User-{this.Type:X02}|";
			}
			return $"|User-{this.Type:X02}:{this.Value:N}|";
		}

		public string ToString(string? format, IFormatProvider? formatProvider) => ToString();

		/// <summary>Returns a type that matches a system key (ex: <c>'\xFF/metadataVersion'</c>)</summary>
		/// <param name="name">Key, excluding the initial <c>\xFF</c> byte (ex: "/metadataVersion" instead of "\xFF/metadataVersion")</param>
		public static TuPackUserType SystemKey(Slice name) => new(TypeSystem, name);

		/// <summary>Returns a type that matches a system key (ex: <c>'\xFF/metadataVersion'</c>)</summary>
		/// <param name="name">Key, excluding the initial <c>\xFF</c> byte (ex: "/metadataVersion" instead of "\xFF/metadataVersion")</param>
		public static TuPackUserType SystemKey(ReadOnlySpan<byte> name) => new(TypeSystem, name.ToSlice());

		/// <summary>Returns a type that matches a system key (ex: <c>'\xFF/metadataVersion'</c>)</summary>
		/// <param name="name">Name of key, excluding the initial <c>\xFF</c> byte (ex: "/metadataVersion" instead of "\xFF/metadataVersion")</param>
		public static TuPackUserType SystemKey(string name) => new(TypeSystem, Slice.FromByteString(name));

		/// <summary>Returns a custom user type with a given prefix (ex: <c>40</c>)</summary>
		/// <param name="type">Prefix for this type, which must be between <see cref="TupleTypes.UserType0"/> (0x40) and <see cref="TupleTypes.UserTypeF"/> (0x4F)</param>
		public static TuPackUserType Custom(byte type) => type is >= TupleTypes.UserType0 and <= TupleTypes.UserTypeF ? new(type) : throw ErrorTypeOutOfRange(type);

		/// <summary>Returns a custom user type with a given prefix and payload (ex: <c>40 XX YY ZZ ...</c>)</summary>
		/// <param name="type">Prefix for this type, which must be between <see cref="TupleTypes.UserType0"/> (0x40) and <see cref="TupleTypes.UserTypeF"/> (0x4F)</param>
		/// <param name="value">Payload for this type</param>
		public static TuPackUserType Custom(byte type, Slice value) => type is >= TupleTypes.UserType0 and <= TupleTypes.UserTypeF ? new(type, value) : throw ErrorTypeOutOfRange(type);

		/// <summary>Returns a custom user type with a given prefix and payload (ex: <c>40 XX YY ZZ ...</c>)</summary>
		/// <param name="type">Prefix for this type, which must be between <see cref="TupleTypes.UserType0"/> (0x40) and <see cref="TupleTypes.UserTypeF"/> (0x4F)</param>
		/// <param name="value">Payload for this type</param>
		public static TuPackUserType Custom(byte type, ReadOnlySpan<byte> value) => type is >= TupleTypes.UserType0 and <= TupleTypes.UserTypeF ? new(type, value.ToSlice()) : throw ErrorTypeOutOfRange(type);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static ArgumentOutOfRangeException ErrorTypeOutOfRange(byte type) => new(nameof(type), type, "Custom user types must be between 0x40 and 0x4F");

		#region Equality...

		public override bool Equals(object? obj) => obj is TuPackUserType ut && Equals(ut);

		public bool Equals(TuPackUserType? other)
		{
			if (other == null) return false;
			if (ReferenceEquals(this, other)) return true;
			return this.Type == other.Type && this.Value.Equals(other.Value);
		}

		public override int GetHashCode() => HashCode.Combine(this.Type, this.Value.GetHashCode());

		#endregion

	}

}
