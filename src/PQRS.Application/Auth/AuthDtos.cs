namespace PQRS.Application.Auth;

//Datos para registrar un nuevo agente (uso administrativo/setup)
public record RegisterAgentRequest(Guid TenantId, string FullName, string Email, string Password);

//Credenciales de login de un agente
public record LoginRequest(string Email, string Password);

//Respuesta tras un login exitoso
public record LoginResponse(string Token, DateTime ExpiresAtUtc, string FullName, string Role);