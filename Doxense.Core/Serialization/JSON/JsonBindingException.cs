#region Copyright (c) 2023 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace Doxense.Serialization.Json
{
	using System;
	using System.Runtime.Serialization;

	/// <summary>Erreur survenue lors de la désérialisation d'une valeur JSON en objet CLR</summary>
	[Serializable]
	public class JsonBindingException : InvalidOperationException
	{

		public JsonValue? Value { get; }

		public string? Path { get; }

		public JsonBindingException()
		{ }

		public JsonBindingException(string message)
			: base(message)
		{ }

		public JsonBindingException(string message, Exception? innerException)
			: base(message, innerException)
		{ }

		public JsonBindingException(string message, JsonValue? value, Exception? innerException = null)
			: base(message, innerException)
		{
			this.Value = value;
		}

		public JsonBindingException(string message, string? path, JsonValue? value, Exception? innerException = null)
			: base(message, innerException)
		{
			this.Path = path;
			this.Value = value;
		}

		protected JsonBindingException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			this.Value = JsonValue.Parse(info.GetString("Value"));
			this.Path = info.GetString("Path");
		}

		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue("Value", this.Value?.ToJson());
			info.AddValue("Path", this.Path);
		}

	}

}
