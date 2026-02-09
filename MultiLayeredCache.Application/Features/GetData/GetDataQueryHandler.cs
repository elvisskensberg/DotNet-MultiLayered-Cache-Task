using MediatR;
using MultiLayeredCache.Domain.Interfaces;

namespace MultiLayeredCache.Application.Features.GetData;

/// <summary>
/// Handler for GetDataQuery
/// Uses the decorator chain (IDataProvider) to retrieve data through cache layers
/// Exceptions are handled by GlobalExceptionMiddleware
/// </summary>
public class GetDataQueryHandler : IRequestHandler<GetDataQuery, GetDataResponse>
{
    private readonly IDataProvider _dataProvider;

    public GetDataQueryHandler(IDataProvider dataProvider)
    {
        _dataProvider = dataProvider;
    }

    public async Task<GetDataResponse> Handle(GetDataQuery request, CancellationToken cancellationToken)
    {
        // GetByIdAsync throws NotFoundException if not found (handled by middleware)
        var data = await _dataProvider.GetByIdAsync(request.Id);

        var response = new GetDataResponse
        {
            Id = data.Id,
            Value = data.Value,
            CreatedAt = data.CreatedAt,
            LastAccessedAt = data.LastAccessedAt
        };

        return response;
    }
}
