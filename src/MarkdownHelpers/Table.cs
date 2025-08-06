using System;
using System.Collections.Generic;
using System.Linq;

namespace MarkdownHelpers;

public class Table
{
    private readonly IWriter _writer;
    private readonly List<List<string>> _rows = new();
    private string[]? _headers;
    private bool _headerAdded = false;
    private int[]? _columnWidths;
    
    public bool UseOuterPipes { get; set; } = true;
    public double PercentilePosition { get; set; } = 50.0; // B: 50th percentile
    public double WithinPercentage { get; set; } = 20.0;   // C: within 20% of B

    public Table(IWriter writer)
    {
        _writer = writer;
    }

    public void AddHeader(params string[] headers)
    {
        if (_headerAdded)
        {
            throw new InvalidOperationException("Header can only be added once");
        }
        
        _headers = headers;
        _headerAdded = true;
    }

    public void AddRow(params string[] columns)
    {
        if (!_headerAdded)
        {
            throw new InvalidOperationException("Header must be added before adding rows");
        }
        
        if (_headers != null && columns.Length != _headers.Length)
        {
            throw new ArgumentException($"Row must have {_headers.Length} columns");
        }
        
        _rows.Add(new List<string>(columns));
    }

    public void Render()
    {
        if (!_headerAdded || _headers == null)
        {
            throw new InvalidOperationException("Cannot render table without header");
        }

        CalculateColumnWidths();
        
        // Write header
        WriteTableRow(_headers, ' ');
        
        // Write separator
        WriteTableRow(_headers.Select(_ => "").ToArray(), '-', true);
        
        // Write data rows
        foreach (var row in _rows)
        {
            WriteTableRow(row.ToArray(), ' ');
        }
    }

    private void CalculateColumnWidths()
    {
        if (_headers == null) return;
        
        int columnCount = _headers.Length;
        _columnWidths = new int[columnCount];
        
        for (int col = 0; col < columnCount; col++)
        {
            // A: Header length (sets the min)
            int headerLength = _headers[col].Length;
            
            // Get all row values for this column
            var columnValues = _rows.Select(row => col < row.Count ? row[col] : "").ToList();
            
            if (columnValues.Count == 0)
            {
                _columnWidths[col] = headerLength + 2; // +2 for padding spaces
                continue;
            }
            
            // B: Length of the 50% percentile row for the column
            var sortedLengths = columnValues.Select(v => v.Length).OrderBy(x => x).ToList();
            int percentileIndex = (int)Math.Ceiling(sortedLengths.Count * (PercentilePosition / 100.0)) - 1;
            percentileIndex = Math.Max(0, Math.Min(percentileIndex, sortedLengths.Count - 1));
            int percentileLength = sortedLengths[percentileIndex];
            
            // C: Length of the longest row that is within 20% of 'B'
            double threshold = percentileLength * (1.0 + WithinPercentage / 100.0);
            int maxWithinThreshold = columnValues
                .Select(v => v.Length)
                .Where(len => len <= threshold)
                .DefaultIfEmpty(0)
                .Max();
            
            // D: max(A, B, C)
            int calculatedWidth = Math.Max(Math.Max(headerLength, percentileLength), maxWithinThreshold);
            
            // Add 2 for padding spaces (one on each side)
            _columnWidths[col] = calculatedWidth + 2;
        }
    }

    private void WriteTableRow(string[] columns, char fillChar, bool isSeparator = false)
    {
        if (_columnWidths == null) return;
        
        for (int i = 0; i < columns.Length; i++)
        {
            // Write opening pipe
            if (i == 0 && UseOuterPipes)
            {
                _writer.Write('|');
            }
            else if (i > 0)
            {
                _writer.Write('|');
            }
            
            string content = columns[i];
            int targetWidth = _columnWidths[i];
            
            if (isSeparator)
            {
                // For separator rows, fill entire width with the fill character
                _writer.WriteRepeatCharacter(fillChar, targetWidth);
            }
            else
            {
                // For content rows, add padding space, then content
                _writer.Write(' '); // Leading padding space
                _writer.Write(content);
                
                // Calculate remaining space (target width includes the 2 padding spaces)
                int contentAndPaddingLength = content.Length + 2; // +2 for both padding spaces
                
                if (contentAndPaddingLength <= targetWidth)
                {
                    // Content fits within target width, pad to align columns
                    int remaining = targetWidth - contentAndPaddingLength;
                    _writer.WriteRepeatCharacter(fillChar, remaining);
                    _writer.Write(' '); // Trailing padding space
                }
                else
                {
                    // Content exceeds target width, stop immediately (no trailing padding)
                    // The content is never truncated, just extends past the calculated width
                }
            }
        }
        
        // Write closing pipe
        if (UseOuterPipes)
        {
            _writer.Write('|');
        }
        
        _writer.WriteLine();
    }
}
