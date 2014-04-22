#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.API
{
	using FoundationDB.Client;
	using FoundationDB.Storage.Memory.Core;
	using FoundationDB.Storage.Memory.Utils;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.Contracts;

	internal sealed class LevelWriter : IDisposable
	{

		/// <summary>Scratch use to format keys when committing (single writer)</summary>
		private UnmanagedSliceBuilder m_scratch = new UnmanagedSliceBuilder(128 * 1024);
		private List<IntPtr> m_list;
		private KeyHeap m_keys;
		private ValueHeap m_values;

		public LevelWriter(int count, KeyHeap keyHeap, ValueHeap valueHeap)
		{
			Contract.Requires(count > 0 && keyHeap != null && valueHeap != null);
			m_keys = keyHeap;
			m_values = valueHeap;
			m_list = new List<IntPtr>(count);
		}

		public List<IntPtr> Data { get { return m_list; } }

		public unsafe void Add(ulong sequence, KeyValuePair<Slice, Slice> current)
		{
			// allocate the key
			var tmp = MemoryDatabaseHandler.PackUserKey(m_scratch, current.Key);
			Key* key = m_keys.Append(tmp);
			Contract.Assert(key != null, "key == null");

			// allocate the value
			Slice userValue = current.Value;
			uint size = checked((uint)userValue.Count);
			Value* value = m_values.Allocate(size, sequence, null, key);
			Contract.Assert(value != null, "value == null");
			UnmanagedHelpers.CopyUnsafe(&(value->Data), userValue);

			key->Values = value;

			m_list.Add(new IntPtr(key));
		}

		public void Reset()
		{
			m_list.Clear();
		}

		public void Dispose()
		{
			m_scratch.Dispose();
		}
	}

}
