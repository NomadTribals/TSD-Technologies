namespace TsdTechnologies.Overlay
{
    public enum MapExpansionMode { Hold, Toggle }
    internal sealed class MapExpansionControl
    {
        private MapExpansionMode previousMode;
        private bool expanded;
        internal bool Update(MapExpansionMode mode,bool available,bool held,bool pressed)
        {
            if(mode!=previousMode){expanded=false;previousMode=mode;}
            if(!available){expanded=false;return false;}
            if(mode==MapExpansionMode.Hold)expanded=held;
            else if(pressed)expanded=!expanded;
            return expanded;
        }
    }
}
