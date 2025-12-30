using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Excel = Microsoft.Office.Interop.Excel;
using Microsoft.Office.Interop.Excel;

namespace generatexml
{
    public class ExcelReader
    {
        public List<string> logstring = new List<string>();
        public bool errorsEncountered = false;
        public bool worksheetErrorsEncountered = false;
        public List<Question> QuestionList = new List<Question>();
        readonly int numberOfColumns = 14;
        readonly string[] columnNamesArray = { "FieldName", "QuestionType", "FieldType", "QuestionText", "MaxCharacters", "Responses", "LowerRange", "UpperRange", "LogicCheck", "DontKnow", "Refuse", "NA", "Skip", "Comments" };

        // Static compiled Regex patterns for performance (compiled once, reused)
        private static readonly Regex NumericOnlyRegex = new Regex(@"^\d+$", RegexOptions.Compiled);
        private static readonly Regex DecimalRegex = new Regex(@"^\d+(\.\d+)?$", RegexOptions.Compiled);
        private static readonly Regex DateRangeRegex = new Regex(@"^([+-])(\d+)([dwmy])$", RegexOptions.Compiled);
        private static readonly Regex HardCodedDateRegex = new Regex(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled);
        private static readonly Regex FieldNameRegex = new Regex(@"\b[a-z_][a-z0-9_]*\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex QuotedStringRegex = new Regex(@"'[^']*'", RegexOptions.Compiled);
        private static readonly Regex FilterMatchRegex = new Regex(@"^(\w+)\s*(?:(=|!=|<>|>|<|>=|<=)\s*)?(.+)$", RegexOptions.Compiled);
        private static readonly Regex ParameterRegex = new Regex(@"^(@?\w+)\s*=\s*(\w+)$", RegexOptions.Compiled);
        private static readonly Regex WhenConditionRegex = new Regex(@"^(\w+)\s+(=|!=|<>|>=|<=|>|<)\s+(.+?)\s*=>\s*(.+)$", RegexOptions.Compiled);

        // Static HashSet constants for O(1) lookups (instead of O(n) array scans)
        private static readonly HashSet<string> ValidQuestionTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "radio", "combobox", "checkbox", "text", "date", "information", "automatic", "button" };

        private static readonly HashSet<string> ValidFieldTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "text", "datetime", "date", "phone_num", "integer", "text_integer", "text_decimal", "text_id", "n/a", "hourmin" };

        private static readonly HashSet<string> BuiltInAutoFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "starttime", "stoptime", "uniqueid", "swver", "survey_id", "lastmod" };

        private static readonly HashSet<string> DateFieldTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "date", "datetime" };

        private static readonly HashSet<string> LogicKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "and", "or", "not" };

        // Helper method for bulk read - gets cell value from cached array with trimming
        private static string GetCellValue(object[,] data, int row, int col)
        {
            if (data == null || row < 1 || row > data.GetLength(0) || col < 1 || col > data.GetLength(1))
                return "";
            return data[row, col]?.ToString()?.Trim() ?? "";
        }

        // Helper method for bulk read - gets cell value without trimming (for responses that may have intentional formatting)
        private static string GetCellValueRaw(object[,] data, int row, int col)
        {
            if (data == null || row < 1 || row > data.GetLength(0) || col < 1 || col > data.GetLength(1))
                return "";
            return data[row, col]?.ToString() ?? "";
        }

        public void CreateQuestionList(Worksheet worksheet, Action<string, string> onQuestionProcessed = null)
        {
            try
            {
                // Set the flag to false
                worksheetErrorsEncountered = false;

                // Write table name to log file
                logstring.Add("\rChecking worksheet: '" + worksheet.Name + "'");

                Excel.Range range;

                // Get the range of used cells in the Excel file
                range = worksheet.UsedRange;

                // BULK READ: Load entire worksheet data into memory for fast access
                // This dramatically reduces COM interop calls from ~4760 per worksheet to just 1
                object[,] data = range.Value2 as object[,];

                // Variable to get the total number of rows used in the Excel file
                int numRows = data != null ? data.GetLength(0) : 0;

                // Used to determine if a row is merged or not
                // All rows that are not questions, must be merged
                Range rowRange = null;

                // Clear the previous QuestionList, if it existed
                QuestionList.Clear();

                // Iterate through each row (question)
                // and create a question object for each question.
                // Each question object is added to the QuestionList list.
                for (int rowCount = 1; rowCount <= numRows; rowCount++)
                {
                    try
                    {
                        rowRange = worksheet.Cells[rowCount, numberOfColumns];

                        string[] currentColumnNamesArr = new string[numberOfColumns];
                        if (rowCount == 1)
                        {

                            for (int i = 0; i < numberOfColumns; i++)
                            {
                                currentColumnNamesArr[i] = GetCellValue(data, 1, i + 1);
                            }
                            // Check to make sure the column names are correct
                            if (!columnNamesArray.SequenceEqual(currentColumnNamesArr))
                            {
                                errorsEncountered = true;
                                worksheetErrorsEncountered = true;
                                logstring.Add("ERROR: " + "The header names in the " + worksheet.Name + " are incorrect. " + "Header names should be: " + "FieldName, QuestionType, FieldType, QuestionText, MaxCharacters, Responses, LowerRange, UpperRange, LogicCheck, DontKnow, Refuse, NA, Skip, Comments");
                            }
                        }
                        else
                        {
                            if (!rowRange.MergeCells)
                            {
                                // Create a new question
                                var curQuestion = new Question { };

                                // Get the fieldName and verify it
                                curQuestion.fieldName = GetCellValue(data, rowCount, 1);
                                if (string.IsNullOrEmpty(curQuestion.fieldName))
                                {
                                    errorsEncountered = true;
                                    worksheetErrorsEncountered = true;
                                    logstring.Add("ERROR - FieldName: Row " + rowCount + " in worksheet '" + worksheet.Name + "' has a blank FieldName.");
                                    continue;
                                }
                                CheckFieldName(worksheet.Name, curQuestion.fieldName);

                                // Get the questionType
                                curQuestion.questionType = GetCellValue(data, rowCount, 2);

                                // Get the fieldType
                                curQuestion.fieldType = GetCellValue(data, rowCount, 3);

                                // Get Question Text
                                curQuestion.questionText = GetCellValue(data, rowCount, 4);
                                if (curQuestion.questionText == "" && curQuestion.questionType != "automatic")
                                {
                                    errorsEncountered = true;
                                    worksheetErrorsEncountered = true;
                                    logstring.Add("ERROR - QuestionText: FieldName '" + curQuestion.fieldName + "' in worksheet '" + worksheet.Name + "' has blank QuestionText.");
                                }

                                // Get max Characters
                                string maxCharsValue = GetCellValue(data, rowCount, 5);
                                curQuestion.maxCharacters = string.IsNullOrEmpty(maxCharsValue) ? "-9" : maxCharsValue;
                                if (curQuestion.maxCharacters != "-9")
                                {
                                    CheckMaxCharacters(worksheet.Name, curQuestion.maxCharacters, curQuestion.fieldName);
                                }

                                // Get the responses string (use raw to preserve formatting)
                                string rawResponses = GetCellValueRaw(data, rowCount, 6);

                                if (rawResponses.Trim().StartsWith("source:", StringComparison.OrdinalIgnoreCase))
                                {
                                    ParseDynamicResponses(rawResponses, curQuestion, worksheet.Name, curQuestion.fieldName);
                                }
                                else if (rawResponses.Trim().StartsWith("calc:", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Only parse calculations for automatic question types
                                    if (curQuestion.questionType == "automatic")
                                    {
                                        // Exclude built-in automatic fields that don't need calculations
                                        // Using static HashSet for O(1) lookup (case-insensitive)
                                        if (!BuiltInAutoFields.Contains(curQuestion.fieldName))
                                        {
                                            ParseAutomaticCalculation(rawResponses, curQuestion, worksheet.Name, curQuestion.fieldName);
                                        }
                                    }
                                    else
                                    {
                                        errorsEncountered = true;
                                        worksheetErrorsEncountered = true;
                                        logstring.Add($"ERROR - Calculation: FieldName '{curQuestion.fieldName}' in worksheet '{worksheet.Name}' has calculation syntax but QuestionType is not 'automatic'.");
                                    }
                                }
                                else if (rawResponses.Trim().StartsWith("mask:", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (curQuestion.questionType == "text")
                                    {
                                        curQuestion.mask = rawResponses.Trim().Substring(5).Trim();
                                    }
                                    else
                                    {
                                        errorsEncountered = true;
                                        worksheetErrorsEncountered = true;
                                        logstring.Add($"ERROR - Mask: FieldName '{curQuestion.fieldName}' in worksheet '{worksheet.Name}' has mask syntax but QuestionType is not 'text'.");
                                    }
                                }
                                else
                                {
                                    curQuestion.responses = rawResponses;
                                }
                                // Need to check for blank reponses, but sometimes they are supposed to be blank if they are dynamically generated
                                //if (curQuestion.responses == "" && curQuestion.questionType == "radio")
                                //{
                                //    errorsEncountered = true;
                                //    worksheetErrorsEncountered = true;
                                //    logstring.Add("ERROR - Responses: FieldName '" + curQuestion.fieldName + "' in worksheet '" + worksheet.Name + "' does not have any responses.");
                                //}

                                CheckQuestionFieldType(curQuestion, worksheet.Name);

                                // Get Lower range
                                string lowerValue = GetCellValue(data, rowCount, 7);
                                curQuestion.lowerRange = string.IsNullOrEmpty(lowerValue) ? "-9" : lowerValue;
                                string upperValue = GetCellValue(data, rowCount, 8);
                                curQuestion.upperRange = string.IsNullOrEmpty(upperValue) ? "-9" : upperValue;
                                if (curQuestion.questionType == "date")
                                {
                                    CheckDateRange(worksheet.Name, curQuestion.lowerRange, curQuestion.fieldName, "LowerRange");
                                    CheckDateRange(worksheet.Name, curQuestion.upperRange, curQuestion.fieldName, "UpperRange");
                                }
                                else
                                {
                                    if (curQuestion.lowerRange != "-9")
                                    {
                                        CheckUpperLowerRange(worksheet.Name, curQuestion.lowerRange, curQuestion.fieldName, "LowerRange");
                                    }
                                    if (curQuestion.upperRange != "-9")
                                    {
                                        CheckUpperLowerRange(worksheet.Name, curQuestion.upperRange, curQuestion.fieldName, "UpperRange");
                                    }
                                }


                                // Get Logic check
                                string logicCheckRaw = GetCellValue(data, rowCount, 9);
                                if (!string.IsNullOrWhiteSpace(logicCheckRaw))
                                {
                                    string[] logicChecks = logicCheckRaw.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                                    foreach (string check in logicChecks)
                                    {
                                        string trimmedCheck = check.Trim();
                                        if (trimmedCheck.StartsWith("unique;"))
                                        {
                                            string[] parts = trimmedCheck.Split(new char[] { ';' }, 2);
                                            if (parts.Length == 2)
                                            {
                                                string message = parts[1].Trim();
                                                if (message.StartsWith("'") && message.EndsWith("'"))
                                                {
                                                    curQuestion.uniqueCheckMessage = message.Trim('\'');
                                                }
                                                else
                                                {
                                                    errorsEncountered = true;
                                                    worksheetErrorsEncountered = true;
                                                    logstring.Add("ERROR - LogicCheck: FieldName '" + curQuestion.fieldName + "' in worksheet '" + worksheet.Name + "' has invalid syntax for unique check message (must be in single quotes): " + trimmedCheck);
                                                }
                                            }
                                            else
                                            {
                                                errorsEncountered = true;
                                                worksheetErrorsEncountered = true;
                                                logstring.Add("ERROR - LogicCheck: FieldName '" + curQuestion.fieldName + "' in worksheet '" + worksheet.Name + "' has invalid syntax for unique check (missing message): " + trimmedCheck);
                                            }
                                        }
                                        else
                                        {
                                            curQuestion.logicChecks.Add(trimmedCheck);
                                            CheckLogicCheckSyntax(worksheet.Name, trimmedCheck, curQuestion.fieldName);
                                        }
                                    }
                                }


                                // Special Buttons
                                // don't know
                                string dontKnowValue = GetCellValue(data, rowCount, 10);
                                curQuestion.dontKnow = string.IsNullOrEmpty(dontKnowValue) ? "-9" : dontKnowValue;
                                if (curQuestion.dontKnow != "-9")
                                {
                                    CheckSpecialButton(worksheet.Name, curQuestion.dontKnow, curQuestion.fieldName, "DontKnow");
                                }
                                //refuse
                                string refuseValue = GetCellValue(data, rowCount, 11);
                                curQuestion.refuse = string.IsNullOrEmpty(refuseValue) ? "-9" : refuseValue;
                                if (curQuestion.refuse != "-9")
                                {
                                    CheckSpecialButton(worksheet.Name, curQuestion.refuse, curQuestion.fieldName, "Refuse");
                                }


                                string naValue = GetCellValue(data, rowCount, 12);
                                curQuestion.na = string.IsNullOrEmpty(naValue) ? "-9" : naValue;
                                if (curQuestion.na != "-9")
                                {
                                    CheckSpecialButton(worksheet.Name, curQuestion.na, curQuestion.fieldName, "NA");
                                }


                                curQuestion.skip = GetCellValue(data, rowCount, 13);
                                if (curQuestion.skip != "")
                                {
                                    CheckSkipSyntax(worksheet.Name, curQuestion.skip, curQuestion.fieldName);
                                }
                                QuestionList.Add(curQuestion);

                                // Report progress
                                onQuestionProcessed?.Invoke(worksheet.Name, curQuestion.fieldName);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        errorsEncountered = true;
                        worksheetErrorsEncountered = true;
                        logstring.Add("ERROR: An unexpected error occurred while processing row " + rowCount + " in worksheet '" + worksheet.Name + "'. The error was: " + ex.Message);
                    }
                }

                // Note: Trimming is now done in GetCellValue() during the bulk read

                if (worksheetErrorsEncountered == false)
                {
                    // Check fieldnames in logic checks
                    CheckLogicFieldNames(worksheet.Name);
                    // Check fieldnames in skips
                    CheckSkipToFieldNames(worksheet.Name);
                    // Check if missing MaxCharacters for text fields
                    CheckMaxCharacters(worksheet.Name);
                    // Check for duplicate columns in the question list before moving on
                    CheckDuplicateColumns(worksheet.Name);
                    if (worksheetErrorsEncountered == false)
                    {
                        logstring.Add("No errors found in '" + worksheet.Name + "'");
                    }
                }
            }
            // Error handling in caase we could not crread the Excel file
            catch (Exception ex)
            {
                errorsEncountered = true;
                worksheetErrorsEncountered = true;
                MessageBox.Show("ERROR: There were unexpected errors in the Excel Data Dictionary: " + "Worksheet: " + worksheet.Name + " Error: " + ex.Message, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                logstring.Add("ERROR: There were unexpected errors in the Excel Data Dictionary: " + "Worksheet: " + worksheet.Name + " Error: " + ex.Message);
            }
        }


        //////////////////////////////////////////////////////////////////////
        // Function to count data rows (questions) in a worksheet
        //////////////////////////////////////////////////////////////////////
        public static int CountDataRows(Worksheet worksheet)
        {
            try
            {
                Excel.Range range = worksheet.UsedRange;
                int numRows = range.Rows.Count;
                int count = 0;

                // Count non-merged rows (actual questions), starting from row 2 (skip header)
                for (int rowCount = 2; rowCount <= numRows; rowCount++)
                {
                    Range rowRange = worksheet.Cells[rowCount, 14]; // Check column 14 (Comments)
                    if (!rowRange.MergeCells)
                    {
                        count++;
                    }
                }
                return count;
            }
            catch
            {
                return 0;
            }
        }

        //////////////////////////////////////////////////////////////////////
        // Function to verify field name
        //////////////////////////////////////////////////////////////////////
        private void CheckFieldName(string worksheet, string fieldname)
        {

            if (char.IsDigit(fieldname[0]))
            {
                errorsEncountered = true;
                worksheetErrorsEncountered = true;
                logstring.Add("ERROR - FieldName: " + worksheet + " has a FieldName that starts with a number: " + fieldname);
            }
            else if (fieldname.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
            {
                errorsEncountered = true;
                worksheetErrorsEncountered = true;
                logstring.Add("ERROR - FieldName: " + worksheet + " has an invalid FieldName.  Only letters, digits, and underscores are allowed: " + fieldname);
            }
            else if (fieldname != fieldname.ToLower())
            {
                errorsEncountered = true;
                worksheetErrorsEncountered = true;
                logstring.Add("ERROR - FieldName: " + worksheet + " has a FieldName that is not all lowercase: " + fieldname);
            }
            else if (fieldname[0] == '_')
            {
                errorsEncountered = true;
                worksheetErrorsEncountered = true;
                logstring.Add("ERROR - FieldName: " + worksheet + " has a FieldName that starts with an underscore: " + fieldname);
            }
            else if (fieldname.Contains(" "))
            {
                errorsEncountered = true;
                worksheetErrorsEncountered = true;
                logstring.Add("ERROR - FieldName: " + worksheet + " has a FieldName that contains a space: " + fieldname);
            }
        }


        //////////////////////////////////////////////////////////////////////
        // Function to check max characters
        //////////////////////////////////////////////////////////////////////
        private void CheckMaxCharacters(string worksheet, string maxChars, string fieldname)
        {
            // Extract the numeric part (remove optional '=' prefix)
            string numericPart = maxChars.StartsWith("=") ? maxChars.Substring(1) : maxChars;

            // Check if the numeric part is valid
            if (!NumericOnlyRegex.IsMatch(numericPart))
            {
                errorsEncountered = true;
                worksheetErrorsEncountered = true;
                logstring.Add("ERROR - MaxCharacters: FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has a non-numeric value for MaxCharacters: " + maxChars);
                return;
            }

            if (int.TryParse(numericPart, out int num))
            {
                if (num < 1 || num > 2000)
                {
                    errorsEncountered = true;
                    worksheetErrorsEncountered = true;
                    logstring.Add("ERROR - MaxCharacters: FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has a MaxCharacters value that is out of range (1 to 2000): " + maxChars);
                }
            }
        }



        /////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Function to check any of the questions types, field types and corresponding datatypes are wrongly defined
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void CheckQuestionFieldType(Question question, string tblename)
        {
            string questiontype = question.questionType;
            string fieldtype = question.fieldType;
            string fieldname = question.fieldName;
            string responseStr = question.responses;

            // Using static HashSets for O(1) lookup instead of array O(n) scan
            if (!ValidQuestionTypes.Contains(questiontype))
            {
                errorsEncountered = true;
                worksheetErrorsEncountered = true;
                logstring.Add("ERROR - QuestionType: The QuestionType " + questiontype + " for FieldName '" + fieldname + "' in table '" + tblename + "' is not among the predefined list.");
            }

            if (!ValidFieldTypes.Contains(fieldtype))
            {
                errorsEncountered = true;
                worksheetErrorsEncountered = true;
                logstring.Add("ERROR - FieldType: The FieldType '" + fieldtype + "' for FieldName '" + fieldname + "' in table '" + tblename + "' is not among the predefined list.");
            }

            // check the corresponding data types for all radio question type to ensure they are integer type
            if (questiontype == "radio")
            {
                if (fieldtype != "integer")
                {
                    errorsEncountered = true;
                    worksheetErrorsEncountered = true;
                    logstring.Add("ERROR - FieldType: The FieldType for FieldName '" + fieldname + "' in table '" + tblename + "' must be integer when the QuestionType is 'radio'.");
                }
            }

            // check the corresponding data types for all checkbox question type to ensure they are text type
            if (questiontype == "checkbox")
            {
                if (fieldtype != "text")
                {
                    errorsEncountered = true;
                    worksheetErrorsEncountered = true;
                    logstring.Add("ERROR - FieldType: The FieldType for FieldName '" + fieldname + "' in table '" + tblename + "' must be text when the QuestionType is 'checkbox'.");
                }
            }

            // check the corresponding data types for all date question type to ensure they are date type
            if (questiontype == "date")
            {
                List<string> datetypeslist = new List<string>();
                datetypeslist.Add("date");
                datetypeslist.Add("datetime");
                var match = datetypeslist
                    .FirstOrDefault(stringToCheck => stringToCheck.Contains(fieldtype));
                if (match == null)
                {
                    errorsEncountered = true;
                    worksheetErrorsEncountered = true;
                    logstring.Add("ERROR - FieldType: The FieldType for FieldName '" + fieldname + "' in table '" + tblename + "' must be date when the QuestionType is 'date' or 'datetime'.");
                }
            }

            // check the duplicate responses for radio buttons and checkboxes, only for static responses
            if ((questiontype == "radio" || questiontype == "checkbox") && question.ResponseSourceType == ResponseSourceType.Static && !string.IsNullOrEmpty(responseStr))
            {
                //split the list of responses/answers to generate the list/array
                string[] responses = responseStr.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                if (responses.Length != 0)
                {
                    List<string> list = new List<string>();
                    foreach (string response in responses)
                    {
                        // using the substring function to get the list of keys for responses
                        int index = response.IndexOf(@":");

                        // Check if there is no colon
                        if (index == -1)
                        {
                            errorsEncountered = true;
                            worksheetErrorsEncountered = true;
                            logstring.Add($"ERROR - Responses: Invalid static radio button options for '{fieldname}' in table '{tblename}'. Expected format 'number:Statement', found '{response}'.");
                            return;
                        }

                        // Check for more than 1 colon (:) in a line
                        string[] responseString = response.Split(':'); // split the string using :
                        if (responseString.Length != 2)
                        {
                            errorsEncountered = true;
                            worksheetErrorsEncountered = true;
                            logstring.Add($"ERROR - Responses: Invalid static radio button options for '{fieldname}' in table '{tblename}'. Expected format 'number:Statement', found '{response}'.");
                            return;
                        }
                        else
                        {
                            list.Add(response.Substring(0, index));
                            var duplicateKeys = list.GroupBy(x => x)
                                                .Where(group => group.Count() > 1)
                                                .Select(group => group.Key);
                            if (list.Count != list.Distinct().Count())
                            {
                                errorsEncountered = true;
                                worksheetErrorsEncountered = true;
                                logstring.Add("ERROR - Responses: The Responses for FieldName '" + fieldname + "' in table '" + tblename + "' has duplicates " + String.Join(",", duplicateKeys));
                                return;
                            }
                        }

                        // Check if there is a space at the beginning
                        if (response.Substring(0, 1) == " ")
                        {
                            errorsEncountered = true;
                            worksheetErrorsEncountered = true;
                            logstring.Add("ERROR - Responses: Invalid static radio button options for '" + fieldname + "' in table '" + tblename + "'. Please remove leading spaces.");
                            return;
                        }
                        // Check if there is a space after the colon
                        if (response.Contains(": "))
                        {
                            errorsEncountered = true;
                            worksheetErrorsEncountered = true;
                            logstring.Add("ERROR - Responses: Invalid static radio button options for '" + fieldname + "' in table '" + tblename + "'. Please remove space after the colon (:) for static responses.");
                            return;
                        }
                    }
                }
            }
        }




        //////////////////////////////////////////////////////////////////////
        // Function to check upper and lower range range
        //////////////////////////////////////////////////////////////////////
        private void CheckUpperLowerRange(string worksheet, string range, string fieldname, string rangeName)
        {
            // Check if range is numeric
            //if (!NumericOnlyRegex.IsMatch(range))
            if (!DecimalRegex.IsMatch(range))
            {
                errorsEncountered = true;
                worksheetErrorsEncountered = true;
                logstring.Add("ERROR - " + rangeName + ": FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has a non-numeric value for " + rangeName + ": " + range);
                return;
            }
        }

        private void CheckDateRange(string worksheet, string range, string fieldname, string rangeName)
        {
            if (range == "-9")
            {
                errorsEncountered = true;
                worksheetErrorsEncountered = true;
                logstring.Add("ERROR - " + rangeName + ": FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has a missing value for " + rangeName);
                return;
            }

            if (range == "0" || range == "+0d" || range == "-0d") return;

            if (!DateRangeRegex.IsMatch(range))
            {
                if (HardCodedDateRegex.IsMatch(range))
                {
                    // Check if the date is a valid date
                    if (!DateTime.TryParseExact(range, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                    {
                        errorsEncountered = true;
                        worksheetErrorsEncountered = true;
                        logstring.Add("ERROR - " + rangeName + ": FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has an invalid date value: " + range);
                    }
                }
                else
                {
                    errorsEncountered = true;
                    worksheetErrorsEncountered = true;
                    logstring.Add("ERROR - " + rangeName + ": FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has an invalid format for " + rangeName + ": " + range);
                }
            }
        }



        //////////////////////////////////////////////////////////////////////
        // Function to check logic check syntax
        //////////////////////////////////////////////////////////////////////
        private void CheckLogicCheckSyntax(string worksheet, string logicCheck, string fieldname)
        {
            try
            {
                // The new format is: expression; 'error message'
                // Example: tabletnum2 != tabletnum; 'This does not match your previous entry!'
                // Example: (movedate_month = '2' and movedate_day = '29') or (movedate_month = '2' and movedate_day = '30'); 'Invalid day'

                // Make sure the logic check contains a semicolon
                if (!logicCheck.Contains(";"))
                {
                    errorsEncountered = true;
                    worksheetErrorsEncountered = true;
                    logstring.Add("ERROR - LogicCheck: FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has invalid syntax for LogicCheck (missing semicolon): " + logicCheck);
                    return;
                }

                // Split by semicolon to get logic expression and message
                string[] parts = logicCheck.Split(new char[] { ';' }, 2);
                if (parts.Length != 2)
                {
                    errorsEncountered = true;
                    worksheetErrorsEncountered = true;
                    logstring.Add("ERROR - LogicCheck: FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has invalid syntax for LogicCheck: " + logicCheck);
                    return;
                }

                string expression = parts[0].Trim();
                string message = parts[1].Trim();

                // Check that the message is in single quotes
                if (!message.StartsWith("'") || !message.EndsWith("'"))
                {
                    errorsEncountered = true;
                    worksheetErrorsEncountered = true;
                    logstring.Add("ERROR - LogicCheck: FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has invalid syntax for LogicCheck (message must be in single quotes): " + logicCheck);
                    return;
                }

                // Basic validation that the expression contains some comparison operator
                string[] operators = { "=", "!=", "<>", ">", ">=", "<", "<=", "and", "or" };
                bool hasOperator = false;
                foreach (string op in operators)
                {
                    if (expression.Contains(op))
                    {
                        hasOperator = true;
                        break;
                    }
                }

                if (!hasOperator)
                {
                    errorsEncountered = true;
                    worksheetErrorsEncountered = true;
                    logstring.Add("ERROR - LogicCheck: FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has invalid syntax for LogicCheck (no operator found): " + logicCheck);
                    return;
                }
            }
            // Error handling in case we could not create the Excel file
            catch (Exception ex)
            {
                errorsEncountered = true;
                worksheetErrorsEncountered = true;
                MessageBox.Show("ERROR - LogicCheck: FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has invalid syntax for LogicCheck." + ex.Message, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                logstring.Add("ERROR - LogicCheck: FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has invalid syntax for LogicCheck." + ex.Message);
            }
        }


        //////////////////////////////////////////////////////////////////////
        // Function to check 'special' buttons
        //////////////////////////////////////////////////////////////////////
        private void CheckSpecialButton(string worksheet, string val, string fieldname, string buttonName)
        {
            // Check if value is true or false
            if (val != "True" && val != "False")
            {
                errorsEncountered = true;
                worksheetErrorsEncountered = true;
                logstring.Add("ERROR: - " + buttonName + " FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has an invalid value for '" + buttonName + "': " + val);
                return;
            }
        }




        //////////////////////////////////////////////////////////////////////
        // Function to check skip syntax
        //////////////////////////////////////////////////////////////////////
        private void CheckSkipSyntax(string worksheet, string skipText, string fieldname)
        {
            try
            {
                // This stores the text for the skips
                string[] skips = skipText.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                int lenSkip;
                string skipType;

                // Populate the list for each type of logic checks
                foreach (string skip in skips)
                {
                    // Make sure skip contains a colon (:)
                    if (!skip.Contains(":"))
                    {
                        errorsEncountered = true;
                        worksheetErrorsEncountered = true;
                        logstring.Add("ERROR - Skip: FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has invalid syntax for Skip: " + skip);
                        return;
                    }

                    // Make sure the skip starts with 'preskip' or 'postskip'
                    // Split the logic string into two parts: one before the : and one after
                    skipType = skip.Substring(0, skip.IndexOf(@":")) == "preskip" ? "preskip" : "postskip";
                    if (skipType != "preskip" && skipType != "postskip")
                    {
                        errorsEncountered = true;
                        worksheetErrorsEncountered = true;
                        logstring.Add("ERROR - Skip: FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has invalid syntax for Skip: " + skip);
                        return;
                    }

                    // Make sure the skip has one comma and one comma only
                    string[] parts = skip.Split(','); // split the string using the comma delimiter
                    if (parts.Length != 2)
                    {
                        errorsEncountered = true;
                        worksheetErrorsEncountered = true;
                        logstring.Add("ERROR - Skip: FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has invalid syntax for Skip: " + skip);
                        return;
                    }

                    lenSkip = skipType == "postskip" ? 13 : 12;

                    string logic_section = parts[0];
                    string skip_to_section = parts[1];

                    // Make sure the logic section only has 1 : (colon)
                    string[] logicString = logic_section.Split(':'); // split the string using :
                    if (logicString.Length != 2)
                    {
                        errorsEncountered = true;
                        worksheetErrorsEncountered = true;
                        logstring.Add("ERROR - Skip: FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has invalid syntax for Skip: " + skip);
                        return;
                    }

                    // and make sure the logic section has 4 spaces (if it is not 'does not contain')
                    logicString = logic_section.Split(' '); // split the string using space

                    if (logicString.Length != 5 && !logic_section.Contains("does not contain"))
                    {
                        errorsEncountered = true;
                        worksheetErrorsEncountered = true;
                        logstring.Add("ERROR - Skip: FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has invalid syntax for Skip: " + skip);
                        return;
                    }

                    // Check number of 'words' for 'does not contain'
                    if (logicString.Length != 7 && logic_section.Contains("does not contain"))
                    {
                        errorsEncountered = true;
                        worksheetErrorsEncountered = true;
                        logstring.Add("ERROR - Skip: FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has invalid syntax for Skip: " + skip);
                        return;
                    }

                    // Create a list to store the index of each 'space' in the skip text
                    var spaceIndices = new List<int>();

                    // Populate the spaceIndices list
                    for (int i = 0; i < skip.Length; i++)
                        if (skip[i] == ' ') spaceIndices.Add(i);

                    // Check if the field to check is a single word
                    string fieldname_to_check = skip.Substring(lenSkip, spaceIndices[2] - spaceIndices[1] - 1);
                    if (fieldname_to_check.Contains(" "))
                    {
                        errorsEncountered = true;
                        worksheetErrorsEncountered = true;
                        logstring.Add("ERROR - Skip: FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has invalid syntax for Skip: " + skip);
                        return;
                    }


                    string condition;

                    // Check for the condition 'does not contain' or 'contains'
                    if (!logic_section.Contains("does not contain"))
                    {
                        // Make sure the condition is correct
                        condition = skip.Substring(spaceIndices[2] + 1, spaceIndices[3] - spaceIndices[2] - 1);
                        string[] conditions = { "=", ">", ">=", "<", "<=", "<>", "'contains'" }; // example string array

                        if (!conditions.Contains(condition))
                        {
                            errorsEncountered = true;
                            worksheetErrorsEncountered = true;
                            logstring.Add("ERROR - Skip: FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has invalid syntax for LogicCheck: " + skip);
                            return;
                        }
                    }
                }
            }
            // Error handling in caase we could not crread the Excel file
            catch (Exception ex)
            {
                errorsEncountered = true;
                worksheetErrorsEncountered = true;
                MessageBox.Show("ERROR - Skip: FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has invalid syntax for Skip." + ex.Message, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                logstring.Add("ERROR - Skip: FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has invalid syntax for Skip." + ex.Message);
            }
        }




        //////////////////////////////////////////////////////////////////////
        // Function to check if the logic checks have legitimate fieldnames
        //////////////////////////////////////////////////////////////////////
        private void CheckLogicFieldNames(string worksheet)
        {
            string curFieldname = "";
            try
            {
                // Create a Dictionary for O(1) index lookup instead of O(n) List.IndexOf
                Dictionary<string, int> fieldnameIndex = new Dictionary<string, int>();
                for (int i = 0; i < QuestionList.Count; i++)
                {
                    fieldnameIndex[QuestionList[i].fieldName] = i;
                }

                foreach (Question question in QuestionList)
                {
                    foreach (string logicCheck in question.logicChecks)
                    {
                        curFieldname = question.fieldName;

                        // New format: extract expression from "expression; 'message'"
                        string[] parts = logicCheck.Split(new char[] { ';' }, 2);
                        string expression = parts[0].Trim();

                        // Extract potential field names from the expression
                        // Remove quoted strings first to avoid matching field names in quotes
                        string cleanExpression = QuotedStringRegex.Replace(expression, "");

                        // Match word characters (field names) - excluding operators and numbers
                        // Field names are alphanumeric + underscore, starting with a letter
                        MatchCollection matches = FieldNameRegex.Matches(cleanExpression);

                        HashSet<string> referencedFieldNames = new HashSet<string>();
                        foreach (Match match in matches)
                        {
                            string potentialFieldName = match.Value;
                            // Skip SQL/logic keywords (using HashSet for O(1) lookup)
                            if (!LogicKeywords.Contains(potentialFieldName))
                            {
                                referencedFieldNames.Add(potentialFieldName);
                            }
                        }

                        // Check each referenced field name
                        foreach (string referencedFieldName in referencedFieldNames)
                        {
                            // Check if it exists using Dictionary O(1) lookup
                            if (fieldnameIndex.TryGetValue(referencedFieldName, out int refIndex))
                            {
                                int curIndex = fieldnameIndex[curFieldname];

                                // Check if the referenced field is after the current question
                                if (refIndex > curIndex)
                                {
                                    errorsEncountered = true;
                                    worksheetErrorsEncountered = true;
                                    logstring.Add("ERROR - LogicCheck: In worksheet '" + worksheet + "', the LogicCheck for FieldName '" + curFieldname + "' uses a FieldName AFTER the current question: " + referencedFieldName);
                                }
                            }
                            else
                            {
                                errorsEncountered = true;
                                worksheetErrorsEncountered = true;
                                logstring.Add("ERROR - LogicCheck: In worksheet '" + worksheet + "', the LogicCheck for FieldName '" + curFieldname + "' uses a nonexistent FieldName: " + referencedFieldName);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errorsEncountered = true;
                worksheetErrorsEncountered = true;
                MessageBox.Show("ERROR - LogicCheck: FieldName '" + curFieldname + "' in worksheet '" + worksheet + "' has invalid syntax for LogicCheck." + ex.Message, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                logstring.Add("ERROR - LogicCheck: FieldName '" + curFieldname + "' in worksheet '" + worksheet + "' has invalid syntax for LogicCheck." + ex.Message);
            }
        }




        //////////////////////////////////////////////////////////////////////
        // Function to check if the skip has legitimate fieldnames
        //////////////////////////////////////////////////////////////////////
        private void CheckSkipToFieldNames(string worksheet)
        {
            string curFieldname = "";
            try
            {
                string fieldname_to_skip_to = "";
                string fieldname_to_check = "";

                // Create a Dictionary for O(1) index lookup instead of O(n) List.IndexOf
                Dictionary<string, int> fieldnameIndex = new Dictionary<string, int>();
                for (int i = 0; i < QuestionList.Count; i++)
                {
                    fieldnameIndex[QuestionList[i].fieldName] = i;
                }

                foreach (Question question in QuestionList)
                {
                    if (question.skip != "")
                    {
                        curFieldname = question.fieldName;

                        string[] skips = question.skip.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (string skip in skips)
                        {
                            string[] words = skip.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            fieldname_to_check = words[2].Trim().Trim(',');
                            fieldname_to_skip_to = words[words.Length - 1].Trim();

                            int curIndex = fieldnameIndex[curFieldname];

                            // Check if the field name to check value of exists and is before the current question
                            if (fieldnameIndex.TryGetValue(fieldname_to_check, out int checkIndex))
                            {
                                if (checkIndex > curIndex)
                                {
                                    errorsEncountered = true;
                                    worksheetErrorsEncountered = true;
                                    logstring.Add("ERROR - Skip: In worksheet '" + worksheet + "', the skip for FieldName '" + curFieldname + "' checks skip for a FieldName AFTER the current question: " + fieldname_to_check);
                                }
                            }
                            else
                            {
                                errorsEncountered = true;
                                worksheetErrorsEncountered = true;
                                logstring.Add("ERROR - Skip: In worksheet '" + worksheet + "', the skip for FieldName '" + curFieldname + "' checks skip of a nonexistent FieldName: " + fieldname_to_check);
                            }

                            // Check if the field name to skip to is legitimate - exists and is after the current question
                            if (fieldnameIndex.TryGetValue(fieldname_to_skip_to, out int skipToIndex))
                            {
                                if (skipToIndex < curIndex)
                                {
                                    errorsEncountered = true;
                                    worksheetErrorsEncountered = true;
                                    logstring.Add("ERROR - Skip: In worksheet '" + worksheet + "', the skip for FieldName '" + curFieldname + "' skips to a FieldName BEFORE the current question: " + fieldname_to_skip_to);
                                }
                                else if (skipToIndex == curIndex)
                                {
                                    errorsEncountered = true;
                                    worksheetErrorsEncountered = true;
                                    logstring.Add("ERROR - Skip: In worksheet '" + worksheet + "', the skip for FieldName '" + curFieldname + "' skips to the current question: " + fieldname_to_skip_to);
                                }
                            }
                            else
                            {
                                errorsEncountered = true;
                                worksheetErrorsEncountered = true;
                                logstring.Add("ERROR - Skip: In worksheet '" + worksheet + "', the skip for FieldName '" + curFieldname + "' skips to a nonexistent FieldName: " + fieldname_to_skip_to);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errorsEncountered = true;
                worksheetErrorsEncountered = true;
                MessageBox.Show("ERROR - Skip: FieldName '" + curFieldname + "' in worksheet '" + worksheet + "' has invalid syntax for Skip." + ex.Message, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                logstring.Add("ERROR - Skip: FieldName '" + curFieldname + "' in worksheet '" + worksheet + "' has invalid syntax for Skip." + ex.Message);
            }
        }




        //////////////////////////////////////////////////////////////////////
        // Function to check if MaxCharacters is blank for text_integer, phone_num and text fields
        //////////////////////////////////////////////////////////////////////
        private void CheckMaxCharacters(string worksheet)
        {
            string curFieldname = "";
            try
            {
                foreach (Question question in QuestionList)
                {
                    curFieldname = question.fieldName;
                    if ((question.fieldType == "text" || question.fieldType == "text_integer" || question.fieldType == "phone_num") && question.questionType != "automatic" && question.questionType != "checkbox" && question.questionType != "combobox")
                    {
                        if (question.maxCharacters == "-9")
                        {
                            errorsEncountered = true;
                            worksheetErrorsEncountered = true;
                            logstring.Add("ERROR - MaxCharacters: In worksheet '" + worksheet + "', MaxCharacters for FieldName '" + curFieldname + "' needs a value");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errorsEncountered = true;
                worksheetErrorsEncountered = true;
                MessageBox.Show("ERROR - Skip: FieldName '" + curFieldname + "' in worksheet '" + worksheet + "' has invalid syntax for Skip." + ex.Message, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                logstring.Add("ERROR - Skip: FieldName '" + curFieldname + "' in worksheet '" + worksheet + "' has invalid syntax for Skip." + ex.Message);
            }
        }


        //////////////////////////////////////////////////////////////////////
        // Function to check any duplicate column names
        //////////////////////////////////////////////////////////////////////
        private void CheckDuplicateColumns(string tblename)
        {
            List<string> list = new List<string>();
            foreach (Question question in QuestionList)
            {
                if (question.questionType != "information")
                {
                    list.Add(question.fieldName);
                }
            }

            var duplicateKeys = list.GroupBy(x => x)
                        .Where(group => group.Count() > 1)
                        .Select(group => group.Key);

            if (list.Count != list.Distinct().Count())
            {
                errorsEncountered = true;
                worksheetErrorsEncountered = true;
                logstring.Add("ERROR - Duplicate fieldnames found in worksheet: " + tblename + ". Duplicated fieldnames: " + String.Join(",", duplicateKeys) + ". Check for empty rows at the end of the spreadsheet and delete them.");
            }
        }


        private string ParseOperator(string op)
        {
            switch (op.Trim())
            {
                case "=": return "=";
                case "!=": return "!=";
                case "<>": return "<>";
                case ">": return "&gt;";
                case "<": return "&lt;";
                case ">=": return "&gt;=";
                case "<=": return "&lt;=";
                default: return "="; // Default to equals
            }
        }

        private void ParseDynamicResponses(string responsesStr, Question question, string worksheetName, string fieldName)
        {
            var lines = responsesStr.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine)) continue;

                var parts = trimmedLine.Split(new[] { ':' }, 2);
                if (parts.Length != 2)
                {
                    logstring.Add($"ERROR - Responses: Invalid dynamic response line format for FieldName '{fieldName}' in worksheet '{worksheetName}': '{trimmedLine}'");
                    errorsEncountered = true;
                    worksheetErrorsEncountered = true;
                    continue;
                }

                var key = parts[0].Trim().ToLower();
                var value = parts[1].Trim();

                switch (key)
                {
                    case "source":
                        if (Enum.TryParse(value, true, out ResponseSourceType sourceType))
                        {
                            question.ResponseSourceType = sourceType;
                        }
                        else
                        {
                            logstring.Add($"ERROR - Responses: Invalid source type '{value}' for FieldName '{fieldName}' in worksheet '{worksheetName}'. Must be 'csv' or 'database'.");
                            errorsEncountered = true;
                            worksheetErrorsEncountered = true;
                        }
                        break;
                    case "file":
                        question.ResponseSourceFile = value;
                        break;
                    case "table":
                        question.ResponseSourceTable = value;
                        break;
                    case "filter":
                        // Expected format: column operator value or column = value
                        var filterMatch = FilterMatchRegex.Match(value);
                        if (filterMatch.Success)
                        {
                            question.ResponseFilters.Add(new Filter
                            {
                                Column = filterMatch.Groups[1].Value.Trim(),
                                Operator = ParseOperator(filterMatch.Groups[2].Success ? filterMatch.Groups[2].Value : "="),
                                Value = filterMatch.Groups[3].Value.Trim()
                            });
                        }
                        else
                        {
                            logstring.Add($"ERROR - Responses: Invalid filter format for FieldName '{fieldName}' in worksheet '{worksheetName}': '{value}'. Expected 'column [operator] value'.");
                            errorsEncountered = true;
                            worksheetErrorsEncountered = true;
                        }
                        break;
                    case "display":
                        question.ResponseDisplayColumn = value;
                        break;
                    case "value":
                        question.ResponseValueColumn = value;
                        break;
                    case "distinct":
                        if (bool.TryParse(value, out bool distinct))
                        {
                            question.ResponseDistinct = distinct;
                        }
                        else
                        {
                            logstring.Add($"ERROR - Responses: Invalid boolean value for 'distinct' for FieldName '{fieldName}' in worksheet '{worksheetName}'. Must be 'true' or 'false'.");
                            errorsEncountered = true;
                            worksheetErrorsEncountered = true;
                        }
                        break;
                    case "empty_message":
                        question.ResponseEmptyMessage = value;
                        break;
                    case "dont_know":
                        var dkParts = value.Split(new[] { ',' }, 2);
                        question.ResponseDontKnowValue = dkParts[0].Trim();
                        if (dkParts.Length > 1)
                        {
                            question.ResponseDontKnowLabel = dkParts[1].Trim();
                        }
                        break;
                    case "not_in_list":
                        var nilParts = value.Split(new[] { ',' }, 2);
                        question.ResponseNotInListValue = nilParts[0].Trim();
                        if (nilParts.Length > 1)
                        {
                            question.ResponseNotInListLabel = nilParts[1].Trim();
                        }
                        break;
                    default:
                        logstring.Add($"WARNING - Responses: Unknown dynamic response key '{key}' for FieldName '{fieldName}' in worksheet '{worksheetName}'.");
                        break;
                }
            }
        }

        //////////////////////////////////////////////////////////////////////
        // Function to parse automatic calculation configuration
        //////////////////////////////////////////////////////////////////////
        private void ParseAutomaticCalculation(string responsesStr, Question question, string worksheetName, string fieldName)
        {
            var lines = responsesStr.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            string currentCalcType = "";
            List<string> currentWhenLines = new List<string>();
            List<string> currentPartLines = new List<string>();

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine)) continue;

                var parts = trimmedLine.Split(new[] { ':' }, 2);
                if (parts.Length != 2)
                {
                    logstring.Add($"ERROR - Calculation: Invalid line format for FieldName '{fieldName}' in worksheet '{worksheetName}': '{trimmedLine}'");
                    errorsEncountered = true;
                    worksheetErrorsEncountered = true;
                    continue;
                }

                var key = parts[0].Trim().ToLower();
                var value = parts[1].Trim();

                switch (key)
                {
                    case "calc":
                        currentCalcType = value.ToLower();
                        if (currentCalcType == "age_from_date")
                        {
                            question.CalculationType = CalculationType.AgeFromDate;
                        }
                        else if (currentCalcType == "age_at_date")
                        {
                            question.CalculationType = CalculationType.AgeAtDate;
                        }
                        else if (currentCalcType == "date_offset")
                        {
                            question.CalculationType = CalculationType.DateOffset;
                        }
                        else if (currentCalcType == "date_diff")
                        {
                            question.CalculationType = CalculationType.DateDiff;
                        }
                        else if (Enum.TryParse(value, true, out CalculationType calcType))
                        {
                            question.CalculationType = calcType;
                        }
                        else
                        {
                            logstring.Add($"ERROR - Calculation: Invalid calculation type '{value}' for FieldName '{fieldName}' in worksheet '{worksheetName}'. Must be 'query', 'case', 'constant', 'lookup', 'math', 'concat', 'age_from_date', 'age_at_date', 'date_offset', or 'date_diff'.");
                            errorsEncountered = true;
                            worksheetErrorsEncountered = true;
                        }
                        break;

                    case "sql":
                        question.CalculationQuerySql = value;
                        break;

                    case "param":
                        ParseParameter(value, question, worksheetName, fieldName);
                        break;

                    case "when":
                        currentWhenLines.Add(value);
                        break;

                    case "else":
                        if (currentCalcType == "case")
                        {
                            question.CalculationCaseElse = ParseResultValue(value, worksheetName, fieldName);
                        }
                        break;

                    case "value":
                        if (currentCalcType == "constant" || currentCalcType == "age_from_date" || currentCalcType == "age_at_date" || currentCalcType == "date_offset" || currentCalcType == "date_diff")
                        {
                            question.CalculationConstantValue = value;
                        }
                        break;

                    case "field":
                        if (currentCalcType == "lookup" || currentCalcType == "age_from_date" || currentCalcType == "age_at_date" || currentCalcType == "date_offset" || currentCalcType == "date_diff")
                        {
                            question.CalculationLookupField = value;
                        }
                        break;

                    case "unit":
                        if (currentCalcType == "date_diff")
                        {
                            question.CalculationUnit = value;
                        }
                        break;

                    case "operator":
                        if (currentCalcType == "math")
                        {
                            if (new[] { "+", "-", "*", "/" }.Contains(value))
                            {
                                question.CalculationMathOperator = value;
                            }
                            else
                            {
                                logstring.Add($"ERROR - Calculation: Invalid math operator '{value}' for FieldName '{fieldName}' in worksheet '{worksheetName}'. Must be +, -, *, or /.");
                                errorsEncountered = true;
                                worksheetErrorsEncountered = true;
                            }
                        }
                        break;

                    case "separator":
                        if (currentCalcType == "concat" || currentCalcType == "age_at_date")
                        {
                            question.CalculationConcatSeparator = value;
                        }
                        break;

                    case "part":
                        currentPartLines.Add(value);
                        break;

                    default:
                        logstring.Add($"WARNING - Calculation: Unknown calculation key '{key}' for FieldName '{fieldName}' in worksheet '{worksheetName}'.");
                        break;
                }
            }

            // Process when conditions for case calculations
            if (currentCalcType == "case" && currentWhenLines.Count > 0)
            {
                foreach (var whenLine in currentWhenLines)
                {
                    ParseWhenCondition(whenLine, question, worksheetName, fieldName);
                }
            }

            // Process parts for math/concat calculations
            if ((currentCalcType == "math" || currentCalcType == "concat") && currentPartLines.Count > 0)
            {
                foreach (var partLine in currentPartLines)
                {
                    var part = ParsePartLine(partLine, worksheetName, fieldName);
                    if (part != null)
                    {
                        if (currentCalcType == "math")
                        {
                            question.CalculationMathParts.Add(part);
                        }
                        else if (currentCalcType == "concat")
                        {
                            question.CalculationConcatParts.Add(part);
                        }
                    }
                }
            }

            // Validate required fields per calculation type
            ValidateCalculationFields(question, worksheetName, fieldName);
        }

        private void ParseParameter(string paramStr, Question question, string worksheetName, string fieldName)
        {
            // Expected format: @paramName = fieldName
            var match = ParameterRegex.Match(paramStr);
            if (match.Success)
            {
                var param = new CalculationParameter
                {
                    Name = match.Groups[1].Value.Trim(),
                    FieldName = match.Groups[2].Value.Trim()
                };

                // Ensure parameter name starts with @
                if (!param.Name.StartsWith("@"))
                {
                    param.Name = "@" + param.Name;
                }

                question.CalculationQueryParameters.Add(param);
            }
            else
            {
                logstring.Add($"ERROR - Calculation: Invalid parameter format '{paramStr}' for FieldName '{fieldName}' in worksheet '{worksheetName}'. Expected format: '@paramName = fieldName'.");
                errorsEncountered = true;
                worksheetErrorsEncountered = true;
            }
        }

        private void ParseWhenCondition(string whenStr, Question question, string worksheetName, string fieldName)
        {
            // Expected format: field operator value => result
            var match = WhenConditionRegex.Match(whenStr);
            if (match.Success)
            {
                var condition = new CaseCondition
                {
                    Field = match.Groups[1].Value.Trim(),
                    Operator = match.Groups[2].Value.Trim(),
                    Value = match.Groups[3].Value.Trim(),
                    Result = ParseResultValue(match.Groups[4].Value.Trim(), worksheetName, fieldName)
                };

                question.CalculationCaseConditions.Add(condition);
            }
            else
            {
                logstring.Add($"ERROR - Calculation: Invalid when condition format '{whenStr}' for FieldName '{fieldName}' in worksheet '{worksheetName}'. Expected format: 'field operator value => result'.");
                errorsEncountered = true;
                worksheetErrorsEncountered = true;
            }
        }

        private CalculationPart ParseResultValue(string resultStr, string worksheetName, string fieldName)
        {
            // Result is typically a simple constant value
            return new CalculationPart
            {
                Type = CalculationType.Constant,
                ConstantValue = resultStr
            };
        }

        private CalculationPart ParsePartLine(string partLine, string worksheetName, string fieldName)
        {
            // Expected formats:
            // "constant VALUE"
            // "lookup FIELD"
            // "query SQL"

            var words = partLine.Split(new[] { ' ' }, 2);
            if (words.Length < 2)
            {
                logstring.Add($"ERROR - Calculation: Invalid part format '{partLine}' for FieldName '{fieldName}' in worksheet '{worksheetName}'. Expected 'type value'.");
                errorsEncountered = true;
                worksheetErrorsEncountered = true;
                return null;
            }

            var partType = words[0].Trim().ToLower();
            var partValue = words[1].Trim();

            var part = new CalculationPart();

            switch (partType)
            {
                case "constant":
                    part.Type = CalculationType.Constant;
                    part.ConstantValue = partValue;
                    break;

                case "lookup":
                    part.Type = CalculationType.Lookup;
                    part.LookupField = partValue;
                    break;

                case "query":
                    part.Type = CalculationType.Query;
                    part.QuerySql = partValue;
                    // Note: Parameters in parts are not currently supported in Excel syntax
                    break;

                default:
                    logstring.Add($"ERROR - Calculation: Invalid part type '{partType}' for FieldName '{fieldName}' in worksheet '{worksheetName}'. Must be 'constant', 'lookup', or 'query'.");
                    errorsEncountered = true;
                    worksheetErrorsEncountered = true;
                    return null;
            }

            return part;
        }

        private void ValidateCalculationFields(Question question, string worksheetName, string fieldName)
        {
            switch (question.CalculationType)
            {
                case CalculationType.Query:
                    if (string.IsNullOrEmpty(question.CalculationQuerySql))
                    {
                        logstring.Add($"ERROR - Calculation: Query calculation for FieldName '{fieldName}' in worksheet '{worksheetName}' is missing required 'sql' field.");
                        errorsEncountered = true;
                        worksheetErrorsEncountered = true;
                    }
                    break;

                case CalculationType.Case:
                    if (question.CalculationCaseConditions.Count == 0)
                    {
                        logstring.Add($"ERROR - Calculation: Case calculation for FieldName '{fieldName}' in worksheet '{worksheetName}' is missing 'when' conditions.");
                        errorsEncountered = true;
                        worksheetErrorsEncountered = true;
                    }
                    break;

                case CalculationType.Constant:
                    if (string.IsNullOrEmpty(question.CalculationConstantValue))
                    {
                        logstring.Add($"ERROR - Calculation: Constant calculation for FieldName '{fieldName}' in worksheet '{worksheetName}' is missing required 'value' field.");
                        errorsEncountered = true;
                        worksheetErrorsEncountered = true;
                    }
                    break;

                case CalculationType.Lookup:
                    if (string.IsNullOrEmpty(question.CalculationLookupField))
                    {
                        logstring.Add($"ERROR - Calculation: Lookup calculation for FieldName '{fieldName}' in worksheet '{worksheetName}' is missing required 'field' field.");
                        errorsEncountered = true;
                        worksheetErrorsEncountered = true;
                    }
                    break;

                case CalculationType.Math:
                    if (string.IsNullOrEmpty(question.CalculationMathOperator))
                    {
                        logstring.Add($"ERROR - Calculation: Math calculation for FieldName '{fieldName}' in worksheet '{worksheetName}' is missing required 'operator' field.");
                        errorsEncountered = true;
                        worksheetErrorsEncountered = true;
                    }
                    if (question.CalculationMathParts.Count < 2)
                    {
                        logstring.Add($"ERROR - Calculation: Math calculation for FieldName '{fieldName}' in worksheet '{worksheetName}' must have at least 2 parts.");
                        errorsEncountered = true;
                        worksheetErrorsEncountered = true;
                    }
                    break;

                case CalculationType.Concat:
                    if (question.CalculationConcatParts.Count == 0)
                    {
                        logstring.Add($"ERROR - Calculation: Concat calculation for FieldName '{fieldName}' in worksheet '{worksheetName}' must have at least 1 part.");
                        errorsEncountered = true;
                        worksheetErrorsEncountered = true;
                    }
                    break;

                case CalculationType.AgeFromDate:
                    if (string.IsNullOrEmpty(question.CalculationLookupField))
                    {
                        logstring.Add($"ERROR - Calculation: AgeFromDate calculation for FieldName '{fieldName}' in worksheet '{worksheetName}' is missing required 'field' field.");
                        errorsEncountered = true;
                        worksheetErrorsEncountered = true;
                    }
                    if (string.IsNullOrEmpty(question.CalculationConstantValue))
                    {
                        logstring.Add($"ERROR - Calculation: AgeFromDate calculation for FieldName '{fieldName}' in worksheet '{worksheetName}' is missing required 'value' field.");
                        errorsEncountered = true;
                        worksheetErrorsEncountered = true;
                    }
                    break;

                case CalculationType.AgeAtDate:
                    if (string.IsNullOrEmpty(question.CalculationLookupField))
                    {
                        logstring.Add($"ERROR - Calculation: AgeAtDate calculation for FieldName '{fieldName}' in worksheet '{worksheetName}' is missing required 'field' field.");
                        errorsEncountered = true;
                        worksheetErrorsEncountered = true;
                    }
                    if (string.IsNullOrEmpty(question.CalculationConstantValue))
                    {
                        logstring.Add($"ERROR - Calculation: AgeAtDate calculation for FieldName '{fieldName}' in worksheet '{worksheetName}' is missing required 'value' field.");
                        errorsEncountered = true;
                        worksheetErrorsEncountered = true;
                    }
                    break;

                case CalculationType.DateOffset:
                    if (string.IsNullOrEmpty(question.CalculationLookupField))
                    {
                        logstring.Add($"ERROR - Calculation: DateOffset calculation for FieldName '{fieldName}' in worksheet '{worksheetName}' is missing required 'field' field.");
                        errorsEncountered = true;
                        worksheetErrorsEncountered = true;
                    }
                    if (string.IsNullOrEmpty(question.CalculationConstantValue))
                    {
                        logstring.Add($"ERROR - Calculation: DateOffset calculation for FieldName '{fieldName}' in worksheet '{worksheetName}' is missing required 'value' field.");
                        errorsEncountered = true;
                        worksheetErrorsEncountered = true;
                    }
                    else if (!DateRangeRegex.IsMatch(question.CalculationConstantValue))
                    {
                         logstring.Add($"ERROR - Calculation: DateOffset calculation for FieldName '{fieldName}' in worksheet '{worksheetName}' has invalid 'value' format: {question.CalculationConstantValue}. Expected format like '+28d', '-1y', etc.");
                         errorsEncountered = true;
                         worksheetErrorsEncountered = true;
                    }
                    break;

                case CalculationType.DateDiff:
                    if (string.IsNullOrEmpty(question.CalculationLookupField))
                    {
                        logstring.Add($"ERROR - Calculation: DateDiff calculation for FieldName '{fieldName}' in worksheet '{worksheetName}' is missing required 'field' field (start date).");
                        errorsEncountered = true;
                        worksheetErrorsEncountered = true;
                    }
                    if (string.IsNullOrEmpty(question.CalculationConstantValue))
                    {
                        logstring.Add($"ERROR - Calculation: DateDiff calculation for FieldName '{fieldName}' in worksheet '{worksheetName}' is missing required 'value' field (end date).");
                        errorsEncountered = true;
                        worksheetErrorsEncountered = true;
                    }
                    if (string.IsNullOrEmpty(question.CalculationUnit))
                    {
                        logstring.Add($"ERROR - Calculation: DateDiff calculation for FieldName '{fieldName}' in worksheet '{worksheetName}' is missing required 'unit' field.");
                        errorsEncountered = true;
                        worksheetErrorsEncountered = true;
                    }
                    else if (!new[] { "d", "w", "m", "y" }.Contains(question.CalculationUnit.ToLower()))
                    {
                        logstring.Add($"ERROR - Calculation: DateDiff calculation for FieldName '{fieldName}' in worksheet '{worksheetName}' has invalid 'unit': {question.CalculationUnit}. Must be 'd', 'w', 'm', or 'y'.");
                        errorsEncountered = true;
                        worksheetErrorsEncountered = true;
                    }
                    break;
            }
        }

    }
}