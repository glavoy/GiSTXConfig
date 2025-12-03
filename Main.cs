using Newtonsoft.Json;
using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Excel = Microsoft.Office.Interop.Excel;
using Microsoft.Office.Interop.Excel;
using GistConfigX;

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

        // List to track all generated files for zip creation
        List<string> generatedFiles = new List<string>();


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
                generatedFiles.Clear();

                // Open the Excel file
                Excel.Application xlApp;
                Excel.Workbook xlWorkBook;
                xlApp = new Excel.Application();
                xlWorkBook = xlApp.Workbooks.Open(@config.excelFile, 0, true, 5, "", "", true,
                                                  Microsoft.Office.Interop.Excel.XlPlatform.xlWindows,
                                                  "\t", false, false, 0, true, 1, 0);

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
                    List<string> xmlFiles = new List<string>();
                    foreach (Worksheet worksheet in xlWorkBook.Worksheets)
                    {
                        if (worksheet.Name.Substring(worksheet.Name.Length - 3) == "_dd" || worksheet.Name.Substring(worksheet.Name.Length - 4) == "_xml")
                        {
                            string xmlFileName = worksheet.Name.Replace("_dd", ".xml").Replace("_xml", ".xml");
                            xmlFiles.Add(xmlFileName);
                            ExcelReader excelReader = new ExcelReader();
                            excelReader.CreateQuestionList(worksheet);
                            QuestionList = excelReader.QuestionList;
                            // Write to the XML file
                            XmlGenerator xmlGenerator = new XmlGenerator();
                            xmlGenerator.WriteXML(worksheet.Name, QuestionList, config.outputPath);
                            logstring.AddRange(xmlGenerator.logstring);
                            // Track the generated XML file
                            generatedFiles.Add(Path.Combine(config.outputPath, xmlFileName));
                        }
                        // Get the primary keys for the tables
                        else
                        {
                            if (worksheet.Name == "crfs")
                            {
                                CrfReader crfReader = new CrfReader();
                                List<Crf> crfs = crfReader.ReadCrfsWorksheet(worksheet);

                                string databaseName = config.surveyId + ".sqlite";
                                SurveyManifest manifest = new SurveyManifest
                                {
                                    surveyName = config.surveyName,
                                    surveyId = config.surveyId,
                                    databaseName = databaseName,
                                    xmlFiles = xmlFiles,
                                    crfs = crfs
                                };

                                JsonGenerator jsonGenerator = new JsonGenerator();
                                string manifestPath = Path.Combine(config.outputPath, "survey_manifest.gistx");
                                jsonGenerator.Generate(manifestPath, manifest);
                                logstring.Add("");
                                logstring.Add("Successfully generated survey_manifest.gistx");
                                // Track the generated manifest file
                                generatedFiles.Add(manifestPath);
                            }
                        }
                    }
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

                // Show the appropriate Message Box
                if (errorsEncountered)
                {
                    MessageBox.Show("The Data Dictionary contains errors! \r\rThe XML files and manifest HAVE NOT not been created! \r\rPlease refer to the log file and rectify all errors.", "ERRORS FOUND", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    // Create zip file with all generated files
                    CreateZipFile();
                    MessageBox.Show("Done Building the xml file(s) and the manifest. No errors were found. \r\rAll files have been packaged in " + config.surveyId + ".zip. \r\rPlease refer to the log file.", "SUCCESS", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }



            // Error handling in caase we could not crread the Excel file
            catch (Exception ex)
            {
                Console.WriteLine("Error msg " + ex.Message);
                MessageBox.Show("ERROR: There are unexpected errors with the Excel Data Dictionary!" + ex.Message, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                string logfilePath = Path.Combine(config.outputPath, logfilename + ".txt");
                // Open a log file and start writing lines of text to it
                using (StreamWriter outputFile = new StreamWriter(logfilePath))
                {
                    foreach (string line in logstring)
                        outputFile.WriteLine(line);
                    outputFile.WriteLine("\n");
                }
                // Do NOT add log file to generatedFiles - it should remain standalone
            }
            catch (Exception ex)
            {
                MessageBox.Show("CRITICAL ERROR: Could not write to log file! Ensure path is correct." + ex.Message, "CRITICAL ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void CreateZipFile()
        {
            try
            {
                string zipFilePath = Path.Combine(config.outputPath, config.surveyId + ".zip");

                // Delete existing zip file if it exists
                if (File.Exists(zipFilePath))
                {
                    File.Delete(zipFilePath);
                }

                // Create the zip file
                using (ZipArchive archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
                {
                    // Add generated files (XML and manifest)
                    foreach (string filePath in generatedFiles)
                    {
                        if (File.Exists(filePath))
                        {
                            string entryName = Path.GetFileName(filePath);
                            archive.CreateEntryFromFile(filePath, entryName);
                            logstring.Add("Added to zip: " + entryName);
                        }
                    }

                    // Add CSV files if csvFiles path is specified
                    if (!string.IsNullOrEmpty(config.csvFiles))
                    {
                        // Normalize the path to handle both with and without trailing backslash
                        string csvPath = config.csvFiles.TrimEnd('\\', '/');

                        if (Directory.Exists(csvPath))
                        {
                            string[] csvFileList = Directory.GetFiles(csvPath, "*.csv");

                            if (csvFileList.Length > 0)
                            {
                                logstring.Add("");
                                logstring.Add("Adding CSV files to package:");

                                foreach (string csvFile in csvFileList)
                                {
                                    string entryName = Path.GetFileName(csvFile);
                                    archive.CreateEntryFromFile(csvFile, entryName);
                                    logstring.Add("Added to zip: " + entryName);
                                }
                            }
                            else
                            {
                                logstring.Add("WARNING: No CSV files found in " + csvPath);
                            }
                        }
                        else
                        {
                            logstring.Add("WARNING: CSV files directory not found: " + csvPath);
                        }
                    }
                }

                logstring.Add("");
                logstring.Add("Successfully created zip file: " + zipFilePath);

                // Delete individual files after successful zip creation
                foreach (string filePath in generatedFiles)
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        logstring.Add("Deleted temporary file: " + Path.GetFileName(filePath));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("ERROR: Could not create zip file! " + ex.Message, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                logstring.Add("ERROR: Could not create zip file! " + ex.Message);
            }
        }

    }
}