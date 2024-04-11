using rF2MonitorStriped.rFactor2Data;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using static rF2MonitorStriped.rFactor2Constants;

namespace rF2MonitorStriped
{
    public partial class MainForm : Form
    {
        // Constants
        private const int CONNECTION_RETRY_INTERVAL_MS = 1000;
        private const int DISCONNECTED_CHECK_INTERVAL_MS = 15000;
        private const int REFRESH_MS = 20;

        // Booleans
        public static bool useStockCarRulesPlugin = false;
        private bool connected = false;

        // Timers
        private System.Windows.Forms.Timer connectTimer = new System.Windows.Forms.Timer();
        private System.Windows.Forms.Timer disconnectTimer = new System.Windows.Forms.Timer();

        // Read buffers:
        MappedBuffer<rF2Telemetry> telemetryBuffer = new MappedBuffer<rF2Telemetry>(rFactor2Constants.MM_TELEMETRY_FILE_NAME, true /*partial*/, true /*skipUnchanged*/);
        MappedBuffer<rF2Scoring> scoringBuffer = new MappedBuffer<rF2Scoring>(rFactor2Constants.MM_SCORING_FILE_NAME, true /*partial*/, true /*skipUnchanged*/);
        MappedBuffer<rF2Rules> rulesBuffer = new MappedBuffer<rF2Rules>(rFactor2Constants.MM_RULES_FILE_NAME, true /*partial*/, true /*skipUnchanged*/);
        MappedBuffer<rF2ForceFeedback> forceFeedbackBuffer = new MappedBuffer<rF2ForceFeedback>(rFactor2Constants.MM_FORCE_FEEDBACK_FILE_NAME, false /*partial*/, false /*skipUnchanged*/);
        MappedBuffer<rF2Graphics> graphicsBuffer = new MappedBuffer<rF2Graphics>(rFactor2Constants.MM_GRAPHICS_FILE_NAME, false /*partial*/, false /*skipUnchanged*/);
        MappedBuffer<rF2PitInfo> pitInfoBuffer = new MappedBuffer<rF2PitInfo>(rFactor2Constants.MM_PITINFO_FILE_NAME, false /*partial*/, true /*skipUnchanged*/);
        MappedBuffer<rF2Weather> weatherBuffer = new MappedBuffer<rF2Weather>(rFactor2Constants.MM_WEATHER_FILE_NAME, false /*partial*/, true /*skipUnchanged*/);
        MappedBuffer<rF2Extended> extendedBuffer = new MappedBuffer<rF2Extended>(rFactor2Constants.MM_EXTENDED_FILE_NAME, false /*partial*/, true /*skipUnchanged*/);

        // Write buffers:
        MappedBuffer<rF2HWControl> hwControlBuffer = new MappedBuffer<rF2HWControl>(rFactor2Constants.MM_HWCONTROL_FILE_NAME);
        MappedBuffer<rF2WeatherControl> weatherControlBuffer = new MappedBuffer<rF2WeatherControl>(rFactor2Constants.MM_WEATHER_CONTROL_FILE_NAME);
        MappedBuffer<rF2RulesControl> rulesControlBuffer = new MappedBuffer<rF2RulesControl>(rFactor2Constants.MM_RULES_CONTROL_FILE_NAME);
        MappedBuffer<rF2PluginControl> pluginControlBuffer = new MappedBuffer<rF2PluginControl>(rFactor2Constants.MM_PLUGIN_CONTROL_FILE_NAME);

        // Marshalled views:
        rF2Telemetry telemetry;
        rF2Scoring scoring;
        rF2Rules rules;
        rF2ForceFeedback forceFeedback;
        rF2Graphics graphics;
        rF2PitInfo pitInfo;
        rF2Weather weather;
        rF2Extended extended;

        // Marashalled output views:
        rF2HWControl hwControl;
        rF2WeatherControl weatherControl;
        rF2RulesControl rulesControl;
        rF2PluginControl pluginControl;

        [StructLayout(LayoutKind.Sequential)]
        public struct NativeMessage
        {
            public IntPtr Handle;
            public uint Message;
            public IntPtr WParameter;
            public IntPtr LParameter;
            public uint Time;
            public Point Location;
        }

        [DllImport("user32.dll")]
        public static extern int PeekMessage(out NativeMessage message, IntPtr window, uint filterMin, uint filterMax, uint remove);

        public MainForm()
        {
            this.InitializeComponent();

            this.connectTimer.Interval = MainForm.CONNECTION_RETRY_INTERVAL_MS;
            this.connectTimer.Tick += this.ConnectTimer_Tick;
            this.disconnectTimer.Interval = MainForm.DISCONNECTED_CHECK_INTERVAL_MS;
            this.disconnectTimer.Tick += this.DisconnectTimer_Tick;
            this.connectTimer.Start();
            this.disconnectTimer.Start();

            Application.Idle += this.HandleApplicationIdle;
        }

        bool IsApplicationIdle()
        {
            NativeMessage result;
            return PeekMessage(out result, IntPtr.Zero, (uint)0, (uint)0, (uint)0) == 0;
        }

        void HandleApplicationIdle(object sender, EventArgs e)
        {
            while (this.IsApplicationIdle())
            {
                try
                {
                    this.MainUpdate();
                    Thread.Sleep(REFRESH_MS);
                }
                catch (Exception)
                {
                    this.Disconnect();
                }
            }
        }

        void MainUpdate()
        {
            if (!this.connected)
                return;

            try
            {
                extendedBuffer.GetMappedData(ref extended);
                scoringBuffer.GetMappedData(ref scoring);
                telemetryBuffer.GetMappedData(ref telemetry);
                rulesBuffer.GetMappedData(ref rules);
                forceFeedbackBuffer.GetMappedDataUnsynchronized(ref forceFeedback);
                graphicsBuffer.GetMappedDataUnsynchronized(ref graphics);
                pitInfoBuffer.GetMappedData(ref pitInfo);
                weatherBuffer.GetMappedData(ref weather);

                // TODO - YOUR CODE HERE!!!
            }
            catch (Exception)
            {
                this.Disconnect();
            }
        }

        private void ConnectTimer_Tick(object sender, EventArgs e)
        {
            if (!this.connected)
            {
                try
                {
                    this.extendedBuffer.Connect();
                    this.telemetryBuffer.Connect();
                    this.scoringBuffer.Connect();
                    this.rulesBuffer.Connect();
                    this.forceFeedbackBuffer.Connect();
                    this.graphicsBuffer.Connect();
                    this.pitInfoBuffer.Connect();
                    this.weatherBuffer.Connect();
                    this.hwControlBuffer.Connect();
                    this.hwControlBuffer.GetMappedData(ref this.hwControl);
                    this.hwControl.mLayoutVersion = rFactor2Constants.MM_HWCONTROL_LAYOUT_VERSION;
                    this.weatherControlBuffer.Connect();
                    this.weatherControlBuffer.GetMappedData(ref this.weatherControl);
                    this.weatherControl.mLayoutVersion = rFactor2Constants.MM_WEATHER_CONTROL_LAYOUT_VERSION;
                    this.rulesControlBuffer.Connect();
                    this.rulesControlBuffer.GetMappedData(ref this.rulesControl);
                    this.rulesControl.mLayoutVersion = rFactor2Constants.MM_RULES_CONTROL_LAYOUT_VERSION;
                    this.pluginControlBuffer.Connect();
                    this.pluginControlBuffer.GetMappedData(ref this.pluginControl);
                    this.pluginControl.mLayoutVersion = rFactor2Constants.MM_PLUGIN_CONTROL_LAYOUT_VERSION;
                    this.pluginControl.mRequestEnableBuffersMask = /*(int)SubscribedBuffer.Scoring | */(int)SubscribedBuffer.Telemetry | (int)SubscribedBuffer.Rules | (int)SubscribedBuffer.ForceFeedback | (int)SubscribedBuffer.Graphics | (int)SubscribedBuffer.Weather | (int)SubscribedBuffer.PitInfo;
                    this.pluginControl.mRequestHWControlInput = 1;
                    this.pluginControl.mRequestRulesControlInput = 1;
                    this.pluginControl.mRequestWeatherControlInput = 1;
                    this.pluginControl.mVersionUpdateBegin = this.pluginControl.mVersionUpdateEnd = this.pluginControl.mVersionUpdateBegin + 1;
                    this.pluginControlBuffer.PutMappedData(ref this.pluginControl);

                    this.connected = true;
                }
                catch (Exception)
                {
                    this.Disconnect();
                }
            }
        }

        private void DisconnectTimer_Tick(object sender, EventArgs e)
        {
            if (!this.connected)
                return;

            try
            {
                Process[] processes = Process.GetProcessesByName(RFACTOR2_PROCESS_NAME);
                if (processes.Length == 0)
                    Disconnect();
            }
            catch (Exception)
            {
                Disconnect();
            }
        }

        private void Disconnect()
        {
            this.extendedBuffer.Disconnect();
            this.scoringBuffer.Disconnect();
            this.rulesBuffer.Disconnect();
            this.telemetryBuffer.Disconnect();
            this.forceFeedbackBuffer.Disconnect();
            this.pitInfoBuffer.Disconnect();
            this.weatherBuffer.Disconnect();
            this.graphicsBuffer.Disconnect();
            this.hwControlBuffer.Disconnect();
            this.weatherControlBuffer.Disconnect();
            this.rulesControlBuffer.Disconnect();
            this.pluginControlBuffer.Disconnect();

            this.connected = false;
        }

        private static string GetStringFromBytes(byte[] bytes)
        {
            if (bytes == null)
                return "";

            var nullIdx = Array.IndexOf(bytes, (byte)0);

            return nullIdx >= 0
              ? Encoding.Default.GetString(bytes, 0, nullIdx)
              : Encoding.Default.GetString(bytes);
        }

        public static rF2VehicleScoring GetPlayerVehScoring(rF2Scoring scoring)
        {
            rF2VehicleScoring playerVehScoring = new rF2VehicleScoring();
            for (int i = 0; i < scoring.mScoringInfo.mNumVehicles; ++i)
            {
                rF2VehicleScoring vehScoring = scoring.mVehicles[i];
                if (vehScoring.mIsPlayer == 1)
                {
                    playerVehScoring = vehScoring;
                    break;
                }
            }

            return playerVehScoring;
        }

        public static rF2VehicleTelemetry GetPlayerVehTelemetry(rF2Scoring scoring, rF2Telemetry telemetry)
        {
            rF2VehicleTelemetry playerVehTelemetry = new rF2VehicleTelemetry();
            for (int i = 0; i < scoring.mScoringInfo.mNumVehicles; ++i)
            {
                rF2VehicleScoring vehScoring = scoring.mVehicles[i];
                if (vehScoring.mIsPlayer == 1)
                {
                    playerVehTelemetry = telemetry.mVehicles[i];
                    break;
                }
            }

            return playerVehTelemetry;
        }

        #region Moving forms
        private Point? mouseOffset = null;

        private void MainForm_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                mouseOffset = new Point(-e.X, -e.Y);
            }
        }

        private void MainForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (mouseOffset.HasValue && e.Button == MouseButtons.Left)
            {
                Point newLocation = this.Location;
                newLocation.X += e.X + mouseOffset.Value.X;
                newLocation.Y += e.Y + mouseOffset.Value.Y;
                this.Location = newLocation;
            }
        }

        private void MainForm_MouseUp(object sender, MouseEventArgs e)
        {
            mouseOffset = null;
        }
        #endregion
    }
}
