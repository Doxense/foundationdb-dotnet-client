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

namespace FoundationDB.Client.Utils
{
	using System;
#if !NET_4_0
	using System.Runtime.ExceptionServices;
#endif

	/// <summary>Either has a value, nothing, or an exception</summary>
	/// <typeparam name="T"></typeparam>
	internal struct Maybe<T>
	{
		/// <summary>If true, there is a value. If false, either no value or an exception</summary>
		public readonly bool HasValue;

		/// <summary>If HasValue is true, holds the value. Else, contains default(T)</summary>
		public readonly T Value;

		/// <summary>If HasValue is false optinally holds an error that was captured</summary>
#if NET_4_0
		public readonly Exception Error;
#else
		public readonly ExceptionDispatchInfo Error;
#endif

		public static Maybe<T> Empty { get { return default(Maybe<T>); } }

		public static Maybe<T> FromError(Exception error)
		{		
#if NET_4_0
			return new Maybe<T>(error);
#else
			return new Maybe<T>(ExceptionDispatchInfo.Capture(error));
#endif
		}

		public static Maybe<T> FromError(ExceptionDispatchInfo error)
		{
			return new Maybe<T>(error);
		}

		public Maybe(T value)
		{
			this.HasValue = true;
			this.Value = value;
			this.Error = null;
		}

#if NET_4_0
		public Maybe(Exception error)
		{
			this.HasValue = false;
			this.Value = default(T);
			this.Error = error;
		}
#else
		public Maybe(ExceptionDispatchInfo error)
		{
			this.HasValue = false;
			this.Value = default(T);
			this.Error = error;
		}
#endif

		/// <summary>If true then there was an error captured</summary>
		public bool HasFailed { get { return this.Error != null; } }

		/// <summary>Rethrows any captured error, if there was one.</summary>
		public void ThrowIfFailed()
		{
			if (this.Error != null)
			{
#if NET_4_0
				throw this.Error;
#else
				this.Error.Throw();
#endif
			}
		}
	}

}
