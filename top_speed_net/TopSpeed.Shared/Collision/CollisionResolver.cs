using System;

namespace TopSpeed.Collision
{
    public readonly struct VehicleCollisionBody
    {
        public VehicleCollisionBody(
            float positionX,
            float positionY,
            float speedKph,
            float widthM,
            float lengthM,
            float massKg)
        {
            PositionX = positionX;
            PositionY = Math.Max(0f, positionY);
            SpeedKph = Math.Max(0f, speedKph);
            WidthM = Math.Max(0.1f, widthM);
            LengthM = Math.Max(0.1f, lengthM);
            MassKg = Math.Max(1f, massKg);
        }

        public float PositionX { get; }
        public float PositionY { get; }
        public float SpeedKph { get; }
        public float WidthM { get; }
        public float LengthM { get; }
        public float MassKg { get; }
    }

    public readonly struct VehicleCollisionImpulse
    {
        public VehicleCollisionImpulse(float bumpX, float bumpY, float speedDeltaKph)
        {
            BumpX = bumpX;
            BumpY = bumpY;
            SpeedDeltaKph = speedDeltaKph;
        }

        public float BumpX { get; }
        public float BumpY { get; }
        public float SpeedDeltaKph { get; }
    }

    public readonly struct VehicleCollisionResponse
    {
        public VehicleCollisionResponse(VehicleCollisionImpulse first, VehicleCollisionImpulse second)
        {
            First = first;
            Second = second;
        }

        public VehicleCollisionImpulse First { get; }
        public VehicleCollisionImpulse Second { get; }
    }

    public static class VehicleCollisionResolver
    {
        private const float Epsilon = 0.0001f;
        private const float MaxTransferKph = 65f;

        public static bool TryResolve(
            in VehicleCollisionBody first,
            in VehicleCollisionBody second,
            out VehicleCollisionResponse response)
        {
            response = default;

            var halfWidthSum = (first.WidthM * 0.5f) + (second.WidthM * 0.5f);
            var halfLengthSum = (first.LengthM * 0.5f) + (second.LengthM * 0.5f);

            var dx = first.PositionX - second.PositionX;
            var dy = first.PositionY - second.PositionY;
            var absDx = Math.Abs(dx);
            var absDy = Math.Abs(dy);
            if (absDx >= halfWidthSum || absDy >= halfLengthSum)
                return false;

            var xOverlap = halfWidthSum - absDx;
            var yOverlap = halfLengthSum - absDy;
            if (xOverlap <= 0f || yOverlap <= 0f)
                return false;

            var firstMassEffect = second.MassKg / (first.MassKg + second.MassKg);
            var secondMassEffect = first.MassKg / (first.MassKg + second.MassKg);

            var speedDiff = first.SpeedKph - second.SpeedKph;
            var firstRearClosing = dy < 0f && speedDiff > 0f;
            var secondRearClosing = dy > 0f && speedDiff < 0f;
            var sideSign = ResolveSign(dx, speedDiff);
            var longitudinalSign = ResolveSign(dy, speedDiff);
            var longitudinalContact = (yOverlap <= xOverlap) || firstRearClosing || secondRearClosing;
            var closingSpeed = firstRearClosing ? speedDiff : (secondRearClosing ? -speedDiff : 0f);

            var severity = Clamp01(closingSpeed / 70f);
            var exchangeFactor = longitudinalContact ? 0.78f : 0.32f;
            var exchangeSpeed = closingSpeed * exchangeFactor * (0.40f + (0.60f * severity));
            if (exchangeSpeed > MaxTransferKph)
                exchangeSpeed = MaxTransferKph;

            var lateralBase = xOverlap * (longitudinalContact ? 0.55f : 0.80f);
            var lateralImpact = (closingSpeed / 120f) * (longitudinalContact ? 0.55f : 0.35f);
            var lateralMagnitude = Math.Max(0.02f, lateralBase + lateralImpact);

            var longitudinalBase = yOverlap * (longitudinalContact ? 1.05f : 0.35f);
            var longitudinalImpact = (closingSpeed / 120f) * 0.40f;
            var longitudinalMagnitude = Math.Max(0.01f, longitudinalBase + longitudinalImpact);

            var firstDelta = 0f;
            var secondDelta = 0f;

            if (firstRearClosing)
            {
                firstDelta -= exchangeSpeed * firstMassEffect;
                secondDelta += exchangeSpeed * secondMassEffect;
            }
            else if (secondRearClosing)
            {
                secondDelta -= exchangeSpeed * secondMassEffect;
                firstDelta += exchangeSpeed * firstMassEffect;
            }

            var sideScrub = (lateralMagnitude * (longitudinalContact ? 1.1f : 1.8f))
                + (longitudinalMagnitude * 0.2f)
                + (closingSpeed * (longitudinalContact ? 0.035f : 0.02f));
            firstDelta -= sideScrub * firstMassEffect;
            secondDelta -= sideScrub * secondMassEffect;

            if (firstDelta < -first.SpeedKph)
                firstDelta = -first.SpeedKph;
            if (secondDelta < -second.SpeedKph)
                secondDelta = -second.SpeedKph;

            response = new VehicleCollisionResponse(
                new VehicleCollisionImpulse(
                    sideSign * lateralMagnitude * firstMassEffect,
                    longitudinalSign * longitudinalMagnitude * firstMassEffect,
                    firstDelta),
                new VehicleCollisionImpulse(
                    -sideSign * lateralMagnitude * secondMassEffect,
                    -longitudinalSign * longitudinalMagnitude * secondMassEffect,
                    secondDelta));
            return true;
        }

        private static float ResolveSign(float axisDelta, float speedDiff)
        {
            if (Math.Abs(axisDelta) > Epsilon)
                return axisDelta > 0f ? 1f : -1f;
            if (Math.Abs(speedDiff) > Epsilon)
                return speedDiff > 0f ? 1f : -1f;
            return 1f;
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
                return 0f;
            if (value >= 1f)
                return 1f;
            return value;
        }
    }
}
