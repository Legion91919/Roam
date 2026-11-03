using CarRental.Application.Abstractions.Messaging;
using CarRental.Application.Abstractions.ReadModels;
using MediatR;

namespace CarRental.Application.Branches.GetBranches;

public sealed record GetBranchesQuery : IQuery<IReadOnlyList<BranchDto>>;

public sealed record BranchDto(Guid Id, string Code, string Name, string City, string Type, string Address);

public sealed class GetBranchesHandler(IBranchReadModel readModel) : IRequestHandler<GetBranchesQuery, IReadOnlyList<BranchDto>>
{
    public Task<IReadOnlyList<BranchDto>> Handle(GetBranchesQuery query, CancellationToken cancellationToken) =>
        readModel.GetAllAsync(cancellationToken);
}
