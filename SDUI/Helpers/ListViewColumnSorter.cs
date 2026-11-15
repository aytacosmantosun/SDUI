using System.Collections;
using System.Globalization;
using System.Windows.Forms;

namespace SDUI.Helpers;

public class ListViewColumnSorter : IComparer
{
    /// <summary>
    /// Specifies the column to be sorted
    /// </summary>
    private int _columnToSort;

    /// <summary>
    /// Specifies the order in which to sort (i.e. 'Ascending').
    /// </summary>
    private SortOrder _orderOfSort;

    /// <summary>
    /// Case insensitive comparer object
    /// </summary>
    private readonly CaseInsensitiveComparer _objectCompare;

    /// <summary>
    /// Class constructor.  Initializes various elements
    /// </summary>
    public ListViewColumnSorter()
    {
        _columnToSort = 0;
        _orderOfSort = SortOrder.None;
        _objectCompare = new CaseInsensitiveComparer();
    }

    /// <summary>
    /// This method is inherited from the IComparer interface.  It compares the two objects passed using a case insensitive comparison.
    /// </summary>
    /// <param name="x">First object to be compared</param>
    /// <param name="y">Second object to be compared</param>
    /// <returns>The result of the comparison. "0" if equal, negative if 'x' is less than 'y' and positive if 'x' is greater than 'y'</returns>
    public int Compare(object x, object y)
    {
        var listviewX = (ListViewItem)x;
        var listviewY = (ListViewItem)y;
        if (listviewX == null || listviewY == null)
            return 0;

        if (listviewX.SubItems[0].Text == ".." || listviewY.SubItems[0].Text == "..")
            return 0;

        if (_orderOfSort == SortOrder.None)
            return 0;

        var compareResult = CompareCells(CellText(listviewX), CellText(listviewY));

        if (_orderOfSort == SortOrder.Ascending)
            return compareResult;
        else if (_orderOfSort == SortOrder.Descending)
            return -compareResult;
        else
            return 0;
    }

    /// <summary>The sorted column's text; a row without that cell counts as empty.</summary>
    private string CellText(ListViewItem item) =>
        _columnToSort >= 0 && _columnToSort < item.SubItems.Count ? item.SubItems[_columnToSort].Text ?? string.Empty : string.Empty;

    /// <summary>
    /// AtBot UTa-new-10: numbers sort as numbers (9 before 10, "1,234,567" by value); the text sort put "10" before
    /// "9". The order is total: empty cells, then numbers by value, then other text (case-insensitive), so a column
    /// with a few "-" or names among the numbers still sorts consistently.
    /// </summary>
    private int CompareCells(string x, string y)
    {
        var xEmpty = string.IsNullOrWhiteSpace(x);
        var yEmpty = string.IsNullOrWhiteSpace(y);
        if (xEmpty || yEmpty)
            return xEmpty == yEmpty ? 0 : xEmpty ? -1 : 1;

        var xNumber = TryNumber(x, out var xValue);
        var yNumber = TryNumber(y, out var yValue);
        if (xNumber && yNumber)
        {
            var byValue = xValue.CompareTo(yValue);
            return byValue != 0 ? byValue : _objectCompare.Compare(x, y);
        }

        if (xNumber != yNumber)
            return xNumber ? -1 : 1;

        return _objectCompare.Compare(x, y);
    }

    /// <summary>A whole cell that reads as a number in the interface culture (thousands separators allowed), else invariant.</summary>
    private static bool TryNumber(string text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
        || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);

    /// <summary>
    /// Gets or sets the number of the column to which to apply the sorting operation (Defaults to '0').
    /// </summary>
    public int SortColumn
    {
        set { _columnToSort = value; }
        get { return _columnToSort; }
    }

    /// <summary>
    /// Gets or sets the order of sorting to apply (for example, 'Ascending' or 'Descending').
    /// </summary>
    public SortOrder Order
    {
        set { _orderOfSort = value; }
        get { return _orderOfSort; }
    }
}
