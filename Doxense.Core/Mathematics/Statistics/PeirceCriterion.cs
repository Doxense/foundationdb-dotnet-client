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

namespace Doxense.Mathematics.Statistics
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	public static class PeirceCriterion
	{
		public static readonly double[][] Tables = new double[][]
		{
			new [] { 1.196 },
			new [] { 1.383, 1.078 },
			new [] { 1.509, 1.200 },
			new [] { 1.610, 1.299, 1.099 },
			new [] { 1.693, 1.382, 1.187, 1.022 },
			new [] { 1.763, 1.453, 1.261, 1.109 },
			new [] { 1.824, 1.515, 1.324, 1.178, 1.045 },
			new [] { 1.878, 1.570, 1.380, 1.237, 1.114 },
			new [] { 1.925, 1.619, 1.430, 1.289, 1.172, 1.059 },
			new [] { 1.969, 1.663, 1.475, 1.336, 1.221, 1.118, 1.009 },
			new [] { 2.007, 1.704, 1.516, 1.379, 1.266, 1.167, 1.070 },
			new [] { 2.043, 1.741, 1.554, 1.417, 1.307, 1.210, 1.120, 1.026 },
			new [] { 2.076, 1.775, 1.589, 1.453, 1.344, 1.249, 1.164, 1.078 },
			new [] { 2.106, 1.807, 1.622, 1.486, 1.378, 1.285, 1.202, 1.122, 1.039 },
			new [] { 2.134, 1.836, 1.652, 1.517, 1.409, 1.318, 1.237, 1.161, 1.084 },
			new [] { 2.161, 1.864, 1.680, 1.546, 1.438, 1.348, 1.268, 1.195, 1.123 },
			new [] { 2.185, 1.890, 1.707, 1.573, 1.466, 1.377, 1.298, 1.226, 1.158 },
			new [] { 2.209, 1.914, 1.732, 1.599, 1.492, 1.404, 1.326, 1.255, 1.190 },
			new [] { 2.230,  1.938,  1.756,  1.623,  1.517,  1.429,  1.352,  1.282,  1.218 }, 
			new [] { 2.251,  1.960,  1.779,  1.646,  1.540,  1.452,  1.376,  1.308,  1.245  },
			new [] { 2.271,  1.981,  1.800,  1.668,  1.563,  1.475,  1.399,  1.332,  1.270  },
			new [] { 2.290,  2.000,  1.821,  1.689,  1.584,  1.497,  1.421,  1.354,  1.293  },
			new [] { 2.307,  2.019,  1.840,  1.709,  1.604,  1.517,  1.442,  1.375,  1.315  },
			new [] { 2.324,  2.037,  1.859,  1.728,  1.624,  1.537,  1.462,  1.396,  1.336  },
			new [] { 2.341,  2.055,  1.877,  1.746,  1.642,  1.556,  1.481,  1.415,  1.356  },
			new [] { 2.356,  2.071,  1.894,  1.764,  1.660,  1.574,  1.500,  1.434,  1.375  },
			new [] { 2.371,  2.088,  1.911,  1.781,  1.677,  1.591,  1.517,  1.452,  1.393  },
			new [] { 2.385,  2.103,  1.927,  1.797,  1.694,  1.608,  1.534,  1.469 , 1.411  },
			new [] { 2.399,  2.118,  1.942,  1.812,  1.710,  1.624,  1.550,  1.486 , 1.428  },
			new [] { 2.412,  2.132,  1.957,  1.828,  1.725,  1.640,  1.567,  1.502 , 1.444  },
			new [] { 2.425,  2.146,  1.971,  1.842,  1.740,  1.655,  1.582,  1.517 , 1.459  },
			new [] { 2.438,  2.159,  1.985,  1.856,  1.754,  1.669,  1.597,  1.532,  1.475  },
			new [] { 2.450,  2.172,  1.998,  1.870,  1.768,  1.683,  1.611,  1.547,  1.489  },
			new [] { 2.461,  2.184,  2.011,  1.883,  1.782,  1.697,  1.624,  1.561,  1.504  },
			new [] { 2.472,  2.196,  2.024,  1.896,  1.795,  1.711,  1.638,  1.574,  1.517  },
			new [] { 2.483,  2.208,  2.036,  1.909,  1.807,  1.723,  1.651,  1.587,  1.531  },
			new [] { 2.494,  2.219,  2.047,  1.921,  1.820,  1.736,  1.664,  1.600,  1.544  },
			new [] { 2.504,  2.230,  2.059,  1.932,  1.832,  1.748,  1.676,  1.613,  1.556  },
			new [] { 2.514,  2.241,  2.070,  1.944,  1.843,  1.760,  1.688,  1.625,  1.568  },
			new [] { 2.524,  2.251,  2.081,  1.955,  1.855,  1.771,  1.699,  1.636,  1.580  },
			new [] { 2.533,  2.261,  2.092,  1.966,  1.866,  1.783,  1.711,  1.648,  1.592  },
			new [] { 2.542,  2.271,  2.102,  1.976,  1.876,  1.794,  1.722,  1.659,  1.603  },
			new [] { 2.551,  2.281,  2.112,  1.987,  1.887,  1.804,  1.733,  1.670,  1.614  },
			new [] { 2.560,  2.290,  2.122,  1.997,  1.897,  1.815,  1.743,  1.681,  1.625  },
			new [] { 2.568,  2.299,  2.131,  2.006,  1.907,  1.825,  1.754,  1.691,  1.636  },
			new [] { 2.577,  2.308,  2.140,  2.016,  1.917,  1.835,  1.764,  1.701,  1.646  },
			new [] { 2.585,  2.317,  2.149,  2.026,  1.927,  1.844,  1.773,  1.711,  1.656  },
			new [] { 2.592,  2.326,  2.158,  2.035,  1.936,  1.854,  1.783,  1.721,  1.666  },
			new [] { 2.600,  2.334,  2.167,  2.044,  1.945,  1.863,  1.792,  1.730,  1.675  },
			new [] { 2.608,  2.342,  2.175,  2.052,  1.954,  1.872,  1.802,  1.740,  1.685  },
			new [] { 2.615,  2.350,  2.184,  2.061,  1.963,  1.881,  1.811,  1.749,  1.694  },
			new [] { 2.622,  2.358,  2.192,  2.069,  1.972,  1.890,  1.820,  1.758,  1.703  },
			new [] { 2.629,  2.365,  2.200,  2.077,  1.980,  1.898,  1.828,  1.767,  1.711  },
			new [] { 2.636,  2.373,  2.207,  2.085,  1.988,  1.907,  1.837,  1.775,  1.720  },
			new [] { 2.643,  2.380,  2.215,  2.093,  1.996,  1.915,  1.845,  1.784,  1.729  },
			new [] { 2.650,  2.387,  2.223,  2.101,  2.004,  1.923,  1.853,  1.792,  1.737  },
			new [] { 2.656,  2.394,  2.230,  2.109,  2.012,  1.931,  1.861,  1.800,  1.745  },
			new [] { 2.663,  2.401,  2.237,  2.116,  2.019,  1.939,  1.869,  1.808,  1.753  },
		};

		static double PeircesTable(int n, int k)
		{
			if (n < 3) throw new ArgumentOutOfRangeException(nameof(n), n, "Source list must have at least 3 elements");
			if (n > 60) throw new ArgumentOutOfRangeException(nameof(n), n, "Source list must have at most 60 elements");
			if (k < 1 || k > 9) throw new ArgumentOutOfRangeException(nameof(k), k, "Can only remove between 1 and 9 outliers");

			var table = Tables[n - 3];
			if (table.Length <= k - 1) throw new ArgumentOutOfRangeException(nameof(k), k, $"Cannot remove {k} outliers with only {n} elements");

			return table[k - 1];
		}

		public static double StandardDeviation(double[] source, out double mean)
		{
			var avg = mean = source.Average();

			var fromAvg = source.Select(i => Math.Abs((i - avg) * (i - avg)));
			// assume a randomly sampled data set, so divide by count - 1
			// a complete data set should divide by count instead
			var stdDev = Math.Sqrt(fromAvg.Sum() / (source.Length - 1));
			return stdDev;
		}

		public static IEnumerable<T> FilterOutliers<T>(IList<T> source, [InstantHandle] Func<T, double> selector, out IEnumerable<int> outliers, out int eliminated)
		{
			Contract.NotNull(source);
			Contract.NotNull(selector);

			int count = source.Count;
			if (count < 3) throw new ArgumentOutOfRangeException(nameof(source), "Source must have at least 3 elements");
			if (count > 60) throw new ArgumentOutOfRangeException(nameof(source), "Source cannot have more than 60 elements");

			var values = source.Select(selector).ToArray();

			double stdDev = StandardDeviation(values, out double mean);

			int last = 0;
			while(true)
			{
				var r = PeircesTable(count, last + 1);

				var maxDeviation = r * stdDev;
				eliminated = values.Count(v => Math.Abs(v - mean) > maxDeviation);
				if (last >= eliminated)
				{
					outliers = FilterAndApply(values, (v) => Math.Abs(v - mean) > maxDeviation, (j) => j);
					return FilterAndApply(values, (v) => Math.Abs(v - mean) <= maxDeviation, (j) => source[j]);
				}
				last = eliminated;
			}
		}

		/// <summary>Filtre puis transforme les éléments d'une source</summary>
		/// <typeparam name="TSource">Type des éléments sources</typeparam>
		/// <typeparam name="TResult">Type des éléments retournés</typeparam>
		/// <param name="source">Source à convertir</param>
		/// <param name="filter">Fonction qui filtre les éléments source</param>
		/// <param name="selector">Function qui convertit les éléments</param>
		/// <returns>Liste des résultats qui matchent le filtre (AVANT conversion)</returns>
		[LinqTunnel]
		private static IEnumerable<TResult> FilterAndApply<TSource, TResult>(IEnumerable<TSource> source, Func<TSource, bool> filter, Func<int, TResult> selector)
		{
			Contract.NotNull(source);
			Contract.NotNull(filter);
			Contract.NotNull(selector);

			int p = 0;
			foreach (var item in source)
			{
				if (filter(item))
				{
					yield return selector(p);
				}
				++p;
			}
		}

	}

}
