# Instrucciones de Base de Datos: Módulo de Clientes (Sistema Gibag)

## 1. Entidades del Módulo de Clientes

### 1.1. Entidad: `Customer` (Cliente de la Empresa)
* **Descripción:** Personas o empresas que le compran a las sucursales del Tenant.
* **Propiedades:**
  * `Id` (Guid, PK)
  * `TenantId` (Guid, Required)
  * `CustomerType` (Enum: Individual, Company)
  * `FullNameOrCompanyName` (String 200, Required)
  * `TaxId` (String 50, Opcional) - Cédula, NIT o Identificador fiscal del comprador.
  * `Email` (String 150, Opcional)
  * `Phone` (String 50, Opcional)
  * `Address` (String 255, Opcional)
  * `CreatedAt` (DateTimeOffset, Required)
* **Restricciones:** Índice Único Compuesto para `(TenantId, TaxId)` asegurando que una misma empresa no cree dos veces al mismo cliente con la misma cédula/NIT.