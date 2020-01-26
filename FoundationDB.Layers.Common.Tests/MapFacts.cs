#region BSD License
/* Copyright (c) 2013-2018, Doxense SAS
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

namespace FoundationDB.Layers.Collections.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Net;
	using System.Threading.Tasks;
	using Doxense.Collections.Tuples;
	using Doxense.Serialization.Encoders;
	using FoundationDB.Client;
	using FoundationDB.Client.Tests;
	using NUnit.Framework;

	[TestFixture]
	public class MapFacts : FdbTest
	{

		[Test]
		public async Task Test_FdbMap_Read_Write_Delete()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Root["Collections"]["Maps"];
				await CleanLocation(db, location);

				var mapFoos = new FdbMap<string, string>(location.ByKey("Foos"), BinaryEncoding.StringEncoder);

				string secret = "world:" + Guid.NewGuid().ToString();

				// read non existing value
				await db.WriteAsync(async tr =>
				{
					var foos = await mapFoos.Resolve(tr);

					Assert.That(async () => await foos.GetAsync(tr, "hello"), Throws.InstanceOf<KeyNotFoundException>());

					var value = await foos.TryGetAsync(tr, "hello");
					Assert.That(value.HasValue, Is.False);
					Assert.That(value.Value, Is.Null);
				}, this.Cancellation);

				// write value
				await db.WriteAsync(async tr =>
				{
					var foos = await mapFoos.Resolve(tr);
					foos.Set(tr, "hello", secret);
				}, this.Cancellation);

#if DEBUG
				await DumpSubspace(db, location);
#endif

				// read value back
				await db.ReadAsync(async tr =>
				{
					var foos = await mapFoos.Resolve(tr);

					var value = await foos.GetAsync(tr, "hello");
					Assert.That(value, Is.EqualTo(secret));

					var opt = await foos.TryGetAsync(tr, "hello");
					Assert.That(opt.HasValue, Is.True);
					Assert.That(opt.Value, Is.EqualTo(secret));
				}, this.Cancellation);

				// directly read the value, behind the table's back
				await db.ReadAsync(async tr =>
				{
					var folder = await location.Resolve(tr);

					var value = await tr.GetAsync(folder.Encode("Foos", "hello"));
					Assert.That(value, Is.Not.EqualTo(Slice.Nil));
					Assert.That(value.ToString(), Is.EqualTo(secret));
				}, this.Cancellation);

				// delete the value
				await db.WriteAsync(async tr =>
				{
					var foos = await mapFoos.Resolve(tr);
					foos.Remove(tr, "hello");
				}, this.Cancellation);

#if DEBUG
				await DumpSubspace(db, location);
#endif

				// verifiy that it is gone
				await db.ReadAsync(async tr =>
				{
					var foos = await mapFoos.Resolve(tr);

					Assert.That(async () => await foos.GetAsync(tr, "hello"), Throws.InstanceOf<KeyNotFoundException>());

					var value = await foos.TryGetAsync(tr, "hello");
					Assert.That(value.HasValue, Is.False);

					// also check directly
					var folder = await location.Resolve(tr);
					var data = await tr.GetAsync(folder.Encode("Foos", "hello"));
					Assert.That(data, Is.EqualTo(Slice.Nil));
				}, this.Cancellation);

			}

		}

		[Test]
		public async Task Test_FdbMap_List()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Root["Collections"]["Maps"];
				await CleanLocation(db, location);

				var mapFoos = new FdbMap<string, string>(location.ByKey("Foos"), BinaryEncoding.StringEncoder);

				// write a bunch of keys
				await db.WriteAsync(async (tr) =>
				{
					var foos = await mapFoos.Resolve(tr);
					foos.Set(tr, "foo", "foo_value");
					foos.Set(tr, "bar", "bar_value");
				}, this.Cancellation);

#if DEBUG
				await DumpSubspace(db, location);
#endif

				// read them back

				await db.ReadAsync(async tr =>
				{
					var foos = await mapFoos.Resolve(tr);

					var value = await foos.GetAsync(tr, "foo");
					Assert.That(value, Is.EqualTo("foo_value"));

					value = await foos.GetAsync(tr, "bar");
					Assert.That(value, Is.EqualTo("bar_value"));

					Assert.That(async () => await foos.GetAsync(tr, "baz"), Throws.InstanceOf<KeyNotFoundException>());

					var opt = await foos.TryGetAsync(tr, "baz");
					Assert.That(opt.HasValue, Is.False);
				}, this.Cancellation);

			}
		}

		[Test]
		public async Task Test_FdbMap_With_Custom_Key_Encoder()
		{
			// Use a table as a backing store for the rules of a Poor Man's firewall, where each keys are the IPEndPoint (tcp only!), and the values are "pass" or "block"

			// Encode IPEndPoint as the (IP, Port,) encoded with the Tuple codec
			// note: there is a much simpler way or creating composite keys, this is just a quick and dirty test!
			var keyEncoder = new KeyEncoder<IPEndPoint>(
				(ipe) => ipe == null ? Slice.Empty : TuPack.EncodeKey(ipe.Address, ipe.Port),
				(packed) =>
				{
					if (packed.IsNullOrEmpty) return default(IPEndPoint);
					var t = TuPack.Unpack(packed);
					return new IPEndPoint(t.Get<IPAddress>(0), t.Get<int>(1));
				}
			);

			var rules = new Dictionary<IPEndPoint, string>()
			{
				{ new IPEndPoint(IPAddress.Parse("172.16.12.34"), 6667), "block" },
				{ new IPEndPoint(IPAddress.Parse("192.168.34.56"), 80), "pass" },
				{ new IPEndPoint(IPAddress.Parse("192.168.34.56"), 443), "pass" }
			};

			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Root["Collections"]["Maps"];
				await CleanLocation(db, location);

				var mapHosts = new FdbMap<IPEndPoint, string>(location.ByKey("Hosts").AsTyped<IPEndPoint>(keyEncoder), BinaryEncoding.StringEncoder);

				// import all the rules
				await db.WriteAsync(async (tr) =>
				{
					var hosts = await mapHosts.Resolve(tr);
					foreach(var rule in rules)
					{
						hosts.Set(tr, rule.Key, rule.Value);
					}
				}, this.Cancellation);

#if DEBUG
				await DumpSubspace(db, location);
#endif

				// test the rules

				await db.ReadAsync(async tr =>
				{
					var hosts = await mapHosts.Resolve(tr);

					var value = await hosts.GetAsync(tr, new IPEndPoint(IPAddress.Parse("172.16.12.34"), 6667));
					Assert.That(value, Is.EqualTo("block"));

					value = await hosts.GetAsync(tr, new IPEndPoint(IPAddress.Parse("192.168.34.56"), 443));
					Assert.That(value, Is.EqualTo("pass"));

					var baz = new IPEndPoint(IPAddress.Parse("172.16.12.34"), 80);
					Assert.That(async () => await hosts.GetAsync(tr, baz), Throws.InstanceOf<KeyNotFoundException>());

					var opt = await hosts.TryGetAsync(tr, baz);
					Assert.That(opt.HasValue, Is.False);
				}, this.Cancellation);

			}
		}

	}


}
