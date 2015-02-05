using System;
using JetBrains.Annotations;
using System.Runtime.CompilerServices;

namespace FoundationDB.Client //REVIEW: what namespace?
{

	public static class TypeSystem
	{
		public static readonly IFdbTypeSystem Default;
		public static readonly IFdbTypeSystem Tuples;

		static TypeSystem()
		{
			var tuples = new TupleTypeSystem();
			Tuples = tuples;

			Default = tuples;
		}

		public static IFdbTypeSystem FromName(string name)
		{
			//TODO: use a dictionary!
			switch (name)
			{
				case "tuples": return Tuples;
			}

			throw new InvalidOperationException("Type System '{0}' is not known. You must register a typesystem by calling TypeSystem.Register() in your initialization logic or in a static constructor, before using it.");
		}

	}

}