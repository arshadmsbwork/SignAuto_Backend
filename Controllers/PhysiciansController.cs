using Microsoft.AspNetCore.Mvc;
using PhysicianSignatureAPI.Services;

namespace PhysicianSignatureAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PhysiciansController : ControllerBase
{
    private readonly IPhysicianService _physicianService;

    public PhysiciansController(IPhysicianService physicianService)
    {
        _physicianService = physicianService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPhysicians()
    {
        var physicians = await _physicianService.GetAllPhysiciansAsync();
        return Ok(physicians);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPhysician(string id)
    {
        var physician = await _physicianService.GetPhysicianByIdAsync(id);
        if (physician == null)
            return NotFound();

        return Ok(physician);
    }
}

