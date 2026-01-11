
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
#nullable disable

namespace BackendSGH.Models{

public class TypeChambre
{
    public int Id { get; set; }

    [Required]
    public string Nom { get; set; } 

    public string Description { get; set; }

    public int Capacite { get; set; } 
    [Column(TypeName = "decimal(18,2)")] 
    public decimal Tarif { get; set; } 

    public string ImagePath { get; set; } 
    
    public string Vue { get; set; } 

    public ICollection<Chambre> Chambres { get; set; }
}}