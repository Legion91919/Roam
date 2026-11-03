using MediatR;

namespace CarRental.Application.Abstractions.Messaging;

// ---------------------------------------------------------------------------------------------
// CQRS-lite markers.
//
// MediatR only knows "requests". These two marker interfaces make the split explicit, so a reader
// (or an architecture test) can see at a glance which requests CHANGE state and which only READ it:
//
//   ICommand<T> : goes through the Domain (load aggregate, call method, save). May fail on rules.
//   IQuery<T>   : bypasses the Domain entirely and reads a projection (AsNoTracking SQL or Mongo).
//
// "Lite" because both sides share one database by default. The Mongo read model shows how the
// read side can be moved to its own store later without touching any command code.
// ---------------------------------------------------------------------------------------------

public interface ICommand<out TResponse> : IRequest<TResponse> { }

public interface IQuery<out TResponse> : IRequest<TResponse> { }

/// <summary>
/// Opt-in marker: if saving hits an optimistic concurrency conflict, re-run the whole command with
/// fresh data (see ConcurrencyRetryBehavior). Only for commands that are safe to repeat.
/// </summary>
public interface IRetryOnConcurrencyConflict { }
