using System.ComponentModel.DataAnnotations;
#nullable disable

namespace BackendSGH.Models{
    public class Chambre
{
    public int Id { get; set; }

    [Required]
    public string Numero { get; set; } 

    public string Etat { get; set; } 
    
 

    public int TypeChambreId { get; set; } 
    public TypeChambre TypeChambre { get; set; }
}}