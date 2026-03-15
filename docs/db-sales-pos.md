# Instrucciones de Base de Datos: Módulo de Ventas y Caja Fuerte (Sistema Gibag)

## 1. Consideraciones de Sincronización Offline
* El frontend (PWA) generará los Guids (`Id`) de las Ventas y Detalles al estar offline, y los enviará al backend cuando haya internet. El backend DEBE aceptar estos Ids pre-generados.

## 2. Entidades del Módulo POS

### 2.1. Entidad: `CashRegisterSession` (Sesión / Turno de Caja)
* **Descripción:** Controla la apertura y cierre de caja de un cajero en una sucursal.
* **Propiedades:**
  * `Id` (Guid, PK)
  * `TenantId` (Guid, Required)
  * `BranchId` (Guid, Required, FK -> Branch)
  * `UserId` (Guid, Required, FK -> User) - Cajero responsable.
  * `OpenedAt` (DateTimeOffset, Required)
  * `ClosedAt` (DateTimeOffset, Opcional)
  * `InitialBaseAmount` (Decimal 18,2, Required) - Base o sencillo inicial.
  * `ExpectedAmount` (Decimal 18,2, Opcional) - Lo que el sistema calculó al cerrar.
  * `ActualAmount` (Decimal 18,2, Opcional) - Lo que el cajero contó físicamente.
  * `Status` (Enum: Open, Closed)

### 2.2. Entidad: `Sale` (Factura / Cabecera de Venta)
* **Descripción:** Registro principal de una transacción de venta.
* **Propiedades:**
  * `Id` (Guid, PK)
  * `TenantId` (Guid, Required)
  * `BranchId` (Guid, Required, FK -> Branch)
  * `CashRegisterSessionId` (Guid, Required, FK -> CashRegisterSession)
  * `CustomerId` (Guid, Opcional, FK -> Customer) - Null si es "Cliente Final/Mostrador".
  * `UserId` (Guid, Required, FK -> User) - Vendedor.
  * `InvoiceNumber` (String 50, Opcional) - Número secuencial fiscal (generado centralmente).
  * `Subtotal` (Decimal 18,2, Required)
  * `TaxAmount` (Decimal 18,2, Required)
  * `DiscountAmount` (Decimal 18,2, Required)
  * `TotalAmount` (Decimal 18,2, Required)
  * `Status` (Enum: Completed, Voided/Anulada)
  * `CreatedAt` (DateTimeOffset, Required)

### 2.3. Entidad: `SaleDetail` (Detalle de Venta)
* **Descripción:** Los ítems individuales vendidos en una factura.
* **Propiedades:**
  * `Id` (Guid, PK)
  * `TenantId` (Guid, Required)
  * `SaleId` (Guid, Required, FK -> Sale)
  * `ProductId` (Guid, Required, FK -> Product)
  * `Quantity` (Decimal 18,2, Required)
  * `UnitPrice` (Decimal 18,2, Required) - Precio en el momento exacto de la venta (histórico).
  * `Subtotal` (Decimal 18,2, Required)

### 2.4. Entidad: `Payment` (Pago)
* **Descripción:** Cómo pagó el cliente la venta (puede ser pago mixto: mitad efectivo, mitad tarjeta).
* **Propiedades:**
  * `Id` (Guid, PK)
  * `TenantId` (Guid, Required)
  * `SaleId` (Guid, Required, FK -> Sale)
  * `PaymentMethod` (Enum: Cash, CreditCard, DebitCard, Transfer, Other)
  * `Amount` (Decimal 18,2, Required)
  * `ReferenceCode` (String 100, Opcional) - Número de voucher de tarjeta o transferencia.