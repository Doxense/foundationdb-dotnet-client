using FoundationDB.Layers.Tuples;
using System;
using System.Runtime.CompilerServices;

namespace FoundationDB.Client
{
	public static class Zobi
	{
		public static void Zoba()
		{
			var ts = FdbSubspace.CreateDynamic(FdbTuple.Create(42), TypeSystem.Tuples);

            var s0 = ts.Keys.Pack(FdbTuple.Create("hello", "world", 123, true));
			Console.WriteLine(s0);
			var t0 = ts.Keys.Unpack(s0);
			Console.WriteLine(t0);

			var s1 = ts.Keys.Encode("hello");
			Console.WriteLine(s1);
			var t1 = ts.Keys.Decode<string>(s1);
			Console.WriteLine(t1);

			var s2 = ts.Keys.Encode("hello", 123);
			Console.WriteLine(s2);
			var t2 = ts.Keys.Decode<string, int>(s2);
			Console.WriteLine(t2);
		}
	}


}