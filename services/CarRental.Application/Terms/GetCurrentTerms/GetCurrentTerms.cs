using CarRental.Application.Abstractions.Messaging;
using CarRental.Application.Abstractions.Terms;
using MediatR;

namespace CarRental.Application.Terms.GetCurrentTerms;

public sealed record GetCurrentTermsQuery : IQuery<TermsDocument>;

public sealed class GetCurrentTermsHandler(ITermsProvider terms) : IRequestHandler<GetCurrentTermsQuery, TermsDocument>
{
    public Task<TermsDocument> Handle(GetCurrentTermsQuery query, CancellationToken cancellationToken) => Task.FromResult(terms.Current);
}
