using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MultiLayeredCache.Application.Features.CreateData;
using MultiLayeredCache.Application.Features.GetData;

namespace MultiLayeredCache.Api.Controllers;

/// <summary>
/// API controller for data operations with multi-layered caching
/// Uses MediatR for CQRS pattern
/// All exceptions handled by Global Exception Middleware
///
/// PRODUCTION RECOMMENDATION: Enable API versioning for backward compatibility
/// Uncomment the line below and use: [Route("api/v{version:apiVersion}/[controller]")]
/// This allows for v2, v3 endpoints without breaking existing clients.
/// See: https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/routing#attribute-routing-with-http-verb-attributes
/// </summary>
[ApiController]
[Route("[controller]")] // Task requirement: /data/{id} and /data (no versioning)
// [Route("api/v{version:apiVersion}/[controller]")] // RECOMMENDED: Uncomment for production API versioning
// [ApiVersion("1.0")] // RECOMMENDED: Uncomment for production API versioning
public class DataController : ControllerBase
{
    private readonly IMediator _mediator;

    public DataController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Retrieves data by ID through multi-layered cache system (Decorator pattern)
    /// </summary>
    /// <param name="id">The unique identifier of the data</param>
    /// <returns>The requested data</returns>
    /// <response code="200">Returns the requested data</response>
    /// <response code="404">If the data with specified ID is not found (handled by middleware)</response>
    /// <response code="400">If validation fails (handled by middleware)</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(GetDataResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GetDataResponse>> GetData(string id)
    {
        var response = await _mediator.Send(new GetDataQuery(id));
        return Ok(response);
    }

    /// <summary>
    /// Creates new data and stores it in the database
    /// </summary>
    /// <param name="command">The command containing data to be created</param>
    /// <returns>The ID of the newly created data</returns>
    /// <response code="201">Returns the ID of the newly created data</response>
    /// <response code="400">If validation fails (handled by middleware)</response>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<object>> CreateData([FromBody] CreateDataCommand command)
    {
        var id = await _mediator.Send(command);

        return CreatedAtAction(nameof(GetData), new { id }, new { id });
    }
}
