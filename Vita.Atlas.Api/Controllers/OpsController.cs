using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vita.Atlas.Infrastructure.Data;

namespace Vita.Atlas.Api.Controllers;

[ApiController]
[Route("api/ops")]
public sealed class OpsController : ControllerBase
{
    private readonly AtlasDbContext _dbContext;

    public OpsController(AtlasDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            status = "ok",
            utcNow = DateTime.UtcNow
        });
    }

    [HttpGet("db-test")]
    public async Task<IActionResult> TestDatabase(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.Database.OpenConnectionAsync(cancellationToken);
            await _dbContext.Database.CloseConnectionAsync();

            return Ok(new
            {
                status = "ok",
                message = "Database connection succeeded"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                status = "error",
                message = ex.Message,
                inner = ex.InnerException?.Message
            });
        }
    }
}