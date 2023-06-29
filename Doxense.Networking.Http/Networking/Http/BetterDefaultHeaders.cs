#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
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
