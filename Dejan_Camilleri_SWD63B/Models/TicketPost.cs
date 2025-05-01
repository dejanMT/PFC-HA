using Google.Cloud.Firestore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dejan_Camilleri_SWD63B.Models
{
    [FirestoreData]
    public class TicketPost
    {
        [Required]
        [FirestoreProperty]
        public string TicketId { get; set; }

        [Required]
        [FirestoreProperty]
        public string PostTitle { get; set; }

        [Required]
        [FirestoreProperty]
        public string PostDescription { get; set; }

        [FirestoreProperty]
        public string PostAuthor { get; set; }

        [FirestoreProperty]
        public string PostAuthorEmail { get; set; }

        [FirestoreProperty]
        public DateTimeOffset PostDate { get; set; }

        [Required]
        [FirestoreProperty]
        public string Priority { get; set; }



        [FirestoreProperty]
        public bool ClosedTicket { get; set; } = false;

        [FirestoreProperty]
        public string SupportAgent { get; set; }



        [FirestoreProperty("TicketImageUrls")]
        public List<string> TicketImageUrls { get; set; } = new List<string>();

        [NotMapped]
        public IFormFile TicketImage { get; set; }
    }
}
