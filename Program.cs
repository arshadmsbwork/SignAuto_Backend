using PhysicianSignatureAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IDocumentService, DocumentService>();
builder.Services.AddSingleton<IPhysicianService, PhysicianService>();

// Configure CORS for local development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:3000", "http://localhost:3001")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});


var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Use AllowAll for PDF endpoints, AllowNextJs for others
app.UseRouting();
app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => "SignAuto Backend is LIVE 🚀");
app.Run();