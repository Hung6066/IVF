var builder = WebApplication.CreateBuilder(args);

// Add Services
builder.Services.AddControllers();
builder.Services.AddHttpClient();

// Add YARP
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapControllers();
app.MapReverseProxy();
app.MapGet("/", () => "IVF API Gateway Running");

app.Run();
