using MediatR;

namespace MultiLayeredCache.Application.Features.GetData;

/// <summary>
/// Query to retrieve data by ID
/// Exceptions are handled by middleware
/// </summary>
public record GetDataQuery(string Id) : IRequest<GetDataResponse>;
