#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Mathematics.Statistics
{
	using System;
	using System.Diagnostics;
	using System.Globalization;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using System.Threading;
	using JetBrains.Annotations;

	/// <summary>Distributed global counter made up of individual counters</summary>
	[DebuggerDisplay("Count={Counters.Length}, Total={GetTotal()}")]
	public sealed class RobustCounters
	{
		public RobustCounter[] Counters { get; }

		public RobustCounters(int count)
		{
			var counters = new RobustCounter[count];
			for (int i = 0; i < counters.Length; i++)
			{
				counters[i] = new RobustCounter();
			}
			this.Counters = counters;
		}

		/// <summary>Return the global tally of all individual counters</summary>
		/// <remarks>
		/// The value is only considered "stable" if all workers threads have stopped./
		/// While the workers are running, the tally may not be accurate</remarks>
		public long GetTotal()
		{
			long total = 0;
			foreach (var counter in this.Counters)
			{
				total += counter.SafeRead();
			}
			return total;
		}

	}

	/// <summary>Isolated counter that will not introduce false-sharing between concurrent threads</summary>
	[DebuggerDisplay("{Counter.Value}")]
	public sealed class RobustCounter
	{

		private PaddedInt32 Counter;

		public RobustCounter()
		{ }

		public RobustCounter(int initialValue)
		{
			Volatile.Write(ref this.Counter.Value, initialValue);
		}

		/// <summary>Increment from inside the worker thread, when no one else is expected to write to this counter</summary>
		/// <returns>Incremented value</returns>
		/// <example>new RobustCounter(42).Increment() == 43</example>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int UnsafeIncrement()
		{
			return ++this.Counter.Value;
		}

		/// <summary>Read the value from inside the worker thread, when no one else is expected to write to this counter</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int UnsafeRead()
		{
			return this.Counter.Value;
		}

		/// <summary>Reset the value from inside the worker thread, when no one else is expected to write to this counter</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void UnsafeReset()
		{
			this.Counter.Value = 0;
		}

		/// <summary>Read the value from outside</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int SafeRead()
		{
			return Volatile.Read(ref this.Counter.Value);
		}

		/// <summary>Holds a 32-bit integer that is guaranteed to not cause false sharing issues</summary>
		/// <remarks>CAUTION: this struct has a size of 128 bytes</remarks>
		[StructLayout(LayoutKind.Explicit, Size = 128)]
		[DebuggerDisplay("{Value}")]
		private struct PaddedInt32
		{
			// The layout of in memory should be:
			// - PADDING: 64 bytes
			// - VALUE:    4 bytes
			// - PADDING: 60 bytes

			[FieldOffset(64)]
			public int Value;

			public override string ToString()
			{
				return this.Value.ToString(CultureInfo.InvariantCulture);
			}

		}

	}

}
