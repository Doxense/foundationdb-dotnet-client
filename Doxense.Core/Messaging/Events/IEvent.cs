#region Copyright Doxense 2018-2022
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Messaging.Events
{
	using System;
	using System.Collections.Generic;
	using Doxense.Serialization.Json;
	using NodaTime;

	[JsonType(TypePropertyName = nameof(IEvent.Type))]
	public interface IEvent
	{
		/// <summary>Type of event</summary>
		string Type { get; }

		/// <summary>When the event was created</summary>
		Instant Timestamp { get; }

		string? OperationId { get; }

		/// <summary>Liste des topics sur lesquelles délivrer cet event</summary>
		IEnumerable<string> GetTopics();
		//REVIEW: est-ce que c'est l'event qui doit décider? ou alors un autre composant qui voit passer les events et qui décide ou les envoyer?

	}

}
