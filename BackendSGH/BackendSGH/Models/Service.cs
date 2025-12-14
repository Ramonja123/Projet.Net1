using System.ComponentModel.DataAnnotations.Schema;
#nullable disable

namespace BackendSGH.Models{
    public class Service
{
    public int Id { get; set; }
    public string Nom { get; set; } 
    public string Description { get; set; } 
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Prix { get; set; } 
    
    public string TypeService { get; set; } 
}}