using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HAL9000
{
    internal static class NavigationInfoTool
    {
        public static string GetOrbitInfoJson()
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null || vessel.orbit == null)
            {
                return Error("No active vessel orbit is available.");
            }

            double now = Planetarium.GetUniversalTime();
            Dictionary<string, object> info = new Dictionary<string, object>();
            info["vessel_name"] = vessel.vesselName;
            info["body"] = vessel.mainBody == null ? null : vessel.mainBody.bodyName;
            info["situation"] = vessel.situation.ToString();
            info["universal_time"] = CleanNumber(now);
            info["surface"] = GetSurfaceInfo(vessel);
            info["orbit"] = OrbitSummary(vessel.orbit, now);
            info["reference_frame"] = ReferenceFrame(vessel.orbit, now);
            info["patch"] = PatchSummary(vessel.orbit, now);
            return JsonUtil.Serialize(info);
        }

        public static string GetTargetInfoJson()
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            object target = FlightGlobals.fetch == null ? null : FlightGlobals.fetch.VesselTarget;
            if (vessel == null)
            {
                return Error("No active vessel.");
            }

            if (target == null)
            {
                return Error("No active target.");
            }

            double now = Planetarium.GetUniversalTime();
            Orbit targetOrbit = InvokeMethod(target, "GetOrbit") as Orbit;
            Dictionary<string, object> info = new Dictionary<string, object>();
            info["target_name"] = SafeTargetName(target);
            info["target_type"] = target.GetType().FullName;
            info["active_vessel"] = vessel.vesselName;

            Vessel targetVessel = target as Vessel;
            CelestialBody targetBody = target as CelestialBody;
            if (targetVessel != null)
            {
                info["target_vessel_situation"] = targetVessel.situation.ToString();
            }
            if (targetBody != null)
            {
                info["target_body"] = targetBody.bodyName;
                info["target_body_radius_m"] = CleanNumber(targetBody.Radius);
            }

            Vector3d vesselPosition = vessel.GetWorldPos3D();
            Vector3d targetPosition = TargetWorldPosition(target, now);
            if (targetPosition != Vector3d.zero)
            {
                info["distance_m"] = CleanNumber(Vector3d.Distance(vesselPosition, targetPosition));
            }

            Vector3d targetVelocity = TargetWorldVelocity(target, targetOrbit, now);
            Vector3d vesselVelocity = vessel.obt_velocity;
            if (targetVelocity != Vector3d.zero || vesselVelocity != Vector3d.zero)
            {
                info["relative_velocity_mps"] = CleanNumber((targetVelocity - vesselVelocity).magnitude);
            }

            if (targetOrbit != null)
            {
                info["target_orbit"] = OrbitSummary(targetOrbit, now);
            }

            if (vessel.orbit != null && targetOrbit != null && vessel.orbit.referenceBody == targetOrbit.referenceBody)
            {
                info["same_reference_body"] = true;
                info["relative_inclination_deg"] = CleanNumber(Vector3d.Angle(OrbitNormal(vessel.orbit), OrbitNormal(targetOrbit)));
                Dictionary<string, object> approach = ClosestApproach(vessel.orbit, targetOrbit, now);
                info["closest_approach"] = approach;
            }
            else
            {
                info["same_reference_body"] = false;
            }

            return JsonUtil.Serialize(info);
        }

        public static string GetManeuverNodesJson()
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null)
            {
                return Error("No active vessel.");
            }

            List<object> nodes = new List<object>();
            double now = Planetarium.GetUniversalTime();
            if (vessel.patchedConicSolver != null && vessel.patchedConicSolver.maneuverNodes != null)
            {
                for (int i = 0; i < vessel.patchedConicSolver.maneuverNodes.Count; i++)
                {
                    ManeuverNode node = vessel.patchedConicSolver.maneuverNodes[i];
                    Dictionary<string, object> nodeInfo = new Dictionary<string, object>();
                    nodeInfo["index"] = i;
                    nodeInfo["ut"] = CleanNumber(node.UT);
                    nodeInfo["time_until_s"] = CleanNumber(node.UT - now);
                    nodeInfo["prograde_mps"] = CleanNumber(node.DeltaV.z);
                    nodeInfo["normal_mps"] = CleanNumber(node.DeltaV.y);
                    nodeInfo["radial_mps"] = CleanNumber(node.DeltaV.x);
                    nodeInfo["total_delta_v_mps"] = CleanNumber(node.DeltaV.magnitude);
                    nodeInfo["patch_body"] = node.patch == null || node.patch.referenceBody == null ? null : node.patch.referenceBody.bodyName;
                    if (node.nextPatch != null)
                    {
                        nodeInfo["resulting_patch"] = PatchSummary(node.nextPatch, now);
                        nodeInfo["resulting_orbit"] = OrbitSummary(node.nextPatch, node.UT);
                    }
                    nodes.Add(nodeInfo);
                }
            }

            return JsonUtil.Serialize(new Dictionary<string, object>
            {
                { "vessel_name", vessel.vesselName },
                { "node_count", nodes.Count },
                { "nodes", nodes }
            });
        }

        public static string GetEngineStatusJson()
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null)
            {
                return Error("No active vessel.");
            }

            List<ModuleEngines> engines = vessel.FindPartModulesImplementing<ModuleEngines>();
            List<object> engineInfos = new List<object>();
            double currentThrust = 0.0;
            double maxThrust = 0.0;
            int ignited = 0;
            int operational = 0;

            for (int i = 0; i < engines.Count; i++)
            {
                ModuleEngines engine = engines[i];
                if (engine == null)
                {
                    continue;
                }

                double limitedMax = engine.maxThrust * engine.thrustPercentage / 100.0;
                currentThrust += engine.finalThrust;
                maxThrust += limitedMax;
                if (engine.EngineIgnited) ignited++;
                if (engine.isOperational) operational++;

                Dictionary<string, object> engineInfo = new Dictionary<string, object>();
                engineInfo["index"] = i;
                engineInfo["part_title"] = engine.part == null || engine.part.partInfo == null ? null : engine.part.partInfo.title;
                engineInfo["part_name"] = engine.part == null ? null : engine.part.name;
                engineInfo["module_name"] = engine.moduleName;
                engineInfo["stage"] = engine.part == null ? null : (object)engine.part.inverseStage;
                engineInfo["ignited"] = engine.EngineIgnited;
                engineInfo["enabled"] = engine.isEnabled;
                engineInfo["operational"] = engine.isOperational;
                engineInfo["flameout"] = engine.getFlameoutState;
                engineInfo["throttle_locked"] = engine.throttleLocked;
                engineInfo["requested_throttle"] = CleanNumber(engine.requestedThrottle);
                engineInfo["thrust_limiter_percent"] = CleanNumber(engine.thrustPercentage);
                engineInfo["current_thrust_kn"] = CleanNumber(engine.finalThrust);
                engineInfo["max_thrust_kn"] = CleanNumber(engine.maxThrust);
                engineInfo["limited_max_thrust_kn"] = CleanNumber(limitedMax);
                engineInfo["min_thrust_kn"] = CleanNumber(engine.minThrust);
                engineInfo["current_isp_s"] = CleanNumber(engine.realIsp);
                engineInfo["propellants"] = Propellants(engine);
                engineInfos.Add(engineInfo);
            }

            double massTons = vessel.GetTotalMass();
            return JsonUtil.Serialize(new Dictionary<string, object>
            {
                { "vessel_name", vessel.vesselName },
                { "engine_count", engines.Count },
                { "ignited_engine_count", ignited },
                { "operational_engine_count", operational },
                { "current_thrust_kn", CleanNumber(currentThrust) },
                { "limited_max_thrust_kn", CleanNumber(maxThrust) },
                { "vessel_mass_t", CleanNumber(massTons) },
                { "max_acceleration_mps2", massTons > 0 ? CleanNumber(maxThrust / massTons) : null },
                { "current_acceleration_mps2", massTons > 0 ? CleanNumber(currentThrust / massTons) : null },
                { "engines", engineInfos }
            });
        }

        public static string EstimateBurnJson(Dictionary<string, object> arguments)
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null)
            {
                return Error("No active vessel.");
            }

            double requestedDv = GetArgumentDouble(arguments, "delta_v_mps", 0.0);
            if (requestedDv <= 0.0)
            {
                requestedDv = GetArgumentDouble(arguments, "required_delta_v_mps", 0.0);
            }
            if (requestedDv <= 0.0 && vessel.patchedConicSolver != null && vessel.patchedConicSolver.maneuverNodes.Count > 0)
            {
                requestedDv = vessel.patchedConicSolver.maneuverNodes[0].DeltaV.magnitude;
            }

            Dictionary<string, object> result = new Dictionary<string, object>();
            result["vessel_name"] = vessel.vesselName;
            result["requested_delta_v_mps"] = CleanNumber(requestedDv);
            result["stage_estimates"] = StageBurnEstimates(vessel, requestedDv, result);
            result["engine_status"] = JsonUtil.Deserialize(GetEngineStatusJson());
            return JsonUtil.Serialize(result);
        }

        public static string GetReferenceFramesJson(Dictionary<string, object> arguments)
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null || vessel.orbit == null)
            {
                return Error("No active vessel orbit is available.");
            }

            double ut = GetArgumentDouble(arguments, "ut", Planetarium.GetUniversalTime());
            return JsonUtil.Serialize(new Dictionary<string, object>
            {
                { "vessel_name", vessel.vesselName },
                { "ut", CleanNumber(ut) },
                { "body", vessel.orbit.referenceBody == null ? null : vessel.orbit.referenceBody.bodyName },
                { "frame", ReferenceFrame(vessel.orbit, ut) },
                { "maneuver_node_components", "KSP maneuver node DeltaV uses x=radial_plus, y=normal_plus, z=prograde." }
            });
        }

        public static string SimulateManeuverJson(Dictionary<string, object> arguments)
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null || vessel.orbit == null)
            {
                return Error("No active vessel orbit is available.");
            }

            double ut = GetArgumentDouble(arguments, "ut", Planetarium.GetUniversalTime());
            double prograde = GetArgumentDouble(arguments, "prograde_mps", 0.0);
            double normal = GetArgumentDouble(arguments, "normal_mps", 0.0);
            double radial = GetArgumentDouble(arguments, "radial_mps", 0.0);
            Dictionary<string, object> frame = ReferenceFrame(vessel.orbit, ut);
            Vector3d dV =
                VectorFromDict(frame, "prograde") * prograde +
                VectorFromDict(frame, "normal_plus") * normal +
                VectorFromDict(frame, "radial_plus") * radial;

            Orbit simulated = OrbitFromStateVectors(vessel.orbit, ut, dV);
            if (simulated == null)
            {
                return Error("Could not simulate maneuver from state vectors.");
            }

            return JsonUtil.Serialize(new Dictionary<string, object>
            {
                { "ut", CleanNumber(ut) },
                { "input_delta_v", new Dictionary<string, object>
                    {
                        { "prograde_mps", CleanNumber(prograde) },
                        { "normal_mps", CleanNumber(normal) },
                        { "radial_mps", CleanNumber(radial) },
                        { "total_mps", CleanNumber(new Vector3d(radial, normal, prograde).magnitude) }
                    }
                },
                { "simulated_orbit", OrbitSummary(simulated, ut) },
                { "note", "Read-only two-body estimate. It does not create a KSP maneuver node or run full patched-conic planning." }
            });
        }

        private static Dictionary<string, object> GetSurfaceInfo(Vessel vessel)
        {
            return new Dictionary<string, object>
            {
                { "altitude_asl_m", CleanNumber(vessel.altitude) },
                { "radar_altitude_m", CleanNumber(vessel.radarAltitude) },
                { "latitude_deg", CleanNumber(vessel.latitude) },
                { "longitude_deg", CleanNumber(vessel.longitude) },
                { "surface_speed_mps", CleanNumber(vessel.srfSpeed) },
                { "horizontal_speed_mps", CleanNumber(vessel.horizontalSrfSpeed) },
                { "vertical_speed_mps", CleanNumber(vessel.verticalSpeed) },
                { "mass_t", CleanNumber(vessel.GetTotalMass()) }
            };
        }

        private static Dictionary<string, object> OrbitSummary(Orbit orbit, double ut)
        {
            if (orbit == null)
            {
                return null;
            }

            return new Dictionary<string, object>
            {
                { "reference_body", orbit.referenceBody == null ? null : orbit.referenceBody.bodyName },
                { "apoapsis_m", CleanNumber(orbit.ApA) },
                { "periapsis_m", CleanNumber(orbit.PeA) },
                { "apoapsis_radius_m", CleanNumber(orbit.ApR) },
                { "periapsis_radius_m", CleanNumber(orbit.PeR) },
                { "semi_major_axis_m", CleanNumber(orbit.semiMajorAxis) },
                { "eccentricity", CleanNumber(orbit.eccentricity) },
                { "inclination_deg", CleanNumber(orbit.inclination) },
                { "lan_deg", CleanNumber(orbit.LAN) },
                { "argument_of_periapsis_deg", CleanNumber(orbit.argumentOfPeriapsis) },
                { "true_anomaly_deg", CleanNumber(orbit.trueAnomaly) },
                { "mean_anomaly_rad", CleanNumber(orbit.meanAnomaly) },
                { "period_s", CleanNumber(orbit.period) },
                { "time_to_apoapsis_s", CleanNumber(orbit.timeToAp) },
                { "time_to_periapsis_s", CleanNumber(orbit.timeToPe) },
                { "orbital_speed_mps", CleanNumber(orbit.getOrbitalVelocityAtUT(ut).magnitude) }
            };
        }

        private static Dictionary<string, object> PatchSummary(Orbit orbit, double now)
        {
            Dictionary<string, object> patch = new Dictionary<string, object>();
            patch["start_ut"] = CleanNumber(orbit.StartUT);
            patch["end_ut"] = CleanNumber(orbit.EndUT);
            patch["time_to_patch_end_s"] = orbit.EndUT > now ? CleanNumber(orbit.EndUT - now) : null;
            patch["patch_end_transition"] = orbit.patchEndTransition.ToString();
            patch["next_patch_body"] = orbit.nextPatch == null || orbit.nextPatch.referenceBody == null ? null : orbit.nextPatch.referenceBody.bodyName;
            patch["next_patch_transition"] = orbit.nextPatch == null ? null : orbit.nextPatch.patchEndTransition.ToString();
            return patch;
        }

        private static Dictionary<string, object> ReferenceFrame(Orbit orbit, double ut)
        {
            Vector3d velocity = orbit.getOrbitalVelocityAtUT(ut);
            Vector3d up = orbit.getRelativePositionAtUT(ut).normalized;
            Vector3d prograde = velocity.normalized;
            Vector3d radial = (up - prograde * Vector3d.Dot(up, prograde)).normalized;
            Vector3d normal = Vector3d.Cross(radial, prograde).normalized;

            return new Dictionary<string, object>
            {
                { "prograde", Vector(prograde) },
                { "radial_plus", Vector(radial) },
                { "normal_plus", Vector(normal) }
            };
        }

        private static List<object> Propellants(ModuleEngines engine)
        {
            List<object> propellants = new List<object>();
            if (engine.propellants == null)
            {
                return propellants;
            }

            for (int i = 0; i < engine.propellants.Count; i++)
            {
                Propellant propellant = engine.propellants[i];
                propellants.Add(new Dictionary<string, object>
                {
                    { "name", propellant.name },
                    { "ratio", CleanNumber(propellant.ratio) },
                    { "current_requirement", CleanNumber(propellant.currentRequirement) },
                    { "total_resource_available", CleanNumber(propellant.totalResourceAvailable) },
                    { "draw_stack_gauge", propellant.drawStackGauge }
                });
            }

            return propellants;
        }

        private static List<object> StageBurnEstimates(Vessel vessel, double requestedDv, Dictionary<string, object> result)
        {
            List<object> stages = new List<object>();
            double remaining = requestedDv;
            double totalDv = 0.0;
            double totalDuration = 0.0;

            try
            {
                object vesselDeltaV = GetMemberValue(vessel, "VesselDeltaV");
                IEnumerable stageInfos = GetMemberValue(vesselDeltaV, "OperatingStageInfo") as IEnumerable;
                if (stageInfos == null)
                {
                    result["delta_v_available"] = null;
                    result["estimated_burn_duration_s"] = null;
                    result["has_enough_delta_v"] = null;
                    return stages;
                }

                int index = 0;
                foreach (object stage in stageInfos)
                {
                    double stageDv = TryGetDouble(stage, "deltaVinVac") ?? TryGetDouble(stage, "DeltaV") ?? TryGetDouble(stage, "deltaV") ?? 0.0;
                    if (stageDv <= 0.0)
                    {
                        index++;
                        continue;
                    }

                    double usedDv = remaining > 0.0 ? Math.Min(remaining, stageDv) : 0.0;
                    double duration = usedDv > 0.0 ? CalculateStageBurnDuration(stage, usedDv) : 0.0;
                    stages.Add(new Dictionary<string, object>
                    {
                        { "index", index },
                        { "stage_delta_v_mps", CleanNumber(stageDv) },
                        { "used_delta_v_mps", CleanNumber(usedDv) },
                        { "estimated_duration_s", CleanNumber(duration) }
                    });
                    totalDv += stageDv;
                    totalDuration += duration;
                    remaining -= usedDv;
                    index++;
                }
            }
            catch
            {
                result["delta_v_available"] = null;
                result["estimated_burn_duration_s"] = null;
                result["has_enough_delta_v"] = null;
                return stages;
            }

            result["delta_v_available_mps"] = CleanNumber(totalDv);
            result["estimated_burn_duration_s"] = requestedDv > 0.0 ? CleanNumber(totalDuration) : null;
            result["has_enough_delta_v"] = requestedDv <= 0.0 ? null : (object)(totalDv >= requestedDv);
            result["remaining_delta_v_after_burn_mps"] = requestedDv > 0.0 ? CleanNumber(totalDv - requestedDv) : null;
            return stages;
        }

        private static double CalculateStageBurnDuration(object stage, double usedDv)
        {
            MethodInfo method = stage.GetType().GetMethod("CalculateTimeRequiredDV", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                return 0.0;
            }

            object value = method.Invoke(stage, new object[] { false, (float)usedDv });
            return value == null ? 0.0 : Convert.ToDouble(value);
        }

        private static Dictionary<string, object> ClosestApproach(Orbit a, Orbit b, double now)
        {
            double interval = a.eccentricity > 1.0 ? 100.0 / Math.Max(a.meanMotion, 0.000001) : a.period;
            if (double.IsNaN(interval) || double.IsInfinity(interval) || interval <= 0.0)
            {
                interval = 21600.0;
            }

            double bestTime = now;
            double bestDistance = double.MaxValue;
            double minTime = now;
            double maxTime = now + interval;

            for (int iteration = 0; iteration < 6; iteration++)
            {
                double step = (maxTime - minTime) / 20.0;
                for (int i = 0; i <= 20; i++)
                {
                    double t = minTime + step * i;
                    double distance = Vector3d.Distance(a.getPositionAtUT(t), b.getPositionAtUT(t));
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestTime = t;
                    }
                }

                minTime = Math.Max(now, bestTime - step);
                maxTime = Math.Min(now + interval, bestTime + step);
            }

            return new Dictionary<string, object>
            {
                { "ut", CleanNumber(bestTime) },
                { "time_until_s", CleanNumber(bestTime - now) },
                { "distance_m", CleanNumber(bestDistance) }
            };
        }

        private static Orbit OrbitFromStateVectors(Orbit source, double ut, Vector3d deltaV)
        {
            Orbit orbit = new Orbit();
            orbit.UpdateFromStateVectors(source.getRelativePositionAtUT(ut), source.getOrbitalVelocityAtUT(ut) + deltaV, source.referenceBody, ut);
            return orbit;
        }

        private static Vector3d TargetWorldPosition(object target, double ut)
        {
            Vessel vessel = target as Vessel;
            if (vessel != null)
            {
                return vessel.GetWorldPos3D();
            }

            CelestialBody body = target as CelestialBody;
            if (body != null)
            {
                return body.position;
            }

            object position = InvokeMethod(target, "GetTransform");
            Transform transform = position as Transform;
            if (transform == null)
            {
                return Vector3d.zero;
            }

            Vector3 positionVector = transform.position;
            return new Vector3d(positionVector.x, positionVector.y, positionVector.z);
        }

        private static Vector3d TargetWorldVelocity(object target, Orbit targetOrbit, double ut)
        {
            Vessel vessel = target as Vessel;
            if (vessel != null)
            {
                return vessel.obt_velocity;
            }

            if (targetOrbit != null)
            {
                return targetOrbit.getOrbitalVelocityAtUT(ut);
            }

            return Vector3d.zero;
        }

        private static string SafeTargetName(object target)
        {
            object value = InvokeMethod(target, "GetName");
            return value == null ? target.ToString() : value.ToString();
        }

        private static object InvokeMethod(object instance, string name)
        {
            if (instance == null)
            {
                return null;
            }

            MethodInfo method = instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[0], null);
            return method == null ? null : method.Invoke(instance, null);
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

        private static Vector3d OrbitNormal(Orbit orbit)
        {
            return Vector3d.Cross(orbit.getRelativePositionAtUT(Planetarium.GetUniversalTime()), orbit.getOrbitalVelocityAtUT(Planetarium.GetUniversalTime())).normalized;
        }

        private static Dictionary<string, object> Vector(Vector3d vector)
        {
            return new Dictionary<string, object>
            {
                { "x", CleanNumber(vector.x) },
                { "y", CleanNumber(vector.y) },
                { "z", CleanNumber(vector.z) }
            };
        }

        private static Vector3d VectorFromDict(Dictionary<string, object> frame, string key)
        {
            Dictionary<string, object> vector = frame[key] as Dictionary<string, object>;
            return new Vector3d(ToDouble(vector["x"]), ToDouble(vector["y"]), ToDouble(vector["z"]));
        }

        private static double GetArgumentDouble(Dictionary<string, object> arguments, string key, double fallback)
        {
            object value;
            if (arguments == null || !arguments.TryGetValue(key, out value) || value == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToDouble(value);
            }
            catch
            {
                return fallback;
            }
        }

        private static double ToDouble(object value)
        {
            return value == null ? 0.0 : Convert.ToDouble(value);
        }

        private static string Error(string message)
        {
            return JsonUtil.Serialize(new Dictionary<string, object> { { "error", message } });
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
