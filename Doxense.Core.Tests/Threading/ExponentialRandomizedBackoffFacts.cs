#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Threading.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using NUnit.Framework;
	using SnowBank.Testing;

	[TestFixture]
	public class ExponentialRandomizedBackoffFacts : SimpleTest
	{

		[Test]
		public void Test_Basics()
		{
			// Test that the delay will ramp-up until the maximum value

			var backoff = new ExponentialRandomizedBackoff(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));

			Assert.That(backoff.Initial, Is.EqualTo(TimeSpan.FromSeconds(1)), ".Initial");
			Assert.That(backoff.Maximum, Is.EqualTo(TimeSpan.FromSeconds(10)), ".Maximum");
			Assert.That(backoff.BackoffFactor, Is.EqualTo(2), ".BackoffFactor");
			Assert.That(backoff.RandomLow, Is.EqualTo(1.0), ".RandomLow");
			Assert.That(backoff.RandomHigh, Is.EqualTo(1.5), ".RandomHigh");
			Assert.That(backoff.Last, Is.EqualTo(TimeSpan.Zero), ".Last");
			Assert.That(backoff.Iterations, Is.EqualTo(0), ".Iterations");
		}

		[Test]
		public void Test_Does_Randomize_Delay()
		{
			// test that the randomized delay is indeed randomly and evenly spread over the specified range

			const int ITERATIONS = 1000;

			var rnd = new Random();

			var backoff = new ExponentialRandomizedBackoff(
				TimeSpan.FromSeconds(1),
				TimeSpan.FromSeconds(10),
				backoffFactor: 1, // in this test we lock the delay
				randomLow: 1,
				randomHigh: 1.5,
				rng: rnd);

			// sample the delay, and compute some statistics (avg, stdev, ..)

			TimeSpan prev = default;
			var samples = new List<double>(ITERATIONS);
			for(int i = 0; i < ITERATIONS; i++)
			{
				var delay = backoff.GetNext();

				// in theory, delay should NOT be the same as the previous one, but since it is random, it _could_ happen.
				if (delay == prev)
				{
					// => if this happens, we will get another delay, and hope that the probability of it happening twice in a row by chance is small enough that it will not happen during CI!
					delay = backoff.GetNext();
				}

				samples.Add(delay.TotalSeconds);

				Assert.That(delay, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(1)), "Delay must not be lower than RandomLow!");
				Assert.That(delay, Is.LessThanOrEqualTo(TimeSpan.FromSeconds(1.5)), "Delay must not be higher than RandomHigh!");
				Assert.That(delay, Is.Not.EqualTo(prev), "Delay should be randomized and not the same as previous iteration!");
			}

			var avg = samples.Average();
			var min = samples.Min();
			var max = samples.Max();
			var std = Math.Sqrt(samples.Sum(x => (x - avg) * (x - avg)) / (samples.Count - 1));

			// typical values should be avg ~= 1.25 and std ~= 0.15
			Log($"> Min = {min:N3} s");
			Log($"> Max = {max:N3} s");
			Log($"> Avg = {avg:N3} s");
			Log($"> Std = {std:N3} s");

			Assert.That(min, Is.GreaterThanOrEqualTo(1).And.LessThan(1.25), "Mininimum (>= 1)");
			Assert.That(max, Is.LessThanOrEqualTo(1.5).And.GreaterThan(1.25), "Maximum (<= 1.5)");
			Assert.That(avg, Is.EqualTo(1.25).Within(0.02), "Average (should be ~ 1.25)");
			Assert.That(std, Is.EqualTo(0.144).Within(0.02), "Standard Deviation (should be ~ 0.144)");
		}

		[Test]
		public void Test_Does_Not_Exceed_Maximum()
		{
			// Test that the delay will ramp-up until the maximum value

			var rnd = new Random(5234687);

			var backoff = new ExponentialRandomizedBackoff(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10), randomLow: 1, randomHigh: 1, rng: rnd);

			Log("Ticking...");

			{ // 1
				var delay = backoff.GetNext();
				Log($"> {delay}");
				Assert.That(delay, Is.EqualTo(TimeSpan.FromSeconds(1)), ".GetNext()");
				Assert.That(backoff.Last, Is.EqualTo(delay), ".Last");
				Assert.That(backoff.Iterations, Is.EqualTo(1), ".Iterations");
			}

			{ // 2
				var delay = backoff.GetNext();
				Log($"> {delay}");
				Assert.That(delay, Is.EqualTo(TimeSpan.FromSeconds(2)), ".GetNext()");
				Assert.That(backoff.Last, Is.EqualTo(delay), ".Last");
				Assert.That(backoff.Iterations, Is.EqualTo(2), ".Iterations");
			}

			{ // 4
				var delay = backoff.GetNext();
				Log($"> {delay}");
				Assert.That(delay, Is.EqualTo(TimeSpan.FromSeconds(4)), ".GetNext()");
				Assert.That(backoff.Last, Is.EqualTo(delay), ".Last");
				Assert.That(backoff.Iterations, Is.EqualTo(3), ".Iterations");
			}

			{ // 8
				var delay = backoff.GetNext();
				Log($"> {delay}");
				Assert.That(delay, Is.EqualTo(TimeSpan.FromSeconds(8)), ".GetNext()");
				Assert.That(backoff.Last, Is.EqualTo(delay), ".Last");
				Assert.That(backoff.Iterations, Is.EqualTo(4), ".Iterations");
			}

			{ // 16 => 10
				var delay = backoff.GetNext();
				Log($"> {delay}");
				Assert.That(delay, Is.EqualTo(TimeSpan.FromSeconds(10)), ".GetNext()");
				Assert.That(backoff.Last, Is.EqualTo(delay), ".Last");
				Assert.That(backoff.Iterations, Is.EqualTo(5), ".Iterations");
			}

			{ // 10 => 10
				var delay = backoff.GetNext();
				Log($"> {delay}");
				Assert.That(delay, Is.EqualTo(TimeSpan.FromSeconds(10)), ".GetNext()");
				Assert.That(backoff.Last, Is.EqualTo(delay), ".Last");
				Assert.That(backoff.Iterations, Is.EqualTo(6), ".Iterations");
			}

			Log("Reset...");
			backoff.Reset();
			Assert.That(backoff.Iterations, Is.EqualTo(0), ".Iterations");

			{ // => 1
				var delay = backoff.GetNext();
				Log($"> {delay}");
				Assert.That(delay, Is.EqualTo(TimeSpan.FromSeconds(1)), ".GetNext()");
				Assert.That(backoff.Last, Is.EqualTo(delay), ".Last");
				Assert.That(backoff.Iterations, Is.EqualTo(1), ".Iterations");
			}

		}

	}

}
