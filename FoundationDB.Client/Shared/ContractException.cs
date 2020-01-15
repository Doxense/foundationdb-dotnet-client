#region Copyright (c) 2013-2020, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

#if !USE_SHARED_FRAMEWORK

namespace Doxense.Diagnostics.Contracts
{
	using System;
	using System.Runtime.Serialization;
	using System.Security;
	using SDC = System.Diagnostics.Contracts;

	[Serializable]
	internal sealed class ContractException : Exception
	{
		// copie de l'implémentation "internal" de System.Data.Contracts.ContractException

		#region Constructors...

		private ContractException()
		{
			base.HResult = -2146233022;
		}

		public ContractException(SDC.ContractFailureKind kind, string failure, string? userMessage, string? condition, Exception? innerException)
			: base(failure, innerException)
		{
			base.HResult = -2146233022;
			this.Kind = kind;
			this.UserMessage = userMessage;
			this.Condition = condition;
		}

		private ContractException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			this.Kind = (SDC.ContractFailureKind)info.GetInt32("Kind");
			this.UserMessage = info.GetString("UserMessage");
			this.Condition = info.GetString("Condition");
		}

		#endregion

		#region Public Properties...

		public string? Condition { get; }

		public SDC.ContractFailureKind Kind { get; }

		public string? UserMessage { get; }

		public string Failure => this.Message;

		#endregion

		[SecurityCritical]
		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue("Kind", (int) this.Kind);
			info.AddValue("UserMessage", this.UserMessage);
			info.AddValue("Condition", this.Condition);
		}

	}

}

#endif
