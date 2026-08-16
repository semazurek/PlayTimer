using PT2.Properties;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PT2
{
    public class MainForm : Form
    {
        public static readonly string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PlayTimer");
        public static readonly string timeFilePath = Path.Combine(AppDataFolder, "remaining_t.txt");

        private CheckBox chkGpuTrigger;
        private ComboBox cmbGpuThreshold;
        private CheckBox chkCpuTrigger;
        private ComboBox cmbCpuThreshold;
        private CheckBox chkProcessTrigger;
        private TextBox txtProcessList;
        private ComboBox cmbHoursAllowed;
        private Button btnStart, btnStop, btnSaveList;
        private Label lblTimeRemaining;
        private NotifyIcon notifyIcon1;

        private Timer monitoringTimer;
        private int timeRemainingSeconds = 0;
        private bool isRunning = false;
        private bool isUnlocking = false;
        private GpuMonitor gpuMonitor;
        private PerformanceCounter cpuCounter;

        private Panel titleBar;
        private Label lblWindowTitle;
        private Button btnMinimize;
        private Button btnClose;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(
            IntPtr hWnd,
            int Msg,
            IntPtr wParam,
            IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        public MainForm(string[] args)
        {
            InitializeModernUI();
            LoadData();
            

            this.MaximizeBox = false;
            this.MinimizeBox = false;

            notifyIcon1 = new NotifyIcon();
            notifyIcon1.Icon = Resources.PTIcon;
            notifyIcon1.Text = "PlayTimer";
            //notifyIcon1.Visible = true;
            notifyIcon1.MouseDoubleClick += NotifyIcon1_MouseDoubleClick;

            gpuMonitor = new GpuMonitor();

            try
            {
                cpuCounter = new PerformanceCounter(
                    "Processor",
                    "% Processor Time",
                    "_Total");

                cpuCounter.NextValue();
            }
            catch
            {
                cpuCounter = null;
            }

            monitoringTimer = new Timer();
            monitoringTimer.Interval = 5000;
            monitoringTimer.Tick += MonitoringTimer_Tick;

            this.Shown += (s, e) =>
            {
                notifyIcon1.Visible = true;

                if (args.Contains("/sillent") || args.Contains("-sillent") || args.Contains("sillent"))
                {
                    int savedTime = LoadTimeFromFile();
                    if (savedTime >= 0)
                    {
                        timeRemainingSeconds = savedTime;
                    }
                    else
                    {
                        int hours = int.Parse(cmbHoursAllowed.SelectedItem.ToString());
                        timeRemainingSeconds = hours * 3600;
                    }

                    if (timeRemainingSeconds <= 0)
                    {
                        timeRemainingSeconds = 0;
                        UpdateTimerLabel();
                        EnforceTimeLimit();
                        return;
                    }

                    isRunning = true;

                    btnStart.Enabled = false;
                    btnStop.Enabled = true;
                    btnSaveList.Enabled = false;
                    txtProcessList.Enabled = false;

                    chkGpuTrigger.Enabled = false;
                    cmbGpuThreshold.Enabled = false;
                    chkCpuTrigger.Enabled = false;
                    cmbCpuThreshold.Enabled = false;
                    chkProcessTrigger.Enabled = false;

                    monitoringTimer.Start();
                    UpdateTimerLabel();

                    this.Hide();
                    this.ShowInTaskbar = false;
                }
            };
        }
        
        private void NotifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            this.BringToFront();
            this.Activate();
        }

        private void InitializeModernUI()
        {
            this.Text = "PlayTimer";
            this.Size = new Size(520, 580);
            this.BackColor = Color.FromArgb(18, 18, 18);
            this.ForeColor = Color.White;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormClosing += MainForm_FormClosing;
            this.Icon = Resources.PTIcon;

            titleBar = new Panel()
            {
                Dock = DockStyle.Top,
                Height = 42,
                BackColor = Color.FromArgb(20, 20, 23)
            };

            titleBar.MouseDown += TitleBar_MouseDown;

            lblWindowTitle = new Label()
            {
                Text = "PlayTimer",
                Location = new Point(15, 0),
                Size = new Size(300, 42),
                ForeColor = Color.FromArgb(235, 235, 240),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            lblWindowTitle.MouseDown += TitleBar_MouseDown;

            btnMinimize = new Button()
            {
                Text = "—",
                Dock = DockStyle.Right,
                Width = 46,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(20, 20, 23),
                ForeColor = Color.FromArgb(210, 210, 215),
                Font = new Font("Segoe UI", 10F),
                Cursor = Cursors.Hand,
                TabStop = false
            };

            btnMinimize.FlatAppearance.BorderSize = 0;
            btnMinimize.FlatAppearance.MouseOverBackColor =
                Color.FromArgb(45, 45, 50);

            btnMinimize.Click += (s, e) =>
            {
                this.WindowState = FormWindowState.Minimized;
            };

            btnClose = new Button()
            {
                Text = "✕",
                Dock = DockStyle.Right,
                Width = 46,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(20, 20, 23),
                ForeColor = Color.FromArgb(210, 210, 215),
                Font = new Font("Segoe UI", 10F),
                Cursor = Cursors.Hand,
                TabStop = false
            };

            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor =
                Color.FromArgb(190, 45, 45);

            btnClose.Click += (s, e) =>
            {
                Close();
            };

            titleBar.Controls.Add(btnClose);
            //titleBar.Controls.Add(btnMinimize);
            titleBar.Controls.Add(lblWindowTitle);

            this.Controls.Add(titleBar);

            Panel mainPanel = new Panel()
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(24, 24, 27),
                Padding = new Padding(20)
            };

            Label lblTitle = new Label()
            {
                Text = "Time Limit Configuration",
                Location = new Point(20, 55),
                Size = new Size(400, 30),
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.FromArgb(240, 240, 245)
            };

            Panel trigPanel = new Panel()
            {
                Location = new Point(20, 95),
                Size = new Size(460, 115),
                BackColor = Color.FromArgb(32, 32, 36)
            };

            chkGpuTrigger = new CheckBox()
            {
                Text = "Respond to GPU usage",
                Location = new Point(15, 12),
                Size = new Size(225, 24),
                Checked = true,
                ForeColor = Color.FromArgb(220, 220, 225),
                Font = new Font("Segoe UI", 9.5F)
            };

            cmbGpuThreshold = new ComboBox()
            {
                Location = new Point(245, 10),
                Size = new Size(195, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 45, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F)
            };

            cmbGpuThreshold.Items.AddRange(
                new object[]
                {
                    "50%",
                    "60%",
                    "70%",
                    "80%",
                    "90%"
                });

            cmbGpuThreshold.SelectedIndex = 1;

            chkCpuTrigger = new CheckBox()
            {
                Text = "Respond to CPU usage",
                Location = new Point(15, 46),
                Size = new Size(225, 24),
                Checked = true,
                ForeColor = Color.FromArgb(220, 220, 225),
                Font = new Font("Segoe UI", 9.5F)
            };

            cmbCpuThreshold = new ComboBox()
            {
                Location = new Point(245, 44),
                Size = new Size(195, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 45, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F)
            };

            cmbCpuThreshold.Items.AddRange(
                new object[]
                {
                    "50%",
                    "60%",
                    "70%",
                    "80%",
                    "90%"
                });

            cmbCpuThreshold.SelectedIndex = 1;

            chkProcessTrigger = new CheckBox()
            {
                Text = "React to the detection of games from the process list (.exe)",
                Location = new Point(15, 80),
                Size = new Size(425, 24),
                Checked = true,
                ForeColor = Color.FromArgb(220, 220, 225),
                Font = new Font("Segoe UI", 9.5F)
            };

            trigPanel.Controls.Add(chkGpuTrigger);
            trigPanel.Controls.Add(cmbGpuThreshold);
            trigPanel.Controls.Add(chkCpuTrigger);
            trigPanel.Controls.Add(cmbCpuThreshold);
            trigPanel.Controls.Add(chkProcessTrigger);

            Label lblHours = new Label()
            {
                Text = "Allowed playtime:",
                Location = new Point(20, 225),
                Size = new Size(150, 25),
                ForeColor = Color.FromArgb(200, 200, 205),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };

            cmbHoursAllowed = new ComboBox()
            {
                Location = new Point(180, 222),
                Size = new Size(80, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 45, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F)
            };

            cmbHoursAllowed.Items.AddRange(
                new object[]
                {
                    "1",
                    "2",
                    "3",
                    "4",
                    "5",
                    "8",
                    "10",
                    "12",
                    "14",
                    "16",
                    "18",
                    "20"
                });

            cmbHoursAllowed.SelectedIndex = 1;

            Label lblList = new Label()
            {
                Text = "List of processes (.exe):",
                Location = new Point(20, 265),
                Size = new Size(200, 25),
                ForeColor = Color.FromArgb(200, 200, 205),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };

            txtProcessList = new TextBox()
            {
                Location = new Point(20, 295),
                Size = new Size(460, 115),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                AcceptsReturn = true,
                BackColor = Color.FromArgb(32, 32, 36),
                ForeColor = Color.FromArgb(230, 230, 235),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9.5F)
            };

            btnSaveList = new Button()
            {
                Text = "Save the list",
                Location = new Point(360, 420),
                Size = new Size(120, 35),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(63, 63, 70),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F),
                Cursor = Cursors.Hand
            };

            btnSaveList.FlatAppearance.BorderSize = 0;

            btnSaveList.Click += (s, e) =>
            {
                File.WriteAllText(
                    AppManager.GameListFile,
                    txtProcessList.Text);

                MessageBox.Show(
                    "List saved successfully!",
                    "PlayTimer",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            };

            btnStart = new Button()
            {
                Text = "START",
                Location = new Point(20, 475),
                Size = new Size(220, 45),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };

            btnStart.FlatAppearance.BorderSize = 0;
            btnStart.Click += BtnStart_Click;

            btnStop = new Button()
            {
                Text = "STOP",
                Location = new Point(260, 475),
                Size = new Size(220, 45),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(239, 68, 68),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Enabled = false,
                Cursor = Cursors.Hand
            };

            btnStop.FlatAppearance.BorderSize = 0;
            btnStop.Click += BtnStop_Click;

            lblTimeRemaining = new Label()
            {
                Text = "Time remaining: ---",
                Location = new Point(20, 535),
                Size = new Size(460, 30),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 211, 153),
                TextAlign = ContentAlignment.MiddleCenter
            };

            mainPanel.Controls.Add(lblTitle);
            mainPanel.Controls.Add(trigPanel);
            mainPanel.Controls.Add(lblHours);
            mainPanel.Controls.Add(cmbHoursAllowed);
            mainPanel.Controls.Add(lblList);
            mainPanel.Controls.Add(txtProcessList);
            mainPanel.Controls.Add(btnSaveList);
            mainPanel.Controls.Add(btnStart);
            mainPanel.Controls.Add(btnStop);
            mainPanel.Controls.Add(lblTimeRemaining);

            this.Controls.Add(mainPanel);

            titleBar.BringToFront();
        }

        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();

                SendMessage(
                    this.Handle,
                    WM_NCLBUTTONDOWN,
                    (IntPtr)HTCAPTION,
                    IntPtr.Zero);
            }
        }

        private void LoadData()
        {
            if (File.Exists(AppManager.GameListFile))
            {
                txtProcessList.Text =
                    File.ReadAllText(AppManager.GameListFile);
            }
            else
            {
                txtProcessList.Text =
                    "cs2.exe\r\n" +
                    "valorant.exe\r\n" +
                    "minecraft.exe\r\n" +
                    "GTA5.exe\r\n" +
                    "League of Legends.exe\r\n" +
                    "VALORANT-Win64-Shipping.exe\r\n" +
                    "FortniteClient-Win64-Shipping.exe\r\n" +
                    "Cyberpunk2077.exe\r\n" +
                    "witcher3.exe\r\n" +
                    "RobloxPlayerBeta.exe\r\n" +
                    "dota2.exe\r\n" +
                    "r5apex.exe\r\n" +
                    "cod.exe\r\n" +
                    "Wow.exe\r\n" +
                    "RDR2.exe\r\n" +
                    "TS4_x64.exe\r\n" +
                    "hl2.exe\r\n" +
                    "Terraria.exe\r\n" +
                    "SkyrimSE.exe\r\n" +
                    "EldenRing.exe\r\n" +
                    "csgo.exe\r\n" +
                    "Left4Dead2.exe\r\n" +
                    "Borderlands2.exe\r\n" +
                    "Fallout4.exe\r\n" +
                    "re4.exe\r\n" +
                    "BioShock.exe\r\n" +
                    "CivVI.exe\r\n" +
                    "Stardew Valley.exe\r\n" +
                    "Subnautica.exe\r\n" +
                    "DoomEternal.exe\r\n" +
                    "Hades.exe\r\n" +
                    "MonsterHunterWorld.exe\r\n" +
                    "Overwatch.exe\r\n" +
                    "Destiny2.exe\r\n" +
                    "DeadByDaylight-Win64-Shipping.exe\r\n" +
                    "RainbowTwo.exe\r\n" +
                    "SeaOfThieves.exe\r\n" +
                    "RustClient.exe\r\n" +
                    "ARK7.exe\r\n" +
                    "Phasmophobia.exe\r\n" +
                    "Cities.exe\r\n" +
                    "EuroTrucks2.exe\r\n" +
                    "SimCity.exe\r\n" +
                    "FarmingSimulator2022.exe\r\n" +
                    "Palworld-Win64-Shipping.exe\r\n" +
                    "Helldivers2.exe\r\n" +
                    "BaldursGate3.exe\r\n" +
                    "GenshinImpact.exe\r\n" +
                    "StarRail.exe\r\n" +
                    "wutheringwaves-win64-shipping.exe\r\n" +
                    "diablo4.exe\r\n" +
                    "HogwartsLegacy.exe\r\n" +
                    "Starfield.exe\r\n" +
                    "AssassinsCreedValhalla.exe\r\n" +
                    "FarCry6.exe\r\n" +
                    "WatchDogs2.exe\r\n" +
                    "Division2.exe\r\n" +
                    "GhostRecon.exe\r\n" +
                    "ForzaHorizon5.exe\r\n" +
                    "NeedForSpeed.exe\r\n" +
                    "F1_2024.exe\r\n" +
                    "FIFA23.exe\r\n" +
                    "FC24.exe\r\n" +
                    "FC25.exe\r\n" +
                    "NBA2K25.exe\r\n" +
                    "WWE2K24.exe\r\n" +
                    "Spiderman.exe\r\n" +
                    "GodofWar.exe\r\n" +
                    "HorizonZeroDawn.exe\r\n" +
                    "Uncharted.exe\r\n" +
                    "DaysGone.exe\r\n" +
                    "DetroitBecomeHuman.exe\r\n" +
                    "RedDeadRedemption.exe\r\n" +
                    "MafiaDefinitiveEdition.exe\r\n" +
                    "L.A.Noire.exe\r\n" +
                    "MaxPayne3.exe\r\n" +
                    "Bully.exe\r\n" +
                    "ViceCity.exe\r\n" +
                    "SanAndreas.exe\r\n" +
                    "GTA3.exe\r\n" +
                    "Among Us.exe\r\n" +
                    "FallGuys_client.exe\r\n" +
                    "RocketLeague.exe\r\n" +
                    "Brawlhalla.exe\r\n" +
                    "PAYDAY2_win32_release.exe\r\n" +
                    "Payday3.exe\r\n" +
                    "DyingLightGame.exe\r\n" +
                    "DeadIsland2-Win64-Shipping.exe\r\n" +
                    "SonsOfTheForest.exe\r\n" +
                    "TheForest.exe\r\n" +
                    "GreenHell.exe\r\n" +
                    "StrandedDeep.exe\r\n" +
                    "Raft.exe\r\n" +
                    "Rust.exe\r\n" +
                    "DayZ_x64.exe\r\n" +
                    "arma3battleye.exe\r\n" +
                    "Squad.exe\r\n" +
                    "Insurgency.exe\r\n" +
                    "HellLetLoose_x64.exe\r\n" +
                    "PostScriptum.exe\r\n" +
                    "Enlisted.exe\r\n" +
                    "WarThunder.exe\r\n" +
                    "WorldOfTanks.exe\r\n" +
                    "WorldOfWarships.exe\r\n" +
                    "Crossout.exe\r\n" +
                    "Warframe.x64.exe\r\n" +
                    "PathofExile.exe\r\n" +
                    "Grim Dawn.exe\r\n" +
                    "Torchlight2.exe\r\n" +
                    "LostArk.exe\r\n" +
                    "NewWorld.exe\r\n" +
                    "Albion-Online.exe\r\n" +
                    "ESO64.exe\r\n" +
                    "FFXIVBoot.exe\r\n" +
                    "SWTOR.exe\r\n" +
                    "GuildWars2.exe\r\n" +
                    "EVE.exe\r\n" +
                    "Runescape.exe\r\n" +
                    "Trove.exe\r\n" +
                    "MapleStory.exe\r\n" +
                    "SporeApp.exe\r\n" +
                    "Sims3.exe\r\n" +
                    "Sims2.exe\r\n" +
                    "PlanetCoaster.exe\r\n" +
                    "PlanetZoo.exe\r\n" +
                    "JurassicWorldEvolution2.exe\r\n" +
                    "RollerCoasterTycoon.exe\r\n" +
                    "ZooTycoon.exe\r\n" +
                    "AgeofEmpiresII.exe\r\n" +
                    "AoE4.exe\r\n" +
                    "StarCraftII.exe\r\n" +
                    "War3.exe\r\n" +
                    "CommandAndConquer.exe\r\n" +
                    "TotalWar.exe\r\n" +
                    "CrusaderKings3.exe\r\n" +
                    "HeartsOfIron4.exe\r\n" +
                    "EuropaUniversalis4.exe\r\n" +
                    "Stellaris.exe\r\n" +
                    "Victoria3.exe\r\n" +
                    "Anno1800.exe\r\n" +
                    "Tropico6.exe\r\n" +
                    "Frostpunk.exe\r\n" +
                    "RimWorldWin64.exe\r\n" +
                    "Factorio.exe\r\n" +
                    "Satisfactory.exe\r\n" +
                    "DysonSphereProgram.exe\r\n" +
                    "OxygenNotIncluded.exe\r\n" +
                    "TerrariaServer.exe\r\n" +
                    "Starbound.exe\r\n" +
                    "CoreKeeper.exe\r\n" +
                    "Valheim.exe\r\n" +
                    "VRising.exe\r\n" +
                    "Enshrouded.exe\r\n" +
                    "ArkAscended.exe\r\n" +
                    "ConanExiles.exe\r\n" +
                    "SCUM.exe\r\n" +
                    "7DaysToDie.exe\r\n" +
                    "ProjectZomboid64.exe\r\n" +
                    "StateOfDecay2.exe\r\n" +
                    "Left4Dead.exe\r\n" +
                    "KillingFloor2.exe\r\n" +
                    "Back4Blood.exe\r\n" +
                    "WorldWarZ.exe\r\n" +
                    "Left4Dead2_x64.exe\r\n" +
                    "Payday_win32_release.exe\r\n" +
                    "TeamFortress2.exe\r\n" +
                    "Portal2.exe\r\n" +
                    "Portal.exe\r\n" +
                    "HalfLife.exe\r\n" +
                    "BlackMesa.exe\r\n" +
                    "BioShockInfinite.exe\r\n" +
                    "BioShock2.exe\r\n" +
                    "Dishonored.exe\r\n" +
                    "Dishonored2.exe\r\n" +
                    "Prey.exe\r\n" +
                    "Deathloop.exe\r\n" +
                    "GhostwireTokyo.exe\r\n" +
                    "Doom.exe\r\n" +
                    "WolfensteinNewOrder.exe\r\n" +
                    "WolfensteinYoungblood.exe\r\n" +
                    "Rage2.exe\r\n" +
                    "QuakeChampions.exe\r\n" +
                    "UnrealTournament.exe\r\n" +
                    "Crysis.exe\r\n" +
                    "MetroExodus.exe\r\n" +
                    "Metro2033.exe\r\n" +
                    "MetroLastLight.exe\r\n" +
                    "STALKER.exe\r\n" +
                    "Stalker2-Win64-Shipping.exe\r\n" +
                    "AtomicHeart-Win64-Shipping.exe\r\n" +
                    "TombRaider.exe\r\n" +
                    "RotTR.exe\r\n" +
                    "ShadowOfTheTombRaider.exe\r\n" +
                    "UnchartedLegacyOfThieves.exe\r\n" +
                    "TheLastOfUsPart1.exe\r\n" +
                    "DaysGone_x64.exe\r\n" +
                    "SpiderManRemastered.exe\r\n" +
                    "SpiderManMilesMorales.exe\r\n" +
                    "RatchetAndClank.exe\r\n" +
                    "Returnal.exe\r\n" +
                    "Sackboy.exe\r\n" +
                    "GhostOfTsushima.exe\r\n" +
                    "GodOfWarRagnarok.exe\r\n" +
                    "HorizonForbiddenWest.exe\r\n" +
                    "DeathStranding.exe\r\n" +
                    "MetalGearSolidV.exe\r\n" +
                    "MGRInven.exe\r\n" +
                    "DevilMayCry5.exe\r\n" +
                    "MonsterHunterWilds.exe\r\n" +
                    "ResidentEvil4.exe\r\n" +
                    "ResidentEvilVillage.exe\r\n" +
                    "ResidentEvil7.exe\r\n" +
                    "ResidentEvil3.exe\r\n" +
                    "ResidentEvil2.exe\r\n" +
                    "StreetFighter6.exe\r\n" +
                    "Tekken8.exe\r\n" +
                    "MortalKombat1.exe\r\n" +
                    "MortalKombat11.exe\r\n" +
                    "GuiltyGearStrive.exe\r\n" +
                    "DragonBallFighterZ.exe\r\n" +
                    "NarutoStorm4.exe\r\n" +
                    "OnePiecePirateWarriors4.exe\r\n" +
                    "JumpForce.exe\r\n" +
                    "MyHeroOneJustice2.exe\r\n" +
                    "DemonSlayer.exe\r\n" +
                    "Persona5Royal.exe\r\n" +
                    "Persona3Reload.exe\r\n" +
                    "Metaphor.exe\r\n" +
                    "YakuzaLikeADragon.exe\r\n" +
                    "LikeADragonInfiniteWealth.exe\r\n" +
                    "Judgment.exe\r\n" +
                    "LostJudgment.exe\r\n" +
                    "Sifu.exe\r\n" +
                    "Sekiro.exe\r\n" +
                    "DarkSoulsRemastered.exe\r\n" +
                    "DarkSoulsII.exe\r\n" +
                    "DarkSoulsIII.exe\r\n" +
                    "ArmoredCore6.exe\r\n" +
                    "LiesOfP-Win64-Shipping.exe\r\n" +
                    "BlackMythWukong.exe\r\n" +
                    "Nioh.exe\r\n" +
                    "Nioh2.exe\r\n" +
                    "WoLong.exe\r\n" +
                    "WildHearts.exe\r\n" +
                    "MonsterHunterRise.exe\r\n" +
                    "Palworld.exe\r\n" +
                    "Enshrouded-Win64-Shipping.exe\r\n" +
                    "Nightingale.exe\r\n" +
                    "PacificDrive.exe\r\n" +
                    "Helldivers2_x64.exe\r\n" +
                    "WarhammerSpaceMarine2.exe\r\n" +
                    "Darktide.exe\r\n" +
                    "Vermintide2.exe\r\n" +
                    "Necromunda.exe\r\n" +
                    "SpaceHulk.exe\r\n" +
                    "AliensFireteamElite.exe\r\n" +
                    "AliensDarkDescent.exe\r\n" +
                    "PredatorHuntingGrounds.exe\r\n" +
                    "DeadSpace.exe\r\n" +
                    "TheCallistoProtocol.exe\r\n" +
                    "AlanWake2.exe\r\n" +
                    "Control.exe\r\n" +
                    "QuantumBreak.exe\r\n" +
                    "MaxPayne.exe\r\n" +
                    "AlanWake.exe\r\n" +
                    "SuicideSquad.exe\r\n" +
                    "GothamKnights.exe\r\n" +
                    "BatmanArkhamKnight.exe\r\n" +
                    "BatmanArkhamCity.exe\r\n" +
                    "BatmanArkhamAsylum.exe\r\n" +
                    "BatmanArkhamOrigins.exe\r\n" +
                    "MadMax.exe\r\n" +
                    "ShadowOfWar.exe\r\n" +
                    "ShadowOfMordor.exe\r\n" +
                    "Hogwarts.exe\r\n" +
                    "LegoStarWars.exe\r\n" +
                    "LegoMarvel.exe\r\n" +
                    "LegoBatman.exe\r\n" +
                    "Injustice2.exe\r\n" +
                    "MortalKombatX.exe\r\n" +
                    "Hitman3.exe\r\n" +
                    "Hitman2.exe\r\n" +
                    "Hitman.exe\r\n" +
                    "SplinterCell.exe\r\n" +
                    "GhostReconBreakpoint.exe\r\n" +
                    "GhostReconWildlands.exe\r\n" +
                    "Division.exe\r\n" +
                    "RainbowSixSiege.exe\r\n" +
                    "FarCry5.exe\r\n" +
                    "FarCry4.exe\r\n" +
                    "FarCry3.exe\r\n" +
                    "AssassinsCreedMirage.exe\r\n" +
                    "AssassinsCreedShadows.exe\r\n" +
                    "AssassinsCreedOdyssey.exe\r\n" +
                    "AssassinsCreedOrigins.exe\r\n" +
                    "AssassinsCreedSyndicate.exe\r\n" +
                    "AssassinsCreedUnity.exe\r\n" +
                    "AssassinsCreedBlackFlag.exe\r\n" +
                    "WatchDogs.exe\r\n" +
                    "WatchDogsLegion.exe\r\n" +
                    "TheCrewMotorfest.exe\r\n" +
                    "TheCrew2.exe\r\n" +
                    "TheCrew.exe\r\n" +
                    "RidersRepublic.exe\r\n" +
                    "Steep.exe\r\n" +
                    "TrialsRising.exe\r\n" +
                    "Trackmania.exe\r\n" +
                    "RaymanLegends.exe\r\n" +
                    "PrinceOfPersia.exe\r\n" +
                    "ImmortalsFenyxRising.exe\r\n" +
                    "SkullAndBones.exe\r\n" +
                    "XDefiant.exe\r\n" +
                    "Hyperscape.exe\r\n" +
                    "ForHonor.exe\r\n" +
                    "Anno2070.exe\r\n" +
                    "Anno2205.exe\r\n" +
                    "TheSettlers.exe\r\n" +
                    "SouthPark.exe\r\n" +
                    "StarWarsOutlaws.exe\r\n" +
                    "StarWarsJediSurvivor.exe\r\n" +
                    "StarWarsJediFallenOrder.exe\r\n" +
                    "StarWarsBattlefrontII.exe\r\n" +
                    "StarWarsBattlefront.exe\r\n" +
                    "StarWarsSquadrons.exe\r\n" +
                    "MassEffectAndromeda.exe\r\n" +
                    "MassEffectLegendary.exe\r\n" +
                    "DragonAgeTheVeilguard.exe\r\n" +
                    "DragonAgeInquisition.exe\r\n" +
                    "Anthem.exe\r\n" +
                    "Battlefield2042.exe\r\n" +
                    "BattlefieldV.exe\r\n" +
                    "Battlefield1.exe\r\n" +
                    "Battlefield4.exe\r\n" +
                    "Battlefield3.exe\r\n" +
                    "BattlefieldHardline.exe\r\n" +
                    "ApexLegends.exe\r\n" +
                    "Titanfall2.exe\r\n" +
                    "Titanfall.exe\r\n" +
                    "NeedForSpeedUnbound.exe\r\n" +
                    "NeedForSpeedHeat.exe\r\n" +
                    "NeedForSpeedPayback.exe\r\n" +
                    "NeedForSpeedRivals.exe\r\n" +
                    "GridLegends.exe\r\n" +
                    "Dirt5.exe\r\n" +
                    "F1_23.exe\r\n" +
                    "F1_22.exe\r\n" +
                    "WRC.exe\r\n" +
                    "BurnoutParadise.exe\r\n" +
                    "DeadSpace3.exe\r\n" +
                    "DeadSpace2.exe\r\n" +
                    "Crysis3.exe\r\n" +
                    "Crysis2.exe\r\n" +
                    "MirrorEdge.exe\r\n" +
                    "MirrorEdgeCatalyst.exe\r\n" +
                    "PlantsVsZombies.exe\r\n" +
                    "Peggle.exe\r\n" +
                    "Bejeweled3.exe\r\n" +
                    "Zuma.exe\r\n" +
                    "Spore.exe\r\n" +
                    "Sims4.exe\r\n" +
                    "SimCity4.exe\r\n" +
                    "CommandAndConquer4.exe\r\n" +
                    "RedAlert3.exe\r\n" +
                    "TiberiumWars.exe\r\n" +
                    "Generals.exe\r\n" +
                    "MedalOfHonor.exe\r\n" +
                    "DeadlyPremonition.exe\r\n" +
                    "SilentHill.exe\r\n" +
                    "MetalGearSolidDelta.exe\r\n" +
                    "Castlevania.exe\r\n" +
                    "Contra.exe\r\n" +
                    "YuGiOhMasterDuel.exe\r\n" +
                    "eFootball.exe\r\n" +
                    "PES2021.exe\r\n" +
                    "Suikoden.exe\r\n" +
                    "SilentHill2Remake.exe\r\n" +
                    "MetalGearSolidMasterCollection.exe\r\n" +
                    "VampireSurvivors.exe\r\n" +
                    "DaveTheDiver.exe\r\n" +
                    "Dredge.exe\r\n" +
                    "Balatro.exe\r\n" +
                    "CultOfTheLamb.exe\r\n" +
                    "Inscryption.exe\r\n" +
                    "LoopHero.exe\r\n" +
                    "DeathMustDie.exe\r\n" +
                    "HallsOfTorment.exe\r\n" +
                    "Brotato.exe\r\n" +
                    "20MinutesTillDawn.exe\r\n" +
                    "BindingOfIsaac.exe\r\n" +
                    "EnterTheGungeon.exe\r\n" +
                    "DeadCells.exe\r\n" +
                    "RogueLegacy2.exe\r\n" +
                    "SlayTheSpire.exe\r\n" +
                    "MonsterTrain.exe\r\n" +
                    "DarkestDungeon.exe\r\n" +
                    "DarkestDungeon2.exe\r\n" +
                    "CryptOfTheNecroDancer.exe\r\n" +
                    "Spelunky2.exe\r\n" +
                    "Spelunky.exe\r\n" +
                    "RiskOfRain2.exe\r\n" +
                    "RiskOfRainReturns.exe\r\n" +
                    "Noita.exe\r\n" +
                    "Teardown.exe\r\n" +
                    "GarrysMod.exe\r\n" +
                    "RustClient_x64.exe\r\n" +
                    "Unturned.exe\r\n" +
                    "ScrapMechanic.exe\r\n" +
                    "Trailmakers.exe\r\n" +
                    "Besiege.exe\r\n" +
                    "PolyBridge2.exe\r\n" +
                    "KerbalSpaceProgram.exe\r\n" +
                    "KSP2.exe\r\n" +
                    "SpaceEngineers.exe\r\n" +
                    "MedievalEngineers.exe\r\n" +
                    "SubnauticaBelowZero.exe\r\n" +
                    "TheLongDark.exe\r\n" +
                    "GreenHell_x64.exe\r\n" +
                    "StrandedDeep_x64.exe\r\n" +
                    "Breathedge.exe\r\n" +
                    "OuterWilds.exe\r\n" +
                    "Subnautica_x64.exe\r\n" +
                    "Satisfactory-Win64-Shipping.exe\r\n" +
                    "Factorio_x64.exe\r\n" +
                    "RimWorld.exe\r\n" +
                    "PrisonArchitect.exe\r\n" +
                    "RimWorldLinux.exe\r\n" +
                    "DwarfFortress.exe\r\n" +
                    "SongsOfSyx.exe\r\n" +
                    "Ostriv.exe\r\n" +
                    "ManorLords.exe\r\n" +
                    "FarthestFrontier.exe\r\n" +
                    "GoingMedieval.exe\r\n" +
                    "Timberborn.exe\r\n" +
                    "Beaver.exe\r\n" +
                    "CaptainOfIndustry.exe\r\n" +
                    "WorkersAndResources.exe\r\n" +
                    "CitiesSkylines2.exe\r\n" +
                    "CitiesSkylines.exe\r\n" +
                    "SimCity_x64.exe\r\n" +
                    "TheoTown.exe\r\n" +
                    "OpenTTD.exe\r\n" +
                    "RailwayEmpire2.exe\r\n" +
                    "TransportFever2.exe\r\n" +
                    "TrainSimWorld4.exe\r\n" +
                    "TrainSimulator.exe\r\n" +
                    "EuroTruckSimulator2.exe\r\n" +
                    "AmericanTruckSimulator.exe\r\n" +
                    "BusSimulator21.exe\r\n" +
                    "ConstructionSimulator.exe\r\n" +
                    "FarmingSimulator25.exe\r\n" +
                    "FarmingSimulator19.exe\r\n" +
                    "FarmingSimulator17.exe\r\n" +
                    "SnowRunner.exe\r\n" +
                    "MudRunner.exe\r\n" +
                    "Expeditions.exe\r\n" +
                    "CarMechanicSimulator2021.exe\r\n" +
                    "PCBuildingSimulator2.exe\r\n" +
                    "GasStationSimulator.exe\r\n" +
                    "HouseFlipper2.exe\r\n" +
                    "HouseFlipper.exe\r\n" +
                    "PowerWashSimulator.exe\r\n" +
                    "LawnMowingSimulator.exe\r\n" +
                    "ThiefSimulator2.exe\r\n" +
                    "CookingSimulator.exe\r\n" +
                    "MySummerCar.exe\r\n" +
                    "BeamNG.drive.exe\r\n" +
                    "AssettoCorsa.exe\r\n" +
                    "AssettoCorsaCompetizione.exe\r\n" +
                    "iRacing.exe\r\n" +
                    "rFactor2.exe\r\n" +
                    "Automobilista2.exe\r\n" +
                    "ProjectCARS3.exe\r\n" +
                    "ProjectCARS2.exe\r\n" +
                    "Wreckfest.exe\r\n" +
                    "FlatOut2.exe\r\n" +
                    "DirtRally2.exe\r\n" +
                    "Dirt4.exe\r\n" +
                    "WRC10.exe\r\n" +
                    "ForzaHorizon4.exe\r\n" +
                    "ForzaMotorsport.exe\r\n" +
                    "TheCrew3.exe\r\n" +
                    "TestDriveUnlimitedSolarCrown.exe\r\n" +
                    "TDU2.exe\r\n" +
                    "NFSMostWanted.exe\r\n" +
                    "NFSUnderground2.exe\r\n" +
                    "NFSUnderground.exe\r\n" +
                    "NFSCarbon.exe\r\n" +
                    "NFSProStreet.exe\r\n" +
                    "NFSShift.exe\r\n" +
                    "NFSTheRun.exe\r\n" +
                    "NFS2015.exe\r\n" +
                    "Grid.exe\r\n" +
                    "Grid2.exe\r\n" +
                    "GridAutosport.exe\r\n" +
                    "TOCA.exe\r\n" +
                    "ColinMcRae.exe\r\n" +
                    "RichardBurnsRally.exe\r\n" +
                    "LiveForSpeed.exe\r\n" +
                    "TrackmaniaTurbo.exe\r\n" +
                    "TrackmaniaNationsForever.exe\r\n" +
                    "Distance.exe\r\n" +
                    "NitronicRush.exe\r\n" +
                    "Redout2.exe\r\n" +
                    "Wipeout.exe\r\n" +
                    "FZero.exe\r\n" +
                    "SuperTuxKart.exe\r\n" +
                    "SonicAllStarsRacingTransformed.exe\r\n" +
                    "CrashTeamRacing.exe\r\n" +
                    "HotWheelsUnleashed2.exe\r\n" +
                    "Lego2KDrive.exe\r\n" +
                    "WormsArmageddon.exe\r\n" +
                    "WormsWMD.exe\r\n" +
                    "WormsRevolution.exe\r\n" +
                    "WormsUltimateMayhem.exe\r\n" +
                    "Hedgewars.exe\r\n" +
                    "ShellshockLive.exe\r\n" +
                    "Gunbound.exe\r\n" +
                    "Soldat.exe\r\n" +
                    "Teeworlds.exe\r\n" +
                    "DuckGame.exe\r\n" +
                    "StickFight.exe\r\n" +
                    "Brawlhalla_x64.exe\r\n" +
                    "GangBeasts.exe\r\n" +
                    "HumanFallFlat.exe\r\n" +
                    "PartyAnimals.exe\r\n" +
                    "FallGuys.exe\r\n" +
                    "StumbleGuys.exe\r\n" +
                    "AmongUs.exe\r\n" +
                    "GooseGooseDuck.exe\r\n" +
                    "ProjectWinter.exe\r\n" +
                    "DreadHunger.exe\r\n" +
                    "Deceit2.exe\r\n" +
                    "TownOfSalem2.exe\r\n" +
                    "WerewolvesWithin.exe\r\n" +
                    "SecretNeighbor.exe\r\n" +
                    "HelloNeighbor2.exe\r\n" +
                    "HelloNeighbor.exe\r\n" +
                    "FiveNightsAtFreddys.exe\r\n" +
                    "FNAFSecurityBreach.exe\r\n" +
                    "FNAFHelpWanted2.exe\r\n" +
                    "Phasmophobia_x64.exe\r\n" +
                    "Demonologist.exe\r\n" +
                    "Lethal Company.exe\r\n" +
                    "LethalCompany.exe\r\n" +
                    "ContentWarning.exe\r\n" +
                    "PacificDrive-Win64-Shipping.exe\r\n" +
                    "TheOutlastTrials.exe\r\n" +
                    "Outlast2.exe\r\n" +
                    "Outlast.exe\r\n" +
                    "AmnesiaTheBunker.exe\r\n" +
                    "AmnesiaRebirth.exe\r\n" +
                    "AmnesiaTheDarkDescent.exe\r\n" +
                    "SOMA.exe\r\n" +
                    "AlienIsolation.exe\r\n" +
                    "DeadSpaceRemake.exe\r\n" +
                    "TheEvilWithin2.exe\r\n" +
                    "TheEvilWithin.exe\r\n" +
                    "AlanWakeRemastered.exe\r\n" +
                    "Control_DX12.exe\r\n" +
                    "QuantumBreak_DX12.exe\r\n" +
                    "MaxPayne2.exe\r\n" +
                    "AlanWake_x64.exe\r\n" +
                    "MaxPayne_x64.exe\r\n" +
                    "FEAR.exe\r\n" +
                    "FEAR2.exe\r\n" +
                    "FEAR3.exe\r\n" +
                    "Condemned.exe\r\n" +
                    "Manhunt2.exe\r\n" +
                    "Manhunt.exe\r\n" +
                    "Postal4.exe\r\n" +
                    "PostalRedux.exe\r\n" +
                    "Postal2.exe\r\n" +
                    "Postal.exe\r\n" +
                    "Hatred.exe\r\n" +
                    "Manhunt_x64.exe\r\n" +
                    "GTA_VC.exe\r\n" +
                    "GTA_SA.exe\r\n" +
                    "GTA_3.exe\r\n" +
                    "GTA_IV.exe\r\n" +
                    "EFLC.exe\r\n" +
                    "GTA5_x64.exe\r\n" +
                    "RDR2_x64.exe\r\n" +
                    "LANoire.exe\r\n" +
                    "Bully_Scholarship_Edition.exe\r\n" +
                    "MidnightClub3.exe\r\n" +
                    "MaxPayne3_x64.exe\r\n" +
                    "Agent.exe\r\n" +
                    "Judas.exe\r\n" +
                    "BioShockRemastered.exe\r\n" +
                    "BioShock2Remastered.exe";
            }
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            if (!isRunning)
            {
                int hours = int.Parse(cmbHoursAllowed.SelectedItem.ToString());
                timeRemainingSeconds = hours * 3600;

                SaveTimeToFile(timeRemainingSeconds);

                try
                {
                    AppManager.SetAutostart(true);
                }
                catch
                {
                }

                isRunning = true;

                btnStart.Enabled = false;
                btnStop.Enabled = true;
                btnSaveList.Enabled = false;
                txtProcessList.Enabled = false;
                cmbHoursAllowed.Enabled = false;
                chkGpuTrigger.Enabled = false;
                cmbGpuThreshold.Enabled = false;
                chkCpuTrigger.Enabled = false;
                cmbCpuThreshold.Enabled = false;
                chkProcessTrigger.Enabled = false;

                monitoringTimer.Start();

                UpdateTimerLabel();
            }
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            if (!AppManager.VerifyPassword(PromptForPassword()))
                return;

            isRunning = false;

            monitoringTimer.Stop();

            btnStart.Enabled = true;
            btnStop.Enabled = false;
            btnSaveList.Enabled = true;
            txtProcessList.Enabled = true;
            cmbHoursAllowed.Enabled = true;
            chkGpuTrigger.Enabled = true;
            cmbGpuThreshold.Enabled = true;
            chkCpuTrigger.Enabled = true;
            cmbCpuThreshold.Enabled = true;
            chkProcessTrigger.Enabled = true;

            lblTimeRemaining.Text = "Time limit cancelled.";

            try
            {
                AppManager.SetAutostart(false);
            }
            catch
            {
            }
        }

        private async void MonitoringTimer_Tick(object sender, EventArgs e)
        {
            bool isGamingDetected = false;

            if (chkProcessTrigger.Checked)
            {
                string[] gamesToWatch =
                    txtProcessList.Text.Split(
                        new[] { "\r\n", "\n" },
                        StringSplitOptions.RemoveEmptyEntries);

                Process[] runningProcesses =
                    Process.GetProcesses();

                foreach (var game in gamesToWatch)
                {
                    string gameName =
                        game.ToLower().Replace(".exe", "");

                    if (runningProcesses.Any(
                        p => p.ProcessName.ToLower() == gameName))
                    {
                        isGamingDetected = true;
                        break;
                    }
                }
            }

            if (chkGpuTrigger.Checked && !isGamingDetected)
            {
                float threshold = 50f;

                if (cmbGpuThreshold.SelectedItem != null)
                {
                    string val =
                        cmbGpuThreshold.SelectedItem
                            .ToString()
                            .Replace("%", "");

                    float.TryParse(val, out threshold);
                }

                if (gpuMonitor.IsAnyGpuUsageAbove(threshold))
                    isGamingDetected = true;
            }

            if (chkCpuTrigger.Checked &&
                !isGamingDetected &&
                cpuCounter != null)
            {
                float threshold = 20f;

                if (cmbCpuThreshold.SelectedItem != null)
                {
                    string val =
                        cmbCpuThreshold.SelectedItem
                            .ToString()
                            .Replace("%", "");

                    float.TryParse(val, out threshold);
                }

                try
                {
                    cpuCounter.NextValue();

                    await System.Threading.Tasks.Task.Delay(100);

                    float cpuUsage = cpuCounter.NextValue();

                    if (cpuUsage >= threshold)
                        isGamingDetected = true;
                }
                catch
                {
                }
            }

            if (isGamingDetected)
            {
                timeRemainingSeconds -= 5;
                SaveTimeToFile(timeRemainingSeconds);

                if (timeRemainingSeconds == 900)
                {
                    MessageBox.Show(
                        "There are only 15 minutes of playtime left! Get ready to wrap things up.",
                        "Time warning",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }

                UpdateTimerLabel();

                if (timeRemainingSeconds <= 0)
                {
                    timeRemainingSeconds = 0;
                    SaveTimeToFile(0);
                    EnforceTimeLimit();
                }
            }
        }

        private bool PromptForUnlockWithTimeout()
        {
            bool unlocked = false;
            int secondsLeft = 300;

            Form prompt = new Form()
            {
                Width = 380,
                Height = 205,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = "Time's up – Password required",
                StartPosition = FormStartPosition.CenterScreen,
                BackColor = Color.FromArgb(24, 24, 27),
                ForeColor = Color.White,
                MaximizeBox = false,
                MinimizeBox = false,
                ControlBox = false
            };

            Label lblInfo = new Label()
            {
                Text = "Time's up! Enter the password within 5 minutes, otherwise the computer will shut down again.",
                Location = new Point(20, 15),
                Size = new Size(330, 40),
                Font = new Font("Segoe UI", 9.5F)
            };

            Label lblCountdown = new Label()
            {
                Text = "Time remaining: 05:00",
                Location = new Point(20, 60),
                Size = new Size(330, 25),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(239, 68, 68)
            };

            TextBox txt = new TextBox()
            {
                Location = new Point(20, 95),
                Size = new Size(320, 30),
                UseSystemPasswordChar = true,
                BackColor = Color.FromArgb(38, 38, 42),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F)
            };

            Button btnConfirm = new Button()
            {
                Text = "Unlock",
                Location = new Point(240, 135),
                Size = new Size(100, 35),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            btnConfirm.FlatAppearance.BorderSize = 0;

            Timer countdownTimer = new Timer() { Interval = 1000 };
            countdownTimer.Tick += (s, ev) =>
            {
                secondsLeft--;
                int m = secondsLeft / 60;
                int sRem = secondsLeft % 60;
                lblCountdown.Text = $"Pozostały czas na wpisanie hasła: {m:D2}:{sRem:D2}";

                if (secondsLeft <= 0)
                {
                    countdownTimer.Stop();
                    prompt.Close();
                }
            };

            btnConfirm.Click += (s, ev) =>
            {
                if (AppManager.VerifyPassword(txt.Text))
                {
                    unlocked = true;
                    countdownTimer.Stop();
                    prompt.Close();
                }
                else
                {
                    MessageBox.Show("Nieprawidłowe hasło!", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            prompt.Controls.Add(lblInfo);
            prompt.Controls.Add(lblCountdown);
            prompt.Controls.Add(txt);
            prompt.Controls.Add(btnConfirm);
            prompt.AcceptButton = btnConfirm;

            countdownTimer.Start();
            prompt.ShowDialog();
            countdownTimer.Dispose();

            return unlocked;
        }

        private void EnforceTimeLimit()
        {
            if (isUnlocking) return;

            isUnlocking = true;
            monitoringTimer.Stop();

            string[] gamesToWatch = txtProcessList.Text
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(g => g.Trim().ToLower().Replace(".exe", ""))
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .ToArray();

            Process[] runningProcesses = Process.GetProcesses();

            try
            {
                foreach (var p in runningProcesses)
                {
                    try
                    {
                        if (gamesToWatch.Contains(p.ProcessName.ToLower()))
                        {
                            p.Kill();
                        }
                    }
                    catch { }
                    finally
                    {
                        p.Dispose();
                    }
                }
            }
            catch { }

            bool unlocked = PromptForUnlockWithTimeout();

            if (unlocked)
            {
                timeRemainingSeconds = 0;
                if (File.Exists(timeFilePath))
                {
                    try { File.Delete(timeFilePath); } catch { }
                }

                isRunning = false;

                btnStart.Enabled = true;
                btnStop.Enabled = false;
                btnSaveList.Enabled = true;
                txtProcessList.Enabled = true;
                cmbHoursAllowed.Enabled = true;
                chkGpuTrigger.Enabled = true;
                cmbGpuThreshold.Enabled = true;
                chkCpuTrigger.Enabled = true;
                cmbCpuThreshold.Enabled = true;
                chkProcessTrigger.Enabled = true;

                lblTimeRemaining.Text = "Protection disabled (unblocked).";

                try { AppManager.SetAutostart(false); } catch { }
            }
            else
            {
                try
                {
                    Process.Start("shutdown", "/s /f /t 0");
                }
                catch { }
            }

            isUnlocking = false;
        }

        private void UpdateTimerLabel()
        {
            TimeSpan time =
                TimeSpan.FromSeconds(timeRemainingSeconds);

            lblTimeRemaining.Text =
                $"Time remaining: {time.Hours}h {time.Minutes}m {time.Seconds}s";
        }

        private void MainForm_FormClosing(
            object sender,
            FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing &&
                isRunning)
            {
                e.Cancel = true;

                this.Hide();
                this.ShowInTaskbar = false;
                notifyIcon1.Visible = true;
            }
            else
            {
                notifyIcon1.Visible = false;
                notifyIcon1.Dispose();

                gpuMonitor.Close();
                cpuCounter?.Dispose();
            }
        }

        private string PromptForPassword()
        {
            Form prompt = new Form()
            {
                Width = 320,
                Height = 160,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = "Password required",
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(24, 24, 27),
                ForeColor = Color.White
            };

            TextBox txt = new TextBox()
            {
                Location = new Point(20, 20),
                Size = new Size(260, 30),
                UseSystemPasswordChar = true,
                BackColor = Color.FromArgb(38, 38, 42),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F)
            };

            Button confirmation = new Button()
            {
                Text = "Confirm",
                Location = new Point(180, 70),
                Size = new Size(100, 35),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };

            confirmation.FlatAppearance.BorderSize = 0;

            confirmation.Click += (sender, ev) =>
            {
                prompt.Close();
            };

            prompt.Controls.Add(txt);
            prompt.Controls.Add(confirmation);

            prompt.AcceptButton = confirmation;

            prompt.ShowDialog();

            return txt.Text;
        }
        private void SaveTimeToFile(int seconds)
        {
            try
            {
                string dir = Path.GetDirectoryName(timeFilePath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(timeFilePath, seconds.ToString());
            }
            catch { }
        }

        private int LoadTimeFromFile()
        {
            try
            {
                if (File.Exists(timeFilePath))
                {
                    string content = File.ReadAllText(timeFilePath).Trim();
                    if (int.TryParse(content, out int seconds))
                    {
                        return seconds;
                    }
                }
            }
            catch { }
            return -1;
        }
    }

}