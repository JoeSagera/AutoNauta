using System;
using System.Drawing;
using System.Windows.Forms;

namespace NautaReconnect
{
    // Main window layout. UiContext owns the behavior; this class only creates
    // the controls and places them in four blocks: status, operation, account
    // and recent activity. Sizes are 96-DPI units scaled by Theme.S.
    internal sealed class MainWindow : Form {
        internal readonly CanvasPanel Canvas;
        internal readonly Label PreviewBanner, StateLabel, StateTime, StateDetail, TimeValue, DownValue, UpValue, TimeNote;
        internal readonly Label WatchTitle, WatchDescription, AutoTitle, AutoDescription;
        internal readonly Label AccountTitle, AccountSummary, AccountHint, UserCaption, PasswordCaption, ActivityTitle, FooterNote;
        readonly Label timeCaption, downCaption, upCaption;
        internal readonly LedDot StateDot;
        internal readonly FlatButton Check, Toggle, EditAccount, CancelEdit, Save, Clear, Minimize, Exit;
        internal readonly ToggleSwitch Automatic;
        internal readonly FieldBox UserField, PasswordField;
        internal readonly ActivityList Log;
        internal readonly ToolTip Tips;
        readonly bool preview;
        bool accountExpanded = true, accountCancelable, arranging;
        Rectangle bannerCard, statusCard, controlCard, accountCard, activityCard;
        int statusDivider, controlDivider, dividerLeft, dividerRight, preferredHeight;

        internal MainWindow(bool preview) {
            this.preview = preview;
            Text = "AutoNauta" + (preview ? " · Vista previa" : "");
            AutoScaleMode = AutoScaleMode.None; Font = Theme.Body; BackColor = Palette.Background; ForeColor = Palette.Text;
            StartPosition = FormStartPosition.CenterScreen;
            Tips = new ToolTip { InitialDelay = 600, AutoPopDelay = 15000, ReshowDelay = 200 };
            Canvas = new CanvasPanel { Dock = DockStyle.Fill, BackColor = Palette.Background, AutoScroll = true };
            Controls.Add(Canvas);

            Color surface = Palette.Surface;
            PreviewBanner = Make("Vista previa · datos de ejemplo, sin conexiones de red",Theme.Small,Palette.Warning,Mix(Palette.Background,Palette.Warning,.12F));
            PreviewBanner.TextAlign = ContentAlignment.MiddleLeft; PreviewBanner.Visible = preview;
            StateDot = new LedDot();
            StateLabel = Make("Esperando",Theme.Headline,Palette.Text,surface); StateLabel.AutoEllipsis = true;
            StateTime = Make("",Theme.Small,Palette.Muted,surface);
            StateDetail = Make("",Theme.Body,Palette.Secondary,surface); StateDetail.AutoEllipsis = true;
            Check = new FlatButton("Comprobar ahora",ButtonKind.Secondary);
            timeCaption = Make("Tiempo restante",Theme.Small,Palette.Muted,surface);
            downCaption = Make("Bajada",Theme.Small,Palette.Muted,surface);
            upCaption = Make("Subida",Theme.Small,Palette.Muted,surface);
            TimeValue = Make("No disponible",Theme.Metric,Palette.Secondary,surface); TimeValue.AutoEllipsis = true;
            DownValue = Make("—",Theme.Metric,Palette.Text,surface); DownValue.AutoEllipsis = true;
            UpValue = Make("—",Theme.Metric,Palette.Text,surface); UpValue.AutoEllipsis = true;
            TimeNote = Make("",Theme.Small,Palette.Muted,surface); TimeNote.AutoEllipsis = true;

            WatchTitle = Make("Vigilancia",Theme.Strong,Palette.Text,surface);
            WatchDescription = Make("",Theme.Small,Palette.Secondary,surface); WatchDescription.AutoEllipsis = true;
            Toggle = new FlatButton("Iniciar",ButtonKind.Primary);
            AutoTitle = Make("Login automático",Theme.Strong,Palette.Text,surface);
            AutoDescription = Make("",Theme.Small,Palette.Secondary,surface); AutoDescription.AutoEllipsis = true;
            Automatic = new ToggleSwitch { AccessibleName = "Login automático" };

            AccountTitle = Make("Cuenta Nauta",Theme.Strong,Palette.Text,surface);
            AccountSummary = Make("",Theme.Small,Palette.Secondary,surface); AccountSummary.AutoEllipsis = true;
            EditAccount = new FlatButton("Cambiar cuenta",ButtonKind.Secondary);
            CancelEdit = new FlatButton("Cancelar",ButtonKind.Quiet);
            UserCaption = Make("Usuario",Theme.Body,Palette.Secondary,surface); UserCaption.TextAlign = ContentAlignment.MiddleLeft;
            PasswordCaption = Make("Contraseña",Theme.Body,Palette.Secondary,surface); PasswordCaption.TextAlign = ContentAlignment.MiddleLeft;
            UserField = new FieldBox(); UserField.Input.AccessibleName = "Usuario de la cuenta Nauta";
            PasswordField = new FieldBox(); PasswordField.Input.AccessibleName = "Contraseña de la cuenta Nauta"; PasswordField.Input.UseSystemPasswordChar = true;
            AccountHint = Make("",Theme.Small,Palette.Muted,surface); AccountHint.AutoEllipsis = true;
            Save = new FlatButton("Guardar cuenta",ButtonKind.Secondary);
            Clear = new FlatButton("Eliminar cuenta",ButtonKind.Danger);

            ActivityTitle = Make("Actividad reciente",Theme.Section,Palette.Secondary,Palette.Background);
            Log = new ActivityList { AccessibleName = "Actividad reciente" };
            FooterNote = Make("Cerrar la ventana no detiene AutoNauta.",Theme.Small,Palette.Muted,Palette.Background); FooterNote.TextAlign = ContentAlignment.MiddleLeft; FooterNote.AutoEllipsis = true;
            Minimize = new FlatButton("Minimizar",ButtonKind.Secondary) { BackColor = Palette.Background };
            Exit = new FlatButton("Salir",ButtonKind.Secondary) { BackColor = Palette.Background };

            // Insertion order is the keyboard order: top to bottom, left to right.
            Control[] order = { PreviewBanner,StateDot,StateLabel,StateTime,StateDetail,timeCaption,TimeValue,downCaption,DownValue,upCaption,UpValue,TimeNote,Check,
                WatchTitle,WatchDescription,Toggle,AutoTitle,AutoDescription,Automatic,AccountTitle,AccountSummary,EditAccount,CancelEdit,
                UserCaption,UserField,PasswordCaption,PasswordField,AccountHint,Save,Clear,ActivityTitle,Log,FooterNote,Minimize,Exit };
            for(int i = 0; i < order.Length; i++) { order[i].TabIndex = i; Canvas.Controls.Add(order[i]); }

            string traffic = "Tráfico total del equipo en sus interfaces físicas, incluidas otras aplicaciones.\nNo es un test de velocidad.";
            foreach(Control c in new Control[] { downCaption,DownValue,upCaption,UpValue }) Tips.SetToolTip(c,traffic);
            Tips.SetToolTip(Check,"Comprueba ahora la conexión y el tiempo de la cuenta.");
            Tips.SetToolTip(Toggle,"Pausar detiene las comprobaciones.\nNo cierra la sesión Nauta ni desconecta la VPN.");
            Tips.SetToolTip(Automatic,"Desactivado, AutoNauta sigue vigilando pero no inicia sesión por ti.");
            Tips.SetToolTip(Clear,"Borra la cuenta y la contraseña guardadas en esta computadora.");
            Tips.SetToolTip(Minimize,"Oculta la ventana. El LED y la vigilancia siguen activos.");
            Tips.SetToolTip(Exit,"Cierra AutoNauta por completo y detiene la vigilancia.");

            Canvas.Paint += PaintCards;
            Canvas.Layout += delegate { Arrange(); };
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Size client = new Size(Theme.S(480),Theme.S(668));
            Size frame = SizeFromClientSize(client) - client;
            ClientSize = new Size(Math.Min(client.Width,area.Width - frame.Width),Math.Min(client.Height,area.Height - frame.Height));
            MinimumSize = SizeFromClientSize(new Size(Theme.S(420),Theme.S(400)));
            SetAccountExpanded(true,false);
        }

        static Label Make(string text,Font font,Color color,Color back) {
            return new Label { Text = text, Font = font, ForeColor = color, BackColor = back, AutoSize = false, UseMnemonic = false, Margin = Padding.Empty };
        }
        internal static Color Mix(Color a,Color b,float t) {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t),(int)(a.G + (b.G - a.G) * t),(int)(a.B + (b.B - a.B) * t));
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Native.DarkFrame(Handle); }

        // Opening height: everything visible plus about five activity rows, within the screen.
        internal void FitToContent() {
            Arrange(); if(preferredHeight <= 0) return;
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            Size frame = SizeFromClientSize(ClientSize) - ClientSize;
            ClientSize = new Size(ClientSize.Width,Math.Max(Theme.S(400),Math.Min(preferredHeight,area.Height - frame.Height)));
        }
        internal bool AccountExpanded { get { return accountExpanded; } }
        internal void SetAccountExpanded(bool expanded,bool cancelable) {
            accountExpanded = expanded; accountCancelable = cancelable;
            AccountSummary.Visible = EditAccount.Visible = !expanded;
            foreach(Control c in new Control[] { UserCaption,UserField,PasswordCaption,PasswordField,AccountHint,Save,Clear }) c.Visible = expanded;
            CancelEdit.Visible = expanded && cancelable;
            Arrange();
        }

        static void Place(Control c,int x,int y,int width,int height) { c.SetBounds(x,y,Math.Max(0,width),Math.Max(0,height)); }

        void Arrange() {
            if(arranging || Canvas.ClientSize.Width <= 0) return; arranging = true;
            Canvas.SuspendLayout();
            try {
                int m = Theme.S(18), p = Theme.S(16), gap = Theme.S(10), rowH = Theme.S(60), button = Theme.S(32);
                int cardW = Canvas.ClientSize.Width - m * 2, x = m + p, w = cardW - p * 2, oy = Canvas.AutoScrollPosition.Y, y = m, top;
                dividerLeft = x; dividerRight = x + w;

                if(preview) { bannerCard = new Rectangle(m,y,cardW,Theme.S(30)); Place(PreviewBanner,m + Theme.S(12),y + oy,cardW - Theme.S(24),Theme.S(30)); y += Theme.S(30) + gap; }

                top = y;
                // The headline gets the full width: engine messages can be long.
                int checkW = Check.PreferredWidthFor(Check.Text), textX = x + Theme.S(36);
                Place(StateDot,x,y + p + Theme.S(2) + oy,Theme.S(24),Theme.S(24));
                Place(StateLabel,textX,y + p - Theme.S(1) + oy,x + w - textX,Theme.S(28));
                Place(StateTime,textX,y + p + Theme.S(26) + oy,x + w - textX,Theme.S(18));
                y += p + Theme.S(52);
                Place(StateDetail,x,y + oy,w,Theme.S(36));
                y += Theme.S(36) + Theme.S(10); statusDivider = y; y += 1 + Theme.S(12);
                int c1 = w * 38 / 100, c2 = (w - c1) / 2;
                Place(timeCaption,x,y + oy,c1,Theme.S(18)); Place(downCaption,x + c1,y + oy,c2,Theme.S(18)); Place(upCaption,x + c1 + c2,y + oy,w - c1 - c2,Theme.S(18));
                y += Theme.S(18);
                Place(TimeValue,x,y + oy,c1 - Theme.S(8),Theme.S(28)); Place(DownValue,x + c1,y + oy,c2 - Theme.S(8),Theme.S(28)); Place(UpValue,x + c1 + c2,y + oy,w - c1 - c2,Theme.S(28));
                y += Theme.S(34);
                Place(TimeNote,x,y + oy,w - checkW - Theme.S(16),Theme.S(32));
                Place(Check,x + w - checkW,y + oy,checkW,button);
                y += Theme.S(32) + p;
                statusCard = new Rectangle(m,top,cardW,y - top); y += gap;

                top = y;
                int toggleW = Math.Max(Theme.S(92),Toggle.PreferredWidthFor("Iniciar","Pausar")), switchW = Automatic.PreferredWidthFor();
                int descW = w - Math.Max(toggleW,switchW) - Theme.S(16);
                Place(WatchTitle,x,y + Theme.S(11) + oy,descW,Theme.S(20)); Place(WatchDescription,x,y + Theme.S(31) + oy,descW,Theme.S(18));
                Place(Toggle,x + w - toggleW,y + (rowH - button) / 2 + oy,toggleW,button);
                y += rowH; controlDivider = y; y += 1;
                Place(AutoTitle,x,y + Theme.S(11) + oy,descW,Theme.S(20)); Place(AutoDescription,x,y + Theme.S(31) + oy,descW,Theme.S(18));
                Place(Automatic,x + w - switchW,y + (rowH - button) / 2 + oy,switchW,button);
                y += rowH; controlCard = new Rectangle(m,top,cardW,y - top); y += gap;

                top = y;
                if(!accountExpanded) {
                    int editW = EditAccount.PreferredWidthFor(EditAccount.Text);
                    Place(AccountTitle,x,y + Theme.S(11) + oy,w - editW - Theme.S(16),Theme.S(20)); Place(AccountSummary,x,y + Theme.S(31) + oy,w - editW - Theme.S(16),Theme.S(18));
                    Place(EditAccount,x + w - editW,y + (rowH - button) / 2 + oy,editW,button);
                    y += rowH;
                } else {
                    int cancelW = accountCancelable ? CancelEdit.PreferredWidthFor(CancelEdit.Text) : 0, labelW = Theme.S(92);
                    Place(AccountTitle,x,y + p + oy,w - cancelW - Theme.S(8),Theme.S(22));
                    if(accountCancelable) Place(CancelEdit,x + w - cancelW,y + p - Theme.S(5) + oy,cancelW,button);
                    y += p + Theme.S(30);
                    Place(UserCaption,x,y + oy,labelW,Theme.S(34)); Place(UserField,x + labelW,y + oy,w - labelW,Theme.S(34)); y += Theme.S(42);
                    Place(PasswordCaption,x,y + oy,labelW,Theme.S(34)); Place(PasswordField,x + labelW,y + oy,w - labelW,Theme.S(34)); y += Theme.S(40);
                    Place(AccountHint,x + labelW,y + oy,w - labelW,Theme.S(34)); y += Theme.S(38);
                    int saveW = Save.PreferredWidthFor(Save.Text), clearW = Clear.PreferredWidthFor(Clear.Text);
                    Place(Save,x + labelW,y + oy,saveW,button); Place(Clear,x + labelW + saveW + Theme.S(8),y + oy,clearW,button);
                    y += button + p;
                }
                accountCard = new Rectangle(m,top,cardW,y - top); y += gap + Theme.S(6);

                Place(ActivityTitle,m + Theme.S(4),y + oy,cardW - Theme.S(8),Theme.S(20)); y += Theme.S(26);
                int footer = button, minimum = Theme.S(64), listTop = y;
                int listH = Math.Max(minimum,Canvas.ClientSize.Height - m - footer - gap - listTop);
                activityCard = new Rectangle(m,listTop,cardW,listH);
                Place(Log,m + Theme.S(4),listTop + Theme.S(6) + oy,cardW - Theme.S(8),listH - Theme.S(12));
                y = listTop + listH + gap;
                int exitW = Math.Max(Theme.S(84),Exit.PreferredWidthFor(Exit.Text)), minimizeW = Math.Max(Theme.S(96),Minimize.PreferredWidthFor(Minimize.Text));
                Place(Exit,m + cardW - exitW,y + oy,exitW,footer); Place(Minimize,m + cardW - exitW - Theme.S(8) - minimizeW,y + oy,minimizeW,footer);
                Place(FooterNote,m + Theme.S(4),y + oy,cardW - exitW - minimizeW - Theme.S(24),footer);
                y += footer + m;
                preferredHeight = y - listH + Theme.S(146);

                Size needed = listH > minimum || Canvas.ClientSize.Height >= y ? Size.Empty : new Size(0,y);
                if(Canvas.AutoScrollMinSize != needed) Canvas.AutoScrollMinSize = needed;
            } finally { Canvas.ResumeLayout(false); arranging = false; }
            Canvas.Invalidate();
        }

        void PaintCards(object sender,PaintEventArgs e) {
            Graphics g = e.Graphics; g.Clear(Palette.Background);
            int oy = Canvas.AutoScrollPosition.Y, radius = Theme.S(10);
            if(preview) Theme.FillRounded(g,Offset(bannerCard,oy),Theme.S(8),PreviewBanner.BackColor,Mix(Palette.Background,Palette.Warning,.3F));
            foreach(Rectangle card in new Rectangle[] { statusCard,controlCard,accountCard,activityCard }) Theme.FillRounded(g,Offset(card,oy),radius,Palette.Surface,Palette.Border);
            using(Pen line = new Pen(Palette.Border)) {
                g.DrawLine(line,dividerLeft,statusDivider + oy,dividerRight,statusDivider + oy);
                g.DrawLine(line,dividerLeft,controlDivider + oy,dividerRight,controlDivider + oy);
            }
        }
        static Rectangle Offset(Rectangle r,int dy) { r.Offset(0,dy); return r; }
    }
}
