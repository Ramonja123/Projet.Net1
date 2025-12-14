using System.ComponentModel.DataAnnotations.Schema;
#nullable disable

namespace BackendSGH.Models{
    public class Panier
{
    public int Id { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Total { get; set; } 
    
    public string Statut { get; set; } 

    public int ClientId { get; set; }
    public Client Client { get; set; }

    public ICollection<ReservationChambre> ReservationChambres { get; set; }
    public ICollection<ReservationService> ReservationServices { get; set; }
    
    public Facture Facture { get; set; }
}}