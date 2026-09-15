# Error Handling MVP Tasks

## Foundation

- [x] Create or confirm the `ErrorHandlingMvp.sln` solution contains the API project and the xUnit integration-test project. Verify with `dotnet build` from the solution root.
- [x] Configure the API project for the intended .NET target framework and MVC controller support. Verify the project builds without warnings that block execution.
- [x] Add the required test dependencies, including `Microsoft.AspNetCore.Mvc.Testing`, and configure the API assembly to expose `Program` to the test assembly with `InternalsVisibleTo`. Verify a minimal `WebApplicationFactory<Program>` test compiles.

## Application Pipeline

- [ ] Configure `Program.cs` to add controllers and RFC 7807 `ProblemDetails` services. Verify the application starts and an unmapped API route returns a standard problem-details response where applicable.
- [ ] Register `IWidgetService` and `WidgetService` with scoped lifetime. Verify application startup succeeds with DI validation enabled in development.
- [ ] Register the exception handlers in this exact order: not found, application validation, fallback; add `UseExceptionHandler()` before endpoint mapping. Verify the pipeline starts and controller routes remain reachable.

## Domain Error Contract

- [ ] Add `NotFoundException`, carrying a clear user-safe message for missing domain resources. Verify it compiles and can be thrown from a unit or integration test fixture.
- [ ] Add `AppValidationException`, including an optional `IDictionary<string, string[]>` of field errors. Verify callers can create it both with and without an error dictionary.
- [ ] Establish safe public titles and details for 404, 400, and 500 responses; ensure the 500 contract contains no exception message or stack-trace data. Verify the expected response values in focused handler tests.

## Exception Handlers

- [ ] Implement `NotFoundExceptionHandler` using `IExceptionHandler` and `IProblemDetailsService`; return `false` for exceptions it does not own and write a 404 problem response when it matches. Verify it handles a thrown `NotFoundException` end to end.
- [ ] Implement `AppValidationExceptionHandler` to write a 400 `ValidationProblemDetails` response, including supplied field errors. Verify response deserialization and error-key preservation in an integration test.
- [ ] Implement `FallbackExceptionHandler` as the final, unconditional handler that always writes a safe 500 problem response and returns `true`. Verify an `InvalidOperationException` does not escape the exception middleware or reveal internal details.

## Widget Service

- [ ] Define async `IWidgetService` operations for retrieving a widget and creating one, along with request/response DTOs appropriate for the API. Verify controller compilation against the interface.
- [ ] Implement an in-memory `WidgetService` with at least one known widget for happy-path retrieval. Verify a service or integration test returns the expected widget.
- [ ] Make missing widget retrieval throw `NotFoundException`. Verify `GET /api/widgets/{missingId}` returns the global 404 problem response rather than a controller-local response.
- [ ] Enforce a service-layer creation rule such as duplicate name or non-positive quantity, throwing `AppValidationException` on violation. Verify the API returns the custom validation-problem response; do not rely on DataAnnotations model-state validation for this path.

## MVC API Surface

- [ ] Add `WidgetsController` as an `[ApiController]` route at `api/widgets`, with constructor-injected `IWidgetService`. Verify route discovery by calling a basic endpoint.
- [ ] Implement the async `GET /api/widgets/{id}` endpoint and return its successful result without local exception handling. Verify both a valid ID and a missing ID.
- [ ] Implement the async `POST /api/widgets` endpoint and return an appropriate successful creation response without local exception handling. Verify a successful create and a business-rule failure.
- [ ] Add `GET /api/widgets/throw/{type}` solely for development/test harness coverage of not-found, application-validation, and unexpected exception paths. Verify each supported type reaches its intended global handler.

## Integration Tests

- [ ] Add a `WebApplicationFactory<Program>` test fixture that creates an in-process API client. Verify the fixture runs without needing a separately hosted server.
- [ ] Add a test for the debug not-found path that asserts HTTP 404 and a deserializable `ProblemDetails` body with the expected status and title.
- [ ] Add a test for the debug application-validation path that asserts HTTP 400 and a deserializable `ValidationProblemDetails` body, including errors when supplied.
- [ ] Add a test for the debug unexpected-error path that asserts HTTP 500, a deserializable `ProblemDetails` body, and no leaked exception or stack-trace content.
- [ ] Add widget endpoint tests covering successful retrieval, missing-widget 404 mapping, successful creation, and service-rule 400 mapping. Verify all tests pass with `dotnet test`.

## Final Verification

- [ ] Run `dotnet build` from the solution root and resolve all compilation failures.
- [ ] Run `dotnet test` from the solution root and ensure every integration test passes.
- [ ] Run the API locally and manually verify `/api/widgets/throw/notfound`, `/api/widgets/throw/appvalidation`, and `/api/widgets/throw/unknown` return 404, 400, and safe 500 problem responses respectively.
- [ ] Manually verify a valid widget route and a missing widget route to confirm service-layer exceptions flow through the global handlers.
