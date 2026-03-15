# Reglas de Programación y Estándares para Frontend (React + TypeScript) - Sistema Gibag

## 1. Arquitectura y Estructura de React
* **Paradigma:** Usar exclusivamente Componentes Funcionales (Functional Components) y Hooks. Prohibido el uso de Componentes de Clase (Class Components).
* **Estructura de Carpetas:** Seguir un enfoque modular por características (Feature-Sliced Design o similar). 
  * `src/features/inventory/` (Componentes, hooks y servicios específicos de inventario).
  * `src/features/sales/` (Componentes del POS).
  * `src/shared/` (Componentes UI reutilizables como Botones, Modales, Inputs).
* **Estilos:** Utilizar una solución basada en utilidades (como Tailwind CSS) o CSS-in-JS estructurado para evitar colisiones de clases globales.

## 2. Reglas de TypeScript (Tipado Estricto)
* **Modo Estricto:** `tsconfig.json` debe tener `"strict": true`.
* **Prohibición de `any`:** Está ESTRICTAMENTE PROHIBIDO usar el tipo `any`. Si el tipo es desconocido, usar `unknown` y validarlo, o definir la `interface`/`type` correspondiente.
* **Interfaces vs Types:** Usar `interface` para definir la forma de los objetos/modelos y `type` para uniones, tuplas o primitivos.

## 3. Manejo de Estado y Sincronización (Crucial para PWA/POS)
* **Estado de UI Local:** Usar `useState` o `useReducer` nativo de React.
* **Estado Global (UI):** Usar Zustand o Redux Toolkit para estados globales ligeros (ej. Tema oscuro/claro, estado del sidebar, datos del usuario logueado).
* **Estado del Servidor y Caché:** Es OBLIGATORIO usar **TanStack Query (React Query)** para todas las llamadas a la API (GET, POST, PUT, DELETE). Esto maneja automáticamente la caché, reintentos y estados de carga (`isLoading`, `isError`).
* **Estrategia Offline-First (POS):** * Las ventas generadas sin internet deben guardarse localmente en el navegador usando **IndexedDB** (se recomienda la librería `Dexie.js`).
  * Los IDs de las transacciones (Ventas, Clientes) DEBEN generarse en el cliente usando `uuid` (v4) antes de guardarse en IndexedDB o enviarse al servidor. NUNCA esperar a que el servidor asigne el ID.

## 4. Comunicación con la API (Red)
* **Cliente HTTP:** Usar `Axios` (o fetch nativo fuertemente tipado).
* **Interceptors:** Configurar un interceptor global que:
  1. Inyecte automáticamente el token JWT en la cabecera `Authorization: Bearer <token>` en cada petición.
  2. Capture globalmente los errores `401 Unauthorized` para redirigir al usuario a la pantalla de Login y limpiar el almacenamiento local.

## 5. Convenciones de Nomenclatura Frontend
* **Componentes y Archivos de React:** `PascalCase` (Ej: `ProductList.tsx`, `SaleForm.tsx`).
* **Hooks Personalizados:** `camelCase` empezando con "use" (Ej: `useAuth.ts`, `useSyncOfflineSales.ts`).
* **Funciones y Variables:** `camelCase` (Ej: `handleCheckout`, `totalPrice`).