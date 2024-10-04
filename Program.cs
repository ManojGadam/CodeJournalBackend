using PersonalWebsite.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer;
using PersonalWebsite.Repository;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IProblemRepository, ProblemRepository>();
builder.Services.AddDbContext<ProblemContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins", builder =>
    {
        builder.AllowAnyOrigin() // The origin of your frontend
               .AllowAnyHeader()
               .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
        c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
    });
}
else
{
    app.UseExceptionHandler("/Home/Error");

    // Consider using HSTS in production
    app.UseHsts();

    // Enable HTTPS redirection unless running behind a proxy that handles HTTPS
    app.UseHttpsRedirection();
}


app.UseCors("AllowAllOrigins");
app.UseRouting();


app.UseAuthorization();

app.MapControllers();


app.Run();
