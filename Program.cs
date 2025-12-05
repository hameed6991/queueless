using Microsoft.EntityFrameworkCore;
using Queueless.Data;
using Queueless.Services;

var builder = WebApplication.CreateBuilder(args);

// Expose on port 7007 (Render will override PORT to its own value)
var port = Environment.GetEnvironmentVariable("PORT") ?? "7007";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// --------------------------------------
// 1. Register services
// --------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DbContext – uses "DefaultConnection" from appsettings.json
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

// Your existing services
builder.Services.AddScoped<ICustomerHomeService, CustomerHomeService>();
builder.Services.AddScoped<ICustomerQueueService, CustomerQueueService>();

// FCM + background worker
builder.Services.AddSingleton<FcmService>();
builder.Services.AddHostedService<QueueAlertWorker>();

// --------------------------------------
// 2. Build the app
// --------------------------------------
var app = builder.Build();

// --------------------------------------
// 3. Configure HTTP pipeline
// --------------------------------------

// ✅ Always enable Swagger (Development + Production)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Queueless API v1");
    // Optional: uncomment this to show Swagger directly at "/"
    // c.RoutePrefix = string.Empty;
});

// Optional: if HTTPS redirection gives trouble on Render, you can comment this out.
// app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
