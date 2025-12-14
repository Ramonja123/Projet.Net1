using System.ComponentModel.DataAnnotations;
#nullable disable

namespace BackendSGH.Models{
    public class Client
{
    public int Id { get; set; }

    [Required]
    public string Nom { get; set; } 

    [Required]
    public string Prenom { get; set; } 

    public string Adresse { get; set; } 

    public DateTime DateNaissance { get; set; } 

    public DateTime DateInscription { get; set; } = DateTime.Now; 

    public int PointsFidelite { get; set; } 

    [Required]
    public string ApplicationUserId { get; set; }
    public ApplicationUser ApplicationUser { get; set; }

    public ICollection<Panier> Paniers { get; set; }
    public ICollection<ReservationChambre> ReservationsChambres { get; set; }
}
}