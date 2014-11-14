#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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
	using FoundationDB.Layers.Tuples;
	using JetBrains.Annotations;
	using System;
	using System.Linq;
	using System.Collections.Generic;

	public struct FdbSubspacePartition
	{
		private readonly IFdbSubspace m_subspace;

		public FdbSubspacePartition(IFdbSubspace subspace)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");
			m_subspace = subspace;
		}

		public IFdbSubspace Subspace
		{
			get { return m_subspace; }
		}

		/// <summary>Create a new subspace by appdending a suffix to the current subspace</summary>
		/// <param name="suffix">Suffix of the new subspace</param>
		/// <returns>New subspace with prefix equal to the current subspace's prefix, followed by <paramref name="suffix"/></returns>
		public IFdbSubspace this[Slice suffix]
		{
			[NotNull]
			get
			{
				if (suffix.IsNull) throw new ArgumentException("Partition suffix cannot be null", "suffix");
				//TODO: find a way to limit the number of copies of the key?
				return new FdbSubspace(m_subspace.ConcatKey(suffix));
			}
		}

		/// <summary>Create a new subspace by adding a <paramref name="key"/> to the current subspace's prefix</summary>
		/// <param name="key">Key that will be appended to the current prefix</param>
		/// <returns>New subspace whose prefix is the concatenation of the parent prefix, and the packed representation of <paramref name="key"/></returns>
		public IFdbSubspace this[IFdbKey key]
		{
			[ContractAnnotation("null => halt; notnull => notnull")]
			get
			{
				if (key == null) throw new ArgumentNullException("key");
				var packed = key.ToFoundationDbKey();
				return this[packed];
			}
		}

		public IFdbSubspace this[IFdbTuple tuple]
		{
			[ContractAnnotation("null => halt; notnull => notnull")]
			get
			{
				if (tuple == null) throw new ArgumentNullException("tuple");
				//TODO: find a way to limit the number of copies of the packed tuple?
				return new FdbSubspace(m_subspace.Tuples.Pack(tuple));
			}
		}

		public IFdbSubspace this[ITupleFormattable item]
		{
			[ContractAnnotation("null => halt; notnull => notnull")]
			get
			{
				if (item == null) throw new ArgumentNullException("item");
				var tuple = item.ToTuple();
				if (tuple == null) throw new InvalidOperationException("Formattable item returned an empty tuple");
				return this[tuple];
			}
		}

		/// <summary>Partition this subspace into a child subspace</summary>
		/// <typeparam name="T">Type of the child subspace key</typeparam>
		/// <param name="value">Value of the child subspace</param>
		/// <returns>New subspace that is logically contained by the current subspace</returns>
		/// <remarks>Subspace([Foo, ]).Partition(Bar) is equivalent to Subspace([Foo, Bar, ])</remarks>
		/// <example>
		/// new FdbSubspace(["Users", ]).Partition("Contacts") == new FdbSubspace(["Users", "Contacts", ])
		/// </example>
		[NotNull]
		public IFdbSubspace By<T>(T value)
		{
			return this[FdbTuple.Create<T>(value)];
		}

		/// <summary>Partition this subspace into a child subspace</summary>
		/// <typeparam name="T1">Type of the first subspace key</typeparam>
		/// <typeparam name="T2">Type of the second subspace key</typeparam>
		/// <param name="value1">Value of the first subspace key</param>
		/// <param name="value2">Value of the second subspace key</param>
		/// <returns>New subspace that is logically contained by the current subspace</returns>
		/// <remarks>Subspace([Foo, ]).Partition(Bar, Baz) is equivalent to Subspace([Foo, Bar, Baz])</remarks>
		/// <example>
		/// new FdbSubspace(["Users", ]).Partition("Contacts", "Friends") == new FdbSubspace(["Users", "Contacts", "Friends", ])
		/// </example>
		[NotNull]
		public IFdbSubspace By<T1, T2>(T1 value1, T2 value2)
		{
			return this[FdbTuple.Create<T1, T2>(value1, value2)];
		}

		/// <summary>Partition this subspace into a child subspace</summary>
		/// <typeparam name="T1">Type of the first subspace key</typeparam>
		/// <typeparam name="T2">Type of the second subspace key</typeparam>
		/// <typeparam name="T3">Type of the third subspace key</typeparam>
		/// <param name="value1">Value of the first subspace key</param>
		/// <param name="value2">Value of the second subspace key</param>
		/// <param name="value3">Value of the third subspace key</param>
		/// <returns>New subspace that is logically contained by the current subspace</returns>
		/// <example>
		/// new FdbSubspace(["Users", ]).Partition("John Smith", "Contacts", "Friends") == new FdbSubspace(["Users", "John Smith", "Contacts", "Friends", ])
		/// </example>
		[NotNull]
		public IFdbSubspace By<T1, T2, T3>(T1 value1, T2 value2, T3 value3)
		{
			return this[FdbTuple.Create(value1, value2, value3)];
		}

		/// <summary>Partition this subspace into a child subspace</summary>
		/// <typeparam name="T1">Type of the first subspace key</typeparam>
		/// <typeparam name="T2">Type of the second subspace key</typeparam>
		/// <typeparam name="T3">Type of the third subspace key</typeparam>
		/// <typeparam name="T4">Type of the fourth subspace key</typeparam>
		/// <param name="value1">Value of the first subspace key</param>
		/// <param name="value2">Value of the second subspace key</param>
		/// <param name="value3">Value of the third subspace key</param>
		/// <param name="value4">Value of the fourth subspace key</param>
		/// <returns>New subspace that is logically contained by the current subspace</returns>
		/// <example>
		/// new FdbSubspace(["Users", ]).Partition("John Smith", "Contacts", "Friends", "Messages") == new FdbSubspace(["Users", "John Smith", "Contacts", "Friends", "Messages", ])
		/// </example>
		[NotNull]
		public IFdbSubspace By<T1, T2, T3, T4>(T1 value1, T2 value2, T3 value3, T4 value4)
		{
			return this[FdbTuple.Create(value1, value2, value3, value4)];
		}

	}
}
