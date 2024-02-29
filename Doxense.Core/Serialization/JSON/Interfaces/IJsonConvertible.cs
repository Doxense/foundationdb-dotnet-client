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

namespace Doxense.Serialization.Json
{
	using System;
	using System.Diagnostics.CodeAnalysis;

	[Obsolete] // this interface does not provide any benefit and will be removed soon
	public interface IJsonConvertible
	{

		//note: ToString() existe déjà sur System.Object

		/// <summary>Retourne la représentation textuelle correspondante de cette valeur</summary>
		/// <returns>Valeur texte pour des chaînes, nombres, booléens, dates. Retourne null pour toute instance de JsonNull. Génère une exception pour les Array ou Object</returns>
		/// <remarks>Cette méthode permet de faire la différence entre des valeurs null/manquante et chaîne vide, deux cas où <see cref="JsonValue.ToString()"/> retourne <see cref="String.Empty"/>.</remarks>
		[return: NotNullIfNotNull(nameof(defaultValue))]
		string? ToStringOrDefault(string? defaultValue = default);

		bool ToBoolean();
		[return: NotNullIfNotNull(nameof(defaultValue))]
		bool? ToBooleanOrDefault(bool? defaultValue = default);

		byte ToByte();
		[return: NotNullIfNotNull(nameof(defaultValue))]
		byte? ToByteOrDefault(byte? defaultValue = default);

		sbyte ToSByte();
		[return: NotNullIfNotNull(nameof(defaultValue))]
		sbyte? ToSByteOrDefault(sbyte? defaultValue = default);

		char ToChar();
		[return: NotNullIfNotNull(nameof(defaultValue))]
		char? ToCharOrDefault(char? defaultValue = default);

		short ToInt16();
		[return: NotNullIfNotNull(nameof(defaultValue))]
		short? ToInt16OrDefault(short? defaultValue = default);

		ushort ToUInt16();
		[return: NotNullIfNotNull(nameof(defaultValue))]
		ushort? ToUInt16OrDefault(ushort? defaultValue = default);

		int ToInt32();
		[return: NotNullIfNotNull(nameof(defaultValue))]
		int? ToInt32OrDefault(int? defaultValue = default);

		uint ToUInt32();
		[return: NotNullIfNotNull(nameof(defaultValue))]
		uint? ToUInt32OrDefault(uint? defaultValue = default);

		long ToInt64();
		[return: NotNullIfNotNull(nameof(defaultValue))]
		long? ToInt64OrDefault(long? defaultValue = default);

		ulong ToUInt64();
		[return: NotNullIfNotNull(nameof(defaultValue))]
		ulong? ToUInt64OrDefault(ulong? defaultValue = default);

		float ToSingle();
		[return: NotNullIfNotNull(nameof(defaultValue))]
		float? ToSingleOrDefault(float? defaultValue = default);

		double ToDouble();
		[return: NotNullIfNotNull(nameof(defaultValue))]
		double? ToDoubleOrDefault(double? defaultValue = default);

		decimal ToDecimal();
		[return: NotNullIfNotNull(nameof(defaultValue))]
		decimal? ToDecimalOrDefault(decimal? defaultValue = default);

		Guid ToGuid();
		[return: NotNullIfNotNull(nameof(defaultValue))]
		Guid? ToGuidOrDefault(Guid? defaultValue = default);

		Uuid128 ToUuid128();
		[return: NotNullIfNotNull(nameof(defaultValue))]
		Uuid128? ToUuid128OrDefault(Uuid128? defaultValue = default);

		Uuid96 ToUuid96();
		[return: NotNullIfNotNull(nameof(defaultValue))]
		Uuid96? ToUuid96OrDefault(Uuid96? defaultValue = default);

		Uuid80 ToUuid80();
		[return: NotNullIfNotNull(nameof(defaultValue))]
		Uuid80? ToUuid80OrDefault(Uuid80? defaultValue = default);

		Uuid64 ToUuid64();
		[return: NotNullIfNotNull(nameof(defaultValue))]
		Uuid64? ToUuid64OrDefault(Uuid64? defaultValue = default);

		DateTime ToDateTime();
		[return: NotNullIfNotNull(nameof(defaultValue))]
		DateTime? ToDateTimeOrDefault(DateTime? defaultValue = default);

		DateTimeOffset ToDateTimeOffset();
		[return: NotNullIfNotNull(nameof(defaultValue))]
		DateTimeOffset? ToDateTimeOffsetOrDefault(DateTimeOffset? defaultValue = default);

		TimeSpan ToTimeSpan();
		[return: NotNullIfNotNull(nameof(defaultValue))]
		TimeSpan? ToTimeSpanOrDefault(TimeSpan? defaultValue = default);

		TEnum ToEnum<TEnum>() where TEnum : struct, Enum;
		[return: NotNullIfNotNull(nameof(defaultValue))]
		TEnum? ToEnumOrDefault<TEnum>(TEnum? defaultValue = default) where TEnum : struct, Enum;

		NodaTime.Instant ToInstant();
		[return: NotNullIfNotNull(nameof(defaultValue))]
		NodaTime.Instant? ToInstantOrDefault(NodaTime.Instant? defaultValue = default);

		NodaTime.Duration ToDuration();
		[return: NotNullIfNotNull(nameof(defaultValue))]
		NodaTime.Duration? ToDurationOrDefault(NodaTime.Duration? defaultValue = default);
		//REVIEW: soit on fait tous les types de NodaTime, soit on en fait aucun...

	}

	public interface IJsonTryConvertible
	{
		bool TryConvertString(out string value);
		//TODO: Byte, SByte, Char, Bool
		bool TryConvertInt16(out short value);
		bool TryConvertUInt16(out ushort value);
		bool TryConvertInt32(out int value);
		bool TryConvertInt32(out uint value);
		bool TryConvertInt64(out long value);
		bool TryConvertUInt64(out ulong value);
		bool TryConvertSingle(out float value);
		bool TryConvertDouble(out double value);
		bool TryConvertDecimal(out decimal value);
	}


}
