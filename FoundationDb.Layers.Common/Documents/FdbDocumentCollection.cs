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
	* Neither the name of the <organization> nor the
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

namespace FoundationDb.Layers.Blobs
{
	using FoundationDb.Client;
	using FoundationDb.Client.Utils;
	using FoundationDb.Layers.Tuples;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Represents a collection of dictionaries of fields.</summary>
	public class FdbHashSetCollection
	{

		public FdbHashSetCollection(FdbSubspace subspace)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");

			this.Subspace = subspace;
		}

		/// <summary>Subspace used as a prefix for all hashsets in this collection</summary>
		public FdbSubspace Subspace { get; private set; }

		/// <summary>Returns the key prefix of an HashSet: (subspace, id, )</summary>
		/// <param name="id"></param>
		/// <returns></returns>
		private Slice GetKey(IFdbTuple id)
		{
			return this.Subspace.Pack(id);
		}

		/// <summary>Returns the key of a specific field of an HashSet: (subspace, id, field, )</summary>
		/// <param name="id"></param>
		/// <param name="field"></param>
		/// <returns></returns>
		private Slice GetFieldKey(IFdbTuple id, string field)
		{
			return this.Subspace.Pack(id, field);
		}

	}

}
