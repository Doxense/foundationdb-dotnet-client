#region Copyright Doxense 2010-2021
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{
	using System;
	using NodaTime;
	using NodaTime.Text;

	internal static class CrystalJsonNodaPatterns
	{
		//REVIEW: trouver un moyen de s'assurer que cette classe est JITée avant CrystalJsonWriter/JsonString (pour virer le check au runtime si le static ctor s'est executé!)

		public static readonly InstantPattern Instants = InstantPattern.ExtendedIso;

		public static readonly LocalDateTimePattern LocalDateTimes = LocalDateTimePattern.ExtendedIso;

		public static readonly LocalDatePattern LocalDates = LocalDatePattern.Iso;

		public static readonly LocalTimePattern LocalTimes = LocalTimePattern.ExtendedIso;

		public static readonly ZonedDateTimePattern ZonedDateTimes = ZonedDateTimePattern.CreateWithInvariantCulture("uuuu'-'MM'-'dd'T'HH':'mm':'ss;FFFFFFFo<G> z", DateTimeZoneProviders.Tzdb);

		public static readonly OffsetDateTimePattern OffsetDateTimes = OffsetDateTimePattern.Rfc3339;

		public static readonly OffsetPattern Offsets = OffsetPattern.GeneralInvariant;
	}

}
