using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SDUI.Controls;

public class Label : System.Windows.Forms.Label
{
    public float Angle = 45;
    public bool ApplyGradient { get; set; }

    private bool _gradientAnimation;
    public bool GradientAnimation
    {
        get => _gradientAnimation;
        set
        {
            _gradientAnimation = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Gradient text colors
    /// </summary>
    private Color[] _gradient = new[] { Color.Gray, Color.Black };
    public Color[] Gradient
    {
        get => _gradient;
        set
        {
            _gradient = value;
            Invalidate();
        }
    }

    public Label()
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer,
            true
        );
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);

        Invalidate();
    }

    /// <summary>
    ///     The custom <see cref="OnPaint" /> renders text with GDI+ (<see cref="Graphics.DrawString" />), which is a
    ///     touch wider than the GDI measurement the base <see cref="System.Windows.Forms.Label" /> uses for AutoSize.
    ///     The result was labels clipping their last glyph everywhere (e.g. "Standby retry dela", "...(0 = never"). We
    ///     measure with the same GDI+ path here and widen the preferred size so AutoSize text always fits in full.
    /// </summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        var preferred = base.GetPreferredSize(proposedSize);

        if (!AutoSize || string.IsNullOrEmpty(Text))
            return preferred;

        try
        {
            using var graphics = Graphics.FromHwnd(IntPtr.Zero);
            var measured = graphics.MeasureString(Text, Font);
            var width = (int)Math.Ceiling(measured.Width) + Padding.Horizontal + 2;
            if (width > preferred.Width)
                preferred.Width = width;
        }
        catch
        {
            // Fallback: a small fixed buffer still covers the common one-glyph clip.
            preferred.Width += 4;
        }

        return preferred;
    }

    protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
    {
        base.ScaleControl(factor, specified);

        // FontManager fonts are fixed-pixel, so DPI down-scaling shrinks the label below its (unscaled) text, which
        // then clips or wraps and overlaps its neighbours. Restore whichever dimension is too small so the label
        // always shows its full text (height matters for the multi-line help labels that carry explicit newlines).
        if (AutoSize)
        {
            var preferred = GetPreferredSize(Size.Empty);
            if (preferred.Width > Width || preferred.Height > Height)
                Size = new Size(Math.Max(Width, preferred.Width), Math.Max(Height, preferred.Height));
        }
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        ButtonRenderer.DrawParentBackground(e.Graphics, ClientRectangle, this);
        e.Graphics.FillRectangle(BackColor.Brush(), ClientRectangle);

        if (GradientAnimation)
            Angle = Angle % 360 + 1;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        if (ApplyGradient)
        {
            using var brush = new LinearGradientBrush(
                ClientRectangle,
                _gradient[0],
                _gradient[1],
                Angle /*LinearGradientMode.Horizontal */
            );

            using var format = this.CreateStringFormat(TextAlign, AutoEllipsis, UseMnemonic);
            e.Graphics.DrawString(Text, Font, brush, ClientRectangle, format);
        }
        else
            this.DrawString(e.Graphics, TextAlign, ColorScheme.ForeColor, AutoEllipsis, UseMnemonic);
    }
}
