using Google.Cloud.Firestore;

namespace Dejan_Camilleri_SWD63B.Services
{
    public class TechnicianService
    {
        private readonly FirestoreDb _firestoreDb;

        public TechnicianService(FirestoreDb firestoreDb)
        {
            _firestoreDb = firestoreDb;
        }

        /// <summary>
        /// Retrieves a list of technician emails from the Firestore database.
        /// </summary>
        /// <returns></returns>
        public async Task<List<string>> GetTechnicianEmailsAsync()
        {
            var technicianEmails = new List<string>();
            CollectionReference usersRef = _firestoreDb.Collection("users");

            QuerySnapshot snapshot = await usersRef.GetSnapshotAsync();

            foreach (DocumentSnapshot doc in snapshot.Documents)
            {
                if (doc.Exists)
                {
                    var user = doc.ToDictionary();
                    if (user.TryGetValue("role", out var roleObj) &&
                        roleObj?.ToString().Equals("Technician", StringComparison.OrdinalIgnoreCase) == true &&
                        user.TryGetValue("email", out var emailObj))
                    {
                        technicianEmails.Add(emailObj.ToString());
                    }
                }
            }

            return technicianEmails;
        }
    }

}
