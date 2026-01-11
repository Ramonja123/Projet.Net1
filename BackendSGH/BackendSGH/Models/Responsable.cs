using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
#nullable disable
namespace BackendSGH.Models
{
    public class Responsable
    {
        public int Id { get; set; }

        public string Role { get; set; }

        [Required]
        public string ApplicationUserId { get; set; }

        [ForeignKey("ApplicationUserId")]
        public ApplicationUser ApplicationUser { get; set; }

        public bool IsResponsableChambre { get; set; }

        public int? ServiceId { get; set; }
        public Service Service { get; set; }
    }
}