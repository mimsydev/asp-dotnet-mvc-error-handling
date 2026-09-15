# MVP: ASP.NET Core Error Handling Test Harness

## Context

The user wants a small, self-contained .NET program to experiment with and validate exception-handling patterns in ASP.NET Core. The repo (`project-planning`) is currently empty, so this is a greenfield build. Requirements gathered from the user:

- Async throughout
- MVC pattern (ASP.NET Core Web API using MVC controllers, no Razor views)
- Dependency injection used idiomatically
- Demonstrates current (.NET 10) official best practice for error handling: `IExceptionHandler` + `ProblemDetails` (RFC 7807), rather than the older `/Error`-route/`IExceptionHandlerFeature` pattern
- Controllers call into an injected service layer, and the service layer is what throws domain exceptions (more realistic than throwing directly in controllers)
- Basic xUnit test project included to verify each exception type maps to the expected `ProblemDetails` status code

## Approach

### Solution layout
```
ErrorHandlingMvp.sln
src/ErrorHandlingMvp.Api/
  ErrorHandlingMvp.Api.csproj
  Program.cs
  Controllers/
    WidgetsController.cs
  Services/
    IWidgetService.cs
    WidgetService.cs
  Exceptions/
    NotFoundException.cs
    AppValidationException.cs
    (generic/unexpected exceptions fall through to a catch-all handler)
  ExceptionHandling/
    NotFoundExceptionHandler.cs
    AppValidationExceptionHandler.cs
    FallbackExceptionHandler.cs
tests/ErrorHandlingMvp.Api.Tests/
  ErrorHandlingMvp.Api.Tests.csproj
  ExceptionHandlingTests.cs
```

> Reviewed by devils-advocate; fixes below address: a naming collision with the BCL `ValidationException`, an `[ApiController]` auto-validation gotcha that would silently bypass the custom 400 path, a `WebApplicationFactory<Program>` visibility issue, and an unconditional-fallback requirement.

### Program.cs / DI wiring
- `WebApplication.CreateBuilder`
- `builder.Services.AddControllers()`
- `builder.Services.AddProblemDetails()`
- Register exception handlers in priority order via `AddExceptionHandler<NotFoundExceptionHandler>()`, `AddExceptionHandler<AppValidationExceptionHandler>()`, `AddExceptionHandler<FallbackExceptionHandler>()` (order matters — first match wins, so specific handlers come before the fallback)
- Register `IWidgetService` → `WidgetService` as scoped
- `app.UseExceptionHandler()` (no argument — delegates to registered `IExceptionHandler`s)
- `app.MapControllers()`

### Domain exceptions (`Exceptions/`)
- `NotFoundException(string message)` — maps to 404
- `AppValidationException(string message, IDictionary<string, string[]>? errors = null)` — maps to 400 using `ValidationProblemDetails`. Named to avoid colliding with `System.ComponentModel.DataAnnotations.ValidationException` in the BCL.
- Unhandled/generic exceptions fall through to the fallback handler → 500 `ProblemDetails`

### Exception handlers (`ExceptionHandling/`)
Each implements `IExceptionHandler.TryHandleAsync(HttpContext, Exception, CancellationToken)`:
- Pattern-match on exception type; return `false` immediately if it's not the type this handler owns (so the next registered handler gets a turn)
- On match: set `httpContext.Response.StatusCode`, call `IProblemDetailsService.TryWriteAsync(...)` with a `ProblemDetailsContext`, return `true`
- `FallbackExceptionHandler` is unconditional: it always matches (no type check), always sets 500, and always calls `TryWriteAsync`, returning `true` unconditionally. This must be the last handler registered — if it instead returned `false` for unrecognized types, an unmatched exception would rethrow past `UseExceptionHandler()` with no ProblemDetails at all, defeating the whole point of the MVP.

### Service layer (`Services/`)
- `IWidgetService` with async methods like `GetWidgetAsync(int id)` and `CreateWidgetAsync(WidgetDto dto)`
- `WidgetService` implementation throws `NotFoundException` when an id isn't found (in-memory dictionary is fine — no real persistence needed for this MVP) and `AppValidationException` for a business-rule violation on create (e.g. duplicate widget name, or non-positive quantity) — deliberately *not* a DataAnnotations attribute check, since `[ApiController]` intercepts model-state validation failures and returns its own 400 `ProblemDetails` before the controller/service code ever runs, which would bypass the custom handler for that case
- Injected into the controller via constructor DI

### Controller (`Controllers/WidgetsController.cs`)
- `[ApiController] [Route("api/[controller]")]`
- `GET /api/widgets/{id}` → calls service, lets exceptions propagate (no try/catch — that's the point of global handling)
- `POST /api/widgets` → same
- A `GET /api/widgets/throw/{type}` debug endpoint that deliberately throws `NotFoundException`, `AppValidationException`, or a raw `InvalidOperationException` based on a route parameter — this is the quickest way to manually exercise all three handler paths via curl/Swagger without needing real business logic to fail

### Tests (`tests/ErrorHandlingMvp.Api.Tests/`)
- xUnit project using `Microsoft.AspNetCore.Mvc.Testing`'s `WebApplicationFactory<Program>` for in-process integration tests. Top-level-statement `Program` is `internal` by default, so add `<ItemGroup><InternalsVisibleTo Include="ErrorHandlingMvp.Api.Tests" /></ItemGroup>` to `ErrorHandlingMvp.Api.csproj` or the test project won't compile against it.
- One test per exception type hitting the `/throw/{type}` debug endpoint, asserting:
  - Correct HTTP status code (404 / 400 / 500)
  - Response body deserializes to `ProblemDetails` (or `ValidationProblemDetails`) with expected `Status`/`Title`
- A couple of tests hitting the real widget endpoints (happy path + not-found path) to confirm the service-layer wiring also works end-to-end

## Verification
1. `dotnet build` from the solution root — confirms everything compiles
2. `dotnet run --project src/ErrorHandlingMvp.Api` then manually hit:
   - `GET /api/widgets/throw/notfound` → expect 404 ProblemDetails
   - `GET /api/widgets/throw/appvalidation` → expect 400 ValidationProblemDetails
   - `GET /api/widgets/throw/unknown` → expect 500 ProblemDetails, no stack trace leaked
   - `GET /api/widgets/{validId}` and `GET /api/widgets/{missingId}` → confirm service-layer errors also flow through
3. `dotnet test` — all xUnit tests green
