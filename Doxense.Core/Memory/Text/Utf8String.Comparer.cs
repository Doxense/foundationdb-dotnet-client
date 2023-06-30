// adapted from https://github.com/dotnet/corefxlab/tree/master/src/System.Text.Utf8

namespace Doxense.Memory.Text
{
	using System;
	using System.Collections.Generic;
	using Doxense.Text;

	public partial struct Utf8String
	{

		public sealed class Comparer : IEqualityComparer<Utf8String>
		{

			public static readonly IEqualityComparer<Utf8String> Ordinal = new Comparer();
			public static readonly IEqualityComparer<Utf8String> OrdinalIgnoreCase = new IgnoreCaseComparer();

			private Comparer() { }


			public bool Equals(Utf8String x, Utf8String y)
			{
				return x.Equals(y);
			}

			public int GetHashCode(Utf8String obj)
			{
				return obj.GetHashCode();
			}
		}

		private sealed class IgnoreCaseComparer : IEqualityComparer<Utf8String>
		{
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
