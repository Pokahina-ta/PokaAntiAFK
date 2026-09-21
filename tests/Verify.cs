using System;
using System.Drawing;
using System.Reflection;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Text;
using PokaAntiAFK_WinForms;
class Target : Form {
    public int Downs, Ups;
    public long UpFlags;
    protected override void WndProc(ref Message m) {
        if (m.Msg==0x100) Downs++;
        if (m.Msg==0x101) { Ups++; UpFlags=m.LParam.ToInt64(); }
        base.WndProc(ref m);
    }
}
class Verify {
    static object Field(Form1 f,string name) {return typeof(Form1).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);}
    static void Call(Form1 f,string name) { typeof(Form1).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,new object[]{null,EventArgs.Empty}); }
    static void Assert(bool ok,string label) {if(!ok)throw new Exception(label); Console.WriteLine("PASS: "+label);}
    static void Pump() { for(int i=0;i<8;i++) Application.DoEvents(); }
    static void Packet(UdpClient receiver,int value) {
        IPEndPoint from=null;
        byte[] data=receiver.Receive(ref from);
        Assert(data.Length==28 && Encoding.ASCII.GetString(data,0,18)=="/input/MoveForward"
            && data[18]==0 && data[19]==0
            && data[20]==44 && data[21]==105 && data[22]==0 && data[23]==0
            && data[24]==0 && data[25]==0 && data[26]==0 && data[27]==value,
            "OSC address, padding, int32 value = "+value);
    }
    static void OscChecks(string previewPath) {
        using(var receiver=new UdpClient(new IPEndPoint(IPAddress.Loopback,0)))
        using(var f=new Form1()) {
            receiver.Client.ReceiveTimeout=2000;
            ((ComboBox)Field(f,"transportBox")).SelectedIndex=1;
            f.ShowInTaskbar=false; f.Opacity=0; f.Show(); Pump();
            ((ComboBox)Field(f,"windowComboBox")).Items.Clear();
            using(var bmp=new Bitmap(f.Width,f.Height)) {f.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(previewPath);}
            ((NumericUpDown)Field(f,"oscPort")).Value=((IPEndPoint)receiver.Client.LocalEndPoint).Port;
            Assert(!((ComboBox)Field(f,"windowComboBox")).Enabled,"OSC disables window selection");
            Assert(!((CheckBox)Field(f,"holdTimeBox")).Enabled,"OSC disables long hold");
            Call(f,"TestOsc_Click");
            Packet(receiver,0); Packet(receiver,1);
            Assert(!((NumericUpDown)Field(f,"oscPort")).Enabled,"Port locked during pulse");
            var clock=System.Diagnostics.Stopwatch.StartNew();
            while((bool)Field(f,"isRunning") && clock.ElapsedMilliseconds<2000) {Pump();System.Threading.Thread.Sleep(10);}
            Packet(receiver,0);
            Assert(!(bool)Field(f,"isRunning") && receiver.Available==0,"Single test releases and does not repeat");
            Call(f,"ToggleButton_Click"); Call(f,"Timer_Tick");
            Packet(receiver,0); Packet(receiver,1);
            Call(f,"ToggleButton_Click"); Packet(receiver,0);
            Assert(!((bool)Field(f,"oscHeld")),"Stop clears OSC held state");
            Call(f,"ToggleButton_Click"); Call(f,"Timer_Tick");
            Packet(receiver,0); Packet(receiver,1);
            f.Close(); Packet(receiver,0);
            Assert(receiver.Available==0,"Close releases OSC without further input");
        }
    }
    static void Select(Form1 f,Target target) {
        var combo=(ComboBox)Field(f,"windowComboBox");
        var windows=(List<Tuple<IntPtr,string>>)Field(f,"windowHandles");
        combo.Items.Clear(); windows.Clear();
        windows.Add(Tuple.Create(target.Handle,"Poka verification target"));
        combo.Items.Add("Poka verification target");combo.SelectedIndex=0;
    }
    [STAThread] static int Main(string[] args) {
        try { return Run(args); }
        catch(Exception ex) { while(ex.InnerException!=null) ex=ex.InnerException; Console.WriteLine(ex.GetType().Name); Console.WriteLine(ex.Message); return 1; }
    }
    static int Run(string[] args) {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using(var f=new Form1()) using(var target=new Target()) {
            f.ShowInTaskbar=false; f.Opacity=0; f.Show(); Pump();
            var h=f.Handle;
            // Render only this application's controls; no desktop capture.
            var combo=(ComboBox)Field(f,"windowComboBox");combo.Items.Clear();combo.Items.Add("送信先のウィンドウを選択してください");combo.SelectedIndex=0;
            using(var bmp=new Bitmap(f.Width,f.Height)) {f.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(args[0]);}
            combo.Items.Clear();
            Call(f,"ToggleButton_Click");
            Assert(!(bool)Field(f,"isRunning"),"No target cannot start");
            Select(f,target);
            ((CheckBox)Field(f,"holdTimeBox")).Checked=true;
            Call(f,"ToggleButton_Click");
            Assert(!combo.Enabled,"Target locked while running");
            var clock=System.Diagnostics.Stopwatch.StartNew();
            Call(f,"Timer_Tick");Pump();
            Assert(clock.ElapsedMilliseconds<1000,"Long hold does not block UI");
            Assert(target.Downs==1 && target.Ups==0,"Key down reaches only test target");
            Call(f,"ToggleButton_Click");Pump();
            Assert(target.Ups==1,"Stop releases held key immediately");
            Assert((target.UpFlags & 0xC0000000L)==0xC0000000L,"Key up flags correct");
            Assert(combo.Enabled && !(bool)Field(f,"isRunning"),"Stop restores controls");
            Call(f,"ToggleButton_Click");Call(f,"Timer_Tick");Pump();
            target.Dispose();Call(f,"Timer_Tick");
            Assert(!(bool)Field(f,"isRunning"),"Closed target stops run");
        }
        using(var f=new Form1()) using(var target=new Target()) {
            var h=f.Handle; Select(f,target);
            Call(f,"ToggleButton_Click");Call(f,"Timer_Tick");Pump();
            f.Close();Pump();
            Assert(target.Ups==1,"Closing app releases held key");
        }
        OscChecks(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(args[0]),"preview-osc.png"));
        return 0;
    }
}
