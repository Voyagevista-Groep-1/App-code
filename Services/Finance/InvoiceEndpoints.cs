using Microsoft.AspNetCore.Antiforgery;

namespace tui.Services;

public static class InvoiceEndpoints
{
    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/finance/invoices").RequireAuthorization(AccessPolicies.Finance);
        group.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            http.Response.Headers.CacheControl = "no-store";
            try
            {
                if (!HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method))
                    await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http);
                return await next(context);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.BadRequest(new { error = "De beveiligingscontrole is verlopen. Haal een nieuw antiforgerytoken op." });
            }
            catch (InvoiceWorkflowException exception)
            {
                return Results.Json(new { error = exception.Message }, statusCode: exception.Status);
            }
            catch (ManualInvoiceValidationException exception)
            {
                return Results.Json(new { error = exception.Message, errors = exception.Errors }, statusCode: exception.Status);
            }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
            catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                http.RequestServices.GetRequiredService<ILogger<InvoiceService>>().LogError(exception, "De factuuraanvraag kon niet worden verwerkt.");
                return Results.Json(new { error = "De factuuraanvraag kon niet worden verwerkt. Probeer het opnieuw." }, statusCode: 503);
            }
        });

        group.MapGet("", async (string? search, string? status, string? workflowStatus, int? page, InvoiceService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetOverviewAsync(search, status, page ?? 1, cancellationToken, workflowStatus)));
        group.MapGet("/{id:int}", async Task<IResult> (int id, InvoiceWorkflowService service, CancellationToken cancellationToken) =>
            await service.GetAsync(id, cancellationToken) is { } invoice ? Results.Ok(invoice) : Results.NotFound(new { error = "De factuur bestaat niet." }));
        group.MapGet("/antiforgery", (IAntiforgery antiforgery, HttpContext context) =>
            Results.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken }));
        group.MapGet("/manual/options", async (ManualInvoiceService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.OptionsAsync(cancellationToken))).RequireAuthorization(AccessPolicies.InvoiceWrite);
        group.MapGet("/manual/{id:int}", async Task<IResult> (int id, ManualInvoiceService service, CancellationToken cancellationToken) =>
            await service.GetAsync(id, cancellationToken) is { } invoice ? Results.Ok(invoice) : Results.NotFound(new { error = "Deze handmatige factuurregistratie bestaat niet." }));
        group.MapPost("/manual", async (ManualInvoiceRequest request, ManualInvoiceService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var invoice = await service.RegisterAsync(context.User, request, cancellationToken);
            return Results.Created($"/api/finance/invoices/manual/{invoice.InvoiceId}", invoice);
        }).RequireAuthorization(AccessPolicies.InvoiceWrite);
        group.MapPost("", async (SaveInvoiceRequest request, InvoiceWorkflowService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var invoice = await service.ExecuteAsync(context.User, "create", null, request, null, cancellationToken);
            return Results.Created($"/api/finance/invoices/{invoice!.InvoiceId}", invoice);
        }).RequireAuthorization(AccessPolicies.InvoiceWrite);
        group.MapPut("/{id:int}", async (int id, SaveInvoiceRequest request, InvoiceWorkflowService service, HttpContext context, CancellationToken cancellationToken) =>
            Results.Ok(await service.ExecuteAsync(context.User, "update", id, request, null, cancellationToken)))
            .RequireAuthorization(AccessPolicies.InvoiceWrite);
        group.MapDelete("/{id:int}", async (int id, long? version, InvoiceWorkflowService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            await service.ExecuteAsync(context.User, "delete", id, null, new(version), cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization(AccessPolicies.InvoiceWrite);
        foreach (var action in new[] { "submit", "approve", "reject" })
        {
            var currentAction = action;
            group.MapPost($"/{{id:int}}/{action}", async (int id, InvoiceActionRequest request, InvoiceWorkflowService service, HttpContext context, CancellationToken cancellationToken) =>
                Results.Ok(await service.ExecuteAsync(context.User, currentAction, id, null, request, cancellationToken)))
                .RequireAuthorization(action == "submit" ? AccessPolicies.InvoiceWrite : AccessPolicies.InvoiceReview);
        }
        return app;
    }
}
