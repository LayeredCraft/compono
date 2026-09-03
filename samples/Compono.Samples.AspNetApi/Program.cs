using Compono.Samples.AspNetApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
builder.Services.AddSingleton<OrderService>();
builder.Services.AddHttpClient<ShippingClient>(client => client.BaseAddress = new Uri("https://shipping.example.com/"));

var app = builder.Build();

app.MapPost("/orders", async (PlaceOrder command, OrderService service, CancellationToken cancellationToken) =>
{
    var order = await service.PlaceAsync(command, cancellationToken);
    return Results.Created($"/orders/{order.Id}", order);
});

app.Run();

/// <summary>
/// Top-level statements generate an internal <c>Program</c> class by default -
/// <c>WebApplicationFactory&lt;T&gt;</c> in <c>samples/Compono.Samples.AspNetApi.Tests</c> needs it
/// public to reference from another assembly, the standard ASP.NET Core minimal-API testing
/// pattern.
/// </summary>
public partial class Program;
