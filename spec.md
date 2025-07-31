# Dynamic Table Width Refactoring

I have a markdown table generation library that currently uses static, hardcoded column widths. I want to refactor it to calculate optimal column widths dynamically. Think of this as an optimization that requires knowledge of the complete table, stored data about column lengths, and calculated metrics that drive pretty-printer behavior.

The intended solution is a markdown table pretty-printer with aligned columns and whitespace reduction as competing aesthetic goals. The intent is that column pipes match across rows except when making them match requires the use of too much whitespace. The question is how to define "too much" and how to do that consistently across tables. Implementing this solution requires applying a consistent algorithm on a global view of each table. It is not possible to start writing the header pipes until the last row has been seen since the header and the last column row should be made to match, if the algorithm allows.

In a more typical solution, all columns widths are made to match. This is easy to implement, but not necesarily nice to look at. Column widths will be stretched due to an outlier column in one row. A column that has 10 characters in all rows except one that has 50 adds 40 whitespace characters to all the 10-character rows for that column and extends the table width by the same amount. If this situation repeats in multiple columns, then the table can end up much wider than needed, mostly filled with whitespace. These tables are not bad, but a stylistic choice. It isn't what is desired for this implementation.

The static hard-coded widths were used to force a default width. They are a pragmatic but inellegant solution that works for a single programmer but does not scale to a large set of programmers. We want a design that would well for hundreds of programmers generating thousands of different markdown tables.

An earlier protype of this feature led to the discovery that magic numbers (defined later) are required to break the tie between matching columns and whitespace reduction. This is both because it very difficult to determine what "looks good" without significant iteration and we want the same model applied to all tables.

## Goal

Remove the need for (and ability to provide) static column widths entirely. The implementation should analyze all table content and calculate optimal widths automatically while maintaining perfect column alignment for most columns while globally reducing whitespace padding. This is achieved by following the defined algorithm.

Note: I greatly appreciate the degree to which coding assistants support markdown. However, it starts to get weird when the target of your prompt or app is markdown. I often have to ask for the "respoonse in markdown with four backticks". That works. In terms of this domain, some coding assistants have markdown table pretty-printers that implement the "typical solution" discussed above. That approch doesn't match the goals. The LLM can produce the correct solution and then when asked to produce it will print it with the pretty-printer and its associate style. It is confusing and something to watch out for. It also make be the case that converting a markdown table to tokens doesn't preserve the formatting (space and pipes) with high-fidelity.

## Current Implementation
- **File**: `src/MarkdownHelpers/Table.cs`
- **Usage**: Search the codebase - multiple files use this class

The current API requires the following usage pattern, including providing predefined column widths:

```csharp
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

## Core algorithm description

The algorithm is best described in two phases, to improve comprehension. This section describes the core algorithm.

The following scheme provides a smoothing algorithm that helps us implement a "pretty-printer with aligned columns and whitespace reduction as competing aesthetic goals".

Factors:

- "PercentileThreshold": 0.5 (must be between 0 and 1.0)
- "ToleranceMultiplier": 2.0 (must be > 1.0)

We need four numbers for each column:

- A: Header length (sets the min; it is a legal outlier)
- B: Length of the percentile row (like 50% percentile) for the column defined by `PercentileThreshold`
- C: Length of the longest row <= B * `ToleranceMultiplier` (this value needs to be above 1.0 to be effective)
- D: max(A, B, C) + 2

'D' is the total column width including padding spaces (after the leading pipe and before the trailing one).

Note: The +2 accounts for the required padding spaces before and after content (e.g., "| content |" needs 2 extra characters beyond the content length).

Note: "D" could have a special name, too, however it is just a local so doesn't need a special name. "PercentileThreshold" and "ToleranceMultiplier" are intended to be configurable properties on the `Table` type with the default values specified.

Layout rules:

1. All columns should end at 'D' unless the content goes longer. The content should never be truncated.
2. All columns need to start and end with one padding space, for style. That means that allowable space for content is within that, such as "| twenty-six characters here |".
3. If the row content (including the padding spaces) is greater than 'D', then the column should stop immediately with '|'.
4. If the row content (including the padding spaces) is less than 'D', the the column should pad to D ending with '|'.
4. The separator row uses the same padding structure as content rows: `| ---- |` (space + dashes + space), ensuring visual symmetry and alignment.

Let's take a look with an example:

| Lorem | Lorem Ipsum | Dolor dolor   | Ipsum |
| ----- | ----------- | ------------- | ----- |
| Duis  | Duis aute   | cillum dolore | dolor |
| dolor | consequat   | proident      | dolor |
| Velit | sunt in culpa qui | nostrud | dolor |
| Velit | sunt in culpa qui | qui     | sunt in culpa |

This pretty-printing result is considered good.

We see a few things at play:

- In column 1, all rows line up. The differentiation provided by the algorithm is not yet visible.
- In column 2, we see that the header defines the column width and the last two columns are outliers which pushes their pipes right.
- In column 3, we see that the first row defines the column width and that the last two rows are able to correct thier "overages" and end at the desired spot. The "nodstrud" column ends with no space left. The "qui" column pads to the correct position.
- In column 4, we see most rows are able to match on the final row pipe, creating a very nice rendering. The last row exceeds the space budget with a pipe that goes further to the right than others.
- Looked at holistically, you can see that an implementation is best served by a running total for the column widths. The need for that is demonstrated by the sophisticated padding in the last two rows, which itentionally conserves and adds spacs with the intent to align on column pipes further to the right.

## Overflow algorithm description

This section describes an enhanced algorithm that smooths jagged right edges in tables. It builds on the core algorithm by considering how content overflow from earlier columns affects later columns.

### The problem: jagged right edges

Let's look at the following table.

```text
| OS                             | Versions | Architectures              | Lifecycle       |
| ------------------------------ | -------- | -------------------------- | --------------- |
| [Alpine][6]                    | 3.22, 3.21, 3.20, 3.19 | Arm32, Arm64, x64 | [Lifecycle][7] |
| [Azure Linux][8]               | 3.0      | Arm64, x64                 | None            |
| [CentOS Stream][9]             | 10, 9    | Arm64, ppc64le, s390x, x64 | [Lifecycle][10] |
| [Debian][11]                   | 12       | Arm32, Arm64, x64          | [Lifecycle][12] |
| [Fedora][13]                   | 42, 41   | Arm32, Arm64, x64          | [Lifecycle][14] |
| [openSUSE Leap][15]            | 15.6     | Arm64, x64                 | [Lifecycle][16] |
| [Red Hat Enterprise Linux][17] | 10, 9, 8 | Arm64, ppc64le, s390x, x64 | [Lifecycle][18] |
| [SUSE Enterprise Linux][19]    | 15.6     | Arm64, x64                 | [Lifecycle][20] |
| [Ubuntu][21]                   | 25.04, 24.04, 22.04 | Arm32, Arm64, x64 | [Lifecycle][22] |
```

The rightmost edge of the table is more jagged than it needs to be. It was generated via the core algorithm, demonstrating its shortcomings. We can add another layer of smoothing to it such that the right-edge is no longer jagged.

### The solution: right-edge smoothing

The overflow algorithm considers the effective position of content after accounting for earlier column overflows. This allows the algorithm to widen the final column to accommodate shifted content, creating a smoother right edge.

Instead the table could look like the following.

```text
| OS                             | Versions | Architectures              | Lifecycle           |
| ------------------------------ | -------- | -------------------------- | ------------------- |
| [Alpine][6]                    | 3.22, 3.21, 3.20, 3.19 | Arm32, Arm64, x64 | [Lifecycle][7] |
| [Azure Linux][8]               | 3.0      | Arm64, x64                 | None                |
| [CentOS Stream][9]             | 10, 9    | Arm64, ppc64le, s390x, x64 | [Lifecycle][10]     |
| [Debian][11]                   | 12       | Arm32, Arm64, x64          | [Lifecycle][12]     |
| [Fedora][13]                   | 42, 41   | Arm32, Arm64, x64          | [Lifecycle][14]     |
| [openSUSE Leap][15]            | 15.6     | Arm64, x64                 | [Lifecycle][16]     |
| [Red Hat Enterprise Linux][17] | 10, 9, 8 | Arm64, ppc64le, s390x, x64 | [Lifecycle][18]     |
| [SUSE Enterprise Linux][19]    | 15.6     | Arm64, x64                 | [Lifecycle][20]     |
| [Ubuntu][21]                   | 25.04, 24.04, 22.04 | Arm32, Arm64, x64 | [Lifecycle][22]   |
```

### Enhanced algorithm

We need five numbers for each column (extending the core algorithm):

- A: Header length (sets the min; it is a legal outlier)
- B: Length of the percentile row (like 50% percentile) for the column defined by `PercentileThreshold`
- C1: Length of the longest row <= B * `ToleranceMultiplier` (original core algorithm)
- C2: Length of the longest content that, when positioned after accumulated overflow from previous columns, fits within B * `ToleranceMultiplier`
- D2: max(A, B, C1, C2) + 2

### Understanding C2: Effective Positioning

C2 accounts for how earlier column overflows shift later content.

Without C2 consideration:

```text
| [SUSE Enterprise Linux][19]    | 15.6     | Arm64, x64                 | [Lifecycle][20] |
| [Ubuntu][21]                   | 25.04, 24.04, 22.04 | Arm32, Arm64, x64 | [Lifecycle][22] |
```

With C2 consideration:

| [SUSE Enterprise Linux][19]    | 15.6     | Arm64, x64                 | [Lifecycle][20]   |
| [Ubuntu][21]                   | 25.04, 24.04, 22.04 | Arm32, Arm64, x64 | [Lifecycle][22] |

The algorithm recognizes that "[Lifecycle][22]" in the Ubuntu row appears further right due to the longer version string. If this "effective position" (content length + accumulated overflow) falls within the tolerance, the final column (for all rows using the width defined by D) can be widened to accommodate it.

### Implementation Approach

We need to track some more numbers for this algorith, using different letters.

- V: Accumulated length for where the column naturally starts per D (not the overflow case)
- X: Acculimated length for where the column actually starts (overflow case)
- Y: Y - X
- Z: content.Length + Y
- C2: Length of the longest Z value that fits within B * `ToleranceMultiplier` (using the value B from above)
- D2: max(A, B, C1, C2) + 2

Let's reason about that in terms of the example above, without C2 consideration.

- The content length of the last column for both rows is the same: 16 characters.
- The last row starts two characters later. As a result, we want to represent a longer content content length: 18 characters (16 + 2).
- The "Z" calulation represents that.
- "C2" represents the longest "Z" value fits within B * `ToleranceMultiplier`, just like for C1 (same rule over different data)
- After that, it's easy to add another term into the D (renamed D2) calculation.

## Algorithm Summary

**Specific values required:**
- Use the **50th percentile (median)** of content lengths as the base width calculation
- Apply a **100% tolerance multiplier** (2x) to accommodate reasonable outliers
- Formula: `max(header_length, median_row_length, longest_row_within_100%_of_median, longest_overflow_row_within_100%_of_median)`

**Do NOT use:**
- 90th percentile (creates excessive whitespace)
- 95th percentile (creates excessive whitespace)
- Simple maximum (one outlier ruins everything)

This scheme is statistical. It requires a complete view of the content. The 50/100 combination is specifically tuned to produce professional-looking tables that balance readability with efficient space usage. These numbers were arrived at by iteration and scoring them with human eyes not unix core tools. The magic numbers specified give you the same benefit as human eyes for this problem.

## Implementation tips

- The table concepts in the forumula all require having access to the complete table in memory.
- The overflow calculation requires developing an undertanding the acculated width of the row, column by column.
- A great implementation will generate a plan ahead of time and render the table in terms of that plan.
- An amazing implementation woulc be easy to extend with different styles of pretty-printers based on a clear separation between planning and rendering.

## Critical Architectural Insight

⚠️ Key Challenge: You cannot calculate optimal column widths if you write the table row-by-row as data arrives. Think about why: when you write the header, you don't yet know how wide the longest content in each column will be.

This means the fundamental write-immediately architecture must change to a collect-then-render pattern.

## Visual Quality Check

Good example (core rules only applied; note the aligned pipes for most rows, exceptions for outliers):

```text
| OS                             | Versions | Architectures              | Lifecycle       |
| ------------------------------ | -------- | -------------------------- | --------------- |
| [Alpine][6]                    | 3.22, 3.21, 3.20, 3.19 | Arm32, Arm64, x64 | [Lifecycle][7] |
| [Azure Linux][8]               | 3.0      | Arm64, x64                 | None            |
| [CentOS Stream][9]             | 10, 9    | Arm64, ppc64le, s390x, x64 | [Lifecycle][10] |
| [Debian][11]                   | 12       | Arm32, Arm64, x64          | [Lifecycle][12] |
| [Fedora][13]                   | 42, 41   | Arm32, Arm64, x64          | [Lifecycle][14] |
| [openSUSE Leap][15]            | 15.6     | Arm64, x64                 | [Lifecycle][16] |
| [Red Hat Enterprise Linux][17] | 10, 9, 8 | Arm64, ppc64le, s390x, x64 | [Lifecycle][18] |
| [SUSE Enterprise Linux][19]    | 15.6     | Arm64, x64                 | [Lifecycle][20] |
| [Ubuntu][21]                   | 25.04, 24.04, 22.04 | Arm32, Arm64, x64 | [Lifecycle][22] |
```

Great example (both core and overflow rules applied):

```text
| OS                             | Versions | Architectures              | Lifecycle           |
| ------------------------------ | -------- | -------------------------- | ------------------- |
| [Alpine][6]                    | 3.22, 3.21, 3.20, 3.19 | Arm32, Arm64, x64 | [Lifecycle][7] |
| [Azure Linux][8]               | 3.0      | Arm64, x64                 | None                |
| [CentOS Stream][9]             | 10, 9    | Arm64, ppc64le, s390x, x64 | [Lifecycle][10]     |
| [Debian][11]                   | 12       | Arm32, Arm64, x64          | [Lifecycle][12]     |
| [Fedora][13]                   | 42, 41   | Arm32, Arm64, x64          | [Lifecycle][14]     |
| [openSUSE Leap][15]            | 15.6     | Arm64, x64                 | [Lifecycle][16]     |
| [Red Hat Enterprise Linux][17] | 10, 9, 8 | Arm64, ppc64le, s390x, x64 | [Lifecycle][18]     |
| [SUSE Enterprise Linux][19]    | 15.6     | Arm64, x64                 | [Lifecycle][20]     |
| [Ubuntu][21]                   | 25.04, 24.04, 22.04 | Arm32, Arm64, x64 | [Lifecycle][22]   |
```

Bad example (perfectly aligned):

```text
| OS                             | Versions               | Architectures              | Lifecycle       |
|--------------------------------|------------------------|----------------------------|-----------------|
| [Alpine][6]                    | 3.22, 3.21, 3.20, 3.19 | Arm32, Arm64, x64          | [Lifecycle][7]  |
| [Azure Linux][8]               | 3.0                    | Arm64, x64                 | None            |
| [CentOS Stream][9]             | 10, 9                  | Arm64, ppc64le, s390x, x64 | [Lifecycle][10] |
| [Debian][11]                   | 12                     | Arm32, Arm64, x64          | [Lifecycle][12] |
| [Fedora][13]                   | 42, 41                 | Arm32, Arm64, x64          | [Lifecycle][14] |
| [openSUSE Leap][15]            | 15.6                   | Arm64, x64                 | [Lifecycle][16] |
| [Red Hat Enterprise Linux][17] | 10, 9, 8               | Arm64, ppc64le, s390x, x64 | [Lifecycle][18] |
| [SUSE Enterprise Linux][19]    | 15.6                   | Arm64, x64                 | [Lifecycle][20] |
| [Ubuntu][21]                   | 25.04, 24.04, 22.04    | Arm32, Arm64, x64          | [Lifecycle][22] |
```

Bad example (misaligned pipes, poor width distribution):

```text
| OS | Versions | Architectures | Lifecycle |
| -- | -------- | ------------- | --------- |
| [Red Hat Enterprise Linux][17] | 10, 9, 8 | Arm64, ppc64le, s390x, x64 | [Lifecycle][18] |
```

## API Design Considerations

Requirements:
- The B and C values defined above should be named "PercentileThreshold" and "ToleranceMultiplier", respectively
- "PercentileThreshold" (0.5) and "ToleranceMultiplier" (2.0) should be configurable as a Table class property with the stated defaults. These values work quite well but there may be cases where other values work better.

The current API has several issues:
- Requires IWriter parameter (tight coupling)
- Immediate writing prevents width optimization
- Verbose row creation (multiple method calls)
- Poor method naming ("Write" should be replaced with "Add")
- No forced ordering on adding the header vs columns.
- The `UseOuterPipes` capability significantly complicates the implementation and is unused by the callers in the repo. It should be removed.

A successfull implementation should provide the following simpler usage pattern:

```csharp
string[] cveLabels = ["CVE", "Description", "Product", "Platforms", "CVSS"];
Table cveTable = new();

cveTable.AddHeader(cveLabels);

foreach (Cve cve in cves.Cves)
{
    cveTable.AddColumn($"[{cve.Id}][{cve.Id}]", cve.Description, cve.Product, Join(cve.Platforms), cve?.Cvss ?? "");
}

writer.Write(cveTable);
```

The intent is that `Table.AddColumn` takes a `params string[]`, allowing for multiple usage patterns. The `Table` class should be decoupled from `IWriter` and rely on `ToString` as its rendering model (much like `StringBuilder`). `params string[]` requires a project be configured for C# 13+ and/or .NET 9+.

Validation Steps

1. Visual inspection: Generated tables should have well-balanced, aligned columns
2. Test with actual data: Run cd src/SupportedOsMd && DOTNET_ROLL_FORWARD=Major dotnet run 8
3. Compare output: Tables should be significantly better than narrow, cramped columns

Success Criteria

- No hardcoded column widths anywhere
- Perfect column alignment (pipes line up vertically) for most columns
- Content never truncated
- Clean, intuitive API that's pleasant to use
- All existing usage sites work with minimal changes
- Visual output passes the "does this look it matches the algorithm" test
- Only use the SupportedOsMd tool for testing. All code needs to compile but only this tool needs to be tested for quality output.

Architecture Hints:

- Consider how StringBuilder works - you build up content, then call ToString() when ready.
- The same pattern applies here: collect all table data, calculate optimal widths, then render the complete table.
