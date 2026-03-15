# Reglas de Seguridad, Pruebas y Protocolo de Ejecución - Sistema Gibag

## 1. Seguridad y Criptografía (Backend)
* **Hashing de Contraseñas:** ESTRICTAMENTE PROHIBIDO guardar contraseñas en texto plano o usar algoritmos débiles (MD5, SHA1, SHA256 simple). Usar **BCrypt** o **Argon2** (ej. mediante la librería `BCrypt.Net-Next`).
* **CORS (Cross-Origin Resource Sharing):** La API debe tener una política de CORS configurada y restrictiva. En desarrollo (`appsettings.Development.json`) puede permitir `localhost`, pero en producción debe leer los orígenes permitidos desde las variables de entorno.
* **Secretos y Variables de Entorno:** NINGÚN secreto (Cadenas de conexión, JWT Secrets, API Keys) debe estar quemado (hardcoded) en el código. Todo debe leerse desde `IConfiguration` (appsettings.json o Variables de Entorno en Docker).
* **JWT (JSON Web Tokens):** Los tokens deben tener un tiempo de expiración corto (ej. 1 hora) y el sistema debe estar preparado conceptualmente para manejar *Refresh Tokens*. El `TenantId` y el `UserId` deben estar incluidos en los *Claims* del JWT.

## 2. Estrategia de Pruebas (Testing)
* **Backend (C#):**
  * Framework de Pruebas: `xUnit`.
  * Aserciones (Assertions): Usar `FluentAssertions` para que las pruebas sean legibles.
  * Mocks: Usar `Moq` o `NSubstitute` para simular dependencias externas (bases de datos, APIs).
  * Enfoque: Priorizar pruebas unitarias en la capa de `Domain` (entidades) y `Application` (Casos de uso / Command Handlers).
* **Frontend (React):**
  * Framework: `Vitest` (recomendado si se usa Vite) o `Jest`.
  * Testing de Componentes: Usar `React Testing Library`. Enfocarse en probar el comportamiento del usuario, no los detalles de implementación internos del componente.

## 3. Entorno de Desarrollo (Docker)
* El proyecto debe incluir un `docker-compose.yml` en la raíz que levante la base de datos PostgreSQL de desarrollo automáticamente, junto con un pgAdmin o DBeaver para administrarla.

## 4. PROTOCOLO DE EJECUCIÓN (Instrucción estricta para el Agente de IA)
* **Paso a Paso:** NUNCA intentes generar todo el proyecto o un módulo completo en una sola respuesta.
* **Confirmación:** Al recibir una tarea, primero analiza, propón los archivos a crear/modificar y ESPERA la confirmación del usuario antes de escribir el código fuente.
* **Completitud:** Cuando generes código, escribe las clases completas. NUNCA uses comentarios perezosos como `// ... resto del código aquí` o `// implementar lógica`. Si vas a crear el archivo, créalo listo para compilar.