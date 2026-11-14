using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using SDUI;

namespace SDUI.Controls
{
    /// <summary>Raised once per box when a value outside its range was given (AtBot spec 09-04 U8).</summary>
    public sealed class NumUpDownRangeEventArgs : EventArgs
    {
        public NumUpDownRangeEventArgs(decimal requested, decimal shown)
        {
            Requested = requested;
            Shown = shown;
        }

        /// <summary>The value the caller set (e.g. from the settings file).</summary>
        public decimal Requested { get; }

        /// <summary>The range limit the box shows instead.</summary>
        public decimal Shown { get; }
    }

    public class NumUpDown : Control
    {
        private const int LONG_PRESS_TIMER_INTERVAL = 250;

        public event EventHandler ValueChanged;

        /// <summary>
        ///     AtBot spec 09-04 U8 [A3-B19]: a value outside the range is shown at the nearest limit, without
        ///     <see cref="ValueChanged" /> (the setting stays as it is until the user changes the box), and reported
        ///     here once per box so the application can log it.
        /// </summary>
        public static event EventHandler<NumUpDownRangeEventArgs> ValueOutOfRange;

        private decimal _value;
        private decimal _min;
        private decimal _max;
        private Point _mouseLocation;
        private int _pressStep;
        private bool _rangeReported;
        private Timer _longPressTimer = new();

        // U8 [A3-B20]: typing happens in an ordinary text box over the number (selection, caret, Backspace on any
        // value); it is shown while the box has the focus.
        private System.Windows.Forms.TextBox _editor;
        private bool _syncingEditor;

        private RectangleF _upButtonRect;
        private RectangleF _downButtonRect;

        private const int SIZE = 20;
        private float _dpi => DeviceDpi / 96f;

        public decimal Value
        {
            get { return _value; }
            set
            {
                if (value > _max || value < _min)
                {
                    var shown = value > _max ? _max : _min;
                    if (!_rangeReported)
                    {
                        _rangeReported = true;
                        try
                        {
                            ValueOutOfRange?.Invoke(this, new NumUpDownRangeEventArgs(value, shown));
                        }
                        catch (Exception)
                        {
                            // A logging hook never breaks the box.
                        }
                    }

                    _value = shown;
                    SyncEditor();
                    Invalidate();
                    return;
                }

                _value = value;
                SyncEditor();
                ValueChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }

        /// <summary>A minimum above the maximum raises the maximum too; minimum = maximum is allowed (U8).</summary>
        public decimal Minimum
        {
            get { return _min; }
            set
            {
                _min = value;
                if (_max < _min)
                    _max = _min;

                if (_value < _min)
                    Value = _min;

                Invalidate();
            }
        }

        /// <summary>A maximum below the minimum lowers the minimum too; minimum = maximum is allowed (U8).</summary>
        public decimal Maximum
        {
            get { return _max; }
            set
            {
                _max = value;
                if (_min > _max)
                    _min = _max;

                if (_value > _max)
                    Value = _max;

                Invalidate();
            }
        }

        public override Color BackColor
        {
            get => base.BackColor;
            set => base.BackColor = Color.Transparent;
        }

        public NumUpDown()
        {
            SetStyle(
                ControlStyles.SupportsTransparentBackColor
                    | ControlStyles.AllPaintingInWmPaint
                    | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.DoubleBuffer
                    | ControlStyles.ResizeRedraw
                    | ControlStyles.UserPaint,
                true
            );

            UpdateStyles();
            this.DoubleBuffered = true;

            BackColor = Color.Transparent;
            _min = 0;
            _max = 100;
            Size = new Size(80, 25);
            MinimumSize = Size;

            _longPressTimer.Tick += LongPressTimer_Tick;
            _longPressTimer.Interval = LONG_PRESS_TIMER_INTERVAL;

            _upButtonRect = new(Width - SIZE * _dpi, 0, SIZE * _dpi, Height);
            _downButtonRect = new(Width - SIZE * _dpi * 2, 0, SIZE * _dpi, Height);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _mouseLocation = e.Location;
        }

        /// <summary>One step up (+1) or down (-1); a step that would leave the range does nothing.</summary>
        private void Step(int direction)
        {
            CommitEditor();

            var next = _value + direction;
            if (next <= _max && next >= _min)
                Value = next;

            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _mouseLocation = e.Location;

            // U8 [A3-B20]: only the two buttons step (and repeat while held); a click on the number starts typing.
            _pressStep = _mouseLocation.InRect(_upButtonRect) ? 1 : _mouseLocation.InRect(_downButtonRect) ? -1 : 0;
            if (_pressStep == 0)
            {
                BeginEdit();
                return;
            }

            Step(_pressStep);
            _longPressTimer.Start();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _longPressTimer.Stop();
            _longPressTimer.Interval = LONG_PRESS_TIMER_INTERVAL;
            _pressStep = 0;
        }

        private void LongPressTimer_Tick(object sender, EventArgs e)
        {
            if (_pressStep == 0 || (System.Windows.Forms.Control.MouseButtons & System.Windows.Forms.MouseButtons.Left) == 0)
            {
                _longPressTimer.Stop();
                _longPressTimer.Interval = LONG_PRESS_TIMER_INTERVAL;
                return;
            }

            Step(_pressStep);

            if (_longPressTimer.Interval == LONG_PRESS_TIMER_INTERVAL)
                _longPressTimer.Interval = 50;
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            BeginEdit();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Step(e.Delta > 0 ? 1 : -1);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            if (!Enabled)
                HideEditor();
        }

        // --- typing (U8 [A3-B20]) ---

        private void BeginEdit()
        {
            if (!Enabled || IsDisposed)
                return;

            if (_editor == null)
            {
                _editor = new System.Windows.Forms.TextBox
                {
                    BorderStyle = BorderStyle.None,
                    Visible = false,
                    TabStop = false,
                };
                _editor.KeyDown += Editor_KeyDown;
                _editor.KeyPress += Editor_KeyPress;
                _editor.TextChanged += Editor_TextChanged;
                _editor.Leave += (_, _) =>
                {
                    CommitEditor();
                    HideEditor();
                };
                _editor.MouseWheel += (_, e) =>
                {
                    Step(e.Delta > 0 ? 1 : -1);

                    // Handled: the wheel message is not passed on to this box as well (one step per notch).
                    if (e is HandledMouseEventArgs handled)
                        handled.Handled = true;
                };

                // Tab / Shift+Tab leave the box (the text box is its child, so Shift+Tab would land on the box again).
                _editor.PreviewKeyDown += (_, e) =>
                {
                    if (e.KeyCode == Keys.Tab)
                        e.IsInputKey = true;
                };
                Controls.Add(_editor);
            }

            _editor.Font = Font;
            _editor.BackColor = ColorScheme.BackColor;
            _editor.ForeColor = ColorScheme.ForeColor;
            PlaceEditor();

            if (!_editor.Visible)
            {
                // Shown with the current value, all selected: typing replaces it like in any text box.
                SyncEditorForced();
                _editor.Visible = true;
                _editor.SelectAll();
            }

            _editor.Focus();
            Invalidate();
        }

        private void PlaceEditor()
        {
            if (_editor == null)
                return;

            var left = Padding.Left + 4;
            var right = (int)_downButtonRect.X - 4;
            var height = _editor.PreferredHeight;
            _editor.Bounds = new Rectangle(left, Math.Max(0, (Height - height) / 2), Math.Max(10, right - left), height);
        }

        private void HideEditor()
        {
            if (_editor == null || !_editor.Visible)
                return;

            _editor.Visible = false;
            Invalidate();
        }

        /// <summary>Shows the current value in the text box (not while the user's own text is the same number).</summary>
        private void SyncEditor()
        {
            if (_editor == null || !_editor.Visible)
                return;

            if (TryParse(_editor.Text, out var typed) && typed == _value)
                return;

            _syncingEditor = true;
            try
            {
                _editor.Text = _value.ToString(CultureInfo.CurrentCulture);
                _editor.SelectionStart = _editor.Text.Length;
            }
            finally
            {
                _syncingEditor = false;
            }
        }

        /// <summary>Typed text inside the range is taken at once; any other text waits for Enter or leaving the box.</summary>
        private void Editor_TextChanged(object sender, EventArgs e)
        {
            if (_syncingEditor)
                return;

            if (TryParse(_editor.Text, out var typed) && typed >= _min && typed <= _max && typed != _value)
            {
                _syncingEditor = true;
                try
                {
                    Value = typed;
                }
                finally
                {
                    _syncingEditor = false;
                }
            }
        }

        /// <summary>Enter / leaving: the typed number is pulled into the range (0 in a 1–100 box becomes 1); unreadable text is undone.</summary>
        private void CommitEditor()
        {
            if (_editor == null || !_editor.Visible)
                return;

            if (TryParse(_editor.Text, out var typed))
            {
                var inRange = typed > _max ? _max : typed < _min ? _min : typed;
                if (inRange != _value)
                    Value = inRange;
            }

            _syncingEditor = true;
            try
            {
                _editor.Text = _value.ToString(CultureInfo.CurrentCulture);
            }
            finally
            {
                _syncingEditor = false;
            }
        }

        private void Editor_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Enter:
                    CommitEditor();
                    _editor.SelectAll();
                    e.Handled = e.SuppressKeyPress = true;
                    break;
                case Keys.Escape:
                    SyncEditorForced();
                    _editor.SelectAll();
                    e.Handled = e.SuppressKeyPress = true;
                    break;
                case Keys.Tab:
                    CommitEditor();
                    e.Handled = e.SuppressKeyPress = true;
                    FindForm()?.SelectNextControl(this, !e.Shift, true, true, true);
                    break;
                case Keys.Up:
                    Step(1);
                    e.Handled = true;
                    break;
                case Keys.Down:
                    Step(-1);
                    e.Handled = true;
                    break;
            }
        }

        private void Editor_KeyPress(object sender, KeyPressEventArgs e)
        {
            var c = e.KeyChar;
            if (char.IsControl(c) || char.IsDigit(c))
                return;

            var format = CultureInfo.CurrentCulture.NumberFormat;
            if ((c == '-' && _min < 0) || format.NumberDecimalSeparator.IndexOf(c) >= 0 || c == '.')
                return;

            e.Handled = true;
        }

        private void SyncEditorForced()
        {
            if (_editor == null)
                return;

            _syncingEditor = true;
            try
            {
                _editor.Text = _value.ToString(CultureInfo.CurrentCulture);
            }
            finally
            {
                _syncingEditor = false;
            }
        }

        /// <summary>64-bit safe (gold boxes reach 999 999 999 999): decimal in the interface culture, then invariant.</summary>
        private static bool TryParse(string text, out decimal value)
        {
            text = (text ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                value = 0;
                return false;
            }

            return decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
                || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _longPressTimer.Stop();
                _longPressTimer.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);

            _upButtonRect = new(Width - SIZE * _dpi, 0, SIZE * _dpi, Height);
            _downButtonRect = new(Width - SIZE * _dpi * 2, 0, SIZE * _dpi, Height);
            PlaceEditor();
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);

            _upButtonRect = new(Width - SIZE * _dpi, 0, SIZE * _dpi, Height);
            _downButtonRect = new(Width - SIZE * _dpi * 2, 0, SIZE * _dpi, Height);
            PlaceEditor();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            //base.OnPaint(e);
            var graphics = e.Graphics;
            ButtonRenderer.DrawParentBackground(e.Graphics, Bounds, this);

            using var borderPen = new Pen(ColorScheme.BorderColor);
            using var backColorBrush = ColorScheme.BackColor.Alpha(90).Brush();

            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var round = ClientRectangle.Radius(8);

            graphics.FillPath(backColorBrush, round);
            graphics.DrawPath(borderPen, round);

            this.DrawString(graphics, "▲", ColorScheme.ForeColor, _upButtonRect);
            this.DrawString(graphics, "▼", ColorScheme.ForeColor, _downButtonRect);

            graphics.DrawLine(borderPen, _upButtonRect.X, 0, _upButtonRect.X, _upButtonRect.Height);
            graphics.DrawLine(borderPen, _downButtonRect.X, 0, _downButtonRect.X, _downButtonRect.Height);

            // The text box draws the number while the user types.
            if (_editor != null && _editor.Visible)
                return;

            TextRenderer.DrawText(
                graphics,
                Value.ToString(),
                Font,
                new Rectangle(Padding.Left, 0, Width - 1, Height - 1),
                ColorScheme.ForeColor,
                TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.VerticalCenter | TextFormatFlags.Left
            );
        }
    }
}
