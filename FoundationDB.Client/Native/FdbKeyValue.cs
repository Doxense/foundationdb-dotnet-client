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

namespace FoundationDB.Client.Native
{
	using System;
	using System.Runtime.InteropServices;

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	internal unsafe struct FdbKeyValue
	{
		public byte* Key;
		public uint KeyLength;
		public byte* Value;
		public uint ValueLength;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	internal unsafe struct FdbKey
	{
		public byte* Key;
		public uint Length;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	internal unsafe struct FdbKeyRange
	{
		public byte* BeginKey;
		public uint BeginKeyLength;
		public byte* EndKey;
		public uint EndKeyLength;

	}

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	internal ref struct FdbMappedKeyValue
	{
		public FdbKey Key;
		public FdbKey Value;
		public FdbGetRangeReqAndResult GetRange;
		public byte Buffer; // note: this is a byte[32] !
	}

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	internal unsafe struct FdbGetRangeReqAndResult
	{
		public FdbKeySelector Begin;
		public FdbKeySelector End;
		public FdbKeyValue* Data;
		public int Size;
		public int Capacity;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	internal struct FdbKeySelector
	{
		public FdbKey Key;
		public bool OrEqual;
		public int Offset;
	}

}
