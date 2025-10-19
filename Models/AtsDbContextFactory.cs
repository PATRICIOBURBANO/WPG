using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

// Asegúrate de que este using esté incluido
using AtsManager.Models;

namespace AtsManager.Models
{
    // Esta clase le indica a las herramientas de migración de EF Core 
    // dónde encontrar la cadena de conexión en tiempo de diseño.
    public class AtsDbContextFactory : IDesignTimeDbContextFactory<AtsDbContext>
    {
        public AtsDbContext CreateDbContext(string[] args)
        {
            // 1. Configurar dónde buscar appsettings.json
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            // 2. Obtener la cadena de conexión
            var connectionString = configuration.GetConnectionString("ATS_DB_Connection");

            // 3. Crear el DbContextOptions (sin el método TrustServerCertificate)
            var builder = new DbContextOptionsBuilder<AtsDbContext>();
            builder.UseSqlServer(connectionString);

            // 4. Devolver la instancia del contexto
            return new AtsDbContext(builder.Options);
        }
    }
}