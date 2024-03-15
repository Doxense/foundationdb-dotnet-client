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

	public enum InstructionCode
	{
		Invalid = 0,

		// data operations
		Push,
		Dup,
		EmptyStack,
		Swap,
		Pop,
		Sub,
		Concat,
		LogStack,

		// foundationdb operations
		NewTransaction,
		UseTransaction,
		OnError,
		Get,
		GetKey,
		GetRange,
		GetReadVersion,
		GetVersionstamp,
		Set,
		SetReadVersion,
		Clear,
		ClearRange,
		AtomicOp,
		ReadConflictRange,
		WriteConflictRange,
		ReadConflictKey,
		WriteConflictKey,
		DisableWriteConflict,
		Commit,
		Reset,
		Cancel,
		GetCommittedVersion,
		GetApproximateSize,
		WaitFuture,
		GetEstimatedRangeSize,
		GetRangeSplitPoints,

		TuplePack,
		TuplePackWithVersionstamp,
		TupleUnpack,
		TupleRange,
		TupleSort,
		EncodeFloat,
		EncodeDouble,
		DecodeFloat,
		DecodeDouble,

		// Thread Operations
		StartThread,
		WaitEmpty,

		// misc
		UnitTests,

		// Directory/Subspace/Layer Creation
		DirectoryCreateSubspace,
		DirectoryCreateLayer,
		DirectoryCreateOrOpen,
		DirectoryCreate,
		DirectoryOpen,

		// Directory Management
		DirectoryChange,
		DirectorySetErrorIndex,

		// Directory Operations
		DirectoryMove,
		DirectoryMoveTo,
		DirectoryRemove,
		DirectoryRemoveIfExists,
		DirectoryList,
		DirectoryExists,

		// Subspace operation
		DirectoryPackKey,
		DirectoryUnpackKey,
		DirectoryRange,
		DirectoryContains,
		DirectoryOpenSubspace,

		// Directory Logging
		DirectoryLogSubspace,
		DirectoryLogDirectory,

		// Other
		DirectoryStripPrefix,

		// Tenants
		TenantCreate,
		TenantDelete,
		TenantSetActive,
		TenantClearActive,
		TenantList,
	}

}
