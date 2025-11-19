namespace generatexml
{
    public class AppConfig
    {
        public string excelFile { get; set; }
        public string xmlPath { get; set; }
        public string logfilePath { get; set; }
        public string db_path { get; set; }
        public string sourceDatabasePath { get; set; }
        public string[] sourceTableNames { get; set; }
    }
}
