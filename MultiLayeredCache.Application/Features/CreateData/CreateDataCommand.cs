using MediatR;

namespace MultiLayeredCache.Application.Features.CreateData;

/// <summary>
/// Command to create new data
/// Exceptions are handled by middleware
/// </summary>
public record CreateDataCommand(string Value) : IRequest<string>;
