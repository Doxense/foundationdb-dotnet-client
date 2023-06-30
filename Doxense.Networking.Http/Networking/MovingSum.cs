#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Networking
{
	using System;
	using System.Diagnostics;
	using System.Diagnostics.Contracts;
	using System.Numerics;
	using System.Runtime.CompilerServices;

	[DebuggerDisplay("Total={Total}, Last={Last}, Count={Count}/{Data.Length}")]
	public struct MovingSum<T> where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>
	{

		public MovingSum(int length)
		{
			this.Data = new T[length];
			this.Count = 0;
			this.Next = 0;
			this.Total = default!;
		}

		public MovingSum(int length, T initialValue)
		{
			this.Data = new T[length];
			this.Data[0] = initialValue;
			this.Count = 1;
			this.Next = 1;
			this.Total = initialValue;
		}

		/// <summary>Buffer for last remembered samples</summary>
		public readonly T[] Data;

		/// <summary>Number of filled slots</summary>
		public int Count;

		/// <summary>Index of the next slot that will be overwritten</summary>
		public int Next;

		/// <summary>Running total of the last samples (or Zero if no samples yet)</summary>
		public T? Total;

		/// <summary>Last inserted sample (or Zero if no samples yet)</summary>
		public T? Last
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Next != 0 ? this.Data[this.Next - 1] : this.Count != 0 ? this.Data[this.Count - 1] : default;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (T? Total, int Samples) Read() => (this.Total, this.Count);

		public (T Total, int Samples) Add(T sample)
		{
			var data = this.Data;
			var p = this.Next;
			var c = this.Count;
			var t = this.Total;
			if (c < data.Length)
			{ // growing phase
				c = c + 1;
				t = t + sample;
				this.Count = c;
			}
			else
			{ // crusing phase
				t = t - data[p] + sample;
			}
			data[p] = sample;
			this.Next = (p + 1) % this.Data.Length;
			this.Total = t;
			return (t, c);
		}

	}

}
