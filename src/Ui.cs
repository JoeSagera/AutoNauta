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
                previewMetrics = new Telemetry { Remaining = TimeSpan.FromMinutes(125 * 60 + 9), DownloadBytesPerSecond = 250000, UploadBytesPerSecond = 37500, RatesReady = true, AccountCheckedUtc = DateTime.UtcNow, AccountNote = "Datos de ejemplo de la vista previa.",ServiceStartedUtc=DateTime.UtcNow.AddHours(-2),ConnectionDrops=5,ConnectionRecoveries=4,AutoLoginAttempts=3,AutoLoginSuccesses=2 };
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
            restoreIndicatorMenu = new ToolStripMenuItem("Recuperar LED"); restoreIndicatorMenu.Click += delegate { if(!exiting) indicator.EnsureVisible(true); }; menu.Items.Add(restoreIndicatorMenu);
            menu.Items.Add(new ToolStripSeparator());
            var importMenu=new ToolStripMenuItem("Vincular contador del navegador…");importMenu.Click+=delegate{ImportCounter();};menu.Items.Add(importMenu);
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
            main.CopyActivity.Click+=delegate{main.Log.Copy(true);};
            main.Log.FullLogRequested+=delegate{try{string path=Path.Combine(Program.DataPath,"eventos.log");main.Log.ShowText("Registro completo",preview?"Vista previa: no se lee el registro personal.":File.Exists(path)?SafeText(File.ReadAllText(path)):"Todavía no hay un registro guardado.");}catch(IOException){MessageBox.Show(main,"No se pudo leer el registro. Inténtalo de nuevo.","Actividad",MessageBoxButtons.OK,MessageBoxIcon.Information);}};
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
            // Preview only: an example green state to try the lateral notch with the real mouse.
            if (preview && String.Equals(Environment.GetEnvironmentVariable("AUTONAUTA_PREVIEW_STATE"),"verde",StringComparison.OrdinalIgnoreCase)) ApplyStatus(new Status(Light.Green,"Internet conectado","Vista previa: estado de ejemplo, sin red."));
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
                Theme.SetText(main.StatisticsSince,data.ServiceStartedUtc==DateTime.MinValue?"La vigilancia no ha iniciado":"Desde las "+data.ServiceStartedUtc.ToLocalTime().ToString("HH:mm:ss"));
                Theme.SetText(main.DropsValue,data.ConnectionDrops.ToString());Theme.SetText(main.RecoveriesValue,data.ConnectionRecoveries.ToString());Theme.SetText(main.AttemptsValue,data.AutoLoginAttempts.ToString());Theme.SetText(main.SuccessesValue,data.AutoLoginSuccesses.ToString());
            }
            indicator.SetMetrics(data);
        }
        // Never derived from CUP credit: an unknown time is reported as unknown.
        static string TimeNote(Telemetry data) {
            if (data.Remaining.HasValue) return "Consultado a las " + data.AccountCheckedUtc.ToLocalTime().ToString("HH:mm") + ". " + (data.AccountNote ?? "");
            if (data.Credit.HasValue) return "Contador disponible con una sesión de AutoNauta o vinculando la del navegador desde el menú del LED.";
            return String.IsNullOrEmpty(data.AccountNote) ? "Vincula la sesión del navegador desde el menú del LED o espera al próximo login automático." : data.AccountNote;
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
                previousLog = entry;
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
        void ImportCounter(){
            if(preview||exiting||pending!=0)return;
            if(!HasSavedAccount){MessageBox.Show(main,"Guarda primero tu cuenta Nauta.","Vincular contador");return;}
            MessageBox.Show(main,"En la página de Nauta que muestra el tiempo, pulsa Actualizar.\nGuarda esa página desde el menú de Chrome → Guardar como, en formato HTML.\nDespués selecciona el archivo aquí.\n\nLa página contiene datos privados de tu sesión: no la compartas.","Vincular contador del navegador",MessageBoxButtons.OK,MessageBoxIcon.Information);
            using(var dialog=new OpenFileDialog{Title="Selecciona la página de sesión Nauta guardada",Filter="Página HTML|*.html;*.htm",CheckFileExists=true,Multiselect=false}){
                if(dialog.ShowDialog(main)!=DialogResult.OK)return;string path=dialog.FileName;
                RunCommand(delegate{controller.ImportSessionFile(path);},delegate{ApplyMetrics(CurrentMetrics());},delegate(Exception error){MessageBox.Show(main,error is ArgumentException?SafeText(error.Message):"No se pudo consultar el contador. Revisa el acceso al portal e inténtalo de nuevo.","Vincular contador",MessageBoxButtons.OK,MessageBoxIcon.Information);});
            }
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
        void ShowMain(bool activate) { if(exiting) return; indicator.EnsureVisible(false); main.Show(); if(main.WindowState == FormWindowState.Minimized) main.WindowState = FormWindowState.Normal;ApplyMetrics(CurrentMetrics()); if(activate) main.Activate(); }
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
            // The classic checks run with lateral docking off and a pointer far from the
            // LED, so the real mouse and docking delays cannot change their result.
            Point farPointer=new Point(-32000,-32000);indicator.CursorSource=delegate{return farPointer;};indicator.AutoHideEnabled=false;
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
            SmokeOverlayOrder();
            indicator.SetPinned(true);indicator.SetExpanded(true,true);indicator.WindowState=FormWindowState.Minimized;Application.DoEvents();
            Assert(indicator.WindowState==FormWindowState.Normal&&indicator.Pinned&&indicator.Expanded&&indicator.CurrentWidth==indicator.InformationWidth&&indicator.Bounds==indicator.LayeredBounds,"Recuperar conserva la barra fijada informativa y su tamaño. Ventana="+indicator.Bounds+", superficie="+indicator.LayeredBounds+", ancho="+indicator.CurrentWidth);
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
            indicator.SetPinned(true);indicator.SetExpanded(false,true);Assert(indicator.Expanded&&indicator.CurrentWidth==indicator.InformationWidth&&!indicator.ControlsVisible,"La barra fijada conserva información y oculta botones fuera del mouse.");indicator.SaveSnapshot(Path.Combine(folder,"ui-pinned.png"));
            indicator.SetAutomatic(false);indicator.SaveSnapshot(Path.Combine(folder,"ui-manual.png"));
            var unknown=new Telemetry{RatesReady=false,AccountNote="Tiempo disponible al iniciar sesión con AutoNauta."};indicator.ReadMetrics=delegate{return unknown;};indicator.SetMetrics(unknown);indicator.SaveSnapshot(Path.Combine(folder,"ui-expanded-sin-tiempo.png"));
            indicator.ReadMetrics=delegate{return example;};indicator.SetMetrics(example);
            indicator.SetAutomatic(true);indicator.SetPinned(false);indicator.SetExpanded(false,true);Assert(!indicator.Expanded&&indicator.CurrentWidth==indicator.CollapsedSize.Width,"Quitar fijación permite volver al LED.");
            Application.DoEvents();
            Snapshot(main,Path.Combine(folder,"ui-main.png"));
            Action<string> oldCopy=main.Log.CopySink;string copied=null;
            try{
                main.Log.CopySink=text=>copied=text;main.Log.ClearSelected();main.Log.SetSelected(0,true);if(main.Log.Items.Count>1)main.Log.SetSelected(1,true);
                main.Log.TestKey(Keys.Control|Keys.C);Assert(copied==main.Log.CopyText(false)&&copied.Contains(" | "),"Ctrl+C copia entradas completas seleccionadas, sin elipsis de pantalla.");
                main.CopyActivity.PerformClick();Assert(copied==main.Log.CopyText(true),"Copiar actividad incluye todas las filas.");
                main.Log.TestKey(Keys.Control|Keys.A);Assert(main.Log.SelectedIndices.Count==main.Log.Items.Count,"Ctrl+A selecciona todas las entradas.");
            }finally{main.Log.CopySink=oldCopy;main.Log.ClearSelected();}
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
                string longDetail="Conexión comprobada a través de la interfaz de ejemplo. El programa permanece esperando. "+new string('x',300);
                main.Log.Items.Insert(0,new ActivityEntry(DateTime.Now,Light.Red,"No se pudo continuar","Fase: guardar contador del login. Tipo: IOException. Código: 0x80070497. Diagnóstico de ejemplo, sin datos personales."));
                main.Log.Items.Insert(0,new ActivityEntry(DateTime.Now,Light.Green,"Internet conectado",longDetail));
                main.Log.TopIndex=0;
                Assert(main.Log.CopyText(true).Contains(longDetail),"La copia conserva el detalle largo aunque la pantalla lo abrevie.");
                Size beforeWide=main.ClientSize;main.ClientSize=new Size(Theme.S(820),Theme.S(900));Application.DoEvents();
                Assert(main.CopyActivity.Right<=main.Canvas.ClientSize.Width&&main.SuccessesValue.Right<=main.Canvas.ClientSize.Width,"Contadores y copia quedan dentro de la ventana ampliada.");
                Snapshot(main,Path.Combine(folder,"ui-main-ampliada.png"));Snapshot(main.Log,Path.Combine(folder,"ui-actividad-ampliada.png"));
                main.ClientSize=new Size(Theme.S(460),Theme.S(600));Application.DoEvents();main.ClientSize=new Size(Theme.S(820),Theme.S(900));Application.DoEvents();
                main.Log.Update();Snapshot(main,Path.Combine(folder,"ui-main-ampliada-tras-redimensionar.png"));Snapshot(main.Log,Path.Combine(folder,"ui-actividad-tras-redimensionar.png"));main.ClientSize=beforeWide;Application.DoEvents();
                main.EditAccount.PerformClick();Application.DoEvents();
                Assert(main.AccountExpanded&&main.CancelEdit.Visible&&main.PasswordField.Visible,"Cambiar cuenta muestra el formulario y permite cancelar.");
                main.CancelEdit.PerformClick();Application.DoEvents();Assert(!main.AccountExpanded&&password.Text.Length==0,"Cancelar pliega el formulario y descarta la contraseña escrita.");
                Size before=main.ClientSize;main.ClientSize=new Size(Theme.S(420),Theme.S(420));Application.DoEvents();
                Assert(main.Log.Height>0&&(main.Canvas.AutoScrollMinSize.Height>=main.Exit.Bottom-main.Canvas.AutoScrollPosition.Y||main.Exit.Bottom<=main.Canvas.ClientSize.Height),"Una ventana pequeña conserva acceso a todos los controles.");
                Snapshot(main,Path.Combine(folder,"ui-main-compacta.png"));main.ClientSize=before;Application.DoEvents();
            }finally{controller.Settings.Username=savedUser;controller.Settings.ProtectedPassword=savedPassword;editingAccount=false;smokeInteractive=false;RefreshAccount();}
            SmokeLateral(folder,farPointer);
            indicator.MoveAnchor(location);indicator.ReadMetrics=CurrentMetrics;indicator.SetMetrics(CurrentMetrics());ApplyStatus(original);RefreshActions();indicator.SaveSnapshot(Path.Combine(folder,"ui-indicator.png"));
            menu.Show(main,new Point(Theme.S(40),Theme.S(40)));Application.DoEvents();Snapshot(menu,Path.Combine(folder,"ui-menu.png"));menu.Close();Application.DoEvents();
            indicator.CursorSource=delegate{return Cursor.Position;};
        }
        void SmokeOverlayOrder(){
            Assert(indicator.OrderGuard!=null&&indicator.OrderGuard.Listening,"Vigilancia nativa de primer plano y orden registrada.");
            indicator.OrderGuard.TestOwnWindows=true;
            try{
            using(var cover=new Form{Text="Vista previa de ventana superior",FormBorderStyle=FormBorderStyle.None,ShowInTaskbar=false,TopMost=true,StartPosition=FormStartPosition.Manual,Bounds=indicator.Bounds}){
                cover.Show();Application.DoEvents();
                indicator.OrderGuard.TestSettle();NativeSetWindowPos(cover.Handle,IntPtr.Zero,0,0,0,0,0x0001|0x0002|0x0010);
                Assert(Above(cover.Handle,indicator.Handle),"La ventana de prueba debe cubrir el indicador antes de recuperarlo.");
                Rectangle geometry=indicator.Bounds;Point anchor=indicator.AnchorLocation;IntPtr focus=GetForegroundWindow();int renders=indicator.RenderCount,reassertions=indicator.OrderGuard.Reassertions;
                for(int i=0;i<100;i++)indicator.OrderGuard.Request();Application.DoEvents();
                Assert(Above(indicator.Handle,cover.Handle)&&GetForegroundWindow()==focus,"El LED vuelve encima de otra ventana topmost sin cambiar el foco.");
                Assert(indicator.OrderGuard.Reassertions==reassertions+1&&indicator.RenderCount==renders&&indicator.Bounds==geometry&&indicator.AnchorLocation==anchor,"Se agrupan eventos sin redibujar ni mover el indicador.");
                NativeSetWindowPos(cover.Handle,IntPtr.Zero,0,0,0,0,0x0001|0x0002|0x0010);
                Assert(Above(cover.Handle,indicator.Handle),"La ventana vuelve a cubrirlo durante el arranque.");
                indicator.OrderGuard.TestSettle();
                Assert(Above(indicator.Handle,cover.Handle)&&!indicator.OrderGuard.Pending&&GetForegroundWindow()==focus,"La comprobación diferida recupera el orden y se detiene sin tomar foco.");
                indicator.Hide();indicator.OrderGuard.Request();Application.DoEvents();indicator.OrderGuard.TestSettle();
                Assert(!indicator.Visible&&!indicator.OrderGuard.Pending,"La vigilancia no revela una ventana oculta.");
                indicator.EnsureVisible(false);
                using(var guard=new OverlayOrderGuard(cover,delegate{return false;})){
                    Assert(guard.Listening,"La vigilancia temporal registra ambos eventos.");guard.Request();guard.Dispose();Application.DoEvents();guard.Request();
                    Assert(!guard.Listening&&!guard.Pending,"Cerrar libera eventos y temporizador aunque haya una llamada encolada.");
                }
            }
            }finally{indicator.OrderGuard.TestOwnWindows=false;}
        }
        static bool Above(IntPtr first,IntPtr second){
            IntPtr next=first;for(int i=0;i<10000&&next!=IntPtr.Zero;i++){next=GetWindow(next,2);if(next==second)return true;}return false;
        }
        // Lateral docking in green: both sides, hover, delays, state changes during
        // the animation, pin, menu, drag, «Recuperar LED», Show Desktop and resources.
        void SmokeLateral(string folder,Point far) {
            IndicatorForm ind=indicator; Rectangle area=Screen.FromPoint(ind.LedScreenPoint).WorkingArea;
            ind.CursorSource=delegate{return far;};
            var example=previewMetrics.Copy();ind.ReadMetrics=delegate{return example;};
            ind.SetPinned(false);ind.SetExpanded(false,true);ind.SetStatus(Light.Gray,"Pausado","");ind.AutoHideEnabled=true;
            Size capsule=ind.CollapsedSize;int depth=ind.NotchDepth;
            Assert(capsule.Width<=Theme.S(34)&&capsule.Height<=Theme.S(28)&&capsule.Width>capsule.Height,"La cápsula replegada debe ser pequeña y ovalada.");
            // Pure geometry: negative coordinates and a left taskbar keep the notch on its own monitor.
            var leftMonitor=new Rectangle(-1920,0,1920,1040);var sideTaskbar=new Rectangle(1968,0,1712,1080);
            Assert(!IndicatorForm.NearestRight(leftMonitor,-1800,capsule.Width)&&IndicatorForm.DockedX(leftMonitor,false,depth)==-1920,"Monitor con coordenadas negativas: lateral izquierdo propio.");
            Assert(IndicatorForm.NearestRight(leftMonitor,-200,capsule.Width)&&IndicatorForm.DockedX(leftMonitor,true,depth)==-depth,"Monitor con coordenadas negativas: la muesca derecha termina en su borde, no en el monitor vecino.");
            Assert(!IndicatorForm.NearestRight(sideTaskbar,2000,capsule.Width)&&IndicatorForm.DockedX(sideTaskbar,false,depth)==1968,"Con barra de tareas lateral la muesca queda junto a ella, dentro del área útil.");
            foreach(bool right in new bool[]{false,true}) {
                string side=right?"derecha":"izquierda";
                ind.SetStatus(Light.Gray,"Pausado","");
                ind.MoveAnchor(new Point(right?area.Right-area.Width/4:area.Left+area.Width/4,area.Top+area.Height/3));Point anchor=ind.AnchorLocation;
                Assert(!ind.Docked&&!ind.HidePending&&ind.LayeredBounds==new Rectangle(anchor,capsule),"Gris: LED completo visible en su posición ("+side+").");
                Assert(ind.Bounds==ind.LayeredBounds,"La ventana sigue a la superficie al mover ("+side+"). Ventana="+ind.Bounds+", superficie="+ind.LayeredBounds+", error="+ind.LastLayerError+", visible="+ind.Visible+", nativa="+ind.NativeBounds+", estado="+ind.WindowState);
                ind.SetStatus(Light.Green,"Internet conectado","");
                Assert(ind.HidePending&&ind.PendingDelay>=400&&ind.PendingDelay<=600&&!ind.Docked,"Verde espera un breve retardo antes de ocultar ("+side+").");
                ind.TestElapseDelay();Assert(ind.Docked&&ind.Hidden&&ind.Animating,"El ocultamiento es una transición ("+side+").");
                ind.TestAdvance(.5);Assert(ind.LayeredBounds.Width<capsule.Width&&area.Contains(ind.LayeredBounds),"A mitad de la transición sigue dentro de su monitor ("+side+").");
                ind.TestFinishAnimation();Rectangle notch=ind.LayeredBounds;
                Assert(notch.Width==depth&&(right?notch.Right==area.Right:notch.Left==area.Left)&&area.Contains(notch)&&ind.Bounds==notch&&notch.Y==anchor.Y,"La muesca queda en el lateral "+side+", a la altura elegida. Muesca="+notch+", ventana="+ind.Bounds+", área="+area+", ancla="+anchor);
                Assert(ind.AlphaAt(depth/2,capsule.Height/2)==255&&ind.AlphaAt(depth/2,0)==0&&!ind.Expanded&&!ind.HidePending&&!ind.Animating,"Muesca estable, visible y sin temporizadores ("+side+").");
                int renders=ind.RenderCount;for(int i=0;i<100;i++)ind.SetMetrics(new Telemetry{RatesReady=true,DownloadBytesPerSecond=i*1000});
                for(int i=0;i<20;i++)ind.SetStatus(Light.Green,"Internet conectado","");
                Assert(ind.RenderCount==renders,"La muesca no se redibuja por métricas invisibles ni estados repetidos ("+side+").");
                ind.SaveSnapshot(Path.Combine(folder,"ui-lateral-"+side+"-muesca.png"));
                ind.TestHover(true);Assert(ind.Expanded&&!ind.Hidden&&ind.Animating,"Pasar el mouse por la muesca revela la barra ("+side+").");
                ind.TestAdvance(.45);Rectangle opening=ind.LayeredBounds;
                Assert(opening.Contains(notch)&&(right?opening.Right==area.Right:opening.Left==area.Left),"La apertura cubre la muesca: zona continua bajo el cursor ("+side+").");
                ind.SaveSnapshot(Path.Combine(folder,"ui-lateral-"+side+"-apertura.png"));
                ind.TestFinishAnimation();Rectangle bar=ind.LayeredBounds;
                Assert(bar.Width==ind.ExpandedWidth&&area.Contains(bar)&&(right?bar.Right==area.Right&&ind.LedScreenPoint.X>bar.Left+bar.Width/2:bar.Left==area.Left&&ind.LedScreenPoint.X<bar.Left+bar.Width/2),"La barra se abre hacia el interior desde el lateral "+side+".");
                ind.SaveSnapshot(Path.Combine(folder,"ui-lateral-"+side+"-barra.png"));
                ind.TestHover(false);Assert(ind.HidePending&&ind.PendingDelay>=400&&ind.PendingDelay<=600&&ind.Expanded,"Al salir espera antes de volver a la muesca ("+side+").");
                ind.TestElapseDelay();ind.TestAdvance(.3);ind.TestHover(true);
                Assert(ind.Expanded&&!ind.Hidden&&!ind.HidePending,"Volver con el mouse durante el ocultamiento lo invierte sin cerrar ("+side+").");
                ind.TestFinishAnimation();Assert(ind.LayeredBounds==bar,"Tras invertir, la barra queda completa ("+side+").");
                ind.TestHover(false);ind.TestElapseDelay();ind.TestFinishAnimation();Assert(ind.Hidden&&ind.LayeredBounds==notch,"Vuelve a la muesca ("+side+").");
                ind.SetPinned(true);ind.TestFinishAnimation();Point fixedLed=ind.LedScreenPoint;
                Assert(ind.Pinned&&ind.Expanded&&!ind.Hidden&&!ind.ControlsVisible&&ind.CurrentWidth==ind.InformationWidth&&!ind.HidePending&&!ind.Animating,"Fijada sin mouse: solo información y sin temporizadores ("+side+").");
                Assert(ind.TestHitControl(new Point(ind.AutomaticBounds.X+2,ind.AutomaticBounds.Y+2))==0,"AUTO oculto no responde al clic ("+side+").");
                ind.SaveSnapshot(Path.Combine(folder,"ui-pinned-info-"+side+".png"));
                int stableRenders=ind.RenderCount;ind.SetAutomatic(false);ind.SetAutomatic(true);Assert(ind.RenderCount==stableRenders,"AUTO invisible no provoca redibujados ("+side+").");
                ind.TestHover(true);ind.TestFinishAnimation();Assert(ind.ControlsVisible&&ind.CurrentWidth==ind.ExpandedWidth&&ind.LedScreenPoint==fixedLed,"El mouse revela controles sin mover el LED ("+side+").");
                Assert(ind.TestHitControl(new Point(ind.AutomaticBounds.X+2,ind.AutomaticBounds.Y+2))==1&&ind.TestHitControl(new Point(ind.PinBounds.X+2,ind.PinBounds.Y+2))==2,"AUTO y fijar vuelven a responder ("+side+").");
                ind.SaveSnapshot(Path.Combine(folder,"ui-pinned-controls-"+side+".png"));
                ind.TestHover(false);ind.TestFinishAnimation();Assert(ind.CurrentWidth==ind.InformationWidth&&!ind.ControlsVisible&&ind.LedScreenPoint==fixedLed,"Salir conserva cifras y LED y oculta los botones ("+side+").");
                ind.SetPinned(false);ind.TestElapseDelay();ind.TestFinishAnimation();
            }
            // Yellow during the reveal animation: full capsule at the user's position at once.
            Point home=ind.AnchorLocation;
            ind.TestHover(true);ind.TestAdvance(.4);ind.TestHover(false);ind.SetStatus(Light.Yellow,"Esperando recuperación","");
            Assert(!ind.Docked&&!ind.Animating&&!ind.HidePending&&ind.LayeredBounds==new Rectangle(home,capsule)&&ind.AlphaAt(capsule.Width/2,capsule.Height/2)==255,"Amarillo cancela la transición y muestra el LED completo en su posición.");
            ind.SaveSnapshot(Path.Combine(folder,"ui-lateral-amarillo.png"));
            // Back to green, then red in the middle of the trip to the edge.
            ind.SetStatus(Light.Green,"Internet conectado","");Assert(ind.HidePending,"Al recuperar el verde vuelve a programar el ocultamiento.");
            ind.TestElapseDelay();ind.TestAdvance(.5);ind.SetStatus(Light.Red,"Error del portal","");
            Assert(!ind.Docked&&!ind.Animating&&ind.LayeredBounds==new Rectangle(home,capsule),"Rojo durante el ocultamiento muestra el LED completo de inmediato.");
            ind.SetStatus(Light.Gray,"Comprobando conexión","");ind.TestElapseDelay();
            Assert(!ind.Docked&&!ind.HidePending&&ind.LayeredBounds==new Rectangle(home,capsule),"Gris mantiene el LED completo visible.");
            ind.SetStatus(Light.Green,"Internet conectado","");ind.TestElapseDelay();ind.TestFinishAnimation();Assert(ind.Hidden,"Verde sin interacción vuelve a la muesca.");
            // Pin has priority in every state.
            ind.SetPinned(true);ind.TestFinishAnimation();Rectangle pinnedBar=ind.LayeredBounds;
            Assert(ind.Expanded&&!ind.Hidden&&pinnedBar.Width==ind.InformationWidth&&!ind.HidePending,"Fijar fuera del mouse revela la barra informativa en verde.");
            ind.TestElapseDelay();ind.SaveSnapshot(Path.Combine(folder,"ui-lateral-fijada.png"));
            ind.SetStatus(Light.Yellow,"Esperando recuperación","");Assert(ind.Expanded&&!ind.Docked&&ind.CurrentWidth==ind.InformationWidth,"Fijada en amarillo permanece informativa en su posición.");
            ind.SetStatus(Light.Green,"Internet conectado","");Assert(ind.Expanded&&!ind.HidePending,"Fijada en verde no se oculta.");
            ind.SetPinned(false);Assert(ind.HidePending,"Al quitar la fijación vuelve al comportamiento automático.");
            ind.TestElapseDelay();ind.TestFinishAnimation();Assert(ind.Hidden&&!ind.Expanded,"Sin fijación vuelve a la muesca.");
            // Context menu suspends hiding until it closes.
            ind.TestHover(true);ind.TestFinishAnimation();menu.Show(new Point(area.Left+area.Width/2,area.Top+area.Height/2));Application.DoEvents();
            ind.TestHover(false);Assert(ind.MenuOpen&&!ind.HidePending,"Con el menú abierto no se programa el ocultamiento.");
            menu.Close();Application.DoEvents();Assert(!ind.MenuOpen&&ind.HidePending,"Al cerrar el menú se reanuda el ocultamiento.");
            ind.TestElapseDelay();ind.TestFinishAnimation();Assert(ind.Hidden,"Tras el menú vuelve a la muesca.");
            // Drag from the edge: no hiding while dragging; the new position decides the side.
            int moves=0;EventHandler counted=delegate{moves++;};ind.MovedByUser+=counted;
            ind.TestHover(true);ind.TestFinishAnimation();Point savedAnchor=ind.AnchorLocation;
            Point drop=new Point(area.Right-area.Width/3,area.Top+area.Height/2);bool sawDrag=false;
            ind.TestDrag(drop,delegate{sawDrag=ind.Dragging&&!ind.Docked&&!ind.HidePending;ind.TestElapseDelay();sawDrag=sawDrag&&!ind.Hidden;});
            ind.MovedByUser-=counted;
            Assert(sawDrag&&moves==1&&ind.AnchorLocation!=savedAnchor&&!ind.Docked,"El arrastre suspende el ocultamiento y conserva la posición elegida.");
            ind.TestHover(false);ind.TestElapseDelay();ind.TestFinishAnimation();
            Assert(ind.Hidden&&ind.LayeredBounds.Right==area.Right,"Después de mover, se oculta en el lateral más cercano a la nueva posición.");
            // «Recuperar LED» reveals it for a moment without opening the window or taking focus.
            bool mainWasVisible=main.Visible;IntPtr foreground=GetForegroundWindow();restoreIndicatorMenu.PerformClick();Application.DoEvents();
            Assert(!ind.Hidden&&ind.CurrentWidth==capsule.Width&&ind.LayeredBounds.Right==area.Right&&main.Visible==mainWasVisible&&GetForegroundWindow()==foreground,"Recuperar LED revela la cápsula sin abrir la ventana ni tomar el foco.");
            Assert(ind.HidePending&&ind.PendingDelay==IndicatorForm.RevealDelay,"La revelación es temporal.");
            ind.SaveSnapshot(Path.Combine(folder,"ui-lateral-recuperado.png"));
            ind.TestElapseDelay();ind.TestFinishAnimation();Assert(ind.Hidden&&!ind.HidePending,"Tras revelar vuelve a ocultarse una sola vez, sin bucle.");
            // Forced minimize (Show Desktop path) keeps the notch, without revealing or looping.
            Rectangle hiddenBounds=ind.LayeredBounds;ind.WindowState=FormWindowState.Minimized;Application.DoEvents();
            Assert(ind.WindowState==FormWindowState.Normal&&ind.Visible&&ind.Hidden&&ind.LayeredBounds==hiddenBounds&&ind.Bounds==hiddenBounds&&!ind.HidePending,"La recuperación de Mostrar escritorio conserva la muesca.");
            // Resources: after a warm-up round, cycles of reveal/hide and state changes must not grow.
            uint gdiBefore=0,userBefore=0;
            for(int round=0;round<2;round++){
                if(round==1){Application.DoEvents();gdiBefore=GetGuiResources(GetCurrentProcess(),0);userBefore=GetGuiResources(GetCurrentProcess(),1);}
                for(int i=0;i<150;i++){
                    ind.TestHover(true);ind.TestAdvance(.5);ind.SetMetrics(new Telemetry{RatesReady=true,DownloadBytesPerSecond=i*997});ind.TestFinishAnimation();ind.TestHover(false);ind.TestElapseDelay();ind.TestAdvance(.5);
                    if(i%10==0){ind.SetStatus(Light.Yellow,"Esperando","");ind.SetStatus(Light.Green,"Internet conectado","");ind.TestElapseDelay();}
                    ind.TestFinishAnimation();
                }
            }
            Application.DoEvents();
            Assert(GetGuiResources(GetCurrentProcess(),0)<=gdiBefore+4&&GetGuiResources(GetCurrentProcess(),1)<=userBefore+4,"Ocultar y revelar no acumula objetos GDI ni de usuario. GDI "+gdiBefore+"→"+GetGuiResources(GetCurrentProcess(),0));
            Assert(ind.Hidden&&!ind.Animating&&!ind.HidePending,"Al terminar queda estable en la muesca, sin temporizadores activos.");
            ind.AutoHideEnabled=false;Assert(!ind.Docked,"Sin ocultamiento lateral vuelve a la posición habitual.");
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
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window,uint command);
        [DllImport("user32.dll",EntryPoint="SetWindowPos")] static extern bool NativeSetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
        [DllImport("user32.dll")] static extern uint GetGuiResources(IntPtr process,uint flags);
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
    }

    // Floating indicator. At rest it is only the LED inside a small capsule; on
    // hover it widens to show time, speeds, AUTO/MAN and the pin. With a verified
    // (green) connection and no interaction it docks to the nearest side edge of
    // its monitor, leaving a small notch that reveals the bar on hover. The
    // window is layered: one reusable premultiplied DIB gives anti-aliased edges
    // and is redrawn only when something visible changes. Timers run only during
    // a transition or a pending delay.
    internal sealed class IndicatorForm : Form {
        // 96-DPI units: capsule 32×26, bar 356×26 (280 without controls), notch 7×22.
        readonly int compactWidth=Theme.S(32),expandedWidth=Theme.S(356),informationWidth=Theme.S(280),barHeight=Theme.S(26),notchDepth=Math.Max(5,Theme.S(7)),notchHeight=Theme.S(22);
        internal const int DockDelay=500,CollapseDelay=180,RevealDelay=2500;
        readonly ToolTip tooltip=new ToolTip();
        readonly System.Windows.Forms.Timer animation=new System.Windows.Forms.Timer{Interval=15};
        readonly System.Windows.Forms.Timer idleTimer=new System.Windows.Forms.Timer{Interval=DockDelay};
        readonly ContextMenuStrip contextMenu;
        OverlayOrderGuard orderGuard;
        internal OverlayOrderGuard OrderGuard{get{return orderGuard;}}
        readonly StringFormat leftText=new StringFormat(StringFormatFlags.NoWrap){LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter};
        readonly StringFormat centerText=new StringFormat(StringFormatFlags.NoWrap){Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center};
        // X, width and notch amount (0 capsule or bar, 1 notch). Height is constant,
        // so notch and bar share one continuous hover zone.
        struct Geometry{internal int X,W;internal float N;internal bool Same(Geometry o){return X==o.X&&W==o.W&&Math.Abs(N-o.N)<.001F;}}
        Geometry current,from,to;
        long tweenStart;int tweenMs;bool tweening;
        // anchor is the user's capsule position; docking geometry is derived from it and never saved.
        Point anchor,dragCursor,dragLocation,location;
        bool dragging,moved,expanded,pinned,expandLeft,automatic=true,restorePending,timeKnown,syncPending,hovering,menuOpen,docked,hidden,autoHide=true;
        int currentWidth,pressedControl,hoverControl;
        float notch;
        string statusText="",cachedTime="No disponible",cachedDownload="—",cachedUpload="—",cachedTooltip="";
        Telemetry metrics=new Telemetry();
        IntPtr memoryDc,dib,previousBitmap;
        Bitmap surface;
        Graphics surfaceGraphics;
        internal Light CurrentLight {get;private set;}
        internal int RenderCount {get;private set;}
        internal int LastLayerError {get;private set;}
        internal Rectangle NativeBounds{get{RECT r;GetWindowRect(Handle,out r);return Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom);}}
        [StructLayout(LayoutKind.Sequential)] struct RECT{public int Left,Top,Right,Bottom;}
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out RECT rect);
        internal event EventHandler OpenMain,MovedByUser,AutoLoginRequested,PinRequested;
        internal event EventHandler ExpansionChanged;
        internal Func<Telemetry> ReadMetrics;
        // Tests replace the pointer source to stay independent of the real mouse.
        internal Func<Point> CursorSource=delegate{return Cursor.Position;};
        internal Size CollapsedSize{get{return new Size(compactWidth,barHeight);}}
        internal int ExpandedWidth{get{return expandedWidth;}}
        internal int InformationWidth{get{return informationWidth;}}
        internal bool ControlsVisible{get{return expanded&&(!pinned||hovering||dragging||menuOpen);}}
        internal int NotchDepth{get{return notchDepth;}}
        internal int CurrentWidth{get{return currentWidth;}}
        internal Rectangle LayeredBounds{get{return new Rectangle(location,new Size(currentWidth,barHeight));}}
        internal Point AnchorLocation{get{return anchor;}}
        internal bool Expanded{get{return expanded;}}
        internal bool Pinned{get{return pinned;}}
        internal bool Docked{get{return docked;}}
        internal bool Hidden{get{return docked&&hidden;}}
        internal bool Animating{get{return tweening;}}
        internal bool HidePending{get{return idleTimer.Enabled;}}
        internal int PendingDelay{get{return idleTimer.Interval;}}
        internal bool MenuOpen{get{return menuOpen;}}
        internal bool Dragging{get{return dragging;}}
        // Lateral docking in green; the smoke test turns it off for the classic checks.
        internal bool AutoHideEnabled{get{return autoHide;}set{
            if(autoHide==value)return;autoHide=value;
            if(!value){idleTimer.Stop();if(docked){docked=hidden=false;StopTween();Retarget(true);}}
            else if(!hovering)ScheduleIdle(IdleDelay);
        }}
        internal Point LedScreenPoint{get{int side=Math.Min(currentWidth,compactWidth)/2;return new Point(location.X+(expandLeft?currentWidth-side:side),location.Y+barHeight/2);}}
        internal void TestClick(int control){var rectangle=control==1?AutomaticBounds:PinBounds;var mouse=new MouseEventArgs(MouseButtons.Left,1,rectangle.X+rectangle.Width/2,rectangle.Y+rectangle.Height/2,0);OnMouseDown(mouse);OnMouseUp(mouse);}
        internal void TestHover(bool inside){if(inside)PointerEntered();else PointerLeft();}
        internal int TestHitControl(Point point){return HitControl(point);}
        internal void TestElapseDelay(){if(idleTimer.Enabled)IdleTick();}
        internal void TestAdvance(double fraction){if(!tweening)return;tweenStart=System.Diagnostics.Stopwatch.GetTimestamp()-(long)(fraction*tweenMs*System.Diagnostics.Stopwatch.Frequency/1000);Step(fraction);}
        internal void TestFinishAnimation(){if(tweening)Step(1);}
        // Drags from the LED to a screen point through the real mouse handlers.
        internal void TestDrag(Point target,Action whileDragging){
            Point start=LedScreenPoint,cursor=start;Func<Point> previous=CursorSource;CursorSource=delegate{return cursor;};
            try{
                Point client=PointToClient(start);var args=new MouseEventArgs(MouseButtons.Left,1,client.X,client.Y,0);
                OnMouseDown(args);cursor=new Point((start.X+target.X)/2,(start.Y+target.Y)/2);OnMouseMove(args);cursor=target;OnMouseMove(args);
                if(whileDragging!=null)whileDragging();
                OnMouseUp(args);
            }finally{CursorSource=previous;}
        }
        internal void SetHover(int control){if(hoverControl==control)return;hoverControl=control;UpdateTooltip();Render();}
        protected override bool ShowWithoutActivation {get {return true;}}
        protected override CreateParams CreateParams {get {CreateParams p = base.CreateParams; p.Style &= ~0x00030000; p.ExStyle |= 0x08000000 | 0x00000080 | 0x00080000; return p;}}
        internal IndicatorForm(ContextMenuStrip menu) {
            Text="AutoNauta";FormBorderStyle=FormBorderStyle.None;MinimizeBox=false;MaximizeBox=false;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.None;
            BackColor=Palette.Capsule;ForeColor=Palette.Text;Font=Theme.IndicatorText;ContextMenuStrip=menu;Cursor=Cursors.SizeAll;contextMenu=menu;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.Opaque,true);
            current=new Geometry{W=compactWidth};currentWidth=compactWidth;Size=CollapsedSize;
            tooltip.InitialDelay=900;tooltip.ReshowDelay=300;tooltip.AutoPopDelay=12000;tooltip.ShowAlways=true;
            animation.Tick+=delegate{Tick();};
            idleTimer.Tick+=delegate{IdleTick();};
            menu.Opening+=MenuOpening;menu.Closed+=MenuClosed;
            MouseEnter+=delegate{PointerEntered();};
            MouseLeave+=delegate{PointerLeft();};
            MouseDown+=delegate(object sender,MouseEventArgs e){if(e.Button!=MouseButtons.Left)return;pressedControl=HitControl(e.Location);if(pressedControl!=0)return;dragging=true;moved=false;dragCursor=CursorSource();dragLocation=anchor;idleTimer.Stop();Capture=true;};
            MouseMove+=delegate(object sender,MouseEventArgs e){
                int hit=dragging?0:HitControl(e.Location);Cursor=hit==0?Cursors.SizeAll:Cursors.Hand;SetHover(hit);if(!dragging)return;
                Point p=CursorSource();int dx=p.X-dragCursor.X,dy=p.Y-dragCursor.Y;
                if(!moved&&Math.Abs(dx)+Math.Abs(dy)>3){
                    moved=true;
                    // Leave the edge from where the capsule is drawn now, without a jump.
                    if(docked){anchor=UiContext.ClampPosition(VisibleCapsuleLocation(),CollapsedSize);docked=hidden=false;StopTween();dragLocation=new Point(anchor.X-dx,anchor.Y-dy);}
                }
                if(moved)MoveAnchor(new Point(dragLocation.X+dx,dragLocation.Y+dy));
            };
            MouseUp += delegate(object sender,MouseEventArgs e) {
                if(e.Button!=MouseButtons.Left)return;
                if(pressedControl!=0){int action=pressedControl;pressedControl=0;if(HitControl(e.Location)==action){var handler=action==1?AutoLoginRequested:PinRequested;if(handler!=null)handler(this,EventArgs.Empty);}return;}
                if(!dragging)return;dragging=false;Capture=false;if(moved&&MovedByUser!=null)MovedByUser(this,EventArgs.Empty);
                if(orderGuard!=null)orderGuard.Request();
                if(!MouseOverBar())ScheduleIdle(IdleDelay);
            };
            MouseDoubleClick += delegate(object sender,MouseEventArgs e) {if(e.Button == MouseButtons.Left && HitControl(e.Location)==0 && OpenMain != null) OpenMain(this,EventArgs.Empty);};
            MouseCaptureChanged += delegate {if(!Capture&&dragging){dragging=false;if(orderGuard!=null)orderGuard.Request();if(!MouseOverBar())ScheduleIdle(IdleDelay);}};
            UpdateTooltip();
        }
        void PointerEntered(){hovering=true;idleTimer.Stop();if(docked)hidden=false;SetExpandedFlag(true);Retarget(false);}
        void PointerLeft(){hovering=false;SetHover(0);if(pinned)Retarget(false);ScheduleIdle(IdleDelay);}
        void MenuOpening(object sender,System.ComponentModel.CancelEventArgs e){menuOpen=true;idleTimer.Stop();if(pinned)Retarget(false);}
        void MenuClosed(object sender,ToolStripDropDownClosedEventArgs e){menuOpen=false;if(pinned)Retarget(false);if(orderGuard!=null)orderGuard.Request();if(!MouseOverBar())ScheduleIdle(IdleDelay);}
        Point VisibleCapsuleLocation(){return new Point(expandLeft?location.X+currentWidth-compactWidth:location.X,location.Y);}
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
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);Retarget(true);orderGuard=new OverlayOrderGuard(this,delegate{return menuOpen||dragging;});}
        protected override void OnHandleDestroyed(EventArgs e){if(orderGuard!=null){orderGuard.Dispose();orderGuard=null;}base.OnHandleDestroyed(e);}
        protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);if(Visible){Render();if(orderGuard!=null)orderGuard.Request();}}
        protected override void OnPaintBackground(PaintEventArgs e){}
        protected override void OnPaint(PaintEventArgs e){}
        protected override void OnResize(EventArgs e){
            base.OnResize(e);
            if(WindowState!=FormWindowState.Minimized||restorePending||!IsHandleCreated||Disposing||IsDisposed)return;
            restorePending=true;
            try{BeginInvoke((MethodInvoker)delegate{restorePending=false;if(!Disposing&&!IsDisposed)EnsureVisible(false);});}
            catch(InvalidOperationException){restorePending=false;}
        }
        // reveal=true is «Recuperar LED»: a docked indicator shows its capsule at the
        // edge for a few seconds. Show Desktop recovery (reveal=false) keeps the
        // current state, so hiding and restoring never fight each other.
        internal void EnsureVisible(bool reveal){
            if(Disposing||IsDisposed)return;
            if(WindowState!=FormWindowState.Normal)WindowState=FormWindowState.Normal;
            if(reveal&&docked)hidden=false;
            StopTween();Retarget(true);
            if(!Visible)Show();
            TopMost=true;
            SetWindowPos(Handle,new IntPtr(-1),0,0,0,0,0x0001|0x0002|0x0010|0x0040);
            if(expanded&&ReadMetrics!=null)SetMetrics(ReadMetrics());
            if(reveal&&docked&&!pinned&&!hovering)ScheduleIdle(RevealDelay);else if(!MouseOverBar())ScheduleIdle(IdleDelay);
        }
        internal void MoveAnchor(Point point){anchor=UiContext.ClampPosition(point,CollapsedSize);StopTween();Retarget(true);}
        bool MouseOverBar(){
            if(!IsHandleCreated)return false;
            Point p=PointToClient(CursorSource());
            if(p.Y<0||p.Y>=barHeight||p.X<0||p.X>=currentWidth)return false;
            return AlphaAt(p.X,p.Y)>0;
        }
        internal void SetPinned(bool value){
            bool changed=pinned!=value;pinned=value;
            if(value){idleTimer.Stop();if(docked)hidden=false;SetExpandedFlag(true);Retarget(false);}
            else if(!MouseOverBar()){if(autoHide&&CurrentLight==Light.Green)ScheduleIdle(DockDelay);else{SetExpandedFlag(false);Retarget(false);}}
            if(changed){UpdateTooltip();if(expanded)Render();}
        }
        internal void SetAutomatic(bool value){if(automatic==value)return;automatic=value;UpdateTooltip();if(ControlsVisible)Render();}
        void UpdateTooltip(){string text=TooltipText();if(text!=cachedTooltip){cachedTooltip=text;tooltip.SetToolTip(this,text);}}
        internal void SetMetrics(Telemetry value){
            metrics=value??new Telemetry();if(!expanded)return;
            bool known=metrics.Remaining.HasValue;
            string time=known?RateMonitor.TimeText(metrics.Remaining):"No disponible",download=metrics.RatesReady?RateMonitor.Speed(metrics.DownloadBytesPerSecond):"—",upload=metrics.RatesReady?RateMonitor.Speed(metrics.UploadBytesPerSecond):"—";
            bool changed=time!=cachedTime||download!=cachedDownload||upload!=cachedUpload||known!=timeKnown;cachedTime=time;cachedDownload=download;cachedUpload=upload;timeKnown=known;UpdateTooltip();if(changed)Render();
        }
        internal void SetStatus(Light light,string text,string info) {
            bool changed=CurrentLight!=light;CurrentLight=light;statusText=text??"";UpdateTooltip();
            if(!changed)return;
            if(light!=Light.Green&&docked){
                // Yellow, red or gray: cancel hiding and show the full LED at the user's position at once.
                docked=hidden=false;StopTween();SetExpandedFlag(pinned||hovering);Retarget(true);
            }else Render();
            if(hovering)idleTimer.Stop();else ScheduleIdle(IdleDelay);
        }
        internal void SetExpanded(bool value,bool immediate){SetExpandedFlag(value);if(expanded&&docked)hidden=false;if(immediate)StopTween();Retarget(immediate);}
        void SetExpandedFlag(bool value){
            bool old=expanded;expanded=value||pinned;
            if(!expanded)hoverControl=0;
            if(expanded&&ReadMetrics!=null)SetMetrics(ReadMetrics());
            if(old!=expanded){var changed=ExpansionChanged;if(changed!=null)changed(this,EventArgs.Empty);}
        }
        // The real connection color decides docking, never AUTO/MAN.
        int IdleDelay{get{return autoHide&&CurrentLight==Light.Green?DockDelay:CollapseDelay;}}
        bool NeedsIdleWork(){if(pinned)return false;if(autoHide&&CurrentLight==Light.Green)return !docked||!hidden||expanded;return expanded;}
        void ScheduleIdle(int delay){idleTimer.Stop();if(pinned||dragging||menuOpen||!NeedsIdleWork())return;idleTimer.Interval=delay;idleTimer.Start();}
        void IdleTick(){
            idleTimer.Stop();
            // Pointer, menu, drag and pin events reschedule; nothing polls meanwhile.
            if(pinned||dragging||menuOpen||MouseOverBar())return;
            if(autoHide&&CurrentLight==Light.Green){docked=hidden=true;SetExpandedFlag(false);Retarget(false);}
            else SetExpanded(false,false);
        }
        Rectangle Area(){return Screen.FromPoint(new Point(anchor.X+compactWidth/2,anchor.Y+barHeight/2)).WorkingArea;}
        // Nearest side of the monitor that contains the user's position (work area,
        // so a side taskbar is respected and the notch never lands on another monitor).
        internal static bool NearestRight(Rectangle area,int anchorX,int compact){int c=anchorX+compact/2;return area.Right-c<c-area.Left;}
        internal static int DockedX(Rectangle area,bool right,int width){return right?area.Right-width:area.Left;}
        Geometry Target(out bool ledRight){
            Rectangle area=Area();int full=Math.Min(pinned&&!ControlsVisible?informationWidth:expandedWidth,area.Width);
            if(docked){
                ledRight=NearestRight(area,anchor.X,compactWidth);
                if(hidden&&!expanded)return new Geometry{X=DockedX(area,ledRight,notchDepth),W=notchDepth,N=1F};
                int w=expanded?full:compactWidth;return new Geometry{X=DockedX(area,ledRight,w),W=w};
            }
            ledRight=anchor.X+expandedWidth>area.Right;
            int width=expanded?full:compactWidth,x=ledRight?anchor.X+compactWidth-width:anchor.X;
            return new Geometry{X=Math.Max(area.Left,Math.Min(area.Right-width,x)),W=width};
        }
        void Retarget(bool immediate){
            bool ledRight;Geometry target=Target(out ledRight);
            if(immediate||!IsHandleCreated||!Visible){StopTween();expandLeft=ledRight;Apply(target);return;}
            if(tweening?to.Same(target):current.Same(target)){if(!tweening&&expandLeft!=ledRight){expandLeft=ledRight;Render();}return;}
            expandLeft=ledRight;from=current;to=target;tweenStart=System.Diagnostics.Stopwatch.GetTimestamp();
            // About 180 ms; the longer trip from the user's position to the edge takes 220 ms.
            tweenMs=Math.Abs(from.X-to.X)>compactWidth*2&&Math.Max(from.W,to.W)<=compactWidth?220:180;
            tweening=true;animation.Start();
        }
        void Tick(){double t=(System.Diagnostics.Stopwatch.GetTimestamp()-tweenStart)*1000.0/System.Diagnostics.Stopwatch.Frequency/tweenMs;Step(Math.Max(0,Math.Min(1,t)));}
        void Step(double t){
            double e=1-Math.Pow(1-t,3);
            var g=new Geometry{X=(int)Math.Round(from.X+(to.X-from.X)*e),W=(int)Math.Round(from.W+(to.W-from.W)*e),N=(float)(from.N+(to.N-from.N)*e)};
            if(t>=1){StopTween();g=to;}
            Apply(g);
        }
        void StopTween(){tweening=false;animation.Stop();}
        void Apply(Geometry g){
            current=g;currentWidth=g.W;notch=g.N;location=new Point(g.X,anchor.Y);
            if(!IsHandleCreated){Bounds=LayeredBounds;return;}
            Render();
        }
        // Content stays fixed relative to the LED while the capsule opens.
        int ContentX{get{return expandLeft?currentWidth-(pinned?Math.Max(informationWidth,currentWidth):expandedWidth)+Theme.S(10):compactWidth-Theme.S(2);}}
        internal Rectangle AutomaticBounds{get{return new Rectangle(ContentX+Theme.S(242),(barHeight-Theme.S(18))/2,Theme.S(46),Theme.S(18));}}
        internal Rectangle PinBounds{get{return new Rectangle(ContentX+Theme.S(294),(barHeight-Theme.S(22))/2,Theme.S(22),Theme.S(22));}}
        int HitControl(Point p){if(!ControlsVisible||currentWidth<expandedWidth-Theme.S(5))return 0;Rectangle a=AutomaticBounds;a.Inflate(Theme.S(3),Theme.S(4));if(a.Contains(p))return 1;if(PinBounds.Contains(p))return 2;return 0;}
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
            LastLayerError=UpdateLayeredWindow(Handle,IntPtr.Zero,ref destination,ref size,memoryDc,ref source,0,ref blend,2)?0:Marshal.GetLastWin32Error();
            // WinForms does not always see moves made by UpdateLayeredWindow; refresh its cached bounds.
            if(Bounds!=LayeredBounds)UpdateBounds();
            RenderCount++;
        }
        void Draw(Graphics g,int w,int h){
            g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
            // While docking, the body narrows, extends past the screen edge (flat on
            // that side) and its inner end becomes a short rounded drop.
            // At the end it is a soft "D": flat against the edge, rounded toward the screen.
            float n=notch,bodyH=(h-1F)-n*((h-1F)-notchHeight),top=(h-bodyH)/2F,ext=n*notchDepth;
            RectangleF body=n<=0F?new RectangleF(.5F,.5F,w-1F,h-1F):(expandLeft?new RectangleF(.5F,top,w-1F+ext,bodyH):new RectangleF(.5F-ext,top,w-1F+ext,bodyH));
            float radius=Math.Min(bodyH,body.Width)/2F*(1F-n)+Math.Min(notchDepth,bodyH/2F)*n;
            using(GraphicsPath capsule=Theme.Rounded(body,radius)){
                using(Brush fill=new SolidBrush(Palette.Capsule))g.FillPath(fill,capsule);
                using(Pen border=new Pen(Palette.CapsuleBorder,Math.Max(1F,Theme.F(1F))))g.DrawPath(border,capsule);
                Color led=Palette.ForLight(CurrentLight);float side=Math.Min(w,compactWidth)/2F,lx=expandLeft?w-side:side,cy=h/2F,halo=Theme.F(16F),dot=Theme.F(10F),shown=1F-n;
                if(shown>.01F){
                    using(Brush b=new SolidBrush(Color.FromArgb((int)(56*shown),led)))g.FillEllipse(b,lx-halo/2,cy-halo/2,halo,halo);
                    using(Brush b=new SolidBrush(Color.FromArgb((int)(255*shown),led)))g.FillEllipse(b,lx-dot/2,cy-dot/2,dot,dot);
                }
                if(n>.01F){
                    // Notch: a short vertical bead of the status color inside the drop.
                    float bw=Theme.F(2.5F),bh=Theme.F(12F),bx=w/2F;
                    g.SetClip(capsule);
                    using(Brush b=new SolidBrush(Color.FromArgb((int)(40*n),led)))g.FillEllipse(b,bx-bw*1.4F,cy-bh*.75F,bw*2.8F,bh*1.5F);
                    using(GraphicsPath bead=Theme.Rounded(new RectangleF(bx-bw/2F,cy-bh/2F,bw,bh),bw/2F))using(Brush b=new SolidBrush(Color.FromArgb((int)(255*n),led)))g.FillPath(b,bead);
                    g.ResetClip();
                }
                if(w<compactWidth+Theme.S(24)||n>.3F)return;
                g.SetClip(capsule);g.SetClip(expandLeft?new Rectangle(0,0,w-compactWidth+Theme.S(6),h):new Rectangle(compactWidth-Theme.S(6),0,w-compactWidth+Theme.S(6),h),CombineMode.Intersect);
            }
            // Fixed columns: time 90, download 76, upload 74, AUTO/MAN 46 and pin 22.
            int x=ContentX;float cyf=h/2F;
            using(Pen icon=Glyphs.Stroke(Palette.Secondary)){
                Glyphs.Clock(g,icon,x+Theme.F(7F),cyf,Theme.F(12F));
                Glyphs.Arrow(g,icon,x+Theme.S(90)+Theme.F(5F),cyf,Theme.F(11F),true);
                Glyphs.Arrow(g,icon,x+Theme.S(166)+Theme.F(5F),cyf,Theme.F(11F),false);
            }
            using(Brush primary=new SolidBrush(Palette.Text))using(Brush secondary=new SolidBrush(Palette.Secondary)){
                g.DrawString(cachedTime,timeKnown?Theme.IndicatorStrong:Theme.IndicatorText,timeKnown?primary:secondary,new RectangleF(x+Theme.S(18),0,Theme.S(68),h),leftText);
                g.DrawString(cachedDownload,Theme.IndicatorText,primary,new RectangleF(x+Theme.S(104),0,Theme.S(62),h),leftText);
                g.DrawString(cachedUpload,Theme.IndicatorText,primary,new RectangleF(x+Theme.S(180),0,Theme.S(60),h),leftText);
            }
            if(!ControlsVisible||w<expandedWidth-Theme.S(5))return;
            Rectangle auto=AutomaticBounds;bool hoverAuto=hoverControl==1;
            using(GraphicsPath pill=Theme.Rounded(new RectangleF(auto.X+.5F,auto.Y+.5F,auto.Width-1F,auto.Height-1F),(auto.Height-1F)/2F)){
                if(automatic){using(Brush b=new SolidBrush(Color.FromArgb(hoverAuto?78:46,Palette.Accent)))g.FillPath(b,pill);}
                else{if(hoverAuto)using(Brush b=new SolidBrush(Palette.Hover))g.FillPath(b,pill);using(Pen p=new Pen(Palette.BorderStrong,Math.Max(1F,Theme.F(1F))))g.DrawPath(p,pill);}
            }
            using(Brush b=new SolidBrush(automatic?(hoverAuto?Palette.AccentHover:Palette.Accent):(hoverAuto?Palette.Text:Palette.Secondary)))g.DrawString(automatic?"AUTO":"MAN",Theme.IndicatorStrong,b,new RectangleF(auto.X,auto.Y,auto.Width,auto.Height+Theme.F(1F)),centerText);
            Rectangle pin=PinBounds;
            if(hoverControl==2)using(Brush b=new SolidBrush(Palette.Hover))g.FillEllipse(b,pin);
            Color pinColor=pinned?Palette.Accent:(hoverControl==2?Palette.Text:Palette.Secondary);
            using(Pen p=Glyphs.Stroke(pinColor))using(Brush b=pinned?new SolidBrush(pinColor):null)Glyphs.Pin(g,p,b,pin.X+pin.Width/2F,pin.Y+pin.Height/2F,Theme.F(15F));
        }
        internal int AlphaAt(int x,int y){return surface==null?0:surface.GetPixel(x,y).A;}
        internal void SaveSnapshot(string path){if(surface==null)Render();using(Bitmap copy=surface.Clone(new Rectangle(0,0,currentWidth,barHeight),PixelFormat.Format32bppArgb))copy.Save(path,ImageFormat.Png);}
        protected override void Dispose(bool disposing) {
            if(disposing&&orderGuard!=null){orderGuard.Dispose();orderGuard=null;}
            if(disposing){animation.Stop();idleTimer.Stop();contextMenu.Opening-=MenuOpening;contextMenu.Closed-=MenuClosed;tooltip.Dispose();animation.Dispose();idleTimer.Dispose();leftText.Dispose();centerText.Dispose();}
            ReleaseSurface(disposing); base.Dispose(disposing);
        }

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
