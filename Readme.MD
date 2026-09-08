# Plataforma SaaS Multi-tenant de Gestión de PQRS con IA

Plataforma que permite a múltiples empresas centralizar y automatizar la gestión de sus Peticiones, Quejas, Reclamos y Sugerencias (PQRS), mediante un widget embebible con asistente RAG y triaje automático por IA.

## Arquitectura

- **Backend**: ASP.NET Core (.NET 10) con Clean Architecture (Domain, Application, Infrastructure, API)
- **Base de datos**: PostgreSQL 16 + pgvector (búsqueda vectorial)
- **IA**: 
  - Embeddings generados localmente vía **Ollama** (modelo `all-minilm`, 384 dimensiones)
  - Chat, síntesis de respuestas RAG y triaje automático vía **Groq** (API compatible con OpenAI)
- **Widget**: JavaScript vanilla con Shadow DOM, sin dependencias externas
- **Contenedores**: Docker Compose (API + PostgreSQL + Ollama)

## Requisitos previos

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- Una cuenta y API Key de [Groq](https://console.groq.com/keys) (gratuita)

## Puesta en marcha

### 1. Clonar el repositorio

```bash
git clone <url-del-repo>
cd pqrs-saas
```

### 2. Configurar variables de entorno

Crea un archivo `.env` en la raíz del proyecto con el siguiente contenido:

```env
GROQ_API_KEY=tu_api_key_de_groq
JWT_SECRET_KEY=una_clave_aleatoria_de_al_menos_32_caracteres
```

Para generar una clave JWT segura, puedes usar PowerShell:
```powershell
[Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))
```

### 3. Levantar todo el sistema

```bash
docker compose up -d --build
```

Este comando levanta 3 contenedores:
- `pqrs-db`: PostgreSQL con pgvector (puerto 5432)
- `pqrs-ollama`: motor de embeddings locales (puerto 11434)
- `pqrs-api`: backend de la aplicación (puerto 5219)

**Primera vez únicamente**: descarga el modelo de embeddings dentro de Ollama:

```bash
docker exec -it pqrs-ollama ollama pull all-minilm
```

### 4. Verificar que todo funciona

```bash
curl http://localhost:5219/health
```

Debe responder: `{"status":"ok","database":"connected"}`

## Uso de la API

### Crear una empresa (Tenant)

```bash
curl -X POST http://localhost:5219/api/v1/tenants \
  -H "Content-Type: application/json" \
  -d '{"name":"Mi Empresa","allowedDomain":"https://miempresa.com"}'
```

La respuesta incluye un `widgetApiKey` — guárdalo, es el que usará el widget para identificar a esta empresa.

### Registrar un agente

```bash
curl -X POST http://localhost:5219/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{"tenantId":"<id-del-tenant>","fullName":"Nombre Agente","email":"agente@empresa.com","password":"Clave123!"}'
```

### Iniciar sesión

```bash
curl -X POST http://localhost:5219/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"agente@empresa.com","password":"Clave123!"}'
```

Devuelve un token JWT a usar en el header `Authorization: Bearer <token>` para los endpoints protegidos.

### Endpoints principales

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| POST | `/api/v1/tenants` | — | Crear empresa |
| POST | `/api/v1/auth/register` | — | Registrar agente |
| POST | `/api/v1/auth/login` | — | Iniciar sesión |
| GET | `/api/v1/tickets?status=&priority=` | JWT | Listar tickets (con filtros) |
| GET | `/api/v1/tickets/{id}` | JWT | Ver un ticket |
| PUT | `/api/v1/tickets/{id}/status` | JWT | Actualizar estado/asignación |
| POST/GET/PUT/DELETE | `/api/v1/kb-articles` | JWT | CRUD de artículos de conocimiento |
| POST | `/api/v1/widget/rag-search` | API Key | Consulta RAG (fase chat) |
| POST | `/api/v1/widget/tickets` | API Key | Radicar PQRS (fase formulario) |

Los endpoints de `/api/v1/widget/*` requieren el header `X-Widget-Api-Key` con el `widgetApiKey` del tenant.

## Incrustar el widget en un sitio externo

Agrega esta línea antes de cerrar `</body>` en cualquier sitio HTML:

```html
<script
  src="ruta/a/pqrs-widget.js"
  data-tenant="TU_WIDGET_API_KEY"
  data-api-url="http://localhost:5219"
  data-company-name="Nombre de tu empresa"
  data-primary-color="#4f46e5">
</script>
```

### Atributos disponibles

| Atributo | Obligatorio | Descripción |
|---|---|---|
| `data-tenant` | Sí | API Key del tenant (obtenida al crear la empresa) |
| `data-api-url` | No | URL base del backend (por defecto `http://localhost:5219`) |
| `data-company-name` | No | Nombre mostrado en el header del widget |
| `data-primary-color` | No | Color principal en formato hex, para adaptar el branding |

El widget funciona en dos fases:
1. **Chat RAG**: el usuario escribe una pregunta; si existe un artículo de la base de conocimiento suficientemente similar, se sintetiza una respuesta automática.
2. **Formulario**: si no hay respuesta satisfactoria, se muestra un formulario de radicación de PQRS, que al enviarse pasa por triaje automático de IA (tipo, prioridad, sentimiento, resumen).

## Desarrollo local (sin Docker para la API)

Si prefieres correr la API con `dotnet run` en vez de Docker (más rápido para iterar en desarrollo):

```bash
docker compose up -d db ollama   # solo base de datos y embeddings en Docker
dotnet user-secrets set "Groq:ApiKey" "tu_key" --project src/PQRS.Api
dotnet run --project src/PQRS.Api
```

En este modo, la API usa `appsettings.json` (con `Host=localhost` en la cadena de conexión) en vez de las variables de entorno del `docker-compose.yml`.

## Estructura del proyecto

```
pqrs-saas/
├── PQRS.slnx
├── docker-compose.yml        db + ollama + api, 1 solo comando
├── .env / .gitignore          secretos protegidos
├── README.md                   documentación completa, probada reproducible
├── src/
│   ├── PQRS.Domain            Tenant, User, KnowledgeBaseArticle, Ticket
│   ├── PQRS.Application         DTOs, interfaces
│   ├── PQRS.Infrastructure      EF Core + pgvector + Ollama + Groq
│   └── PQRS.Api                 
│       ├── Auth (JWT + API Key widget) + CORS dinámico por tenant
│       ├── CRUD KB Articles con embeddings automáticos
│       ├── Tickets: crear (triaje IA), listar (filtros), actualizar
│       ├── RAG search funcionando end-to-end
│       └── SignalR: notificaciones en tiempo real de tickets críticos
└── widget/
    ├── pqrs-widget.js          chat RAG + formulario, diseño profesional
    ├── test.html                landing page de demo
    └── agent-notifications-test.html   demo de notificaciones en vivo

## Comandos para probar

$body = @{
    customerName      = "Roberto Fernández"
    customerEmail     = "roberto@correo.com"
    subject           = "Pregunta sobre el servicio"
    description       = "Quisiera saber más detalles sobre sus planes disponibles."
    originatedFromRag = $false
} | ConvertTo-Json

$bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($body)

Invoke-RestMethod -Uri "http://localhost:5219/api/v1/widget/tickets" -Method Post -ContentType "application/json; charset=utf-8" -Headers @{ "X-Widget-Api-Key" = "TU_WIDGET_API_KEY_DE_LA_EMPRESA_NUEVA" } -Body $bodyBytes

# Ver las tablas y columnas reales dentro de Postgres (pgvector incluido)

bash
docker exec -it pqrs-db psql -U pqrs_user -d pqrs_saas -c "\d \"KnowledgeBaseArticles\""

# Confirmar el índice vectorial HNSW (requisito explícito del documento)

bash
docker exec -it pqrs-db psql -U pqrs_user -d pqrs_saas -c "\di"

# Login y generación de JWT con claim de tenant

powershell
$r = Invoke-RestMethod -Uri "http://localhost:5219/api/v1/auth/login" -Method Post -ContentType "application/json" -Body '{"email":"ana@empresademo.com","password":"Clave123!"}'
$r.token

# Crear un ticket y ver el triaje de IA calculado en tiempo real

$body = @{
    customerName = "Demo"
    customerEmail = "demo@x.com"
    subject = "Prueba"
    description = "Esto es una prueba de triaje automatico."
    originateFromRag = $false
} | ConvertTo-Json
$bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
Invoke-RestMethod -Uri "http://13.218.226.215:5219/api/v1/widget/tickets" -Method Post -ContentType "application/json; charset=utf-8" -Headers @{ "X-Widget-Api-Key" = "db56f49d2d5f49db875c3936ebe2d3e1" } -Body $bytes'

# Probar RAG (búsqueda semántica + síntesis con IA)

$body = @{ question = "cual es el horario de atencion" } | ConvertTo-Json
$bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
Invoke-RestMethod -Uri "http://localhost:5219/api/v1/widget/rag-search" -Method Post -ContentType "application/json; charset=utf-8" -Headers @{ "X-Widget-Api-Key" = "db56f49d2d5f49db875c3936ebe2d3e1" } -Body $bytes

# Confirmar aislamiento multi-tenant a nivel de base de datos

bash
1 docker exec -it pqrs-db psql -U pqrs_user -d pqrs_saas 
2 SELECT "TenantId", COUNT(*) FROM "Tickets" GROUP BY "TenantId"; 
