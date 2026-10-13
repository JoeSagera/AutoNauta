using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Collections.Generic;
using System.Windows.Forms;

namespace NautaReconnect
{
    public sealed class UiContext : ApplicationContext
    {
        readonly IController controller;
        readonly bool preview;
        readonly Form main;
        readonly IndicatorForm indicator;
        readonly NotifyIcon tray;
        readonly ContextMenuStrip menu;
        readonly ToolStripMenuItem toggleMenu, checkMenu,automaticMenu,pinnedMenu,restoreIndicatorMenu;
        readonly System.Windows.Forms.Timer positionTimer;
        readonly object commandGate = new object();
        readonly TextBox username, password, detail;
        readonly Label stateLabel, stateTime, accountHint,metricsLabel;
        readonly Panel stateDot;
        readonly ListBox log;
        readonly Button toggle, check, save, clear,automatic;
        Icon trayImage;
        readonly Dictionary<Light,Icon> icons=new Dictionary<Light,Icon>();
        volatile bool metricsVisible;
        int metricsPending;
        string previousLog, enteredSecret = String.Empty;
        volatile bool exiting;
        bool cleaned, shutdownCompleted;
        int pending;

        public UiContext(IController controller, bool preview)
        {
            if (controller == null) throw new ArgumentNullException("controller");
            this.controller = controller; this.preview = preview;
            main = new Form { Text = "AutoNauta" + (preview ? " · Vista previa" : ""), ClientSize = new Size(580,658), MinimumSize = new Size(596,696), StartPosition = FormStartPosition.CenterScreen, BackColor = Palette.Background, ForeColor = Palette.Text, Font = new Font("Segoe UI",9F), AutoScaleMode = AutoScaleMode.Dpi, Padding = new Padding(22,16,22,14) };
            main.FormClosing += delegate(object sender, FormClosingEventArgs e) {
                if (!exiting) { e.Cancel = true; if (e.CloseReason == CloseReason.UserClosing) HideMain(); else ExitRequested(); }
            };
            main.Resize += delegate { if (!exiting && main.WindowState == FormWindowState.Minimized) HideMain(); };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8, Margin = Padding.Empty };
            foreach (int height in new int[] {62,106,87,131,42,25}) layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,30)); main.Controls.Add(layout);
            var header = new Panel { Dock = DockStyle.Fill };
            Label title = LabelFor("AutoNauta",22F,Palette.Text); title.SetBounds(0,0,250,35);
            Label subtitle = LabelFor(preview ? "VISTA PREVIA · SIN CONEXIONES DE RED" : "Reconexión automática · estado de tu conexión",9F,Palette.Muted); subtitle.SetBounds(2,37,520,20);
            header.Controls.Add(title); header.Controls.Add(subtitle); layout.Controls.Add(header,0,0);
            var state = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Surface, Margin = new Padding(0,0,0,8) };
            stateDot = new Panel { Size = new Size(12,12), Location = new Point(15,15) };
            stateDot.Paint += delegate(object sender, PaintEventArgs e) { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.Clear(Palette.Surface); using (Brush brush = new SolidBrush(stateDot.BackColor)) e.Graphics.FillEllipse(brush,0,0,11,11); };
            stateLabel = LabelFor("Esperando",11F,Palette.Text); stateLabel.SetBounds(37,9,480,25); stateLabel.AutoEllipsis = true;
            stateTime = LabelFor("",8F,Palette.Muted); stateTime.SetBounds(38,34,470,19);
            metricsLabel=LabelFor("Tiempo: —h —m     ↓ — Kb/s     ↑ — Kb/s",10F,Palette.Text);metricsLabel.SetBounds(15,64,505,25);
            state.Controls.Add(stateDot); state.Controls.Add(stateLabel); state.Controls.Add(stateTime);state.Controls.Add(metricsLabel); layout.Controls.Add(state,0,1);
            detail = TextBoxFor(); detail.Multiline = true; detail.ReadOnly = true; detail.ScrollBars = ScrollBars.Vertical; detail.Dock = DockStyle.Fill; detail.Margin = new Padding(0,0,0,8); detail.TabStop = false; layout.Controls.Add(detail,0,2);
            var account = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Margin = Padding.Empty };
            account.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,95)); account.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            foreach(int height in new int[] {31,31,23,39}) account.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
            Label userCaption = LabelFor("Cuenta Nauta",9F,Palette.Muted); userCaption.Dock = DockStyle.Fill; userCaption.TextAlign = ContentAlignment.MiddleLeft;
            Label passCaption = LabelFor("Contraseña",9F,Palette.Muted); passCaption.Dock = DockStyle.Fill; passCaption.TextAlign = ContentAlignment.MiddleLeft;
            username = TextBoxFor(); username.Dock = DockStyle.Fill; username.Text = controller.Settings.Username ?? "";
            password = TextBoxFor(); password.Dock = DockStyle.Fill; password.UseSystemPasswordChar = true;
            account.Controls.Add(userCaption,0,0); account.Controls.Add(username,1,0); account.Controls.Add(passCaption,0,1); account.Controls.Add(password,1,1);
            accountHint = LabelFor("",8F,Palette.Muted); accountHint.Dock = DockStyle.Fill; account.Controls.Add(accountHint,0,2); account.SetColumnSpan(accountHint,2);
            var accountActions = FlowFor(); save = ButtonFor("Guardar cuenta",125); clear = ButtonFor("Eliminar guardada",135);automatic=ButtonFor("Login automático: ON",180); accountActions.Controls.Add(save); accountActions.Controls.Add(clear);accountActions.Controls.Add(automatic); account.Controls.Add(accountActions,0,3); account.SetColumnSpan(accountActions,2); layout.Controls.Add(account,0,3);
            var actions = FlowFor(); toggle = ButtonFor("Iniciar",110); check = ButtonFor("Comprobar ahora",150); Button minimize = ButtonFor("Minimizar",110), exit = ButtonFor("Salir",76);
            actions.Controls.Add(toggle); actions.Controls.Add(check); actions.Controls.Add(minimize); actions.Controls.Add(exit); layout.Controls.Add(actions,0,4);
            Label logCaption = LabelFor("ACTIVIDAD RECIENTE",8F,Palette.Muted); logCaption.Dock = DockStyle.Fill; logCaption.TextAlign = ContentAlignment.MiddleLeft; layout.Controls.Add(logCaption,0,5);
            log = new ListBox { Dock = DockStyle.Fill, BackColor = Palette.Surface, ForeColor = Palette.Muted, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI",8F), HorizontalScrollbar = true, IntegralHeight = false, Margin = new Padding(0,0,0,5) }; layout.Controls.Add(log,0,6);
            Label footer = LabelFor("LED flotante · pasa el mouse para ver tiempo y velocidades · arrastra para mover.",8F,Palette.Muted); footer.Dock = DockStyle.Fill; footer.TextAlign = ContentAlignment.MiddleLeft; layout.Controls.Add(footer,0,7);
            menu = new ContextMenuStrip { BackColor = Palette.Surface, ForeColor = Palette.Text };
            var showMenu = new ToolStripMenuItem("Mostrar AutoNauta"); showMenu.Click += delegate { ShowMain(true); }; menu.Items.Add(showMenu);
            restoreIndicatorMenu = new ToolStripMenuItem("Recuperar LED"); restoreIndicatorMenu.Click += delegate { if(!exiting) indicator.EnsureVisible(); }; menu.Items.Add(restoreIndicatorMenu);
            toggleMenu = new ToolStripMenuItem("Iniciar"); toggleMenu.Click += delegate { ToggleRunning(); }; menu.Items.Add(toggleMenu);
            checkMenu = new ToolStripMenuItem("Comprobar ahora"); checkMenu.Click += delegate { RunCommand(delegate { controller.CheckNow(); },null); }; menu.Items.Add(checkMenu);
            automaticMenu=new ToolStripMenuItem("Login automático");automaticMenu.Click+=delegate{ToggleAutomatic();};menu.Items.Add(automaticMenu);
            pinnedMenu=new ToolStripMenuItem("Mantener barra expandida");pinnedMenu.Click+=delegate{TogglePinned();};menu.Items.Add(pinnedMenu);
            menu.Items.Add(new ToolStripSeparator()); var exitMenu = new ToolStripMenuItem("Salir"); exitMenu.Click += delegate { ExitRequested(); }; menu.Items.Add(exitMenu);
            indicator = new IndicatorForm(menu); indicator.OpenMain += delegate { ShowMain(true); }; indicator.MovedByUser += delegate { PositionChanged(); };
            indicator.ReadMetrics=delegate{return controller.Metrics;};indicator.ExpansionChanged+=delegate{RefreshMetricsVisibility();};main.VisibleChanged+=delegate{RefreshMetricsVisibility();};
            indicator.AutoLoginRequested+=delegate{ToggleAutomatic();};indicator.PinRequested+=delegate{TogglePinned();};indicator.SetPinned(controller.Settings.IndicatorPinned);
            indicator.MoveAnchor(ClampPosition(new Point(controller.Settings.IndicatorX,controller.Settings.IndicatorY),indicator.CollapsedSize));
            tray = new NotifyIcon { ContextMenuStrip = menu, Text = "AutoNauta" }; tray.DoubleClick += delegate { ShowMain(true); };
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
            positionTimer = new System.Windows.Forms.Timer { Interval = 650 }; positionTimer.Tick += delegate { positionTimer.Stop(); PersistPosition(); };
            minimize.Click += delegate { HideMain(); }; exit.Click += delegate { ExitRequested(); }; toggle.Click += delegate { ToggleRunning(); };
            check.Click += delegate { RunCommand(delegate { controller.CheckNow(); },null); }; save.Click += delegate { SaveAccount(); };
            automatic.Click+=delegate{ToggleAutomatic();};
            clear.Click += delegate { RunCommand(delegate { controller.ClearAccount(); },delegate { username.Clear(); password.Clear(); enteredSecret = ""; RefreshAccountHint(); }); };
            // Create a dispatch handle before subscribing to worker status events.
            IntPtr handle = main.Handle; controller.StatusChanged += StatusChanged;controller.MetricsChanged+=MetricsChanged;
            ApplyStatus(controller.Current);ApplyMetrics(controller.Metrics); RefreshAccountHint(); RefreshActions(); tray.Visible = true; indicator.Show(); main.Show();ApplyMetrics(controller.Metrics);
        }
        static Label LabelFor(string text,float size,Color color) { return new Label { Text = text, ForeColor = color, BackColor = Color.Transparent, Font = new Font("Segoe UI",size), Margin = Padding.Empty }; }
        static TextBox TextBoxFor() { return new TextBox { BackColor = Palette.Surface, ForeColor = Palette.Text, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0,3,0,3) }; }
        static FlowLayoutPanel FlowFor() { return new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = Padding.Empty, WrapContents = false }; }
        static Button ButtonFor(string text,int width) { var b = new DarkButton { Text = text, Width = width, Height = 31, FlatStyle = FlatStyle.Flat, BackColor = Palette.Surface, ForeColor = Palette.Text, Margin = new Padding(0,3,7,3), Cursor = Cursors.Hand }; b.FlatAppearance.BorderColor = Palette.Border; return b; }
        void StatusChanged(object sender,EventArgs e) { Post(delegate { if (!exiting) { ApplyStatus(controller.Current); RefreshActions(); } }); }
        void MetricsChanged(object sender,EventArgs e){
            if(exiting||!metricsVisible||Interlocked.Exchange(ref metricsPending,1)!=0)return;
            Post(delegate{try{if(!exiting&&metricsVisible)ApplyMetrics(controller.Metrics);}finally{Interlocked.Exchange(ref metricsPending,0);}});
        }
        void RefreshMetricsVisibility(){metricsVisible=main.Visible||indicator.Expanded;}
        void ApplyMetrics(Telemetry data){
            if(main.Visible){
            metricsLabel.Text="Tiempo: "+RateMonitor.TimeText(data.Remaining)+"     ↓ "+(data.RatesReady?RateMonitor.Speed(data.DownloadBytesPerSecond):"— Kb/s")+"     ↑ "+(data.RatesReady?RateMonitor.Speed(data.UploadBytesPerSecond):"— Kb/s");
            accountHint.Text=data.Remaining.HasValue?"Tiempo confirmado · última consulta "+data.AccountCheckedUtc.ToLocalTime().ToString("HH:mm:ss"):(data.Credit.HasValue?"El portal informa crédito en CUP; el contador aparecerá al iniciar con AutoNauta.":"Tiempo no disponible todavía; no se repite el login para obtenerlo.");
            }
            indicator.SetMetrics(data);
        }
        void Post(Action action) {
            if (main.IsDisposed || !main.IsHandleCreated) return;
            try { main.BeginInvoke((MethodInvoker)delegate { if (!main.IsDisposed) action(); }); } catch(InvalidOperationException) { }
        }
        string SafeText(string value) {
            string result = value ?? "";
            if (!String.IsNullOrEmpty(enteredSecret)) result = result.Replace(enteredSecret,"[secreto]");
            if (!String.IsNullOrEmpty(password.Text)) result = result.Replace(password.Text,"[secreto]");
            if (!String.IsNullOrEmpty(controller.Settings.Username)) result = result.Replace(controller.Settings.Username,"[cuenta]");
            return result;
        }
        void ApplyStatus(Status status) {
            if (status == null) status = new Status(Light.Gray,"Esperando","Sin información todavía.");
            string message = SafeText(status.Message), info = SafeText(status.Detail); if (String.IsNullOrWhiteSpace(message)) message = "Esperando";
            Color color = Palette.ForLight(status.Light); stateDot.BackColor = color; stateDot.Invalidate(); stateLabel.Text = message;
            stateTime.Text = "Último estado · " + status.Time.ToLocalTime().ToString("HH:mm:ss") + (preview ? " · Vista previa" : ""); detail.Text = info; indicator.SetStatus(status.Light,message,info);
            Icon image;if(!icons.TryGetValue(status.Light,out image)){image=MakeIcon(color);icons[status.Light]=image;}if(trayImage!=image){tray.Icon=image;main.Icon=image;trayImage=image;}
            string tip = "AutoNauta · " + message; tray.Text = tip.Length > 63 ? tip.Substring(0,63) : tip;
            string entry = message + (info.Length > 0 ? " — " + info.Replace("\r"," ").Replace("\n"," ") : "");
            if (entry != previousLog) { previousLog = entry; if(entry.Length > 1600) entry = entry.Substring(0,1600) + "…"; log.Items.Insert(0,"[" + status.Time.ToLocalTime().ToString("HH:mm:ss") + "] " + entry); while(log.Items.Count > 50) log.Items.RemoveAt(log.Items.Count - 1); }
        }
        void RefreshAccountHint() { accountHint.Text = !String.IsNullOrEmpty(controller.Settings.ProtectedPassword) ? "Contraseña guardada. Déjala vacía para conservarla." : "Guarda tu cuenta para activar la reconexión."; }
        void RefreshActions() {
            toggle.Text = toggleMenu.Text = controller.Running ? "Pausar" : "Iniciar";
            automatic.Text="Login automático: "+(controller.Settings.AutoLoginEnabled?"ON":"OFF");automaticMenu.Checked=controller.Settings.AutoLoginEnabled;pinnedMenu.Checked=controller.Settings.IndicatorPinned;indicator.SetAutomatic(controller.Settings.AutoLoginEnabled);
            bool enabled = !preview && !exiting && pending == 0; toggle.Enabled = toggleMenu.Enabled = checkMenu.Enabled = check.Enabled = save.Enabled = clear.Enabled = enabled;
            automatic.Enabled=automaticMenu.Enabled=pinnedMenu.Enabled=enabled;
        }
        void ToggleAutomatic(){bool enabled=!controller.Settings.AutoLoginEnabled;RunCommand(delegate{controller.SetAutoLogin(enabled);},null);}
        void TogglePinned(){bool pinned=!controller.Settings.IndicatorPinned;RunCommand(delegate{controller.Settings.IndicatorPinned=pinned;controller.SaveSettings();},delegate{indicator.SetPinned(pinned);PersistPosition();});}
        void ToggleRunning() { if(preview) return; bool wasRunning = controller.Running; RunCommand(delegate { if(wasRunning) controller.Pause(); else controller.Start(); },null); }
        void SaveAccount() {
            string user = username.Text.Trim(), secret = password.Text;
            if(user.Length == 0) { detail.Text = "Escribe tu cuenta Nauta antes de guardar."; username.Focus(); return; }
            if(secret.Length == 0 && String.IsNullOrEmpty(controller.Settings.ProtectedPassword)) { detail.Text = "Escribe la contraseña de esta cuenta antes de guardar."; password.Focus(); return; }
            if(secret.Length == 0 && !String.Equals(user,controller.Settings.Username,StringComparison.OrdinalIgnoreCase)) { detail.Text = "Para cambiar de cuenta, escribe su contraseña."; password.Focus(); return; }
            enteredSecret = secret; RunCommand(delegate { controller.SaveAccount(user,secret); },delegate { password.Clear(); RefreshAccountHint(); });
        }
        void RunCommand(Action action,Action complete) {
            if(preview || exiting || pending != 0) return; pending = 1; RefreshActions();
            ThreadPool.QueueUserWorkItem(delegate {
                bool success = false;
                try { lock(commandGate) { if(!exiting) { action(); success = true; } } }
                catch(Exception) { Post(delegate { detail.Text = "No se pudo completar la acción. Consulta el estado de conexión e inténtalo de nuevo."; }); }
                Post(delegate { pending = 0; if(success && complete != null) complete(); RefreshActions(); });
            });
        }
        void HideMain() { main.Hide(); }
        void ShowMain(bool activate) { if(exiting) return; indicator.EnsureVisible(); main.Show(); if(main.WindowState == FormWindowState.Minimized) main.WindowState = FormWindowState.Normal;ApplyMetrics(controller.Metrics); if(activate) main.Activate(); }
        public void MinimizeMain(){HideMain();}
        void DisplaySettingsChanged(object sender,EventArgs e) { Post(delegate { if(!exiting) PositionChanged(); }); }
        void PositionChanged() { indicator.MoveAnchor(ClampPosition(indicator.AnchorLocation,indicator.CollapsedSize)); positionTimer.Stop(); positionTimer.Start(); }
        void PersistPosition() {
            Point p = ClampPosition(indicator.AnchorLocation,indicator.CollapsedSize); controller.Settings.IndicatorX = p.X; controller.Settings.IndicatorY = p.Y;
            if(preview) return; ThreadPool.QueueUserWorkItem(delegate { try { lock(commandGate) { if(!exiting) controller.SaveSettings(); } } catch(Exception) { } });
        }
        internal static Point ClampPosition(Point requested,Size size) {
            Rectangle area;
            if(requested.X == -1 && requested.Y == -1) { area = Screen.PrimaryScreen.WorkingArea; return new Point(Math.Max(area.Left,area.Right - size.Width - 22),Math.Max(area.Top,area.Bottom - size.Height - 22)); }
            area = Screen.FromPoint(new Point(requested.X + size.Width / 2,requested.Y + size.Height / 2)).WorkingArea;
            return new Point(Math.Max(area.Left,Math.Min(Math.Max(area.Left,area.Right - size.Width),requested.X)),Math.Max(area.Top,Math.Min(Math.Max(area.Top,area.Bottom - size.Height),requested.Y)));
        }
        void ExitRequested() {
            if(exiting) return; exiting = true; controller.StatusChanged -= StatusChanged;controller.MetricsChanged-=MetricsChanged; positionTimer.Stop(); RefreshActions(); detail.Text = "Cerrando AutoNauta…";
            Point p = ClampPosition(indicator.AnchorLocation,indicator.CollapsedSize); controller.Settings.IndicatorX = p.X; controller.Settings.IndicatorY = p.Y;
            ThreadPool.QueueUserWorkItem(delegate {
                try { lock(commandGate) { try { if(!preview) controller.SaveSettings(); } finally { controller.Shutdown(); } } }
                catch(Exception) { }
                finally { Post(delegate { shutdownCompleted = true; ExitThread(); }); }
            });
        }
        protected override void ExitThreadCore() { if(main.InvokeRequired) { Post(delegate { ExitThread(); }); return; } if(preview) { try { controller.Shutdown(); } finally { Cleanup(); base.ExitThreadCore(); } return; } if(!shutdownCompleted) { ExitRequested(); return; } Cleanup(); base.ExitThreadCore(); }
        protected override void Dispose(bool disposing) { if(disposing) Cleanup(); base.Dispose(disposing); }
        void Cleanup() {
            if(cleaned) return; cleaned = true; exiting = true; controller.StatusChanged -= StatusChanged;controller.MetricsChanged-=MetricsChanged; Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;
            positionTimer.Stop(); positionTimer.Dispose(); tray.Visible = false; tray.Dispose(); indicator.Dispose(); menu.Dispose(); main.Dispose();foreach(var image in icons.Values)image.Dispose();icons.Clear();trayImage=null;
        }
        public void Smoke(string folder) {
            if(Thread.CurrentThread.GetApartmentState() != ApartmentState.STA) throw new InvalidOperationException("La prueba visual necesita STA.");
            if(!preview) throw new InvalidOperationException("La prueba visual debe ejecutarse en vista previa, sin red.");
            Directory.CreateDirectory(folder); Status original = controller.Current; Point location = indicator.AnchorLocation;
            Assert(main.Visible && indicator.Visible,"Principal e indicador deben ser visibles inicialmente."); Assert(password.Text.Length == 0 && password.UseSystemPasswordChar,"La contraseña debe estar vacía y enmascarada."); Assert(indicator.TopMost,"Indicador topmost.");
            main.WindowState = FormWindowState.Minimized; Application.DoEvents(); Assert(!main.Visible && indicator.Visible,"Minimizar oculta solo principal.");
            ShowMain(false); Application.DoEvents(); Assert(main.Visible && main.WindowState == FormWindowState.Normal,"Restaurar principal. Visible=" + main.Visible + ", Estado=" + main.WindowState);
            long style=GetWindowLongPtr(indicator.Handle,-16).ToInt64(),extended=GetWindowLongPtr(indicator.Handle,-20).ToInt64();
            Assert((style&0x00030000)==0&&(extended&0x08000080)==0x08000080,"El LED debe ser una herramienta no activable y sin estilos de minimizar/maximizar.");
            Point restoreAnchor=indicator.AnchorLocation;SendMessage(indicator.Handle,0x0112,new IntPtr(0xF020),IntPtr.Zero);Application.DoEvents();
            Assert(indicator.WindowState==FormWindowState.Normal&&indicator.Visible,"El LED debe ignorar la orden de minimizar del shell.");
            HideMain();indicator.WindowState=FormWindowState.Minimized;Application.DoEvents();
            Assert(indicator.WindowState==FormWindowState.Normal&&indicator.Visible&&!main.Visible&&indicator.AnchorLocation==restoreAnchor,"Minimización externa recupera solo el LED y conserva su posición.");
            for(int i=0;i<20;i++){indicator.WindowState=FormWindowState.Minimized;Application.DoEvents();Assert(indicator.WindowState==FormWindowState.Normal&&indicator.Visible&&indicator.AnchorLocation==restoreAnchor,"La recuperación debe seguir funcionando después de minimizar repetidamente.");}
            indicator.Hide();indicator.TopMost=false;IntPtr beforeRecovery=GetForegroundWindow();restoreIndicatorMenu.PerformClick();Application.DoEvents();
            Assert(indicator.Visible&&indicator.TopMost&&!main.Visible&&GetForegroundWindow()==beforeRecovery,"Recuperar LED restaura el indicador sin abrir la ventana ni tomar el foco.");
            indicator.SetPinned(true);indicator.SetExpanded(true,true);indicator.WindowState=FormWindowState.Minimized;Application.DoEvents();
            Assert(indicator.WindowState==FormWindowState.Normal&&indicator.Pinned&&indicator.Expanded&&indicator.Width==468,"Recuperar conserva la barra fijada y su tamaño.");
            indicator.SetPinned(false);indicator.SetExpanded(false,true);indicator.Hide();ShowMain(false);Application.DoEvents();
            Assert(main.Visible&&indicator.Visible&&indicator.WindowState==FormWindowState.Normal,"Mostrar AutoNauta también recupera un LED oculto.");
            HideMain();indicator.SetExpanded(false,true);Application.DoEvents();
            for(int i=0;i<1000;i++)MetricsChanged(this,EventArgs.Empty);
            Assert(metricsPending==0,"El modo LED no debe encolar actualizaciones de controles ocultos.");
            int invalidations=0;InvalidateEventHandler observed=delegate{invalidations++;};indicator.Invalidated+=observed;
            for(int i=0;i<200;i++)indicator.SetMetrics(new Telemetry{RatesReady=true,DownloadBytesPerSecond=i*1000});
            Assert(invalidations==0,"El LED no debe redibujarse por cambios de velocidad invisibles.");
            indicator.SetExpanded(true,true);var stable=new Telemetry{RatesReady=true,DownloadBytesPerSecond=10000,UploadBytesPerSecond=5000};indicator.SetMetrics(stable);invalidations=0;
            for(int i=0;i<200;i++)indicator.SetMetrics(stable);
            Assert(invalidations==0,"Datos visibles sin cambios no deben provocar repintados.");
            stable.DownloadBytesPerSecond=20000;indicator.SetMetrics(stable);Assert(invalidations>0,"Una velocidad visible modificada debe redibujarse.");
            indicator.Invalidated-=observed;indicator.SetExpanded(false,true);ShowMain(false);
            Point clamped = ClampPosition(new Point(Int32.MaxValue / 2,Int32.MaxValue / 2),indicator.CollapsedSize); indicator.MoveAnchor(clamped);
            Assert(Screen.FromPoint(clamped).WorkingArea.Contains(new Rectangle(clamped,indicator.Size)),"Posición fuera de monitor queda visible.");
            foreach(Light light in new Light[] {Light.Gray,Light.Green,Light.Yellow,Light.Red}) {
                ApplyStatus(new Status(light,"Prueba · " + light,light == Light.Red ? "Acceso directo bloqueado por VPN/Windows" : "Vista previa sin red. Estado de prueba."));
                Assert(indicator.CurrentLight == light && stateDot.BackColor == Palette.ForLight(light),"Color incorrecto: " + light);
                Snapshot(indicator,Path.Combine(folder,"ui-indicator-" + light.ToString().ToLowerInvariant() + ".png"));
            }
            for(int i=0;i<200;i++)ApplyStatus(new Status(Light.Green,"Internet conectado","Estado estable"));Assert(icons.Count<=4,"El icono debe reutilizarse y no crear recursos por cada estado.");
            indicator.MoveAnchor(location);ApplyStatus(new Status(Light.Green,"Internet conectado","Datos de ejemplo para comprobar el diseño; sin red."));
            var example=new Telemetry{Remaining=TimeSpan.FromMinutes(125*60+9),DownloadBytesPerSecond=250000,UploadBytesPerSecond=37500,RatesReady=true,AccountCheckedUtc=DateTime.UtcNow,AccountNote="Datos de ejemplo; vista previa."};indicator.ReadMetrics=delegate{return example;};ApplyMetrics(example);
            indicator.SetPinned(false);indicator.SetExpanded(false,true);Assert(indicator.Width==52&&!indicator.Region.IsVisible(0,0),"El reposo debe ser un LED ovalado, sin esquinas cuadradas.");Point led=indicator.LedScreenPoint;
            Snapshot(indicator,Path.Combine(folder,"ui-led.png"));indicator.SetExpanded(true,true);Assert(indicator.Width==468&&indicator.LedScreenPoint==led,"Expandir debe conservar el LED bajo el mouse.");
            Snapshot(indicator,Path.Combine(folder,"ui-expanded.png"));
            int autoClicks=0,pinClicks=0;indicator.AutoLoginRequested+=delegate{autoClicks++;};indicator.PinRequested+=delegate{pinClicks++;};indicator.TestClick(1);indicator.TestClick(2);Assert(autoClicks==1&&pinClicks==1,"Los botones AUTO y fijar deben emitir acciones separadas.");
            indicator.SetPinned(true);indicator.SetExpanded(false,true);Assert(indicator.Expanded&&indicator.Width==468,"La barra fijada no debe plegarse.");Snapshot(indicator,Path.Combine(folder,"ui-pinned.png"));
            indicator.SetAutomatic(false);Snapshot(indicator,Path.Combine(folder,"ui-manual.png"));indicator.SetAutomatic(true);indicator.SetPinned(false);indicator.SetExpanded(false,true);Assert(!indicator.Expanded&&indicator.Width==52,"Quitar fijación permite volver al LED.");
            Snapshot(main,Path.Combine(folder,"ui-main.png"));indicator.ReadMetrics=delegate{return controller.Metrics;};indicator.SetMetrics(controller.Metrics);ApplyStatus(original);RefreshActions();Snapshot(indicator,Path.Combine(folder,"ui-indicator.png"));
        }
        static void Assert(bool condition,string message) { if(!condition) throw new InvalidOperationException("UI smoke: " + message); }
        static void Snapshot(Form form,string path) { using(Bitmap b = new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(b,new Rectangle(Point.Empty,form.Size));if(form is IndicatorForm)for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)if(!form.Region.IsVisible(x,y))b.SetPixel(x,y,Color.Transparent);b.Save(path,System.Drawing.Imaging.ImageFormat.Png); } }
        static Icon MakeIcon(Color color) {
            using(Bitmap b = new Bitmap(32,32)) {
                using(Graphics g = Graphics.FromImage(b)) { g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent); using(Brush bg = new SolidBrush(Palette.Background)) g.FillEllipse(bg,1,1,30,30); using(Brush dot = new SolidBrush(color)) g.FillEllipse(dot,7,7,18,18); }
                IntPtr handle = b.GetHicon(); try { using(Icon temporary = Icon.FromHandle(handle)) return (Icon)temporary.Clone(); } finally { DestroyIcon(handle); }
            }
        }
        [DllImport("user32.dll",SetLastError = true)] static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    }
    internal sealed class DarkButton : Button {
        protected override void OnPaint(PaintEventArgs e) {
            if(Enabled) { base.OnPaint(e); return; }
            e.Graphics.Clear(BackColor);
            using(Pen border = new Pen(Palette.Border)) e.Graphics.DrawRectangle(border,0,0,Width - 1,Height - 1);
            TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Palette.Muted,TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }
    internal static class Palette {
        internal static readonly Color Background = Color.FromArgb(20,23,28), Surface = Color.FromArgb(30,35,42), Text = Color.FromArgb(234,238,243), Muted = Color.FromArgb(155,166,180), Border = Color.FromArgb(57,65,77);
        internal static Color ForLight(Light light) { switch(light) { case Light.Green: return Color.FromArgb(65,203,140); case Light.Yellow: return Color.FromArgb(245,191,73); case Light.Red: return Color.FromArgb(244,102,110); default: return Color.FromArgb(146,158,173); } }
    }
    internal sealed class IndicatorForm : Form {
        const int CompactWidth=52,ExpandedWidth=468,BarHeight=42;
        readonly ToolTip tooltip=new ToolTip();
        readonly System.Windows.Forms.Timer animation=new System.Windows.Forms.Timer{Interval=15};
        readonly System.Windows.Forms.Timer leaveTimer=new System.Windows.Forms.Timer{Interval=180};
        Point anchor,dragCursor,dragLocation;
        bool dragging,moved,expanded,pinned,expandLeft,automatic=true,restorePending;
        int targetWidth=CompactWidth,pressedControl;
        string information="";
        Telemetry metrics=new Telemetry();
        string cachedTime="—h —m",cachedDownload="— Kb/s",cachedUpload="— Kb/s",cachedTooltip="";
        internal Light CurrentLight {get;private set;}
        internal event EventHandler OpenMain,MovedByUser,AutoLoginRequested,PinRequested;
        internal event EventHandler ExpansionChanged;
        internal Func<Telemetry> ReadMetrics;
        internal Size CollapsedSize{get{return new Size(CompactWidth,BarHeight);}}
        internal Point AnchorLocation{get{return anchor;}}
        internal bool Expanded{get{return expanded;}}
        internal bool Pinned{get{return pinned;}}
        internal Point LedScreenPoint{get{return new Point(Location.X+(expandLeft?Width-CompactWidth/2:CompactWidth/2),Location.Y+BarHeight/2);}}
        internal void TestClick(int control){var rectangle=control==1?AutomaticBounds:PinBounds;var mouse=new MouseEventArgs(MouseButtons.Left,1,rectangle.X+rectangle.Width/2,rectangle.Y+rectangle.Height/2,0);OnMouseDown(mouse);OnMouseUp(mouse);}
        protected override bool ShowWithoutActivation {get {return true;}}
        protected override CreateParams CreateParams {get {CreateParams p = base.CreateParams; p.Style &= ~0x00030000; p.ExStyle |= 0x08000000 | 0x00000080; return p;}}
        internal IndicatorForm(ContextMenuStrip menu) {
            Text="AutoNauta";FormBorderStyle=FormBorderStyle.None;MinimizeBox=false;MaximizeBox=false;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;Size=CollapsedSize;BackColor=Palette.Surface;ForeColor=Palette.Text;Font=new Font("Segoe UI",9F);ContextMenuStrip=menu;DoubleBuffered=true;Cursor=Cursors.SizeAll;AutoScaleMode=AutoScaleMode.None;
            tooltip.InitialDelay=1200;tooltip.ReshowDelay=300;tooltip.AutoPopDelay=12000;tooltip.ShowAlways=true;
            animation.Tick+=delegate{int delta=targetWidth-Width;if(Math.Abs(delta)<=2){ResizeBar(targetWidth);animation.Stop();}else ResizeBar(Width+Math.Sign(delta)*Math.Max(2,(int)(Math.Abs(delta)*.32)));};
            leaveTimer.Tick+=delegate{if(pinned){leaveTimer.Stop();return;}if(dragging||ContextMenuStrip.Visible||MouseOverBar())return;leaveTimer.Stop();SetExpanded(false,false);};
            MouseEnter+=delegate{leaveTimer.Stop();SetExpanded(true,false);};
            MouseLeave+=delegate{leaveTimer.Start();};
            MouseDown+=delegate(object sender,MouseEventArgs e){if(e.Button!=MouseButtons.Left)return;pressedControl=HitControl(e.Location);if(pressedControl!=0)return;dragging=true;moved=false;dragCursor=Cursor.Position;dragLocation=anchor;Capture=true;};
            MouseMove+=delegate(object sender,MouseEventArgs e){Cursor=HitControl(e.Location)==0?Cursors.SizeAll:Cursors.Hand;if(!dragging)return;Point p=Cursor.Position;int dx=p.X-dragCursor.X,dy=p.Y-dragCursor.Y;if(Math.Abs(dx)+Math.Abs(dy)>3)moved=true;if(moved)MoveAnchor(new Point(dragLocation.X+dx,dragLocation.Y+dy));};
            MouseUp += delegate(object sender,MouseEventArgs e) {
                if(e.Button!=MouseButtons.Left)return;
                if(pressedControl!=0){int action=pressedControl;pressedControl=0;if(HitControl(e.Location)==action){var handler=action==1?AutoLoginRequested:PinRequested;if(handler!=null)handler(this,EventArgs.Empty);}return;}
                if(!dragging)return;dragging=false;Capture=false;if(moved&&MovedByUser!=null)MovedByUser(this,EventArgs.Empty);
            };
            MouseDoubleClick += delegate(object sender,MouseEventArgs e) {if(e.Button == MouseButtons.Left && HitControl(e.Location)==0 && OpenMain != null) OpenMain(this,EventArgs.Empty);};
            MouseCaptureChanged += delegate {if(!Capture) dragging = false;};
            ResizeBar(CompactWidth);
        }
        // Show Desktop can minimize otherwise borderless forms. Restore on an
        // actual minimize event only; never poll or activate the foreground window.
        protected override void WndProc(ref Message message){
            if(message.Msg==0x0112&&(message.WParam.ToInt64()&0xFFF0)==0xF020){message.Result=IntPtr.Zero;return;}
            base.WndProc(ref message);
        }
        protected override void OnResize(EventArgs e){
            base.OnResize(e);
            if(WindowState!=FormWindowState.Minimized||restorePending||!IsHandleCreated||Disposing||IsDisposed)return;
            restorePending=true;
            try{BeginInvoke((MethodInvoker)delegate{restorePending=false;if(!Disposing&&!IsDisposed)EnsureVisible();});}
            catch(InvalidOperationException){restorePending=false;}
        }
        internal void EnsureVisible(){
            if(Disposing||IsDisposed)return;
            if(WindowState!=FormWindowState.Normal)WindowState=FormWindowState.Normal;
            animation.Stop();ResizeBar(targetWidth);
            if(!Visible)Show();
            TopMost=true;
            SetWindowPos(Handle,new IntPtr(-1),0,0,0,0,0x0001|0x0002|0x0010|0x0040);
            if(expanded&&ReadMetrics!=null)SetMetrics(ReadMetrics());
            if(expanded&&!pinned&&!MouseOverBar())leaveTimer.Start();
        }
        [DllImport("user32.dll",SetLastError=true)] static extern bool SetWindowPos(IntPtr window,IntPtr insertAfter,int x,int y,int width,int height,uint flags);
        internal void MoveAnchor(Point point){anchor=UiContext.ClampPosition(point,CollapsedSize);Rectangle area=Screen.FromPoint(anchor).WorkingArea;expandLeft=anchor.X+ExpandedWidth>area.Right;ResizeBar(Width);}
        bool MouseOverBar(){return Region!=null&&Region.IsVisible(PointToClient(Cursor.Position));}
        internal void SetPinned(bool value){pinned=value;if(value){leaveTimer.Stop();SetExpanded(true,false);}else if(!MouseOverBar())SetExpanded(false,false);Invalidate();}
        internal void SetAutomatic(bool value){if(automatic==value)return;automatic=value;UpdateTooltip();if(expanded)Invalidate();}
        void UpdateTooltip(){string text=TooltipText();if(text!=cachedTooltip){cachedTooltip=text;tooltip.SetToolTip(this,text);}}
        internal void SetMetrics(Telemetry value){
            metrics=value??new Telemetry();if(!expanded)return;
            string time=RateMonitor.TimeText(metrics.Remaining),download=metrics.RatesReady?RateMonitor.Speed(metrics.DownloadBytesPerSecond):"— Kb/s",upload=metrics.RatesReady?RateMonitor.Speed(metrics.UploadBytesPerSecond):"— Kb/s";
            bool changed=time!=cachedTime||download!=cachedDownload||upload!=cachedUpload;cachedTime=time;cachedDownload=download;cachedUpload=upload;UpdateTooltip();if(changed)Invalidate();
        }
        internal void SetStatus(Light light,string text,string info) {bool changed=CurrentLight!=light;CurrentLight=light;information=info??"";if(changed)Invalidate();}
        internal void SetExpanded(bool value,bool immediate){
            bool old=expanded;expanded=value||pinned;
            if(expanded&&ReadMetrics!=null)SetMetrics(ReadMetrics());
            if(old!=expanded){var changed=ExpansionChanged;if(changed!=null)changed(this,EventArgs.Empty);}
            Rectangle area=Screen.FromPoint(new Point(anchor.X+CompactWidth/2,anchor.Y+BarHeight/2)).WorkingArea;
            expandLeft=anchor.X+ExpandedWidth>area.Right;
            targetWidth=expanded?Math.Min(ExpandedWidth,area.Width):CompactWidth;
            if(immediate){animation.Stop();ResizeBar(targetWidth);}else if(Width!=targetWidth)animation.Start();
        }
        void ResizeBar(int width){
            Rectangle area=Screen.FromPoint(new Point(anchor.X+CompactWidth/2,anchor.Y+BarHeight/2)).WorkingArea;
            Size=new Size(width,BarHeight);Location=new Point(Math.Max(area.Left,Math.Min(area.Right-width,expandLeft?anchor.X+CompactWidth-width:anchor.X)),anchor.Y);
            using(var path=Capsule(new Rectangle(0,0,width,BarHeight))){var old=Region;Region=new Region(path);if(old!=null)old.Dispose();}Invalidate();
        }
        internal Rectangle AutomaticBounds{get{return new Rectangle((expandLeft?16:48)+312,9,56,24);}}
        internal Rectangle PinBounds{get{return new Rectangle((expandLeft?16:48)+380,7,26,28);}}
        int HitControl(Point p){if(Width<ExpandedWidth-5)return 0;if(AutomaticBounds.Contains(p))return 1;if(PinBounds.Contains(p))return 2;return 0;}
        string TooltipText(){return "AutoNauta\n"+metrics.AccountNote+(metrics.AccountCheckedUtc==DateTime.MinValue?"":"\nÚltima consulta: "+metrics.AccountCheckedUtc.ToLocalTime().ToString("HH:mm:ss"))+"\nLogin automático: "+(automatic?"activado":"desactivado")+"\nFijar barra: botón de chincheta o clic derecho.";}
        static GraphicsPath Capsule(Rectangle r){var path=new GraphicsPath();int diameter=r.Height-1;path.AddArc(r.X,r.Y,diameter,diameter,90,180);path.AddArc(r.Right-diameter-1,r.Y,diameter,diameter,270,180);path.CloseFigure();return path;}
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);e.Graphics.Clear(BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=Capsule(new Rectangle(0,0,Width,Height)))using(Brush background=new SolidBrush(Palette.Surface))using(Pen border=new Pen(Palette.Border)){e.Graphics.FillPath(background,path);e.Graphics.DrawPath(border,path);}
            int ledX=expandLeft?Width-CompactWidth/2:CompactWidth/2;
            using(Brush halo=new SolidBrush(Color.FromArgb(38,Palette.ForLight(CurrentLight))))e.Graphics.FillEllipse(halo,ledX-11,10,22,22);
            using(Brush dot=new SolidBrush(Palette.ForLight(CurrentLight)))e.Graphics.FillEllipse(dot,ledX-6,15,12,12);
            if(Width<110)return;
            e.Graphics.SetClip(new Rectangle(expandLeft?0:CompactWidth,0,Width-CompactWidth,Height));
            int x=expandLeft?16:48;
            DrawText(e.Graphics,cachedTime,new Rectangle(x,9,94,24),Palette.Text);
            DrawText(e.Graphics,"↓ "+cachedDownload,new Rectangle(x+98,9,104,24),Color.FromArgb(149,206,241));
            DrawText(e.Graphics,"↑ "+cachedUpload,new Rectangle(x+206,9,104,24),Palette.Muted);
            using(var path=Capsule(AutomaticBounds))using(Brush fill=new SolidBrush(automatic?Color.FromArgb(35,71,57):Color.FromArgb(43,47,54)))e.Graphics.FillPath(fill,path);
            DrawText(e.Graphics,automatic?"AUTO":"MAN",AutomaticBounds,automatic?Palette.ForLight(Light.Green):Palette.Muted);
            var pin=PinBounds;using(Pen pen=new Pen(pinned?Color.FromArgb(149,206,241):Palette.Muted,1.5f)){
                e.Graphics.DrawLine(pen,pin.X+9,pin.Y+7,pin.X+18,pin.Y+7);e.Graphics.DrawLine(pen,pin.X+11,pin.Y+7,pin.X+11,pin.Y+13);e.Graphics.DrawLine(pen,pin.X+16,pin.Y+7,pin.X+16,pin.Y+13);e.Graphics.DrawLine(pen,pin.X+8,pin.Y+15,pin.X+19,pin.Y+15);e.Graphics.DrawLine(pen,pin.X+13,pin.Y+15,pin.X+13,pin.Y+23);
            }
        }
        void DrawText(Graphics graphics,string text,Rectangle area,Color color){TextRenderer.DrawText(graphics,text,Font,area,color,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis);}
        protected override void Dispose(bool disposing) {if(disposing){tooltip.Dispose();animation.Dispose();leaveTimer.Dispose();} base.Dispose(disposing);}
    }
}






