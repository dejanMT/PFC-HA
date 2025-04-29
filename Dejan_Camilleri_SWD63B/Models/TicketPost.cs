using Google.Cloud.Firestore;
using System.ComponentModel.DataAnnotations;

namespace Dejan_Camilleri_SWD63B.Models
{
    [FirestoreData]
    public class TicketPost
    {
        [Required]
        [FirestoreProperty]
        public string TicketId { get; set; }

        [FirestoreProperty]
        public string PostTitle { get; set; }

        [FirestoreProperty]
        public string PostDescription { get; set; }

        [FirestoreProperty]
        public string PostAuthor { get; set; }

        [FirestoreProperty]
        public string PostAuthorEmail { get; set; }

        [FirestoreProperty]
        public DateTimeOffset PostDate { get; set; }



        [FirestoreProperty]
        public bool ClosedTicket { get; set; } = false;

        [FirestoreProperty]
        public string SupportAgent { get; set; }




        public string PostImageUrl { get; set; }
        public IFormFile PostImage { get; set; }
    }
}
