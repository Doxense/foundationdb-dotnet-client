#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.Core
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;

	/// <summary>Generic implementation of an elastic heap that uses one or more page to store objects of the same type</summary>
	/// <typeparam name="TPage">Type of the pages in the elastic heap</typeparam>
	internal abstract class ElasticHeap<TPage>
		where TPage : EntryPage
	{
		/// <summary>List of all allocated pages</summary>
		protected readonly List<TPage> m_pages = new List<TPage>();

		/// <summary>Default size of a page</summary>
		protected uint m_pageSize;

		/// <summary>Current page</summary>
		protected TPage m_current;

		public ElasticHeap(uint pageSize)
		{
			m_pageSize = pageSize;
		}

		[Conditional("DEBUG")]
		public void Debug_Dump()
		{
			Console.WriteLine("# Dumping elastic " + typeof(TPage).Name + " heap (" + m_pages.Count + " pages)");
			foreach(var page in m_pages)
			{
				page.Debug_Dump();
			}
		}
	}

}
