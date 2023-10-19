#region Copyright (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of Doxense nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL DOXENSE BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace Doxense.Threading.Operations
{
	using System;
	using System.Collections.Concurrent;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Runtime.ExceptionServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Messaging.Events;
	using Doxense.Reactive.Disposables;
	using Doxense.Serialization;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.DependencyInjection.Extensions;
	using Microsoft.Extensions.Logging;
	using OpenTelemetry.Trace;

	/// <summary>Représente le contexte d'exécution d'une opération asynchrone</summary>
	public interface IOperationContext
	{

		/// <summary>Id globalement unique de l'opération</summary>
		string Id { get; }

		/// <summary>Type d'opération (pour diagnostique)</summary>
		string Type { get; }

		/// <summary>Si non null, clé de dédoublonage</summary>
		string? Key { get; }

		/// <summary>Si non null, opération parente qui a déclenché cette opération</summary>
		IOperationContext? Parent { get; }

		/// <summary>Gestionnaire d'opérations</summary>
		IOperationOverlord Overlord { get; }

		/// <summary>Token d'annulation qui suit la vie de cette opération</summary>
		CancellationToken Cancellation { get; }

		/// <summary>Etat actuel de l'opération</summary>
		OperationState State { get; }

		bool TryGetResult([MaybeNullWhen(false)] out object? result, [MaybeNullWhen(false)] out Type type);

		/// <summary>Si non null, erreur produite lors de l'exécution de l'opération</summary>
		OperationError? Error { get; }

		//TODO: plus de params
		void Log(LogLevel level, Exception? exception, string message, object[]? args = null);

		void Dispatch(IEvent evt);

		IDisposable ExecuteStep(string id, string? label = null);

		Task<OperationResult<TResult>> ExecuteSubOperation<TResult>(IOperationContext operation, Func<IOperationContext, Task<OperationResult<TResult>>> handler);
		
		Activity? Activity { get; }
		
		OperationFailed Failed(OperationError error);

		OperationResult<TResult> Success<TResult>(TResult result);

		bool TryGetResult<TResult>([MaybeNullWhen(false)] out TResult result);

	}

	public enum OperationState
	{
		Invalid = 0,
		Pending,
		Processing,
		Completed,
	}

	/// <summary>Represents the result of the execution of an operation</summary>
	[DebuggerDisplay("Id={Context.Id}, State={Context.State}")]
	public readonly struct OperationResult<TResult>
	{

		public OperationResult(IOperationContext context)
		{
			this.Context = context;
		}

		public readonly IOperationContext Context;

		public bool HasFailed => this.Context.Error != null;

		public TResult EnsureSuccess()
		{
			if (this.Context.State != OperationState.Completed)
			{
				throw new InvalidOperationException("Operation is not completed yet.");
			}

			if (this.Context.Error != null)
			{
				Context.Error.Exception?.Throw();
				//TODO: mapper l'erreur de manière plus spécifique?
				throw new InvalidOperationException($"Operation failed: [{this.Context.Error.Code}] {this.Context.Error.Message}"); }

			if (!this.Context.TryGetResult(out object? value, out Type? type))
			{
				throw new InvalidOperationException("Operation completed successfully, but does not have a return value.");
			}

			if (!typeof(TResult).IsAssignableFrom(type))
			{
				throw new InvalidOperationException($"Operation completed successfully, but the result type does not match: expected {typeof(TResult).GetFriendlyName()} but was {type.GetFriendlyName()}.");
			}

			if (default(TResult) is null)
			{ // class
				return (TResult) value!;
			}
			else
			{ // valuetype
				return value == null ? default! : (TResult) value!;
			}
		}

		public bool Check([MaybeNullWhen(false)] out TResult result, [MaybeNullWhen(true)] out OperationError error)
		{
			if (this.Context.State != OperationState.Completed)
			{
				result = default;
				error = new OperationError { Code = "not_completed", Message = "Operation is not completed yet." };
				return false;
			}

			if (this.Context.Error != null)
			{
				result = default;
				error = this.Context.Error;
				return false;
			}

			if (!this.Context.TryGetResult(out object? value, out Type? type))
			{
				result = default;
				error = new OperationError
				{
					Code = "invalid_result",
					Message = "Operation completed successfully, but does not have a return value."
				};
				return false;
			}

			if (!typeof(TResult).IsAssignableFrom(type))
			{
				result = default;
				error = new OperationError
				{
					Code = "invalid_result",
					Message = "Operation completed successfully, but the result type does not match.",
					Details = new { ExpectedType = typeof(TResult).GetFriendlyName(), ActualType = type.GetFriendlyName() }
				};
				return false;
			}

			if (default(TResult) is null)
			{ // class
				result = (TResult) value!;
			}
			else
			{ // valuetype
				result = value == null ? default! : (TResult) value!;
			}
			error = null;
			return true;
		}

		public static implicit operator OperationResult<TResult>(OperationFailed failed)
		{
			return new OperationResult<TResult>(failed.Context);
		}

	}

	public readonly struct OperationFailed
	{

		public readonly IOperationContext Context;

		public OperationFailed(IOperationContext context)
		{
			this.Context = context;
		}

		public OperationError Error => this.Context.Error!;

	}

	public sealed record OperationError
	{
		public string Code { get; init; }

		public string Message { get; init; }

		public ExceptionDispatchInfo? Exception { get; init; }

		public object? Details { get; init; }
	}

	public sealed class OperationErrorException : Exception
	{

		public OperationErrorException(OperationError error, Exception? inner = null)
			: base(error.Message, inner)
		{
			this.Error = error;
		}

		public OperationErrorException(string code, string message, object? details = null, Exception? exception = null)
		{
			this.Error = new OperationError { Code = code, Message = message, Details = details, Exception = exception != null ? ExceptionDispatchInfo.Capture(exception) : null };
		}

		public OperationErrorException(string code, string message, object? details = null, ExceptionDispatchInfo? exception = null)
		{
			this.Error = new OperationError { Code = code, Message = message, Details = details, Exception = exception };
		}

		public OperationError Error { get; }

	}

	public interface IStoryBook
	{
		string Type { get; }

		string Recipee { get; }
	}

	public interface IOperationWorflow
	{

	}

	public interface IOperationWorflow<TRequest, TResult> : IOperationWorflow
	{
		Task<OperationResult<TResult>> ExecuteAsync();
	}

	public interface IOperationWorflow<TWorkflow, TRequest, TResult> : IOperationWorflow<TRequest, TResult>
		where TWorkflow: IOperationWorflow<TWorkflow, TRequest, TResult>
	{
		
		//static abstract TWorkflow CreateInstance(IServiceProvider services, string name, IOperationOverlord overlord, TRequest req, CancellationToken ct);

		//static abstract TWorkflow CreateInstance(IServiceProvider services, IOperationContext<TRequest, TResult> ctx);

	}

	public abstract class OperationWorflowBase<TWorkflow, TParameter, TResult> : IOperationWorflow<TWorkflow, TParameter, TResult>
		where TWorkflow: OperationWorflowBase<TWorkflow, TParameter, TResult>
	{

		protected IOperationContext Context { get; }

		protected OperationWorflowBase(IOperationContext context)
		{
			this.Context = context;
		}

		public Task<OperationResult<TResult>> ExecuteAsync()
		{
			return this.Context.Overlord.ExecuteOperation(this.Context, _ => this.ExecuteInternalAsync());
		}

		protected abstract Task<OperationResult<TResult>> ExecuteInternalAsync();

		protected CancellationToken Cancellation => this.Context.Cancellation;

		protected void LogDebug(string message) => this.Context.LogDebug(message);

		protected void LogInformation(string message) => this.Context.LogInformation(message);

		protected void LogWarning(string message) => this.Context.LogWarning(message);

		protected void LogError(string message) => this.Context.LogError(message);

		protected void LogError(Exception e, string message) => this.Context.LogError(e, message);

		//TODO: les autres!

		protected OperationResult<TResult> Success(TResult result) => this.Context.Success(result);

		protected OperationResult<TResult> Failed(OperationError error) => this.Context.Failed(error);

		protected OperationResult<TResult> Throw(Exception exception) => this.Context.Throw(exception);
		//TOD: les autres!

		public static TWorkflow CreateInstance(IServiceProvider services, string name, IOperationOverlord overlord, TParameter req, CancellationToken ct)
        {
			var ctx = overlord.Create(name, null, null, ct);
			return CreateInstance(services, ctx);
		}

		public static TWorkflow CreateInstance(IServiceProvider services, IOperationContext ctx)
		{
			return ActivatorUtilities.CreateInstance<TWorkflow>(services, ctx); //TODO: comment régler le pb du type generique de l'executor?
		}

	}

	public static class OperationContextExtensions
	{

		/// <summary>Register <see cref="IOperationOverlord"/> and allows use of <see cref="IOperationContext"/> and <see cref="IOperationOverlord"/> components</summary>
		public static IServiceCollection AddBackgroundOperations(this IServiceCollection services)
		{
#if DEBUG
			if (services.Any(x => x.ServiceType == typeof(IOperationOverlord)))
			{
				throw new InvalidOperationException("Background operations are already registered!");
			}
#endif
			services.TryAddSingleton<IOperationOverlord, OperationOverlord>();
			services.TryAddSingleton<IEventBus, EventBus>();
			return services;
		}

		public static Task<OperationResult<TResult>> ExecuteOperation<TResult>(this IOperationOverlord overlord, string type, string? key, Func<IOperationContext, Task<OperationResult<TResult>>> handler, CancellationToken ct)
		{
			var context = overlord.Create(type, key, null, ct);
			return overlord.ExecuteOperation(context, handler);
		}

		public static Task<OperationResult<TResult>> ExecuteSubOperation<TResult>(this IOperationContext parent, string type, string? key, Func<IOperationContext, Task<OperationResult<TResult>>> handler)
		{
			//REVIEW: BUGBUG: je sais pas trop quelle est la meilleur manière de combiner les keys avec le parent?
			if (key == null)
			{
				key = parent.Key != null ? parent.Key + ":" + type : null;
			}
			else
			{
				key = parent.Key != null ? parent.Key + ":" + key : key;
			}

			var subContext = parent.Overlord.Create(type, key, parent, parent.Cancellation);
			return parent.Overlord.ExecuteOperation(subContext, handler);
		}

		public static void ThrowIfCancellationRequested(this IOperationContext context)
		{
			context.Cancellation.ThrowIfCancellationRequested();
		}

		#region OperationResult factories...

		public static OperationResult<TResult> Cancelled<TResult>(this IOperationContext context)
		{
			var cancelled = Cancelled(context);
			return (OperationResult<TResult>) cancelled;
		}

		public static OperationFailed Cancelled(this IOperationContext context)
		{
			Contract.Debug.Requires(context.Cancellation.IsCancellationRequested);
			return context.Failed(new OperationError
			{
				Code = "operation_cancelled",
				Message = "Operation has been cancelled.",
				Exception = ExceptionDispatchInfo.Capture(new OperationCanceledException(context.Cancellation))
			});
		}

		public static OperationFailed MissingParameters(this IOperationContext context, string paramName)
		{
			return context.Failed(new OperationError
			{
				Code = "invalid_parameter",
				Message = $"Required parameter '{paramName}' is missing.",
				Details = new { ParamName = paramName, Reason = "required" }
			});
		}

		public static OperationFailed InvalidParameter(this IOperationContext context, string paramName)
		{
			return context.Failed(new OperationError
			{
				Code = "invalid_parameter",
				Message = $"Parameter '{paramName}' is invalid.",
				Details = new { ParamName = paramName, Reason = "invalid" }
			});
		}

		public static OperationFailed Throw(this IOperationContext context, Exception exception)
		{
			if (exception is OperationErrorException opEx)
			{
				return context.Failed(opEx.Error);
			}
			return context.Failed(new OperationError
			{
				Code = "internal_error",
				Message = exception.Message,
				Exception = ExceptionDispatchInfo.Capture(exception),
			});
		}

		public static OperationFailed AccountLinkError(this IOperationContext context, string providerId, string? message = null)
		{
			return context.Failed(new OperationError
			{
				Code = "account_link",
				Message = message ?? "Could not interract with remote provider because user account is not linked",
				Details = new { ProviderId = providerId},
			});
		}

		public static OperationFailed RemoteStoreError(this IOperationContext context, string providerId, string? message = null)
		{
			return context.Failed(new OperationError
			{
				Code = "remote_error",
				Message = message ?? $"An error occured while trying to call remote store '{providerId}'",
				Details = new { ProviderId = providerId},
			});
		}

		public static OperationFailed PathNotFoundError(this IOperationContext context, string providerId, string path, object? details = null)
		{
			return context.Failed(new OperationError
			{
				Code = "path_not_found",
				Message = $"[Provider '{providerId}'] Could not find path '{path}'",
				Details = details ?? new { ProviderId = providerId, Path = path},
			});
		}

		//TODO: ...

		#endregion

		#region Logs...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LogDebug(this IOperationContext context, string message) => context.Log(LogLevel.Debug, null, message, null);
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LogDebug(this IOperationContext context, string format, params object[]? args) => context.Log(LogLevel.Debug, null, format, args);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LogInformation(this IOperationContext context, string message) => context.Log(LogLevel.Information, null, message, null);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LogInformation(this IOperationContext context, string format, params object[]? args) => context.Log(LogLevel.Information, null, format, args);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LogWarning(this IOperationContext context, string message) => context.Log(LogLevel.Warning, null, message, null);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LogWarning(this IOperationContext context, string format, params object[]? args) => context.Log(LogLevel.Warning, null, format, args);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LogError(this IOperationContext context, string message) => context.Log(LogLevel.Error, null, message, null);
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LogError(this IOperationContext context, string format, params object[] args) => context.Log(LogLevel.Error, null, format, args);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void LogError(this IOperationContext context, Exception exception, string message) => context.Log(LogLevel.Error, exception, message, null);

		#endregion

	}

	[DebuggerDisplay("Type={Type}, Key={Key}, Id={Id}, State={State}")]
	public class OperationContext : IOperationContext
	{

		public OperationContext(OperationOverlord overlord, string id, OperationState state, string type, string? key, IOperationContext? parent, CancellationTokenSource cts)
		{
			Contract.NotNull(overlord);

			this.Overlord = overlord;
			this.Id = id;
			this.State = state;
			this.Type = type;
			this.Key = key;
			this.Parent = parent;
			this.Lifetime = cts;
		}

		public string Id { get; }

		public OperationState State { get; private set; }

		public string Type { get; }

		public string? Key { get; }

		protected CancellationTokenSource Lifetime { get; }

		public CancellationToken Cancellation => this.Lifetime.Token;

		public IOperationContext? Parent { get; }

		public bool HasResult { get; private set; }

		public object? Result { get; private set; }

		public OperationError? Error { get; private set; }

		public OperationOverlord Overlord { get; }

		IOperationOverlord IOperationContext.Overlord => this.Overlord;

		internal async Task Run(Func<IOperationContext, Task> handler)
		{
			if (this.Cancellation.IsCancellationRequested)
			{
				if (this.Error == null)
				{
					this.Cancelled();
				}
				return;
			}

			lock (this)
			{
				if (this.State != OperationState.Pending)
				{
					if (this.Error == null)
					{
						Failed(new OperationError { Code = "aborted", Message = "Operation has been aborted.", });
					}
					return;
				}
				this.State = OperationState.Processing;
			}

			try
			{
				await handler(this).ConfigureAwait(false);
			}
			catch (Exception e)
			{
				this.LogError(e, "Operation execution failed.");
				if (this.Error == null)
				{
					Failed(e is OperationErrorException opEx ? opEx.Error : new OperationError
					{
						Code = "execution_failed",
						Message = e.Message,
						Exception = ExceptionDispatchInfo.Capture(e),
						Details = null, //TODO:
					});
				}
			}
			finally
			{
				this.State = OperationState.Completed;
			}

		}

		public bool TryGetResult([MaybeNullWhen(false)] out object? result, [MaybeNullWhen(false)] out Type type)
		{
			if (!this.HasResult || this.State != OperationState.Completed)
			{
				result = null;
				type = null;
				return false;
			}

			result = this.Result;
			type = this.Result?.GetType() ?? typeof(object);
			return true;
		}

		public bool TryGetResult<TResult>([MaybeNullWhen(false)] out TResult result)
		{
			if (!this.HasResult || this.State != OperationState.Completed)
			{
				result = default;
				return false;
			}

			if (this.Result is not TResult r)
			{
				result = default;
				return false;
			}

			result = r;
			return true;
		}

		public OperationResult<TResult> Success<TResult>(TResult? result)
		{
			//REVIEW: est-ce qu'on autorise a Success() apres avoir appelé Failed() ?
			lock (this) //TODO: lock?
			{
				this.HasResult = true;
				this.Result = result;
				this.Error = null;
				this.State = OperationState.Completed;
			}
			return new OperationResult<TResult>(this);
		}

		public OperationResult<TResult> Failed<TResult>(OperationError error)
		{
			var failed = Failed(error);
			return (OperationResult<TResult>) failed;
		}

		public OperationFailed Failed(OperationError error)
		{
			//REVIEW: est-ce qu'on autorise a Failed() apres avoir appelé Success() ?
			Contract.NotNull(error);
			lock (this)
			{
				this.HasResult = false;
				this.Result = default;
				this.Error = error;
				this.State = OperationState.Completed;

				var activity = this.Activity;
				if (activity != null)
				{
					activity.SetStatus(ActivityStatusCode.Error, error.Code);
					activity.SetTag("operation.error.code", error.Code);
					var ex = error?.Exception?.SourceException;
					if (ex != null) activity.RecordException(ex);
				}

			}
			return new OperationFailed(this);
		}

		public void Dispatch(IEvent evt)
		{
			this.Overlord.Dispatch(this, evt);
		}

		public void Log(LogLevel level, Exception? exception, string message, object[]? args = null)
		{
			this.Overlord.Log(this, level, exception, message, args);
		}

		public IDisposable ExecuteStep(string id, string? label = null)
		{
			//BUGBUG: TODO: !
			return Disposable.Empty();
		}

		public Task<OperationResult<TSubResult>> ExecuteSubOperation<TSubResult>(
			IOperationContext operation,
			Func<IOperationContext, Task<OperationResult<TSubResult>>> handler)
		{
			//TOOD: param check?
			return this.Overlord.ExecuteOperation(operation, handler);
		}

		public Activity? Activity { get; private set; }

		internal void SetActivity(Activity? activity)
		{
			this.Activity = activity;
			if (activity?.IsAllDataRequested == true)
			{
				activity.SetTag("operation.id", this.Id);
				activity.SetTag("operation.key", this.Key);
				activity.SetTag("operation.type", this.Type);
				activity.SetTag("operation.parent", this.Parent?.Id);
				//TODO: more?
			}
		}

		//.....
	}

	public interface IOperationOverlord
	{

		bool TryGetOperationById(string id, [MaybeNullWhen(false)] out IOperationContext context);

		//bool TryGetOperationByKey(string key, out IOperationContext? context);

		IOperationContext Create(string type, string? key, IOperationContext? parent = null, CancellationToken ct = default);

		//Task<OperationResult<TResult>> ExecuteOperation<TParameters, TResult>(IOperationContext<TParameters, TResult> context, Func<IOperationContext<TParameters, TResult>, Task<OperationResult<TResult>>> handler);

		Task<OperationResult<TResult>> ExecuteOperation<TResult>(IOperationContext context, Func<IOperationContext, Task<OperationResult<TResult>>> handler);

		void Log(IOperationContext context, LogLevel level, Exception? exception, string message, object[]? args = null);

		void Dispatch(IOperationContext context, IEvent evt);

	}

	public class OperationOverlord : IOperationOverlord
	{

		private static readonly ActivitySource ActivitySource = new("Doxense.Threading.Operations");


		private ILogger<OperationOverlord> Logger { get; }

		private IEventBus EventBus { get; }

		private CancellationTokenSource Lifetime { get; }

		private ConcurrentDictionary<string, IOperationContext> OperationsById { get; } = new ConcurrentDictionary<string, IOperationContext>();

		private ConcurrentDictionary<string, IOperationContext> OperationsByKey { get; } = new ConcurrentDictionary<string, IOperationContext>();

		public OperationOverlord(IEventBus eventBus, ILogger<OperationOverlord> logger)
		{
			this.EventBus = eventBus;
			this.Logger = logger;
			this.Lifetime = new CancellationTokenSource();
		}

		public string NewId()
		{
			//TODO: générateur externe?
			return Uuid128.NewUuid().ToString();
		}

		public bool TryGetOperationById(string id, [MaybeNullWhen(false)] out IOperationContext context)
		{
			//BUGBUG: TODO !
			context = null;
			return false;
		}

		public IOperationContext Create(string type, string? key, IOperationContext? parent = null, CancellationToken ct = default)
		{
			var cts = CancellationTokenSource.CreateLinkedTokenSource(this.Lifetime.Token, ct);
			return new OperationContext(this, NewId(), OperationState.Pending, type, key, parent, cts);
		}

		private OperationFailed HandleCancellation(IOperationContext context)
		{
			Contract.Debug.Requires(context.Cancellation.IsCancellationRequested);
			if (context.Error != null) return new OperationFailed(context);
			return context.Cancelled();
		}

		public async Task<OperationResult<TResult>> ExecuteOperation<TResult>(IOperationContext context, Func<IOperationContext, Task<OperationResult<TResult>>> handler)
		{
			Contract.NotNull(context);
			Contract.NotNull(handler);

			using var activity = ActivitySource.StartActivity(context.Type + " execute");

			if (activity != null && context is OperationContext ctx)
			{
				ctx.SetActivity(activity);
			}

			if (context.Cancellation.IsCancellationRequested)
			{
				return HandleCancellation(context);
			}

			try
			{
				return await handler(context).ConfigureAwait(false);
			}
			catch (OperationErrorException e)
			{
				return context.Failed(e.Error);
			}
			catch (Exception e)
			{
				return context.Failed(new OperationError
				{
					Code = "internal_error",
					Message = e.Message,
					Exception = ExceptionDispatchInfo.Capture(e),
					Details = null,
				});
			}
		}

		public void Log(IOperationContext context, LogLevel level, Exception? exception, string message, object?[]? args = null)
		{
			if (exception == null)
			{
				this.Logger.Log(level, $"{context.Id}: {message}", args ?? Array.Empty<object?>());
			}
			else
			{
				this.Logger.Log(level, exception, $"{context.Id}: {message}", args ?? Array.Empty<object?>());
			}
		}

		public void Dispatch(IOperationContext context, IEvent evt)
		{
			this.EventBus.Dispatch(evt);
		}

	}

}
