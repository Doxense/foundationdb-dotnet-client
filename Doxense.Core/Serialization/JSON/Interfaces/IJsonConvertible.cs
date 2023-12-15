#region Copyright (c) 2023 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

	public interface IJsonConvertible
	{

		//note: ToString() existe déjà sur System.Object

		/// <summary>Retourne la représentation textuelle correspondante de cette valeur</summary>
		/// <returns>Valeur texte pour des chaînes, nombres, booléens, dates. Retourne null pour toute instance de JsonNull. Génère une exception pour les Array ou Object</returns>
		/// <remarks>Cette méthode permet de faire la différence entre des valeurs null/manquante et chaîne vide, deux cas où <see cref="JsonValue.ToString()"/> retourne <see cref="String.Empty"/>.</remarks>
		string? ToStringOrDefault();

		bool ToBoolean();
		bool? ToBooleanOrDefault();

		byte ToByte();
		byte? ToByteOrDefault();

		sbyte ToSByte();
		sbyte? ToSByteOrDefault();

		char ToChar();
		char? ToCharOrDefault();

		short ToInt16();
		short? ToInt16OrDefault();

		ushort ToUInt16();
		ushort? ToUInt16OrDefault();

		int ToInt32();
		int? ToInt32OrDefault();

		uint ToUInt32();
		uint? ToUInt32OrDefault();

		long ToInt64();
		long? ToInt64OrDefault();

		ulong ToUInt64();
		ulong? ToUInt64OrDefault();

		float ToSingle();
		float? ToSingleOrDefault();

		double ToDouble();
		double? ToDoubleOrDefault();

		decimal ToDecimal();
		decimal? ToDecimalOrDefault();

		Guid ToGuid();
		Guid? ToGuidOrDefault();

		Uuid128 ToUuid128();
		Uuid128? ToUuid128OrDefault();

		Uuid96 ToUuid96();
		Uuid96? ToUuid96OrDefault();

		Uuid80 ToUuid80();
		Uuid80? ToUuid80OrDefault();

		Uuid64 ToUuid64();
		Uuid64? ToUuid64OrDefault();

		DateTime ToDateTime();
		DateTime? ToDateTimeOrDefault();

		DateTimeOffset ToDateTimeOffset();
		DateTimeOffset? ToDateTimeOffsetOrDefault();

		TimeSpan ToTimeSpan();
		TimeSpan? ToTimeSpanOrDefault();

		TEnum ToEnum<TEnum>() where TEnum : struct, Enum;
		TEnum? ToEnumOrDefault<TEnum>() where TEnum : struct, Enum;

		NodaTime.Instant ToInstant();
		NodaTime.Instant? ToInstantOrDefault();

		NodaTime.Duration ToDuration();
		NodaTime.Duration? ToDurationOrDefault();
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
