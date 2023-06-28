#region Copyright Doxense 2013-2015
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Mathematics.Statistics // REVIEW: Doxense.Benchmarking ?
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;

	public class RobustTimeLine
	{

		public List<RobustHistogram> Histos { get; }
		public TimeSpan Step { get; }
		public RobustHistogram.TimeScale Scale { get; }
		public Func<RobustHistogram, int, bool> Completed { get; }

		private int LastIndex { get; set; }
		private Stopwatch Clock { get; }
		private int Offset { get; set; }

		public RobustTimeLine(TimeSpan step, RobustHistogram.TimeScale scale = RobustHistogram.TimeScale.Milliseconds, Func<RobustHistogram, int, bool> onCompleted = null)
		{
			if (step <= TimeSpan.Zero) throw new ArgumentException("Time step must be greater than zero", nameof(step));

			this.Histos = new List<RobustHistogram>();
			this.Step = step;
			this.Scale = scale;
			this.Completed = onCompleted;
			this.Clock = Stopwatch.StartNew();
		}

		public int Count
		{
			get { return this.Histos.Count; }
		}

		public void Start()
		{
			this.Clock.Restart();
		}

		public void Stop()
		{
			this.Clock.Stop();
		}

		private int GetGraphIndex(TimeSpan elapsed)
		{
			return (int)(elapsed.Ticks / this.Step.Ticks);
		}

		private bool HasFrame(int index)
		{
			index -= this.Offset;
			return index >= 0 && index < this.Histos.Count;
		}

		private RobustHistogram GetFrame(TimeSpan elapsed)
		{
			int index = GetGraphIndex(elapsed);

			if (index != this.LastIndex && this.Completed != null && HasFrame(this.LastIndex))
			{
				if (this.Completed(this.Histos[this.LastIndex - this.Offset], this.LastIndex))
				{ // reset!
					this.Histos.Clear();
					this.Offset = this.LastIndex;
				}
				this.LastIndex = index;
			}

			while (!HasFrame(index))
			{
				var histo = new RobustHistogram(this.Scale);
				this.Histos.Add(histo);
			}

			return this.Histos[index - this.Offset];
		}

		public void Add(double value)
		{
			GetFrame(this.Clock.Elapsed).Add(value);
		}

		public void Add(TimeSpan value)
		{
			GetFrame(this.Clock.Elapsed).Add(value);
		}

		public RobustHistogram MergeResults(TimeSpan window)
		{
			return MergeResults((int)Math.Max(1, Math.Ceiling((double)window.Ticks / this.Step.Ticks)));
		}

		public RobustHistogram MergeResults(int samples)
		{
			var merged = new RobustHistogram(this.Scale);
			foreach(var histo in this.Histos.Reverse<RobustHistogram>().Take(samples))
			{
				merged.Merge(histo);
			}
			return merged;
		}

		public RobustHistogram MergeResults()
		{
			return MergeResults(this.Histos.Count);
		}

	}

}
