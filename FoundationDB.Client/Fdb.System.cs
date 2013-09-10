#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

namespace FoundationDB.Client
{
	using System;

	public static partial class Fdb
	{

		internal static class SystemKeys
		{
			/// <summary>"\xFF\xFF"</summary>
			public static readonly Slice MaxValue = Slice.FromAscii("\xFF\xFF");

			/// <summary>"\xFF\xFF"</summary>
			public static readonly Slice MinValue = Slice.FromAscii("\xFF\x00");

			/// <summary>"\xFF/conf/..."</summary>
			public static readonly Slice ConfigPrefix = Slice.FromAscii("\xFF/conf/");

			/// <summary>"\xFF/coordinators"</summary>
			public static readonly Slice Coordinators = Slice.FromAscii("\xFF/coordinators");

			/// <summary>"\xFF/keyServer/(key_boundary)" => (..., node_id, ...)</summary>
			public static readonly Slice KeyServers = Slice.FromAscii("\xFF/keyServers/");

			/// <summary>"\xFF/serverKeys/(node_id)/(key_boundary)" => ('' | '1')</summary>
			public static readonly Slice ServerKeys = Slice.FromAscii("\xFF/serverKeys/");

			/// <summary>"\xFF/serverList/(node_id)" => (..., node_id, machine_id, datacenter_id, ...)</summary>
			public static readonly Slice ServerList = Slice.FromAscii("\xFF/serverList/");

			/// <summary>"\xFF/workers/(ip:port)/..." => datacenter + machine + mclass</summary>
			public static readonly Slice Workers = Slice.FromAscii("\xFF/workers/");

			/// <summary>Return the corresponding key for a config attribute</summary>
			/// <param name="name">"foo"</param>
			/// <returns>"\xFF/config/foo"</returns>
			public static Slice GetConfigKey(string name)
			{
				if (string.IsNullOrEmpty(name)) throw new ArgumentException("Config key cannot be null or empty", "name");
				return ConfigPrefix.Concat(Slice.FromAscii(name));
			}

		}

	}

}
