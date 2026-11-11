// SPDX-License-Identifier: GPL-2.0-or-later
namespace RedBladePlus.Studio;
internal static class EditorTests
{
    static void Check(bool value,string message){if(!value)throw new Exception("EDITOR TEST: "+message);}
    internal static void Run()
    {
        var original=StudioConfig.Default();original.Profiles[1].Keys[7]=new Hsv(34,56,78);
        var before=ProfileFile.Save(original);
        var painted=KeyboardEditing.WithColors(original,new[]{new KeyColorChange(0,"#ff0000"),new KeyColorChange(108,"#0066ff")});
        Check(ProfileFile.Save(original).SequenceEqual(before),"painting preserves its source model");
        Check(painted.Profiles[0].Keys[0]==new Hsv(0,255,255)&&painted.Profiles[1].Keys[7]==new Hsv(34,56,78),"batch changes only selected keys in active profile");
        foreach(var invalid in new[]{new[]{new KeyColorChange(0,"#ff0000"),new KeyColorChange(109,"#ffffff")},new[]{new KeyColorChange(0,"#ff0000"),new KeyColorChange(0,"#ffffff")},new[]{new KeyColorChange(0,"not-a-color")},Array.Empty<KeyColorChange>()})
        {
            bool rejected=false;try{KeyboardEditing.WithColors(original,invalid);}catch(InvalidDataException){rejected=true;}
            Check(rejected&&ProfileFile.Save(original).SequenceEqual(before),"invalid batch is rejected atomically");
        }
        var history=new EditHistory(3);history.Record(original);var undo=history.Undo(painted);
        Check(undo!=null&&ProfileFile.Save(undo).SequenceEqual(before)&&history.CanRedo&&!history.CanUndo,"undo restores the whole edit");
        var redo=history.Redo(undo!);Check(redo!=null&&ProfileFile.Save(redo).SequenceEqual(ProfileFile.Save(painted)),"redo restores a batch");
        history.Undo(redo!);history.Record(original);Check(!history.CanRedo,"new edit discards redo branch");
        history.Clear();
        for(int i=0;i<5;i++){original.Profiles[0].Master=(byte)i;history.Record(original);}
        int count=0;StudioConfig current=painted;while(history.CanUndo){current=history.Undo(current)!;count++;}
        Check(count==3&&current.Profiles[0].Master==2,"history has a bounded oldest snapshot");
        history.Clear();Check(!history.CanUndo&&!history.CanRedo,"read/reset clears both branches");
        var hardware=StudioConfig.Default();hardware.Active=2;hardware.Profiles[1].Master=77;
        original.Profiles[original.Active].Master=123;
        var merged=KeyboardEditing.WithActiveProfile(hardware,original.Profiles[original.Active]);
        original.Profiles[original.Active].Master=14;
        Check(merged.Active==2&&merged.Profiles[2].Master==123&&merged.Profiles[1].Master==77&&hardware.Profiles[2].Master==255,"connect keeps an isolated local active profile and the other keyboard profiles");
        var extended=ConfigCodec.Encode(merged);extended[32+2*416+10]=2;extended[32+2*416+11]=4;
        var decoded=ConfigCodec.Decode(extended);Check(decoded.Profiles[2].ReactiveStyle==2&&decoded.Profiles[2].ReactiveWidth==4,"reactive metadata wire roundtrip");
        foreach(var item in new[]{(10,(byte)3),(11,(byte)3),(11,(byte)65),(12,(byte)1)})
        {
            var invalid=(byte[])extended.Clone();invalid[32+2*416+item.Item1]=item.Item2;bool rejected=false;
            try{ConfigCodec.Decode(invalid);}catch(InvalidDataException){rejected=true;}Check(rejected,"invalid reactive or reserved metadata rejected");
        }
        var old=System.Text.Json.Nodes.JsonNode.Parse(ProfileFile.Save(merged))!;
        foreach(var p in old["Profiles"]!.AsArray()){p!.AsObject().Remove("ReactiveStyle");p.AsObject().Remove("ReactiveWidth");}
        Check(ProfileFile.Load(System.Text.Encoding.UTF8.GetBytes(old.ToJsonString())).Profiles.All(p=>p.ReactiveStyle==0&&p.ReactiveWidth==0),"0.10 profile JSON retains legacy reaction defaults");
    }
}
