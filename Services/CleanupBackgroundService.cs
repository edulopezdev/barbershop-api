using System;
using System.Threading;
using System.Threading.Tasks;
using backend.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace backend.Services
{
    /// <summary>
    /// Servicio de fondo que ejecuta limpieza de registros expirados cada 24 horas.
    /// </summary>
    public class CleanupBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CleanupBackgroundService> _logger;
        private Timer? _timer;

        public CleanupBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<CleanupBackgroundService> logger
        )
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CleanupBackgroundService iniciado.");

            // Ejecutar limpieza 5 minutos después del inicio
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            // Crear timer que se ejecute cada 24 horas
            _timer = new Timer(
                callback: async _ => await DoCleanupAsync(),
                state: null,
                dueTime: TimeSpan.FromHours(24),
                period: TimeSpan.FromHours(24)
            );

            // Mantener el servicio ejecutándose
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task DoCleanupAsync()
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var verificacionService =
                        scope.ServiceProvider.GetRequiredService<IVerificacionService>();

                    // Limpiar registros con retención de 7 días
                    var (registrosLimpiados, usuariosEliminados) =
                        await verificacionService.CleanupExpiredVerificationsAsync(
                            daysRetention: 7
                        );

                    if (registrosLimpiados > 0 || usuariosEliminados > 0)
                    {
                        _logger.LogInformation(
                            "Limpieza automática: {RegistrosLimpiados} verificaciones, {UsuariosEliminados} usuarios.",
                            registrosLimpiados,
                            usuariosEliminados
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en limpieza automática de verificaciones.");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("CleanupBackgroundService detenido.");
            _timer?.Dispose();
            await base.StopAsync(cancellationToken);
        }
    }
}
