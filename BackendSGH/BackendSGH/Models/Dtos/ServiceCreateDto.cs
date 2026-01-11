using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace BackendSGH.Models.Dtos
{
    public class ServiceCreateDto
    {
        [Required]
        public string Nom { get; set; }
        public string Description { get; set; }
        public decimal? Prix { get; set; }
        public string TypeService { get; set; }
        public List<IFormFile>? Images { get; set; }
    }
}
