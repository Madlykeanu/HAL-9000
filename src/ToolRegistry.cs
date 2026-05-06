using System.Collections.Generic;

namespace HAL9000
{
    internal static class ToolRegistry
    {
        public static List<object> ToolDefinitions()
        {
            return new List<object>
            {
                CreateTool(
                    "get_ship_info",
                    "Returns live read-only information about the active KSP vessel: body, situation, orbit, stock delta-v when available, resources, maneuver nodes, and target.",
                    new Dictionary<string, object>(),
                    new string[0]),
                CreateTool(
                    "get_hal_personality",
                    "Returns HAL's current TARS-style personality settings: humor_percent and honesty_percent.",
                    new Dictionary<string, object>(),
                    new string[0]),
                CreateTool(
                    "set_hal_personality",
                    "Changes HAL's runtime TARS-style personality settings. Use when the player asks to adjust humor, seriousness, honesty, bluntness, candor, or uncertainty style. These settings affect future responses but do not control the spacecraft.",
                    new Dictionary<string, object>
                    {
                        {
                            "humor_percent",
                            new Dictionary<string, object>
                            {
                                { "type", "integer" },
                                { "description", "Humor level from 0 to 100. Low is plain and serious; high adds brief dry mission-safe wit." },
                                { "minimum", 0 },
                                { "maximum", 100 }
                            }
                        },
                        {
                            "honesty_percent",
                            new Dictionary<string, object>
                            {
                                { "type", "integer" },
                                { "description", "Candor level from 0 to 100. This changes how directly HAL states uncertainty, limitations, and corrections; it never permits false answers." },
                                { "minimum", 0 },
                                { "maximum", 100 }
                            }
                        }
                    },
                    new string[0]),
                CreateTool(
                    "get_celestial_info",
                    "Returns live read-only information about a celestial body, including orbital characteristics, atmosphere, gravity, and the active vessel's current distance to that body. Use for questions about a named body, body facts, or distance to a body.",
                    new Dictionary<string, object>
                    {
                        {
                            "body_name",
                            new Dictionary<string, object>
                            {
                                { "type", "string" },
                                { "description", "The celestial body name, such as Kerbin, Mun, Minmus, Duna, Ike, Jool, or Laythe. If omitted, the current main body is used." }
                            }
                        }
                    },
                    new string[0]),
                CreateTool(
                    "list_celestial_bodies",
                    "Lists every celestial body currently loaded by KSP, including modded planets and stars, parent bodies, system roots, and child bodies. Use when the player asks what planets, moons, stars, or systems exist, or when a named body may come from a planet pack.",
                    new Dictionary<string, object>
                    {
                        {
                            "include_details",
                            new Dictionary<string, object>
                            {
                                { "type", "boolean" },
                                { "description", "Set true to include compact physical and orbital details for every body. Leave false for discovery questions." }
                            }
                        }
                    },
                    new string[0]),
                CreateTool(
                    "list_vessel_parts",
                    "Returns a compact list of the active vessel's parts so the assistant can infer craft type and capabilities. Use when the player asks what kind of craft they are flying, what the vessel is made of, or whether it has engines, tanks, wings, command modules, science parts, docking ports, landing gear, or other capabilities.",
                    new Dictionary<string, object>
                    {
                        {
                            "include_modules",
                            new Dictionary<string, object>
                            {
                                { "type", "boolean" },
                                { "description", "Set true to include compact module names for each part. Useful for identifying engines, command modules, wings, wheels, docking ports, and science parts." }
                            }
                        },
                        {
                            "include_resources",
                            new Dictionary<string, object>
                            {
                                { "type", "boolean" },
                                { "description", "Set true to include resources stored in each part. Defaults to true." }
                            }
                        },
                        {
                            "max_parts",
                            new Dictionary<string, object>
                            {
                                { "type", "integer" },
                                { "description", "Maximum number of parts to return. Defaults to 250." }
                            }
                        }
                    },
                    new string[0]),
                CreateTool(
                    "get_part_info",
                    "Returns detailed information about a specific active-vessel part, including resources, modules, module info text, and module fields. Use after list_vessel_parts when one part needs closer inspection.",
                    new Dictionary<string, object>
                    {
                        {
                            "part_index",
                            new Dictionary<string, object>
                            {
                                { "type", "integer" },
                                { "description", "The part index returned by list_vessel_parts." }
                            }
                        },
                        {
                            "part_name",
                            new Dictionary<string, object>
                            {
                                { "type", "string" },
                                { "description", "A part internal-name substring to search for if part_index is unknown." }
                            }
                        },
                        {
                            "part_title",
                            new Dictionary<string, object>
                            {
                                { "type", "string" },
                                { "description", "A user-facing part-title substring to search for if part_index is unknown." }
                            }
                        }
                    },
                    new string[0]),
                CreateTool(
                    "get_orbit_info",
                    "Returns detailed active-vessel orbit, surface, patched-conic transition, and maneuver-reference-frame information. Use for active-vessel orbit, altitude, surface state, trajectory patch, or reference-frame questions.",
                    new Dictionary<string, object>(),
                    new string[0]),
                CreateTool(
                    "get_target_info",
                    "Returns active target information including distance, relative velocity, target orbit, relative inclination, and closest approach when available. Use for current target, rendezvous, docking target, or target-distance questions.",
                    new Dictionary<string, object>(),
                    new string[0]),
                CreateTool(
                    "get_maneuver_nodes",
                    "Returns all active maneuver nodes with UT, prograde/normal/radial components, total delta-v, and resulting patched-conic summaries. Use for questions about existing maneuver nodes.",
                    new Dictionary<string, object>(),
                    new string[0]),
                CreateTool(
                    "get_engine_status",
                    "Returns active-vessel engine status, thrust, ISP, propellants, flameout/ignition state, and acceleration estimates. Use for thrust, engine, acceleration, flameout, or burn capability questions.",
                    new Dictionary<string, object>(),
                    new string[0]),
                CreateTool(
                    "estimate_burn",
                    "Estimates burn duration and delta-v feasibility for a requested delta-v using stock vessel delta-v data and current engine status. Use for burn-duration and delta-v feasibility questions.",
                    new Dictionary<string, object>
                    {
                        {
                            "delta_v_mps",
                            new Dictionary<string, object>
                            {
                                { "type", "number" },
                                { "description", "Requested burn delta-v in meters per second. If omitted, the next maneuver node delta-v is used when available." }
                            }
                        }
                    },
                    new string[0]),
                CreateTool(
                    "get_reference_frames",
                    "Returns the active-vessel prograde, radial-plus, and normal-plus basis vectors at a specified UT or now.",
                    new Dictionary<string, object>
                    {
                        {
                            "ut",
                            new Dictionary<string, object>
                            {
                                { "type", "number" },
                                { "description", "Universal time for the requested reference frame. Defaults to current UT." }
                            }
                        }
                    },
                    new string[0]),
                CreateTool(
                    "simulate_maneuver",
                    "Returns a read-only rough two-body estimate of the orbit produced by a maneuver-vector input. Use only for rough what-if trajectory checks; it does not create or edit a KSP maneuver node and is not a full patched-conic planner.",
                    new Dictionary<string, object>
                    {
                        {
                            "ut",
                            new Dictionary<string, object>
                            {
                                { "type", "number" },
                                { "description", "Universal time of the simulated burn. Defaults to current UT." }
                            }
                        },
                        {
                            "prograde_mps",
                            new Dictionary<string, object>
                            {
                                { "type", "number" },
                                { "description", "Prograde delta-v in meters per second." }
                            }
                        },
                        {
                            "normal_mps",
                            new Dictionary<string, object>
                            {
                                { "type", "number" },
                                { "description", "Normal-plus delta-v in meters per second." }
                            }
                        },
                        {
                            "radial_mps",
                            new Dictionary<string, object>
                            {
                                { "type", "number" },
                                { "description", "Radial-plus delta-v in meters per second." }
                            }
                        }
                    },
                    new string[0])
            };
        }

        private static Dictionary<string, object> CreateTool(string name, string description, Dictionary<string, object> properties, string[] required)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters["type"] = "object";
            parameters["properties"] = properties;
            parameters["required"] = required;
            parameters["additionalProperties"] = false;

            Dictionary<string, object> function = new Dictionary<string, object>();
            function["name"] = name;
            function["description"] = description;
            function["parameters"] = parameters;

            Dictionary<string, object> tool = new Dictionary<string, object>();
            tool["type"] = "function";
            tool["function"] = function;
            return tool;
        }
    }
}
