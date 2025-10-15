using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace backend.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<AuthController> logger
        )
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        // POST: api/auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                _logger.LogWarning(
                    "Intento de login con campos vacíos. Email: '{Email}', Password vacío: {PasswordEmpty}",
                    request.Email ?? "(nulo)",
                    string.IsNullOrEmpty(request.Password)
                );
                return BadRequest(
                    new
                    {
                        status = 400,
                        error = "Bad Request",
                        message = "El email y la contraseña son obligatorios.",
                    }
                );
            }

            var usuario = await _context
                .Usuario.Include(u => u.Rol)
                .SingleOrDefaultAsync(u => u.Email == request.Email);
            if (
                usuario == null
                || string.IsNullOrEmpty(usuario.PasswordHash)
                || !VerifyPassword(request.Password, usuario.PasswordHash)
            )
            {
                _logger.LogWarning("Login fallido para el email: {Email}", request.Email);
                return Unauthorized(
                    new
                    {
                        status = 401,
                        error = "Unauthorized",
                        message = "Email o contraseña incorrectos.",
                    }
                );
            }
            _logger.LogInformation("Usuario logueado exitosamente: {Email}", usuario.Email);
            var token = GenerateJwtToken(usuario);

            return Ok(
                new
                {
                    status = 200,
                    message = "Inicio de sesión exitoso.",
                    token,
                    usuario = new
                    {
                        usuario.Id,
                        usuario.Nombre,
                        usuario.Email,
                        usuario.RolId,
                        RolNombre = usuario.Rol?.NombreRol,
                        usuario.Avatar,
                    },
                }
            );
        }

        // Método auxiliar para verificar contraseña
        private bool VerifyPassword(string password, string hashedPassword)
        {
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }

        // Método auxiliar para generar token JWT
        private string GenerateJwtToken(Usuario usuario)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(
                _configuration["Jwt:Key"]
                    ?? throw new ArgumentNullException("Jwt:Key no está definido")
            );

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Email, usuario.Email ?? ""),
                new Claim(ClaimTypes.Role, usuario.Rol?.NombreRol ?? "Usuario"),
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(3),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature
                ),
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
