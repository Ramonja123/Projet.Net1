
using System.ComponentModel.DataAnnotations.Schema;
#nullable disable

namespace BackendSGH.Models
{
    public class Facture
    {
        public int Id { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MontantTotal { get; set; }

        public DateTime DateFacture { get; set; } = DateTime.Now;

        public string Statut { get; set; } 


        public int ClientId { get; set; }
        public Client Client { get; set; }


        public int PanierId { get; set; }
        public Panier Panier { get; set; }
    }
}