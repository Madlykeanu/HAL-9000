using System;
using System.Collections.Generic;
using UnityEngine;

namespace HAL9000
{
    internal static class CelestialInfoTool
    {
        public static string ListCelestialBodiesJson(Dictionary<string, object> arguments)
        {
            bool includeDetails = GetArgumentBool(arguments, "include_details");
            Dictionary<string, List<string>> childrenByParent = BuildChildrenByParent();
            List<object> bodies = new List<object>();
            List<object> rootBodies = new List<object>();

            for (int i = 0; i < FlightGlobals.Bodies.Count; i++)
            {
                CelestialBody body = FlightGlobals.Bodies[i];
                CelestialBody parent = GetParentBody(body);
                CelestialBody root = GetSystemRoot(body);

                Dictionary<string, object> bodyInfo = new Dictionary<string, object>();
                bodyInfo["body_name"] = body.bodyName;
                bodyInfo["display_name"] = body.displayName;
                bodyInfo["parent_body"] = parent == null ? null : parent.bodyName;
                bodyInfo["system_root"] = root == null ? body.bodyName : root.bodyName;
                bodyInfo["children"] = GetChildren(childrenByParent, body.bodyName);

                if (includeDetails)
                {
                    bodyInfo["is_home_world"] = body.isHomeWorld;
                    bodyInfo["has_atmosphere"] = body.atmosphere;
                    bodyInfo["radius_m"] = CleanNumber(body.Radius);
                    bodyInfo["sphere_of_influence_m"] = CleanNumber(body.sphereOfInfluence);
                    bodyInfo["surface_gravity_mps2"] = CleanNumber(body.GeeASL * 9.80665);
                    if (body.orbit != null)
                    {
                        bodyInfo["orbital_period_s"] = CleanNumber(body.orbit.period);
                        bodyInfo["semi_major_axis_m"] = CleanNumber(body.orbit.semiMajorAxis);
                    }
                }

                bodies.Add(bodyInfo);
                if (parent == null)
                {
                    rootBodies.Add(body.bodyName);
                }
            }

            return JsonUtil.Serialize(new Dictionary<string, object>
            {
                { "body_count", FlightGlobals.Bodies.Count },
                { "system_roots", rootBodies },
                { "bodies", bodies }
            });
        }

        public static string GetCelestialInfoJson(Dictionary<string, object> arguments)
        {
            CelestialBody body = ResolveBody(arguments);
            if (body == null)
            {
                return JsonUtil.Serialize(new Dictionary<string, object>
                {
                    { "error", "Celestial body not found. Pass body_name such as Kerbin, Mun, Minmus, Duna, Ike, Jool, or Laythe." }
                });
            }

            Dictionary<string, object> info = new Dictionary<string, object>();
            info["body_name"] = body.bodyName;
            info["display_name"] = body.displayName;
            info["parent_body"] = body.referenceBody == null || body.referenceBody == body ? null : body.referenceBody.bodyName;
            info["is_home_world"] = body.isHomeWorld;
            info["has_atmosphere"] = body.atmosphere;
            info["atmosphere_depth_m"] = CleanNumber(body.atmosphereDepth);
            info["radius_m"] = CleanNumber(body.Radius);
            info["sphere_of_influence_m"] = CleanNumber(body.sphereOfInfluence);
            info["surface_gravity_mps2"] = CleanNumber(body.GeeASL * 9.80665);
            info["rotation_period_s"] = CleanNumber(body.rotationPeriod);
            info["tidally_locked"] = body.tidallyLocked;

            if (body.orbit != null)
            {
                Dictionary<string, object> orbitInfo = new Dictionary<string, object>();
                orbitInfo["semi_major_axis_m"] = CleanNumber(body.orbit.semiMajorAxis);
                orbitInfo["eccentricity"] = CleanNumber(body.orbit.eccentricity);
                orbitInfo["inclination_deg"] = CleanNumber(body.orbit.inclination);
                orbitInfo["orbital_period_s"] = CleanNumber(body.orbit.period);
                info["orbit"] = orbitInfo;
            }

            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel != null)
            {
                double now = Planetarium.GetUniversalTime();
                Vector3d vesselPosition = KspDistanceUtil.VesselPositionAtUT(vessel, now);
                Vector3d bodyPosition = KspDistanceUtil.BodyPositionAtUT(body, now);
                double centerDistance = Vector3d.Distance(vesselPosition, bodyPosition);

                Dictionary<string, object> vesselRelation = new Dictionary<string, object>();
                KspDistanceUtil.AddDistanceFields(vesselRelation, "distance_to_body_center", centerDistance);
                KspDistanceUtil.AddDistanceFields(vesselRelation, "distance_to_body_surface", Math.Max(0.0, centerDistance - body.Radius));
                vesselRelation["active_vessel_body"] = vessel.mainBody == null ? null : vessel.mainBody.bodyName;

                if (vessel.mainBody != null)
                {
                    Vector3d mainBodyPosition = KspDistanceUtil.BodyPositionAtUT(vessel.mainBody, now);
                    double bodyToBodyCenterDistance = Vector3d.Distance(mainBodyPosition, bodyPosition);
                    KspDistanceUtil.AddDistanceFields(vesselRelation, "current_body_center_to_body_center", bodyToBodyCenterDistance);
                    KspDistanceUtil.AddDistanceFields(
                        vesselRelation,
                        "current_body_surface_to_body_surface",
                        Math.Max(0.0, bodyToBodyCenterDistance - vessel.mainBody.Radius - body.Radius));
                }

                info["active_vessel_relation"] = vesselRelation;
            }

            return JsonUtil.Serialize(info);
        }

        private static CelestialBody ResolveBody(Dictionary<string, object> arguments)
        {
            string bodyName = GetArgumentString(arguments, "body_name");
            if (string.IsNullOrEmpty(bodyName))
            {
                return FlightGlobals.ActiveVessel == null ? null : FlightGlobals.ActiveVessel.mainBody;
            }

            string normalizedInput = NormalizeName(bodyName);
            for (int i = 0; i < FlightGlobals.Bodies.Count; i++)
            {
                CelestialBody body = FlightGlobals.Bodies[i];
                if (NormalizeName(body.bodyName) == normalizedInput || NormalizeName(body.displayName) == normalizedInput)
                {
                    return body;
                }
            }

            return null;
        }

        private static Dictionary<string, List<string>> BuildChildrenByParent()
        {
            Dictionary<string, List<string>> childrenByParent = new Dictionary<string, List<string>>();
            for (int i = 0; i < FlightGlobals.Bodies.Count; i++)
            {
                CelestialBody body = FlightGlobals.Bodies[i];
                CelestialBody parent = GetParentBody(body);
                if (parent == null)
                {
                    continue;
                }

                List<string> children;
                if (!childrenByParent.TryGetValue(parent.bodyName, out children))
                {
                    children = new List<string>();
                    childrenByParent[parent.bodyName] = children;
                }

                children.Add(body.bodyName);
            }

            return childrenByParent;
        }

        private static List<object> GetChildren(Dictionary<string, List<string>> childrenByParent, string bodyName)
        {
            List<string> children;
            if (!childrenByParent.TryGetValue(bodyName, out children))
            {
                return new List<object>();
            }

            List<object> result = new List<object>();
            for (int i = 0; i < children.Count; i++)
            {
                result.Add(children[i]);
            }

            return result;
        }

        private static CelestialBody GetParentBody(CelestialBody body)
        {
            if (body == null || body.referenceBody == null || body.referenceBody == body)
            {
                return null;
            }

            return body.referenceBody;
        }

        private static CelestialBody GetSystemRoot(CelestialBody body)
        {
            CelestialBody current = body;
            for (int i = 0; i < 64 && current != null; i++)
            {
                CelestialBody parent = GetParentBody(current);
                if (parent == null)
                {
                    return current;
                }

                current = parent;
            }

            return body;
        }

        private static string GetArgumentString(Dictionary<string, object> arguments, string key)
        {
            if (arguments == null)
            {
                return null;
            }

            object value;
            return arguments.TryGetValue(key, out value) && value != null ? value.ToString() : null;
        }

        private static bool GetArgumentBool(Dictionary<string, object> arguments, string key)
        {
            if (arguments == null)
            {
                return false;
            }

            object value;
            if (!arguments.TryGetValue(key, out value) || value == null)
            {
                return false;
            }

            if (value is bool)
            {
                return (bool)value;
            }

            bool parsed;
            return bool.TryParse(value.ToString(), out parsed) && parsed;
        }

        private static string NormalizeName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim().ToLowerInvariant();
            if (trimmed.StartsWith("the "))
            {
                trimmed = trimmed.Substring(4);
            }

            return trimmed.Replace(" ", string.Empty);
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
