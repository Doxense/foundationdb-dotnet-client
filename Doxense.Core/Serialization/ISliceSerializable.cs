#region Copyright Doxense 2015-2019
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization
{
	using System;
	using System.Diagnostics.CodeAnalysis;
	using Doxense.Memory;

	/// <summary>Représente la capacité de se sérialiser de manière binaire</summary>
	public interface ISliceSerializable
	{
		void WriteTo(ref SliceWriter writer);
	}

	public interface ISliceSerializer<T>
	{
		void WriteTo(ref SliceWriter writer, T? value);

		bool TryReadFrom(ref SliceReader reader, out T? value);
	}

}
