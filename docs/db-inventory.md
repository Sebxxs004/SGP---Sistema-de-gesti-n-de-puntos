# Instrucciones de Base de Datos: Módulo de Inventario (Sistema Gibag)

## 1. Reglas Globales (Recordatorio para el Agente)
* Todas las entidades deben heredar de una clase base que incluya `Id` (Guid, PK) y `TenantId` (Guid, FK).
* Mantener la configuración del `Global Query Filter` en EF Core para `TenantId`.

## 2. Entidades del Módulo de Inventario

### 2.1. Entidad: `Category` (Categoría de Producto)
* **Descripción:** Agrupación lógica de los productos de una empresa.
* **Propiedades:**
  * `Id` (Guid, PK)
  * `TenantId` (Guid, Required)
  * `Name` (String 100, Required)
  * `Description` (String 255, Opcional)
  * `IsActive` (Bool, Default true)

### 2.2. Entidad: `Product` (Producto Base)
* **Descripción:** El catálogo central de productos de la empresa.
* **Propiedades:**
  * `Id` (Guid, PK)
  * `TenantId` (Guid, Required)
  * `CategoryId` (Guid, Required, FK -> Category)
  * `Name` (String 150, Required)
  * `SKU` (String 50, Required) - Código interno de la empresa.
  * `Barcode` (String 100, Opcional) - Código de barras para escáner POS.
  * `BasePrice` (Decimal 18,2, Required) - Precio de venta general.
  * `Cost` (Decimal 18,2, Required) - Costo de adquisición.
  * `IsActive` (Bool, Default true)
* **Restricciones:** Índice Único Compuesto para `(TenantId, SKU)` y `(TenantId, Barcode)`.

### 2.3. Entidad: `BranchStock` (Inventario por Sucursal)
* **Descripción:** Controla cuántas unidades de un producto hay en una sucursal específica. ¡Crucial para empresas descentralizadas!
* **Propiedades:**
  * `Id` (Guid, PK)
  * `TenantId` (Guid, Required)
  * `BranchId` (Guid, Required, FK -> Branch)
  * `ProductId` (Guid, Required, FK -> Product)
  * `Quantity` (Decimal 18,2, Required) - Cantidad actual (Decimal para soportar pesaje, ej: 1.5 Kg).
  * `MinStockLevel` (Decimal 18,2, Required) - Nivel de alerta para reabastecimiento.
* **Restricciones:** Índice Único Compuesto para `(TenantId, BranchId, ProductId)`.

### 2.4. Entidad: `StockMovement` (Kardex / Historial de Movimientos)
* **Descripción:** Registro inmutable de todo lo que entra o sale del inventario.
* **Propiedades:**
  * `Id` (Guid, PK)
  * `TenantId` (Guid, Required)
  * `BranchId` (Guid, Required, FK -> Branch)
  * `ProductId` (Guid, Required, FK -> Product)
  * `UserId` (Guid, Required, FK -> User) - Quién hizo el movimiento.
  * `MovementType` (Enum: In, Out, Transfer, Adjustment, Sale)
  * `Quantity` (Decimal 18,2, Required) - Cantidad movida (positiva o negativa).
  * `Reference` (String 255, Opcional) - Ej: "Factura de compra #123", "Venta #456".
  * `CreatedAt` (DateTimeOffset, Required)