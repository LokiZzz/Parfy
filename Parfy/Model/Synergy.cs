namespace Parfy.Model
{
    public class Synergy
    {
        public Component Source { get; set; } = new();

        public Component Synergent { get; set; } = new();

        public string Entry { get; set; } = string.Empty;

        public int Weight { get; set; }
    }
}
