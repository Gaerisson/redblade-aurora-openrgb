// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text.RegularExpressions;
namespace RedBladePlus.Studio;
internal sealed record KeyColorChange(int Led,string Color);
internal static class KeyboardEditing
{
    internal static StudioConfig Clone(StudioConfig config)=>ProfileFile.Load(ProfileFile.Save(config));
    internal static StudioConfig WithActiveProfile(StudioConfig source,OnboardProfile profile)
    {
        var next=Clone(source);next.Profiles[next.Active]=profile;return Clone(next);
    }
    internal static StudioConfig WithColors(StudioConfig source,IReadOnlyList<KeyColorChange> colors)
    {
        if(colors is null||colors.Count is <1 or >109)throw new InvalidDataException("Select between 1 and 109 keys.");
        var seen=new HashSet<int>();
        foreach(var c in colors)
            if(c is null||c.Led is <0 or >108||!seen.Add(c.Led)||c.Color is null||!Regex.IsMatch(c.Color,"^#[0-9a-fA-F]{6}$"))
                throw new InvalidDataException("Invalid or duplicate key color.");
        var next=Clone(source);
        foreach(var c in colors)next.Profiles[next.Active].Keys[c.Led]=Hsv.FromColor(ColorTranslator.FromHtml(c.Color));
        return next;
    }
}
internal sealed class EditHistory
{
    readonly int limit;readonly List<byte[]> past=new(),future=new();
    internal EditHistory(int limit=24){if(limit is <1 or >64)throw new ArgumentOutOfRangeException(nameof(limit));this.limit=limit;}
    internal bool CanUndo=>past.Count!=0;
    internal bool CanRedo=>future.Count!=0;
    void Push(List<byte[]> list,StudioConfig config){list.Add(ProfileFile.Save(config));if(list.Count>limit)list.RemoveAt(0);}
    internal void Record(StudioConfig before){Push(past,before);future.Clear();}
    internal StudioConfig? Undo(StudioConfig current)=>Move(past,future,current);
    internal StudioConfig? Redo(StudioConfig current)=>Move(future,past,current);
    StudioConfig? Move(List<byte[]> from,List<byte[]> to,StudioConfig current)
    {
        if(from.Count==0)return null;
        var next=ProfileFile.Load(from[^1]);Push(to,current);from.RemoveAt(from.Count-1);return next;
    }
    internal void Clear(){past.Clear();future.Clear();}
}
