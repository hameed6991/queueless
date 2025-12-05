using Microsoft.EntityFrameworkCore;
using Queueless.Data;
using Queueless.Services;

var builder = WebApplication.CreateBuilder(args);

// Expose on port 7007
var port = Environment.GetEnvironmentVariable("PORT") ?? "7007"; // 7007 for local dev
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
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
