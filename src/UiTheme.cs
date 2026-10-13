using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NautaReconnect
{
    // Visual tokens shared by the window, the tray menu and the floating LED.
    // One neutral graphite scale, one accent for actions and four status colors.
    internal static class Palette {
        internal static readonly Color
            Background = Color.FromArgb(16,19,23),
            Surface = Color.FromArgb(25,29,35),
            Raised = Color.FromArgb(34,39,47),
            Hover = Color.FromArgb(44,50,60),
            Pressed = Color.FromArgb(54,61,73),
            Border = Color.FromArgb(45,52,62),
            BorderStrong = Color.FromArgb(70,79,93),
            Text = Color.FromArgb(232,236,241),
            Secondary = Color.FromArgb(166,176,189),
            Muted = Color.FromArgb(136,147,161),
            Disabled = Color.FromArgb(98,107,120),
            Accent = Color.FromArgb(138,180,248),
            AccentHover = Color.FromArgb(166,199,250),
            AccentPressed = Color.FromArgb(114,158,228),
            OnAccent = Color.FromArgb(10,18,32),
            Warning = Color.FromArgb(242,193,78),
            Danger = Color.FromArgb(244,112,120),
            Capsule = Color.FromArgb(27,31,38),
            CapsuleBorder = Color.FromArgb(66,75,89);
        internal static Color ForLight(Light light) {
            switch(light) {
                case Light.Green: return Color.FromArgb(61,214,140);
                case Light.Yellow: return Color.FromArgb(242,193,78);
                case Light.Red: return Color.FromArgb(244,96,106);
                default: return Color.FromArgb(138,148,161);
            }
        }
    }

    // Sizes are written in 96-DPI units and multiplied by the system scale. Fonts
    // use pixel units with the same factor so every element scales together.
    internal static class Theme {
        static float dpiScale, scale;
        static Font body, small, strong, headline, metric, indicatorText, indicatorStrong, section;
        internal static float DpiScale {
            get {
                if(dpiScale <= 0) { using(Graphics g = Graphics.FromHwnd(IntPtr.Zero)) dpiScale = Math.Max(1F,g.DpiX / 96F); }
                return dpiScale;
            }
        }
        internal static float Scale { get { if(scale <= 0) scale = DpiScale; return scale; } }
        // Only the preview uses this to inspect other Windows scale factors.
        internal static void OverrideScale(string value) {
            float parsed;
            if(String.IsNullOrEmpty(value) || !Single.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out parsed) || parsed < 1F || parsed > 3F) return;
            Release(); scale = parsed;
        }
        internal static int S(int value) { return (int)Math.Round(value * Scale); }
        internal static float F(float value) { return value * Scale; }
        static Font Make(string family,float pixels) { return new Font(family,pixels * Scale,GraphicsUnit.Pixel); }
        internal static Font Body { get { return body ?? (body = Make("Segoe UI",12F)); } }
        internal static Font Small { get { return small ?? (small = Make("Segoe UI",11F)); } }
        internal static Font Strong { get { return strong ?? (strong = Make("Segoe UI Semibold",12.5F)); } }
        internal static Font Section { get { return section ?? (section = Make("Segoe UI Semibold",11.5F)); } }
        internal static Font Headline { get { return headline ?? (headline = Make("Segoe UI Semibold",18F)); } }
        internal static Font Metric { get { return metric ?? (metric = Make("Segoe UI Semibold",17F)); } }
        internal static Font IndicatorText { get { return indicatorText ?? (indicatorText = Make("Segoe UI",12F)); } }
        internal static Font IndicatorStrong { get { return indicatorStrong ?? (indicatorStrong = Make("Segoe UI Semibold",12F)); } }
        // Fonts are created once and released when the interface closes.
        internal static void Release() {
            foreach(Font font in new Font[] {body,small,strong,headline,metric,indicatorText,indicatorStrong,section}) if(font != null) font.Dispose();
            body = small = strong = headline = metric = indicatorText = indicatorStrong = section = null;
        }
        internal static GraphicsPath Rounded(RectangleF r,float radius) {
            var path = new GraphicsPath(); float d = Math.Min(radius * 2,Math.Min(r.Width,r.Height));
            if(d <= 0) { path.AddRectangle(r); return path; }
            path.AddArc(r.X,r.Y,d,d,180,90); path.AddArc(r.Right - d,r.Y,d,d,270,90);
            path.AddArc(r.Right - d,r.Bottom - d,d,d,0,90); path.AddArc(r.X,r.Bottom - d,d,d,90,90);
            path.CloseFigure(); return path;
        }
        internal static void FillRounded(Graphics g,Rectangle r,int radius,Color fill,Color border) {
            SmoothingMode old = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            using(GraphicsPath path = Rounded(new RectangleF(r.X + .5F,r.Y + .5F,r.Width - 1F,r.Height - 1F),radius))
            using(Brush brush = new SolidBrush(fill)) {
                g.FillPath(brush,path);
                if(border != fill) using(Pen pen = new Pen(border)) g.DrawPath(pen,path);
            }
            g.SmoothingMode = old;
        }
        internal static void FocusRing(Graphics g,Rectangle r,int radius,Color color) {
            SmoothingMode old = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            float w = Math.Max(1.5F,F(1.5F));
            using(GraphicsPath path = Rounded(new RectangleF(r.X + w / 2,r.Y + w / 2,r.Width - w,r.Height - w),radius))
            using(Pen pen = new Pen(color,w)) g.DrawPath(pen,path);
            g.SmoothingMode = old;
        }
        internal static void SetText(Control control,string text) { if(control.Text != text) control.Text = text; }
    }

    internal static class Native {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
        [DllImport("uxtheme.dll",CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr window,string application,string list);
        [DllImport("user32.dll",CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,string lParam);
        [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
        static int ColorRef(Color c) { return c.R | (c.G << 8) | (c.B << 16); }
        // Dark title bar on Windows 10 2004+ and matching caption on Windows 11.
        // Older versions ignore the attributes and keep the system frame.
        internal static void DarkFrame(IntPtr window) {
            try {
                int on = 1; if(DwmSetWindowAttribute(window,20,ref on,4) != 0) DwmSetWindowAttribute(window,19,ref on,4);
                int caption = ColorRef(Palette.Background), text = ColorRef(Palette.Text), border = ColorRef(Palette.Border);
                DwmSetWindowAttribute(window,35,ref caption,4); DwmSetWindowAttribute(window,36,ref text,4); DwmSetWindowAttribute(window,34,ref border,4);
            } catch(DllNotFoundException) { } catch(EntryPointNotFoundException) { }
        }
        internal static void DarkScrollbars(IntPtr window) { try { SetWindowTheme(window,"DarkMode_Explorer",null); } catch(DllNotFoundException) { } catch(EntryPointNotFoundException) { } }
        internal static void CueBanner(TextBox box,string text) { if(box.IsHandleCreated) SendMessage(box.Handle,0x1501,new IntPtr(1),text ?? ""); }
    }

    internal enum ButtonKind { Secondary, Primary, Quiet, Danger }

    // Rounded button with visible hover, pressed, focus and disabled states.
    internal sealed class FlatButton : Button {
        ButtonKind kind; bool hover, down;
        internal FlatButton(string text,ButtonKind kind) {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,true);
            this.kind = kind; Text = text; FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; UseVisualStyleBackColor = false;
            Font = Theme.Body; Cursor = Cursors.Hand; Height = Theme.S(32); BackColor = Palette.Surface; Margin = Padding.Empty;
        }
        internal ButtonKind Kind { get { return kind; } set { if(kind != value) { kind = value; Invalidate(); } } }
        internal int PreferredWidthFor(params string[] texts) {
            int width = 0; foreach(string t in texts) width = Math.Max(width,TextRenderer.MeasureText(t,Font).Width);
            return width + Theme.S(kind == ButtonKind.Quiet || kind == ButtonKind.Danger ? 20 : 30);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if(e.Button == MouseButtons.Left) { down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { if(down) { down = false; Invalidate(); } base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { hover = down = false; Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics; g.Clear(BackColor);
            Color fill, border, text;
            if(!Enabled) {
                fill = kind == ButtonKind.Quiet || kind == ButtonKind.Danger ? BackColor : Palette.Raised; border = kind == ButtonKind.Quiet || kind == ButtonKind.Danger ? fill : Palette.Border; text = Palette.Disabled;
            } else if(kind == ButtonKind.Primary) {
                fill = border = down ? Palette.AccentPressed : hover ? Palette.AccentHover : Palette.Accent; text = Palette.OnAccent;
            } else if(kind == ButtonKind.Secondary) {
                fill = down ? Palette.Pressed : hover ? Palette.Hover : Palette.Raised; border = hover || down ? Palette.BorderStrong : Palette.Border; text = Palette.Text;
            } else {
                fill = border = down ? Palette.Pressed : hover ? Palette.Hover : BackColor; text = kind == ButtonKind.Danger ? Palette.Danger : Palette.Text;
            }
            Rectangle r = ClientRectangle; int radius = Theme.S(6);
            Theme.FillRounded(g,r,radius,fill,border);
            if(Focused && ShowFocusCues) Theme.FocusRing(g,r,radius,kind == ButtonKind.Primary ? Palette.Text : Palette.Accent);
            TextRenderer.DrawText(g,Text,Font,r,text,TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
    }

    // Switch styled after Windows 11. Click and Space raise Click; the caller
    // changes Checked only after the requested setting was applied.
    internal sealed class ToggleSwitch : CheckBox {
        bool hover;
        // The row description names the mode (AUTO/MAN), so no caption by default.
        internal string OnText = "", OffText = "";
        internal ToggleSwitch() {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,true);
            AutoCheck = false; AutoSize = false; Cursor = Cursors.Hand; Font = Theme.Body; Height = Theme.S(32); BackColor = Palette.Surface; ForeColor = Palette.Secondary; Margin = Padding.Empty;
        }
        internal int PreferredWidthFor() {
            int text = Math.Max(OnText.Length == 0 ? 0 : TextRenderer.MeasureText(OnText,Font).Width,OffText.Length == 0 ? 0 : TextRenderer.MeasureText(OffText,Font).Width);
            return (text > 0 ? text + Theme.S(10) : 0) + Theme.S(40 + 8);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
        protected override void OnEnabledChanged(EventArgs e) { hover = false; Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics; g.Clear(BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
            int tw = Theme.S(40), th = Theme.S(20); var track = new Rectangle(Width - tw - Theme.S(3),(Height - th) / 2,tw,th);
            Color trackFill, trackBorder, thumb;
            if(Checked) { trackFill = trackBorder = !Enabled ? Palette.Pressed : hover ? Palette.AccentHover : Palette.Accent; thumb = Enabled ? Palette.OnAccent : Palette.Disabled; }
            else { trackFill = hover && Enabled ? Palette.Hover : Palette.Raised; trackBorder = Enabled ? Palette.BorderStrong : Palette.Border; thumb = Enabled ? Palette.Secondary : Palette.Disabled; }
            Theme.FillRounded(g,track,th / 2,trackFill,trackBorder);
            float d = Theme.F(Checked ? 12F : 10F), cx = Checked ? track.Right - th / 2F : track.X + th / 2F, cy = track.Y + th / 2F;
            using(Brush b = new SolidBrush(thumb)) g.FillEllipse(b,cx - d / 2,cy - d / 2,d,d);
            if(Focused && ShowFocusCues) Theme.FocusRing(g,Rectangle.Inflate(track,Theme.S(3),Theme.S(3)),th / 2 + Theme.S(3),Palette.Accent);
            var label = new Rectangle(0,0,track.X - Theme.S(10),Height);
            if((Checked ? OnText : OffText).Length > 0) TextRenderer.DrawText(g,Checked ? OnText : OffText,Font,label,Enabled ? Palette.Secondary : Palette.Disabled,TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }

    // Borderless TextBox inside a rounded field with hover and focus outline.
    internal sealed class FieldBox : Panel {
        internal readonly TextBox Input;
        bool hover;
        string cue = "";
        internal FieldBox() {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,true);
            BackColor = Palette.Surface; Height = Theme.S(34); Cursor = Cursors.IBeam; Margin = Padding.Empty;
            Input = new TextBox { BorderStyle = BorderStyle.None, BackColor = Palette.Raised, ForeColor = Palette.Text, Font = Theme.Body, Margin = Padding.Empty };
            Controls.Add(Input);
            Input.GotFocus += delegate { Invalidate(); }; Input.LostFocus += delegate { Invalidate(); };
            Input.MouseEnter += delegate { SetHover(true); }; Input.MouseLeave += delegate { SetHover(ClientRectangle.Contains(PointToClient(Cursor.Position))); };
            Input.HandleCreated += delegate { Native.CueBanner(Input,cue); };
            Input.EnabledChanged += delegate { Input.BackColor = Palette.Raised; Invalidate(); };
        }
        internal string Cue { get { return cue; } set { cue = value ?? ""; Native.CueBanner(Input,cue); } }
        void SetHover(bool value) { if(hover != value) { hover = value; Invalidate(); } }
        protected override void OnMouseEnter(EventArgs e) { SetHover(true); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { SetHover(ClientRectangle.Contains(PointToClient(Cursor.Position))); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { Input.Focus(); base.OnMouseDown(e); }
        protected override void OnLayout(LayoutEventArgs e) {
            base.OnLayout(e); if(Input == null) return; int pad = Theme.S(10);
            Input.SetBounds(pad,Math.Max(0,(Height - Input.Height) / 2),Math.Max(10,Width - pad * 2),Input.Height);
        }
        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics; g.Clear(BackColor); int radius = Theme.S(6); if(Input == null) return;
            Color border = Input.Focused ? Palette.Accent : hover && Input.Enabled ? Palette.BorderStrong : Palette.Border;
            Theme.FillRounded(g,ClientRectangle,radius,Palette.Raised,border);
            if(Input.Focused) {
                // Windows 11 marks the focused field with a stronger bottom edge.
                using(Pen pen = new Pen(Palette.Accent,Theme.F(2F))) g.DrawLine(pen,radius,Height - Theme.F(1F),Width - radius,Height - Theme.F(1F));
            }
        }
    }

    internal sealed class LedDot : Control {
        Color color = Palette.ForLight(Light.Gray);
        internal LedDot() {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,true);
            TabStop = false; Size = new Size(Theme.S(24),Theme.S(24)); BackColor = Palette.Surface; AccessibleRole = AccessibleRole.Graphic;
        }
        internal Color Color { get { return color; } set { if(color != value) { color = value; Invalidate(); } } }
        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics; g.Clear(BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
            float halo = Math.Min(Width,Height) - 1, dot = halo * .46F, cx = Width / 2F, cy = Height / 2F;
            using(Brush b = new SolidBrush(Color.FromArgb(48,color))) g.FillEllipse(b,cx - halo / 2,cy - halo / 2,halo,halo);
            using(Brush b = new SolidBrush(color)) g.FillEllipse(b,cx - dot / 2,cy - dot / 2,dot,dot);
        }
    }

    internal sealed class ActivityEntry {
        internal readonly DateTime Time; internal readonly Light Light; internal readonly string Message, Detail;
        internal ActivityEntry(DateTime time,Light light,string message,string detail) { Time = time; Light = light; Message = message ?? ""; Detail = detail ?? ""; }
        public override string ToString() { return Time.ToString("HH:mm:ss") + "  " + Message + (Detail.Length > 0 ? " — " + Detail : ""); }
        internal string CopyLine(){return Time.ToString("yyyy-MM-dd HH:mm:ss")+" | "+Light+" | "+Message+" | "+Detail;}
    }

    // Owner-drawn list: status color, time and message on one line. The full
    // text appears as a tooltip only when the pointer moves to another row.
    internal sealed class ActivityList : ListBox {
        readonly ToolTip tip = new ToolTip { InitialDelay = 500, AutoPopDelay = 15000, ReshowDelay = 200 };
        int tipIndex = -1;
        readonly ContextMenuStrip copyMenu=new ContextMenuStrip();
        internal Action<string> CopySink=Clipboard.SetText;
        internal event EventHandler FullLogRequested;
        internal ActivityList() {
            DrawMode = DrawMode.OwnerDrawFixed; ItemHeight = Theme.S(26); BorderStyle = BorderStyle.None; IntegralHeight = false;
            BackColor = Palette.Surface; ForeColor = Palette.Text; Font = Theme.Small; Margin = Padding.Empty;
            SelectionMode=SelectionMode.MultiExtended;
            copyMenu.Renderer=new MenuRenderer();copyMenu.Font=Theme.Body;
            var selected=new ToolStripMenuItem("Copiar seleccionados",null,delegate{Copy(false);});selected.ShortcutKeys=Keys.Control|Keys.C;
            var all=new ToolStripMenuItem("Copiar toda la actividad",null,delegate{Copy(true);});all.ShortcutKeys=Keys.Control|Keys.Shift|Keys.C;
            copyMenu.Items.Add(selected);copyMenu.Items.Add(all);copyMenu.Items.Add(new ToolStripMenuItem("Ver detalle…",null,delegate{ShowDetail();}));
            copyMenu.Items.Add(new ToolStripMenuItem("Ver registro completo…",null,delegate{var handler=FullLogRequested;if(handler!=null)handler(this,EventArgs.Empty);}));
            copyMenu.Opening+=delegate{selected.Enabled=SelectedIndices.Count>0;all.Enabled=Items.Count>0;};ContextMenuStrip=copyMenu;
        }
        internal string CopyText(bool all){var text=new System.Text.StringBuilder();for(int i=0;i<Items.Count;i++)if(all||SelectedIndices.Contains(i)){var entry=Items[i] as ActivityEntry;if(entry==null)continue;if(text.Length>0)text.AppendLine();text.Append(entry.CopyLine());}return text.ToString();}
        internal void Copy(bool all){string text=CopyText(all);if(text.Length==0)return;try{CopySink(text);}catch(System.Runtime.InteropServices.ExternalException){MessageBox.Show(FindForm(),"El portapapeles está ocupado. Vuelve a intentar copiar.","Copiar actividad",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
        internal void TestKey(Keys key){OnKeyDown(new KeyEventArgs(key));}
        protected override void OnKeyDown(KeyEventArgs e){
            if(e.Control&&e.KeyCode==Keys.C){Copy(e.Shift);e.Handled=e.SuppressKeyPress=true;return;}
            if(e.Control&&e.KeyCode==Keys.A){BeginUpdate();try{for(int i=0;i<Items.Count;i++)SetSelected(i,true);}finally{EndUpdate();}e.Handled=e.SuppressKeyPress=true;return;}
            if(e.KeyCode==Keys.Enter){ShowDetail();e.Handled=e.SuppressKeyPress=true;return;}base.OnKeyDown(e);
        }
        protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Right){int row=IndexFromPoint(e.Location);if(row>=0&&!GetSelected(row)){ClearSelected();SetSelected(row,true);}}base.OnMouseDown(e);}
        protected override void OnDoubleClick(EventArgs e){base.OnDoubleClick(e);ShowDetail();}
        void ShowDetail(){if(SelectedIndex<0)return;var entry=Items[SelectedIndex] as ActivityEntry;if(entry!=null)ShowText("Detalle de actividad",entry.CopyLine());}
        internal void ShowText(string title,string content){
            using(var dialog=new Form{Text=title,StartPosition=FormStartPosition.CenterParent,Size=new Size(Theme.S(560),Theme.S(280)),MinimumSize=new Size(Theme.S(320),Theme.S(160)),BackColor=Palette.Background,ForeColor=Palette.Text,ShowInTaskbar=false}){
                var text=new TextBox{Multiline=true,ReadOnly=true,WordWrap=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,Font=Theme.Body,BackColor=Palette.Surface,ForeColor=Palette.Text,Text=content,AccessibleName="Texto completo de actividad"};dialog.Controls.Add(text);dialog.Padding=new Padding(Theme.S(14));dialog.Shown+=delegate{Native.DarkFrame(dialog.Handle);};dialog.ShowDialog(FindForm());
            }
        }
        protected override void OnResize(EventArgs e){base.OnResize(e);Invalidate();tipIndex=-1;if(tip!=null)tip.SetToolTip(this,"");}
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Native.DarkScrollbars(Handle); }
        protected override void OnDrawItem(DrawItemEventArgs e) {
            if(e.Index < 0 || e.Index >= Items.Count) return;
            var entry = Items[e.Index] as ActivityEntry; Graphics g = e.Graphics; Rectangle r = new Rectangle(0,e.Bounds.Y,ClientSize.Width,e.Bounds.Height);
            var paintState=g.Save();g.SetClip(r,System.Drawing.Drawing2D.CombineMode.Intersect);
            try{
            bool selected = (e.State & DrawItemState.Selected) != 0 && Focused;
            using(Brush back = new SolidBrush(selected ? Palette.Raised : BackColor)) g.FillRectangle(back,r);
            if(entry == null) return;
            SmoothingMode old = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            float dot = Theme.F(6F);
            using(Brush b = new SolidBrush(Palette.ForLight(entry.Light))) g.FillEllipse(b,r.X + Theme.F(10F),r.Y + (r.Height - dot) / 2F,dot,dot);
            g.SmoothingMode = old;
            const TextFormatFlags measure = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            const TextFormatFlags flags = measure | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            int x = r.X + Theme.S(24), timeWidth = TextRenderer.MeasureText(g,"00:00:00",Font,Size.Empty,measure).Width + Theme.S(12);
            TextRenderer.DrawText(g,entry.Time.ToString("HH:mm:ss"),Font,new Rectangle(x,r.Y,timeWidth,r.Height),Palette.Muted,flags);
            x += timeWidth; int available = r.Right - Theme.S(8) - x; if(available <= 0) return;
            int messageWidth = Math.Min(available,TextRenderer.MeasureText(g,entry.Message,Font,Size.Empty,measure).Width + Theme.S(2));
            TextRenderer.DrawText(g,entry.Message,Font,new Rectangle(x,r.Y,messageWidth,r.Height),Palette.Text,flags);
            int detailX = x + messageWidth + Theme.S(8);
            if(entry.Detail.Length > 0 && r.Right - Theme.S(8) - detailX > Theme.S(24))
                TextRenderer.DrawText(g,entry.Detail,Font,new Rectangle(detailX,r.Y,r.Right - Theme.S(8) - detailX,r.Height),Palette.Muted,flags);
            }finally{g.Restore(paintState);}
        }
        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e); int index = IndexFromPoint(e.Location);
            if(index == tipIndex) return; tipIndex = index;
            tip.SetToolTip(this,index >= 0 && index < Items.Count ? Wrap(Items[index].ToString(),90) : "");
        }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); tipIndex = -1; tip.SetToolTip(this,""); }
        internal static string Wrap(string text,int width) {
            if(text == null || text.Length <= width) return text ?? "";
            var builder = new System.Text.StringBuilder(); int line = 0;
            foreach(string word in text.Split(' ')) {
                if(line > 0 && line + word.Length + 1 > width) { builder.Append('\n'); line = 0; }
                else if(line > 0) { builder.Append(' '); line++; }
                builder.Append(word); line += word.Length;
            }
            return builder.ToString();
        }
        protected override void Dispose(bool disposing) { if(disposing){tip.Dispose();copyMenu.Dispose();} base.Dispose(disposing); }
    }

    // Surface that paints the rounded cards behind the controls of the window.
    internal sealed class CanvasPanel : Panel {
        internal CanvasPanel() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,true); }
        protected override void OnScroll(ScrollEventArgs se) { base.OnScroll(se); Invalidate(); }
        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); Invalidate(); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Native.DarkScrollbars(Handle); }
    }

    internal sealed class MenuColors : ProfessionalColorTable {
        public override Color ToolStripDropDownBackground { get { return Palette.Raised; } }
        public override Color ImageMarginGradientBegin { get { return Palette.Raised; } }
        public override Color ImageMarginGradientMiddle { get { return Palette.Raised; } }
        public override Color ImageMarginGradientEnd { get { return Palette.Raised; } }
        public override Color MenuBorder { get { return Palette.BorderStrong; } }
        public override Color MenuItemBorder { get { return Palette.Hover; } }
        public override Color MenuItemSelected { get { return Palette.Hover; } }
        public override Color MenuItemSelectedGradientBegin { get { return Palette.Hover; } }
        public override Color MenuItemSelectedGradientEnd { get { return Palette.Hover; } }
        public override Color MenuItemPressedGradientBegin { get { return Palette.Pressed; } }
        public override Color MenuItemPressedGradientEnd { get { return Palette.Pressed; } }
        public override Color SeparatorDark { get { return Palette.Border; } }
        public override Color SeparatorLight { get { return Palette.Raised; } }
        public override Color CheckBackground { get { return Palette.Raised; } }
        public override Color CheckSelectedBackground { get { return Palette.Hover; } }
        public override Color CheckPressedBackground { get { return Palette.Pressed; } }
        public override Color ButtonSelectedBorder { get { return Palette.Hover; } }
    }

    internal sealed class MenuRenderer : ToolStripProfessionalRenderer {
        internal MenuRenderer() : base(new MenuColors()) { RoundedEdges = false; }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor = e.Item.Enabled ? Palette.Text : Palette.Disabled; base.OnRenderItemText(e); }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) {
            Graphics g = e.Graphics; SmoothingMode old = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = e.ImageRectangle; float s = Math.Min(r.Width,r.Height);
            float x = r.X + (r.Width - s) / 2F, y = r.Y + (r.Height - s) / 2F;
            using(Pen pen = new Pen(e.Item.Enabled ? Palette.Accent : Palette.Disabled,Math.Max(1.5F,s / 9F)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(pen,new PointF[] { new PointF(x + s * .2F,y + s * .52F),new PointF(x + s * .42F,y + s * .74F),new PointF(x + s * .8F,y + s * .3F) });
            g.SmoothingMode = old;
        }
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { e.ArrowColor = Palette.Text; base.OnRenderArrow(e); }
    }

    // Line icons drawn with the current pen: no image files, sharp at any scale.
    internal static class Glyphs {
        internal static Pen Stroke(Color color) { return new Pen(color,Math.Max(1.2F,Theme.F(1.35F))) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }; }
        internal static void Clock(Graphics g,Pen pen,float cx,float cy,float size) {
            float r = size / 2F; g.DrawEllipse(pen,cx - r,cy - r,size,size);
            g.DrawLine(pen,cx,cy,cx,cy - r * .55F); g.DrawLine(pen,cx,cy,cx + r * .45F,cy + r * .2F);
        }
        internal static void Arrow(Graphics g,Pen pen,float cx,float cy,float size,bool down) {
            float h = size / 2F, w = size * .32F, d = down ? 1F : -1F;
            g.DrawLine(pen,cx,cy - h * d,cx,cy + h * d);
            g.DrawLines(pen,new PointF[] { new PointF(cx - w,cy + (h - w) * d),new PointF(cx,cy + h * d),new PointF(cx + w,cy + (h - w) * d) });
        }
        internal static void Pin(Graphics g,Pen pen,Brush fill,float cx,float cy,float size) {
            float u = size / 16F, x = cx - 8 * u, y = cy - 8 * u;
            var body = new PointF[] { new PointF(x + 5.5F * u,y + 2.5F * u),new PointF(x + 10.5F * u,y + 2.5F * u),new PointF(x + 10F * u,y + 7.5F * u),new PointF(x + 12.5F * u,y + 10F * u),new PointF(x + 3.5F * u,y + 10F * u),new PointF(x + 6F * u,y + 7.5F * u) };
            if(fill != null) g.FillPolygon(fill,body);
            g.DrawPolygon(pen,body); g.DrawLine(pen,x + 8 * u,y + 10F * u,x + 8 * u,y + 14.5F * u);
        }
    }
}
