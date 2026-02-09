using MediatR;
using MultiLayeredCache.Domain.Interfaces;
using MultiLayeredCache.Domain.Models;

namespace MultiLayeredCache.Application.Features.CreateData;

/// <summary>
/// Handler for CreateDataCommand
/// Saves data directly to the repository (no caching on write)
/// Exceptions are handled by GlobalExceptionMiddleware
/// </summary>
public class CreateDataCommandHandler : IRequestHandler<CreateDataCommand, string>
{
    private readonly IDataRepository _repository;

    public CreateDataCommandHandler(IDataRepository repository)
    {
        _repository = repository;
    }

    public async Task<string> Handle(CreateDataCommand request, CancellationToken cancellationToken)
    {
        var data = new CachedData
        {
            Value = request.Value,
            CreatedAt = DateTime.UtcNow,
            LastAccessedAt = DateTime.UtcNow
        };

        var id = await _repository.SaveAsync(data);
        return id;
    }
}
