using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
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
        readonly MainWindow main;
        readonly IndicatorForm indicator;
        readonly NotifyIcon tray;
        readonly ContextMenuStrip menu;
        readonly ToolStripMenuItem toggleMenu, checkMenu,automaticMenu,pinnedMenu,restoreIndicatorMenu;
        readonly System.Windows.Forms.Timer positionTimer;
        readonly object commandGate = new object();
        readonly TextBox username, password;
        readonly Telemetry previewMetrics;
        Icon trayImage;
        readonly Dictionary<Light,Icon> icons=new Dictionary<Light,Icon>();
        volatile bool metricsVisible;
        int metricsPending;
        string previousLog, enteredSecret = String.Empty, detailTip, noteTip;
        volatile bool exiting;
        bool cleaned, shutdownCompleted, editingAccount, hintWarning, smokeInteractive;
        int pending;

        public UiContext(IController controller, bool preview)
        {
            if (controller == null) throw new ArgumentNullException("controller");
            this.controller = controller; this.preview = preview;
            if (preview) {
                Theme.OverrideScale(Environment.GetEnvironmentVariable("AUTONAUTA_UI_SCALE"));
                // The preview never reads the network: fixed example values show the layout.
                previewMetrics = new Telemetry { Remaining = TimeSpan.FromMinutes(125 * 60 + 9), DownloadBytesPerSecond = 250000, UploadBytesPerSecond = 37500, RatesReady = true, AccountCheckedUtc = DateTime.UtcNow, AccountNote = "Datos de ejemplo de la vista previa." };
            }
            main = new MainWindow(preview);
            main.FormClosing += delegate(object sender, FormClosingEventArgs e) {
                if (!exiting) { e.Cancel = true; if (e.CloseReason == CloseReason.UserClosing) HideMain(); else ExitRequested(); }
            };
            main.Resize += delegate { if (!exiting && main.WindowState == FormWindowState.Minimized) HideMain(); };
            username = main.UserField.Input; password = main.PasswordField.Input; password.UseSystemPasswordChar = true;
            username.Text = controller.Settings.Username ?? ""; main.UserField.Cue = "usuario@nauta.com.cu";
            menu = new ContextMenuStrip { Renderer = new MenuRenderer(), Font = Theme.Body, BackColor = Palette.Raised, ForeColor = Palette.Text, ShowImageMargin = false, ShowCheckMargin = true };
            var showMenu = new ToolStripMenuItem("Mostrar AutoNauta"); showMenu.Click += delegate { ShowMain(true); }; menu.Items.Add(showMenu);
            restoreIndicatorMenu = new ToolStripMenuItem("Recuperar LED"); restoreIndicatorMenu.Click += delegate { if(!exiting) indicator.EnsureVisible(); }; menu.Items.Add(restoreIndicatorMenu);
            menu.Items.Add(new ToolStripSeparator());
            toggleMenu = new ToolStripMenuItem("Iniciar vigilancia"); toggleMenu.Click += delegate { ToggleRunning(); }; menu.Items.Add(toggleMenu);
            checkMenu = new ToolStripMenuItem("Comprobar ahora"); checkMenu.Click += delegate { RunCommand(delegate { controller.CheckNow(); },null); }; menu.Items.Add(checkMenu);
            automaticMenu=new ToolStripMenuItem("Login automático");automaticMenu.Click+=delegate{ToggleAutomatic();};menu.Items.Add(automaticMenu);
            pinnedMenu=new ToolStripMenuItem("Mantener barra expandida");pinnedMenu.Click+=delegate{TogglePinned();};menu.Items.Add(pinnedMenu);
            menu.Items.Add(new ToolStripSeparator()); var exitMenu = new ToolStripMenuItem("Salir"); exitMenu.Click += delegate { ExitRequested(); }; menu.Items.Add(exitMenu);
            foreach (ToolStripItem item in menu.Items) item.Padding = new Padding(0,Theme.S(2),0,Theme.S(2));
            indicator = new IndicatorForm(menu); indicator.OpenMain += delegate { ShowMain(true); }; indicator.MovedByUser += delegate { PositionChanged(); };
            indicator.ReadMetrics=CurrentMetrics;indicator.ExpansionChanged+=delegate{RefreshMetricsVisibility();};main.VisibleChanged+=delegate{RefreshMetricsVisibility();};
            indicator.AutoLoginRequested+=delegate{ToggleAutomatic();};indicator.PinRequested+=delegate{TogglePinned();};indicator.SetPinned(controller.Settings.IndicatorPinned);
            indicator.MoveAnchor(ClampPosition(StoredAnchor(),indicator.CollapsedSize));
            tray = new NotifyIcon { ContextMenuStrip = menu, Text = "AutoNauta" }; tray.DoubleClick += delegate { ShowMain(true); };
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
            positionTimer = new System.Windows.Forms.Timer { Interval = 650 }; positionTimer.Tick += delegate { positionTimer.Stop(); PersistPosition(); };
            main.Minimize.Click += delegate { HideMain(); }; main.Exit.Click += delegate { ExitRequested(); }; main.Toggle.Click += delegate { ToggleRunning(); };
            main.Check.Click += delegate { RunCommand(delegate { controller.CheckNow(); },null); }; main.Save.Click += delegate { SaveAccount(); };
            main.Automatic.Click+=delegate{ToggleAutomatic();};
            main.Clear.Click += delegate { ClearAccount(); };
            main.EditAccount.Click += delegate { BeginEditAccount(); }; main.CancelEdit.Click += delegate { CancelEditAccount(); };
            KeyEventHandler fieldKeys = delegate(object sender, KeyEventArgs e) {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SaveAccount(); }
                else if (e.KeyCode == Keys.Escape && main.CancelEdit.Visible) { e.SuppressKeyPress = true; CancelEditAccount(); }
            };
            username.KeyDown += fieldKeys; password.KeyDown += fieldKeys;
            EventHandler edited = delegate { if (hintWarning) RefreshAccountHint(); };
            username.TextChanged += edited; password.TextChanged += edited;
            // Create a dispatch handle before subscribing to worker status events.
            IntPtr handle = main.Handle; controller.StatusChanged += StatusChanged;controller.MetricsChanged+=MetricsChanged;
            ApplyStatus(controller.Current);ApplyMetrics(CurrentMetrics()); RefreshAccount(); RefreshActions(); tray.Visible = true; indicator.Show(); main.FitToContent(); main.Show();ApplyMetrics(CurrentMetrics());
        }
        Telemetry CurrentMetrics() { return preview ? previewMetrics.Copy() : controller.Metrics; }
        void StatusChanged(object sender,EventArgs e) { Post(delegate { if (!exiting) { ApplyStatus(controller.Current); RefreshActions(); } }); }
        void MetricsChanged(object sender,EventArgs e){
            if(exiting||!metricsVisible||Interlocked.Exchange(ref metricsPending,1)!=0)return;
            Post(delegate{try{if(!exiting&&metricsVisible)ApplyMetrics(CurrentMetrics());}finally{Interlocked.Exchange(ref metricsPending,0);}});
        }
        void RefreshMetricsVisibility(){metricsVisible=main.Visible||indicator.Expanded;}
        void ApplyMetrics(Telemetry data){
            if(main.Visible){
                bool known = data.Remaining.HasValue;
                Theme.SetText(main.TimeValue,known ? RateMonitor.TimeText(data.Remaining) : "No disponible");
                Color timeColor = known ? Palette.Text : Palette.Secondary; if (main.TimeValue.ForeColor != timeColor) main.TimeValue.ForeColor = timeColor;
                Theme.SetText(main.DownValue,data.RatesReady ? RateMonitor.Speed(data.DownloadBytesPerSecond) : "—");
                Theme.SetText(main.UpValue,data.RatesReady ? RateMonitor.Speed(data.UploadBytesPerSecond) : "—");
                string note = SafeText(TimeNote(data)); Theme.SetText(main.TimeNote,note);
                if (note != noteTip) { noteTip = note; main.Tips.SetToolTip(main.TimeNote,ActivityList.Wrap(note,80)); }
            }
            indicator.SetMetrics(data);
        }
        // Never derived from CUP credit: an unknown time is reported as unknown.
        static string TimeNote(Telemetry data) {
            if (data.Remaining.HasValue) return "Consultado a las " + data.AccountCheckedUtc.ToLocalTime().ToString("HH:mm") + ". " + (data.AccountNote ?? "");
            if (data.Credit.HasValue) return "Nauta solo informa saldo en CUP; el tiempo aparecerá cuando AutoNauta inicie una sesión nueva.";
            return String.IsNullOrEmpty(data.AccountNote) ? "El tiempo aparecerá cuando AutoNauta inicie una sesión nueva." : data.AccountNote;
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
            Color color = Palette.ForLight(status.Light); main.StateDot.Color = color; main.StateDot.AccessibleName = "Estado: " + message;
            Theme.SetText(main.StateLabel,message); Theme.SetText(main.StateTime,"Desde las " + status.Time.ToLocalTime().ToString("HH:mm:ss"));
            Theme.SetText(main.StateDetail,info); if (info != detailTip) { detailTip = info; main.Tips.SetToolTip(main.StateDetail,ActivityList.Wrap(info,80)); }
            indicator.SetStatus(status.Light,message,info);
            Icon image;if(!icons.TryGetValue(status.Light,out image)){image=MakeIcon(color);icons[status.Light]=image;}if(trayImage!=image){tray.Icon=image;main.Icon=image;trayImage=image;}
            string tip = "AutoNauta · " + message; tray.Text = tip.Length > 63 ? tip.Substring(0,63) : tip;
            string flat = info.Replace("\r"," ").Replace("\n"," "), entry = message + (flat.Length > 0 ? " — " + flat : "");
            if (entry != previousLog) {
                previousLog = entry; if (flat.Length > 1600) flat = flat.Substring(0,1600) + "…";
                main.Log.Items.Insert(0,new ActivityEntry(status.Time.ToLocalTime(),status.Light,message,flat)); while(main.Log.Items.Count > 50) main.Log.Items.RemoveAt(main.Log.Items.Count - 1);
            }
        }
        bool HasSavedAccount { get { return !String.IsNullOrEmpty(controller.Settings.ProtectedPassword); } }
        // The summary shows enough of the account to recognize it, not the full name.
        static string MaskedAccount(string user) {
            if (String.IsNullOrEmpty(user)) return "Cuenta guardada";
            int at = user.IndexOf('@'); string local = at < 0 ? user : user.Substring(0,at), domain = at < 0 ? "" : user.Substring(at);
            return local.Substring(0,Math.Min(local.Length > 3 ? 2 : 1,local.Length)) + "•••" + domain;
        }
        void RefreshAccount() {
            bool saved = HasSavedAccount;
            main.SetAccountExpanded(editingAccount || !saved,saved);
            Theme.SetText(main.AccountSummary,saved ? MaskedAccount(controller.Settings.Username) + " · protegida por Windows" : "");
            main.PasswordField.Cue = saved ? "Déjala vacía para conservar la guardada" : "Contraseña de la cuenta";
            RefreshAccountHint();
        }
        void RefreshAccountHint() {
            SetHint(HasSavedAccount ? "Escribe una contraseña solo si quieres cambiarla." : "Usa la cuenta completa de navegación. Se guarda en esta computadora, protegida por Windows.",false);
        }
        void SetHint(string text,bool warning) {
            hintWarning = warning; Theme.SetText(main.AccountHint,text);
            Color color = warning ? Palette.Warning : Palette.Muted; if (main.AccountHint.ForeColor != color) main.AccountHint.ForeColor = color;
        }
        void BeginEditAccount() {
            if (exiting) return; editingAccount = true; username.Text = controller.Settings.Username ?? ""; password.Clear(); RefreshAccount();
            main.Canvas.ScrollControlIntoView(main.Save); username.Focus(); username.SelectAll();
        }
        void CancelEditAccount() {
            if (!HasSavedAccount) return; editingAccount = false; password.Clear(); username.Text = controller.Settings.Username ?? ""; RefreshAccount();
        }
        void RefreshActions() {
            bool running = controller.Running, automatic = controller.Settings.AutoLoginEnabled;
            Theme.SetText(main.Toggle,running ? "Pausar" : "Iniciar"); main.Toggle.Kind = running ? ButtonKind.Secondary : ButtonKind.Primary;
            toggleMenu.Text = running ? "Pausar vigilancia" : "Iniciar vigilancia";
            Theme.SetText(main.WatchDescription,running ? "Activa · revisa la conexión continuamente." : "En pausa · no comprueba ni reconecta.");
            main.Automatic.Checked = automatic;
            Theme.SetText(main.AutoDescription,automatic ? "AUTO · inicia sesión si Nauta pide acceso." : "MAN · vigila, pero no inicia sesión.");
            automaticMenu.Checked=automatic;pinnedMenu.Checked=controller.Settings.IndicatorPinned;indicator.SetAutomatic(automatic);
            bool enabled = (!preview || smokeInteractive) && !exiting && pending == 0;
            main.Toggle.Enabled = toggleMenu.Enabled = checkMenu.Enabled = main.Check.Enabled = main.Save.Enabled = enabled;
            main.Clear.Enabled = enabled && (HasSavedAccount || !String.IsNullOrEmpty(controller.Settings.Username));
            main.Automatic.Enabled=automaticMenu.Enabled=pinnedMenu.Enabled=enabled;
            main.EditAccount.Enabled = main.CancelEdit.Enabled = !exiting;
        }
        void ToggleAutomatic(){bool enabled=!controller.Settings.AutoLoginEnabled;RunCommand(delegate{controller.SetAutoLogin(enabled);},null);}
        void TogglePinned(){bool pinned=!controller.Settings.IndicatorPinned;RunCommand(delegate{controller.Settings.IndicatorPinned=pinned;controller.SaveSettings();},delegate{indicator.SetPinned(pinned);PersistPosition();});}
        void ToggleRunning() { if(preview) return; bool wasRunning = controller.Running; RunCommand(delegate { if(wasRunning) controller.Pause(); else controller.Start(); },null); }
        void SaveAccount() {
            if(preview || exiting || pending != 0 || !main.AccountExpanded) return;
            string user = username.Text.Trim(), secret = password.Text;
            if(user.Length == 0) { SetHint("Escribe tu cuenta Nauta antes de guardar.",true); username.Focus(); return; }
            if(secret.Length == 0 && !HasSavedAccount) { SetHint("Escribe la contraseña de esta cuenta antes de guardar.",true); password.Focus(); return; }
            if(secret.Length == 0 && !String.Equals(user,controller.Settings.Username,StringComparison.OrdinalIgnoreCase)) { SetHint("Para cambiar de cuenta, escribe su contraseña.",true); password.Focus(); return; }
            enteredSecret = secret;
            RunCommand(delegate { controller.SaveAccount(user,secret); },delegate { password.Clear(); editingAccount = false; RefreshAccount(); },
                delegate(Exception error) { SetHint(error is ArgumentException ? SafeText(error.Message) : "No se pudo guardar la cuenta. Inténtalo de nuevo.",true); });
        }
        void ClearAccount() {
            if(preview || exiting || pending != 0) return;
            var answer = MessageBox.Show(main,"¿Eliminar la cuenta y la contraseña guardadas en esta computadora?\n\nAutoNauta pausará la vigilancia. No se cierra una sesión Nauta abierta.","Eliminar cuenta guardada",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2);
            if(answer != DialogResult.Yes) return;
            RunCommand(delegate { controller.ClearAccount(); },delegate { username.Clear(); password.Clear(); enteredSecret = ""; editingAccount = false; RefreshAccount(); });
        }
        void RunCommand(Action action,Action complete,Action<Exception> failed = null) {
            if(preview || exiting || pending != 0) return; pending = 1; RefreshActions();
            ThreadPool.QueueUserWorkItem(delegate {
                bool success = false;
                try { lock(commandGate) { if(!exiting) { action(); success = true; } } }
                catch(Exception error) { Post(delegate { if (failed != null) failed(error); else Theme.SetText(main.StateDetail,"No se pudo completar la acción. Consulta el estado de conexión e inténtalo de nuevo."); }); }
                Post(delegate { pending = 0; if(success && complete != null) complete(); RefreshActions(); });
            });
        }
        void HideMain() { main.Hide(); }
        void ShowMain(bool activate) { if(exiting) return; indicator.EnsureVisible(); main.Show(); if(main.WindowState == FormWindowState.Minimized) main.WindowState = FormWindowState.Normal;ApplyMetrics(CurrentMetrics()); if(activate) main.Activate(); }
        public void MinimizeMain(){HideMain();}
        void DisplaySettingsChanged(object sender,EventArgs e) { Post(delegate { if(!exiting) PositionChanged(); }); }
        void PositionChanged() { indicator.MoveAnchor(ClampPosition(indicator.AnchorLocation,indicator.CollapsedSize)); positionTimer.Stop(); positionTimer.Start(); }
        // Positions are stored in 96-DPI units, as the earlier DPI-unaware versions
        // saved them, so an existing position keeps its place on screen.
        Point StoredAnchor() {
            int x = controller.Settings.IndicatorX, y = controller.Settings.IndicatorY; if (x == -1 && y == -1) return new Point(-1,-1);
            float k = Theme.DpiScale; return new Point((int)Math.Round(x * k),(int)Math.Round(y * k));
        }
        void StoreAnchor(Point p) { float k = Theme.DpiScale; controller.Settings.IndicatorX = (int)Math.Round(p.X / k); controller.Settings.IndicatorY = (int)Math.Round(p.Y / k); }
        void PersistPosition() {
            StoreAnchor(ClampPosition(indicator.AnchorLocation,indicator.CollapsedSize));
            if(preview) return; ThreadPool.QueueUserWorkItem(delegate { try { lock(commandGate) { if(!exiting) controller.SaveSettings(); } } catch(Exception) { } });
        }
        internal static Point ClampPosition(Point requested,Size size) {
            Rectangle area;
            if(requested.X == -1 && requested.Y == -1) { area = Screen.PrimaryScreen.WorkingArea; return new Point(Math.Max(area.Left,area.Right - size.Width - Theme.S(22)),Math.Max(area.Top,area.Bottom - size.Height - Theme.S(22))); }
            area = Screen.FromPoint(new Point(requested.X + size.Width / 2,requested.Y + size.Height / 2)).WorkingArea;
            return new Point(Math.Max(area.Left,Math.Min(Math.Max(area.Left,area.Right - size.Width),requested.X)),Math.Max(area.Top,Math.Min(Math.Max(area.Top,area.Bottom - size.Height),requested.Y)));
        }
        void ExitRequested() {
            if(exiting) return; exiting = true; controller.StatusChanged -= StatusChanged;controller.MetricsChanged-=MetricsChanged; positionTimer.Stop(); RefreshActions(); Theme.SetText(main.StateDetail,"Cerrando AutoNauta…");
            StoreAnchor(ClampPosition(indicator.AnchorLocation,indicator.CollapsedSize));
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
            positionTimer.Stop(); positionTimer.Dispose(); tray.Visible = false; tray.Dispose(); indicator.Dispose(); menu.Dispose(); main.Tips.Dispose(); main.Dispose();foreach(var image in icons.Values)image.Dispose();icons.Clear();trayImage=null;
            Theme.Release();
        }
        public void Smoke(string folder) {
            if(Thread.CurrentThread.GetApartmentState() != ApartmentState.STA) throw new InvalidOperationException("La prueba visual necesita STA.");
            if(!preview) throw new InvalidOperationException("La prueba visual debe ejecutarse en vista previa, sin red.");
            Directory.CreateDirectory(folder); Status original = controller.Current; Point location = indicator.AnchorLocation;
            Assert(main.Visible && indicator.Visible,"Principal e indicador deben ser visibles inicialmente."); Assert(password.Text.Length == 0 && password.UseSystemPasswordChar,"La contraseña debe estar vacía y enmascarada."); Assert(indicator.TopMost,"Indicador topmost.");
            Assert(indicator.Bounds == indicator.LayeredBounds,"La ventana del LED debe coincidir con la superficie dibujada.");
            main.WindowState = FormWindowState.Minimized; Application.DoEvents(); Assert(!main.Visible && indicator.Visible,"Minimizar oculta solo principal.");
            ShowMain(false); Application.DoEvents(); Assert(main.Visible && main.WindowState == FormWindowState.Normal,"Restaurar principal. Visible=" + main.Visible + ", Estado=" + main.WindowState);
            long style=GetWindowLongPtr(indicator.Handle,-16).ToInt64(),extended=GetWindowLongPtr(indicator.Handle,-20).ToInt64();
            Assert((style&0x00030000)==0&&(extended&0x08000080)==0x08000080,"El LED debe ser una herramienta no activable y sin estilos de minimizar/maximizar.");
            Assert((extended&0x00080000)!=0,"El LED debe usar transparencia por píxel para bordes suaves.");
            Point restoreAnchor=indicator.AnchorLocation;SendMessage(indicator.Handle,0x0112,new IntPtr(0xF020),IntPtr.Zero);Application.DoEvents();
            Assert(indicator.WindowState==FormWindowState.Normal&&indicator.Visible,"El LED debe ignorar la orden de minimizar del shell.");
            HideMain();indicator.WindowState=FormWindowState.Minimized;Application.DoEvents();
            Assert(indicator.WindowState==FormWindowState.Normal&&indicator.Visible&&!main.Visible&&indicator.AnchorLocation==restoreAnchor,"Minimización externa recupera solo el LED y conserva su posición.");
            for(int i=0;i<20;i++){indicator.WindowState=FormWindowState.Minimized;Application.DoEvents();Assert(indicator.WindowState==FormWindowState.Normal&&indicator.Visible&&indicator.AnchorLocation==restoreAnchor,"La recuperación debe seguir funcionando después de minimizar repetidamente.");}
            Assert(indicator.Bounds == indicator.LayeredBounds,"La recuperación debe conservar la superficie del LED.");
            indicator.Hide();indicator.TopMost=false;IntPtr beforeRecovery=GetForegroundWindow();restoreIndicatorMenu.PerformClick();Application.DoEvents();
            Assert(indicator.Visible&&indicator.TopMost&&!main.Visible&&GetForegroundWindow()==beforeRecovery,"Recuperar LED restaura el indicador sin abrir la ventana ni tomar el foco.");
            indicator.SetPinned(true);indicator.SetExpanded(true,true);indicator.WindowState=FormWindowState.Minimized;Application.DoEvents();
            Assert(indicator.WindowState==FormWindowState.Normal&&indicator.Pinned&&indicator.Expanded&&indicator.CurrentWidth==indicator.ExpandedWidth&&indicator.Bounds==indicator.LayeredBounds,"Recuperar conserva la barra fijada y su tamaño. Ventana="+indicator.Bounds+", superficie="+indicator.LayeredBounds+", ancho="+indicator.CurrentWidth);
            indicator.SetPinned(false);indicator.SetExpanded(false,true);indicator.Hide();ShowMain(false);Application.DoEvents();
            Assert(main.Visible&&indicator.Visible&&indicator.WindowState==FormWindowState.Normal,"Mostrar AutoNauta también recupera un LED oculto.");
            HideMain();indicator.SetExpanded(false,true);Application.DoEvents();
            for(int i=0;i<1000;i++)MetricsChanged(this,EventArgs.Empty);
            Assert(metricsPending==0,"El modo LED no debe encolar actualizaciones de controles ocultos.");
            int renders=indicator.RenderCount;
            for(int i=0;i<200;i++)indicator.SetMetrics(new Telemetry{RatesReady=true,DownloadBytesPerSecond=i*1000});
            Assert(indicator.RenderCount==renders,"El LED no debe redibujarse por cambios de velocidad invisibles.");
            indicator.SetExpanded(true,true);var stable=new Telemetry{RatesReady=true,DownloadBytesPerSecond=10000,UploadBytesPerSecond=5000};indicator.SetMetrics(stable);renders=indicator.RenderCount;
            for(int i=0;i<200;i++)indicator.SetMetrics(stable);
            Assert(indicator.RenderCount==renders,"Datos visibles sin cambios no deben provocar repintados.");
            stable.DownloadBytesPerSecond=20000;indicator.SetMetrics(stable);Assert(indicator.RenderCount>renders,"Una velocidad visible modificada debe redibujarse.");
            renders=indicator.RenderCount;for(int i=0;i<50;i++)indicator.SetStatus(Light.Green,"Internet conectado","");indicator.SetStatus(Light.Green,"Internet conectado","");
            Assert(indicator.RenderCount<=renders+1,"Un estado repetido no debe redibujar el LED.");
            indicator.SetExpanded(false,true);
            // First round warms the shared caches (one tray icon per color); the second must not grow.
            uint gdiBefore=0,userBefore=0;
            for(int round=0;round<2;round++){
                if(round==1){Application.DoEvents();gdiBefore=GetGuiResources(GetCurrentProcess(),0);userBefore=GetGuiResources(GetCurrentProcess(),1);}
                for(int i=0;i<300;i++){indicator.SetExpanded(true,true);indicator.SetMetrics(new Telemetry{RatesReady=true,DownloadBytesPerSecond=i*997,UploadBytesPerSecond=i*31});indicator.SetHover(i%3);indicator.SetExpanded(false,true);ApplyStatus(new Status((Light)(i%4),"Ciclo "+(i%4),""));}
            }
            Application.DoEvents();
            Assert(GetGuiResources(GetCurrentProcess(),0)<=gdiBefore+4&&GetGuiResources(GetCurrentProcess(),1)<=userBefore+4,"Expandir y redibujar no debe acumular objetos GDI ni de usuario. GDI "+gdiBefore+"→"+GetGuiResources(GetCurrentProcess(),0));
            ShowMain(false);
            Point clamped = ClampPosition(new Point(Int32.MaxValue / 2,Int32.MaxValue / 2),indicator.CollapsedSize); indicator.MoveAnchor(clamped);
            Assert(Screen.FromPoint(clamped).WorkingArea.Contains(new Rectangle(clamped,indicator.Size)),"Posición fuera de monitor queda visible.");
            indicator.SetExpanded(true,true);Rectangle area=Screen.FromPoint(indicator.LedScreenPoint).WorkingArea;
            Assert(area.Contains(indicator.Bounds)&&indicator.LedScreenPoint.X>indicator.Bounds.Left+indicator.Bounds.Width/2,"En la esquina derecha la barra se abre hacia la izquierda y queda dentro de la pantalla.");
            indicator.SetExpanded(false,true);Point corner=ClampPosition(new Point(area.Left,area.Top),indicator.CollapsedSize);indicator.MoveAnchor(corner);Point ledCorner=indicator.LedScreenPoint;indicator.SetExpanded(true,true);
            Assert(area.Contains(indicator.Bounds)&&indicator.LedScreenPoint==ledCorner,"En la esquina izquierda la barra se abre hacia la derecha sin mover el LED.");
            indicator.SetExpanded(false,true);
            foreach(Light light in new Light[] {Light.Gray,Light.Green,Light.Yellow,Light.Red}) {
                ApplyStatus(new Status(light,"Prueba · " + light,light == Light.Red ? "Acceso directo bloqueado por VPN/Windows" : "Vista previa sin red. Estado de prueba."));
                Assert(indicator.CurrentLight == light && main.StateDot.Color == Palette.ForLight(light),"Color incorrecto: " + light);
                indicator.SaveSnapshot(Path.Combine(folder,"ui-indicator-" + light.ToString().ToLowerInvariant() + ".png"));
            }
            for(int i=0;i<200;i++)ApplyStatus(new Status(Light.Green,"Internet conectado","Estado estable"));Assert(icons.Count<=4,"El icono debe reutilizarse y no crear recursos por cada estado.");
            indicator.MoveAnchor(location);ApplyStatus(new Status(Light.Green,"Internet conectado","Internet confirmado. Datos de ejemplo; sin red."));
            var example=previewMetrics.Copy();indicator.ReadMetrics=delegate{return example;};ApplyMetrics(example);
            indicator.SetPinned(false);indicator.SetExpanded(false,true);
            Assert(indicator.CurrentWidth==indicator.CollapsedSize.Width&&indicator.CollapsedSize.Width>indicator.CollapsedSize.Height&&indicator.AlphaAt(0,0)==0&&indicator.AlphaAt(indicator.CollapsedSize.Width/2,indicator.CollapsedSize.Height/2)==255,"El reposo debe ser un LED en cápsula ovalada, sin esquinas cuadradas.");
            Point led=indicator.LedScreenPoint;
            indicator.SaveSnapshot(Path.Combine(folder,"ui-led.png"));indicator.SetExpanded(true,true);Assert(indicator.CurrentWidth==indicator.ExpandedWidth&&indicator.LedScreenPoint==led,"Expandir debe conservar el LED bajo el mouse.");
            indicator.SaveSnapshot(Path.Combine(folder,"ui-expanded.png"));
            int autoClicks=0,pinClicks=0;indicator.AutoLoginRequested+=delegate{autoClicks++;};indicator.PinRequested+=delegate{pinClicks++;};indicator.TestClick(1);indicator.TestClick(2);Assert(autoClicks==1&&pinClicks==1,"Los botones AUTO y fijar deben emitir acciones separadas.");
            indicator.SetHover(1);indicator.SaveSnapshot(Path.Combine(folder,"ui-expanded-hover-auto.png"));indicator.SetHover(0);
            indicator.SetPinned(true);indicator.SetExpanded(false,true);Assert(indicator.Expanded&&indicator.CurrentWidth==indicator.ExpandedWidth,"La barra fijada no debe plegarse.");indicator.SaveSnapshot(Path.Combine(folder,"ui-pinned.png"));
            indicator.SetAutomatic(false);indicator.SaveSnapshot(Path.Combine(folder,"ui-manual.png"));
            var unknown=new Telemetry{RatesReady=false,AccountNote="Tiempo disponible al iniciar sesión con AutoNauta."};indicator.ReadMetrics=delegate{return unknown;};indicator.SetMetrics(unknown);indicator.SaveSnapshot(Path.Combine(folder,"ui-expanded-sin-tiempo.png"));
            indicator.ReadMetrics=delegate{return example;};indicator.SetMetrics(example);
            indicator.SetAutomatic(true);indicator.SetPinned(false);indicator.SetExpanded(false,true);Assert(!indicator.Expanded&&indicator.CurrentWidth==indicator.CollapsedSize.Width,"Quitar fijación permite volver al LED.");
            Application.DoEvents();
            Snapshot(main,Path.Combine(folder,"ui-main.png"));
            smokeInteractive=true;RefreshActions();Application.DoEvents();Snapshot(main,Path.Combine(folder,"ui-main-activa.png"));
            ApplyMetrics(unknown);ApplyStatus(new Status(Light.Red,"Error del portal","Nauta devolvió un error al iniciar sesión. Revisa la cuenta guardada o inténtalo más tarde; no se repite el intento automáticamente."));
            SetHint("Introduce tu cuenta completa de navegación, terminada en @nauta.com.cu.",true);Application.DoEvents();Snapshot(main,Path.Combine(folder,"ui-main-error.png"));
            string savedUser=controller.Settings.Username,savedPassword=controller.Settings.ProtectedPassword;
            try{
                // In-memory example only; the preview never saves these values.
                controller.Settings.Username="ejemplo@nauta.com.cu";controller.Settings.ProtectedPassword="vista-previa";
                ApplyMetrics(example);ApplyStatus(new Status(Light.Green,"Internet conectado","Internet confirmado. Datos de ejemplo; sin red."));
                editingAccount=false;RefreshAccount();RefreshActions();Application.DoEvents();
                Assert(!main.AccountExpanded&&main.AccountSummary.Text.StartsWith("ej•••@"),"Con cuenta guardada se muestra un resumen sin la cuenta completa.");
                Snapshot(main,Path.Combine(folder,"ui-main-cuenta-guardada.png"));
                main.EditAccount.PerformClick();Application.DoEvents();
                Assert(main.AccountExpanded&&main.CancelEdit.Visible&&main.PasswordField.Visible,"Cambiar cuenta muestra el formulario y permite cancelar.");
                main.CancelEdit.PerformClick();Application.DoEvents();Assert(!main.AccountExpanded&&password.Text.Length==0,"Cancelar pliega el formulario y descarta la contraseña escrita.");
                Size before=main.ClientSize;main.ClientSize=new Size(Theme.S(420),Theme.S(420));Application.DoEvents();
                Assert(main.Log.Height>0&&(main.Canvas.AutoScrollMinSize.Height>=main.Exit.Bottom-main.Canvas.AutoScrollPosition.Y||main.Exit.Bottom<=main.Canvas.ClientSize.Height),"Una ventana pequeña conserva acceso a todos los controles.");
                Snapshot(main,Path.Combine(folder,"ui-main-compacta.png"));main.ClientSize=before;Application.DoEvents();
            }finally{controller.Settings.Username=savedUser;controller.Settings.ProtectedPassword=savedPassword;editingAccount=false;smokeInteractive=false;RefreshAccount();}
            indicator.ReadMetrics=CurrentMetrics;indicator.SetMetrics(CurrentMetrics());ApplyStatus(original);RefreshActions();indicator.SaveSnapshot(Path.Combine(folder,"ui-indicator.png"));
            menu.Show(main,new Point(Theme.S(40),Theme.S(40)));Application.DoEvents();Snapshot(menu,Path.Combine(folder,"ui-menu.png"));menu.Close();
        }
        static void Assert(bool condition,string message) { if(!condition) throw new InvalidOperationException("UI smoke: " + message); }
        // PrintWindow captures the real frame (dark title bar); DrawToBitmap is the fallback.
        static void Snapshot(Control control,string path) {
            using(Bitmap b = new Bitmap(control.Width,control.Height)) {
                bool printed = false;
                if(control is Form) using(Graphics g = Graphics.FromImage(b)) { IntPtr dc = g.GetHdc(); try { printed = PrintWindow(control.Handle,dc,2); } finally { g.ReleaseHdc(dc); } }
                if(!printed) control.DrawToBitmap(b,new Rectangle(Point.Empty,control.Size));
                b.Save(path,ImageFormat.Png);
            }
        }
        [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr window,IntPtr dc,uint flags);
        static Icon MakeIcon(Color color) {
            using(Bitmap b = new Bitmap(32,32)) {
                using(Graphics g = Graphics.FromImage(b)) { g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent); using(Brush bg = new SolidBrush(Palette.Capsule)) g.FillEllipse(bg,1,1,30,30); using(Pen ring = new Pen(Palette.CapsuleBorder,1.5F)) g.DrawEllipse(ring,1.75F,1.75F,28.5F,28.5F); using(Brush dot = new SolidBrush(color)) g.FillEllipse(dot,7,7,18,18); }
                IntPtr handle = b.GetHicon(); try { using(Icon temporary = Icon.FromHandle(handle)) return (Icon)temporary.Clone(); } finally { DestroyIcon(handle); }
            }
        }
        [DllImport("user32.dll",SetLastError = true)] static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetGuiResources(IntPtr process,uint flags);
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
    }

    // Floating indicator. At rest it is only the LED inside a small capsule; on
    // hover it widens to show time, speeds, AUTO/MAN and the pin. The window is
    // layered: one reusable premultiplied DIB gives anti-aliased edges and is
    // redrawn only when something visible changes.
    internal sealed class IndicatorForm : Form {
        readonly int compactWidth=Theme.S(46),expandedWidth=Theme.S(426),barHeight=Theme.S(30);
        readonly ToolTip tooltip=new ToolTip();
        readonly System.Windows.Forms.Timer animation=new System.Windows.Forms.Timer{Interval=15};
        readonly System.Windows.Forms.Timer leaveTimer=new System.Windows.Forms.Timer{Interval=180};
        readonly StringFormat leftText=new StringFormat(StringFormatFlags.NoWrap){LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter};
        readonly StringFormat centerText=new StringFormat(StringFormatFlags.NoWrap){Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center};
        Point anchor,dragCursor,dragLocation,location;
        bool dragging,moved,expanded,pinned,expandLeft,automatic=true,restorePending,timeKnown,syncPending;
        int targetWidth,currentWidth,pressedControl,hoverControl;
        string statusText="",cachedTime="No disponible",cachedDownload="—",cachedUpload="—",cachedTooltip="";
        Telemetry metrics=new Telemetry();
        IntPtr memoryDc,dib,previousBitmap;
        Bitmap surface;
        Graphics surfaceGraphics;
        internal Light CurrentLight {get;private set;}
        internal int RenderCount {get;private set;}
        internal event EventHandler OpenMain,MovedByUser,AutoLoginRequested,PinRequested;
        internal event EventHandler ExpansionChanged;
        internal Func<Telemetry> ReadMetrics;
        internal Size CollapsedSize{get{return new Size(compactWidth,barHeight);}}
        internal int ExpandedWidth{get{return expandedWidth;}}
        internal int CurrentWidth{get{return currentWidth;}}
        internal Rectangle LayeredBounds{get{return new Rectangle(location,new Size(currentWidth,barHeight));}}
        internal Point AnchorLocation{get{return anchor;}}
        internal bool Expanded{get{return expanded;}}
        internal bool Pinned{get{return pinned;}}
        internal Point LedScreenPoint{get{return new Point(location.X+(expandLeft?currentWidth-compactWidth/2:compactWidth/2),location.Y+barHeight/2);}}
        internal void TestClick(int control){var rectangle=control==1?AutomaticBounds:PinBounds;var mouse=new MouseEventArgs(MouseButtons.Left,1,rectangle.X+rectangle.Width/2,rectangle.Y+rectangle.Height/2,0);OnMouseDown(mouse);OnMouseUp(mouse);}
        internal void SetHover(int control){if(hoverControl==control)return;hoverControl=control;UpdateTooltip();Render();}
        protected override bool ShowWithoutActivation {get {return true;}}
        protected override CreateParams CreateParams {get {CreateParams p = base.CreateParams; p.Style &= ~0x00030000; p.ExStyle |= 0x08000000 | 0x00000080 | 0x00080000; return p;}}
        internal IndicatorForm(ContextMenuStrip menu) {
            Text="AutoNauta";FormBorderStyle=FormBorderStyle.None;MinimizeBox=false;MaximizeBox=false;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.None;
            BackColor=Palette.Capsule;ForeColor=Palette.Text;Font=Theme.IndicatorText;ContextMenuStrip=menu;Cursor=Cursors.SizeAll;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.Opaque,true);
            currentWidth=targetWidth=compactWidth;Size=CollapsedSize;
            tooltip.InitialDelay=900;tooltip.ReshowDelay=300;tooltip.AutoPopDelay=12000;tooltip.ShowAlways=true;
            animation.Tick+=delegate{int delta=targetWidth-currentWidth;if(Math.Abs(delta)<=2){animation.Stop();ResizeBar(targetWidth);}else ResizeBar(currentWidth+Math.Sign(delta)*Math.Max(2,(int)(Math.Abs(delta)*.32)));};
            leaveTimer.Tick+=delegate{if(pinned){leaveTimer.Stop();return;}if(dragging||ContextMenuStrip.Visible||MouseOverBar())return;leaveTimer.Stop();SetExpanded(false,false);};
            MouseEnter+=delegate{leaveTimer.Stop();SetExpanded(true,false);};
            MouseLeave+=delegate{SetHover(0);leaveTimer.Start();};
            MouseDown+=delegate(object sender,MouseEventArgs e){if(e.Button!=MouseButtons.Left)return;pressedControl=HitControl(e.Location);if(pressedControl!=0)return;dragging=true;moved=false;dragCursor=Cursor.Position;dragLocation=anchor;Capture=true;};
            MouseMove+=delegate(object sender,MouseEventArgs e){int hit=dragging?0:HitControl(e.Location);Cursor=hit==0?Cursors.SizeAll:Cursors.Hand;SetHover(hit);if(!dragging)return;Point p=Cursor.Position;int dx=p.X-dragCursor.X,dy=p.Y-dragCursor.Y;if(Math.Abs(dx)+Math.Abs(dy)>3)moved=true;if(moved)MoveAnchor(new Point(dragLocation.X+dx,dragLocation.Y+dy));};
            MouseUp += delegate(object sender,MouseEventArgs e) {
                if(e.Button!=MouseButtons.Left)return;
                if(pressedControl!=0){int action=pressedControl;pressedControl=0;if(HitControl(e.Location)==action){var handler=action==1?AutoLoginRequested:PinRequested;if(handler!=null)handler(this,EventArgs.Empty);}return;}
                if(!dragging)return;dragging=false;Capture=false;if(moved&&MovedByUser!=null)MovedByUser(this,EventArgs.Empty);
            };
            MouseDoubleClick += delegate(object sender,MouseEventArgs e) {if(e.Button == MouseButtons.Left && HitControl(e.Location)==0 && OpenMain != null) OpenMain(this,EventArgs.Empty);};
            MouseCaptureChanged += delegate {if(!Capture) dragging = false;};
            UpdateTooltip();
        }
        // Show Desktop can minimize otherwise borderless forms. Restore on an
        // actual minimize event only; never poll or activate the foreground window.
        protected override void WndProc(ref Message message){
            if(message.Msg==0x0112&&(message.WParam.ToInt64()&0xFFF0)==0xF020){message.Result=IntPtr.Zero;return;}
            base.WndProc(ref message);
            // After a restore WinForms reapplies its remembered bounds; redraw once
            // so the window matches the layered surface again.
            if(message.Msg==0x0047&&!syncPending&&IsHandleCreated&&!IsDisposed&&!IsIconic(Handle)&&Bounds!=LayeredBounds){
                syncPending=true;
                try{BeginInvoke((MethodInvoker)delegate{syncPending=false;if(!IsDisposed&&!Disposing&&!IsIconic(Handle))Render();});}
                catch(InvalidOperationException){syncPending=false;}
            }
        }
        // The layered surface owns the geometry: bounds requested by WinForms
        // (for example its remembered size after a restore) follow the surface.
        protected override void SetBoundsCore(int x,int y,int width,int height,BoundsSpecified specified){
            if(IsHandleCreated&&!IsIconic(Handle)){x=location.X;y=location.Y;width=currentWidth;height=barHeight;specified=BoundsSpecified.All;}
            base.SetBoundsCore(x,y,width,height,specified);
        }
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);ResizeBar(currentWidth);}
        protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);if(Visible)Render();}
        protected override void OnPaintBackground(PaintEventArgs e){}
        protected override void OnPaint(PaintEventArgs e){}
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
        internal void MoveAnchor(Point point){anchor=UiContext.ClampPosition(point,CollapsedSize);Rectangle area=Screen.FromPoint(anchor).WorkingArea;expandLeft=anchor.X+expandedWidth>area.Right;ResizeBar(currentWidth);}
        bool MouseOverBar(){
            if(!IsHandleCreated)return false;
            Point p=PointToClient(Cursor.Position);float r=barHeight/2F;
            if(p.Y<0||p.Y>=barHeight||p.X<0||p.X>=currentWidth)return false;
            float cx=Math.Max(r,Math.Min(currentWidth-r,p.X));return (p.X-cx)*(p.X-cx)+(p.Y-r)*(p.Y-r)<=r*r;
        }
        internal void SetPinned(bool value){bool changed=pinned!=value;pinned=value;if(value){leaveTimer.Stop();SetExpanded(true,false);}else if(!MouseOverBar())SetExpanded(false,false);if(changed){UpdateTooltip();if(expanded)Render();}}
        internal void SetAutomatic(bool value){if(automatic==value)return;automatic=value;UpdateTooltip();if(expanded)Render();}
        void UpdateTooltip(){string text=TooltipText();if(text!=cachedTooltip){cachedTooltip=text;tooltip.SetToolTip(this,text);}}
        internal void SetMetrics(Telemetry value){
            metrics=value??new Telemetry();if(!expanded)return;
            bool known=metrics.Remaining.HasValue;
            string time=known?RateMonitor.TimeText(metrics.Remaining):"No disponible",download=metrics.RatesReady?RateMonitor.Speed(metrics.DownloadBytesPerSecond):"—",upload=metrics.RatesReady?RateMonitor.Speed(metrics.UploadBytesPerSecond):"—";
            bool changed=time!=cachedTime||download!=cachedDownload||upload!=cachedUpload||known!=timeKnown;cachedTime=time;cachedDownload=download;cachedUpload=upload;timeKnown=known;UpdateTooltip();if(changed)Render();
        }
        internal void SetStatus(Light light,string text,string info) {bool changed=CurrentLight!=light;CurrentLight=light;statusText=text??"";UpdateTooltip();if(changed)Render();}
        internal void SetExpanded(bool value,bool immediate){
            bool old=expanded;expanded=value||pinned;
            if(!expanded)hoverControl=0;
            if(expanded&&ReadMetrics!=null)SetMetrics(ReadMetrics());
            if(old!=expanded){var changed=ExpansionChanged;if(changed!=null)changed(this,EventArgs.Empty);}
            Rectangle area=Screen.FromPoint(new Point(anchor.X+compactWidth/2,anchor.Y+barHeight/2)).WorkingArea;
            expandLeft=anchor.X+expandedWidth>area.Right;
            targetWidth=expanded?Math.Min(expandedWidth,area.Width):compactWidth;
            if(immediate){animation.Stop();ResizeBar(targetWidth);}else if(currentWidth!=targetWidth)animation.Start();
        }
        void ResizeBar(int width){
            Rectangle area=Screen.FromPoint(new Point(anchor.X+compactWidth/2,anchor.Y+barHeight/2)).WorkingArea;
            currentWidth=Math.Max(compactWidth,Math.Min(expandedWidth,width));
            location=new Point(Math.Max(area.Left,Math.Min(area.Right-currentWidth,expandLeft?anchor.X+compactWidth-currentWidth:anchor.X)),anchor.Y);
            if(!IsHandleCreated){Bounds=LayeredBounds;return;}
            Render();
        }
        // Content stays fixed relative to the LED while the capsule opens.
        int ContentX{get{return expandLeft?currentWidth-expandedWidth+Theme.S(12):compactWidth-Theme.S(4);}}
        internal Rectangle AutomaticBounds{get{return new Rectangle(ContentX+Theme.S(292),(barHeight-Theme.S(20))/2,Theme.S(50),Theme.S(20));}}
        internal Rectangle PinBounds{get{return new Rectangle(ContentX+Theme.S(348),(barHeight-Theme.S(26))/2,Theme.S(26),Theme.S(26));}}
        int HitControl(Point p){if(currentWidth<expandedWidth-Theme.S(5))return 0;Rectangle a=AutomaticBounds;a.Inflate(Theme.S(3),Theme.S(4));if(a.Contains(p))return 1;if(PinBounds.Contains(p))return 2;return 0;}
        string TooltipText(){
            if(hoverControl==1)return automatic?"Login automático activado (AUTO).\nClic para pasar a manual: AutoNauta vigila, pero no inicia sesión.":"Login manual (MAN): AutoNauta vigila, pero no inicia sesión.\nClic para activar el login automático.";
            if(hoverControl==2)return pinned?"Barra fijada.\nClic para que se repliegue al retirar el mouse.":"Fijar la barra expandida.";
            string time=metrics.Remaining.HasValue?"Tiempo restante: "+RateMonitor.TimeText(metrics.Remaining)+(metrics.AccountCheckedUtc==DateTime.MinValue?"":" (consultado a las "+metrics.AccountCheckedUtc.ToLocalTime().ToString("HH:mm")+")"):"Tiempo restante: no disponible.\n"+ActivityList.Wrap(metrics.AccountNote??"",70);
            return "AutoNauta · "+statusText+"\n"+time+"\nBajada y subida: tráfico total del equipo.\nArrastra para mover · doble clic abre la ventana.";
        }
        void EnsureSurface(){
            if(surface!=null)return;
            var info=new BITMAPINFOHEADER{biSize=40,biWidth=expandedWidth,biHeight=-barHeight,biPlanes=1,biBitCount=32};
            IntPtr screen=GetDC(IntPtr.Zero);try{memoryDc=CreateCompatibleDC(screen);}finally{ReleaseDC(IntPtr.Zero,screen);}
            IntPtr bits;dib=CreateDIBSection(memoryDc,ref info,0,out bits,IntPtr.Zero,0);
            if(dib==IntPtr.Zero){DeleteDC(memoryDc);memoryDc=IntPtr.Zero;throw new InvalidOperationException("No se pudo crear la superficie del indicador.");}
            previousBitmap=SelectObject(memoryDc,dib);
            surface=new Bitmap(expandedWidth,barHeight,expandedWidth*4,PixelFormat.Format32bppPArgb,bits);
            surfaceGraphics=Graphics.FromImage(surface);
        }
        void ReleaseSurface(bool managed){
            if(managed){if(surfaceGraphics!=null)surfaceGraphics.Dispose();if(surface!=null)surface.Dispose();}
            surfaceGraphics=null;surface=null;
            if(memoryDc!=IntPtr.Zero){if(previousBitmap!=IntPtr.Zero)SelectObject(memoryDc,previousBitmap);DeleteDC(memoryDc);memoryDc=IntPtr.Zero;previousBitmap=IntPtr.Zero;}
            if(dib!=IntPtr.Zero){DeleteObject(dib);dib=IntPtr.Zero;}
        }
        void Render(){
            if(!IsHandleCreated||IsDisposed||Disposing)return;
            EnsureSurface();
            Graphics g=surfaceGraphics;g.ResetClip();g.Clear(Color.Transparent);
            Draw(g,currentWidth,barHeight);g.Flush(FlushIntention.Sync);
            var destination=new POINT{X=location.X,Y=location.Y};var size=new SIZE{CX=currentWidth,CY=barHeight};var source=new POINT();
            var blend=new BLENDFUNCTION{BlendOp=0,BlendFlags=0,SourceConstantAlpha=255,AlphaFormat=1};
            UpdateLayeredWindow(Handle,IntPtr.Zero,ref destination,ref size,memoryDc,ref source,0,ref blend,2);
            RenderCount++;
        }
        void Draw(Graphics g,int w,int h){
            g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
            using(GraphicsPath capsule=Theme.Rounded(new RectangleF(.5F,.5F,w-1F,h-1F),(h-1F)/2F)){
                using(Brush fill=new SolidBrush(Palette.Capsule))g.FillPath(fill,capsule);
                using(Pen border=new Pen(Palette.CapsuleBorder,Math.Max(1F,Theme.F(1F))))g.DrawPath(border,capsule);
                Color led=Palette.ForLight(CurrentLight);float lx=expandLeft?w-compactWidth/2F:compactWidth/2F,cy=h/2F,halo=Theme.F(18F),dot=Theme.F(10F);
                using(Brush b=new SolidBrush(Color.FromArgb(56,led)))g.FillEllipse(b,lx-halo/2,cy-halo/2,halo,halo);
                using(Brush b=new SolidBrush(led))g.FillEllipse(b,lx-dot/2,cy-dot/2,dot,dot);
                if(w<compactWidth+Theme.S(24))return;
                g.SetClip(capsule);g.SetClip(expandLeft?new Rectangle(0,0,w-compactWidth+Theme.S(6),h):new Rectangle(compactWidth-Theme.S(6),0,w-compactWidth+Theme.S(6),h),CombineMode.Intersect);
            }
            // Fixed columns: time 112, download 88, upload 88, AUTO/MAN 50 and pin 26.
            int x=ContentX;float cyf=h/2F;
            using(Pen icon=Glyphs.Stroke(Palette.Secondary)){
                Glyphs.Clock(g,icon,x+Theme.F(7F),cyf,Theme.F(12F));
                Glyphs.Arrow(g,icon,x+Theme.S(112)+Theme.F(5F),cyf,Theme.F(11F),true);
                Glyphs.Arrow(g,icon,x+Theme.S(200)+Theme.F(5F),cyf,Theme.F(11F),false);
            }
            using(Brush primary=new SolidBrush(Palette.Text))using(Brush secondary=new SolidBrush(Palette.Secondary)){
                g.DrawString(cachedTime,timeKnown?Theme.IndicatorStrong:Theme.IndicatorText,timeKnown?primary:secondary,new RectangleF(x+Theme.S(18),0,Theme.S(90),h),leftText);
                g.DrawString(cachedDownload,Theme.IndicatorText,primary,new RectangleF(x+Theme.S(126),0,Theme.S(70),h),leftText);
                g.DrawString(cachedUpload,Theme.IndicatorText,primary,new RectangleF(x+Theme.S(214),0,Theme.S(70),h),leftText);
            }
            Rectangle auto=AutomaticBounds;bool hoverAuto=hoverControl==1;
            using(GraphicsPath pill=Theme.Rounded(new RectangleF(auto.X+.5F,auto.Y+.5F,auto.Width-1F,auto.Height-1F),(auto.Height-1F)/2F)){
                if(automatic){using(Brush b=new SolidBrush(Color.FromArgb(hoverAuto?78:46,Palette.Accent)))g.FillPath(b,pill);}
                else{if(hoverAuto)using(Brush b=new SolidBrush(Palette.Hover))g.FillPath(b,pill);using(Pen p=new Pen(Palette.BorderStrong,Math.Max(1F,Theme.F(1F))))g.DrawPath(p,pill);}
            }
            using(Brush b=new SolidBrush(automatic?(hoverAuto?Palette.AccentHover:Palette.Accent):(hoverAuto?Palette.Text:Palette.Secondary)))g.DrawString(automatic?"AUTO":"MAN",Theme.IndicatorStrong,b,new RectangleF(auto.X,auto.Y,auto.Width,auto.Height+Theme.F(1F)),centerText);
            Rectangle pin=PinBounds;
            if(hoverControl==2)using(Brush b=new SolidBrush(Palette.Hover))g.FillEllipse(b,pin);
            Color pinColor=pinned?Palette.Accent:(hoverControl==2?Palette.Text:Palette.Secondary);
            using(Pen p=Glyphs.Stroke(pinColor))using(Brush b=pinned?new SolidBrush(pinColor):null)Glyphs.Pin(g,p,b,pin.X+pin.Width/2F,pin.Y+pin.Height/2F,Theme.F(16F));
        }
        internal int AlphaAt(int x,int y){return surface==null?0:surface.GetPixel(x,y).A;}
        internal void SaveSnapshot(string path){if(surface==null)Render();using(Bitmap copy=surface.Clone(new Rectangle(0,0,currentWidth,barHeight),PixelFormat.Format32bppArgb))copy.Save(path,ImageFormat.Png);}
        protected override void Dispose(bool disposing) {if(disposing){animation.Stop();leaveTimer.Stop();tooltip.Dispose();animation.Dispose();leaveTimer.Dispose();leftText.Dispose();centerText.Dispose();} ReleaseSurface(disposing); base.Dispose(disposing);}

        [StructLayout(LayoutKind.Sequential)] struct POINT{public int X,Y;}
        [StructLayout(LayoutKind.Sequential)] struct SIZE{public int CX,CY;}
        [StructLayout(LayoutKind.Sequential,Pack=1)] struct BLENDFUNCTION{public byte BlendOp,BlendFlags,SourceConstantAlpha,AlphaFormat;}
        [StructLayout(LayoutKind.Sequential)] struct BITMAPINFOHEADER{public int biSize,biWidth,biHeight;public short biPlanes,biBitCount;public int biCompression,biSizeImage,biXPelsPerMeter,biYPelsPerMeter,biClrUsed,biClrImportant;}
        [DllImport("user32.dll",SetLastError=true)] static extern bool SetWindowPos(IntPtr window,IntPtr insertAfter,int x,int y,int width,int height,uint flags);
        [DllImport("user32.dll",SetLastError=true)] static extern bool UpdateLayeredWindow(IntPtr window,IntPtr destinationDc,ref POINT destination,ref SIZE size,IntPtr sourceDc,ref POINT source,int key,ref BLENDFUNCTION blend,int flags);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr window,IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc,IntPtr value);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr value);
        [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr dc,ref BITMAPINFOHEADER info,uint usage,out IntPtr bits,IntPtr section,uint offset);
    }
}
