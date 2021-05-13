using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.IO;

// Funky
using System.Runtime.InteropServices;

// Misc Libs
using WiimoteLib; // Interacting with the Wiimote + Peripherals
using WindowsInput; // Sending keyboard input (not analog, sadly.)
using WindowsInput.Native;
using NotITG.External; // Interacting with NotITG
using Ini;

// Blue teeth
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;

namespace NotInTheWii
{
    public partial class Form1 : Form
    {
        public Wiimote mote;
        public InputSimulator input;
        public float DefaultThres = 0.7f;

        public enum Orientation
        {
            LEFT,
            BACK,
            FRONT,
            RIGHT
        }

        #region Config
        public IniFile ini = new IniFile("config.ini");
        public bool SaveConfig()
        {
            if (!areThresholdsValid())
            {
                MessageBox.Show("One or more threshold values are invalid", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            try
            {
                ini.Write("MaxKilogram", kg_textbox.Text, "Global");

                ini.Write("LeftKey", left_Combo.SelectedIndex.ToString(), "Global");
                ini.Write("DownKey", down_Combo.SelectedIndex.ToString(), "Global");
                ini.Write("UpKey", up_Combo.SelectedIndex.ToString(), "Global");
                ini.Write("RightKey", right_Combo.SelectedIndex.ToString(), "Global");

                ini.Write("LeftThreshold", left_Threshold.Text.ToString(), "Global");
                ini.Write("DownThreshold", down_Threshold.Text.ToString(), "Global");
                ini.Write("UpThreshold", up_Threshold.Text.ToString(), "Global");
                ini.Write("RightThreshold", right_Threshold.Text.ToString(), "Global");

                ini.Write("Orientation", orientation_Combo.SelectedIndex.ToString(), "Global");
            }
            catch (Exception) { return false;  }

            return true;
        }
        public void LoadConfig()
        {
            try
            {
                kg_textbox.Text = ini.Read("MaxKilogram", "Global");
                left_Combo.SelectedIndex = int.Parse(ini.Read("LeftKey", "Global"));
                left_Threshold.Text = ini.Read("LeftThreshold", "Global");
                down_Combo.SelectedIndex = int.Parse(ini.Read("DownKey", "Global"));
                down_Threshold.Text = ini.Read("DownThreshold", "Global");
                up_Combo.SelectedIndex = int.Parse(ini.Read("UpKey", "Global"));
                up_Threshold.Text = ini.Read("UpThreshold", "Global");
                right_Combo.SelectedIndex = int.Parse(ini.Read("RightKey", "Global"));
                right_Threshold.Text = ini.Read("RightThreshold", "Global");
                orientation_Combo.SelectedIndex = int.Parse(ini.Read("Orientation", "Global"));
            }
            catch( Exception )
            {
                MessageBox.Show("config.ini is Fucked, resetting", "Ayo the pizza here");
                if (File.Exists(@"config.ini")) File.Delete(@"config.ini");
                InitConfig();
            }
        }
        public void InitConfig()
        {
            if (File.Exists(@"config.ini") && File.ReadAllText(@"config.ini").Length > 0) return;

            File.Create(@"config.ini").Close();

            ini.Write("MaxKilogram", "50", "Global");

            ini.Write("LeftKey", "0", "Global");
            ini.Write("DownKey", "0", "Global");
            ini.Write("UpKey", "0", "Global");
            ini.Write("RightKey", "0", "Global");
            ini.Write("LeftThreshold", "0.5", "Global");
            ini.Write("DownThreshold", "0.5", "Global");
            ini.Write("UpThreshold", "0.5", "Global");
            ini.Write("RightThreshold", "0.5", "Global");

            ini.Write("Orientation", "1", "Global");
        }
        #endregion

        public Form1()
        {
            InitializeComponent();

            InitConfig();

            orientation_Combo.DataSource = new Orientation[] {
                Orientation.LEFT,
                Orientation.BACK,
                Orientation.FRONT,
                Orientation.RIGHT
            };

            // valid inputs
            VirtualKeyCode[] keys =
            {
                VirtualKeyCode.VK_A,
                VirtualKeyCode.VK_B,
                VirtualKeyCode.VK_C,
                VirtualKeyCode.VK_D,
                VirtualKeyCode.VK_E,
                VirtualKeyCode.VK_F,
                VirtualKeyCode.VK_G,
                VirtualKeyCode.VK_H,
                VirtualKeyCode.VK_I,
                VirtualKeyCode.VK_J,
                VirtualKeyCode.VK_K,
                VirtualKeyCode.VK_L,
                VirtualKeyCode.VK_M,
                VirtualKeyCode.VK_N,
                VirtualKeyCode.VK_O,
                VirtualKeyCode.VK_P,
                VirtualKeyCode.VK_Q,
                VirtualKeyCode.VK_R,
                VirtualKeyCode.VK_S,
                VirtualKeyCode.VK_T,
                VirtualKeyCode.VK_U,
                VirtualKeyCode.VK_V,
                VirtualKeyCode.VK_W,
                VirtualKeyCode.VK_X,
                VirtualKeyCode.VK_Y,
                VirtualKeyCode.VK_Z,
                VirtualKeyCode.LEFT,
                VirtualKeyCode.DOWN,
                VirtualKeyCode.UP,
                VirtualKeyCode.RIGHT,
            };
            left_Combo.DataSource = keys.Clone();
            down_Combo.DataSource = keys.Clone();
            up_Combo.DataSource = keys.Clone();
            right_Combo.DataSource = keys.Clone();

            // sanitize textbox
            kg_textbox.TextChanged += KGTextbox;
            
            left_Threshold.LostFocus += ThresTextbox; left_Threshold.TextChanged += ThresTextboxCheck;
            down_Threshold.LostFocus += ThresTextbox; down_Threshold.TextChanged += ThresTextboxCheck;
            up_Threshold.LostFocus += ThresTextbox; up_Threshold.TextChanged += ThresTextboxCheck;
            right_Threshold.LostFocus += ThresTextbox; right_Threshold.TextChanged += ThresTextboxCheck;

            LoadConfig();

            inputGlobal_checkBox.Checked = true;

            // Wierd
            Task.Run(async () => await Task.Run(() => ScanNotITG()));
            Timer notITG_t = new Timer();
            notITG_t.Enabled = true;
            notITG_t.Interval = 2;
            notITG_t.Tick += NotITGTick;
            notITG_t.Start();

            input = new InputSimulator();

            Bepis(); // Cross-thread operation not valid: Control '' accessed from a thread other than the thread it was created on.
        }

        #region WPF Text Handling
        private void KGTextbox(object sender, System.EventArgs e)
        {
            base.OnTextChanged(e); // dunno what this does
            var textbox = (sender as TextBox);

            textbox.Text = Regex.Replace(textbox.Text, "[^0-9]", "");
            if (textbox.Text == "")
                textbox.Text = "50";
            textbox.Text = int.Parse(textbox.Text).ToString();
        }

        private void ThresTextbox(object sender, System.EventArgs e)
        {
            base.OnTextChanged(e); // dunno what this does
            var textbox = (sender as TextBox);

            float f;
            if (float.TryParse(textbox.Text, out f))
            {
                f = Math.Max(0.0f, Math.Min(1.0f, f));
                textbox.Text = f.ToString();
                return;
            }
            textbox.Text = DefaultThres.ToString();
        }

        Dictionary<TextBox, bool> thresholdValid = new Dictionary<TextBox, bool>();
        public bool areThresholdsValid()
        {
            return thresholdValid.Values.All(x => x == true);
        }
        private void ThresTextboxCheck(object sender, System.EventArgs e)
        {
            base.OnTextChanged(e); // dunno what this does
            var textbox = (sender as TextBox);

            if (!thresholdValid.ContainsKey(textbox))
                thresholdValid.Add(textbox, true);

            float f;
            if (float.TryParse(textbox.Text, out f))
            {
                textbox.ForeColor = System.Drawing.Color.Green;
                thresholdValid[textbox] = true;
                return;
            }
            textbox.ForeColor = System.Drawing.Color.Red;
            thresholdValid[textbox] = false;
        }
        #endregion

        public static string statusText = "Not Connected";
        public static string deviceText = "Device not connected";
        public async void Bepis()
        {
            while (true)
            {
                this.label_notITGStatus.Text = statusText;
                this.label_deviceStatus.Text = deviceText;
                await Task.Delay(200);
            }
        }

        public NotITGHandler NITG;
        public bool IsConstantMelody = false;
        public async void ScanNotITG()
        {
            NITG = new NotITGHandler(!this.option_unknownFilename.Checked); // Only care if you know the filename while scanning.
            NITG.OnExit += (s, e) => ScanNotITG();

            while ( true )
            {
                //Console.Clear();
                //Console.WriteLine("Scanning for a NotITG instance...");
                if ( NITG.TryScan() )
                {
                    // poggerU
                    // IsConstantMelody = GetActiveWindowTitle( NITG.Process.MainWindowHandle ).ToLower().Contains("constant melody x");
                    statusText = "Connected!";

                    break;
                }

                statusText = "Not Found";
                await Task.Delay(1000 * 2); // Wait 2 seconds before retrying
            }
        }

        public void NotITGTick(object sender, EventArgs e)
        {
            if (NITG == null) return;

            if( NITG.HasNotITG )
            {

                //

            }
        }

        #region Pairing
        private bool TryPair()
        {
            deviceText = "Not Connected";
            try
            {
                WiimoteCollection wiiCollection = new WiimoteCollection();
                wiiCollection.FindAllWiimotes();

                foreach (Wiimote m in wiiCollection)
                {
                    mote = m;
                    mote.WiimoteChanged += Mote_WiimoteChanged;
                    mote.WiimoteExtensionChanged += Mote_WiimoteExtensionChanged;

                    mote.Connect();

                    if (mote.WiimoteState.ExtensionType == ExtensionType.BalanceBoard)
                    {
                        label_boardStatus.Text = "Detected";
                    }
                    else
                    {
                        deviceText = "Wiimote connected";
                        mote.SetReportType(InputReport.IRAccel, false);
                        mote.WiimoteState.IRState.Mode = IRMode.Full;
                        Task.Run(async () =>
                       {
                           mote.SetRumble(true);
                           await Task.Delay(500);
                           mote.SetRumble(false);
                       });
                    }

                    mote.SetLEDs(true, false, false, false);
                    deviceText = "Connected";

                    button1.Text = "Connected!";

                    return true;
                }
            }
            catch (Exception)
            {
                
            }

            return false;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;
            deviceText = "Device not connected";

            Console.WriteLine("Attempting luck pair");

            // By the chance that the Wiimote is paired already
            if (TryPair()) return;

            // Do da bluetooth thinmgy
            var bluetooth = new BluetoothClient();

            Console.WriteLine("Removing existing Nintendo devices");

            // WBB - Remove existing bluetooth devices
            var existingDevice = bluetooth.DiscoverDevices(255, false, true, false);
            foreach (var device in existingDevice)
            {
                Console.WriteLine("Found " + device.DeviceName + "...");
                if (!device.DeviceName.Contains("Nintendo")) continue;

                BluetoothSecurity.RemoveDevice(device.DeviceAddress);
                device.SetServiceState(BluetoothService.HumanInterfaceDevice, false);
            }

            Console.WriteLine("Connecting devices...");

            var discoveredDevice = bluetooth.DiscoverDevices(255, false, false, true);
            foreach( var device in discoveredDevice )
            {
                Console.WriteLine("Found " + device.DeviceName);
                if (!device.DeviceName.Contains("Nintendo")) continue;

                if(checkbox_Pair.Checked)
                {

                    new BluetoothWin32Authentication(device.DeviceAddress, AddressToWiiPin(BluetoothRadio.PrimaryRadio.LocalAddress.ToString()));
                    BluetoothSecurity.PairRequest(device.DeviceAddress, null);

                }

                device.SetServiceState(BluetoothService.HumanInterfaceDevice, true);

                break;
            }

            Console.WriteLine("Attempting to now pair a device");

            if (TryPair()) return;

            Console.WriteLine("Failed!");

            button1.Enabled = true;
        }

        // From Will Balance Walker
        private string AddressToWiiPin(string bluetoothAddress)
        {
            if (bluetoothAddress.Length != 12) return null;

            var bluetoothPin = "";
            for (int i = bluetoothAddress.Length - 2; i >= 0; i -= 2)
            {
                bluetoothPin += Convert.ToInt32(bluetoothAddress.Substring(i, 2), 16);
            }
            return bluetoothPin;
        }

        #endregion

        #region Input Handling

        private void Mote_WiimoteExtensionChanged(object sender, WiimoteExtensionChangedEventArgs e)
        {
            ExtensionType ext = e.ExtensionType;
            if( ext == ExtensionType.BalanceBoard )
            {
                label_boardStatus.Text = "Connected";
                return;
            }
            label_boardStatus.Text = "Not Connected";
        }

        bool[] isHeld = { false, false, false, false };

        public float _maxValue = 0.0f;
        public bool isPressed( float kg, float threshold )
        {
            float maxkg = float.Parse(kg_textbox.Text);
            //float maxkg = _maxValue;
            return (kg / maxkg) > threshold;
        }

        public float _lowestValue = 0.0f;
        public float[] getValues( WiimoteState state )
        {
            float[] v = { 0.0f, 0.0f, 0.0f, 0.0f };
            if (state.BalanceBoardState.WeightKg < 0) return v;

            float tl = state.BalanceBoardState.SensorValuesKg.TopLeft;
            float tr = state.BalanceBoardState.SensorValuesKg.TopRight;
            float bl = state.BalanceBoardState.SensorValuesKg.BottomLeft;
            float br = state.BalanceBoardState.SensorValuesKg.BottomRight;

            if (tl < _lowestValue) _lowestValue = tl;
            if (tr < _lowestValue) _lowestValue = tr;
            if (bl < _lowestValue) _lowestValue = bl;
            if (br < _lowestValue) _lowestValue = br;

            if (tl > _maxValue) _maxValue = tl;
            if (tr > _maxValue) _maxValue = tr;
            if (bl > _maxValue) _maxValue = bl;
            if (br > _maxValue) _maxValue = br;

            tl -= _lowestValue;
            tr -= _lowestValue;
            bl -= _lowestValue;
            br -= _lowestValue;

            float ratio = 100.0f / (tl + tr + bl + br);

            v[0] = ((tl + bl) / 2.0f) * ratio;
            v[1] = ((bl + br) / 2.0f) * ratio;
            v[2] = ((tl + tr) / 2.0f) * ratio;
            v[3] = ((tr + br) / 2.0f) * ratio;

            return v;
        }
        private void Mote_WiimoteChanged(object sender, WiimoteChangedEventArgs e)
        {
            WiimoteState state = e.WiimoteState;

            if( (NITG != null && IsNotITGFocused()) || (inputGlobal_checkBox.Checked) )
            {
                if( areThresholdsValid() )
                {
                    float[] ldur = getValues( state );

                    float _l = ldur[0];
                    float _d = ldur[1];
                    float _u = ldur[2];
                    float _r = ldur[3];

                    // penis
                    switch (orientation_Combo.SelectedIndex)
                    {
                        // Left
                        case 0:
                            {
                                ldur[2] = _l;
                                ldur[0] = _d;
                                ldur[3] = _u;
                                ldur[1] = _r;
                            }
                            break;
                        // Down
                        case 1:
                            {
                                // none
                            }
                            break;
                        // Up
                        case 2:
                            {
                                ldur[3] = _l;
                                ldur[2] = _d;
                                ldur[1] = _u;
                                ldur[0] = _r;
                            }
                            break;
                        // Right
                        case 3:
                            {
                                ldur[1] = _l;
                                ldur[3] = _d;
                                ldur[0] = _u;
                                ldur[2] = _r;
                            }
                            break;
                    }

                    bool[] isPress =
                    {
                        isPressed( ldur[0], float.Parse(left_Threshold.Text) ),
                        isPressed( ldur[1], float.Parse(down_Threshold.Text) ),
                        isPressed( ldur[2], float.Parse(up_Threshold.Text) ),
                        isPressed( ldur[3], float.Parse(right_Threshold.Text) ),
                    };
                    object[] keys =
                    {
                        left_Combo.SelectedItem,
                        down_Combo.SelectedItem,
                        up_Combo.SelectedItem,
                        right_Combo.SelectedItem
                    };

                    for( int i = 0; i < 4; i++ )
                    {
                        if (!(keys[i] is VirtualKeyCode)) continue;

                        if( isPress[i] )
                        {
                            if( !isHeld[i] )
                            {
                                input.Keyboard.KeyDown( (VirtualKeyCode)keys[i] );
                                isHeld[i] = true;
                            }
                        } else
                        {
                            if( isHeld[i] )
                            {
                                input.Keyboard.KeyUp( (VirtualKeyCode)keys[i] );
                                isHeld[i] = false;
                            }
                        }
                    }
                }
            }

            if( NITG != null && send_ExternalData.Checked )
            {
                NITG.SetExternal(0, (int)state.BalanceBoardState.SensorValuesKg.TopLeft);
                NITG.SetExternal(1, (int)state.BalanceBoardState.SensorValuesKg.TopRight);
                NITG.SetExternal(2, (int)state.BalanceBoardState.SensorValuesKg.BottomLeft);
                NITG.SetExternal(3, (int)state.BalanceBoardState.SensorValuesKg.BottomRight);
                NITG.SetExternal(4, (int)state.BalanceBoardState.WeightKg);
            }
        }

        #endregion

        //

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if( mote != null )
            {
                mote.SetLEDs(false, false, false, false);
                mote.Disconnect();
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if( SaveConfig() )
            {
                MessageBox.Show("Saved configuration!", "Poggers");
            } else
            {
                
            }
        }

        //

        #region Misc: IsFocused
        public bool IsNotITGFocused()
        {
            try {
                return NITG.Process.MainWindowHandle == GetForegroundWindow();
            }
            catch( Exception ) {
                return false;
            }
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int charCount);

        public static string GetActiveWindowTitle(IntPtr? custom = null)
        {
            StringBuilder Buff = new StringBuilder(128);
            IntPtr handle = custom == null ? GetForegroundWindow() : (IntPtr)custom;
            if (GetWindowText(handle, Buff, 128) > 0)
                return Buff.ToString();
            return "";
        }

        #endregion

    }
}
