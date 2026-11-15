using UnityEngine;

namespace Training
{
    public class RockContactSensor : MonoBehaviour
    {
        public RockContacts Contacts { get; set; }

        private void OnCollisionEnter(Collision collision)
        {
            Contacts?.Record(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            Contacts?.Record(collision);
        }
    }
}
