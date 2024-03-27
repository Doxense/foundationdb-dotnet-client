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

namespace FoundationDB.Client
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Linq;
	using Doxense.Serialization.Json;
	using JetBrains.Annotations;

	[PublicAPI]
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

			private static FdbTenantMetadata ParseTenantMetadata(FdbTenantName name, Slice data)
			{
				Contract.Debug.Requires(data.Count > 0);

				// as of version 7.2, this is a JSON document that looks like { "id":xxx, "prefix": "\u0000\u0000\u000...\u0042" }
				// The current implementation assign each tenant a new unique sequential integer id, and it's prefix will be this id encoded as 64-bit big-endian.
				// note: the prefix is encoded as a Unicode String, with one "code point" per byte (???) which is weird (it should probably have been encoded as Base64 ??)
				//       this means that "\u0001\u0002\u0003\u0004\u0005\u0006\u0007\u0008" represents the prefix '\x01\x02\x03\x04\x05\x06\x07\x08` or <00 01 02 03 04 05 06 07 08>

				var obj = CrystalJson.Parse(data).AsObjectOrDefault();
				if (obj == null) throw new FormatException("Invalid Tenant Metadata format: required JSON document is missing.");

				var id = obj.Get<int?>("id", null);
				if (id == null) throw new FormatException("Invalid Tenant Metadata format: required 'id' field is missing.");

				var prefixLiteral = obj.Get<string?>("prefix", null);
				if (prefixLiteral == null) throw new FormatException("Invalid Tenant Metadata format: required 'prefix' field is missing.");

				// the prefix is encoded _as a string_ in the JSON which is NOT a good idea :(
				// each character is a unicode value from 0 to FF which corresponds to one byte of the prefix
				var tmp = new byte[prefixLiteral.Length];
				for (int i = 0; i < prefixLiteral.Length; i++)
				{
					tmp[i] = (byte) prefixLiteral[i];
				}
				var prefix = tmp.AsSlice();

				return new FdbTenantMetadata()
				{
					Name = name,
					Id = id.Value,
					Prefix = prefix,
					Range = KeyRange.StartsWith(prefix),
					Raw = data,
				};
			}

			public static async Task<FdbTenantMetadata?> GetTenantMetadata(IFdbReadOnlyTransaction tr, FdbTenantName name)
			{
				var data = await tr.GetAsync(TenantMapPrefix + name.Value);
				return data.HasValue ? ParseTenantMetadata(name, data) : null;
			}

			public static void DeleteTenant(IFdbTransaction tr, FdbTenantName name)
			{
				Contract.NotNull(tr);

				// just deleting the key in the tenant module will trigger the deletion of the teant, once the transaction commits
				tr.Options.WithSpecialKeySpaceEnableWrites();
				tr.Clear(TenantMapPrefix + name.Value);
			}

			public static IAsyncEnumerable<FdbTenantMetadata> QueryTenants(IFdbReadOnlyTransaction tr, Slice? prefix = null, Func<Slice, bool>? filter = null)
			{
				Contract.NotNull(tr);
				return tr
			       // get all keys in the "/management/tenant_map/...." table
			       .GetRange(prefix == null ? TenantMapRange : KeyRange.StartsWith(TenantMapPrefix + prefix.Value))
			       // remove the table prefix to get only the name
			       .Select(kv => (Name: kv.Key.Substring(TenantMapPrefix.Count), Data: kv.Value))
			       // optional filtering on the names
			       .Where(kv => filter == null || filter(kv.Name))
			       // parse the name and the metadata
			       .Select(kv => ParseTenantMetadata(FdbTenantName.Create(kv.Name), kv.Data));
			}
		}
	}

}
