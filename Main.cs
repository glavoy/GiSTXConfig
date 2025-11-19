using Newtonsoft.Json;
using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Excel = Microsoft.Office.Interop.Excel;
using Microsoft.Office.Interop.Excel;



namespace generatexml
{
    public partial class Main : Form
    {
        private AppConfig config;

        // Initialize the form
        public Main()
        {
            InitializeComponent();
        }

        // Version
        readonly string swVer = "2025-11-19";

        private void Main_Load(object sender, EventArgs e)
        {
            // Load configuration from JSON file
            config = JsonConvert.DeserializeObject<AppConfig>(File.ReadAllText("config.json"));

            // Make sure the 'Both' radio button is checked
            radioButtonBoth.Checked = true;

            // Show version
            labelVersion.Text = string.Concat("Version: ", swVer);
        }

        // Flags to determine if spreadsheet has errors
        Boolean errorsEncountered = false;



        //**********************************************************************************************************************************************************************
        //**********************************************************************************************************************************************************************
        //     The following variables need to be set
        //**********************************************************************************************************************************************************************
        //**********************************************************************************************************************************************************************


        //log string
        public List<string> logstring = new List<string>();


        // List of Question objects
        public List<Question> QuestionList = new List<Question>();

        // Dictionary to hold the primary keys
        Dictionary<string, string> Primary_Keys = new Dictionary<string, string>();


        // Function when button is clicked
        private void ButtonXML_Click(object sender, EventArgs e)
        {
            try
            {
                // Use a wait cursor
                Cursor.Current = Cursors.WaitCursor;

                // Start logging of any error
                logstring.Add("Log file for: " + config.excelFile);
                Primary_Keys.Clear();

                // Open the Excel file
                Excel.Application xlApp;
                Excel.Workbook xlWorkBook;
                Excel.Range range;
                xlApp = new Excel.Application();
                xlWorkBook = xlApp.Workbooks.Open(@config.excelFile, 0, true, 5, "", "", true,
                                                  Microsoft.Office.Interop.Excel.XlPlatform.xlWindows,
                                                  "\t", false, false, 0, true, 1, 0);

                DatabaseManager dbManager = new DatabaseManager();

                // Create a blank database
                if (radioButtonBoth.Checked == true)
                {
                    dbManager.CreateSQLiteDatabase(config.db_path);
                    logstring.AddRange(dbManager.logstring);
                }

                // Read each sheet of the Excel file and generate list of questions
                foreach (Worksheet worksheet in xlWorkBook.Worksheets)
                {
                    // Data dictionaries must end in '_dd'
                    if (worksheet.Name.Substring(worksheet.Name.Length - 3) == "_dd" || worksheet.Name.Substring(worksheet.Name.Length - 4) == "_xml")
                    {
                        ExcelReader excelReader = new ExcelReader();
                        excelReader.CreateQuestionList(worksheet);
                        if (excelReader.errorsEncountered)
                        {
                            errorsEncountered = true;
                        }
                        logstring.AddRange(excelReader.logstring);
                    }
                }

                if (!errorsEncountered)
                {
                    foreach (Worksheet worksheet in xlWorkBook.Worksheets)
                    {
                        if (worksheet.Name.Substring(worksheet.Name.Length - 3) == "_dd" || worksheet.Name.Substring(worksheet.Name.Length - 4) == "_xml")
                        {
                            ExcelReader excelReader = new ExcelReader();
                            excelReader.CreateQuestionList(worksheet);
                            QuestionList = excelReader.QuestionList;
                            // Write to the XML file
                            XmlGenerator xmlGenerator = new XmlGenerator();
                            xmlGenerator.WriteXML(worksheet.Name, QuestionList, config.xmlPath);
                            logstring.AddRange(xmlGenerator.logstring);

                            // Add table to database
                            if (radioButtonBoth.Checked == true)
                            {
                                if (worksheet.Name.Substring(worksheet.Name.Length - 3) == "_dd")
                                {
                                    dbManager.CreateTableInDatabase(worksheet.Name, config.db_path, QuestionList);
                                    logstring.AddRange(dbManager.logstring);
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
                                if (radioButtonBoth.Checked == true)
                                {
                                    dbManager.CreateCrfsTable(config.db_path);
                                    dbManager.AddDataToTable(worksheet, config.db_path);
                                    logstring.AddRange(dbManager.logstring);
                                }
                            }
                        }
                    }
                }
                if (radioButtonBoth.Checked == true && !string.IsNullOrEmpty(config.sourceDatabasePath))
                {
                    dbManager.CopyMasterTables(config.sourceDatabasePath, config.db_path, config.sourceTableNames); // This copies the villages table and census survey table
                    logstring.AddRange(dbManager.logstring);
                }

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

        private void writeLogfile()
        {
            try
            {
                var logfilename = "gistlogfile";
                // Open a log file and start writing lines of text to it
                using (StreamWriter outputFile = new StreamWriter(string.Concat(config.logfilePath, logfilename, ".txt")))
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

    }
}