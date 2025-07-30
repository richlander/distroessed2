using System.Text;

namespace MarkdownHelpers;

public class Table(IWriter writer)
{
    private readonly IWriter _writer = writer;
    private readonly List<string[]> _rows = [];
    private string[]? _header;
    private bool _headerAdded = false;
    private bool _isOutputGenerated = false;
    
    public bool UseOuterPipes { get; set; } = true;
    public int ContentPercentile { get; set; } = 50;
    public int LengthVariancePercentile { get; set; } = 20;

    public void AddHeader(ReadOnlySpan<string> labels)
    {
        if (_headerAdded)
        {
            throw new InvalidOperationException("Header can only be added once");
        }

        _header = labels.ToArray();
        _headerAdded = true;
    }

    public void AddRow(params string[] columns)
    {
        if (!_headerAdded)
        {
            throw new InvalidOperationException("Header must be added before rows");
        }

        if (_isOutputGenerated)
        {
            throw new InvalidOperationException("Cannot add rows after output has been generated");
        }

        if (columns.Length != _header!.Length)
        {
            throw new ArgumentException($"Row must have {_header.Length} columns, got {columns.Length}");
        }

        _rows.Add(columns);
    }

    public void Generate()
    {
        if (!_headerAdded)
        {
            throw new InvalidOperationException("Header must be added before generating output");
        }

        if (_isOutputGenerated)
        {
            return; // Already generated
        }

        _isOutputGenerated = true;

        int[] columnWidths = CalculateColumnWidths();
        
        // Write header
        WriteRowWithWidths(_header!, columnWidths);
        WriteHeaderSeparator(columnWidths);

        // Write data rows
        foreach (var row in _rows)
        {
            WriteRowWithWidths(row, columnWidths);
        }
    }

    public override string ToString()
    {
        if (!_headerAdded)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        var stringWriter = Writer.GetWriter(sb);
        var tempTable = new Table(stringWriter)
        {
            UseOuterPipes = this.UseOuterPipes,
            ContentPercentile = this.ContentPercentile,
            LengthVariancePercentile = this.LengthVariancePercentile
        };

        tempTable.AddHeader(_header!);
        foreach (var row in _rows)
        {
            tempTable.AddRow(row);
        }
        
        tempTable.Generate();
        return sb.ToString();
    }

    private int[] CalculateColumnWidths()
    {
        if (_header == null) throw new InvalidOperationException("Header not set");

        int columnCount = _header.Length;
        int[] widths = new int[columnCount];

        for (int col = 0; col < columnCount; col++)
        {
            // A: Header length (sets the min)
            int headerLength = _header[col].Length;

            if (_rows.Count == 0)
            {
                // No rows, use header length + padding
                widths[col] = headerLength + 2; // +2 for padding spaces
                continue;
            }

            // Get all content lengths for this column
            var contentLengths = _rows.Select(row => row[col].Length).OrderBy(x => x).ToArray();

            // B: Length of the 50% percentile row for the column
            int percentileIndex = (contentLengths.Length * ContentPercentile) / 100;
            if (percentileIndex >= contentLengths.Length) percentileIndex = contentLengths.Length - 1;
            int percentileLength = contentLengths[percentileIndex];

            // C: Length of the longest row that is within 20% of 'B'
            int maxVarianceLength = (int)(percentileLength * (1.0 + LengthVariancePercentile / 100.0));
            int longestWithinVariance = contentLengths.Where(len => len <= maxVarianceLength).Max();

            // D: max(A, B, C)
            int calculatedWidth = Math.Max(headerLength, Math.Max(percentileLength, longestWithinVariance));
            
            // Add padding spaces (1 on each side)
            widths[col] = calculatedWidth + 2;
        }

        return widths;
    }

    private void WriteRowWithWidths(string[] columns, int[] widths)
    {
        for (int i = 0; i < columns.Length; i++)
        {
            string content = columns[i];
            int targetWidth = widths[i];

            // Write opening pipe and space
            if (i > 0 || UseOuterPipes)
            {
                _writer.Write("| ");
            }

            // Write content
            _writer.Write(content);

            // Calculate remaining space for padding
            int contentWithPaddingLength = content.Length + 2; // +2 for spaces on both sides
            bool isLastColumn = i == columns.Length - 1;

            if (contentWithPaddingLength >= targetWidth)
            {
                // Content exceeds target width, just add closing space and pipe
                _writer.Write(' ');
                if (isLastColumn && UseOuterPipes)
                {
                    _writer.Write('|');
                }
            }
            else
            {
                // Add padding to reach target width
                int padding = targetWidth - content.Length - 1; // -1 because we add one space after content
                _writer.WriteRepeatCharacter(' ', padding);
                
                if (isLastColumn && UseOuterPipes)
                {
                    _writer.Write('|');
                }
            }
        }
        
        _writer.WriteLine();
    }

    private void WriteHeaderSeparator(int[] widths)
    {
        for (int i = 0; i < widths.Length; i++)
        {
            if (i > 0 || UseOuterPipes)
            {
                _writer.Write("| ");
            }

            // Fill with dashes up to the target width minus padding
            int dashCount = widths[i] - 2; // -2 for padding spaces
            _writer.WriteRepeatCharacter('-', dashCount);
            _writer.Write(' ');

            if (i == widths.Length - 1 && UseOuterPipes)
            {
                _writer.Write('|');
            }
        }
        
        _writer.WriteLine();
    }
}
