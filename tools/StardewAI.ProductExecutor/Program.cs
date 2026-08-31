using System.Text.Json.Nodes;
using StardewAI.Contracts.Capabilities;

namespace StardewAI.ProductExecutor;

public static class Program
{
    public static void Main(string[] args)
    {
        BuildApplication(args).Run();
    }

    public static WebApplication BuildApplication(string[] args)
    {
        var options = ProductExecutorOptions.FromEnvironment();
        options.Validate();
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls(options.ListenUrl);
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<ProductExecutionPolicy>();
        builder.Services.AddSingleton<ProductReceiptStore>();
        builder.Services.AddSingleton(new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(options.NativeTimeoutSeconds)
        });
        builder.Services.AddSingleton<ProductExecutionService>();
        var app = builder.Build();

        app.MapGet("/health", () => Results.Json(new
        {
            schema_version = "stardewai.product_executor_health.v1",
            status = "ready",
            product_executor_count = ProductExecutorCapabilityCatalog.OptionIds.Count,
            native_executor_url = options.NativeExecutorUrl,
            bridge_snapshot_url = options.BridgeSnapshotUrl,
            run_lock = string.IsNullOrWhiteSpace(options.RequiredRunId) ? "dynamic_nonempty" : "configured_exact",
            concurrency = 1
        }));
        app.MapGet("/api/v1/product/capabilities", () => Results.Json(new
        {
            schema_version = "stardewai.product_executor_capabilities.v1",
            count = ProductExecutorCapabilityCatalog.OptionIds.Count,
            option_ids = ProductExecutorCapabilityCatalog.OptionIds
        }));
        app.MapPost("/api/v1/product/execute", async (
            HttpRequest httpRequest,
            ProductExecutionService service,
            CancellationToken cancellationToken) =>
        {
            JsonObject? request;
            try
            {
                request = JsonNode.Parse(await new StreamReader(httpRequest.Body).ReadToEndAsync(cancellationToken))?.AsObject();
            }
            catch
            {
                request = null;
            }
            if (request is null)
                return Results.BadRequest(new { error = "request_json_object_required" });
            return Results.Json(await service.ExecuteAsync(request, cancellationToken));
        });
        return app;
    }
}
