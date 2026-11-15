using Rocks;
using UnityEngine;

namespace Training
{
    public class RockContacts
    {
        private readonly ArticulationBody chassis;

        public RockContacts(ArticulationBody chassis)
        {
            this.chassis = chassis;
        }

        public bool WheelsTouching { get; private set; }
        public bool ChassisTouching { get; private set; }

        public void Record(Collision collision)
        {
            if (collision.contactCount == 0 || !IsRock(collision.collider))
                return;

            Collider own = collision.GetContact(0).thisCollider;
            if (own != null && own.attachedArticulationBody == chassis)
                ChassisTouching = true;
            else
                WheelsTouching = true;
        }

        public void Clear()
        {
            WheelsTouching = false;
            ChassisTouching = false;
        }

        private static bool IsRock(Collider collider)
        {
            for (Transform parent = collider.transform.parent; parent != null; parent = parent.parent)
            {
                if (parent.name == RockField.RootName)
                    return true;
            }

            return false;
        }
    }
}
