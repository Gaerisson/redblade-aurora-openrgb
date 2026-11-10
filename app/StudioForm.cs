// SPDX-License-Identifier: GPL-2.0-or-later
using System.Diagnostics;
using System.Drawing.Drawing2D;
namespace RedBladePlus.Studio;

internal static class Theme
{
    internal static readonly Color Back=Color.FromArgb(14,15,19),Panel=Color.FromArgb(22,23,28),Raised=Color.FromArgb(31,32,39),Border=Color.FromArgb(54,55,64),Text=Color.FromArgb(239,239,243),Muted=Color.FromArgb(163,164,177),Red=Color.FromArgb(221,0,43);
    internal static Button Button(string label,Action action,bool primary=false)
    {
        var b=new StudioButton{Text=label,AutoSize=false,Height=38,Width=146,FlatStyle=FlatStyle.Flat,BackColor=primary?Red:Raised,ForeColor=Text,Font=new Font("Segoe UI",9.5f,FontStyle.Bold),Cursor=Cursors.Hand,Margin=new Padding(0,0,10,0)};b.FlatAppearance.BorderColor=primary?Red:Border;b.FlatAppearance.MouseOverBackColor=primary?Color.FromArgb(240,15,58):Color.FromArgb(43,48,59);b.Click+=(_,_)=>action();return b;
    }
    internal static Label Label(string text,float size=10,Color? color=null,bool bold=false)=>new(){Text=text,ForeColor=color??Text,BackColor=Color.Transparent,Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular),AutoSize=false};
    internal static StudioChoice Combo()=>new(){BackColor=Raised,ForeColor=Text};
}
internal sealed class StudioChoice:Control
{
    internal List<string> Items{get;}=new();int selected=-1;ContextMenuStrip? menu;
    internal event EventHandler? SelectedIndexChanged;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal int SelectedIndex
    {
        get=>selected;set{if(value<0||value>=Items.Count)throw new ArgumentOutOfRangeException(nameof(value));if(selected==value)return;selected=value;Text=Items[value];Invalidate();SelectedIndexChanged?.Invoke(this,EventArgs.Empty);}
    }
    internal StudioChoice(){DoubleBuffered=true;TabStop=true;AccessibleRole=AccessibleRole.ComboBox;Cursor=Cursors.Hand;Size=new Size(278,31);}
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);using var border=new Pen(Focused?Theme.Red:Theme.Border);e.Graphics.DrawRectangle(border,0,0,Width-1,Height-1);
        TextRenderer.DrawText(e.Graphics,selected>=0?Items[selected]:"",Font,new Rectangle(9,0,Width-36,Height),Enabled?ForeColor:Theme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        using var brush=new SolidBrush(Theme.Muted);int x=Width-18,y=Height/2;e.Graphics.FillPolygon(brush,new[]{new Point(x-4,y-2),new Point(x+4,y-2),new Point(x,y+3)});
    }
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);Focus();if(e.Button==MouseButtons.Left)Open();}
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);if(e.KeyCode is Keys.Enter or Keys.Space||(e.Alt&&e.KeyCode==Keys.Down)){Open();e.Handled=true;}
        else if(e.KeyCode is Keys.Down or Keys.Up){SelectedIndex=Math.Clamp(selected+(e.KeyCode==Keys.Down?1:-1),0,Items.Count-1);e.Handled=true;}
    }
    void Open()
    {
        if(!Enabled||Items.Count==0||menu!=null)return;menu=new ContextMenuStrip{BackColor=Theme.Raised,ForeColor=Theme.Text,ShowImageMargin=false,ShowCheckMargin=true,Font=Font,MaximumSize=new Size(Math.Max(Width,120),420)};
        for(int i=0;i<Items.Count;i++){int index=i;var item=new ToolStripMenuItem(Items[i]){Checked=i==selected,ForeColor=Theme.Text,BackColor=Theme.Raised,AutoSize=false,Size=new Size(Math.Max(Width-12,100),29)};item.Click+=(_,_)=>SelectedIndex=index;menu.Items.Add(item);}
        menu.Closed+=(_,_)=>{var old=menu;menu=null;old?.Dispose();};menu.Show(this,new Point(0,Height));
    }
    protected override void Dispose(bool disposing){if(disposing){var old=menu;menu=null;old?.Dispose();}base.Dispose(disposing);}
}
internal sealed class StudioCheckBox:CheckBox
{
    internal StudioCheckBox(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        int box=Math.Max(14,LogicalToDeviceUnits(15));int x=1,y=(Height-box)/2;
        var bounds=new Rectangle(x,y,box,box);
        using var fill=new SolidBrush(Checked?Theme.Red:Theme.Raised);e.Graphics.FillRectangle(fill,bounds);
        using var border=new Pen(Checked?Theme.Red:Theme.Border);e.Graphics.DrawRectangle(border,x,y,box-1,box-1);
        if(Checked){using var mark=new Pen(Enabled?Color.White:Theme.Muted,Math.Max(1.5f,DeviceDpi/96f*1.7f)){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round};e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.DrawLines(mark,new[]{new PointF(x+box*.22f,y+box*.52f),new PointF(x+box*.43f,y+box*.72f),new PointF(x+box*.79f,y+box*.30f)});}
        var textBounds=new Rectangle(x+box+8,0,Math.Max(0,Width-box-9),Height);
        TextRenderer.DrawText(e.Graphics,Text,Font,textBounds,Enabled?ForeColor:Theme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(textBounds,-1,-2),ForeColor,BackColor);
    }
}
internal sealed class StudioButton:Button
{
    protected override void OnPaint(PaintEventArgs e)
    {
        using var fill=new SolidBrush(Enabled?BackColor:(BackColor==Theme.Red?Color.FromArgb(75,31,40):Theme.Panel));e.Graphics.FillRectangle(fill,ClientRectangle);
        using var border=new Pen(Enabled?FlatAppearance.BorderColor:Theme.Border);e.Graphics.DrawRectangle(border,0,0,Width-1,Height-1);
        var rectangle=ClientRectangle;rectangle=Rectangle.FromLTRB(rectangle.Left+Padding.Left+7,rectangle.Top,rectangle.Right-Padding.Right-7,rectangle.Bottom);
        var alignment=TextAlign==ContentAlignment.MiddleLeft?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter;
        TextRenderer.DrawText(e.Graphics,Text,Font,rectangle,Enabled?ForeColor:Theme.Muted,alignment|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-4,-4),ForeColor,BackColor);
    }
}
internal sealed class StudioForm:Form
{
    StudioConfig config=StudioConfig.Default();int selectedProfile;bool french,dirty,busy,azerty=true;readonly bool preview;string page="Keyboard";
    uint? generation;bool unsupported;string firmware="";readonly Panel content=new(),sidebar=new(),footer=new();readonly FlowLayoutPanel profileTabs=new();
    readonly Label title=Theme.Label("",27,null,true),subtitle=Theme.Label("",10,Theme.Muted),status=Theme.Label("",9,Theme.Muted);
    readonly Label sidebarNote=Theme.Label("",8,Theme.Muted);
    readonly Dictionary<string,Button> navigation=new();Button read=null!,save=null!,previewButton=null!,cancel=null!;KeyboardCanvas? keyboard;string? jobId;
    readonly Dictionary<string,string> fr=new(){["Keyboard"]="Clavier",["Functions"]="Fonctions",["Colors"]="Couleurs",["Profiles"]="Profils",["Firmware"]="Firmware"};
    string T(string en,string translated)=>french?translated:en;
    internal StudioForm(bool preview)
    {
        this.preview=preview;Text="RedBlade+ Studio";Width=1360;Height=860;MinimumSize=new Size(1180,760);StartPosition=FormStartPosition.CenterScreen;BackColor=Theme.Back;ForeColor=Theme.Text;Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;
        sidebar.Dock=DockStyle.Left;sidebar.Width=198;sidebar.BackColor=Theme.Panel;Controls.Add(sidebar);
        using var brandStream=typeof(StudioForm).Assembly.GetManifestResourceStream("brand.png") ?? throw new InvalidOperationException("Brand image missing.");
        using var brandSource=Image.FromStream(brandStream);
        var brand=new PictureBox{Image=new Bitmap(brandSource),SizeMode=PictureBoxSizeMode.Zoom,BackColor=Theme.Panel};brand.SetBounds(13,20,172,87);sidebar.Controls.Add(brand);Disposed+=(_,_)=>brand.Image?.Dispose();
        var studio=Theme.Label("S T U D I O",9,Theme.Text,true);studio.SetBounds(26,112,150,24);sidebar.Controls.Add(studio);
        var tag=Theme.Label("FS ISO  /  109 LEDs",8,Theme.Muted);tag.SetBounds(26,137,160,22);sidebar.Controls.Add(tag);
        int n=0;foreach(string name in new[]{"Keyboard","Functions","Colors","Profiles","Firmware"}){string target=name;var button=Theme.Button(name,()=>Navigate(target));button.SetBounds(16,182+n++*50,166,43);button.TextAlign=ContentAlignment.MiddleLeft;button.Padding=new Padding(14,0,0,0);sidebar.Controls.Add(button);navigation[name]=button;}
        var language=Theme.Button("EN  /  FR",()=>{french=!french;RefreshShell();});language.SetBounds(24,Height-151,150,34);language.Anchor=AnchorStyles.Bottom|AnchorStyles.Left;sidebar.Controls.Add(language);
        sidebarNote.SetBounds(26,Height-105,170,38);sidebarNote.Anchor=AnchorStyles.Bottom|AnchorStyles.Left;sidebar.Controls.Add(sidebarNote);
        var main=new Panel{Dock=DockStyle.Fill,Padding=new Padding(30,22,30,0)};Controls.Add(main);main.BringToFront();
        var header=new Panel{Dock=DockStyle.Top,Height=111};main.Controls.Add(header);title.SetBounds(0,0,800,42);subtitle.SetBounds(1,49,950,28);header.Controls.Add(title);header.Controls.Add(subtitle);
        profileTabs.Dock=DockStyle.Top;profileTabs.Height=61;profileTabs.WrapContents=false;profileTabs.Resize+=(_,_)=>ResizeProfileTabs();main.Controls.Add(profileTabs);profileTabs.BringToFront();
        footer.Dock=DockStyle.Bottom;footer.Height=108;main.Controls.Add(footer);status.SetBounds(0,62,1000,38);status.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Bottom;footer.Controls.Add(status);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Top,Height=49,WrapContents=false};footer.Controls.Add(actions);
        read=Theme.Button("",()=>_=RunJob(HardwareOperation.Read));save=Theme.Button("",()=>_=RunJob(HardwareOperation.Save),true);save.Width=181;previewButton=Theme.Button("",()=>_=RunJob(HardwareOperation.Preview));previewButton.Width=157;cancel=Theme.Button("",()=>{if(jobId!=null)HardwareJobs.Cancel(jobId);});cancel.Width=130;cancel.Visible=false;actions.Controls.AddRange(new Control[]{read,save,previewButton,cancel});
        content.Dock=DockStyle.Fill;content.AutoScroll=true;main.Controls.Add(content);
        main.Controls.SetChildIndex(content,0);main.Controls.SetChildIndex(footer,1);main.Controls.SetChildIndex(profileTabs,2);main.Controls.SetChildIndex(header,3);
        FormClosing+=(_,e)=>{if(busy){e.Cancel=true;MessageBox.Show(this,T("A hardware operation is active. Keep Studio open until RGB restoration finishes.","Une opération est en cours. Gardez Studio ouvert jusqu’au rétablissement du RGB."),Text,MessageBoxButtons.OK,MessageBoxIcon.Information);}};
        RefreshShell();
    }
    internal void ShowPreviewPage(string name,bool useFrench)
    {
        if(!preview||!navigation.ContainsKey(name))throw new ArgumentException("Unknown preview page.");page=name;french=useFrench;RefreshShell();
    }
    OnboardProfile Current=>config.Profiles[selectedProfile];
    string ProfileName(int index)
    {
        var p=config.Profiles[index];return p.GeneratedName&&p.Name==$"Profile {index+1}"?T("Profile ","Profil ")+(index+1):p.Name;
    }
    void Changed(){dirty=true;UpdateStatus();keyboard?.Invalidate();}
    void Navigate(string target){if(busy)return;page=target;RefreshShell();}
    void RefreshShell()
    {
        sidebarNote.Text=T("BUILT-IN MODULES\nOFFLINE EDITING  ·  v0.9","MODULES INTÉGRÉS\nÉDITION HORS LIGNE  ·  v0.9");
        title.Text=french?fr[page]:page;subtitle.Text=page switch{"Keyboard"=>T("Your keyboard. Your colors. Saved onboard when you choose.","Votre clavier. Vos couleurs. Enregistrées à bord quand vous le décidez."),"Functions"=>T("Ten built-in modules. Enable only what fits the way you play.","Dix modules intégrés. Activez ceux qui correspondent à votre usage."),"Colors"=>T("Give every effect its own signature. Dynamic intensity still follows the firmware.","Une couleur pour chaque effet. L’intensité dynamique reste pilotée par le firmware."),"Profiles"=>T("Four complete onboard profiles. A portable library on your PC.","Quatre profils complets à bord. Une bibliothèque portable sur votre PC."),_=>T("Guided updates with private backups, identity checks and verification.","Mises à jour guidées avec sauvegardes privées et vérification d’identité.")};
        foreach(var pair in navigation){pair.Value.Text=french?fr[pair.Key]:pair.Key;pair.Value.BackColor=pair.Key==page?Color.FromArgb(64,18,29):Theme.Panel;pair.Value.FlatAppearance.BorderColor=pair.Key==page?Theme.Red:Theme.Panel;}
        profileTabs.Controls.Clear();for(int i=0;i<4;i++){int index=i;var tab=Theme.Button($"0{i+1}  {ProfileName(i)}"+(config.Active==i?"  ●":""),()=>{selectedProfile=index;RefreshShell();});tab.Width=Math.Min(224,Math.Max(180,(profileTabs.ClientSize.Width-40)/4));tab.Height=39;tab.BackColor=selectedProfile==i?Theme.Raised:Theme.Back;tab.FlatAppearance.BorderColor=selectedProfile==i?Theme.Red:Theme.Border;profileTabs.Controls.Add(tab);}
        foreach(Control control in content.Controls.Cast<Control>().ToArray())control.Dispose();content.Controls.Clear();keyboard=null;
        switch(page){case "Keyboard":BuildKeyboard();break;case "Functions":BuildFunctions();break;case "Colors":BuildColors();break;case "Profiles":BuildProfiles();break;case "Firmware":BuildFirmware();break;}
        read.Text=T("Read keyboard","Lire le clavier");save.Text=T("Save all 4 onboard","Enregistrer les 4");previewButton.Text=T("Preview keys · 5s","Aperçu touches · 5s");cancel.Text=T("Cancel before flash","Annuler avant flash");UpdateStatus();
    }
    void ResizeProfileTabs(){foreach(Control tab in profileTabs.Controls)tab.Width=Math.Min(224,Math.Max(180,(profileTabs.ClientSize.Width-40)/4));}
    void UpdateStatus()
    {
        save.Enabled=!preview&&!busy&&!unsupported&&generation.HasValue;read.Enabled=!preview&&!busy;previewButton.Enabled=!preview&&!busy&&!unsupported&&generation.HasValue;
        if(!busy)
        {
            string connection=preview?T("PREVIEW RENDER · hardware actions disabled","RENDU D’APERÇU · actions clavier désactivées"):unsupported?firmware+" · "+T("Full 0.9.0+ required for onboard Studio profiles","Full 0.9.0+ requis pour les profils Studio"):generation.HasValue?$"{firmware}  ·  "+T("generation ","génération ")+generation:T("OFFLINE · Read keyboard to enable Save/Preview · no device access on startup","HORS LIGNE · Lisez le clavier pour activer Enregistrer/Aperçu · aucun accès au démarrage");
            status.Text=connection+(dirty?T("  ·  unsaved edits","  ·  modifications non enregistrées"):"");status.ForeColor=dirty?Color.FromArgb(255,193,110):generation.HasValue&&!unsupported?Color.FromArgb(147,209,175):Theme.Muted;
        }
    }
    Panel Card(int x,int y,int width,int height){var panel=new Panel{Location=new Point(x,y),Size=new Size(width,height),BackColor=Theme.Panel,Padding=new Padding(18)};panel.Paint+=(_,e)=>{using var border=new Pen(Theme.Border);e.Graphics.DrawRectangle(border,0,0,panel.Width-1,panel.Height-1);};content.Controls.Add(panel);return panel;}
    void BuildKeyboard()
    {
        keyboard=new KeyboardCanvas(()=>Current,()=>azerty);keyboard.SetBounds(0,0,Math.Max(900,content.Width-2),330);keyboard.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;content.Controls.Add(keyboard);
        var count=Theme.Label(T("Click a key · Ctrl+click to select several","Cliquez une touche · Ctrl+clic pour en sélectionner plusieurs"),9,Theme.Muted);count.SetBounds(4,339,680,23);content.Controls.Add(count);
        var legends=Theme.Combo();legends.SetBounds(Math.Max(640,content.Width-280),333,278,31);legends.Anchor=AnchorStyles.Top|AnchorStyles.Right;legends.AccessibleName=T("Physical keyboard legends","Légendes du clavier physique");legends.Items.AddRange(new[]{T("Legends · AZERTY / ISO","Légendes · AZERTY / ISO"),T("Legends · QWERTY / ISO","Légendes · QWERTY / ISO")});legends.SelectedIndex=azerty?0:1;legends.SelectedIndexChanged+=(_,_)=>{azerty=legends.SelectedIndex==0;keyboard.Invalidate();};content.Controls.Add(legends);
        keyboard.SelectionChanged+=()=>count.Text=T($"{keyboard.Selection.Count} selected · Ctrl+click adds or removes keys",$"{keyboard.Selection.Count} sélectionnée(s) · Ctrl+clic ajoute ou retire une touche");
        var tools=new FlowLayoutPanel{Location=new Point(0,369),Size=new Size(1000,48),WrapContents=false};content.Controls.Add(tools);
        tools.Controls.Add(Theme.Button(T("Select all 109","Tout sélectionner"),()=>keyboard.SelectAll()));
        tools.Controls.Add(Theme.Button(T("Clear selection","Effacer la sélection"),()=>keyboard.ClearSelection()));
        var choose=Theme.Button(T("Choose key color…","Couleur des touches…"),()=>{if(keyboard.Selection.Count==0){MessageBox.Show(T("Select one or more keys first.","Sélectionnez d’abord une ou plusieurs touches."));return;}using var picker=new ColorDialog{FullOpen=true,Color=Current.Keys[keyboard.Selection.First()].ToColor()};if(picker.ShowDialog(this)==DialogResult.OK){foreach(int led in keyboard.Selection)Current.Keys[led]=Hsv.FromColor(picker.Color);Changed();}},true);choose.Width=200;tools.Controls.Add(choose);
        var hint=Theme.Label(T("Choose a color, try it, then save it to your keyboard.","Choisissez une couleur, testez-la, puis enregistrez-la dans le clavier."),10,Theme.Muted);hint.SetBounds(4,432,990,46);content.Controls.Add(hint);
    }
    void BuildFunctions()
    {
        for(int i=0;i<10;i++)
        {
            int bit=i;var card=Card((i%2)*490,(i/2)*66,474,56);var check=new StudioCheckBox{Text=french?Defaults.ModulesFr[i]:Defaults.ModuleNames[i],Checked=(Current.Modules&(1<<i))!=0,ForeColor=Theme.Text,BackColor=Theme.Panel,Font=new Font("Segoe UI",10,FontStyle.Bold),Location=new Point(18,16),Size=new Size(430,25)};check.CheckedChanged+=(_,_)=>{Current.Modules=(ushort)(check.Checked?Current.Modules|(1<<bit):Current.Modules&~(1<<bit));Changed();};card.Controls.Add(check);
        }
        int y=350;var settings=Card(0,y,964,190);
        AddNumeric(settings,T("Master brightness","Luminosité globale"),0,0,255,Current.Master,v=>Current.Master=(byte)v);
        AddNumeric(settings,T("Host timeout · seconds","Délai hôte · secondes"),1,1,60,Current.TimeoutSeconds,v=>Current.TimeoutSeconds=(byte)v);
        AddNumeric(settings,T("Reactive trail · frames","Traînée réactive · images"),2,1,15,Current.TrailFrames,v=>Current.TrailFrames=(byte)v);
        AddCombo(settings,T("Default lighting","Éclairage par défaut"),3,new[]{T("Host / OpenRGB","Hôte / OpenRGB"),T("Local per-key colors","Couleurs locales"),T("Accent / Red","Accent / Rouge"),T("Off","Éteint")},Current.DefaultMode,v=>Current.DefaultMode=(byte)v);
        AddCombo(settings,T("Turbo · presses/s","Turbo · pressions/s"),4,Defaults.TurboRates.Select(x=>x.ToString()).ToArray(),Array.IndexOf(Defaults.TurboRates,(int)Current.TurboRate),v=>Current.TurboRate=(byte)Defaults.TurboRates[v]);
        AddCombo(settings,T("Recorder speed","Vitesse enregistreur"),5,Defaults.Speeds,Current.RecorderSpeed,v=>Current.RecorderSpeed=(byte)v);
        var note=Theme.Label(T("These are runtime enables for built-in firmware modules. Saving never starts a generated key or mouse action.","Ces cases activent des modules intégrés au firmware. L’enregistrement ne démarre aucune action clavier ou souris."),9,Theme.Muted);note.SetBounds(0,y+205,964,50);content.Controls.Add(note);
    }
    void AddNumeric(Panel panel,string name,int index,int minimum,int maximum,int value,Action<int> set)
    {
        int x=18+(index%3)*310,y=15+(index/3)*86;var label=Theme.Label(name,9,Theme.Muted);label.SetBounds(x,y,285,25);panel.Controls.Add(label);
        var input=new NumericUpDown{Minimum=minimum,Maximum=maximum,Value=value,Location=new Point(x,y+29),Size=new Size(278,30),BackColor=Theme.Raised,ForeColor=Theme.Text,BorderStyle=BorderStyle.FixedSingle};input.ValueChanged+=(_,_)=>{set((int)input.Value);Changed();};panel.Controls.Add(input);
    }
    void AddCombo(Panel panel,string name,int index,string[] choices,int value,Action<int> set)
    {
        int x=18+(index%3)*310,y=15+(index/3)*86;var label=Theme.Label(name,9,Theme.Muted);label.SetBounds(x,y,285,25);panel.Controls.Add(label);
        var input=Theme.Combo();input.AccessibleName=name;input.Location=new Point(x,y+29);input.Size=new Size(278,31);input.Items.AddRange(choices);input.SelectedIndex=value;input.SelectedIndexChanged+=(_,_)=>{set(input.SelectedIndex);Changed();};panel.Controls.Add(input);
    }
    void BuildColors()
    {
        for(int i=0;i<24;i++)
        {
            int effect=i;var card=Card((i%4)*245,(i/4)*86,231,73);var button=new Button{Text=french?Defaults.EffectsFr[i]:Defaults.EffectNames[i],ForeColor=Theme.Text,BackColor=Theme.Panel,FlatStyle=FlatStyle.Flat,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(37,0,5,0),Font=new Font("Segoe UI",9),Dock=DockStyle.Fill};button.FlatAppearance.BorderSize=0;
            button.Paint+=(_,e)=>{using var brush=new SolidBrush(Current.Effects[effect].ToColor());e.Graphics.FillEllipse(brush,8,17,21,21);};button.Click+=(_,_)=>{using var picker=new ColorDialog{FullOpen=true,Color=Current.Effects[effect].ToColor()};if(picker.ShowDialog(this)==DialogResult.OK){Current.Effects[effect]=Hsv.FromColor(picker.Color);button.Invalidate();Changed();}};card.Padding=new Padding(7);card.Controls.Add(button);
        }
        var note=Theme.Label(T("24 independent base colors · breathing, blink and master brightness still apply. Click a swatch to edit.","24 couleurs de base indépendantes · respiration, clignotement et luminosité globale restent actifs."),9,Theme.Muted);note.SetBounds(0,528,970,46);content.Controls.Add(note);
    }
    void BuildProfiles()
    {
        for(int i=0;i<4;i++)
        {
            int index=i;var card=Card((i%2)*490,(i/2)*172,474,156);var number=Theme.Label($"0{i+1}",19,Theme.Red,true);number.SetBounds(18,16,55,32);card.Controls.Add(number);
            var name=new TextBox{Text=ProfileName(i),MaxLength=64,Location=new Point(82,20),Size=new Size(365,28),BackColor=Theme.Raised,ForeColor=Theme.Text,BorderStyle=BorderStyle.FixedSingle};name.TextChanged+=(_,_)=>{config.Profiles[index].Name=name.Text;config.Profiles[index].GeneratedName=false;Changed();};card.Controls.Add(name);
            var use=Theme.Button(config.Active==i?T("● Active at boot","● Actif au démarrage"):T("Use at boot","Utiliser au démarrage"),()=>{config.Active=index;Changed();RefreshShell();},config.Active==i);use.SetBounds(18,91,210,36);card.Controls.Add(use);
            var copy=Theme.Button(T("Copy edited profile here","Copier le profil édité"),()=>{if(index==selectedProfile)return;var clone=ProfileFile.Load(ProfileFile.Save(config));string old=config.Profiles[index].Name;bool generated=config.Profiles[index].GeneratedName;config.Profiles[index]=clone.Profiles[selectedProfile];config.Profiles[index].Name=old;config.Profiles[index].GeneratedName=generated;Changed();RefreshShell();});copy.SetBounds(244,91,210,36);card.Controls.Add(copy);
        }
        var actions=new FlowLayoutPanel{Location=new Point(0,367),Size=new Size(990,48)};actions.Controls.Add(Theme.Button(T("Import JSON…","Importer JSON…"),Import));actions.Controls.Add(Theme.Button(T("Export JSON…","Exporter JSON…"),Export,true));var reset=Theme.Button(T("Reset edited profile","Réinitialiser le profil"),()=>{if(MessageBox.Show(this,T("Reset this offline profile to defaults?","Réinitialiser ce profil hors ligne ?"),Text,MessageBoxButtons.YesNo)==DialogResult.Yes){config.Profiles[selectedProfile]=new(){Name=$"Profile {selectedProfile+1}",GeneratedName=true};Changed();RefreshShell();}});reset.Width=215;actions.Controls.Add(reset);content.Controls.Add(actions);
        var note=Theme.Label(T("JSON contains settings and colors only. VIA layers and private macros are backed up separately during flashing.\nOnboard profile names live in your PC library; all four payloads and the active index are stored on the keyboard.","Le JSON ne contient que les réglages et couleurs. Les couches VIA et macros privées sont sauvegardées séparément lors d’un flash.\nLes noms restent sur le PC ; les quatre profils et l’index actif sont enregistrés dans le clavier."),9,Theme.Muted);note.SetBounds(0,432,970,78);content.Controls.Add(note);
    }
    void Import()
    {
        using var dialog=new OpenFileDialog{Filter="RedBlade+ profile (*.json)|*.json",CheckFileExists=true};if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        try{config=ProfileFile.LoadFile(dialog.FileName);Changed();RefreshShell();}catch(Exception error){ShowError(error);}
    }
    void Export()
    {
        using var dialog=new SaveFileDialog{Filter="RedBlade+ profile (*.json)|*.json",FileName="redblade-profiles.json",DefaultExt="json",AddExtension=true};if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        try{File.WriteAllBytes(dialog.FileName,ProfileFile.Save(config));status.Text=T("Profiles exported. No keyboard setting was changed.","Profils exportés. Aucun réglage du clavier n’a changé.");}catch(Exception error){ShowError(error);}
    }
    void BuildFirmware()
    {
        var card=Card(0,0,964,310);var heading=Theme.Label(T("A checked path to your next firmware","Un parcours vérifié vers votre prochain firmware"),17,null,true);heading.SetBounds(22,21,920,38);card.Controls.Add(heading);
        var description=Theme.Label(T("1  Back up VIA layers, macros and supported onboard profiles privately.\n2  Hold Fn + Esc for 2 seconds when instructed.\n3  Verify the unique SN32F24XB / 64 KB / CS0 bootloader.\n4  Program the bundled image, verify checksum and reconnect.\n5  Verify firmware identity, preserve/restore compatible settings, return RGB control.","1  Sauvegarde privée de VIA, des macros et des profils pris en charge.\n2  Maintenez Fn + Échap pendant 2 secondes à l’invitation.\n3  Vérification du bootloader unique SN32F24XB / 64 Ko / CS0.\n4  Écriture de l’image intégrée, contrôle de somme et reconnexion.\n5  Vérification du firmware, des réglages compatibles et retour du contrôle RGB."),10,Theme.Muted);description.SetBounds(24,76,915,147);card.Controls.Add(description);
        var edition=Theme.Combo();edition.AccessibleName=T("Firmware edition","Édition du firmware");edition.Location=new Point(24,249);edition.Size=new Size(470,31);
        string full="Full",core="Core";try{full+=" "+FirmwareImage.Load("full").Version;core+=" "+FirmwareImage.Load("core").Version;}catch(Exception error){full+=" · "+error.Message;}
        edition.Items.Add(full+T(" · all built-in modules"," · tous les modules intégrés"));edition.Items.Add(core+T(" · OpenRGB / VIA only, no Full configuration"," · OpenRGB / VIA, sans configuration Full"));edition.SelectedIndex=0;card.Controls.Add(edition);
        var flash=Theme.Button(T("Begin guided flash…","Démarrer le flash guidé…"),()=>_=RunJob(HardwareOperation.Flash,edition.SelectedIndex==0?"full":"core"),true);flash.SetBounds(520,244,410,39);flash.Enabled=!preview&&!busy;card.Controls.Add(flash);
        var note=Theme.Label(T("Keep a second input device and the manufacturer recovery image available. During active programming, cancellation and closing Studio are disabled. Firmware files and the flasher are bundled and validated; arbitrary executables are never loaded.\n\nActual power-cycle persistence and hardware flashing require physical validation. Automated self-tests do not access your keyboard.","Gardez un second périphérique de saisie et l’image de récupération du fabricant disponibles. Pendant l’écriture, l’annulation et la fermeture de Studio sont désactivées. Firmware et flasher sont intégrés et vérifiés ; aucun exécutable arbitraire n’est chargé.\n\nLa persistance après coupure et le flash doivent être validés physiquement. Les autotests n’accèdent pas au clavier."),10,Theme.Muted);note.SetBounds(0,335,964,150);content.Controls.Add(note);
    }
    void ShowError(Exception error)=>MessageBox.Show(this,error.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Error);
    async Task RunJob(HardwareOperation operation,string edition="full")
    {
        if(preview||busy)return;
        if(operation==HardwareOperation.Read&&dirty&&MessageBox.Show(this,T("Replace offline edits with the keyboard’s four profiles?","Remplacer les modifications hors ligne par les quatre profils du clavier ?"),Text,MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
        if(operation==HardwareOperation.Flash&&MessageBox.Show(this,T("Start a guided firmware update? Studio will request administrator access, pause RGB clients and create a private backup. Hold Fn + Esc only when instructed. Keep the USB cable connected throughout programming.","Démarrer une mise à jour guidée ? Studio demandera les droits administrateur, suspendra le RGB et créera une sauvegarde privée. Maintenez Fn + Échap uniquement à l’invitation. Gardez le câble USB branché pendant l’écriture."),Text,MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
        try
        {
            var request=new JobRequest{Operation=operation,Edition=edition,ExpectedGeneration=generation??0};
            if(operation is HardwareOperation.Save or HardwareOperation.Preview){var payload=ConfigCodec.Encode(config);if(operation==HardwareOperation.Preview)payload[0]=(byte)selectedProfile;request.Payload=Convert.ToBase64String(payload);}
            busy=true;foreach(var button in navigation.Values)button.Enabled=false;profileTabs.Enabled=false;content.Enabled=false;UpdateStatus();status.Text=T("Starting explicit hardware job · administrator access may be requested…","Démarrage de l’opération explicite · des droits administrateur peuvent être demandés…");
            var started=HardwareJobs.Start(request);jobId=started.Id;using var process=started.Process;cancel.Visible=operation==HardwareOperation.Flash;JobResult? result=null;
            while(true){result=HardwareJobs.Poll(jobId);if(result!=null){status.Text=result.Message;cancel.Enabled=!result.Programming&&!result.Done;if(result.Done)break;}if(process.HasExited){result=HardwareJobs.Poll(jobId);if(result?.Done!=true)throw new IOException("Hardware worker exited before reporting completion. RGB restoration may need attention.");break;}await Task.Delay(200);}
            if(result==null)throw new IOException("No hardware result.");
            if(!result.Success)throw new IOException(result.Message+(result.BackupDirectory!=""?"\nPrivate backup: "+result.BackupDirectory:""));
            firmware=result.Firmware;unsupported=result.Unsupported;
            if(result.Payload!="")
            {
                var names=config.Profiles.Select(p=>(p.Name,p.GeneratedName)).ToArray();config=ConfigCodec.Decode(Convert.FromBase64String(result.Payload));for(int i=0;i<4;i++){config.Profiles[i].Name=names[i].Name;config.Profiles[i].GeneratedName=names[i].GeneratedName;}generation=result.Generation;dirty=false;
            }
            if(unsupported)generation=null;string message=result.Message;if(result.BackupDirectory!="")message+="\n"+T("Private backup: ","Sauvegarde privée : ")+result.BackupDirectory;
            MessageBox.Show(this,message,Text,MessageBoxButtons.OK,MessageBoxIcon.Information);
        }
        catch(Exception error){ShowError(error);}
        finally{busy=false;jobId=null;cancel.Visible=false;foreach(var button in navigation.Values)button.Enabled=true;profileTabs.Enabled=true;content.Enabled=true;RefreshShell();}
    }
}
internal sealed class KeyboardCanvas:Control
{
    readonly PhysicalKey[] keys=KeyboardLayout.Load();readonly Func<OnboardProfile> profile;readonly Func<bool> azerty;internal HashSet<int> Selection{get;}=new();internal Action? SelectionChanged;
    internal KeyboardCanvas(Func<OnboardProfile> profile,Func<bool> azerty){this.profile=profile;this.azerty=azerty;DoubleBuffered=true;BackColor=Theme.Panel;Cursor=Cursors.Hand;SetStyle(ControlStyles.ResizeRedraw,true);}
    float Unit=>Math.Min((Width-34)/22.5f,(Height-42)/6.25f);
    RectangleF BoundsFor(PhysicalKey key)=>new(17+key.X*Unit,18+key.Y*Unit,key.W*Unit-5,key.H*Unit-5);
    GraphicsPath Shape(PhysicalKey key)
    {
        var rectangle=BoundsFor(key);var path=new GraphicsPath();
        if(key.Led==74){float cut=Unit*.25f,mid=rectangle.Top+Unit;path.AddPolygon(new[]{new PointF(rectangle.Left+cut,rectangle.Top),new PointF(rectangle.Right,rectangle.Top),new PointF(rectangle.Right,rectangle.Bottom),new PointF(rectangle.Left,rectangle.Bottom),new PointF(rectangle.Left,mid),new PointF(rectangle.Left+cut,mid)});}
        else {float r=5;path.AddArc(rectangle.X,rectangle.Y,r*2,r*2,180,90);path.AddArc(rectangle.Right-r*2,rectangle.Y,r*2,r*2,270,90);path.AddArc(rectangle.Right-r*2,rectangle.Bottom-r*2,r*2,r*2,0,90);path.AddArc(rectangle.X,rectangle.Bottom-r*2,r*2,r*2,90,90);path.CloseFigure();}return path;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var p=profile();using var font=new Font("Segoe UI",Math.Clamp(Unit*.2f,6.5f,10),FontStyle.Regular,GraphicsUnit.Point);
        foreach(var key in keys)
        {
            var color=p.Keys[key.Led].ToColor();using var shape=Shape(key);var fill=Color.FromArgb(30+color.R*35/255,33+color.G*35/255,41+color.B*35/255);using var brush=new SolidBrush(fill);e.Graphics.FillPath(brush,shape);
            using var border=new Pen(Selection.Contains(key.Led)?Theme.Red:Theme.Border,Selection.Contains(key.Led)?2:1);e.Graphics.DrawPath(border,shape);var rectangle=BoundsFor(key);
            using var light=new Pen(color,2);e.Graphics.DrawLine(light,rectangle.X+7,rectangle.Bottom-4,rectangle.Right-7,rectangle.Bottom-4);
            string label=KeyboardLayout.Legend(key,azerty());var bounds=Rectangle.Round(rectangle);TextRenderer.DrawText(e.Graphics,label,font,bounds,Selection.Contains(key.Led)?Color.White:Theme.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
        }
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);var key=keys.FirstOrDefault(k=>{using var path=Shape(k);return path.IsVisible(e.Location);});if(key==null)return;
        if((ModifierKeys&Keys.Control)==0)Selection.Clear();if(!Selection.Add(key.Led))Selection.Remove(key.Led);SelectionChanged?.Invoke();Invalidate();
    }
    internal void SelectAll(){Selection.Clear();foreach(var key in keys)Selection.Add(key.Led);SelectionChanged?.Invoke();Invalidate();}
    internal void ClearSelection(){Selection.Clear();SelectionChanged?.Invoke();Invalidate();}
}
