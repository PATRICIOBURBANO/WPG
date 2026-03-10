using System.ComponentModel.DataAnnotations;

namespace AtsManager.Models
{
    public class Empresa
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(13)]
        public string Ruc { get; set; } = string.Empty;

        [Required]
        [MaxLength(300)]
        public string RazonSocial { get; set; } = string.Empty;

        [MaxLength(10)]
        public string? CodEstablecimiento { get; set; }

        [MaxLength(500)]
        public string? Direccion { get; set; }

        public bool Activa { get; set; } = true;
    }
}
