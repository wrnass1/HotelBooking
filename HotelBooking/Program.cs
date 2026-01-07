using Microsoft.EntityFrameworkCore;
using HotelBooking.Data;
using HotelBooking.Repositories;
using HotelBooking.Repositories.Interfaces;
using HotelBooking.Services;
using HotelBooking.Services.Interfaces;
using FluentValidation;
using FluentValidation.AspNetCore;
using HotelBooking.Validators;
using HotelBooking.Models.DTO;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=HotelBooking.db";

builder.Services.AddDbContext<HotelBookingDbContext>(options =>
    options.UseSqlite(connectionString));

// Repositories
builder.Services.AddScoped<IHotelRepository, HotelRepository>();
builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();

// Services
builder.Services.AddScoped<IHotelService, HotelService>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IBookingService, BookingService>();

// AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile));

// FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<CreateHotelDtoValidator>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Hotel Booking API V1");
    c.RoutePrefix = "swagger"; // Swagger UI at /swagger
});

// Only use HTTPS redirection if HTTPS is properly configured
if (app.Environment.IsDevelopment() || !string.IsNullOrEmpty(builder.Configuration["ASPNETCORE_HTTPS_PORT"]))
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<HotelBookingDbContext>();
    context.Database.EnsureCreated();
}

app.Run();
