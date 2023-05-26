#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
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
	using System.Collections.Generic;
	using System.Threading.Tasks;

	public enum FdbTenantMode
	{
		Invalid = -1,
		Disabled = 0,
		Optional = 1,
		Required = 2,
	}

	public static partial class Fdb
	{

		public static class Tenants
		{

			/// <summary><c>\xFF\xFF/management/tenant_map/</c></summary>
			private static readonly Slice TenantMapPrefix = Fdb.System.SpecialKey("/management/tenant_map/");

			/// <summary><c>\xFF\xFF/management/tenant_map/...</c></summary>
			private static readonly KeyRange TenantMapRange = KeyRange.Create(TenantMapPrefix, Fdb.System.SpecialKey("/management/tenant_map0"));

			/// <summary><c>\xFF/conf/tenant_mode</c></summary>
			private static readonly Slice TenantModeKey = Fdb.System.ConfigKey("tenant_mode");

			public static async Task<FdbTenantMode> GetTenantMode(IFdbReadOnlyTransaction tr)
			{
				tr.Options.WithReadAccessToSystemKeys();
				var val = await tr.GetAsync(TenantModeKey);
				if (val.IsNullOrEmpty) return FdbTenantMode.Disabled;
				// this is a number represented as a decimal string
				// so "0" is Disabled, "1" is Optional, etc..
				if (!int.TryParse(val.ToString(), out var value))
				{
					return FdbTenantMode.Invalid;
				}

				return (FdbTenantMode) value;
			}

			public static void CreateTenant(IFdbTransaction tr, FdbTenantName name)
			{
				// just setting the key in the tenant module will trigger the creation of the teant, once the transaction commits
				// the tenant module will do the prefix allocation and generate the new tenant entry in the cluster.
				tr.Options.WithSpecialKeySpaceEnableWrites();
				tr.Set(TenantMapPrefix + name.Value, Slice.Empty);
				//note: if the tenant already exists, this is a "no-op"
			}

			public static async Task<bool> HasTenant(IFdbReadOnlyTransaction tr, FdbTenantName name)
			{
				var value = await tr.GetAsync(TenantMapPrefix + name.Value);
				return value.HasValue;
			}

			public static Task<Slice> GetTenantMetadata(IFdbReadOnlyTransaction tr, FdbTenantName name)
			{
				return tr.GetAsync(TenantMapPrefix + name.Value);
			}

			public static void DeleteTenant(IFdbTransaction tr, FdbTenantName name)
			{
				// just deleting the key in the tenant module will trigger the deletion of the teant, once the transaction commits
				tr.Options.WithSpecialKeySpaceEnableWrites();
				tr.Clear(TenantMapPrefix + name.Value);
			}

			public static async Task<List<(FdbTenantName Name, Slice Metadata)>> ListTenants(IFdbReadOnlyTransaction tr)
			{
				return await tr.GetRange(TenantMapRange).Select(kv => (new FdbTenantName(kv.Key.Substring(TenantMapPrefix.Count)), kv.Value)).ToListAsync();
			}
		}
	}

}
