using PhysicianSignatureAPI.Models;

namespace PhysicianSignatureAPI.Services;

public interface IPhysicianService
{
    Task<Physician?> GetPhysicianByIdAsync(string id);
    Task<List<Physician>> GetAllPhysiciansAsync();
}

