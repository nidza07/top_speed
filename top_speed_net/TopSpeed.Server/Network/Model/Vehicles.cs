using TopSpeed.Protocol;

namespace TopSpeed.Server.Network
{
    internal readonly struct VehicleDimensions
    {
        public VehicleDimensions(float widthM, float lengthM, float massKg)
        {
            WidthM = widthM;
            LengthM = lengthM;
            MassKg = massKg;
        }

        public float WidthM { get; }
        public float LengthM { get; }
        public float MassKg { get; }
    }

}
