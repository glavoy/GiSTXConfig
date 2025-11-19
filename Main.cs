using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Excel = Microsoft.Office.Interop.Excel;
using Microsoft.Office.Interop.Excel;
using System.Data;
using System.Data.SQLite;
using System.Text.RegularExpressions;
using System.Text;

namespace generatexml
{
    public partial class Main : Form
    {
        // Initialize the form
        public Main()
        {
            InitializeComponent();
        }


        private void Main_Load(object sender, EventArgs e)
        {
            // Make sure the 'Both' radio button is checked
            radioButtonBoth.Checked = true;

            // Show version
            labelVersion.Text = string.Concat("Version: ", swVer);
        }

        // Flags to determine if spreadsheet has errors
        Boolean errorsEncountered = false;
        Boolean worksheetErrorsEncountered = false;

        // Version
        readonly string swVer = "2025-11-09";



        //**********************************************************************************************************************************************************************
        //**********************************************************************************************************************************************************************
        //     The following variables need to be set
        //**********************************************************************************************************************************************************************
        //**********************************************************************************************************************************************************************


        //Test survey
        //***************************
        // Path to Excel file - this is the path to the Data Dictionary you want to use
        readonly string excelFile = "C:\\GeoffOffline\\GiSTConfigX\\Excel\\survey.xlsx";

        //***************************
        // Path to XML file - this is where the generated xml files wil be written
        readonly string xmlPath = "C:\\GeoffOffline\\GiSTX\\assets\\surveys\\";

        //***************************
        // Path to log file
        readonly string logfilePath = "C:\\temp\\";

        //***************************
        // Path to database
        //readonly string db_path = "C:\\PRISMCOMP\\MSAccessDatabase\\PRISMCOMP.mdb";
        readonly string db_path = "C:\\gistx\\database\\fake_survey.sqlite";

        //***************************
        // Source database to copy tables
        public string sourceDatabasePath = "C:\\gistx\\database\\gistx_config.sqlite";

        //***************************
        // name of the source tables to copy
        public string[] sourceTableNames = { "config"};


        //log string
        public List<string> logstring = new List<string>();


        // Question class
        // There is a new Question object created for each question in the Excel file
        // Each Question object is added to the QuestionList List.
        public class Question
        {
            public string fieldName;
            public string questionType;
            public string fieldType;
            public string questionText;
            public string maxCharacters;
            public string responses;
            public string lowerRange;
            public string upperRange;
            public string logicCheck;
            public string dontKnow;
            public string refuse;
            public string na;
            public string skip;
        }


        // List of Question objects
        public List<Question> QuestionList = new List<Question>();

        // Dictionary to hold the primary keys
        Dictionary<string, string> Primary_Keys = new Dictionary<string, string>();

        // Number of columns used in Excel spreadsheet
        readonly int numberOfColumns = 14;

        // String for column names
        readonly string[] columnNamesArray = { "FieldName", "QuestionType", "FieldType", "QuestionText", "MaxCharacters", "Responses", "LowerRange", "UpperRange", "LogicCheck", "DontKnow", "Refuse", "NA", "Skip", "Comments" };


        // Function when button is clicked
        private void ButtonXML_Click(object sender, EventArgs e)
        {
            try
            {
                // Use a wait cursor
                Cursor.Current = Cursors.WaitCursor;

                // Start logging of any error
                logstring.Add("Log file for: " + excelFile);

                // Open the Excel file
                Excel.Application xlApp;
                Excel.Workbook xlWorkBook;
                Excel.Range range;
                xlApp = new Excel.Application();
                xlWorkBook = xlApp.Workbooks.Open(@excelFile, 0, true, 5, "", "", true,
                                                  Microsoft.Office.Interop.Excel.XlPlatform.xlWindows,
                                                  "\t", false, false, 0, true, 1, 0);

                // Create a blank database
                if (radioButtonBoth.Checked == true)
                {
                    CreateMSAccessDatabase();
                }

                // Read each sheet of the Excel file and generate list of questions
                foreach (Worksheet worksheet in xlWorkBook.Worksheets)
                {
                    // Data dictionaries must end in '_dd'
                    if (worksheet.Name.Substring(worksheet.Name.Length - 3) == "_dd" || worksheet.Name.Substring(worksheet.Name.Length - 4) == "_xml")
                    {
                        CreateQuestionList(worksheet);

                        //// Check for duplicate columns in the question list before moving on
                        //CheckDuplicateColumns(QuestionList, worksheet.Name.Substring(0, worksheet.Name.Length - 3));

                        // If there are no errors in the spreadsheet, create XML files and write to database
                        if (!errorsEncountered)
                        {
                            // Write to the XML file
                            WriteXML(worksheet.Name);

                            // Add table to database
                            if (radioButtonBoth.Checked == true)
                            {
                                if (worksheet.Name.Substring(worksheet.Name.Length - 3) == "_dd")
                                    CreateTableInDatabase(worksheet.Name);
                            }
                        }
                    }
                    // Get the primary keys for the tables
                    else
                    {
                        if (worksheet.Name == "crfs")
                        {
                            // Get the range of used cells in the Excel file
                            range = worksheet.UsedRange;

                            // Variable to get the total number of rows used in the Excel file
                            int numRows = range.Rows.Count;

                            // Add the Primary Keys to the dictionary
                            for (int rowCount = 2; rowCount <= numRows; rowCount++)
                            {
                                Primary_Keys.Add(range.Cells[rowCount, 1].Value2.ToString(), range.Cells[rowCount, 2].Value2.ToString());
                            }

                            // Create the crfs table
                            CreateCrfsTable();
                            AddDataToTable(worksheet);
                            // CopyMasterTables(); // This copies the villages table and census survey table - comment this code out 
                        }
                    }
                }
                CopyMasterTables(); // This copies the villages table and census survey table - comment this code out 

                // Show the appropriate Message Box
                if (errorsEncountered)
                {
                    MessageBox.Show("The Data Dictionary contains errors! \r\rThe XML files and database HAVE NOT not been created! \r\rPlease refer to the log file and rectify all errors.");
                }
                else
                {
                    MessageBox.Show("Done Building the xml file(s) and the database and no errors were found. Please refer to the log file.");
                }


                //cleanup
                GC.Collect();
                GC.WaitForPendingFinalizers();
                xlWorkBook.Close(true, null, null);
                xlApp.Quit();
                Marshal.ReleaseComObject(xlWorkBook);
                Marshal.ReleaseComObject(xlApp);
                logstring.Add("\r--------------------------------------------------------------------------------");
                logstring.Add("End of log file");
                logstring.Add("--------------------------------------------------------------------------------");
                writeLogfile();
            }



            // Error handling in caase we could not crread the Excel file
            catch (Exception ex)
            {
                Console.WriteLine("Error msg " + ex.Message);
                MessageBox.Show("ERROR: There are unexpected errors with the Excel Data Dictionary!" + ex.Message);
                logstring.Add("ERROR: There are unexpected errors with the Excel Data Dictionary!" + ex.Message);
                logstring.Add("\r--------------------------------------------------------------------------------");
                logstring.Add("End of log file");
                logstring.Add("--------------------------------------------------------------------------------");
            }

            // Put the cursor back to normal
            Cursor.Current = Cursors.Default;
        }






        //////////////////////////////////////////////////////////////////////
        // Function to create the SQLite database
        //////////////////////////////////////////////////////////////////////
        private void CreateMSAccessDatabase()
        {
            try
            {
                // Delete the SQLite database if it exists
                if (File.Exists(db_path))
                {
                    File.Delete(db_path);
                }

                // Create the SQLite database
                SQLiteConnection.CreateFile(db_path);

                // Optionally, you can open a connection to verify
                using (var connection = new SQLiteConnection($"Data Source={db_path};Version=3;"))
                {
                    connection.Open();
                    // Database created successfully
                }

                // Create the form changes table
                CreateFormChanges();
            }
            catch (Exception ex)
            {
                MessageBox.Show("ERROR: Could not create SQLite database: " + ex.Message);
                logstring.Add("ERROR: Could not create SQLite database: " + ex.Message);
            }
        }



        //////////////////////////////////////////////////////////////////////
        // Function to create the question List from the Excel spreadsheet
        //////////////////////////////////////////////////////////////////////
        private void CreateQuestionList(Worksheet worksheet)
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

                // Variable to get the total number of rows used in the Excel file
                int numRows = range.Rows.Count;

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

                    rowRange = worksheet.Cells[rowCount, numberOfColumns];

                    string[] currentColumnNamesArr = new string[numberOfColumns];
                    if (rowCount == 1)
                    {

                        for (int i = 0; i < numberOfColumns; i++)
                        {
                            currentColumnNamesArr[i] = range.Cells[1, i + 1].Value2.ToString();
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
                            curQuestion.fieldName = range.Cells[rowCount, 1] != null && range.Cells[rowCount, 1].Value2 != null ? range.Cells[rowCount, 1].Value2.ToString() : "";
                            CheckFieldName(worksheet.Name, curQuestion.fieldName);
                            
                            // Get the questionType
                            curQuestion.questionType = range.Cells[rowCount, 2] != null && range.Cells[rowCount, 2].Value2 != null ? range.Cells[rowCount, 2].Value2.ToString() : "";

                            // Get the fieldType
                            curQuestion.fieldType = range.Cells[rowCount, 3] != null && range.Cells[rowCount, 3].Value2 != null ? range.Cells[rowCount, 3].Value.ToString() : "";

                            // Get Question Text
                            curQuestion.questionText = range.Cells[rowCount, 4] != null && range.Cells[rowCount, 4].Value2 != null ? range.Cells[rowCount, 4].Value2.ToString() : "";
                            if (curQuestion.questionText == "" && curQuestion.questionType != "automatic")
                            {
                                errorsEncountered = true;
                                worksheetErrorsEncountered = true;
                                logstring.Add("ERROR - QuestionText: FieldName '" + curQuestion.fieldName + "' in worksheet '" + worksheet.Name + "' has blank QuestionText.");
                            }

                            // Get max Characters
                            curQuestion.maxCharacters = range.Cells[rowCount, 5] != null && range.Cells[rowCount, 5].Value2 != null ? range.Cells[rowCount, 5].Value2.ToString() : "-9";
                            if (curQuestion.maxCharacters != "-9")
                            {
                                CheckMaxCharacters(worksheet.Name, curQuestion.maxCharacters, curQuestion.fieldName);
                            }

                            // Get the responses and then ensure that all questions and field types are correctly defined
                            curQuestion.responses = range.Cells[rowCount, 6] != null && range.Cells[rowCount, 6].Value2 != null ? range.Cells[rowCount, 6].Value2.ToString() : "";
                            // Need to check for blank reponses, but sometimes they are supposed to be blank if they are dynamically generated
                            //if (curQuestion.responses == "" && curQuestion.questionType == "radio")
                            //{
                            //    errorsEncountered = true;
                            //    worksheetErrorsEncountered = true;
                            //    logstring.Add("ERROR - Responses: FieldName '" + curQuestion.fieldName + "' in worksheet '" + worksheet.Name + "' does not have any responses.");
                            //}

                            CheckQuestionFieldType(curQuestion.questionType, curQuestion.fieldType, curQuestion.fieldName, worksheet.Name, curQuestion.responses);

                            // Get Lower range
                            curQuestion.lowerRange = range.Cells[rowCount, 7] != null && range.Cells[rowCount, 7].Value2 != null ? range.Cells[rowCount, 7].Value2.ToString() : "-9";
                            if (curQuestion.lowerRange != "-9")
                            {
                                CheckUpperLowerRange(worksheet.Name, curQuestion.lowerRange, curQuestion.fieldName, "LowerRange");
                            }

                            // Get Upper range
                            curQuestion.upperRange = range.Cells[rowCount, 8] != null && range.Cells[rowCount, 8].Value2 != null ? range.Cells[rowCount, 8].Value2.ToString() : "-9";
                            if (curQuestion.upperRange != "-9")
                            {
                                CheckUpperLowerRange(worksheet.Name, curQuestion.upperRange, curQuestion.fieldName, "UpperRange");
                            }

                            // Get Logic check
                            curQuestion.logicCheck = range.Cells[rowCount, 9] != null && range.Cells[rowCount, 9].Value2 != null ? range.Cells[rowCount, 9].Value2.ToString() : "";
                            if (curQuestion.logicCheck != "")
                            {
                                CheckLogicCheckSyntax(worksheet.Name, curQuestion.logicCheck, curQuestion.fieldName);
                            }


                            // Special Buttons
                            // don't know
                            curQuestion.dontKnow = range.Cells[rowCount, 10] != null && range.Cells[rowCount, 10].Value2 != null ? range.Cells[rowCount, 10].Value2.ToString() : "-9";
                            if (curQuestion.dontKnow != "-9")
                            {
                                CheckSpecialButton(worksheet.Name, curQuestion.dontKnow, curQuestion.fieldName, "DontKnow");
                            }
                            //refuse
                            curQuestion.refuse = range.Cells[rowCount, 11] != null && range.Cells[rowCount, 11].Value2 != null ? range.Cells[rowCount, 11].Value2.ToString() : "-9";
                            if (curQuestion.refuse != "-9")
                            {
                                CheckSpecialButton(worksheet.Name, curQuestion.refuse, curQuestion.fieldName, "Refuse");
                            }


                            curQuestion.na = range.Cells[rowCount, 12] != null && range.Cells[rowCount, 12].Value2 != null ? range.Cells[rowCount, 12].Value2.ToString() : "-9";
                            if (curQuestion.na != "-9")
                            {
                                CheckSpecialButton(worksheet.Name, curQuestion.na, curQuestion.fieldName, "NA");
                            }


                            curQuestion.skip = range.Cells[rowCount, 13] != null && range.Cells[rowCount, 13].Value2 != null ? range.Cells[rowCount, 13].Value2.ToString() : "";
                            if (curQuestion.skip != "")
                            {
                                CheckSkipSyntax(worksheet.Name, curQuestion.skip, curQuestion.fieldName);
                            }
                            QuestionList.Add(curQuestion);
                        }
                    }
                }

                // Trim and leading and trailing spaces
                foreach (Question question in QuestionList)
                {
                    question.fieldName = question.fieldName.Trim();
                    question.questionType = question.questionType.Trim();
                    question.fieldType = question.fieldType.Trim();
                    question.questionText = question.questionText.Trim();
                    question.maxCharacters = question.maxCharacters.Trim();
                    question.responses = question.responses.Trim();
                    question.lowerRange = question.lowerRange.Trim();
                    question.upperRange = question.upperRange.Trim();
                    question.logicCheck = question.logicCheck.Trim();
                    question.dontKnow = question.dontKnow.Trim();
                    question.refuse = question.refuse.Trim();
                    question.na = question.na.Trim();
                    question.skip = question.skip.Trim();
                }

                if (worksheetErrorsEncountered == false)
                {
                    // Check fieldnames in logic checks
                    CheckLogicFieldNames(worksheet.Name);
                    // Check fieldnames in skips
                    CheckSkipToFieldNames(worksheet.Name);
                    // Check if missing MaxCharacters for text fields
                    CheckMaxCharacters(worksheet.Name);
                    // Add automatic variables
                    
                    if (worksheetErrorsEncountered == false)
                    {
                        ListAutomaticVariables();
                    }
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
                MessageBox.Show("ERROR: There were unexpected errors in the Excel Data Dictionary: " + "Worksheet: " + worksheet.Name + " Error: " + ex.Message);
                logstring.Add("ERROR: There were unexpected errors in the Excel Data Dictionary: " + "Worksheet: " + worksheet.Name + " Error: " + ex.Message);
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
            // Check if maxCharacters is numeric
            if (!Regex.IsMatch(maxChars, @"^\d+$"))
            {
                errorsEncountered = true;
                worksheetErrorsEncountered = true;
                logstring.Add("ERROR - MaxCharacters: FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has a non-numeric value for MaxCharacters: " + maxChars);
                return;
            }

            if (int.TryParse(maxChars, out int num))
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
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void CheckQuestionFieldType(string questiontype, string fieldtype, string fieldname, string tblename, string responseStr)
        {
            string[] qtype = { "radio", "combobox", "checkbox", "text", "date", "information", "automatic", "button" };
            string[] ftype = { "text", "datetime", "date", "phone_num", "integer", "text_integer", "text_decimal", "text_id", "n/a", "hourmin" };


            if (!qtype.Contains(questiontype))
            {
                errorsEncountered = true;
                worksheetErrorsEncountered = true;
                logstring.Add("ERROR - QuestionType: The QuestionType " + questiontype + " for FieldName '" + fieldname + "' in table '" + tblename + "' is not among the predefined list.");
            }

            if (!ftype.Contains(fieldtype))
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

            // check the duplicate responses for radio buttons and checkboxes
            if (questiontype == "radio" | questiontype == "checkbox")
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
                            logstring.Add("ERROR - Responses: Invalid radio button options for '" + fieldname + "' in table '" + tblename + "'");
                            return;
                        }

                        // Check for more than 1 colon (:) in a line
                        string[] responseString = response.Split(':'); // split the string using :
                        if (responseString.Length != 2)
                        {
                            errorsEncountered = true;
                            worksheetErrorsEncountered = true;
                            logstring.Add("ERROR - Responses: Invalid radio button options for '" + fieldname + "' in table '" + tblename + "'");
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
                            logstring.Add("ERROR - Responses: Invalid radio button options for '" + fieldname + "' in table '" + tblename + "'. Please remove leading spaces");
                            return;
                        }
                        // Check if there is a space after the colon
                        if (response.Contains(": "))
                        {
                            errorsEncountered = true;
                            worksheetErrorsEncountered = true;
                            logstring.Add("ERROR - Responses: Invalid radio button options for '" + fieldname + "' in table '" + tblename + "'. Please remove space after the colon (:)");
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
            //if (!Regex.IsMatch(range, @"^\d+$"))
              if (!Regex.IsMatch(range, @"^\d+(\.\d+)?$"))
            {
                errorsEncountered = true;
                worksheetErrorsEncountered = true;
                logstring.Add("ERROR - " + rangeName + ": FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has a non-numeric value for " + rangeName + ": " + range);
                return;
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
                MessageBox.Show("ERROR - LogicCheck: FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has invalid syntax for LogicCheck." + ex.Message);
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
                logstring.Add("ERROR: - " + buttonName+  " FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has an invalid value for '" + buttonName + "': " + val);
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
                MessageBox.Show("ERROR - Skip: FieldName '" + fieldname + "' in worksheet '" + worksheet + "' has invalid syntax for Skip." + ex.Message);
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
                // Create a list of all the fieldnames in the worksheet
                List<string> fieldnames = new List<string>();
                foreach (Question question in QuestionList)
                {
                    fieldnames.Add(question.fieldName);
                }

                foreach (Question question in QuestionList)
                {
                    if (question.logicCheck != "")
                    {
                        curFieldname = question.fieldName;

                        // New format: extract expression from "expression; 'message'"
                        string[] parts = question.logicCheck.Split(new char[] { ';' }, 2);
                        string expression = parts[0].Trim();

                        // Extract potential field names from the expression
                        // Remove quoted strings first to avoid matching field names in quotes
                        string cleanExpression = Regex.Replace(expression, @"'[^']*'", "");

                        // Match word characters (field names) - excluding operators and numbers
                        // Field names are alphanumeric + underscore, starting with a letter
                        MatchCollection matches = Regex.Matches(cleanExpression, @"\b[a-z_][a-z0-9_]*\b");

                        HashSet<string> referencedFieldNames = new HashSet<string>();
                        foreach (Match match in matches)
                        {
                            string potentialFieldName = match.Value;
                            // Skip SQL/logic keywords
                            if (potentialFieldName != "and" && potentialFieldName != "or" && potentialFieldName != "not")
                            {
                                referencedFieldNames.Add(potentialFieldName);
                            }
                        }

                        // Check each referenced field name
                        foreach (string referencedFieldName in referencedFieldNames)
                        {
                            // Check if it exists in the fieldnames list
                            if (fieldnames.Contains(referencedFieldName))
                            {
                                int fieldname_to_check_index = fieldnames.IndexOf(referencedFieldName);
                                int curFieldnameIndex = fieldnames.IndexOf(curFieldname);

                                // Check if the referenced field is after the current question
                                if (fieldname_to_check_index > curFieldnameIndex)
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
                MessageBox.Show("ERROR - LogicCheck: FieldName '" + curFieldname + "' in worksheet '" + worksheet + "' has invalid syntax for LogicCheck." + ex.Message);
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

                // Create a list of all the fieldnames in the worksheet
                List<string> fieldnames = new List<string>();
                foreach (Question question in QuestionList)
                {
                    fieldnames.Add(question.fieldName);
                }

                foreach (Question question in QuestionList)
                {
                    if (question.skip != "")
                    {
                        curFieldname = question.fieldName;

                        string[] skips = question.skip.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (string skip in skips)
                        {
                            string[] words = skip.Split(' ');
                            fieldname_to_check = words[2];
                            fieldname_to_skip_to = words[words.Length - 1];
                        }

                        // Check if the field name to check value of exists and is before the current question
                        if (fieldnames.Contains(fieldname_to_check))
                        {
                            int fieldname_to_check_index = fieldnames.IndexOf(fieldname_to_check);
                            int fieldname_of_skip = fieldnames.IndexOf(curFieldname);

                            if (fieldname_to_check_index > fieldname_of_skip)
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

                        // Check iof the field name to skip to is legitimate - exists and is after the current question
                        if (fieldnames.Contains(fieldname_to_skip_to))
                        {
                            int fieldname_to_skip_to_index = fieldnames.IndexOf(fieldname_to_skip_to);
                            int fieldname_of_skip = fieldnames.IndexOf(curFieldname);

                            if (fieldname_to_skip_to_index < fieldname_of_skip)
                            {
                                errorsEncountered = true;
                                worksheetErrorsEncountered = true;
                                logstring.Add("ERROR - Skip: In worksheet '" + worksheet + "', the skip for FieldName '" + curFieldname + "' skips to a FieldName BEFORE the current question: " + fieldname_to_skip_to);
                            }
                            else if (fieldname_to_skip_to_index == fieldname_of_skip)
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
            catch (Exception ex)
            {
                errorsEncountered = true;
                worksheetErrorsEncountered = true;
                MessageBox.Show("ERROR - Skip: FieldName '" + curFieldname + "' in worksheet '" + worksheet + "' has invalid syntax for Skip." + ex.Message);
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
                MessageBox.Show("ERROR - Skip: FieldName '" + curFieldname + "' in worksheet '" + worksheet + "' has invalid syntax for Skip." + ex.Message);
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


        //////////////////////////////////////////////////////////////////////
        // Function to create a list of automatic variables
        //////////////////////////////////////////////////////////////////////
        private void ListAutomaticVariables()
        {
            List<string> list = new List<string>();
            foreach (Question question in QuestionList)
            {
                if (question.questionType == "automatic")
                {
                    list.Add(question.fieldName);
                }
            }

            if (list.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                foreach (string item in list)
                {
                    sb.Append(item);
                    sb.Append(", ");
                }

                // Remove the last comma from the string
                sb.Remove(sb.Length - 2, 2);

                // Use the final string
                string finalString = sb.ToString();
                logstring.Add("Be sure to write code for each automatic variable: " + finalString);
            }
        }


        //////////////////////////////////////////////////////////////////////
        // Function to create and write to XML file
        //////////////////////////////////////////////////////////////////////
        private void WriteXML(string xmlfilename)
        {
            try
            {
                if (xmlfilename.Substring(xmlfilename.Length - 3) == "_dd")
                {
                    xmlfilename = xmlfilename.Substring(0, xmlfilename.Length - 3);
                }
                else
                {
                    xmlfilename = xmlfilename.Substring(0, xmlfilename.Length - 4);
                }

                // These are strings for the first two of lines in the xml file
                string[] xmlStart = { "<?xml version = '1.0' encoding = 'utf-8'?>", "<survey>" };

                // Open a XML file and start writing lines of text to it
                using (StreamWriter outputFile = new StreamWriter(string.Concat(xmlPath, xmlfilename, ".xml")))
                {
                    // Write the first 2 lines to the XML file
                    foreach (string line in xmlStart)
                        outputFile.WriteLine(line);

                    // Write a blank line 
                    outputFile.WriteLine("\n");


                    // Iterate through each question object in the QuestionList list
                    // and write the necessary text to the XML file
                    foreach (Question question in QuestionList)
                    {
                        // Write the main part of the question
                        // Uses questionType, fieldName and fieldType
                        outputFile.WriteLine(string.Concat("\t<question type = '", question.questionType,
                                                           "' fieldname = '", question.fieldName,
                                                           "' fieldtype = '", question.fieldType, "'>"));


                        // Write the text if it is not an automatic question
                        if (question.questionType != "automatic")
                            outputFile.WriteLine(string.Concat("\t\t<text>", question.questionText, "</text>"));


                        // The maximum characters if necessary
                        if (question.maxCharacters != "-9")
                            outputFile.WriteLine(string.Concat("\t\t<maxCharacters>", question.maxCharacters, "</maxCharacters>"));


                        // Upper and Lower range (numeric check)
                        if (question.lowerRange != "-9")
                        {
                            outputFile.WriteLine("\t\t<numeric_check>");
                            outputFile.WriteLine(string.Concat("\t\t\t<values minvalue ='", question.lowerRange,
                                                               "' maxvalue='", question.upperRange,
                                                               "' other_values = '", question.lowerRange,
                                                               "' message = 'Number must be between ", question.lowerRange,
                                                               " and ", question.upperRange, "!'></values>"));
                            outputFile.WriteLine("\t\t</numeric_check>");
                        }

                        //  Logic Checks (Added by werick)
                        if (question.logicCheck != "")
                        {
                            // New format: just output the logic check directly
                            outputFile.WriteLine("\t\t<logic_check>");
                            outputFile.WriteLine(GenerateLogicChecks(question.logicCheck));
                            outputFile.WriteLine("\t\t</logic_check>");
                        }

                        // Write responses if it is a radio or checkbox type question
                        if (question.questionType == "radio" || question.questionType == "checkbox" || question.questionType == "combobox")
                        {
                            outputFile.WriteLine("\t\t<responses>");
                            string[] responses = question.responses.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                            if (responses.Length == 0)
                            {
                                outputFile.WriteLine("\t\t\t<response></response>");
                            }
                            else
                            {
                                foreach (string response in responses)
                                {
                                    int index = response.IndexOf(@":");
                                    outputFile.WriteLine(string.Concat("\t\t\t<response value = '", response.Substring(0, index), "'>",
                                                                        response.Substring(index + 1).Trim(), "</response>"));
                                }
                            }

                            outputFile.WriteLine("\t\t</responses>");
                        }


                        // Skips
                        if (question.skip != "")
                        {
                            // This stores the text for the skip
                            string[] skips = question.skip.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                            // Lists to store preskips and postskips
                            List<string> preSkips = new List<string>();
                            List<string> postSkips = new List<string>();


                            // Populate the list for each type of skip
                            foreach (string skip in skips)
                            {
                                int index = skip.IndexOf(@":");

                                if (skip.Substring(0, index) == "preskip")
                                    preSkips.Add(skip);

                                if (skip.Substring(0, index) == "postskip")
                                    postSkips.Add(skip);
                            }


                            // Create text preskips
                            if (preSkips.Count > 0)
                            {
                                outputFile.WriteLine("\t\t<preskip>");
                                foreach (string preSkip in preSkips)
                                {
                                    // Call the GenerateSkips() function
                                    outputFile.WriteLine(GenerateSkips(preSkip, "preSkip"));
                                }
                                outputFile.WriteLine("\t\t</preskip>");
                            }


                            // Create text postskips
                            if (postSkips.Count > 0)
                            {
                                outputFile.WriteLine("\t\t<postskip>");
                                // Call the GenerateSkips() function
                                foreach (string postSkip in postSkips)
                                {
                                    outputFile.WriteLine(GenerateSkips(postSkip, "postSkip"));
                                }
                                outputFile.WriteLine("\t\t</postskip>");
                            }
                        }



                        // Don't know
                        if (question.dontKnow == "TRUE" || question.dontKnow == "True")
                            outputFile.WriteLine("\t\t<dont_know>-7</dont_know>");

                        // Refuse to answer
                        if (question.refuse == "TRUE" || question.refuse == "True")
                            outputFile.WriteLine("\t\t<refuse>-8</refuse>");

                        // Not applicable
                        if (question.na == "TRUE" || question.na == "True")
                            outputFile.WriteLine("\t\t<na>-6</na>");

                        // Close off the question
                        outputFile.WriteLine("\t</question>");
                        outputFile.WriteLine("\n");
                    }

                    // The last 'info' question ending every survey
                    string[] xmlEnd = {"\t<question type = 'information' fieldname = 'end_of_questions' fieldtype = 'n/a'>",
                                   "\t\t<text>Press the 'Finish' button to save the data.</text >", "\t</question>" };
                    foreach (string line in xmlEnd)
                        outputFile.WriteLine(line);

                    outputFile.WriteLine("\n");
                    outputFile.WriteLine("</survey>");
                }
            }


            // Error handling in caase we could not create the XML file
            catch (Exception ex)
            {
                MessageBox.Show("ERROR - Writing to XML file: Could not create XML file " + xmlfilename + " Ensure path is correct." + ex.Message);
                logstring.Add("ERROR - Writing to XML file: Could not create XML file " + xmlfilename + " Ensure path is correct." + ex.Message);
            }
        }



        //////////////////////////////////////////////////////////////////////
        // Function to generate the text for the skips
        //////////////////////////////////////////////////////////////////////
        private string GenerateSkips(string skip, string skipType)
        {
            // Number of initial characters depending on whether it's a preskip or postskip
            int lenSkip = skipType == "postSkip" ? 13 : 12;


            // Create a list to store the index of each 'space' in the skip text
            var spaceIndices = new List<int>();

            // Populate the spaceIndices list
            for (int i = 0; i < skip.Length; i++)
                if (skip[i] == ' ') spaceIndices.Add(i);


            // Get the name of the field to check for skip
            string fieldname_to_check = skip.Substring(lenSkip, spaceIndices[2] - spaceIndices[1] - 1);

            // Variables to store the condition and the value of the skip
            string condition;
            string value;

            // If there are 9 spaces, then we know that the condition is 'does not contain'
            if (spaceIndices.Count == 9)
            {
                // Get the condition
                condition = "does not contain";
                // Get the value
                value = skip.Substring(spaceIndices[5] + 1, spaceIndices[6] - spaceIndices[5] - 2);
            }
            // Check if the skip has 'contains'
            else if (skip.Contains("contains"))
            {
                // Get the condition
                condition = "contains";
                // Get the value
                value = skip.Substring(spaceIndices[3] + 1, spaceIndices[4] - spaceIndices[3] - 2);
            }
            // Skip does not have 'does not contain' or 'contains'
            else
            {
                // Get the condition
                condition = skip.Substring(spaceIndices[2] + 1, spaceIndices[3] - spaceIndices[2] - 1);

                // Replace '<' and '>' symbols, if necessary
                condition = condition.Replace("<", "&lt;");
                condition = condition.Replace(">", "&gt;");

                // Get the value
                value = skip.Substring(spaceIndices[3] + 1, spaceIndices[4] - spaceIndices[3] - 2);
            }

            // Get the field name to skip to
            string fieldname_to_skip_to = skip.Substring(spaceIndices[spaceIndices.Count - 1] + 1);

            // Build the string and return it
            return string.Concat("\t\t\t<skip fieldname='", fieldname_to_check,
                                 "' condition = '", condition,
                                 "' response='", value,
                                 "' response_type='fixed' skiptofieldname ='",
                                 fieldname_to_skip_to, "'></skip>");
        }


        // Added by Werick
        //////////////////////////////////////////////////////////////////////
        // Function to generate the text for the logic checks
        //////////////////////////////////////////////////////////////////////
        private string GenerateLogicChecks(string logicCheck)
        {
            // New format: expression; 'error message'
            // Example: tabletnum2 != tabletnum; 'This does not match your previous entry!'

            // Split by semicolon to get expression and message
            string[] parts = logicCheck.Split(new char[] { ';' }, 2);
            string expression = parts[0].Trim();
            string message = parts[1].Trim();

            // Replace operators with XML entities
            expression = expression.Replace("!=", "&lt;&gt;");
            expression = expression.Replace("<>", "&lt;&gt;");
            expression = expression.Replace("<=", "&lt;=");
            expression = expression.Replace(">=", "&gt;=");
            // Replace individual < and > that aren't part of <= or >=
            expression = Regex.Replace(expression, @"(?<!&lt;)(?<!&gt;)<(?!=)", "&lt;");
            expression = Regex.Replace(expression, @"(?<!&lt;=)(?<!&gt;=)>(?!=)", "&gt;");

            StringBuilder result = new StringBuilder();

            // Check if expression contains 'or' - if so, format it across multiple lines
            if (expression.Contains(" or "))
            {
                string[] orParts = expression.Split(new string[] { " or " }, StringSplitOptions.None);

                for (int i = 0; i < orParts.Length; i++)
                {
                    result.Append("\t\t\t");
                    result.Append(orParts[i].Trim());

                    if (i < orParts.Length - 1)
                    {
                        result.Append(" or");
                        result.AppendLine();
                    }
                }
                result.AppendLine(";");
                result.Append("\t\t\t");
                result.Append(message);
            }
            else
            {
                // Single line format
                result.Append("\t\t\t");
                result.Append(expression);
                result.Append("; ");
                result.Append(message);
            }

            return result.ToString();
        }



        //////////////////////////////////////////////////////////////////////
        // Function write to log file
        //////////////////////////////////////////////////////////////////////
        private void writeLogfile()
        {
            try
            {
                var logfilename = "gistlogfile";
                // Open a log file and start writing lines of text to it
                using (StreamWriter outputFile = new StreamWriter(string.Concat(logfilePath, logfilename, ".txt")))
                {
                    foreach (string line in logstring)
                        outputFile.WriteLine(line);
                    outputFile.WriteLine("\n");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("CRITICAL ERROR: Could not write to log file! Ensure path is correct." + ex.Message);
            }

        }

        //////////////////////////////////////////////////////////////////////
        // Function to create a table in the SQLite database
        //////////////////////////////////////////////////////////////////////
        private void CreateTableInDatabase(string tablename)
        {
            try
            {
                tablename = tablename.Substring(0, tablename.Length - 3);

                using (var connection = new SQLiteConnection($"Data Source={db_path};Version=3;"))
                {
                    connection.Open();

                    // Build CREATE TABLE statement
                    StringBuilder createTableQuery = new StringBuilder();
                    createTableQuery.Append($"CREATE TABLE IF NOT EXISTS [{tablename}] (");

                    List<string> columns = new List<string>();

                    // Create a field name for each question
                    foreach (Question question in QuestionList)
                    {
                        // Don't need to create a field for 'information' questions
                        if (question.questionType != "information" && question.fieldType != "n/a")
                        {
                            string columnDef = $"[{question.fieldName}] ";

                            // Map field types to SQLite types
                            switch (question.fieldType)
                            {
                                case "text_integer":
                                case "integer":
                                    columnDef += "INTEGER";
                                    break;
                                case "text":
                                case "text_id":
                                case "phone_num":
                                case "hourmin":
                                    columnDef += "TEXT";
                                    break;
                                case "text_decimal":
                                    columnDef += "REAL";
                                    break;
                                case "date":
                                case "datetime":
                                    columnDef += "TEXT"; // SQLite stores dates as TEXT, REAL, or INTEGER
                                    break;
                                default:
                                    columnDef += "TEXT";
                                    break;
                            }

                            columns.Add(columnDef);
                        }
                    }

                    createTableQuery.Append(string.Join(", ", columns));
                    createTableQuery.Append(")");

                    using (var command = new SQLiteCommand(createTableQuery.ToString(), connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("ERROR - Database: Could not create " + tablename + " in database. " + ex.Message);
                logstring.Add("ERROR - Database: Could not create " + tablename + " in database. " + ex.Message);
            }
        }





        //////////////////////////////////////////////////////////////////////
        // Function to create the formchanges table in SQLite database
        //////////////////////////////////////////////////////////////////////
        private void CreateFormChanges()
        {
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={db_path};Version=3;"))
                {
                    connection.Open();

                    string createTableQuery = @"
                        CREATE TABLE formchanges (
                            changeid     INTEGER PRIMARY KEY AUTOINCREMENT,
                            tablename    TEXT NOT NULL,
                            fieldname    TEXT NOT NULL,
                            uniqueid     TEXT NOT NULL,
                            oldvalue     TEXT,
                            newvalue     TEXT,
                            changed_at   DATETIME DEFAULT (CURRENT_TIMESTAMP)
                        )";

                    using (var command = new SQLiteCommand(createTableQuery, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("ERROR: Could not create 'formchanges' table in database. " + ex.Message);
                logstring.Add("ERROR: Could not create 'formchanges' table in database. " + ex.Message);
            }
        }





        //////////////////////////////////////////////////////////////////////
        // Function to create the crfs table in SQLite database
        //////////////////////////////////////////////////////////////////////
        private void CreateCrfsTable()
        {
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={db_path};Version=3;"))
                {
                    connection.Open();

                    string createTableQuery = @"
                        CREATE TABLE IF NOT EXISTS crfs (
	                        tablename	TEXT,
	                        primarykey	TEXT,
	                        displayname	TEXT,
	                        isbase	INTEGER DEFAULT 0,
	                        linkingfield	TEXT,
	                        parenttable	TEXT,
	                        incrementfield	TEXT,
	                        requireslink	INTEGER DEFAULT 0,
	                        idconfig	TEXT
                        )";

                    using (var command = new SQLiteCommand(createTableQuery, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("ERROR: Could not create 'crfs' table in database. " + ex.Message);
                logstring.Add("ERROR: Could not create 'crfs' table in database. " + ex.Message);
            }
        }


        private void AddDataToTable(Excel.Worksheet crf_ws)
        {
            try
            {
                using (var connection = new SQLiteConnection($"Data Source={db_path};Version=3;"))
                {
                    connection.Open();

                    string insertQuery =
                        "INSERT INTO crfs (tablename, primarykey, displayname, isbase, linkingfield, parenttable, incrementfield, requireslink, idconfig) " +
                        "VALUES (@tablename, @primarykey, @displayname, @isbase, @linkingfield, @parenttable, @incrementfield, @requireslink, @idconfig)";

                    using (var insertCommand = new SQLiteCommand(insertQuery, connection))
                    {
                        Excel.Range usedRange = crf_ws.UsedRange;
                        for (int row = 2; row <= usedRange.Rows.Count; row++)
                        {
                            insertCommand.Parameters.Clear();

                            insertCommand.Parameters.AddWithValue("@tablename", ((Excel.Range)usedRange.Cells[row, 1]).Value2);
                            insertCommand.Parameters.AddWithValue("@primarykey", ((Excel.Range)usedRange.Cells[row, 2]).Value2);
                            insertCommand.Parameters.AddWithValue("@displayname", ((Excel.Range)usedRange.Cells[row, 3]).Value2);
                            insertCommand.Parameters.AddWithValue("@isbase", ((Excel.Range)usedRange.Cells[row, 4]).Value2 ?? 0);
                            insertCommand.Parameters.AddWithValue("@linkingfield", ((Excel.Range)usedRange.Cells[row, 5]).Value2);
                            insertCommand.Parameters.AddWithValue("@parenttable", ((Excel.Range)usedRange.Cells[row, 6]).Value2);
                            insertCommand.Parameters.AddWithValue("@incrementfield", ((Excel.Range)usedRange.Cells[row, 7]).Value2);
                            insertCommand.Parameters.AddWithValue("@requireslink", ((Excel.Range)usedRange.Cells[row, 8]).Value2 ?? 0);
                            insertCommand.Parameters.AddWithValue("@idconfig", ((Excel.Range)usedRange.Cells[row, 9]).Value2);

                            insertCommand.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("ERROR: Could not add data to crfs table. " + ex.Message);
                logstring.Add("ERROR: Could not add data to crfs table. " + ex.Message);
            }
        }








        //////////////////////////////////////////////////////////////////////
        // Copies the 'master' tables to database
        //////////////////////////////////////////////////////////////////////
        private void CopyMasterTables()
        {
            try
            {
                // Check if the source database exists
                if (!File.Exists(sourceDatabasePath))
                {
                    // Exit the function if the master database doesn't exist
                    return;
                }

                // Connection strings for SQLite databases
                string sourceConnectionString = $"Data Source={sourceDatabasePath};Version=3;";
                string destConnectionString = $"Data Source={db_path};Version=3;";

                using (var sourceConnection = new SQLiteConnection(sourceConnectionString))
                {
                    sourceConnection.Open();

                    using (var destConnection = new SQLiteConnection(destConnectionString))
                    {
                        destConnection.Open();

                        foreach (string sourceTableName in sourceTableNames)
                        {
                            // Get the table schema from source
                            string getSchemaQuery = $"SELECT sql FROM sqlite_master WHERE type='table' AND name='{sourceTableName}'";
                            string createTableSql = "";

                            using (var schemaCommand = new SQLiteCommand(getSchemaQuery, sourceConnection))
                            {
                                var result = schemaCommand.ExecuteScalar();
                                if (result != null)
                                {
                                    createTableSql = result.ToString();
                                }
                            }

                            // Create the table in destination database
                            if (!string.IsNullOrEmpty(createTableSql))
                            {
                                using (var createCommand = new SQLiteCommand(createTableSql, destConnection))
                                {
                                    createCommand.ExecuteNonQuery();
                                }

                                // Copy data from source to destination
                                string selectQuery = $"SELECT * FROM {sourceTableName}";
                                using (var selectCommand = new SQLiteCommand(selectQuery, sourceConnection))
                                {
                                    using (var reader = selectCommand.ExecuteReader())
                                    {
                                        // Build INSERT command dynamically based on columns
                                        var columnCount = reader.FieldCount;
                                        var columnNames = new List<string>();
                                        var paramNames = new List<string>();

                                        for (int i = 0; i < columnCount; i++)
                                        {
                                            columnNames.Add(reader.GetName(i));
                                            paramNames.Add($"@param{i}");
                                        }

                                        string insertQuery = $"INSERT INTO {sourceTableName} ({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", paramNames)})";

                                        while (reader.Read())
                                        {
                                            using (var insertCommand = new SQLiteCommand(insertQuery, destConnection))
                                            {
                                                for (int i = 0; i < columnCount; i++)
                                                {
                                                    insertCommand.Parameters.AddWithValue($"@param{i}", reader.GetValue(i));
                                                }
                                                insertCommand.ExecuteNonQuery();
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("ERROR: Could not copy master tables. " + ex.Message);
                logstring.Add("ERROR: Could not copy master tables. " + ex.Message);
            }
        }

    }
}
