# GiSTConfigX

A Windows Forms application for generating XML configuration files and SQLite databases from Excel-based data dictionaries. This tool is designed to streamline the creation of survey forms and data collection instruments by automating the generation of configuration files and database schemas.

- compound logic checks
- date range addition
- added unique logic check

## Overview

GiSTConfigX reads Excel workbooks containing structured data dictionaries and automatically generates:
- XML configuration files for survey/form definitions
- SQLite database schemas with appropriate table structures
- Comprehensive validation and error logging

## Features

- **Excel to XML Conversion**: Automatically converts Excel data dictionaries into structured XML configuration files
- **Database Generation**: Creates SQLite databases with tables based on data dictionary specifications
- **Comprehensive Validation**: Validates field names, question types, field types, logic checks, skip patterns, and more
- **Error Detection**: Identifies duplicate columns, invalid syntax, missing required fields, and logic errors
- **Master Table Copying**: Supports copying reference tables from a master configuration database
- **Detailed Logging**: Generates comprehensive log files for debugging and validation

## System Requirements

- Windows OS (tested on Windows 10/11)
- .NET Framework 4.7.2 or higher
- Microsoft Excel (for Excel Interop functionality)
- SQLite support

## Dependencies

- **System.Data.SQLite** (v1.0.119.0) - SQLite database support
- **Microsoft.Office.Interop.Excel** - Excel file reading and processing
- **Windows Forms** - User interface framework

## Installation

1. Clone or download this repository
2. Open `GistConfigX.sln` in Visual Studio
3. Restore NuGet packages:
   - Right-click on the project in Solution Explorer
   - Select "Manage NuGet Packages"
   - Install `System.Data.SQLite` if not already installed
4. Build the solution (Build > Build Solution)
5. Run the application

## Configuration

Before running the application, update the following paths in `Main.cs` (lines 52-74):

```csharp
// Path to your Excel data dictionary file
readonly string excelFile = "C:\\GeoffOffline\\GiSTConfigX\\Excel\\survey.xlsx";

// Path where generated XML files will be saved
readonly string xmlPath = "C:\\temp\\";

// Path where log files will be saved
readonly string logfilePath = "C:\\temp\\";

// Path where SQLite database will be created
readonly string db_path = "C:\\gistx\\database\\gistx.sqlite";

// Path to master configuration database (for copying reference tables)
public string sourceDatabasePath = "C:\\gistx\\database\\gistx_config.sqlite";

// Names of tables to copy from master database
public string[] sourceTableNames = { "config" };
```

## Usage

1. **Prepare Excel Data Dictionary**
   - Create an Excel workbook with worksheets named with suffix `_dd` or `_xml`
   - Include a `crfs` worksheet to define table metadata (tablename, primarykey, displayname)
   - Follow the data dictionary format (see below)

2. **Run the Application**
   - Launch GiSTConfigX.exe
   - Click the "Generate XML" button
   - The application will:
     - Validate all worksheets
     - Generate XML files
     - Create SQLite database and tables
     - Copy master tables (if configured)
     - Display success or error messages

3. **Check Output**
   - XML files: Located in the configured `xmlPath`
   - SQLite database: Located at the configured `db_path`
   - Log file: `gistlogfile.txt` in the configured `logfilePath`

## Excel Data Dictionary Format

### Required Column Headers

Each worksheet ending in `_dd` or `_xml` must have these 14 columns:

| Column # | Header Name | Description |
|----------|-------------|-------------|
| 1 | FieldName | Variable/field name (lowercase, alphanumeric + underscore) |
| 2 | QuestionType | Type of question (radio, checkbox, text, date, etc.) |
| 3 | FieldType | Data type (text, integer, date, etc.) |
| 4 | QuestionText | The actual question text shown to users |
| 5 | MaxCharacters | Maximum character length (for text fields) |
| 6 | Responses | Response options (for radio/checkbox/combobox) |
| 7 | LowerRange | Minimum numeric value (for validation) |
| 8 | UpperRange | Maximum numeric value (for validation) |
| 9 | LogicCheck | Conditional validation rules |
| 10 | DontKnow | Enable "Don't Know" button (True/False) |
| 11 | Refuse | Enable "Refuse to Answer" button (True/False) |
| 12 | NA | Enable "Not Applicable" button (True/False) |
| 13 | Skip | Skip/branching logic |
| 14 | Comments | Developer comments (not processed) |

### Valid Question Types

- `radio` - Single selection (radio buttons)
- `checkbox` - Multiple selection
- `combobox` - Dropdown selection
- `text` - Text entry
- `date` - Date picker
- `information` - Display-only text
- `automatic` - Auto-calculated/system field
- `button` - Action button

### Valid Field Types

- `text` - Text string
- `integer` - Whole numbers
- `text_integer` - Text field accepting only integers
- `text_decimal` - Text field accepting decimal numbers
- `text_id` - Text identifier
- `phone_num` - Phone number
- `date` - Date only
- `datetime` - Date and time
- `hourmin` - Hour:minute format
- `n/a` - Not applicable (for information questions)

### Response Format

For radio buttons, checkboxes, and comboboxes, responses should be formatted as:
```
value1:Display Text 1
value2:Display Text 2
value3:Display Text 3
```

**Important Rules:**
- Each response on a new line
- Format: `value:text`
- No spaces before the value
- No spaces after the colon
- Values must be unique

### Logic Check Format

Logic checks validate responses dynamically or against fixed values:

**Dynamic Logic** (comparing two fields):
```
dynamic: field1 = field2, error_message Your custom error message
dynamic: field1 > field2, error_message Field1 must be greater than Field2
dynamic: field1 'contains' field2, error_message Field1 must contain Field2
```

**Fixed Logic** (comparing field to constant):
```
fixed: field1 = 5 'and' current_response = 10, error_message Invalid combination
```

Supported conditions: `=`, `>`, `>=`, `<`, `<=`, `<>`, `'contains'`, `'does not contain'`

### Skip Logic Format

Skip patterns control question flow based on previous responses:

**Preskip** (evaluated before showing question):
```
preskip: fieldname = value, skip_to_fieldname
preskip: fieldname 'contains' text, skip_to_fieldname
```

**Postskip** (evaluated after answering question):
```
postskip: fieldname <> value, skip_to_fieldname
postskip: fieldname 'does not contain' text, skip_to_fieldname
```

## Validation Rules

The application validates:

### Field Names
- Must be lowercase
- Must start with a letter (not a number or underscore)
- Only alphanumeric characters and underscores allowed
- No spaces allowed
- Must be unique within a worksheet

### Question/Field Type Combinations
- `radio` questions must have `integer` field type
- `checkbox` questions must have `text` field type
- `date` questions must have `date` or `datetime` field type

### Required Fields
- QuestionText is required (except for automatic questions)
- MaxCharacters is required for text, text_integer, and phone_num field types
- Responses are required for radio and checkbox questions

### Logic Checks
- Referenced field names must exist in the worksheet
- Referenced fields must appear before the current question
- Proper syntax for conditions and operators

### Skip Logic
- Field to check must exist and appear before current question
- Field to skip to must exist and appear after current question
- Cannot skip to the same question

### Response Options
- No duplicate values allowed
- Proper format with colon separator
- Each response must have both value and text

### Duplicate Detection
- Checks for duplicate field names within worksheets
- Warns about empty rows at the end of worksheets

## Output

### XML Files

Generated XML files follow this structure:

```xml
<?xml version='1.0' encoding='utf-8'?>
<survey>
    <question type='radio' fieldname='age' fieldtype='integer'>
        <text>What is your age?</text>
        <numeric_check>
            <values minvalue='0' maxvalue='120' other_values='0' message='Age must be between 0 and 120!'></values>
        </numeric_check>
        <responses>
            <response value='1'>Under 18</response>
            <response value='2'>18-65</response>
            <response value='3'>Over 65</response>
        </responses>
    </question>
</survey>
```

### SQLite Database

The application creates:
- One table per data dictionary worksheet
- Columns matching field names with appropriate data types
- A `crfs` table containing metadata about all forms
- Optional master tables copied from configuration database

### Log File

A detailed log file (`gistlogfile.txt`) contains:
- Validation results for each worksheet
- Error messages with specific field names and issues
- List of automatic variables requiring code implementation
- Processing summary

## Error Handling

The application will:
- Stop processing if errors are found in any worksheet
- Display a message box indicating success or failure
- Generate a detailed log file regardless of success/failure
- Prevent database creation if validation errors exist

Common errors include:
- Invalid field names
- Mismatched question types and field types
- Missing required values
- Invalid syntax in logic checks or skip patterns
- Duplicate field names
- References to non-existent fields

## Technical Details

- **Language**: C# (.NET Framework 4.7.2)
- **UI Framework**: Windows Forms
- **Database**: SQLite 3
- **Excel Processing**: Microsoft Office Interop
- **Architecture**: Single-form Windows application with comprehensive validation logic

### Key Classes

- `Question`: Represents a single question/field with all properties
- `Main`: Main form containing all processing logic

### Key Functions

- `CreateQuestionList()`: Parses Excel worksheet into Question objects
- `WriteXML()`: Generates XML configuration files
- `CreateTableInDatabase()`: Creates SQLite tables from Question objects
- `CheckLogicFieldNames()`: Validates logic check syntax and field references
- `CheckSkipToFieldNames()`: Validates skip logic syntax and field references

## Version

Current Version: 2025-11-09 (displayed in application)

## License

(Add your license information here)

## Author

(Add author/contributor information here)

## Support

For issues or questions:
1. Check the generated log file for detailed error information
2. Verify Excel data dictionary format matches specifications
3. Ensure all file paths are correctly configured
4. Confirm all required dependencies are installed

## Notes

- The application expects specific file paths to be configured before use
- Master table copying is optional and only occurs if the source database exists
- All worksheets not ending in `_dd` or `_xml` are ignored (except the `crfs` worksheet)
- Information-type questions do not create database columns
- Merged rows in Excel worksheets are treated as non-question rows and skipped
