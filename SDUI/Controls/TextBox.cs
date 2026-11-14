using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SDUI.Controls;

public class TextBox : Control
{
    public class InternalTextBox : System.Windows.Forms.TextBox
    {
        public InternalTextBox()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }
    }

    private int _radius = 2;
    public int Radius
    {
        get => _radius;
        set
        {
            _radius = value;

            Invalidate();
        }
    }
    private InternalTextBox _textBox;

    private bool _passmask = false;
    public bool UseSystemPasswordChar
    {
        get { return _passmask; }
        set
        {
            // The field first: the inner box used to get the previous value.
            _passmask = value;
            _textBox.UseSystemPasswordChar = value;
            Invalidate();
        }
    }

    private bool _passFocusShow = false;
    public bool PassFocusShow
    {
        get { return _passFocusShow; }
        set
        {
            _passFocusShow = value;
            Invalidate();
        }
    }

    protected override void OnEnter(System.EventArgs e)
    {
        if (UseSystemPasswordChar && PassFocusShow)
            _textBox.UseSystemPasswordChar = false;
    }

    protected override void OnLeave(System.EventArgs e)
    {
        if (UseSystemPasswordChar && PassFocusShow)
            _textBox.UseSystemPasswordChar = UseSystemPasswordChar;
    }

    private int _maxchars = 32767;
    public int MaxLength
    {
        get { return _maxchars; }
        set
        {
            _maxchars = value;
            _textBox.MaxLength = MaxLength;
            Invalidate();
        }
    }

    private HorizontalAlignment _align;
    public HorizontalAlignment TextAlignment
    {
        get { return _align; }
        set
        {
            _align = value;
            Invalidate();
        }
    }

    private bool _multiline = false;
    public bool MultiLine
    {
        get { return _multiline; }
        set
        {
            // A1-B9: the setting now reaches the inner edit box (it stayed single-line: Enter pressed the dialog's
            // default button and a pasted list kept one line). Multi-line: Enter is a new line, a vertical scroll
            // bar, and the edit box takes the control's height.
            _multiline = value;
            _textBox.Multiline = value;
            _textBox.AcceptsReturn = value;
            _textBox.WordWrap = value;
            _textBox.ScrollBars = value ? ScrollBars.Vertical : ScrollBars.None;
            FitInnerBox();
            Invalidate();
        }
    }

    /// <summary>A multi-line edit box fills the control; a single-line one sets the control's height (as before).</summary>
    private void FitInnerBox()
    {
        if (!_multiline || _textBox == null)
            return;

        var size = new Size(System.Math.Max(1, Width - 10), System.Math.Max(1, Height - 6));
        if (_textBox.Size != size)
            _textBox.Size = size;
    }

    protected override void OnSizeChanged(System.EventArgs e)
    {
        base.OnSizeChanged(e);
        FitInnerBox();
    }

    public override string Text
    {
        get => _textBox.Text;
        set => _textBox.Text = value;
    }

    protected override void OnBackColorChanged(System.EventArgs e)
    {
        base.OnBackColorChanged(e);
        _textBox.BackColor = Color.FromArgb(BackColor.R, BackColor.G, BackColor.B);
        Invalidate();
    }

    protected override void OnForeColorChanged(System.EventArgs e)
    {
        base.OnForeColorChanged(e);
        _textBox.ForeColor = ForeColor;
        Invalidate();
    }

    protected override void OnFontChanged(System.EventArgs e)
    {
        base.OnFontChanged(e);
        _textBox.Font = Font;
    }

    protected override void OnGotFocus(System.EventArgs e)
    {
        base.OnGotFocus(e);
        _textBox.Focus();
    }

    public TextBox()
    {
        _textBox = new InternalTextBox();
        _textBox.Multiline = false;
        _textBox.Text = string.Empty;
        _textBox.TextAlign = HorizontalAlignment.Center;
        _textBox.BorderStyle = BorderStyle.None;
        _textBox.BackColor = Color.Transparent;
        _textBox.Location = new Point(3, 2);
        _textBox.Font = Font;
        _textBox.Size = new Size(Width - 10, Height - 11);
        _textBox.UseSystemPasswordChar = UseSystemPasswordChar;
        _textBox.TextChanged += _textbox_TextChanged;
        _textBox.PreviewKeyDown += _textbox_PreviewKeyDown;
        Controls.Add(_textBox);

        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);

        Size = new Size(135, 35);
        DoubleBuffered = true;
    }

    private void _textbox_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
    {
        OnPreviewKeyDown(e);
    }

    private void _textbox_TextChanged(object sender, System.EventArgs e)
    {
        Text = _textBox.Text;
        OnTextChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        ButtonRenderer.DrawParentBackground(e.Graphics, ClientRectangle, this);

        var color = Color.CornflowerBlue;

        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.HighQuality;

        var determinedColor = ColorScheme.BackColor.Determine();
        var backColor = ColorScheme.BackColor.Brightness(-.1f);

        var textbox = _textBox;
        if (_multiline)
            FitInnerBox();
        else
        {
            Height = _textBox.Height + 5;
            textbox.Width = Width - 10;
        }

        textbox.TextAlign = TextAlignment;
        // PassFocusShow shows the text while the box has the focus (painting used to mask it again).
        textbox.UseSystemPasswordChar = UseSystemPasswordChar && !(PassFocusShow && ContainsFocus);
        textbox.ForeColor = ColorScheme.ForeColor;
        textbox.BackColor = backColor;

        using var backBrush = new SolidBrush(backColor);
        graphics.FillPath(backBrush, new Rectangle(0, 0, Width - 1, Height - 1).Radius(_radius));

        var colorBegin = determinedColor.Brightness(.1f).Alpha(90);
        var colorEnd = determinedColor.Brightness(-.1f).Alpha(60);

        using var innerBorderBrush = new LinearGradientBrush(
            new Rectangle(1, 1, Width - 2, Height - 2),
            colorBegin,
            colorEnd,
            90
        );
        using var innerBorderPen = new Pen(innerBorderBrush);

        graphics.DrawPath(innerBorderPen, new Rectangle(1, 1, Width - _radius, Height - _radius).Radius(_radius));
        graphics.DrawLine(ColorScheme.BorderColor, new Point(1, 1), new Point(Width - 3, 1));
    }
}
