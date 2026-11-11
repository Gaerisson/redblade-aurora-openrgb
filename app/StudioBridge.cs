// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text.Json;
using System.Text.RegularExpressions;
namespace RedBladePlus.Studio;

internal sealed record StudioRequest(string Id,string Action,JsonElement Args);
internal static class StudioBridge
{
    internal static readonly JsonSerializerOptions Json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
    static readonly HashSet<string> Actions=new(StringComparer.Ordinal){"getState","view","language","setKeys","setKeyColors","undo","redo","setEffectColor","patchProfile","connect","save","preview","control","importProfile","exportProfile","resetProfile","checkUpdates","downloadUpdate","installUpdate","updatePreferences","flash","cancelJob","startEffect","updateEffect","stopEffect","openLink"};
    internal static StudioRequest Parse(string json)
    {
        if(json.Length>131072)throw new InvalidDataException("Message too large.");
        using var document=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=12});var root=document.RootElement;
        Fields(root,"id","action","args");
        string id=root.GetProperty("id").GetString()??"",action=root.GetProperty("action").GetString()??"";
        if(!Regex.IsMatch(id,"^[A-Za-z0-9_-]{1,64}$")||!Actions.Contains(action))throw new InvalidDataException("Unknown interface action.");
        var args=root.GetProperty("args");if(args.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("Expected action arguments.");
        return new(id,action,args.Clone());
    }
    internal static void Fields(JsonElement value,params string[] allowed)
    {
        if(value.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("Expected object.");
        var seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(var property in value.EnumerateObject())if(!allowed.Contains(property.Name,StringComparer.Ordinal)||!seen.Add(property.Name))throw new InvalidDataException("Unexpected or duplicate property: "+property.Name);
    }
    internal static string Text(JsonElement args,string name,int maximum=64)
    {
        string text=args.GetProperty(name).GetString()??throw new InvalidDataException("Missing "+name);
        if(text.Length>maximum||text.Any(char.IsControl))throw new InvalidDataException("Invalid "+name);return text;
    }
    internal static int Number(JsonElement args,string name,int minimum,int maximum)
    {
        if(!args.GetProperty(name).TryGetInt32(out int number)||number<minimum||number>maximum)throw new InvalidDataException("Invalid "+name);return number;
    }
    internal static Color Color(JsonElement args,string name)
    {
        string text=Text(args,name,7);if(!Regex.IsMatch(text,"^#[0-9a-fA-F]{6}$"))throw new InvalidDataException("Expected #RRGGBB.");return ColorTranslator.FromHtml(text);
    }
    internal static bool AllowedOrigin(string? source)
    {
        return Uri.TryCreate(source,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.Host=="redblade.local"&&uri.IsDefaultPort&&string.IsNullOrEmpty(uri.UserInfo)&&string.IsNullOrEmpty(uri.Query)&&uri.AbsolutePath=="/index.html";
    }
}
