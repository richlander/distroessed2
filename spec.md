# Table resizing spec

There is a markdown table generation class in this repo that operates on static/fragile data. I want to make it dynamic.

- Usage: `src/CveMarkdown/CveReport.cs`
- Implementation: `src/MarkdownHelpers/Table.cs`

Example:

```csharp!
// CVE table
string[] cveLabels = ["CVE", "Description", "Product", "Platforms", "CVSS"];
int[] cveLengths = [16, 20, 16, 16, 20];
Table cveTable = new(Writer.GetWriter(writer), cveLengths);

cveTable.WriteHeader(cveLabels);

foreach (Cve cve in cves.Cves)
{
    cveTable.WriteColumn($"[{cve.Id}][{cve.Id}]");
    cveTable.WriteColumn(cve.Description);
    cveTable.WriteColumn(cve.Product);
    cveTable.WriteColumn(Join(cve.Platforms));
    cveTable.WriteColumn(cve?.Cvss ?? "");
    cveTable.EndRow();
}
```

Change the library so that it no longer takes column lengths (even as an option) but still generates tables with aligned columns.

Algorithm: 

- We need four numbers for each column:
  - A: Header length (sets the min)
  - B: Length of the 50% percentile row for the column
  - C: Length of the longest row that is within 20% of 'B'
  - D: max(A, B, C)
- 'D' is the default column width (where the pipe goes).
- All columns should end at 'D' unless the content goes longer. The content should never be truncated.
- All columns need to start and end with one padding space, for style. That means that allowable space for content is within that, such as "| twenty-six characters here |".
- If the row content (including the padding spaces) is already past 'D', it should stop immediately with '|'.
- B and C percentile values (50, 20) should be configurable as a `Table` class property with the stated defaults. 50 and 20 work quite well but there may be cases where other values work better.

Additional requirements:

- `WriteHeader` and `WriteColumn` can be called in any order with undefined behavior. Make it so WriteHeader can be called only once and `WriteColumn` can only be called after that.
- `Write` is a poor term. Make it `Add`, which is more conventional.
- We can switch to `AddRow` to take `params string[]`, switching multiple calls to a single one.
- Remove `EndRow` as a concept. The call to `AddRow` implicitly ends the row.
- Remove any methods that are not called by implementations within the repo.
- Consider other code quality and usability improvements.
- There is flexibility on the way `IWriter` is handled. `Table` can still take `IWriter` or we can use a pattern like `writer(table)` relying to `table.ToString()`.
