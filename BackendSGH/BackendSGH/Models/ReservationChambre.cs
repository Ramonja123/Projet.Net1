using System.ComponentModel.DataAnnotations.Schema;
#nullable disable

namespace BackendSGH.Models{
    public class ReservationChambre
{
    public int Id { get; set; }
    public DateTime DateDebut { get; set; } 
    public DateTime DateFin { get; set; } 
    public string Statut { get; set; } 
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal PrixTotal { get; set; } 
    
    public DateTime DateCreation { get; set; } 

    public int? ClientId { get; set; } 
    public Client Client { get; set; }

    public string NomClientNonInscrit { get; set; }

    public int ChambreId { get; set; }
    public Chambre Chambre { get; set; }

    public int? PanierId { get; set; } 
    public Panier Panier { get; set; }
}}