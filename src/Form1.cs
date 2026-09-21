

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace PokaAntiAFK_WinForms
{
    public partial class Form1 : Form
    {
        private ComboBox windowComboBox, modeBox, intervalBox;
        private CheckBox rand5Box, rand10Box, rand15Box, shiftBox, ctrlBox, spaceBox, holdTimeBox;
        private Button toggleButton, refreshButton;
        private Label statusLabel, titleLabel;
        private System.Windows.Forms.Panel cardPanel;
        private ComboBox transportBox;
        private NumericUpDown oscPort;
        private Button testButton;
        private Label oscHint;
        private OscInput osc;
        private bool oscHeld, singleShot, sessionOsc;
        private bool IsOsc => transportBox.SelectedIndex == 1;

        private System.Windows.Forms.Timer timer;
        private System.Windows.Forms.Timer releaseTimer;
        private IntPtr activeTarget;
        private uint activeProcess;
        private byte? heldKey;
        private Random rand = new Random();
        private bool isRunning = false;
        private List<Tuple<IntPtr, string>> windowHandles = new List<Tuple<IntPtr, string>>();

        // WinAPI
        [DllImport("user32.dll")]
        static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool IsWindow(IntPtr handle);
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
        [DllImport("user32.dll")]
        static extern uint MapVirtualKey(uint code, uint mapType);

        const uint WM_KEYDOWN = 0x0100;
        const uint WM_KEYUP = 0x0101;

        private readonly Color accent = Color.FromArgb(238, 198, 105);
        private Label permissionLabel;

        private Label Caption(string text, int x, int y, int width, int size, Color color)
        {
            var label = new Label { Text = text, Location = new Point(x,y), Size = new Size(width,32),
                Font = new Font("Yu Gothic UI",size,FontStyle.Regular), ForeColor = color };
            cardPanel.Controls.Add(label);
            return label;
        }
        private ComboBox Choice(int x, int y, int width, string[] items)
        {
            var box = new ComboBox { Location = new Point(x,y), Width = width,
                DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
                DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 28,
                BackColor = Color.FromArgb(40,43,51), ForeColor = Color.White,
                Font = new Font("Yu Gothic UI",11) };
            box.DrawItem += (s,e) => {
                using (var fill = new SolidBrush((e.State & DrawItemState.Selected) != 0
                    ? Color.FromArgb(65,61,48) : box.BackColor)) e.Graphics.FillRectangle(fill,e.Bounds);
                if(e.Index>=0) TextRenderer.DrawText(e.Graphics,box.Items[e.Index].ToString(),box.Font,
                    e.Bounds,box.Enabled ? Color.White : Color.Gray,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
                e.DrawFocusRectangle();
            };
            if (items != null) { box.Items.AddRange(items); box.SelectedIndex=0; }
            cardPanel.Controls.Add(box); return box;
        }
        private CheckBox Check(string text,int x,int y,int width)
        {
            var box = new CheckBox { Text=text, Location=new Point(x,y), Size=new Size(width,30),
                ForeColor=Color.FromArgb(205,210,220), Font=new Font("Yu Gothic UI",10) };
            cardPanel.Controls.Add(box); return box;
        }
        private Button ActionButton(string text,int x,int y,int width)
        {
            var button = new Button { Text=text, Location=new Point(x,y), Size=new Size(width,40),
                FlatStyle=FlatStyle.Flat, BackColor=accent, ForeColor=Color.FromArgb(24,25,30),
                Font=new Font("Yu Gothic UI",11,FontStyle.Bold), Cursor=Cursors.Hand };
            button.FlatAppearance.BorderSize=0;
            cardPanel.Controls.Add(button); return button;
        }
        public Form1()
        {
            InitializeComponent();
            Text="Poka AntiAFK • 2.2 OSC Preview";
            using (var iconStream = typeof(Form1).Assembly.GetManifestResourceStream("PokaAntiAFK.AppIcon"))
            using (var appIcon = new Icon(iconStream))
                Icon = (Icon)appIcon.Clone();
            Font=new Font("Yu Gothic UI",10);
            BackColor=Color.FromArgb(18,20,25);
            ClientSize=new Size(820,790);
            FormBorderStyle=FormBorderStyle.FixedSingle;
            MaximizeBox=false;
            StartPosition=FormStartPosition.CenterScreen;
            cardPanel=new System.Windows.Forms.Panel { Dock=DockStyle.Fill, BackColor=BackColor };
            Controls.Add(cardPanel);
            Caption("POKA  /  DESKTOP UTILITY",32,24,600,10,accent);
            titleLabel=Caption("AntiAFK",30,60,600,32,Color.White);
            titleLabel.Height=64;
            transportBox=Choice(34,132,340,new[]{"ウィンドウ入力（従来方式）","VRChat（OSC）"});
            Caption("OSC ポート",400,134,120,10,Color.Silver);
            oscPort=new NumericUpDown { Minimum=1, Maximum=65535, Value=9000,
                Location=new Point(522,134), Width=112, Font=new Font("Yu Gothic UI",11) };
            cardPanel.Controls.Add(oscPort);
            Caption("01   対象ウィンドウ",32,186,600,11,accent);
            windowComboBox=Choice(34,226,620,null);
            windowComboBox.DropDownWidth=720;
            refreshButton=ActionButton("更新",668,222,112);
            refreshButton.Click+=(s,e)=>RefreshWindowList();
            Caption("02   入力設定",32,284,600,11,accent);
            Caption("キーセット",34,324,160,10,Color.Silver);
            Caption("送信間隔（秒）",278,324,220,10,Color.Silver);
            modeBox=Choice(34,360,214,new[]{"WASD","ESDF"});
            intervalBox=Choice(278,360,214,new[]{"1","5","10","15","30","60"});
            intervalBox.SelectedIndex=3;
            holdTimeBox=Check("長押し 3〜5秒",540,360,235);
            Caption("間隔のゆらぎ",34,410,170,10,Color.Silver);
            rand5Box=Check("±5秒",206,407,110);
            rand10Box=Check("±10秒",330,407,110);
            rand15Box=Check("±15秒",454,407,110);
            rand5Box.Click+=RandomizeBox_Changed;
            rand10Box.Click+=RandomizeBox_Changed;
            rand15Box.Click+=RandomizeBox_Changed;
            intervalBox.SelectedIndexChanged+=IntervalBox_SelectedIndexChanged;
            Caption("追加のランダムキー",34,450,180,10,Color.Silver);
            shiftBox=Check("Shift",230,447,110);
            ctrlBox=Check("Ctrl",354,447,110);
            spaceBox=Check("Space",478,447,110);
            Caption("追加キーも単独で送信します。ショートカットの同時押しではありません。",34,488,746,9,Color.Gray);
            permissionLabel=Caption("通常権限で動作 • 自動昇格なし",34,535,746,10,Color.Silver);
            using (var identity=WindowsIdentity.GetCurrent())
                if (new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
                { permissionLabel.Text="管理者権限で起動されています • 通常起動を推奨"; permissionLabel.ForeColor=accent; }
            toggleButton=ActionButton("開始する",34,589,214);
            toggleButton.Height=54;
            toggleButton.Click+=ToggleButton_Click;
            statusLabel=Caption("停止中",278,588,502,12,Color.White);
            statusLabel.Height=64;
            Caption("この画面で Esc を押すと停止  /  対象アプリによっては入力を受け付けません",34,670,752,9,Color.Gray);
            testButton=ActionButton("前進を0.25秒テスト",34,721,250);
            testButton.Click+=TestOsc_Click;
            oscHint=Caption("",304,711,474,10,Color.Silver);
            oscHint.Height=70;
            transportBox.SelectedIndexChanged+=(s,e)=>UpdateTransportOptions();
            timer=new System.Windows.Forms.Timer(components);
            timer.Tick+=Timer_Tick;
            releaseTimer=new System.Windows.Forms.Timer(components);
            releaseTimer.Tick+=(s,e)=> {
                releaseTimer.Stop();
                if (!ReleaseKey()) { StopRunning("キー解放に失敗しました"); return; }
                if (singleShot) { StopRunning("テスト送信完了 • VRCで動作を確認"); return; }
                if (isRunning) { timer.Interval=NextInterval(); timer.Start(); }
            };
            KeyPreview=true;
            KeyDown+=(s,e)=> { if(e.KeyCode==Keys.Escape) StopRunning("停止中"); };
            FormClosing+=(s,e)=>StopRunning("停止中");
            RefreshWindowList();
            UpdateRandomizeCheckBoxes();
            UpdateTransportOptions();
        }
        private void UpdateTransportOptions()
        {
            bool idle=!isRunning;
            transportBox.Enabled=idle;
            windowComboBox.Enabled=refreshButton.Enabled=idle && !IsOsc;
            modeBox.Enabled=shiftBox.Enabled=ctrlBox.Enabled=spaceBox.Enabled=holdTimeBox.Enabled=idle && !IsOsc;
            oscPort.Enabled=testButton.Enabled=idle && IsOsc;
            oscHint.Text=IsOsc ? "VRCで OSC → Enabled を有効にしてください。\n前進0.25秒／AFK判定の抑止は未確認" : "";
        }
        private bool PrepareSession(bool test)
        {
            sessionOsc=IsOsc;
            singleShot=test;
            if (sessionOsc)
            {
                osc?.Dispose();
                osc=null;
                try { osc=new OscInput((int)oscPort.Value); }
                catch (System.Net.Sockets.SocketException ex)
                { statusLabel.Text="OSC初期化失敗 • " + ex.SocketErrorCode; return false; }
                return true;
            }
            activeTarget=GetSelectedWindowHandle();
            GetWindowThreadProcessId(activeTarget,out activeProcess);
            if (!TargetExists()) { statusLabel.Text="対象を選び直してください"; return false; }
            return true;
        }
        private void TestOsc_Click(object sender, EventArgs e)
        {
            if (isRunning || !IsOsc || oscHeld) return;
            if (!PrepareSession(true)) return;
            isRunning=true;
            SetOptionsEnabled(false);
            toggleButton.Text="停止する";
            statusLabel.Text="テスト送信中 • 前進0.25秒";
            Timer_Tick(sender,e);
        }
        private void RefreshWindowList()
        {
            IntPtr previous = GetSelectedWindowHandle();
            windowComboBox.Items.Clear();
            windowHandles.Clear();
            foreach (var win in GetWindowList())
            {
                windowHandles.Add(win);
                windowComboBox.Items.Add(win.Item2);
            }
            if (windowComboBox.Items.Count > 0)
                windowComboBox.SelectedIndex = Math.Max(0, windowHandles.FindIndex(w => w.Item1 == previous));
        }
        private List<Tuple<IntPtr, string>> GetWindowList()
        {
            var windows = new List<Tuple<IntPtr, string>>();
            EnumWindows((hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd) && hWnd != Handle)
                {
                    StringBuilder sb = new StringBuilder(256);
                    GetWindowText(hWnd, sb, sb.Capacity);
                    string title = sb.ToString();
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        uint pid;
                        GetWindowThreadProcessId(hWnd, out pid);
                        windows.Add(Tuple.Create(hWnd, title + "  [PID " + pid + "]"));
                    }
                }
                return true;
            }, IntPtr.Zero);
            return windows;
        }
        private IntPtr GetSelectedWindowHandle()
        {
            int idx = windowComboBox.SelectedIndex;
            if (idx >= 0 && idx < windowHandles.Count)
                return windowHandles[idx].Item1;
            return IntPtr.Zero;
        }
        private void IntervalBox_SelectedIndexChanged(object sender, EventArgs e) => UpdateRandomizeCheckBoxes();
        private void UpdateRandomizeCheckBoxes()
        {
            string selected = intervalBox.SelectedItem?.ToString();
            bool enableRand = (selected == "15" || selected == "30" || selected == "60");
            rand5Box.Enabled = enableRand;
            rand10Box.Enabled = enableRand;
            rand15Box.Enabled = enableRand;
            if (!enableRand)
            {
                rand5Box.Checked = false;
                rand10Box.Checked = false;
                rand15Box.Checked = false;
            }
        }
        private void RandomizeBox_Changed(object sender, EventArgs e)
        {
            var box = sender as CheckBox;
            if (box.Checked)
            {
                if (box != rand5Box) rand5Box.Checked = false;
                if (box != rand10Box) rand10Box.Checked = false;
                if (box != rand15Box) rand15Box.Checked = false;
            }
        }
        private void ToggleButton_Click(object sender, EventArgs e)
        {
            if (isRunning) { StopRunning("停止中"); return; }
            if (oscHeld) return;
            if (!PrepareSession(false)) return;
            isRunning = true;
            SetOptionsEnabled(false);
            toggleButton.Text = "停止する";
            statusLabel.Text = sessionOsc ? "OSC定期送信中 • 受信は未確認" : "実行中 • 対象に定期送信";
            timer.Interval = NextInterval();
            timer.Start();
        }

        private bool TargetExists()
        {
            uint pid;
            return activeTarget != IntPtr.Zero && IsWindow(activeTarget)
                && GetWindowThreadProcessId(activeTarget, out pid) != 0 && pid == activeProcess;
        }

        private void SetOptionsEnabled(bool enabled)
        {
            foreach (Control control in cardPanel.Controls)
                if (control != toggleButton && !(control is Label)) control.Enabled = enabled;
            if (enabled) UpdateRandomizeCheckBoxes();
            UpdateTransportOptions();
        }

        private bool SendKey(byte key, bool up)
        {
            uint bits = 1 | (MapVirtualKey(key, 0) << 16);
            if (up) bits |= 0xC0000000;
            return PostMessage(activeTarget, up ? WM_KEYUP : WM_KEYDOWN,
                (IntPtr)key, new IntPtr(unchecked((int)bits)));
        }

        private bool ReleaseKey()
        {
            if (oscHeld)
            {
                try { osc.Forward(false); oscHeld=false; }
                catch (System.Net.Sockets.SocketException) { return false; }
            }
            if (!heldKey.HasValue) return true;
            if (!TargetExists()) { heldKey = null; return true; }
            if (!SendKey(heldKey.Value, true)) return false;
            heldKey = null;
            return true;
        }

        private void StopRunning(string status)
        {
            isRunning = false;
            timer.Stop();
            releaseTimer.Stop();
            bool released = ReleaseKey();
            singleShot=false;
            toggleButton.Text = "開始する";
            SetOptionsEnabled(true);
            // Keep the target and key available for another release attempt on close.
            toggleButton.Enabled = released;
            if (!released) { transportBox.Enabled=false; testButton.Enabled=false; }
            statusLabel.Text = released ? status : "キー解放に失敗 • 対象アプリを確認してください";
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            timer.Stop();
            if (!isRunning) return;
            if (sessionOsc)
            {
                try
                {
                    // Clear any previous state before each new button press.
                    osc.Forward(false);
                    oscHeld=true;
                    osc.Forward(true);
                    releaseTimer.Interval=250;
                    releaseTimer.Start();
                }
                catch (System.Net.Sockets.SocketException ex)
                { StopRunning("OSC送信失敗 • " + ex.SocketErrorCode); }
                return;
            }
            if (!TargetExists()) { StopRunning("対象が閉じられたため停止しました"); return; }
            string[] keys = modeBox.SelectedItem?.ToString() == "WASD"
                ? new[] { "W", "A", "S", "D" } : new[] { "E", "S", "D", "F" };
            var keyList = new List<string>(keys);
            if (shiftBox.Checked) keyList.Add("SHIFT");
            if (ctrlBox.Checked) keyList.Add("CTRL");
            if (spaceBox.Checked) keyList.Add("SPACE");
            byte vk = GetKeyCode(keyList[rand.Next(keyList.Count)]);
            if (!SendKey(vk, false))
            {
                int error = Marshal.GetLastWin32Error();
                StopRunning(error == 5 ? "権限の違いにより送信できません。停止しました" : "送信失敗 • エラー " + error);
                return;
            }
            heldKey = vk;
            releaseTimer.Interval = holdTimeBox.Checked ? rand.Next(3000, 5001) : 100;
            releaseTimer.Start();
        }

        private int NextInterval()
        {
            int range = rand5Box.Checked ? 5 : rand10Box.Checked ? 10 : rand15Box.Checked ? 15 : 0;
            return Math.Max(1000, GetInterval() + (range == 0 ? 0 : rand.Next(-range * 1000, range * 1000 + 1)));
        }
        private int GetInterval()
        {
            string selected = intervalBox.SelectedItem?.ToString();
            return selected switch
            {
                "1" => 1000,
                "5" => 5000,
                "10" => 10000,
                "15" => 15000,
                "30" => 30000,
                "60" => 60000,
                _ => 15000
            };
        }
        private byte GetKeyCode(string key)
        {
            return key switch
            {
                "A" => (byte)Keys.A,
                "W" => (byte)Keys.W,
                "S" => (byte)Keys.S,
                "D" => (byte)Keys.D,
                "E" => (byte)Keys.E,
                "F" => (byte)Keys.F,
                "SHIFT" => (byte)Keys.ShiftKey,
                "CTRL" => (byte)Keys.ControlKey,
                "SPACE" => (byte)Keys.Space,
                _ => (byte)Keys.A
            };
        }
    }
}
