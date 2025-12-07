using Newtonsoft.Json;
using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Diagnostics;
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
        readonly string swVer = "2025-12-07";

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


        // Track progress
        private int totalQuestions = 0;
        private int processedQuestions = 0;
        private Stopwatch stopwatch = new Stopwatch();

        // Function when button is clicked
        private async void ButtonXML_Click(object sender, EventArgs e)
        {
            // Disable button and show progress UI
            ButtonGenerate.Enabled = false;
            progressBar.Visible = true;
            progressBar.Value = 0;
            labelProgress.Visible = true;
            labelProgress.Text = "";
            labelStatus.Text = "Initializing...";

            // Reset state and start timing
            errorsEncountered = false;
            logstring.Clear();
            logstring.Add("Log file for: " + config.excelFile);
            Primary_Keys.Clear();
            generatedFiles.Clear();
            totalQuestions = 0;
            processedQuestions = 0;
            stopwatch.Restart();

            // Create progress reporter for UI updates
            // processedQuestions goes 1 to 680 (340 validation + 340 generation)
            // Display as 1 to 340 by dividing by 2
            var progress = new Progress<(string worksheet, string fieldName, string phase)>(report =>
            {
                processedQuestions++;
                if (totalQuestions > 0)
                {
                    // Progress bar: 0-100% across both phases
                    int percentage = (int)((processedQuestions * 100.0) / (totalQuestions * 2));
                    progressBar.Value = Math.Min(percentage, 100);

                    // Display count: divide by 2 so it shows 1-340 once (not twice)
                    int displayedCount = (processedQuestions + 1) / 2;
                    labelProgress.Text = $"{displayedCount} / {totalQuestions}";
                }
                labelStatus.Text = $"{report.phase}: {report.worksheet} - {report.fieldName}";
            });

            try
            {
                await Task.Run(() => ProcessExcelFile(progress));

                // Stop timing
                stopwatch.Stop();
                TimeSpan elapsed = stopwatch.Elapsed;

                // Write log file
                writeLogfile();

                // Update UI to show completion
                progressBar.Value = 100;
                labelProgress.Text = $"{totalQuestions} / {totalQuestions}";

                // Show the appropriate Message Box
                if (errorsEncountered)
                {
                    labelStatus.Text = $"Completed with errors in {elapsed.TotalSeconds:F1} seconds";
                    MessageBox.Show("The Data Dictionary contains errors! \r\rThe XML files and manifest HAVE NOT not been created! \r\rPlease refer to the log file and rectify all errors.", "ERRORS FOUND", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    // Create zip file with all generated files
                    CreateZipFile();
                    labelStatus.Text = $"Complete! Processed {totalQuestions} questions in {elapsed.TotalSeconds:F1} seconds";
                    MessageBox.Show("Done Building the xml file(s) and the manifest. No errors were found. \r\rAll files have been packaged in " + config.surveyId + ".zip. \r\rPlease refer to the log file.", "SUCCESS", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Console.WriteLine("Error msg " + ex.Message);
                labelStatus.Text = $"Error occurred after {stopwatch.Elapsed.TotalSeconds:F1} seconds";
                MessageBox.Show("ERROR: There are unexpected errors with the Excel Data Dictionary!" + ex.Message, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                logstring.Add("ERROR: There are unexpected errors with the Excel Data Dictionary!" + ex.Message);
                logstring.Add("\r--------------------------------------------------------------------------------");
                logstring.Add("End of log file");
                logstring.Add("--------------------------------------------------------------------------------");
                writeLogfile();
            }
            finally
            {
                // Re-enable button but keep progress visible to show completion status
                ButtonGenerate.Enabled = true;
            }
        }

        // Process Excel file on background thread
        private void ProcessExcelFile(IProgress<(string worksheet, string fieldName, string phase)> progress)
        {
            Excel.Application xlApp = null;
            Excel.Workbook xlWorkBook = null;

            try
            {
                // Open the Excel file
                xlApp = new Excel.Application();
                xlWorkBook = xlApp.Workbooks.Open(@config.excelFile, 0, true, 5, "", "", true,
                                                  Microsoft.Office.Interop.Excel.XlPlatform.xlWindows,
                                                  "\t", false, false, 0, true, 1, 0);

                // First pass: Count total questions for progress bar
                foreach (Worksheet worksheet in xlWorkBook.Worksheets)
                {
                    if (worksheet.Name.Length >= 3 &&
                        (worksheet.Name.Substring(worksheet.Name.Length - 3) == "_dd" ||
                         (worksheet.Name.Length >= 4 && worksheet.Name.Substring(worksheet.Name.Length - 4) == "_xml")))
                    {
                        totalQuestions += ExcelReader.CountDataRows(worksheet);
                    }
                }

                // Validation pass: Read each sheet and validate
                foreach (Worksheet worksheet in xlWorkBook.Worksheets)
                {
                    if (worksheet.Name.Length >= 3 &&
                        (worksheet.Name.Substring(worksheet.Name.Length - 3) == "_dd" ||
                         (worksheet.Name.Length >= 4 && worksheet.Name.Substring(worksheet.Name.Length - 4) == "_xml")))
                    {
                        ExcelReader excelReader = new ExcelReader();
                        excelReader.CreateQuestionList(worksheet, (ws, field) =>
                        {
                            progress.Report((ws, field, "Validating"));
                        });
                        if (excelReader.errorsEncountered)
                        {
                            errorsEncountered = true;
                        }
                        logstring.AddRange(excelReader.logstring);
                    }
                }

                // Generation pass: Generate XML files if no errors
                if (!errorsEncountered)
                {
                    List<string> xmlFiles = new List<string>();
                    foreach (Worksheet worksheet in xlWorkBook.Worksheets)
                    {
                        if (worksheet.Name.Length >= 3 &&
                            (worksheet.Name.Substring(worksheet.Name.Length - 3) == "_dd" ||
                             (worksheet.Name.Length >= 4 && worksheet.Name.Substring(worksheet.Name.Length - 4) == "_xml")))
                        {
                            string xmlFileName = worksheet.Name.Replace("_dd", ".xml").Replace("_xml", ".xml");
                            xmlFiles.Add(xmlFileName);
                            ExcelReader excelReader = new ExcelReader();
                            excelReader.CreateQuestionList(worksheet, (ws, field) =>
                            {
                                progress.Report((ws, field, "Generating"));
                            });
                            QuestionList = excelReader.QuestionList;
                            // Write to the XML file
                            XmlGenerator xmlGenerator = new XmlGenerator();
                            xmlGenerator.WriteXML(worksheet.Name, QuestionList, config.outputPath);
                            logstring.AddRange(xmlGenerator.logstring);
                            // Track the generated XML file
                            generatedFiles.Add(Path.Combine(config.outputPath, xmlFileName));
                        }
                        // Process crfs worksheet
                        else if (worksheet.Name == "crfs")
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

                logstring.Add("\r--------------------------------------------------------------------------------");
                logstring.Add("End of log file");
                logstring.Add("--------------------------------------------------------------------------------");
            }
            finally
            {
                // Cleanup COM objects
                if (xlWorkBook != null)
                {
                    xlWorkBook.Close(false, null, null);
                    Marshal.ReleaseComObject(xlWorkBook);
                }
                if (xlApp != null)
                {
                    xlApp.Quit();
                    Marshal.ReleaseComObject(xlApp);
                }
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
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