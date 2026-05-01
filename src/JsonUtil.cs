using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HAL9000
{
    internal static class JsonUtil
    {
        public static string Serialize(object value)
        {
            StringBuilder builder = new StringBuilder();
            WriteValue(builder, value);
            return builder.ToString();
        }

        public static object Deserialize(string json)
        {
            if (json == null)
            {
                return null;
            }

            Parser parser = new Parser(json);
            return parser.ParseValue();
        }

        public static string Escape(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < 32)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(c);
                        }
                        break;
                }
            }

            return builder.ToString();
        }

        private static void WriteValue(StringBuilder builder, object value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            string text = value as string;
            if (text != null)
            {
                builder.Append('"').Append(Escape(text)).Append('"');
                return;
            }

            if (value is bool)
            {
                builder.Append((bool)value ? "true" : "false");
                return;
            }

            if (value is int || value is long || value is float || value is double || value is decimal)
            {
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            IDictionary dictionary = value as IDictionary;
            if (dictionary != null)
            {
                bool first = true;
                builder.Append('{');
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (!first)
                    {
                        builder.Append(',');
                    }

                    builder.Append('"').Append(Escape(Convert.ToString(entry.Key, CultureInfo.InvariantCulture))).Append("\":");
                    WriteValue(builder, entry.Value);
                    first = false;
                }
                builder.Append('}');
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                bool first = true;
                builder.Append('[');
                foreach (object item in enumerable)
                {
                    if (!first)
                    {
                        builder.Append(',');
                    }

                    WriteValue(builder, item);
                    first = false;
                }
                builder.Append(']');
                return;
            }

            builder.Append('"').Append(Escape(value.ToString())).Append('"');
        }

        private sealed class Parser
        {
            private readonly string json;
            private int index;

            public Parser(string json)
            {
                this.json = json;
            }

            public object ParseValue()
            {
                SkipWhitespace();
                if (index >= json.Length)
                {
                    return null;
                }

                char c = json[index];
                if (c == '{') return ParseObject();
                if (c == '[') return ParseArray();
                if (c == '"') return ParseString();
                if (c == 't' || c == 'f') return ParseBool();
                if (c == 'n')
                {
                    index += 4;
                    return null;
                }

                return ParseNumber();
            }

            private Dictionary<string, object> ParseObject()
            {
                Dictionary<string, object> obj = new Dictionary<string, object>();
                index++;
                while (true)
                {
                    SkipWhitespace();
                    if (index >= json.Length)
                    {
                        return obj;
                    }

                    if (json[index] == '}')
                    {
                        index++;
                        return obj;
                    }

                    string key = ParseString();
                    SkipWhitespace();
                    if (index < json.Length && json[index] == ':')
                    {
                        index++;
                    }

                    obj[key] = ParseValue();
                    SkipWhitespace();
                    if (index < json.Length && json[index] == ',')
                    {
                        index++;
                    }
                }
            }

            private List<object> ParseArray()
            {
                List<object> array = new List<object>();
                index++;
                while (true)
                {
                    SkipWhitespace();
                    if (index >= json.Length)
                    {
                        return array;
                    }

                    if (json[index] == ']')
                    {
                        index++;
                        return array;
                    }

                    array.Add(ParseValue());
                    SkipWhitespace();
                    if (index < json.Length && json[index] == ',')
                    {
                        index++;
                    }
                }
            }

            private string ParseString()
            {
                StringBuilder builder = new StringBuilder();
                if (index < json.Length && json[index] == '"')
                {
                    index++;
                }

                while (index < json.Length)
                {
                    char c = json[index++];
                    if (c == '"')
                    {
                        break;
                    }

                    if (c != '\\' || index >= json.Length)
                    {
                        builder.Append(c);
                        continue;
                    }

                    char escaped = json[index++];
                    switch (escaped)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u':
                            if (index + 4 <= json.Length)
                            {
                                string hex = json.Substring(index, 4);
                                int code;
                                if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                                {
                                    builder.Append((char)code);
                                }
                                index += 4;
                            }
                            break;
                        default:
                            builder.Append(escaped);
                            break;
                    }
                }

                return builder.ToString();
            }

            private bool ParseBool()
            {
                if (index + 4 <= json.Length && json.Substring(index, 4) == "true")
                {
                    index += 4;
                    return true;
                }

                index += 5;
                return false;
            }

            private object ParseNumber()
            {
                int start = index;
                while (index < json.Length)
                {
                    char c = json[index];
                    if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E')
                    {
                        index++;
                    }
                    else
                    {
                        break;
                    }
                }

                string token = json.Substring(start, index - start);
                if (token.IndexOf('.') >= 0 || token.IndexOf('e') >= 0 || token.IndexOf('E') >= 0)
                {
                    double number;
                    if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                    {
                        return number;
                    }
                }
                else
                {
                    long integer;
                    if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out integer))
                    {
                        return integer;
                    }
                }

                return 0L;
            }

            private void SkipWhitespace()
            {
                while (index < json.Length)
                {
                    char c = json[index];
                    if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                    {
                        index++;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
    }
}
