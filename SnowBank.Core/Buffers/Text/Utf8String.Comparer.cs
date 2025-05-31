// adapted from https://github.com/dotnet/corefxlab/tree/master/src/System.Text.Utf8

namespace SnowBank.Buffers.Text
{
	using SnowBank.Text;

	public partial struct Utf8String
	{

		/// <summary>Implementation that compares UTF-8 encoded strings using ordinal sort rules.</summary>
		public sealed class Comparer : IEqualityComparer<Utf8String>
		{

			/// <summary>Compare strings using the <see cref="StringComparison.Ordinal"/> sort rules.</summary>
			public static readonly IEqualityComparer<Utf8String> Ordinal = new Comparer();

			/// <summary>Compare strings using the <see cref="StringComparison.OrdinalIgnoreCase"/> sort rules and ignoring the case of the strings being compared.</summary>
			public static readonly IEqualityComparer<Utf8String> OrdinalIgnoreCase = new IgnoreCaseComparer();

			private Comparer() { }

			/// <inheritdoc />
			public bool Equals(Utf8String x, Utf8String y)
			{
				return x.Equals(y);
			}

			/// <inheritdoc />
			public int GetHashCode(Utf8String obj)
			{
				return obj.GetHashCode();
			}

		}

		/// <summary>Implementation that compares UTF-8 encoded strings using case-insensitive ordinal sort rules.</summary>
		private sealed class IgnoreCaseComparer : IEqualityComparer<Utf8String>
		{

			/// <inheritdoc />
			public bool Equals(Utf8String x, Utf8String y)
			{
				if (x.Length != y.Length) return false;
				using (var itx = x.GetEnumerator())
				using (var ity = y.GetEnumerator())
				{
					while (itx.MoveNext())
					{
						if (!ity.MoveNext()) throw new InvalidOperationException();
						if (itx.Current.ToLower() != ity.Current.ToLower())
							return false;
					}
					return true;
				}
			}

			/// <inheritdoc />
			public int GetHashCode(Utf8String obj)
			{
				uint h = 0;
				foreach(var cp in obj)
				{
					h = UnicodeCodePoint.ContinueHashCode(h, cp.ToLower());
				}
				return UnicodeCodePoint.CompleteHashCode(h, obj.Length);
			}

		}

	}

}
