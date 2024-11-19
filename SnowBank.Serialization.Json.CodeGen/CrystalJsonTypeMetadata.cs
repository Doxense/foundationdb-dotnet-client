using System;

namespace Doxense.Serialization.Json.CodeGen
{
	using Microsoft.CodeAnalysis;

	public sealed record CrystalJsonTypeMetadata : IEquatable<CrystalJsonTypeMetadata>
	{
		
		public TypeRef Symbol { get; }

		public string Name => this.Symbol.Name;

		public string NameSpace => this.Symbol.NameSpace;
		
		public AttributeData? Attribute { get; }

		public CrystalJsonTypeMetadata(ITypeSymbol symbol, AttributeData attr)
		{
			this.Symbol = new TypeRef(symbol);
			this.Attribute = attr;
		}

	}
}
