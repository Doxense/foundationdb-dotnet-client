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

namespace Doxense.Networking.Http
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Net.Http.Headers;

	/// <summary>Helper to configure default headers for <see cref="BetterHttpClient">HTTP clients</see></summary>
	public sealed class BetterDefaultHeaders
	{

		/// <summary>Add a custom default HTTP header</summary>
		/// <param name="name">Name of the header</param>
		/// <param name="value">Value of the header</param>
		public void Add(string name, string value) => this.Extra.Add(new KeyValuePair<string, IEnumerable<string>>(name, new [] { value }));

		/// <summary>Add a custom default HTTP header with multiple values</summary>
		/// <param name="name">Name of the header</param>
		/// <param name="values">Values of the header</param>
		public void Add(string name, IEnumerable<string> values) => this.Extra.Add(new KeyValuePair<string, IEnumerable<string>>(name, values as string[] ?? values.ToArray()));

		public IList<MediaTypeWithQualityHeaderValue>? Accept { get; set; }

		public IList<StringWithQualityHeaderValue>? AcceptCharset { get; set; }

		public IList<StringWithQualityHeaderValue>? AcceptEncoding { get; set; }

		public IList<StringWithQualityHeaderValue>? AcceptLanguage { get; set; }

		public AuthenticationHeaderValue? Authorization { get; set; }

		public CacheControlHeaderValue? CacheControl { get; set; }

		public bool? ConnectionClose { get; set; }

		public IList<ProductInfoHeaderValue>? UserAgent { get; set; }

		public IList<ViaHeaderValue>? Via { get; set; }

		//TODO: ajouter ce qu'il manque!!!

		public List<KeyValuePair<string, IEnumerable<string>>> Extra { get; set; } = new List<KeyValuePair<string, IEnumerable<string>>>();

		public Uri? Referrer { get; set; }

		private static void AddRange<TValue>(IList<TValue> items, HttpHeaderValueCollection<TValue> collection)
			where TValue : class
		{
			foreach(var item in items) collection.Add(item);
		}

		public void Apply(HttpRequestHeaders headers)
		{
			if (this.Accept != null) AddRange(this.Accept, headers.Accept);
			if (this.AcceptCharset != null) AddRange(this.AcceptCharset, headers.AcceptCharset);
			if (this.AcceptEncoding != null) AddRange(this.AcceptEncoding, headers.AcceptEncoding);
			if (this.AcceptLanguage != null) AddRange(this.AcceptLanguage, headers.AcceptLanguage);
			if (this.Authorization != null) headers.Authorization = this.Authorization;
			if (this.CacheControl != null) headers.CacheControl = this.CacheControl;
			if (this.ConnectionClose != null) headers.ConnectionClose = this.ConnectionClose;
			if (this.UserAgent != null) AddRange(this.UserAgent, headers.UserAgent);
			if (this.Referrer != null) headers.Referrer = this.Referrer;
			if (this.Via != null) AddRange(this.Via, headers.Via);

			foreach (var kv in this.Extra)
			{
				headers.Add(kv.Key, kv.Value);
			}
		}

	}

}
