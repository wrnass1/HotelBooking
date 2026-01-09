using FluentAssertions;
using HotelBooking.Data;
using HotelBooking.Models.Entities;
using HotelBooking.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelBooking.Tests.Repositories;

public class HotelRepositoryTests : IDisposable
{
    private readonly HotelBookingDbContext _context;
    private readonly HotelRepository _repository;

    public HotelRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<HotelBookingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new HotelBookingDbContext(options);
        _repository = new HotelRepository(_context);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateHotel()
    {
        // Arrange
        var hotel = new Hotel
        {
            Name = "Test Hotel",
            Address = "Test Address",
            City = "Test City",
            Country = "Test Country",
            StarRating = 5
        };

        // Act
        var result = await _repository.CreateAsync(hotel);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.Name.Should().Be("Test Hotel");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnHotel_WhenExists()
    {
        // Arrange
        var hotel = new Hotel
        {
            Name = "Test Hotel",
            Address = "Test Address",
            City = "Test City",
            Country = "Test Country",
            StarRating = 5
        };
        await _repository.CreateAsync(hotel);

        // Act
        var result = await _repository.GetByIdAsync(hotel.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test Hotel");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Act
        var result = await _repository.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateHotel()
    {
        // Arrange
        var hotel = new Hotel
        {
            Name = "Test Hotel",
            Address = "Test Address",
            City = "Test City",
            Country = "Test Country",
            StarRating = 5
        };
        await _repository.CreateAsync(hotel);
        hotel.Name = "Updated Hotel";

        // Act
        var result = await _repository.UpdateAsync(hotel);

        // Assert
        result.Name.Should().Be("Updated Hotel");
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnTrue_WhenHotelExists()
    {
        // Arrange
        var hotel = new Hotel
        {
            Name = "Test Hotel",
            Address = "Test Address",
            City = "Test City",
            Country = "Test Country",
            StarRating = 5
        };
        await _repository.CreateAsync(hotel);

        // Act
        var result = await _repository.DeleteAsync(hotel.Id);

        // Assert
        result.Should().BeTrue();
        var deleted = await _repository.GetByIdAsync(hotel.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenHotelNotExists()
    {
        // Act
        var result = await _repository.DeleteAsync(999);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenHotelExists()
    {
        // Arrange
        var hotel = new Hotel
        {
            Name = "Test Hotel",
            Address = "Test Address",
            City = "Test City",
            Country = "Test Country",
            StarRating = 5
        };
        await _repository.CreateAsync(hotel);

        // Act
        var result = await _repository.ExistsAsync(hotel.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenHotelNotExists()
    {
        // Act
        var result = await _repository.ExistsAsync(999);

        // Assert
        result.Should().BeFalse();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
