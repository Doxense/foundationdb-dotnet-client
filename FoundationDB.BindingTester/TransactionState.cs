#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace FoundationDB.Client.Testing
{
	using System;

	public sealed record TransactionState : IDisposable
	{

		public required IFdbTransaction Transaction { get; init; }

		public FdbTenant? Tenant { get; init; }

		public bool Dead { get; private set; }

		public void Dispose()
		{
			this.Dead = true;
			this.Transaction.Dispose();
		}

	}

}
