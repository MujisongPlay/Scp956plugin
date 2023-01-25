using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Exiled.Events.EventArgs.Player;
using SCP956Plugin.SCP956;

namespace SCP956Plugin.Handlers
{
    public class PlayerHandler
    {
        bool IsAllowed(ReferenceHub hub)
        {
            if (SCP956Plugin.Instance.Config.LockInventory)
            {
                foreach (SCP956AI aI in SchematicHandler.aIs)
                {
                    if (aI.Targeted.Contains(hub))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public void ChangeItem(ChangingItemEventArgs ev)
        {
            if (!IsAllowed(ev.Player.ReferenceHub))
            {
                ev.IsAllowed = false;
            }
        }

        public void DropItem(DroppingItemEventArgs ev)
        {
            if (!IsAllowed(ev.Player.ReferenceHub))
            {
                ev.IsAllowed = false;
            }
        }

        public void InteractDoor(InteractingDoorEventArgs ev)
        {
            if (!IsAllowed(ev.Player.ReferenceHub))
            {
                ev.IsAllowed = false;
            }
        }

        public void InteractElevator(InteractingElevatorEventArgs ev)
        {
            if (!IsAllowed(ev.Player.ReferenceHub))
            {
                ev.IsAllowed = false;
            }
        }

        public void InteractLocker(InteractingLockerEventArgs ev)
        {
            if (!IsAllowed(ev.Player.ReferenceHub))
            {
                ev.IsAllowed = false;
            }
        }
    }
}
