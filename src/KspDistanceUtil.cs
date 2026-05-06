using System;
using System.Collections.Generic;
using UnityEngine;

namespace HAL9000
{
    internal static class KspDistanceUtil
    {
        public static Vector3d BodyPositionAtUT(CelestialBody body, double ut)
        {
            return BodyPositionAtUT(body, ut, 0);
        }

        public static Vector3d VesselPositionAtUT(Vessel vessel, double ut)
        {
            if (vessel == null)
            {
                return Vector3d.zero;
            }

            if (vessel.orbit != null && vessel.orbit.referenceBody != null)
            {
                return BodyPositionAtUT(vessel.orbit.referenceBody, ut) + vessel.orbit.getRelativePositionAtUT(ut);
            }

            return vessel.GetWorldPos3D();
        }

        public static void AddDistanceFields(Dictionary<string, object> target, string prefix, double meters)
        {
            target[prefix + "_m"] = CleanNumber(meters);
            target[prefix + "_km"] = CleanNumber(meters / 1000.0);
            target[prefix + "_display"] = FormatDistance(meters);
        }

        public static string FormatDistance(double meters)
        {
            double absolute = Math.Abs(meters);
            if (absolute >= 1000000000000.0)
            {
                return (meters / 1000000000000.0).ToString("0.##") + " Tm";
            }

            if (absolute >= 1000000000.0)
            {
                return (meters / 1000000000.0).ToString("0.##") + " Gm";
            }

            if (absolute >= 1000000.0)
            {
                return (meters / 1000000.0).ToString("0.##") + " Mm";
            }

            if (absolute >= 1000.0)
            {
                return (meters / 1000.0).ToString("0.##") + " km";
            }

            return meters.ToString("0.##") + " m";
        }

        private static Vector3d BodyPositionAtUT(CelestialBody body, double ut, int depth)
        {
            if (body == null || depth > 64 || body.orbit == null)
            {
                return Vector3d.zero;
            }

            CelestialBody parent = body.orbit.referenceBody;
            if (parent == null || parent == body)
            {
                return body.orbit.getRelativePositionAtUT(ut);
            }

            return BodyPositionAtUT(parent, ut, depth + 1) + body.orbit.getRelativePositionAtUT(ut);
        }

        private static object CleanNumber(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return null;
            }

            return Math.Round(value, 3);
        }
    }
}
