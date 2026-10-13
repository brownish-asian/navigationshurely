using navigationshurely.models;

namespace navigationshurely.services
{
    public class Command
    {
        public State Mode { get; set; }
        public int Quantity { get; set; }
        public Direct Direction { get; set; }
        public String? SearchText { get; set; }
    }
}
