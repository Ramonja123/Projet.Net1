using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BackendSGH.Models
{
    public class ReservationService
    {
        public int Id { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public TimeSpan Heure { get; set; } // e.g., 14:00

        public int? ClientId { get; set; }
        public Client Client { get; set; }

        // For non-registered clients
        public string? NomClientNonInscrit { get; set; }

        public int ServiceId { get; set; }
        public Service Service { get; set; }

        public string Statut { get; set; } = "Confirmée"; // Confirmée, Annulée

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Prix { get; set; }

        public int? PanierId { get; set; }
        public Panier Panier { get; set; }
    }
}