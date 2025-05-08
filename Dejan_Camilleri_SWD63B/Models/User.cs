using Google.Cloud.Firestore;

namespace Dejan_Camilleri_SWD63B.Models
{
    [FirestoreData]
    public class User
    {
        [FirestoreDocumentId]
        public string Id { get; set; }

        [FirestoreProperty("Email")]
        public string Email { get; set; }

        [FirestoreProperty("DisplayName")]
        public string DisplayName { get; set; }

        [FirestoreProperty("Role")]
        public string Role { get; set; }
    }
}
