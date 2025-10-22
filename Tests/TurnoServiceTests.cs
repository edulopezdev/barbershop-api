using backend.Data;
using backend.Extensions;
using backend.Models;
using backend.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace backend.Tests
{
    public class TurnoServiceTests
    {
        private readonly TurnoService _turnoService;
        private readonly Mock<ApplicationDbContext> _mockContext;

        public TurnoServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDatabase")
                .Options;

            _mockContext = new Mock<ApplicationDbContext>(options);
            _turnoService = new TurnoService(_mockContext.Object);
        }
    }
}