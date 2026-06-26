using UnityEngine;
using Verse;

namespace RimClaim
{
    public class ClaimOverlay : MapComponent, ICellBoolGiver
    {
        public static bool showOverlay = false;

        private CellBoolDrawer? drawer;
        private int lastZoneVersion = -1;

        public ClaimOverlay(Map map) : base(map) { }

        public Color Color => Color.white;

        public bool GetCellBool(int index)
        {
            var registry = LandclaimRegistry.For(map);
            if (registry == null) return false;

            int x = index % map.Size.x;
            int z = index / map.Size.x;
            var cell = new IntVec3(x, 0, z);

            return registry.GetZoneAt(cell) != null;
        }

        public Color GetCellExtraColor(int index)
        {
            var registry = LandclaimRegistry.For(map);
            if (registry == null) return Color.clear;

            int x = index % map.Size.x;
            int z = index / map.Size.x;
            var cell = new IntVec3(x, 0, z);

            var zone = registry.GetZoneAt(cell);
            if (zone == null) return Color.clear;

            var players = RcWorld.Players_Safe;
            var owner = players?.GetPlayer(zone.ownerPlayerIndex);
            var color = owner?.playerColor ?? Color.gray;
            color.a = 0.25f;
            return color;
        }

        private CellBoolDrawer Drawer
        {
            get
            {
                drawer ??= new CellBoolDrawer(this, map.Size.x, map.Size.z, 0.36f);
                return drawer;
            }
        }

        public override void MapComponentUpdate()
        {
            if (!showOverlay) return;

            var registry = LandclaimRegistry.For(map);
            if (registry != null && registry.ZoneVersion != lastZoneVersion)
            {
                lastZoneVersion = registry.ZoneVersion;
                Drawer.SetDirty();
            }

            Drawer.MarkForDraw();
            Drawer.CellBoolDrawerUpdate();
        }
    }
}
