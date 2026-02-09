using MultiLayeredCache.Domain.Exceptions;
using MultiLayeredCache.Domain.Interfaces;
using MultiLayeredCache.Domain.Models;

namespace MultiLayeredCache.Infrastructure.Decorators;

/// <summary>
/// Base data provider implementation that wraps the repository
/// This is the innermost layer in the decorator chain
/// Throws exceptions for error cases (handled by middleware)
/// </summary>
public class RepositoryDataProvider : IDataProvider
{
    private readonly IDataRepository _repository;

    public RepositoryDataProvider(IDataRepository repository)
    {
        _repository = repository;
    }

    public async Task<CachedData> GetByIdAsync(string id)
    {
        var data = await _repository.GetByIdAsync(id);

        if (data == null)
        {
            throw new NotFoundException($"Data with ID '{id}' not found in database");
        }

        return data;
    }

    public async Task<string> SetAsync(CachedData data)
    {
        var id = await _repository.SaveAsync(data);
        return id;
    }
}
