using Gibag.Shared.Domain;

namespace Gibag.Modules.Core.Domain;

public class Role : TenantEntityBase
{
    public string Name { get; private set; }
    // En EF Core / PostgreSQL con JSONB el property se maneja como string y tiene configuraciones adicionales
    public string PermissionsJson { get; private set; }
    
    // Navigation property
    public virtual Tenant? Tenant { get; private set; }

    private Role() 
    {
        Name = string.Empty;
        PermissionsJson = "[]";
    }

    public Role(Guid tenantId, string name, string permissionsJson) : base(tenantId)
    {
        Id = Guid.NewGuid();
        Name = name;
        PermissionsJson = permissionsJson;
    }
}
