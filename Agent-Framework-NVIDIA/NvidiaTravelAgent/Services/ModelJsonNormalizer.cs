using System.Text.Json.Nodes;

namespace NvidiaTravelAgent.Services;

internal static class ModelJsonNormalizer
{
    public static JsonObject NormalizeFor<T>(JsonObject root)
    {
        if (typeof(T).Name == "TravelRequest")
        {
            NormalizeTravelRequest(root);
            return root;
        }

        if (typeof(T).Name == "TravelPlan")
        {
            NormalizeTravelPlan(root);
            return root;
        }

        return root;
    }

    public static void ValidateRequiredFields<T>(JsonObject root)
    {
        if (typeof(T).Name == "TravelRequest")
        {
            var destination = root["destination"]?.GetValue<string>()?.Trim();
            var days = root["days"]?.GetValue<int?>() ?? 0;
            var travelStyle = root["travelStyle"]?.GetValue<string>()?.Trim();

            if (string.IsNullOrWhiteSpace(destination) || days <= 0 || string.IsNullOrWhiteSpace(travelStyle))
            {
                throw new InvalidOperationException("需求解析失敗：模型回傳缺少必要欄位 destination、days 或 travelStyle。");
            }
        }

        if (typeof(T).Name == "TravelPlan")
        {
            var summary = root["summary"]?.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(summary))
            {
                throw new InvalidOperationException("行程生成失敗：模型回傳缺少 summary。");
            }
        }
    }

    private static void NormalizeTravelRequest(JsonObject root)
    {
        root["specialRequirements"] = NormalizeStringListNode(root["specialRequirements"]);
    }

    private static void NormalizeTravelPlan(JsonObject root)
    {
        root["transportationNotes"] = NormalizeStringListNode(root["transportationNotes"]);
        root["accommodationNotes"] = NormalizeStringListNode(root["accommodationNotes"]);
        root["cautions"] = NormalizeStringListNode(root["cautions"]);

        if (root["dailyPlans"] is not JsonArray dailyPlans)
        {
            root["dailyPlans"] = new JsonArray();
            return;
        }

        foreach (var dayNode in dailyPlans.OfType<JsonObject>())
        {
            if (dayNode["items"] is null)
            {
                dayNode["items"] = new JsonArray();
                continue;
            }

            if (dayNode["items"] is JsonObject itemObject)
            {
                dayNode["items"] = new JsonArray(itemObject);
                continue;
            }

            if (dayNode["items"] is not JsonArray)
            {
                dayNode["items"] = new JsonArray();
            }
        }
    }

    private static JsonNode NormalizeStringListNode(JsonNode? node)
    {
        if (node is null)
        {
            return new JsonArray();
        }

        if (node is JsonValue value)
        {
            var single = ConvertNodeToString(value);
            return string.IsNullOrWhiteSpace(single) ? new JsonArray() : new JsonArray(single);
        }

        if (node is JsonObject obj)
        {
            var flattened = FlattenObject(obj);
            return string.IsNullOrWhiteSpace(flattened) ? new JsonArray() : new JsonArray(flattened);
        }

        if (node is JsonArray array)
        {
            var normalized = new JsonArray();
            foreach (var item in array)
            {
                var text = ConvertNodeToString(item);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    normalized.Add(text);
                }
            }

            return normalized;
        }

        return new JsonArray();
    }

    private static string ConvertNodeToString(JsonNode? node)
    {
        return node switch
        {
            null => string.Empty,
            JsonValue value => value.TryGetValue<string>(out var text) ? text : value.ToJsonString().Trim('"'),
            JsonObject obj => FlattenObject(obj),
            JsonArray array => string.Join("；", array.Select(ConvertNodeToString).Where(static x => !string.IsNullOrWhiteSpace(x))),
            _ => node.ToJsonString()
        };
    }

    private static string FlattenObject(JsonObject obj)
    {
        return string.Join(
            "；",
            obj.Select(pair =>
            {
                var value = ConvertNodeToString(pair.Value);
                return string.IsNullOrWhiteSpace(value) ? null : $"{pair.Key}: {value}";
            }).Where(static x => !string.IsNullOrWhiteSpace(x))!);
    }
}
