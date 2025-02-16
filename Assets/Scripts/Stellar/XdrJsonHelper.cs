using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class XdrJsonHelper
{
    public static JObject DeserializeXdrJson(string jsonString)
    {
        Debug.Log("DeserializeXdrJson started");
        // Deserialize into a generic object
        JObject jsonObject = JsonConvert.DeserializeObject<JObject>(jsonString);

        // Recursively process the object
        JObject result = (JObject)ProcessJson(jsonObject);
        return result;
    }

    private static object ProcessJson(object obj)
    {
        if (obj is JObject jObject)
        {
            Debug.Log("ProcessJson found object");
            // Convert to Dictionary<string, object>
            Dictionary<string, object> dict = jObject.ToObject<Dictionary<string, object>>();
            Debug.Log(dict.Count);
            // Recursively process each key-value pair
            foreach (string key in dict.Keys)
            {
                dict[key] = ProcessJson(dict[key]);
            }
            // Special case: Convert XDR "val" objects into simple types
            if (dict.ContainsKey("val"))
            {
                Debug.LogWarning(">> found val");
                var valObject = dict["val"] as JObject;
                if (valObject != null)
                {
                    if (valObject.ContainsKey("u32"))
                    {
                        Debug.Log($" u32 found with val: {valObject["u32"].ToObject<int>()}");
                    }

                    if (valObject.ContainsKey("string"))
                    {
                        Debug.Log($" string found with val: {valObject["string"].ToString()}");
                    }

                    if (valObject.ContainsKey("address"))
                    {
                        Debug.Log($" address found with val: {valObject["address"].ToString()}");
                    }

                    if (valObject.ContainsKey("symbol"))
                    {
                        Debug.Log($" u32 object found with val: {valObject["symbol"].ToString()}");
                    }
                }
                if (valObject != null)
                {
                    if (valObject.ContainsKey("u32")) return valObject["u32"].ToObject<int>();
                    if (valObject.ContainsKey("string")) return valObject["string"].ToString();
                    if (valObject.ContainsKey("address")) return valObject["address"].ToString();
                    if (valObject.ContainsKey("symbol")) return valObject["symbol"].ToString();
                }
            }
            return dict;
        }
        else if (obj is JArray jArray)
        {
            Debug.Log("ProcessJson found array");
            // Convert to List<object> and process elements recursively
            List<object> list = jArray.ToObject<List<object>>();

            for (int i = 0; i < list.Count; i++)
            {
                list[i] = ProcessJson(list[i]);
            }

            return list;
        }

        return obj; // Return as-is if it's already a primitive type
    }
}
