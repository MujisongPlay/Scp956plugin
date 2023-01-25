using MapEditorReborn.Events.EventArgs;
using SCP956Plugin.SCP956;
using System;
using System.Collections.Generic;

namespace SCP956Plugin.Handlers
{
    class SchematicHandler
    {
        public static List<SCP956AI> aIs = new List<SCP956AI> { };

        public void OnSpawn(SchematicSpawnedEventArgs ev)
        {
            if (ev.Name.Equals("scp956", StringComparison.InvariantCultureIgnoreCase))
            {
                SCP956AI aI = ev.Schematic.gameObject.AddComponent<SCP956AI>();
                if (aI.gameObject.transform.position.y == -300f)
                {
                    aI.SpecialConstruction();
                }
                aIs.Add(aI);
            }
        }

        public void OnDestroy(SchematicDestroyedEventArgs ev)
        {
            if (ev.Name.Equals("scp956", StringComparison.InvariantCultureIgnoreCase) && ev.Schematic.gameObject.TryGetComponent<SCP956AI>(out SCP956AI aI))
            {
                aIs.Remove(aI);
            }
        }
    }
}
