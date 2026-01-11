using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace BackendSGH.Models.Dtos
{
    public class TypeChambreCreateDto
    {
        [Required]
        public string Nom { get; set; }
        public string Description { get; set; }
        public int Capacite { get; set; }
        public decimal Tarif { get; set; }
        public string Vue { get; set; }
        
        public List<IFormFile> Images { get; set; } 
    }
}
