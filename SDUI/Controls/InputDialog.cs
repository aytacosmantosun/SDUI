using System;
using System.Windows.Forms;

namespace SDUI.Controls;

public partial class InputDialog : UIWindowBase
{
    public enum InputType
    {
        Combobox,
        Textbox,
        Numeric,
    }

    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <value>
    /// The value.
    /// </value>
    public object Value { get; private set; }

    /// <summary>
    /// Gets the selector.
    /// </summary>
    /// <value>
    /// The selector.
    /// </value>
    public ComboBox Selector => comboBox;

    /// <summary>
    /// Gets the selector.
    /// </summary>
    /// <value>
    /// The selector.
    /// </value>
    public NumUpDown Numeric => numValue;

    /// <summary>
    /// Form input type
    /// </summary>
    private InputType _inputType;

    /// <summary>
    /// An empty text is a valid answer (e.g. "automatic"); otherwise OK warns with <see cref="EmptyValueText"/>.
    /// </summary>
    public bool AllowEmpty { get; set; }

    /// <summary>
    /// The empty-value warning; the application sets it in the interface language.
    /// </summary>
    public static string EmptyValueText { get; set; } = "The value can't be empty!";

    /// <summary>
    /// The button texts; the application sets them in the interface language.
    /// </summary>
    public static string OkText { get; set; } = "OK";

    public static string CancelText { get; set; } = "Cancel";

    /// <summary>
    /// Initializes a new instance of the <see cref="InputDialog"/> class.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <param name="message">The message.</param>
    /// <param name="selector">If you want to active the selector instead of textbox <c>true</c>; otherwise <c>false</c></param>
    public InputDialog(
        string formTitle,
        string title,
        string message,
        InputType inputType = InputType.Textbox,
        object defaultValue = null
    )
    {
        InitializeComponent();
        btnOK.Text = OkText;
        btnCancel.Text = CancelText;
        this.AcceptButton = this.btnOK;
        BackColor = ColorScheme.BackColor;
        ForeColor = ColorScheme.ForeColor;

        Text = formTitle;
        lblTitle.Text = title;
        lblMessage.Text = message;
        _inputType = inputType;

        switch (_inputType)
        {
            case InputType.Combobox:

                comboBox.Visible = true;
                txtValue.Visible = false;
                numValue.Visible = false;

                if (defaultValue != null)
                    comboBox.SelectedIndex = (int)defaultValue;

                break;

            case InputType.Textbox:

                comboBox.Visible = false;
                txtValue.Visible = true;
                numValue.Visible = false;

                if (defaultValue != null)
                    txtValue.Text = defaultValue.ToString();

                break;

            case InputType.Numeric:

                comboBox.Visible = false;
                txtValue.Visible = false;
                numValue.Visible = true;

                if (defaultValue != null)
                    numValue.Value = (decimal)defaultValue;

                break;
        }
    }

    /// <summary>
    /// Show the dialog
    /// </summary>
    /// <param name="title">The title content</param>
    /// <param name="message">The dialog message</param>
    /// <param name="selector">If you want to active the selector instead of textbox <c>true</c>; otherwise <c>false</c></param>
    /// <returns>The <seealso cref="DialogResult"/></returns>
    public static DialogResult Show(string formTitle, string title, string message, InputType inputType) =>
        new InputDialog(formTitle, title, message, inputType).ShowDialog();

    /// <summary>
    /// Handles the Click event of the btnOK control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private void btnOK_Click(object sender, EventArgs e)
    {
        object value = null;
        switch (_inputType)
        {
            case InputType.Combobox:
                value = comboBox.SelectedItem;
                break;
            case InputType.Textbox:
                value = txtValue.Text;
                break;
            case InputType.Numeric:
                value = numValue.Value;
                break;
            default:
                break;
        }

        if (_inputType == InputType.Textbox && !AllowEmpty && string.IsNullOrWhiteSpace(value?.ToString()))
        {
            // UTa-new-8 (AtBot): the warning is shown in the window itself, in place of the message, instead of a
            // blocking box; the window stays open and the message comes back when the user types.
            ShowEmptyWarning();

            DialogResult = DialogResult.Retry;
            return;
        }

        Value = value;
    }

    /// <summary>The message the caller gave, shown again once the empty-value warning is gone.</summary>
    private string _message;

    private void ShowEmptyWarning()
    {
        if (_message == null)
        {
            _message = lblMessage.Text;
            txtValue.TextChanged += (_, _) =>
            {
                if (_message != null && lblMessage.Text != _message)
                    lblMessage.Text = _message;
            };
        }

        lblMessage.Text = EmptyValueText;
        txtValue.Focus();
    }

    /// <summary>
    /// Handles the PreviewKeyDown event of the txtValue control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="PreviewKeyDownEventArgs"/> instance containing the event data.</param>
    private void txtValue_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            // An empty value is left to OK (the accept button), which warns; Enter used to accept it.
            if (!AllowEmpty && string.IsNullOrWhiteSpace(txtValue.Text))
                return;

            Value = txtValue.Text;
            DialogResult = DialogResult.OK;
        }
    }

    /// <summary>
    /// Handles the FormClosing event of the form control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="KeyEventArgs"/> instance containing the event data.</param>
    private void InputDialog_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.Retry)
            e.Cancel = true;
    }

    /// <summary>
    /// Handles the KeyUp event of the numValue control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="KeyEventArgs"/> instance containing the event data.</param>
    private void numValue_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
            return;

        Value = numValue.Value;
        DialogResult = DialogResult.OK;
    }
}
