#region Copyright Doxense 2010-2014
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
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
