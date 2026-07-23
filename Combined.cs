using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace WheelApp
{
    public class WheelData
    {
        public string[] Prizes;
        public WheelData[] Children;
        public Color[] Colors;
        public float[] Weights;

        public int SectorCount { get { return Prizes.Length; } }

        public WheelData(string[] prizes, Color[] colors)
        {
            Prizes = prizes;
            Colors = colors;
            Children = new WheelData[prizes.Length];
            Weights = new float[prizes.Length];
            for (int i = 0; i < Weights.Length; i++)
                Weights[i] = 1f;
        }

        public float WeightTotal
        {
            get { float s = 0; foreach (float w in Weights) s += w; return s; }
        }

        public bool HasWeightCustomized
        {
            get
            {
                for (int i = 1; i < Weights.Length; i++)
                    if (Math.Abs(Weights[i] - Weights[0]) > 0.001f) return true;
                return false;
            }
        }

        public void ResetWeights()
        {
            for (int i = 0; i < Weights.Length; i++)
                Weights[i] = 1f;
        }

        public void SetWeight(int idx, float newWeight)
        {
            float old = Weights[idx];
            Weights[idx] = newWeight;
            float remaining = 100f - newWeight;
            int customized = 0, plain = 0;
            float customTotal = 0;
            for (int i = 0; i < Weights.Length; i++)
            {
                if (i == idx) continue;
                if (Math.Abs(Weights[i] - 1f) > 0.001f) { customized++; customTotal += Weights[i]; }
                else plain++;
            }
            if (customized == 0 && plain == 0) return;
            if (customized > 0)
            {
                float remainingAfterCustom = remaining;
                for (int i = 0; i < Weights.Length; i++)
                {
                    if (i == idx) continue;
                    if (Math.Abs(Weights[i] - 1f) > 0.001f)
                    {
                        float ratio = Weights[i] / customTotal;
                        Weights[i] = remaining * ratio;
                        remainingAfterCustom -= Weights[i];
                    }
                }
                if (plain > 0)
                {
                    float each = remainingAfterCustom / plain;
                    for (int i = 0; i < Weights.Length; i++)
                    {
                        if (i == idx) continue;
                        if (Math.Abs(Weights[i] - 1f) < 0.001f)
                            Weights[i] = each;
                    }
                }
            }
            else
            {
                float each = remaining / plain;
                for (int i = 0; i < Weights.Length; i++)
                {
                    if (i == idx) continue;
                    Weights[i] = each;
                }
            }
        }

        public bool HasChild(int idx)
        {
            return idx >= 0 && idx < Children.Length && Children[idx] != null;
        }

        public WheelData DeepClone()
        {
            string[] p = (string[])Prizes.Clone();
            Color[] c = (Color[])Colors.Clone();
            WheelData w = new WheelData(p, c);
            w.Weights = (float[])Weights.Clone();
            for (int i = 0; i < Children.Length; i++)
            {
                if (Children[i] != null)
                    w.Children[i] = Children[i].DeepClone();
            }
            return w;
        }

        public string Serialize()
        {
            StringBuilder sb = new StringBuilder();
            SerializeInto(sb, "");
            return sb.ToString();
        }

        private void SerializeInto(StringBuilder sb, string prefix)
        {
            sb.Append(prefix).Append("P:").Append(string.Join("|", Prizes)).AppendLine();
            if (HasWeightCustomized)
            {
                sb.Append(prefix).Append("W:").Append(string.Join(",", Weights)).AppendLine();
            }
            for (int i = 0; i < Children.Length; i++)
            {
                if (Children[i] != null)
                    Children[i].SerializeInto(sb, prefix + i.ToString() + ".");
            }
        }

        public static WheelData Deserialize(string text)
        {
            string[] lines = text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            WheelData root = null;

            // first pass: find root prizes
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("P:"))
                {
                    string[] prizes = lines[i].Substring(2).Split('|');
                    if (prizes.Length >= 1)
                    {
                        root = new WheelData(prizes, GenerateColorsStatic(prizes.Length));
                    }
                    break;
                }
            }
            if (root == null) return null;

            // second pass: populate children and weights
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int colon = line.IndexOf(':');
                if (colon < 0) continue;
                string prefix = line.Substring(0, colon);
                string content = line.Substring(colon + 1);

                // root-level weights
                if (prefix == "W" && root != null)
                {
                    string[] parts = content.Split(',');
                    if (parts.Length == root.Weights.Length)
                    {
                        for (int wi = 0; wi < parts.Length; wi++)
                        {
                            float v;
                            if (float.TryParse(parts[wi], out v)) root.Weights[wi] = v;
                        }
                    }
                    continue;
                }

                // only handle lines with dots
                if (!prefix.Contains(".")) continue;

                string[] segs = prefix.Split('.');
                string last = segs[segs.Length - 1];

                if (last == "P")
                {
                    WheelData target = root;
                    bool ok = true;
                    for (int pi = 0; pi < segs.Length - 2; pi++)
                    {
                        int seg = int.Parse(segs[pi]);
                        if (seg >= 0 && seg < target.Children.Length && target.Children[seg] != null)
                            target = target.Children[seg];
                        else
                        { ok = false; break; }
                    }
                    if (!ok) continue;

                    int childIdx = int.Parse(segs[segs.Length - 2]);
                    if (childIdx < target.Children.Length)
                    {
                        string[] childPrizes = content.Split('|');
                        if (childPrizes.Length >= 1)
                            target.Children[childIdx] = new WheelData(childPrizes, GenerateColorsStatic(childPrizes.Length));
                    }
                }
                else if (last == "W")
                {
                    WheelData target = root;
                    bool ok = true;
                    for (int pi = 0; pi < segs.Length - 1; pi++)
                    {
                        int seg = int.Parse(segs[pi]);
                        if (seg >= 0 && seg < target.Children.Length && target.Children[seg] != null)
                            target = target.Children[seg];
                        else
                        { ok = false; break; }
                    }
                    if (!ok) continue;

                    string[] parts = content.Split(',');
                    if (parts.Length == target.Weights.Length)
                    {
                        for (int wi = 0; wi < parts.Length; wi++)
                        {
                            float v;
                            if (float.TryParse(parts[wi], out v)) target.Weights[wi] = v;
                        }
                    }
                }
            }

            return root;
        }

        private static Color[] GenerateColorsStatic(int count)
        {
            Color[] result = new Color[count];
            for (int i = 0; i < count; i++)
            {
                int hue = i * 360 / count;
                result[i] = ColorFromHsv(hue, 180, 220);
            }
            return result;
        }

        private static Color ColorFromHsv(int hue, int saturation, int value)
        {
            double h = hue / 60.0;
            double s = saturation / 255.0;
            double v = value / 255.0;
            int i = (int)Math.Floor(h) % 6;
            double f = h - Math.Floor(h);
            double p = v * (1 - s);
            double q = v * (1 - f * s);
            double t = v * (1 - (1 - f) * s);
            double r = 0, g = 0, b = 0;
            switch (i)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                case 5: r = v; g = p; b = q; break;
            }
            return Color.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
        }
    }

    public class WheelForm : Form
    {
        private WheelData data;
        private WheelData originalData;
        private float currentAngle;
        private float targetAngle;
        private float pointerAngle;
        private float targetPointerAngle;
        private int spinMode = 1;
        private bool isSpinning;
        private bool isResetting;
        private float resetTarget;
        private Timer spinTimer;
        private Label goLabel;
        private static Random rand = new Random();
        private ListBox resultList;
private bool musicOn;
        private Button musicBtn;
        private Button langBtn;
        private string customMidiPath;
        private const string DefaultMidiFile = "defaultBGM.mid";
        internal static bool english;
        internal static bool savedEnglish;
        internal static Dictionary<string, string> en = new Dictionary<string, string>();
        internal static Dictionary<string, string> zh = new Dictionary<string, string>();
        internal static Dictionary<string, string> enToZh = new Dictionary<string, string>();
        private string _titleKey;

        public bool StartSpinOnLoad { get; set; }
        public string LastResult { get; private set; }

        private static void WriteVarLen(List<byte> dest, int value)
        {
            if (value < 0x80) dest.Add((byte)value);
            else if (value < 0x4000) { dest.Add((byte)(0x80 | (value >> 7))); dest.Add((byte)(value & 0x7F)); }
            else if (value < 0x200000) { dest.Add((byte)(0x80 | (value >> 14))); dest.Add((byte)(0x80 | ((value >> 7) & 0x7F))); dest.Add((byte)(value & 0x7F)); }
            else if (value < 0x10000000) { dest.Add((byte)(0x80 | (value >> 21))); dest.Add((byte)(0x80 | ((value >> 14) & 0x7F))); dest.Add((byte)(0x80 | ((value >> 7) & 0x7F))); dest.Add((byte)(value & 0x7F)); }
            else { dest.Add((byte)(0x80 | (value >> 28))); dest.Add((byte)(0x80 | ((value >> 21) & 0x7F))); dest.Add((byte)(0x80 | ((value >> 14) & 0x7F))); dest.Add((byte)(0x80 | ((value >> 7) & 0x7F))); dest.Add((byte)(value & 0x7F)); }
        }

        private static void AddMidiEv(List<byte> track, ref int lastTick, int newTick, byte[] data)
        {
            int delta = newTick - lastTick;
            if (delta < 0) delta = 0;
            WriteVarLen(track, delta);
            track.AddRange(data);
            lastTick = newTick;
        }

        private static byte[] GenerateDefaultMidi()
        {
            List<byte> track = new List<byte>();
            int t = 0;

            AddMidiEv(track, ref t, 0, new byte[] { 0xFF, 0x51, 0x03, 0x07, 0xA1, 0x20 });
            AddMidiEv(track, ref t, 0, new byte[] { 0xC0, 0 });

            int[] melody = new int[] {
                72,76,79,84, 79,76,72,67, 72,76,79,86, 79,76,72,67,
                60,64,67,72, 76,72,79,84, 72,76,79,84, 79,76,72,67,
                60,62,64,65, 67,71,72,76, 79,84,79,76, 72,67,64,60,
            };
            int e = 240;

            for (int i = 0; i < melody.Length; i++)
            {
                int note = melody[i];
                int d = (i % 4 == 3) ? e * 2 : e;
                AddMidiEv(track, ref t, t, new byte[] { 0x90, (byte)note, 90 });
                AddMidiEv(track, ref t, t + d, new byte[] { 0x80, (byte)note, 0 });
            }

            byte[] arr = track.ToArray();
            byte[] result = new byte[22 + arr.Length + 4];
            Buffer.BlockCopy(Encoding.ASCII.GetBytes("MThd"), 0, result, 0, 4);
            var b = BitConverter.GetBytes(6); Array.Reverse(b); Buffer.BlockCopy(b, 0, result, 4, 4);
            b = BitConverter.GetBytes((short)0); Array.Reverse(b); Buffer.BlockCopy(b, 0, result, 8, 2);
            b = BitConverter.GetBytes((short)1); Array.Reverse(b); Buffer.BlockCopy(b, 0, result, 10, 2);
            b = BitConverter.GetBytes((short)480); Array.Reverse(b); Buffer.BlockCopy(b, 0, result, 12, 2);
            Buffer.BlockCopy(Encoding.ASCII.GetBytes("MTrk"), 0, result, 14, 4);
            b = BitConverter.GetBytes(arr.Length + 4); Array.Reverse(b); Buffer.BlockCopy(b, 0, result, 18, 4);
            Buffer.BlockCopy(arr, 0, result, 22, arr.Length);
            result[22 + arr.Length] = 0x00; result[23 + arr.Length] = 0xFF; result[24 + arr.Length] = 0x2F; result[25 + arr.Length] = 0x00;
            return result;
        }

        [DllImport("winmm.dll")]
        private static extern int mciSendString(string command, StringBuilder ret, int retLen, IntPtr callback);

        private float cx, cy, r;
        private const float wheelCenterX = 250f;
        private ContextMenuStrip contextMenu;
        private ToolStripMenuItem newSectorItem;
        private ToolStripMenuItem modifyItem;
        private ToolStripMenuItem childWheelItem;
        private ToolStripMenuItem mode1Item;
        private ToolStripMenuItem mode2Item;
        private int rightClickSector;
        private Label modeLabel;
        private string parentResult = null;
        private ToolStripMenuItem deleteItem;
        private ToolStripMenuItem clearChildItem;
        private ToolStripMenuItem weightItem;

        private const float startOffset = 90f;

        public WheelForm(WheelData data, string title)
        {
            this.data = data;
            this.originalData = data.DeepClone();
            this.Text = title;
            this.Size = new Size(720, 680);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.BackColor = Color.FromArgb(0xF0, 0xF0, 0xF5);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.DoubleBuffered = true;
            this.pointerAngle = startOffset;
            try { this.Icon = new Icon(Path.Combine(Application.StartupPath, "wheel.ico")); } catch { }
            _titleKey = title;

            Label titleLabel = new Label();
            titleLabel.Text = title;
            titleLabel.Tag = title;
            titleLabel.Font = new Font("Microsoft YaHei", 14, FontStyle.Bold);
            titleLabel.ForeColor = Color.FromArgb(0x2C, 0x3E, 0x50);
            titleLabel.TextAlign = ContentAlignment.MiddleCenter;
            titleLabel.Size = new Size(460, 28);
            titleLabel.Location = new Point(10, 8);
            this.Controls.Add(titleLabel);

            modeLabel = new Label();
            modeLabel.Font = new Font("Microsoft YaHei", 9, FontStyle.Regular);
            modeLabel.ForeColor = Color.FromArgb(0x7F, 0x8C, 0x8D);
            modeLabel.TextAlign = ContentAlignment.MiddleCenter;
            modeLabel.Size = new Size(200, 18);
            modeLabel.Location = new Point(140, 36);
            this.Controls.Add(modeLabel);

            resultList = new ListBox();
            resultList.Location = new Point(490, 40);
            resultList.Size = new Size(200, 540);
            resultList.Font = new Font("Microsoft YaHei", 9);
            resultList.HorizontalScrollbar = true;
            resultList.BackColor = Color.FromArgb(0xFF, 0xFF, 0xFF);
            resultList.BorderStyle = BorderStyle.FixedSingle;
            resultList.MouseUp += new MouseEventHandler(ResultList_MouseUp);
            this.Controls.Add(resultList);

            Button clearAllBtn = new Button();
            clearAllBtn.Text = "清除全部记录";
            clearAllBtn.Tag = clearAllBtn.Text;
            clearAllBtn.Size = new Size(200, 28);
            clearAllBtn.Location = new Point(490, 584);
            clearAllBtn.Font = new Font("Microsoft YaHei", 9);
            clearAllBtn.FlatStyle = FlatStyle.Flat;
            clearAllBtn.FlatAppearance.BorderColor = Color.FromArgb(0xBD, 0xC3, 0xC7);
            clearAllBtn.BackColor = Color.FromArgb(0xEC, 0xF0, 0xF1);
            clearAllBtn.Click += new EventHandler(ClearAllResults_Click);
            this.Controls.Add(clearAllBtn);

            Label resultTitle = new Label();
            resultTitle.Text = "抽奖记录";
            resultTitle.Tag = resultTitle.Text;
            resultTitle.Font = new Font("Microsoft YaHei", 11, FontStyle.Bold);
            resultTitle.ForeColor = Color.FromArgb(0x2C, 0x3E, 0x50);
            resultTitle.TextAlign = ContentAlignment.MiddleLeft;
            resultTitle.Size = new Size(200, 22);
            resultTitle.Location = new Point(490, 14);
            this.Controls.Add(resultTitle);

            goLabel = new Label();
            goLabel.Text = "GO";
            goLabel.Font = new Font("Arial", 22, FontStyle.Bold);
            goLabel.ForeColor = Color.White;
            goLabel.BackColor = Color.FromArgb(0xE7, 0x4C, 0x3C);
            goLabel.TextAlign = ContentAlignment.MiddleCenter;
            goLabel.Size = new Size(80, 44);
            goLabel.Location = new Point((int)wheelCenterX - 40, this.ClientSize.Height - 63);
            goLabel.Cursor = Cursors.Hand;
            goLabel.Click += new EventHandler(GoLabel_Click);
            this.Controls.Add(goLabel);

            Button resetBtn = new Button();
            resetBtn.Text = "重置";
            resetBtn.Tag = resetBtn.Text;
            resetBtn.Font = new Font("Microsoft YaHei", 9);
            resetBtn.Size = new Size(70, 24);
            resetBtn.Location = new Point((int)wheelCenterX - 35, 63);
            resetBtn.FlatStyle = FlatStyle.Flat;
            resetBtn.FlatAppearance.BorderColor = Color.FromArgb(0xBD, 0xC3, 0xC7);
            resetBtn.BackColor = Color.FromArgb(0xEC, 0xF0, 0xF1);
            resetBtn.Click += new EventHandler(ResetBtn_Click);
            this.Controls.Add(resetBtn);

            musicBtn = new Button();
            musicBtn.Text = "🎵 " + T("开启背景音乐");
            musicBtn.Font = new Font("Microsoft YaHei", 9);
            musicBtn.Size = new Size(130, 24);
            musicBtn.Location = new Point(300, 63);
            musicBtn.FlatStyle = FlatStyle.Flat;
            musicBtn.FlatAppearance.BorderColor = Color.FromArgb(0xBD, 0xC3, 0xC7);
            musicBtn.BackColor = Color.FromArgb(0xEC, 0xF0, 0xF1);
            musicBtn.Click += new EventHandler(MusicBtn_Click);
            this.Controls.Add(musicBtn);

            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            List<string> otherMusic = new List<string>();
            foreach (string f in Directory.GetFiles(dir))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if ((ext == ".mid" || ext == ".midi" || ext == ".mp3") &&
                    !Path.GetFileName(f).Equals(DefaultMidiFile, StringComparison.OrdinalIgnoreCase))
                {
                    otherMusic.Add(f);
                }
            }
            if (otherMusic.Count == 1)
                customMidiPath = otherMusic[0];
            if (!File.Exists(Path.Combine(dir, DefaultMidiFile)))
            {
                try { File.WriteAllBytes(Path.Combine(dir, DefaultMidiFile), GenerateDefaultMidi()); }
                catch { }
            }

            Button selectMusicBtn = new Button();
            selectMusicBtn.Text = "选择";
            selectMusicBtn.Tag = selectMusicBtn.Text;
            selectMusicBtn.Font = new Font("Microsoft YaHei", 8);
            selectMusicBtn.Size = new Size(40, 24);
            selectMusicBtn.Location = new Point(432, 63);
            selectMusicBtn.FlatStyle = FlatStyle.Flat;
            selectMusicBtn.FlatAppearance.BorderColor = Color.FromArgb(0xBD, 0xC3, 0xC7);
            selectMusicBtn.BackColor = Color.FromArgb(0xEC, 0xF0, 0xF1);
            selectMusicBtn.Click += new EventHandler(SelectMusicBtn_Click);
            this.Controls.Add(selectMusicBtn);

            langBtn = new Button();
            langBtn.Text = "EN";
            langBtn.Font = new Font("Microsoft YaHei", 8);
            langBtn.Size = new Size(32, 24);
            langBtn.Location = new Point((int)wheelCenterX - 75, 63);
            langBtn.FlatStyle = FlatStyle.Flat;
            langBtn.FlatAppearance.BorderColor = Color.FromArgb(0xBD, 0xC3, 0xC7);
            langBtn.BackColor = Color.FromArgb(0xEC, 0xF0, 0xF1);
            langBtn.Click += delegate
            {
                english = !english;
                langBtn.Text = english ? "中" : "EN";
                ApplyLang();
                this.Invalidate();
            };
            this.Controls.Add(langBtn);
            savedEnglish = english;

            InitDict();

            spinTimer = new Timer();
            spinTimer.Interval = 16;
            spinTimer.Tick += new EventHandler(SpinTimer_Tick);

            contextMenu = new ContextMenuStrip();
            newSectorItem = new ToolStripMenuItem("新建片区");
            newSectorItem.Tag = newSectorItem.Text;
            newSectorItem.Click += new EventHandler(NewSectorItem_Click);
            modifyItem = new ToolStripMenuItem("修改内容");
            modifyItem.Tag = modifyItem.Text;
            modifyItem.Click += new EventHandler(ModifyItem_Click);
            childWheelItem = new ToolStripMenuItem("设置子转盘");
            childWheelItem.Tag = childWheelItem.Text;
            childWheelItem.Click += new EventHandler(ChildWheelItem_Click);
            clearChildItem = new ToolStripMenuItem("清除子转盘");
            clearChildItem.Tag = clearChildItem.Text;
            clearChildItem.Click += new EventHandler(ClearChildItem_Click);

            deleteItem = new ToolStripMenuItem("删除");
            deleteItem.Tag = deleteItem.Text;
            deleteItem.Click += new EventHandler(DeleteItem_Click);
            deleteItem.Image = MakeTrashIcon();

            ToolStripMenuItem switchModeItem = new ToolStripMenuItem("切换动画模式");
            switchModeItem.Tag = switchModeItem.Text;
            mode1Item = new ToolStripMenuItem("模式1");
            mode1Item.Tag = mode1Item.Text;
            mode1Item.Click += new EventHandler(Mode1Item_Click);
            mode2Item = new ToolStripMenuItem("模式2");
            mode2Item.Tag = mode2Item.Text;
            mode2Item.Click += new EventHandler(Mode2Item_Click);
            switchModeItem.DropDownItems.Add(mode1Item);
            switchModeItem.DropDownItems.Add(mode2Item);

            contextMenu.Items.Add(newSectorItem);
            contextMenu.Items.Add(modifyItem);

            weightItem = new ToolStripMenuItem("改变占比");
            weightItem.Tag = weightItem.Text;
            weightItem.Click += new EventHandler(WeightItem_Click);
            contextMenu.Items.Add(weightItem);

            contextMenu.Items.Add(childWheelItem);
            contextMenu.Items.Add(clearChildItem);
            contextMenu.Items.Add(deleteItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(switchModeItem);

            mode1Item.Checked = true;
            mode2Item.Checked = false;
            UpdateModeLabel();

            this.Shown += new EventHandler(Form_Shown);
        }

        private void Form_Shown(object sender, EventArgs e)
        {
            if (StartSpinOnLoad)
            {
                StartSpin();
            }
        }

        private void GoLabel_Click(object sender, EventArgs e)
        {
            StartSpin();
        }

        private void ResultList_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            int idx = resultList.IndexFromPoint(e.Location);
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem delItem = new ToolStripMenuItem("删除记录");
            delItem.Tag = delItem.Text;
            delItem.Image = MakeTrashIcon();
            if (idx >= 0)
            {
                int captured = idx;
                delItem.Click += delegate(object s, EventArgs ea)
                {
                    resultList.Items.RemoveAt(captured);
                };
            }
            else
            {
                delItem.Enabled = false;
                delItem.ForeColor = Color.Gray;
            }
            menu.Items.Add(delItem);
            menu.Show(resultList, e.Location);
        }

        private void ClearAllResults_Click(object sender, EventArgs e)
        {
            resultList.Items.Clear();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopMusic();
            base.OnFormClosing(e);
            if (e.CloseReason == CloseReason.UserClosing && !e.Cancel)
            {
                e.Cancel = true;
                using (Form dlg = new Form())
                {
                    dlg.Text = T("关闭确认");
                    dlg.Size = new Size(300, 150);
                    dlg.StartPosition = FormStartPosition.CenterParent;
                    dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                    dlg.MaximizeBox = false;
                    dlg.MinimizeBox = false;

                    Label lbl = new Label();
                    lbl.Text = T("请选择关闭方式：");
                    lbl.Location = new Point(12, 12);
                    lbl.Size = new Size(260, 20);
                    dlg.Controls.Add(lbl);

                    Button btnDirect = new Button();
                    btnDirect.Text = T("直接关闭");
                    btnDirect.Size = new Size(120, 35);
                    btnDirect.Location = new Point(12, 45);
                    btnDirect.Click += delegate { dlg.DialogResult = DialogResult.No; dlg.Close(); };
                    dlg.Controls.Add(btnDirect);

                    Button btnSave = new Button();
                    btnSave.Text = T("保存并关闭");
                    btnSave.Size = new Size(120, 35);
                    btnSave.Location = new Point(150, 45);
                    btnSave.Click += delegate { dlg.DialogResult = DialogResult.Yes; dlg.Close(); };
                    dlg.Controls.Add(btnSave);

                    dlg.ShowDialog(this);
                    if (dlg.DialogResult == DialogResult.Yes)
                    {
                        SaveConfig();
                        Application.Exit();
                    }
                    else if (dlg.DialogResult == DialogResult.No)
                    {
                        if (english != savedEnglish)
                        {
                            english = savedEnglish;
                            langBtn.Text = english ? "中" : "EN";
                            ApplyLang();
                        }
                        Application.Exit();
                    }
                }
            }
        }

private void ResetBtn_Click(object sender, EventArgs e)
        {
            if (isSpinning || isResetting) return;
            if (MessageBox.Show(T("确定重置转盘为初始状态？所有修改和记录将被清除。"), T("确认重置"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            StopMusic();
            if (customMidiPath != null)
            {
                customMidiPath = null;
                musicBtn.Text = "🎵 " + T("开启背景音乐");
            }
            data = originalData.DeepClone();
            currentAngle = 0;
            pointerAngle = startOffset;
            resultList.Items.Clear();
            if (english)
            {
                english = false;
                langBtn.Text = "EN";
                ApplyLang();
            }
            savedEnglish = english;
            SaveConfig();
            this.Invalidate();
        }

        private void InitDict()
        {
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            string enFile = Path.Combine(dir, "lang_en.txt");
            string zhFile = Path.Combine(dir, "lang_zh.txt");

            string[] keys = new string[] {
                    "背景音乐", "开启背景音乐", "开启", "选择",
                    "新建片区", "修改内容", "改变占比", "设置子转盘", "清除子转盘",
                    "删除", "删除记录", "清除全部记录", "重置",
                    "关闭确认", "请选择关闭方式：", "直接关闭", "保存并关闭",
                    "确定重置转盘为初始状态？所有修改和记录将被清除。", "确认重置",
                    "输入片区名称：", "新片区", "输入片区新名称：", "输入百分比 (1-100)：",
                    "确定清除此扇区的子转盘？", "确认",
                    "至少保留1个片区！", "提示", "确认删除",
                    "恭喜您抽中了：", "抽奖结果", "确定", "取消",
                    "配置子转盘 - ", "为此扇区设置子转盘奖项（直接输入内容，空格隔开）：",
                    "保存", "导出示例",
                    "当前子转盘（点击扇区编辑更深的子转盘）：", " [有子转盘]",
                    "编辑", "删", "进入", "调整占比",
                    "至少需要2个奖项！", "已保存 ", " 个奖项！", "成功",
                    "文本文件|*.txt", "音频文件|*.mid;*.midi;*.mp3|所有文件|*.*",
                    "子转盘配置示例.txt", "选项A 选项B 选项C 选项D",
                    "示例已导出！", "导出成功",
                    "抽奖记录", "切换动画模式", "模式1", "模式2",
                    "当前模式：模式1（扇区转动）", "当前模式：模式2（指针转动）",
                    "无法打开 MIDI 设备！", "无法解析 MIDI 文件！",
                    "无法打开音频设备！", "无法解析音频文件！",
                    "奖励一", "奖励二", "奖励三", "奖励四", "奖励五", "奖励六",
                    "幸运转盘 (父转盘)"
                };

                string[] enValues = new string[] {
                    "BGM", "Open BGM", "Open", "Sel",
                    "New Sector", "Edit", "Change Weight", "Sub Wheel", "Clear Sub",
                    "Delete", "Delete Record", "Clear All", "Reset",
                    "Exit Confirm", "Choose exit mode:", "Exit (no save)", "Save && Exit",
                    "Reset to defaults? All data will be lost.", "Confirm Reset",
                    "Enter sector name:", "New Sector", "Enter new name:", "Enter percentage (1-100):",
                    "Clear sub-wheel for this sector?", "Confirm",
                    "At least 1 sector required!", "Info", "Delete Confirm",
                    "You won:", "Result", "OK", "Cancel",
                    "Sub Wheel - ", "Enter sub prizes (space separated):",
                    "Save", "Export Sample",
                    "Current sub-wheel (click to edit deeper):", " [has sub]",
                    "Edit", "Del", "Open", "Adjust Wt",
                    "At least 2 prizes required!", "Saved ", " prizes!", "Success",
                    "Text|*.txt", "Audio|*.mid;*.midi;*.mp3|All|*.*",
                    "subwheel_sample.txt", "OptionA OptionB OptionC OptionD",
                    "Sample exported!", "Export OK",
                    "History", "Switch Mode", "Mode1", "Mode2",
                    "Mode: Sector Spin", "Mode: Pointer Spin",
                    "Cannot open MIDI device!", "Invalid MIDI file!",
                    "Cannot open audio device!", "Invalid audio file!",
                    "Prize 1", "Prize 2", "Prize 3", "Prize 4", "Prize 5", "Prize 6",
                    "Lucky Wheel"
                };

                var sbEn = new StringBuilder();
                var sbZh = new StringBuilder();
                for (int i = 0; i < keys.Length; i++)
                {
                    sbEn.AppendLine(keys[i] + "=" + enValues[i]);
                    sbZh.AppendLine(keys[i] + "=" + keys[i]);
                }
                try { File.WriteAllText(enFile, sbEn.ToString(), Encoding.UTF8); } catch { }
                try { File.WriteAllText(zhFile, sbZh.ToString(), Encoding.UTF8); } catch { }
            LoadLang();
        }

        private void LoadLang()
        {
            en.Clear();
            zh.Clear();
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            string enFile = Path.Combine(dir, "lang_en.txt");
            string zhFile = Path.Combine(dir, "lang_zh.txt");
            try
            {
                foreach (string line in File.ReadAllLines(enFile, Encoding.UTF8))
                {
                    string l = line;
                    int eq = l.IndexOf('=');
                    if (eq > 0)
                    {
                        string k = l.Substring(0, eq);
                        string v = l.Substring(eq + 1);
                        if (k.Length > 0) en[k] = v;
                    }
                }
            }
            catch { }
            try
            {
                foreach (string line in File.ReadAllLines(zhFile, Encoding.UTF8))
                {
                    string l = line;
                    int eq = l.IndexOf('=');
                    if (eq > 0)
                    {
                        string k = l.Substring(0, eq);
                        string v = l.Substring(eq + 1);
                        if (k.Length > 0) zh[k] = v;
                    }
                }
            }
            catch { }
            enToZh.Clear();
            foreach (var kv in en)
            {
                if (zh.ContainsKey(kv.Key))
                    enToZh[kv.Value] = zh[kv.Key];
            }
        }

        internal static string T(string key)
        {
            if (english)
            {
                string val;
                if (en.TryGetValue(key, out val)) return val;
                return key;
            }
            else
            {
                string val;
                if (zh.TryGetValue(key, out val)) return val;
                if (enToZh.TryGetValue(key, out val)) return val;
                return key;
            }
        }

        private void ApplyLang()
        {
            this.Text = T(_titleKey);
            foreach (Control c in this.Controls)
            {
                string orig = c.Tag as string;
                if (orig != null && orig.Length > 0)
                    c.Text = T(orig);
            }
            musicBtn.Text = MusicLabel();
            foreach (ToolStripItem item in contextMenu.Items)
            {
                string ot = item.Tag as string;
                if (ot != null)
                {
                    item.Text = T(ot);
                    ToolStripMenuItem mi = item as ToolStripMenuItem;
                    if (mi != null)
                    {
                        foreach (ToolStripItem sub in mi.DropDownItems)
                        {
                            string st = sub.Tag as string;
                            if (st != null)
                                sub.Text = T(st);
                        }
                    }
                }
            }
            UpdateModeLabel();
            for (int i = 0; i < data.Prizes.Length; i++)
            {
                if (i < originalData.Prizes.Length && data.Prizes[i] == originalData.Prizes[i])
                    data.Prizes[i] = T(data.Prizes[i]);
            }
            for (int i = 0; i < data.Prizes.Length && i < originalData.Prizes.Length; i++)
            {
                originalData.Prizes[i] = data.Prizes[i];
            }
        }

        private void SelectMusicBtn_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = T("音频文件|*.mid;*.midi;*.mp3|所有文件|*.*");
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    StopMusic();
                    customMidiPath = dlg.FileName;
                    musicBtn.Text = MusicLabel();
                    SaveConfig();
                }
            }
        }

        private void StopMusic()
        {
            if (musicOn)
            {
                StringBuilder sb = new StringBuilder(16);
                mciSendString("stop music", sb, sb.Capacity, IntPtr.Zero);
                mciSendString("close music", sb, sb.Capacity, IntPtr.Zero);
                musicOn = false;
                musicBtn.ForeColor = Color.Black;
            }
        }

        private string MusicLabel()
        {
            string label = customMidiPath != null
                ? Path.GetFileNameWithoutExtension(customMidiPath)
                : T("背景音乐");
            return "🎵 " + T("开启") + "(" + label + ")";
        }

        private string MidiFullPath(string name)
        {
            if (Path.IsPathRooted(name)) return name;
            return Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), name);
        }

        private void MusicBtn_Click(object sender, EventArgs e)
        {
            if (!musicOn)
            {
                string file = MidiFullPath(customMidiPath ?? DefaultMidiFile);
                StringBuilder sb = new StringBuilder(512);
                string ext = Path.GetExtension(file).ToLowerInvariant();
                string mciType = (ext == ".mp3") ? "mpegvideo" : "sequencer";
                int r = mciSendString("open \"" + file + "\" type " + mciType + " alias music", sb, sb.Capacity, IntPtr.Zero);
                if (r != 0)
                {
                    mciSendString("close all", sb, sb.Capacity, IntPtr.Zero);
                    r = mciSendString("open \"" + file + "\" type " + mciType + " alias music", sb, sb.Capacity, IntPtr.Zero);
                }
                if (r == 0)
                {
                    string loop = (customMidiPath == null) ? " repeat" : "";
                    mciSendString("set music volume 1000", sb, sb.Capacity, IntPtr.Zero);
                    mciSendString("play music" + loop, sb, sb.Capacity, IntPtr.Zero);
                    musicBtn.ForeColor = Color.FromArgb(0xE7, 0x4C, 0x3C);
                    musicOn = true;
                }
            }
            else
            {
                StopMusic();
                musicBtn.Text = MusicLabel();
            }
        }

        private const string ConfigFile = "WheelConfig.dat";

        private void SaveConfig()
        {
            try
            {
                string config = data.Serialize();
                if (customMidiPath != null)
                    config += "MUSIC:" + customMidiPath + "\n";
                config += "LANG:" + (english ? "en" : "zh") + "\n";
                File.WriteAllText(ConfigFile, config);
            }
            catch { }
        }

        public void LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    string text = File.ReadAllText(ConfigFile);
                    string[] lines = text.Split('\n');
                    string dataText = "";
                    foreach (string l in lines)
                    {
                        string line = l.Trim();
                        if (line.StartsWith("MIDI:") || line.StartsWith("MUSIC:"))
                        {
                            string mp = line.Substring(line.StartsWith("MUSIC:") ? 6 : 5).Trim();
                            if (File.Exists(mp))
                            {
                                customMidiPath = mp;
                                musicBtn.Text = MusicLabel();
                            }
                        }
                        else if (line == "LANG:en")
                        {
                            english = true;
                        }
                        else if (!line.StartsWith("LANG:"))
                        {
                            dataText += line + "\n";
                        }
                    }
                    WheelData loaded = WheelData.Deserialize(dataText.Trim());
                    if (loaded != null)
                        data = loaded;
                }
            }
            catch { }
            if (english)
            {
                langBtn.Text = "中";
                ApplyLang();
            }
            savedEnglish = english;
        }

        private void UpdateModeLabel()
        {
            if (spinMode == 1)
                modeLabel.Text = english ? T("当前模式：模式1（扇区转动）") : "当前模式：模式1（扇区转动）";
            else
                modeLabel.Text = english ? T("当前模式：模式2（指针转动）") : "当前模式：模式2（指针转动）";
        }

        private void Mode1Item_Click(object sender, EventArgs e)
        {
            spinMode = 1;
            mode1Item.Checked = true;
            mode2Item.Checked = false;
            UpdateModeLabel();
            if (!isSpinning && !isResetting)
            {
                if (currentAngle % 360 != 0)
                {
                    currentAngle = ((int)(currentAngle / 360) + 1) * 360;
                }
                pointerAngle = startOffset;
                this.Invalidate();
            }
        }

        private void Mode2Item_Click(object sender, EventArgs e)
        {
            spinMode = 2;
            mode1Item.Checked = false;
            mode2Item.Checked = true;
            UpdateModeLabel();
            if (!isSpinning && !isResetting)
            {
                if (currentAngle % 360 != 0)
                {
                    currentAngle = ((int)(currentAngle / 360) + 1) * 360;
                }
                pointerAngle = startOffset;
                this.Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            cx = wheelCenterX;
            cy = (this.ClientSize.Height + 80) / 2f - 10;
            r = Math.Min(cx, cy) - 25;

            int n = data.SectorCount;
            float totalW = data.WeightTotal;
            float normAngle = ((currentAngle % 360) + 360) % 360;

            for (int i = 0; i < n; i++)
            {
                float sweep = (data.Weights[i] / totalW) * 360f;
                float sa;
                if (spinMode == 1)
                    sa = normAngle + startOffset + GetSweepOffset(i);
                else
                    sa = startOffset + GetSweepOffset(i);

                using (SolidBrush brush = new SolidBrush(data.Colors[i % data.Colors.Length]))
                {
                    g.FillPie(brush, cx - r, cy - r, r * 2, r * 2, sa, sweep);
                }
            }

            using (Pen pen = new Pen(Color.White, 2))
            {
                for (int i = 0; i < n; i++)
                {
                    double angle;
                    float offset = GetSweepOffset(i);
                    if (spinMode == 1)
                        angle = (normAngle + startOffset + offset) * Math.PI / 180;
                    else
                        angle = (startOffset + offset) * Math.PI / 180;
                    g.DrawLine(pen, cx, cy, cx + r * (float)Math.Cos(angle), cy + r * (float)Math.Sin(angle));
                }
            }

            StringFormat sf = new StringFormat();
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;

            for (int i = 0; i < n; i++)
            {
                float sweep = (data.Weights[i] / totalW) * 360f;
                double midAngle;
                float offset = GetSweepOffset(i);
                if (spinMode == 1)
                    midAngle = (normAngle + startOffset + offset + sweep / 2) * Math.PI / 180;
                else
                    midAngle = (startOffset + offset + sweep / 2) * Math.PI / 180;
                float tx = cx + r * 0.62f * (float)Math.Cos(midAngle);
                float ty = cy + r * 0.62f * (float)Math.Sin(midAngle);
                g.DrawString(data.Prizes[i], new Font("Microsoft YaHei", 9, FontStyle.Bold), Brushes.White, tx, ty, sf);
            }

            using (Pen rimPen = new Pen(Color.Gray, 3))
            {
                g.DrawEllipse(rimPen, cx - r, cy - r, r * 2, r * 2);
            }

            float ptrAngle = (spinMode == 1) ? startOffset : pointerAngle;
            DrawPointerTriangle(g, ptrAngle);

            g.FillEllipse(Brushes.DarkSlateGray, cx - 8, cy - 8, 16, 16);
            g.DrawEllipse(Pens.White, cx - 8, cy - 8, 16, 16);
        }

        private float GetSweepOffset(int sectorIndex)
        {
            float totalW = data.WeightTotal;
            float offset = 0;
            for (int i = 0; i < sectorIndex; i++)
                offset += (data.Weights[i] / totalW) * 360f;
            return offset;
        }

        private int HitTestSector(float normalizedAngle)
        {
            float totalW = data.WeightTotal;
            float accum = 0;
            for (int i = 0; i < data.SectorCount; i++)
            {
                float sweep = (data.Weights[i] / totalW) * 360f;
                if (normalizedAngle >= accum && normalizedAngle < accum + sweep)
                    return i;
                accum += sweep;
            }
            return data.SectorCount - 1;
        }

        private void DrawPointerTriangle(Graphics g, float angle)
        {
            float length = r - 8;
            float baseOffset = 6;
            float halfBase = 4;

            double rad = angle * Math.PI / 180;
            double perp = (angle + 90) * Math.PI / 180;

            float tipX = cx + length * (float)Math.Cos(rad);
            float tipY = cy + length * (float)Math.Sin(rad);
            float bcX = cx + baseOffset * (float)Math.Cos(rad);
            float bcY = cy + baseOffset * (float)Math.Sin(rad);

            PointF[] pts = new PointF[]
            {
                new PointF(tipX, tipY),
                new PointF(bcX + halfBase * (float)Math.Cos(perp), bcY + halfBase * (float)Math.Sin(perp)),
                new PointF(bcX - halfBase * (float)Math.Cos(perp), bcY - halfBase * (float)Math.Sin(perp)),
            };

            g.FillPolygon(Brushes.Red, pts);
            g.DrawPolygon(Pens.DarkRed, pts);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (isSpinning || isResetting) return;

            float dx = e.X - cx;
            float dy = e.Y - cy;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);

            if (e.Button == MouseButtons.Left)
            {
                if (dist < r && dist > 15)
                {
                    StartSpin();
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                bool onSector = dist < r && dist > 15;

                if (onSector)
                {
                    double a = Math.Atan2(dy, dx) * 180 / Math.PI;
                    if (a < 0) a += 360;
                    double raw;
                    if (spinMode == 1)
                        raw = a - currentAngle - startOffset;
                    else
                        raw = a - startOffset;
                    double normalized = ((raw % 360) + 360) % 360;
                    rightClickSector = HitTestSector((float)normalized);
                }
                else
                {
                    rightClickSector = -1;
                }

                newSectorItem.Enabled = !onSector;
                modifyItem.Enabled = onSector;
                weightItem.Enabled = onSector;
                weightItem.ForeColor = onSector ? Color.Black : Color.Gray;

                bool hasChild = onSector && data.HasChild(rightClickSector);
                childWheelItem.Enabled = onSector && !hasChild;
                childWheelItem.ForeColor = childWheelItem.Enabled ? Color.Black : Color.Gray;
                childWheelItem.Visible = !hasChild || !onSector;

                clearChildItem.Enabled = hasChild;
                clearChildItem.ForeColor = hasChild ? Color.Black : Color.Gray;
                clearChildItem.Visible = hasChild;

                deleteItem.Enabled = onSector;
                deleteItem.ForeColor = onSector ? Color.Black : Color.Gray;
                deleteItem.Visible = true;

                modifyItem.ForeColor = onSector ? Color.Black : Color.Gray;

                contextMenu.Show(this, e.Location);
            }
        }

        private void NewSectorItem_Click(object sender, EventArgs e)
        {
            string defaultName = (english ? "Prize " : "奖励") + (data.SectorCount + 1);
            string name = ShowInputDialog(T("新建片区"), T("输入片区名称："), defaultName);
            if (name == null || name.Trim() == "") return;

            string[] newPrizes = new string[data.Prizes.Length + 1];
            Array.Copy(data.Prizes, newPrizes, data.Prizes.Length);
            newPrizes[data.Prizes.Length] = name.Trim();

            Color[] newColors = new Color[data.Colors.Length + 1];
            Array.Copy(data.Colors, newColors, data.Colors.Length);
            int hue = data.SectorCount * 360 / (data.SectorCount + 1) + rand.Next(20);
            newColors[data.Colors.Length] = HsvToRgb(hue % 360, 180, 220);

            WheelData[] newChildren = new WheelData[data.Children.Length + 1];
            Array.Copy(data.Children, newChildren, data.Children.Length);
            newChildren[data.Children.Length] = null;

            float[] newWeights = new float[data.Weights.Length + 1];
            Array.Copy(data.Weights, newWeights, data.Weights.Length);
            newWeights[data.Weights.Length] = 1f;

            data.Prizes = newPrizes;
            data.Colors = newColors;
            data.Children = newChildren;
            data.Weights = newWeights;

            string[] origPrizes = new string[originalData.Prizes.Length + 1];
            Array.Copy(originalData.Prizes, origPrizes, originalData.Prizes.Length);
            origPrizes[originalData.Prizes.Length] = name.Trim();
            Color[] origColors = new Color[originalData.Colors.Length + 1];
            Array.Copy(originalData.Colors, origColors, originalData.Colors.Length);
            origColors[originalData.Colors.Length] = newColors[newColors.Length - 1];
            WheelData[] origChildren = new WheelData[originalData.Children.Length + 1];
            Array.Copy(originalData.Children, origChildren, originalData.Children.Length);
            origChildren[originalData.Children.Length] = null;
            float[] origWeights = new float[originalData.Weights.Length + 1];
            Array.Copy(originalData.Weights, origWeights, originalData.Weights.Length);
            origWeights[originalData.Weights.Length] = 1f;
            originalData.Prizes = origPrizes;
            originalData.Colors = origColors;
            originalData.Children = origChildren;
            originalData.Weights = origWeights;

            this.Invalidate();
        }

        private void ModifyItem_Click(object sender, EventArgs e)
        {
            if (rightClickSector < 0 || rightClickSector >= data.SectorCount) return;
            string name = ShowInputDialog(T("修改内容"), T("输入片区新名称："), data.Prizes[rightClickSector]);
            if (name == null || name.Trim() == "") return;
            data.Prizes[rightClickSector] = name.Trim();
            this.Invalidate();
        }

        private void WeightItem_Click(object sender, EventArgs e)
        {
            if (rightClickSector < 0 || rightClickSector >= data.SectorCount) return;
            float? result = ShowWeightDialog(T("改变占比"), data.Weights[rightClickSector]);
            if (result.HasValue)
            {
                data.SetWeight(rightClickSector, result.Value);
                this.Invalidate();
            }
        }

        private void ChildWheelItem_Click(object sender, EventArgs e)
        {
            if (rightClickSector < 0 || rightClickSector >= data.SectorCount) return;
            ConfigDialog dlg = new ConfigDialog(data, rightClickSector, data.Prizes[rightClickSector]);
            dlg.ShowDialog(this);
            this.Invalidate();
        }

        private void ClearChildItem_Click(object sender, EventArgs e)
        {
            if (rightClickSector < 0 || rightClickSector >= data.SectorCount) return;
            if (MessageBox.Show(T("确定清除此扇区的子转盘？"), T("确认"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                data.Children[rightClickSector] = null;
                this.Invalidate();
            }
        }

        private void DeleteItem_Click(object sender, EventArgs e)
        {
            if (rightClickSector < 0 || rightClickSector >= data.SectorCount) return;
            if (data.SectorCount <= 1)
            {
                MessageBox.Show(T("至少保留1个片区！"), T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (MessageBox.Show(T("确定删除") + data.Prizes[rightClickSector] + "？", T("确认删除"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                string[] newPrizes = new string[data.Prizes.Length - 1];
                Color[] newColors = new Color[data.Colors.Length - 1];
                WheelData[] newChildren = new WheelData[data.Children.Length - 1];
                float[] newWeights = new float[data.Weights.Length - 1];
                int idx = 0;
                for (int i = 0; i < data.Prizes.Length; i++)
                {
                    if (i == rightClickSector) continue;
                    newPrizes[idx] = data.Prizes[i];
                    newColors[idx] = data.Colors[i];
                    newChildren[idx] = data.Children[i];
                    newWeights[idx] = data.Weights[i];
                    idx++;
                }
                data.Prizes = newPrizes;
                data.Colors = newColors;
                data.Children = newChildren;
                data.Weights = newWeights;
                this.Invalidate();
            }
        }

        public void StartSpin()
        {
            if (isSpinning || isResetting) return;
            isSpinning = true;
            if (spinMode == 1)
                targetAngle = currentAngle + rand.Next(1200, 2400);
            else
                targetPointerAngle = pointerAngle + rand.Next(1200, 2400);
            spinTimer.Start();
        }

        private void SpinTimer_Tick(object sender, EventArgs e)
        {
            if (isResetting)
            {
                if (spinMode == 1)
                    currentAngle = resetTarget;
                else
                    pointerAngle = resetTarget;
                spinTimer.Stop();
                isResetting = false;
                this.Invalidate();
                return;
            }

            if (spinMode == 1)
            {
                float r = targetAngle - currentAngle;
                if (r > 5)
                {
                    currentAngle += Math.Min(r, Math.Max(3, r / 25));
                    this.Invalidate();
                }
                else
                {
                    currentAngle = targetAngle;
                    spinTimer.Stop();
                    isSpinning = false;
                    this.Invalidate();
                    OnSpinComplete();
                }
            }
            else
            {
                float r = targetPointerAngle - pointerAngle;
                if (r > 5)
                {
                    pointerAngle += Math.Min(r, Math.Max(3, r / 25));
                    this.Invalidate();
                }
                else
                {
                    pointerAngle = targetPointerAngle;
                    spinTimer.Stop();
                    isSpinning = false;
                    this.Invalidate();
                    OnSpinComplete();
                }
            }
        }

        private int GetSelectedSector()
        {
            if (spinMode == 1)
            {
                float normalized = currentAngle % 360;
                float adjusted = (360 - normalized) % 360;
                return (int)(adjusted / (360f / data.SectorCount)) % data.SectorCount;
            }
            else
            {
                float adjusted = (pointerAngle - startOffset + 1440) % 360;
                return (int)(adjusted / (360f / data.SectorCount)) % data.SectorCount;
            }
        }

        private void OnSpinComplete()
        {
            int idx = GetSelectedSector();

            if (data.HasChild(idx))
            {
                WheelForm childForm = new WheelForm(data.Children[idx], data.Prizes[idx]);
                childForm.StartSpinOnLoad = true;
                if (parentResult != null)
                    childForm.ParentContext = parentResult + " → " + data.Prizes[idx];
                else
                    childForm.ParentContext = data.Prizes[idx];
                childForm.ShowDialog(this);
                LastResult = childForm.LastResult;
                resultList.Items.Insert(0, LastResult);
                FlashReset();
            }
            else
            {
                string msg;
                if (parentResult != null)
                    msg = parentResult + " → " + data.Prizes[idx];
                else
                    msg = data.Prizes[idx];
                LastResult = msg;
                resultList.Items.Insert(0, msg);
                MessageBox.Show(T("恭喜您抽中了：") + msg + "！", T("抽奖结果"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (this.Modal)
                {
                    this.Close();
                }
                else
                {
                    FlashReset();
                }
            }
        }

        private void FlashReset()
        {
            isResetting = true;
            if (spinMode == 1)
                resetTarget = ((int)(currentAngle / 360) + 1) * 360;
            else
                resetTarget = startOffset + ((int)((pointerAngle - startOffset) / 360) + 1) * 360;
            spinTimer.Start();
        }

        internal static string ShowInputDialog(string title, string prompt, string defaultValue)
        {
            Form dlg = new Form();
            dlg.Text = title;
            dlg.Size = new Size(320, 150);
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
            dlg.MaximizeBox = false;
            dlg.MinimizeBox = false;

            Label lbl = new Label();
            lbl.Text = T(prompt);
            lbl.Location = new Point(12, 12);
            lbl.Size = new Size(280, 20);
            dlg.Controls.Add(lbl);

            TextBox txt = new TextBox();
            txt.Text = defaultValue;
            txt.Location = new Point(12, 38);
            txt.Size = new Size(280, 22);
            dlg.Controls.Add(txt);

            Button okBtn = new Button();
            okBtn.Text = T("确定");
            okBtn.Location = new Point(80, 72);
            okBtn.Size = new Size(70, 28);
            okBtn.DialogResult = DialogResult.OK;
            dlg.Controls.Add(okBtn);

            Button cancelBtn = new Button();
            cancelBtn.Text = T("取消");
            cancelBtn.Location = new Point(170, 72);
            cancelBtn.Size = new Size(70, 28);
            cancelBtn.DialogResult = DialogResult.Cancel;
            dlg.Controls.Add(cancelBtn);

            dlg.AcceptButton = okBtn;
            dlg.CancelButton = cancelBtn;

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                return txt.Text;
            }
            return null;
        }

        internal static float? ShowWeightDialog(string title, float currentValue)
        {
            Form dlg = new Form();
            dlg.Text = title;
            dlg.Size = new Size(340, 160);
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
            dlg.MaximizeBox = false;
            dlg.MinimizeBox = false;

            Label lbl = new Label();
            lbl.Text = T("输入百分比 (1-100)：");
            lbl.Location = new Point(12, 12);
            lbl.Size = new Size(300, 20);
            dlg.Controls.Add(lbl);

            TextBox txt = new TextBox();
            txt.Text = Math.Round(currentValue).ToString();
            txt.Location = new Point(12, 38);
            txt.Size = new Size(50, 22);
            dlg.Controls.Add(txt);

            TrackBar tb = new TrackBar();
            tb.Minimum = 1;
            tb.Maximum = 100;
            tb.Value = (int)Math.Round(currentValue);
            tb.Location = new Point(70, 36);
            tb.Size = new Size(240, 30);
            tb.TickFrequency = 10;
            dlg.Controls.Add(tb);

            tb.ValueChanged += delegate
            {
                txt.Text = tb.Value.ToString();
            };
            txt.TextChanged += delegate
            {
                int v;
                if (int.TryParse(txt.Text, out v) && v >= 1 && v <= 100)
                    tb.Value = v;
            };

            Button okBtn = new Button();
            okBtn.Text = T("确定");
            okBtn.Location = new Point(80, 85);
            okBtn.Size = new Size(70, 28);
            okBtn.DialogResult = DialogResult.OK;
            dlg.Controls.Add(okBtn);

            Button cancelBtn = new Button();
            cancelBtn.Text = T("取消");
            cancelBtn.Location = new Point(170, 85);
            cancelBtn.Size = new Size(70, 28);
            cancelBtn.DialogResult = DialogResult.Cancel;
            dlg.Controls.Add(cancelBtn);

            dlg.AcceptButton = okBtn;
            dlg.CancelButton = cancelBtn;

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                int v;
                if (int.TryParse(txt.Text, out v) && v >= 1 && v <= 100)
                    return v;
            }
            return null;
        }

        private Color HsvToRgb(int hue, int saturation, int value)
        {
            double h = hue / 60.0;
            double s = saturation / 255.0;
            double v = value / 255.0;
            int i = (int)Math.Floor(h) % 6;
            double f = h - Math.Floor(h);
            double p = v * (1 - s);
            double q = v * (1 - f * s);
            double t = v * (1 - (1 - f) * s);
            double r = 0, g = 0, b = 0;
            switch (i)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                case 5: r = v; g = p; b = q; break;
            }
            return Color.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
        }

        private Bitmap MakeTrashIcon()
        {
            Bitmap bmp = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                using (Pen p = new Pen(Color.Black, 1.5f))
                {
                    g.DrawLine(p, 3, 2, 13, 2);
                    g.DrawLine(p, 5, 2, 5, 1);
                    g.DrawLine(p, 11, 2, 11, 1);
                    g.DrawRectangle(p, 2, 4, 12, 10);
                    g.DrawLine(p, 4, 7, 4, 13);
                    g.DrawLine(p, 8, 7, 8, 13);
                    g.DrawLine(p, 12, 7, 12, 13);
                }
            }
            return bmp;
        }

        public string ParentContext
        {
            get { return parentResult; }
            set { parentResult = value; }
        }
    }

    public class ConfigDialog : Form
    {
        private WheelData parentData;
        private int sectorIndex;
        private TextBox prizeTextBox;
        private Panel childPanel;

        public ConfigDialog(WheelData parentData, int sectorIndex, string sectorName)
        {
            this.parentData = parentData;
            this.sectorIndex = sectorIndex;
            this.Text = WheelForm.T("配置子转盘 - ") + sectorName;
            this.Size = new Size(420, 420);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            Label lbl = new Label();
            lbl.Text = WheelForm.T("为此扇区设置子转盘奖项（直接输入内容，空格隔开）：");
            lbl.Location = new Point(12, 12);
            lbl.Size = new Size(380, 20);
            this.Controls.Add(lbl);

            prizeTextBox = new TextBox();
            prizeTextBox.Multiline = true;
            prizeTextBox.Size = new Size(380, 80);
            prizeTextBox.Location = new Point(12, 38);
            prizeTextBox.ScrollBars = ScrollBars.Vertical;
            this.Controls.Add(prizeTextBox);

            if (parentData.Children[sectorIndex] != null)
            {
                WheelData child = parentData.Children[sectorIndex];
                prizeTextBox.Text = string.Join(" ", child.Prizes);
            }

            Button saveBtn = new Button();
            saveBtn.Text = WheelForm.T("保存");
            saveBtn.Size = new Size(80, 30);
            saveBtn.Location = new Point(12, 130);
            saveBtn.Click += new EventHandler(SaveBtn_Click);
            this.Controls.Add(saveBtn);

            Button clearBtn = new Button();
            clearBtn.Text = WheelForm.T("清除子转盘");
            clearBtn.Size = new Size(100, 30);
            clearBtn.Location = new Point(100, 130);
            clearBtn.Click += new EventHandler(ClearBtn_Click);
            this.Controls.Add(clearBtn);

            Button sampleBtn = new Button();
            sampleBtn.Text = WheelForm.T("导出示例");
            sampleBtn.Size = new Size(80, 30);
            sampleBtn.Location = new Point(208, 130);
            sampleBtn.Click += new EventHandler(SampleBtn_Click);
            this.Controls.Add(sampleBtn);

            childPanel = new Panel();
            childPanel.Location = new Point(12, 175);
            childPanel.Size = new Size(380, 200);
            childPanel.AutoScroll = true;
            this.Controls.Add(childPanel);

            RefreshChildPanel();
        }

        private void RefreshChildPanel()
        {
            childPanel.Controls.Clear();

            WheelData child = parentData.Children[sectorIndex];
            if (child == null) return;

            int y = 0;
            Label infoLbl = new Label();
            infoLbl.Text = WheelForm.T("当前子转盘（点击扇区编辑更深的子转盘）：");
            infoLbl.Size = new Size(360, 20);
            infoLbl.Location = new Point(0, y);
            childPanel.Controls.Add(infoLbl);
            y += 25;

            for (int i = 0; i < child.SectorCount; i++)
            {
                Label sectorLbl = new Label();
                if (child.Children[i] != null)
                {
                    sectorLbl.Text = (i + 1) + ". " + child.Prizes[i] + WheelForm.T(" [有子转盘]");
                }
                else
                {
                    sectorLbl.Text = (i + 1) + ". " + child.Prizes[i];
                }
                sectorLbl.Size = new Size(155, 25);
                sectorLbl.Location = new Point(5, y);
                childPanel.Controls.Add(sectorLbl);

                Button editBtn = new Button();
                editBtn.Text = WheelForm.T("编辑");
                editBtn.Size = new Size(35, 23);
                editBtn.Location = new Point(162, y);
                int capturedIdx = i;
                editBtn.Click += delegate(object s, EventArgs e)
                {
                    ConfigDialog deeper = new ConfigDialog(child, capturedIdx, child.Prizes[capturedIdx]);
                    deeper.ShowDialog(this);
                    RefreshChildPanel();
                };
                childPanel.Controls.Add(editBtn);

                Button wtBtn = new Button();
                wtBtn.Text = WheelForm.T("调整占比");
                wtBtn.Size = new Size(35, 23);
                wtBtn.Location = new Point(199, y);
                int capturedWt = i;
                wtBtn.Click += delegate(object s, EventArgs e)
                {
                    float? result = WheelForm.ShowWeightDialog(WheelForm.T("改变占比"), child.Weights[capturedWt]);
                    if (result.HasValue)
                    {
                        child.SetWeight(capturedWt, result.Value);
                        RefreshChildPanel();
                    }
                };
                childPanel.Controls.Add(wtBtn);

                Button delBtn = new Button();
                delBtn.Text = WheelForm.T("删");
                delBtn.Size = new Size(28, 23);
                delBtn.Location = new Point(236, y);
                int capturedDel = i;
                delBtn.Click += delegate(object s, EventArgs e)
                {
                    if (child.SectorCount <= 1)
                    {
                        MessageBox.Show(WheelForm.T("至少保留1个片区！"), WheelForm.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    if (MessageBox.Show(WheelForm.T("确定删除") + child.Prizes[capturedDel] + "？", WheelForm.T("确认删除"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        string[] newPrizes = new string[child.Prizes.Length - 1];
                        Color[] newColors = new Color[child.Colors.Length - 1];
                        WheelData[] newChildren = new WheelData[child.Children.Length - 1];
                        int di = 0;
                        for (int j = 0; j < child.Prizes.Length; j++)
                        {
                            if (j == capturedDel) continue;
                            newPrizes[di] = child.Prizes[j];
                            newColors[di] = child.Colors[j];
                            newChildren[di] = child.Children[j];
                            di++;
                        }
                        child.Prizes = newPrizes;
                        child.Colors = newColors;
                        child.Children = newChildren;
                        RefreshChildPanel();
                    }
                };
                childPanel.Controls.Add(delBtn);

                if (child.Children[i] != null)
                {
                    Button gotoBtn = new Button();
                    gotoBtn.Text = WheelForm.T("进入");
                    gotoBtn.Size = new Size(45, 23);
                    gotoBtn.Location = new Point(315, y);
                    int capturedGoto = i;
                    gotoBtn.Click += delegate(object s, EventArgs e)
                    {
                        WheelForm preview = new WheelForm(child.Children[capturedGoto], child.Prizes[capturedGoto]);
                        preview.ShowDialog(this);
                    };
                    childPanel.Controls.Add(gotoBtn);
                }

                y += 28;
            }
        }

        private string[] ParsePrizes(string text)
        {
            return text.Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private Color[] GenerateColors(int count)
        {
            Color[] result = new Color[count];
            for (int i = 0; i < count; i++)
            {
                int hue = (i * 360 / count + new Random().Next(20)) % 360;
                result[i] = HsvToRgb(hue, 180, 220);
            }
            return result;
        }

        private Color HsvToRgb(int hue, int saturation, int value)
        {
            double h = hue / 60.0;
            double s = saturation / 255.0;
            double v = value / 255.0;
            int i = (int)Math.Floor(h) % 6;
            double f = h - Math.Floor(h);
            double p = v * (1 - s);
            double q = v * (1 - f * s);
            double t = v * (1 - (1 - f) * s);
            double r = 0, g = 0, b = 0;
            switch (i)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                case 5: r = v; g = p; b = q; break;
            }
            return Color.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
        }

        private void SaveBtn_Click(object sender, EventArgs e)
        {
            string[] prizes = ParsePrizes(prizeTextBox.Text);
            if (prizes.Length < 2)
            {
                MessageBox.Show(WheelForm.T("至少需要2个奖项！"), WheelForm.T("提示"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Color[] colors = GenerateColors(prizes.Length);
            parentData.Children[sectorIndex] = new WheelData(prizes, colors);
            RefreshChildPanel();
            MessageBox.Show(WheelForm.T("已保存 ") + prizes.Length + WheelForm.T(" 个奖项！"), WheelForm.T("成功"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ClearBtn_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(WheelForm.T("确定清除此扇区的子转盘？"), WheelForm.T("确认"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                parentData.Children[sectorIndex] = null;
                prizeTextBox.Text = "";
                RefreshChildPanel();
            }
        }

        private void SampleBtn_Click(object sender, EventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = WheelForm.T("文本文件|*.txt");
            dialog.FileName = WheelForm.T("子转盘配置示例.txt");
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                string sample = WheelForm.T("选项A 选项B 选项C 选项D");
                File.WriteAllText(dialog.FileName, sample);
                MessageBox.Show(WheelForm.T("示例已导出！"), WheelForm.T("导出成功"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string[] defaultPrizes = new string[] { "奖励一", "奖励二", "奖励三", "奖励四", "奖励五", "奖励六" };
            Color[] defaultColors = new Color[]
            {
                Color.FromArgb(0xFF, 0x6B, 0x6B), Color.FromArgb(0x4E, 0xCD, 0xC4), Color.FromArgb(0x45, 0xB7, 0xD1),
                Color.FromArgb(0x96, 0xCE, 0xB4), Color.FromArgb(0xFF, 0xEA, 0xA7), Color.FromArgb(0xDD, 0xA0, 0xDD)
            };
            WheelData data = new WheelData(defaultPrizes, defaultColors);

            WheelForm mainForm = new WheelForm(data, "幸运转盘 (父转盘)");
            mainForm.LoadConfig();
            Application.Run(mainForm);
        }
    }
}