# Dynamic Table Width Refactoring

I have a markdown table generation library that currently uses static, hardcoded column widths. I want to refactor it to calculate optimal column widths
dynamically based on the actual content.

## Current Implementation
- **File**: `src/MarkdownHelpers/Table.cs`
- **Usage**: Search the codebase - multiple files use this class

The current API requires predefined column widths:

```csharp
int[] cveLengths = [16, 20, 16, 16, 20];
Table cveTable = new(Writer.GetWriter(writer), cveLengths);
cveTable.WriteHeader(cveLabels);
// ... multiple WriteColumn calls
cveTable.EndRow();
```

## Goal

Remove the need for static column widths entirely. The table should analyze all content and calculate optimal widths automatically while maintaining perfect
column alignment.

## Critical Architectural Insight

⚠️ Key Challenge: You cannot calculate optimal column widths if you write the table row-by-row as data arrives. Think about why: when you write the header,
you don't yet know how wide the longest content in each column will be.

This means the fundamental write-immediately architecture must change to a collect-then-render pattern.

Width Calculation Requirements

For each column, calculate the optimal width using:

- Minimum: header length
- Data-driven: statistical analysis of content lengths (consider percentiles)
- Tolerance: accommodate reasonable outliers without making columns excessively wide
- Result: never truncate content, but avoid excessive whitespace

## Visual Quality Check

Good example (note the aligned pipes for most rows, exceptions for outliers):

```text
| OS                  | Versions | Architectures     | Lifecycle       |
| ------------------- | -------- | ----------------- | --------------- |
| [Alpine][6]         | 3.22, 3.21, 3.20, 3.19 | Arm32, Arm64, x64 | [Lifecycle][7] |
| [Azure Linux][8]    | 3.0      | Arm64, x64        | None            |
| [CentOS Stream][9]  | 10, 9    | Arm64, ppc64le, s390x, x64 | [Lifecycle][10] |
| [Debian][11]        | 12       | Arm32, Arm64, x64 | [Lifecycle][12] |
| [Fedora][13]        | 42, 41   | Arm32, Arm64, x64 | [Lifecycle][14] |
| [openSUSE Leap][15] | 15.6     | Arm64, x64        | [Lifecycle][16] |
| [Red Hat Enterprise Linux][17] | 10, 9, 8 | Arm64, ppc64le, s390x, x64 | [Lifecycle][18] |
| [SUSE Enterprise Linux][19] | 15.6 | Arm64, x64    | [Lifecycle][20] |
| [Ubuntu][21]        | 25.04, 24.04, 22.04 | Arm32, Arm64, x64 | [Lifecycle][22] |
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

The current API has several issues:
- Requires IWriter parameter (tight coupling)
- Immediate writing prevents width optimization
- Verbose row creation (multiple method calls)
- Poor method naming

Consider: What would a modern, fluent API look like? How do other .NET classes handle the "build then output" pattern?

Validation Steps

1. Visual inspection: Generated tables should have well-balanced, aligned columns
2. Test with actual data: Run cd src/SupportedOsMd && DOTNET_ROLL_FORWARD=Major dotnet run 8
3. Compare output: Tables should be significantly better than narrow, cramped columns

Success Criteria

- No hardcoded column widths anywhere
- Perfect column alignment (pipes line up vertically)
- Appropriate column widths (not too narrow, not excessively wide)
- Content never truncated
- Clean, intuitive API that's pleasant to use
- All existing usage sites work with minimal changes
- Visual output passes the "does this look professional?" test
- Only use the SupportedOsMd tool for testing. All code needs to compile but only this tool needs to be tested for quality output.

Architecture Hint

Consider how StringBuilder works - you build up content, then call ToString() when ready. The same pattern applies here: collect all table data, calculate
optimal widths, then render the complete table.

## Key Improvements:

### 1. **Explicit Architectural Guidance**
- States the core problem directly: "You cannot calculate optimal column widths if you write row-by-row"
- Hints at collect-then-render pattern
- Mentions `StringBuilder` as an architectural example

### 2. **Visual Examples**
- Shows what good output looks like vs bad output
- Emphasizes pipe alignment as a quality indicator
- Makes the "eyes are useful" aspect concrete

### 3. **Validation Steps**
- Includes visual inspection as step 1
- Provides concrete test command
- Emphasizes comparing output quality

### 4. **Quality Gates**
- "Professional looking" test
- Pipe alignment requirement
- Balanced width requirement

### 5. **Progressive Hints**
- Starts with the problem
- Hints at architectural needs
- Suggests API patterns (StringBuilder analogy)
- Points to spec for details, but emphasizes architecture first
