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
                    "get_celestial_info",
                    "Returns live read-only information about a celestial body, including orbital characteristics, atmosphere, gravity, and the active vessel's current distance to that body.",
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
                    "Lists every celestial body currently loaded by KSP, including modded planets and stars, parent bodies, system roots, and child bodies.",
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
                    "Returns a compact list of the active vessel's parts so the assistant can infer craft type and capabilities.",
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
                    "Returns detailed information about a specific active-vessel part, including resources, modules, module info text, and module fields.",
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
