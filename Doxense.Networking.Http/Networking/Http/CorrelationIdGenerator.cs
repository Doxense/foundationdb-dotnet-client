#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Networking.Http
{
	using System;
	using System.Threading;

	internal static class CorrelationIdGenerator
	{
		// Base32 encoding - in ascii sort order for easy text based sorting
		private static readonly char[] s_encode32Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUV".ToCharArray();

		// Seed the _lastConnectionId for this application instance with
		// the number of 100-nanosecond intervals that have elapsed since 12:00:00 midnight, January 1, 0001
		// for a roughly increasing _lastId over restarts
		private static long _lastId = DateTime.UtcNow.Ticks;

		public static string GetNextId() => GenerateId(Interlocked.Increment(ref _lastId));

		private static string GenerateId(long id)
		{
			return string.Create(14, id, (buffer, value) =>
			{
				char[] encode32Chars = s_encode32Chars;

				buffer[13] = 'C'; //note: on rajoute un 'C' a la fin comme 'Client', pour �viter de rentrer en collision avec les TraceIdentifier de requetes servers (qui utilisent le m�me format!)
				buffer[12] = encode32Chars[value & 31];
				buffer[11] = encode32Chars[(value >> 5) & 31];
				buffer[10] = encode32Chars[(value >> 10) & 31];
				buffer[9] = encode32Chars[(value >> 15) & 31];
				buffer[8] = encode32Chars[(value >> 20) & 31];
				buffer[7] = encode32Chars[(value >> 25) & 31];
				buffer[6] = encode32Chars[(value >> 30) & 31];
				buffer[5] = encode32Chars[(value >> 35) & 31];
				buffer[4] = encode32Chars[(value >> 40) & 31];
				buffer[3] = encode32Chars[(value >> 45) & 31];
				buffer[2] = encode32Chars[(value >> 50) & 31];
				buffer[1] = encode32Chars[(value >> 55) & 31];
				buffer[0] = encode32Chars[(value >> 60) & 31];
			});
		}

	}
}
