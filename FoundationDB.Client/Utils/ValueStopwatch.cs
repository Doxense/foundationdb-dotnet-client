#region BSD License
/* Copyright (c) 2013-2023 Doxense SAS
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
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using JetBrains.Annotations;

	/// <summary>Helper that can measure time elapsed with the same precision has <see cref="Stopwatch"/>, but without allocating an instance</summary>
	/// <remarks>This watch cannot be stopped or restarted</remarks>
	internal readonly struct ValueStopwatch
	{
		private static readonly double Frequency = Stopwatch.Frequency;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ValueStopwatch StartNew()
		{
			return new ValueStopwatch(Stopwatch.GetTimestamp());
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ValueStopwatch(long startTicks)
		{
			this.Start = startTicks;
		}

		public readonly long Start;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public long GetRawElapsedTicks()
		{
			return Stopwatch.GetTimestamp() - this.Start;
		}

		public long GetElapsedDateTimeTicks()
		{
			long rawTicks = GetRawElapsedTicks();
			if (Stopwatch.IsHighResolution)
			{
				// convert high resolution perf counter to DateTime ticks
				return unchecked((long) (rawTicks * ValueStopwatch.Frequency));
			}
			else
			{
				return rawTicks;
			}
		}

		public TimeSpan Elapsed
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new TimeSpan(GetElapsedDateTimeTicks());
		}
	}
}
