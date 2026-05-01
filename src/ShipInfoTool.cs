using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HAL9000
{
    internal static class ShipInfoTool
    {
        public static string GetShipInfoJson()
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null)
            {
                return JsonUtil.Serialize(new Dictionary<string, object> { { "error", "No active vessel." } });
            }

            Dictionary<string, object> info = new Dictionary<string, object>();
            info["vessel_name"] = vessel.vesselName;
            info["current_body"] = vessel.mainBody == null ? null : vessel.mainBody.bodyName;
            info["situation"] = vessel.situation.ToString();
            info["universal_time"] = Planetarium.GetUniversalTime();

            Orbit orbit = vessel.orbit;
            if (orbit != null)
            {
                Dictionary<string, object> orbitInfo = new Dictionary<string, object>();
                orbitInfo["apoapsis_m"] = CleanNumber(orbit.ApA);
                orbitInfo["periapsis_m"] = CleanNumber(orbit.PeA);
                orbitInfo["apoapsis_radius_m"] = CleanNumber(orbit.ApR);
                orbitInfo["periapsis_radius_m"] = CleanNumber(orbit.PeR);
                orbitInfo["inclination_deg"] = CleanNumber(orbit.inclination);
                orbitInfo["eccentricity"] = CleanNumber(orbit.eccentricity);
                orbitInfo["orbital_period_s"] = CleanNumber(orbit.period);
                orbitInfo["time_to_apoapsis_s"] = CleanNumber(orbit.timeToAp);
                orbitInfo["time_to_periapsis_s"] = CleanNumber(orbit.timeToPe);
                info["orbit"] = orbitInfo;
            }

            double? deltaV = TryGetStockDeltaV(vessel);
            info["available_delta_v_mps"] = deltaV.HasValue ? (object)CleanNumber(deltaV.Value) : null;
            info["resources"] = GetResources(vessel);
            info["maneuver_nodes"] = GetManeuverNodes(vessel);
            info["target"] = GetTargetName();

            return JsonUtil.Serialize(info);
        }

        private static Dictionary<string, object> GetResources(Vessel vessel)
        {
            Dictionary<string, ResourceTotals> totals = new Dictionary<string, ResourceTotals>();
            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part part = vessel.parts[i];
                if (part == null || part.Resources == null)
                {
                    continue;
                }

                for (int r = 0; r < part.Resources.Count; r++)
                {
                    PartResource resource = part.Resources[r];
                    ResourceTotals total;
                    if (!totals.TryGetValue(resource.resourceName, out total))
                    {
                        total = new ResourceTotals();
                        totals[resource.resourceName] = total;
                    }

                    total.Amount += resource.amount;
                    total.MaxAmount += resource.maxAmount;
                }
            }

            Dictionary<string, object> resources = new Dictionary<string, object>();
            foreach (KeyValuePair<string, ResourceTotals> pair in totals)
            {
                Dictionary<string, object> resource = new Dictionary<string, object>();
                resource["amount"] = CleanNumber(pair.Value.Amount);
                resource["max_amount"] = CleanNumber(pair.Value.MaxAmount);
                resources[pair.Key] = resource;
            }

            return resources;
        }

        private static List<object> GetManeuverNodes(Vessel vessel)
        {
            List<object> nodes = new List<object>();
            if (vessel.patchedConicSolver == null || vessel.patchedConicSolver.maneuverNodes == null)
            {
                return nodes;
            }

            double now = Planetarium.GetUniversalTime();
            for (int i = 0; i < vessel.patchedConicSolver.maneuverNodes.Count; i++)
            {
                ManeuverNode node = vessel.patchedConicSolver.maneuverNodes[i];
                Dictionary<string, object> nodeInfo = new Dictionary<string, object>();
                nodeInfo["ut"] = CleanNumber(node.UT);
                nodeInfo["time_until_s"] = CleanNumber(node.UT - now);
                nodeInfo["delta_v_mps"] = CleanNumber(node.DeltaV.magnitude);
                nodeInfo["prograde_mps"] = CleanNumber(node.DeltaV.z);
                nodeInfo["normal_mps"] = CleanNumber(node.DeltaV.y);
                nodeInfo["radial_mps"] = CleanNumber(node.DeltaV.x);
                nodes.Add(nodeInfo);
            }

            return nodes;
        }

        private static string GetTargetName()
        {
            try
            {
                if (FlightGlobals.fetch == null || FlightGlobals.fetch.VesselTarget == null)
                {
                    return null;
                }

                return FlightGlobals.fetch.VesselTarget.GetName();
            }
            catch
            {
                return null;
            }
        }

        private static double? TryGetStockDeltaV(Vessel vessel)
        {
            try
            {
                object vesselDeltaV = GetMemberValue(vessel, "VesselDeltaV");
                object stageInfo = GetMemberValue(vesselDeltaV, "OperatingStageInfo");
                IEnumerable stages = stageInfo as IEnumerable;
                if (stages == null)
                {
                    return null;
                }

                double total = 0.0;
                bool found = false;
                foreach (object stage in stages)
                {
                    double? stageDv = TryGetDouble(stage, "deltaVinVac");
                    if (!stageDv.HasValue)
                    {
                        stageDv = TryGetDouble(stage, "DeltaV");
                    }
                    if (!stageDv.HasValue)
                    {
                        stageDv = TryGetDouble(stage, "deltaV");
                    }

                    if (stageDv.HasValue && stageDv.Value > 0.0)
                    {
                        total += stageDv.Value;
                        found = true;
                    }
                }

                return found ? (double?)total : null;
            }
            catch
            {
                return null;
            }
        }

        private static object GetMemberValue(object instance, string name)
        {
            if (instance == null)
            {
                return null;
            }

            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                return property.GetValue(instance, null);
            }

            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(instance);
        }

        private static double? TryGetDouble(object instance, string name)
        {
            object value = GetMemberValue(instance, name);
            if (value == null)
            {
                return null;
            }

            try
            {
                return Convert.ToDouble(value);
            }
            catch
            {
                return null;
            }
        }

        private static object CleanNumber(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return null;
            }

            return Math.Round(value, 3);
        }

        private sealed class ResourceTotals
        {
            public double Amount;
            public double MaxAmount;
        }
    }
}
