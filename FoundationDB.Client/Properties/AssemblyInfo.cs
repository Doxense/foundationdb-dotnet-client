#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

[assembly: ComVisible(false)]

[assembly: Guid("0fce138d-cb61-49fd-bb0a-a0ecb37abe78")]

[assembly: InternalsVisibleTo("FoundationDB.Layers.Common, PublicKey=0024000004800000940000000602000000240000525341310004000001000100a9e653303024d91e3e98cdb33228897aebc9aeb0dd5e0890a2362ff08231643525d86e955d52a9be450a9602eedbc1c0eb463d227320a6b6ad1c7129f21353b2b28242d712a0e7b3aaff55c0ab1019c92bea6806b9cf64e93d976143dc57e0a8e73a65c03422ab2624c1220d84f7e88c5a5c3c9edefcf4a76969d458348403ce")]
[assembly: InternalsVisibleTo("FoundationDB.Layers.Experimental, PublicKey=0024000004800000940000000602000000240000525341310004000001000100a9e653303024d91e3e98cdb33228897aebc9aeb0dd5e0890a2362ff08231643525d86e955d52a9be450a9602eedbc1c0eb463d227320a6b6ad1c7129f21353b2b28242d712a0e7b3aaff55c0ab1019c92bea6806b9cf64e93d976143dc57e0a8e73a65c03422ab2624c1220d84f7e88c5a5c3c9edefcf4a76969d458348403ce")]
[assembly: InternalsVisibleTo("FoundationDB.Linq.Providers, PublicKey=0024000004800000940000000602000000240000525341310004000001000100a9e653303024d91e3e98cdb33228897aebc9aeb0dd5e0890a2362ff08231643525d86e955d52a9be450a9602eedbc1c0eb463d227320a6b6ad1c7129f21353b2b28242d712a0e7b3aaff55c0ab1019c92bea6806b9cf64e93d976143dc57e0a8e73a65c03422ab2624c1220d84f7e88c5a5c3c9edefcf4a76969d458348403ce")]
[assembly: InternalsVisibleTo("FoundationDB.Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100a9e653303024d91e3e98cdb33228897aebc9aeb0dd5e0890a2362ff08231643525d86e955d52a9be450a9602eedbc1c0eb463d227320a6b6ad1c7129f21353b2b28242d712a0e7b3aaff55c0ab1019c92bea6806b9cf64e93d976143dc57e0a8e73a65c03422ab2624c1220d84f7e88c5a5c3c9edefcf4a76969d458348403ce")]

// we exclude this assembly from test coverage by default when it is being consumed as a NuGet package
#if RELEASE
[assembly: System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
#endif
