# Plataforma SaaS Multi-tenant de Gestión de PQRS con IA

Plataforma que permite a múltiples empresas centralizar y automatizar la gestión de sus Peticiones, Quejas, Reclamos y Sugerencias (PQRS), mediante un widget embebible con asistente RAG y triaje automático por IA.

## Arquitectura

- **Backend**: ASP.NET Core (.NET 10) con Clean Architecture (Domain, Application, Infrastructure, API)
- **Base de datos**: PostgreSQL 16 + pgvector (búsqueda vectorial)
- **IA**: 
  - Embeddings generados localmente vía **Ollama** (modelo `all-minilm`, 384 dimensiones)
  - Chat, síntesis de respuestas RAG y triaje automático vía **Groq** (API compatible con OpenAI)
- **Widget**: JavaScript vanilla con Shadow DOM, sin dependencias externas
- **Panel de administración**: HTML/JS vanilla, conectado en tiempo real vía SignalR
- **Contenedores**: Docker Compose (API + PostgreSQL + Ollama)
- **Despliegue**: AWS EC2 (Ubuntu) + nginx para archivos estáticos

## Despliegue en producción

El sistema está desplegado en AWS EC2 y accesible públicamente:

- **API**: http://13.218.226.215:5219
- **Widget de demostración**: http://13.218.226.215/test.html
- **Panel de agentes**: http://13.218.226.215/admin.html

## Requisitos previos (para correr localmente)

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- Una cuenta y API Key de [Groq](https://console.groq.com/keys) (gratuita)

## Puesta en marcha local

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

### 4. Aplicar las migraciones de base de datos

```bash
dotnet ef database update --project src/PQRS.Infrastructure --startup-project src/PQRS.Api
```

### 5. Verificar que todo funciona

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

## Módulo de Inteligencia Artificial

### RAG (Retrieval-Augmented Generation)

1. El widget envía la pregunta del usuario a `/api/v1/widget/rag-search`
2. La API genera el embedding de la pregunta usando Ollama (modelo `all-minilm`)
3. Se busca por similitud coseno en la tabla `KnowledgeBaseArticles`, filtrado por Tenant, usando el índice vectorial HNSW de pgvector
4. Si la similitud supera el umbral (0.5), se sintetiza una respuesta con Groq usando únicamente los artículos recuperados como contexto
5. Si no hay coincidencia suficiente, se escala al formulario de radicación

### Triaje automático

Al crear un ticket, el asunto y la descripción se envían a Groq (modo JSON estructurado) para clasificar automáticamente:
- **Tipo**: Petición, Queja, Reclamo o Sugerencia
- **Prioridad**: Baja, Media o Alta
- **Sentimiento**: Positivo, Neutro o Negativo
- **Resumen ejecutivo**: síntesis de 1-2 oraciones

Si el ticket resulta de prioridad Alta o sentimiento Negativo, se dispara una notificación en tiempo real vía SignalR a los agentes conectados de ese Tenant.

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
1. **Chat RAG**: el usuario escribe una pregunta; si existe un artículo de la base de conocimiento suficientemente similar, se sintetiza una respuesta automática, con botones "¿Te ayudó? Sí/No".
2. **Formulario**: si no hay respuesta satisfactoria, se muestra un formulario de radicación de PQRS, que al enviarse pasa por triaje automático de IA.

## Panel de administración (admin.html)

Panel web para agentes, sin dependencias de build, que permite:
- Iniciar sesión (JWT)
- Ver y filtrar tickets por estado/prioridad, con triaje de IA visible (tipo, prioridad, sentimiento, resumen)
- Actualizar el estado de un ticket en vivo
- Recibir notificaciones en tiempo real de tickets críticos (SignalR)
- Crear y ver artículos de la base de conocimiento
- Crear nuevas empresas (Tenants) y registrar su primer agente

## Aislamiento Multi-tenant

- **Peticiones del widget** (sin login): se identifican mediante el header `X-Widget-Api-Key`, validado por un middleware contra la tabla `Tenants`.
- **Peticiones de agentes** (con login): el `TenantId` viaja como Claim dentro del JWT, firmado y verificado en cada petición.
- Todas las consultas a la base de datos filtran explícitamente por `TenantId`, garantizando que ninguna empresa pueda ver datos de otra.
- **CORS dinámico**: un middleware valida el header `Origin` de cada petición del widget contra el `AllowedDomain` configurado para ese Tenant específico.

## Estructura del proyecto

pqrs-saas/
├── docker-compose.yml
├── .env # Secretos (no versionado)
├── src/
│ ├── PQRS.Domain/ # Entidades del negocio
│ ├── PQRS.Application/ # DTOs, interfaces de casos de uso
│ ├── PQRS.Infrastructure/ # EF Core, integración con IA (Ollama + Groq)
│ └── PQRS.Api/ # Endpoints, middleware, autenticación, SignalR
└── widget/
├── pqrs-widget.js # Widget embebible
├── test.html # Página de prueba (landing page de demo)
└── admin.html # Panel de administración para agentes


## Decisiones técnicas relevantes

- **Ollama en vez de OpenAI/Groq para embeddings**: la cuenta institucional de OpenAI disponible no tenía acceso a modelos de embeddings, y Groq tampoco lo ofrecía en el tier disponible. Se optó por correr un modelo de embeddings localmente (Ollama, `all-minilm`, 384 dimensiones), eliminando la dependencia de terceros para esa funcionalidad.
- **Groq para chat/triaje**: modelo `openai/gpt-oss-20b`, elegido por ser rápido, gratuito en su tier, y compatible con el mismo formato de API que OpenAI (mismo SDK de .NET, solo cambia la URL base).
- **Umbral de similitud ajustado a 0.5**: calibrado empíricamente para el modelo `all-minilm`, que produce scores de similitud más conservadores que modelos más grandes.
