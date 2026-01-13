using System.Text.Json;
using PhysicianSignatureAPI.Models;

namespace PhysicianSignatureAPI.Services;

public class PhysicianService : IPhysicianService
{
    private readonly string _contentRootPath;
    private readonly string _physiciansJsonPath;

    public PhysicianService(IWebHostEnvironment env)
    {
        _contentRootPath = env.ContentRootPath;
        var storageRoot = Path.Combine(_contentRootPath, "Storage");
        _physiciansJsonPath = Path.Combine(storageRoot, "physicians.json");
        
        EnsureStorageExists();
    }

    private void EnsureStorageExists()
    {
        if (!File.Exists(_physiciansJsonPath))
        {
            var initialPhysicians = new List<Physician>
            {
                new Physician { Id = "phys-001", Name = "Dr. John Smith" },
                new Physician { Id = "phys-002", Name = "Dr. Sarah Johnson" },
                new Physician { Id = "phys-003", Name = "Dr. Michael Brown" }
            };
            
            var json = JsonSerializer.Serialize(initialPhysicians, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_physiciansJsonPath, json);
        }
    }

    public async Task<Physician?> GetPhysicianByIdAsync(string id)
    {
        var physicians = await GetAllPhysiciansAsync();
        return physicians.FirstOrDefault(p => p.Id == id);
    }

    public async Task<List<Physician>> GetAllPhysiciansAsync()
    {
        if (!File.Exists(_physiciansJsonPath))
        {
            return new List<Physician>();
        }

        var json = await File.ReadAllTextAsync(_physiciansJsonPath);
        return JsonSerializer.Deserialize<List<Physician>>(json) ?? new List<Physician>();
    }
}

