using System;
using System.Collections.Generic;
using UnityEngine;

namespace HAL9000
{
    internal static class PartInfoTool
    {
        public static string ListVesselPartsJson(Dictionary<string, object> arguments)
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null)
            {
                return JsonUtil.Serialize(new Dictionary<string, object> { { "error", "No active vessel." } });
            }

            bool includeModules = GetArgumentBool(arguments, "include_modules");
            bool includeResources = GetArgumentBool(arguments, "include_resources", true);
            int maxParts = GetArgumentInt(arguments, "max_parts", 250);
            List<object> parts = new List<object>();

            int count = Math.Min(vessel.parts.Count, Math.Max(1, maxParts));
            for (int i = 0; i < count; i++)
            {
                Part part = vessel.parts[i];
                if (part == null)
                {
                    continue;
                }

                Dictionary<string, object> partInfo = BuildPartSummary(part, i, includeModules, includeResources);
                parts.Add(partInfo);
            }

            return JsonUtil.Serialize(new Dictionary<string, object>
            {
                { "vessel_name", vessel.vesselName },
                { "part_count", vessel.parts.Count },
                { "returned_part_count", parts.Count },
                { "truncated", vessel.parts.Count > count },
                { "parts", parts }
            });
        }

        public static string GetPartInfoJson(Dictionary<string, object> arguments)
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null)
            {
                return JsonUtil.Serialize(new Dictionary<string, object> { { "error", "No active vessel." } });
            }

            int index;
            Part part = ResolvePart(vessel, arguments, out index);
            if (part == null)
            {
                return JsonUtil.Serialize(new Dictionary<string, object>
                {
                    { "error", "Part not found. Pass part_index from list_vessel_parts, or a part_name/part_title substring." }
                });
            }

            Dictionary<string, object> detail = BuildPartSummary(part, index, true, true);
            detail["description"] = Truncate(GetAvailablePartString(part, "description"), 1200);
            detail["resources"] = GetResources(part, true);
            detail["modules"] = GetModules(part, true);
            return JsonUtil.Serialize(detail);
        }

        private static Dictionary<string, object> BuildPartSummary(Part part, int index, bool includeModules, bool includeResources)
        {
            Dictionary<string, object> partInfo = new Dictionary<string, object>();
            partInfo["index"] = index;
            partInfo["part_name"] = part.name;
            partInfo["title"] = GetAvailablePartString(part, "title");
            partInfo["manufacturer"] = GetAvailablePartString(part, "manufacturer");
            partInfo["category"] = GetAvailablePartValue(part, "category");
            partInfo["stage"] = GetMemberValue(part, "inverseStage");
            partInfo["dry_mass_t"] = CleanNumber(part.mass);
            partInfo["resource_mass_t"] = CleanNumber(SafeResourceMass(part));
            partInfo["total_mass_t"] = CleanNumber(part.mass + SafeResourceMass(part));

            if (includeResources)
            {
                partInfo["resources"] = GetResources(part, false);
            }

            if (includeModules)
            {
                partInfo["modules"] = GetModules(part, false);
            }

            return partInfo;
        }

        private static Part ResolvePart(Vessel vessel, Dictionary<string, object> arguments, out int index)
        {
            index = GetArgumentInt(arguments, "part_index", -1);
            if (index >= 0 && index < vessel.parts.Count)
            {
                return vessel.parts[index];
            }

            string query = GetArgumentString(arguments, "part_name");
            if (string.IsNullOrEmpty(query))
            {
                query = GetArgumentString(arguments, "part_title");
            }

            if (string.IsNullOrEmpty(query))
            {
                return null;
            }

            string normalizedQuery = Normalize(query);
            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part part = vessel.parts[i];
                if (part == null)
                {
                    continue;
                }

                string partName = Normalize(part.name);
                string title = Normalize(GetAvailablePartString(part, "title"));
                if (partName.Contains(normalizedQuery) || title.Contains(normalizedQuery))
                {
                    index = i;
                    return part;
                }
            }

            return null;
        }

        private static List<object> GetResources(Part part, bool includeFlowState)
        {
            List<object> resources = new List<object>();
            if (part.Resources == null)
            {
                return resources;
            }

            for (int i = 0; i < part.Resources.Count; i++)
            {
                PartResource resource = part.Resources[i];
                Dictionary<string, object> resourceInfo = new Dictionary<string, object>();
                resourceInfo["name"] = resource.resourceName;
                resourceInfo["amount"] = CleanNumber(resource.amount);
                resourceInfo["max_amount"] = CleanNumber(resource.maxAmount);
                if (includeFlowState)
                {
                    resourceInfo["flow_state"] = resource.flowState;
                }

                resources.Add(resourceInfo);
            }

            return resources;
        }

        private static List<object> GetModules(Part part, bool includeFields)
        {
            List<object> modules = new List<object>();
            if (part.Modules == null)
            {
                return modules;
            }

            for (int i = 0; i < part.Modules.Count; i++)
            {
                PartModule module = part.Modules[i];
                if (module == null)
                {
                    continue;
                }

                Dictionary<string, object> moduleInfo = new Dictionary<string, object>();
                moduleInfo["module_name"] = module.moduleName;
                moduleInfo["type"] = module.GetType().FullName;

                if (includeFields)
                {
                    string moduleText = null;
                    try
                    {
                        moduleText = module.GetInfo();
                    }
                    catch
                    {
                        moduleText = null;
                    }

                    moduleInfo["info"] = Truncate(moduleText, 1200);
                    moduleInfo["fields"] = GetModuleFields(module);
                }

                modules.Add(moduleInfo);
            }

            return modules;
        }

        private static List<object> GetModuleFields(PartModule module)
        {
            List<object> fields = new List<object>();
            if (module.Fields == null)
            {
                return fields;
            }

            int count = Math.Min(module.Fields.Count, 60);
            for (int i = 0; i < count; i++)
            {
                BaseField field = module.Fields[i];
                if (field == null)
                {
                    continue;
                }

                object value = null;
                try
                {
                    value = field.GetValue(module);
                }
                catch
                {
                    value = null;
                }

                Dictionary<string, object> fieldInfo = new Dictionary<string, object>();
                fieldInfo["name"] = field.name;
                fieldInfo["gui_name"] = field.guiName;
                fieldInfo["value"] = value == null ? null : Truncate(value.ToString(), 300);
                fields.Add(fieldInfo);
            }

            return fields;
        }

        private static double SafeResourceMass(Part part)
        {
            try
            {
                return part.GetResourceMass();
            }
            catch
            {
                return 0.0;
            }
        }

        private static string GetAvailablePartString(Part part, string memberName)
        {
            object value = GetAvailablePartValue(part, memberName);
            return value == null ? null : value.ToString();
        }

        private static object GetAvailablePartValue(Part part, string memberName)
        {
            if (part == null || part.partInfo == null)
            {
                return null;
            }

            object value = GetMemberValue(part.partInfo, memberName);
            return value == null ? null : value.ToString();
        }

        private static object GetMemberValue(object instance, string name)
        {
            if (instance == null)
            {
                return null;
            }

            Type type = instance.GetType();
            System.Reflection.PropertyInfo property = type.GetProperty(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (property != null)
            {
                return property.GetValue(instance, null);
            }

            System.Reflection.FieldInfo field = type.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(instance);
        }

        private static bool GetArgumentBool(Dictionary<string, object> arguments, string key, bool fallback = false)
        {
            object value;
            if (arguments == null || !arguments.TryGetValue(key, out value) || value == null)
            {
                return fallback;
            }

            if (value is bool)
            {
                return (bool)value;
            }

            bool parsed;
            return bool.TryParse(value.ToString(), out parsed) ? parsed : fallback;
        }

        private static int GetArgumentInt(Dictionary<string, object> arguments, string key, int fallback)
        {
            object value;
            if (arguments == null || !arguments.TryGetValue(key, out value) || value == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return fallback;
            }
        }

        private static string GetArgumentString(Dictionary<string, object> arguments, string key)
        {
            object value;
            return arguments != null && arguments.TryGetValue(key, out value) && value != null ? value.ToString() : null;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength) + "...";
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
