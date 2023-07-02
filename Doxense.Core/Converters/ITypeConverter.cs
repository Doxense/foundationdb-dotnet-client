﻿#region Copyright (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of Doxense nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL DOXENSE BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

#if !USE_SHARED_FRAMEWORK

namespace Doxense.Runtime.Converters
{
	using System;
	using System.Diagnostics.CodeAnalysis;
	using JetBrains.Annotations;

	/// <summary>Base class of all value converters</summary>
	public interface ITypeConverter
	{
		/// <summary>Type of the instance to be converted</summary>
		Type Source { get; }

		/// <summary>Type of the result of the conversion</summary>
		Type Destination { get; }

		[Pure]
		object? ConvertBoxed(object? value);
	}

	/// <summary>Class that can convert values of type <typeparamref name="TSource"/> into values of type <typeparamref name="TDestination"/></summary>
	/// <typeparam name="TSource">Source type</typeparam>
	/// <typeparam name="TDestination">Destination type</typeparam>
	public interface ITypeConverter<in TSource, out TDestination> : ITypeConverter
	{
		/// <summary>Converts a <typeparamref name="TSource"/> into a <typeparamref name="TDestination"/></summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Converted value</returns>
		[Pure]
		TDestination Convert(TSource value);
	}

}

#endif
