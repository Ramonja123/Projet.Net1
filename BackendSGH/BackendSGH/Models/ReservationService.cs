using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace BackendSGH.Models
{
    public class ReservationService
    {
        public int Id { get; set; }

        [Required]
        public DateTime DateReservation { get; set; }

        public TimeSpan HeureDebut { get; set; }

        public TimeSpan HeureFin { get; set; }

        public int Quantite { get; set; } = 1; 

        [Column(TypeName = "decimal(18,2)")]
        public decimal Prix { get; set; }

        public string Statut { get; set; } 


        public int ClientId { get; set; }
        public Client Client { get; set; }

        public int ServiceId { get; set; }
        public Service Service { get; set; }

        public int? PanierId { get; set; }
        public Panier Panier { get; set; }
    }
}